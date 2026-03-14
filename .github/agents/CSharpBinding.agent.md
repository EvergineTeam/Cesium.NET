# AGENTS.md

## Project Overview

This repository provides tooling and agent instructions for generating **C# P/Invoke binding libraries** from C header files (`.h`). The generated bindings follow the Evergine low-level binding pattern used across multiple projects:

- [RenderDoc.NET](https://github.com/EvergineTeam/RenderDoc.NET) — bindings for RenderDoc
- [WebGPU.NET](https://github.com/EvergineTeam/WebGPU.NET) — bindings for WebGPU
- [Meshoptimizer.NET](https://github.com/EvergineTeam/Meshoptimizer.NET) — bindings for meshoptimizer
- [XAtlas.NET](https://github.com/EvergineTeam/XAtlas.NET) — bindings for xatlas

**Target framework**: .NET 8.0  
**Key dependency**: [CppAst](https://www.nuget.org/packages/CppAst/) (v0.21.1) — a C/C++ header parser for .NET  
**Language**: C# with `unsafe` code and raw pointer P/Invoke  
**Pattern**: Console app parses `.h` → generates `.cs` files → compiled into a NuGet package

---

## Repository Structure Convention

When creating a new binding library (e.g., for a library called `FooLib`), follow this exact directory layout:

```
FooLibGen/                              # Root solution folder
├── FooLibGen.sln                       # Solution file (all projects below)
├── README.md                           # Project README (badges, purpose, platforms)
├── LICENSE                             # License file
├── .github/
│   └── workflows/
│       ├── CI.yml                      # Continuous integration workflow
│       ├── CD.yml                      # Continuous delivery / NuGet publish workflow
│       └── sync-standards.yml          # Evergine standards sync workflow
├── FooLibGen/                          # Generator console app
│   ├── FooLibGen.csproj                # .NET 8.0 console app with CppAst dependency
│   ├── Program.cs                      # Entry point: parse header, call generator
│   ├── CsCodeGenerator.cs             # Main code generation logic (singleton)
│   ├── Helpers.cs                      # Type conversion, marshalling, comment helpers
│   └── Headers/
│       └── foolib.h                    # Original C header file (copied to output)
├── Evergine.Bindings.FooLib/           # Output binding class library
│   ├── Evergine.Bindings.FooLib.csproj # .NET 8.0 library, AllowUnsafeBlocks=true
│   ├── Generated/                      # Auto-generated .cs files (output target)
│   │   ├── Enums.cs
│   │   ├── Structs.cs
│   │   ├── Functions.cs               # (or Funtions.cs — keep consistent)
│   │   ├── Constants.cs               # (if macros/defines exist)
│   │   ├── Delegates.cs               # (if function pointer typedefs exist)
│   │   └── Handles.cs                 # (if opaque handle typedefs exist)
│   └── runtimes/                       # Native shared libraries per platform
│       ├── win-x64/native/
│       ├── win-arm64/native/
│       ├── linux-x64/native/
│       ├── linux-arm64/native/
│       └── osx-arm64/native/
└── Test/  (or BasicTest/ or HelloXxx/) # Optional test/example console app
    ├── Test.csproj
    └── Program.cs
```

---

## Creating a New Binding Library — Step by Step

### 1. Obtain the C Header File

- Get the public C API header (`.h`) from the target library's repository or release.
- If the library only exposes a C++ API, you need a C-compatible wrapper header (a `_c.h` shim with `extern "C"` functions). See XAtlas for this pattern (`xatlas_c.h`).
- Place the header in `FooLibGen/Headers/foolib.h`.

### 2. Create the Generator Console App (`FooLibGen.csproj`)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <!-- Workaround for https://github.com/microsoft/ClangSharp/issues/129 -->
    <RuntimeIdentifier Condition="'$(RuntimeIdentifier)' == '' AND '$(PackAsTool)' != 'true'">$(NETCoreSdkRuntimeIdentifier)</RuntimeIdentifier>
  </PropertyGroup>

  <ItemGroup>
    <Content Include="Headers\foolib.h">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="CppAst" Version="0.21.1" />
  </ItemGroup>
</Project>
```

### 3. Write `Program.cs` (Entry Point)

```csharp
using CppAst;
using System;
using System.Diagnostics;
using System.IO;

namespace FooLibGen
{
    class Program
    {
        static void Main(string[] args)
        {
            var headerFile = Path.Combine(AppContext.BaseDirectory, "Headers", "foolib.h");
            var options = new CppParserOptions
            {
                ParseMacros = true,
                // Add Defines if the header requires platform macros:
                // Defines = { "_WIN32" }
            };

            var compilation = CppParser.ParseFile(headerFile, options);

            if (compilation.HasErrors)
            {
                foreach (var message in compilation.Diagnostics.Messages)
                {
                    Debug.WriteLine(message);
                }
            }
            else
            {
                string outputPath = Path.Combine(
                    "..", "..", "..", "..", "..",
                    "Evergine.Bindings.FooLib", "Generated");

                if (!Directory.Exists(outputPath))
                    Directory.CreateDirectory(outputPath);

                CsCodeGenerator.Instance.Generate(compilation, outputPath);
            }
        }
    }
}
```

### 4. Write `CsCodeGenerator.cs` (Core Generator)

This singleton class produces one `.cs` file per C construct category. Call each `Generate*` method in order:

```csharp
public void Generate(CppCompilation compilation, string outputPath)
{
    // 1. Pre-collect typedef lists for opaque handle detection
    Helpers.TypedefList = compilation.Typedefs
        .Where(t => t.TypeKind == CppTypeKind.Typedef
               && t.ElementType is CppPointerType
               && ((CppPointerType)t.ElementType).ElementType.TypeKind != CppTypeKind.Function)
        .Select(t => t.Name).ToList();

    // 2. Generate each file
    GenerateConstants(compilation, outputPath);   // #define macros → const fields
    GenerateEnums(compilation, outputPath);        // C enums → C# enums
    GenerateDelegates(compilation, outputPath);    // Function pointer typedefs → C# delegates
    GenerateStructs(compilation, outputPath);      // C structs/unions → C# structs
    GenerateFunctions(compilation, outputPath);    // C functions → [DllImport] static extern methods
    // GenerateHandles(compilation, outputPath);   // Opaque pointer typedefs → wrapper structs (optional)
}
```

Each `Generate*` method follows the same pattern:
1. Open a `StreamWriter` to `Path.Combine(outputPath, "FileName.cs")`
2. Write `using` statements, namespace declaration, class/struct declaration
3. Iterate over the relevant `CppCompilation` collection
4. Use `Helpers` to convert types, marshal, and emit comments

### 5. Write `Helpers.cs` (Type Conversion Engine)

The `Helpers` class is the most critical piece. It handles:

#### Type Mapping Dictionary

Map C types to C# equivalents:

```csharp
private static readonly Dictionary<string, string> csNameMappings = new()
{
    { "bool", "bool" },         // or "byte" if bool size matters
    { "uint8_t", "byte" },
    { "uint16_t", "ushort" },
    { "uint32_t", "uint" },
    { "uint64_t", "ulong" },
    { "int8_t", "sbyte" },
    { "int16_t", "short" },
    { "int32_t", "int" },
    { "int64_t", "long" },
    { "char", "byte" },
    { "size_t", "nuint" },      // or "UIntPtr" or "uint" depending on needs
    { "intptr_t", "nint" },
    { "uintptr_t", "nuint" },
};
```

#### `ConvertToCSharpType(CppType type, bool isPointer = false)`

Recursive method that handles the full CppAst type hierarchy:
- `CppPrimitiveType` → map via switch on `CppPrimitiveKind`
- `CppQualifiedType` → unwrap `const` qualifier, recurse on `ElementType`
- `CppEnum` → use enum name (with `GetCsCleanName`)
- `CppTypedef` → resolve to mapped name or `IntPtr` for opaque handles
- `CppClass` → use struct name directly
- `CppPointerType` → recurse and append `*`
- `CppArrayType` → recurse on element type

#### `ShowAsMarshalType(string type, Family family)`

Converts types for different contexts (parameter, field, return value):
- `bool` param → `[MarshalAs(UnmanagedType.Bool)] bool`
- `bool` field → `byte` (for blittable struct layout)
- `char*` param → `[MarshalAs(UnmanagedType.LPStr)] string`
- `char*` field → `byte*`

#### `GetCsCleanName(string name)`

Resolves typedef names:
- Names starting with `PFN` → `IntPtr` (function pointer typedefs)
- Names containing `Flags` → strip the `Flags` suffix (enum flag pattern)
- Names in `TypedefList` (opaque handles) → `IntPtr`
- Names in `csNameMappings` → recursively resolve

#### `PrintComments(StreamWriter, CppComment, tabs, newLine)`

Emits XML doc comments (`/// <summary>`) from C header comments.

#### `EscapeReservedKeyword(string name)`

Prefix C# reserved keywords with `@` (e.g., `@event`, `@object`).

---

## Generated Code Patterns

### Constants (`Constants.cs`)

```csharp
namespace Evergine.Bindings.FooLib
{
    public static partial class FooLibNative
    {
        public const uint FOOLIB_MAX_SIZE = 1024;
        public const uint FOOLIB_VERSION = 3;
    }
}
```

Source: `compilation.Macros` — skip empty values and internal macros.

### Enums (`Enums.cs`)

```csharp
namespace Evergine.Bindings.FooLib
{
    /// <summary>
    /// Description from header comment
    /// </summary>
    [Flags]  // Only if a typedef named EnumNameFlags exists
    public enum FooLibStatus
    {
        Success = 0,
        Error = 1,
        InvalidParam = 2,
    }
}
```

Source: `compilation.Enums` — filter `e.Items.Count > 0 && !e.IsAnonymous`.  
Detect `[Flags]` by checking `compilation.Typedefs.Any(t => t.Name == enumName + "Flags")`.

### Delegates (`Delegates.cs`)

```csharp
namespace Evergine.Bindings.FooLib
{
    public unsafe delegate void FooLibCallback(
         uint status,
         void* userData);
}
```

Source: Typedefs where `ElementType` is `CppPointerType` and its inner `ElementType.TypeKind == CppTypeKind.Function`.

### Structs (`Structs.cs`)

```csharp
namespace Evergine.Bindings.FooLib
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct FooLibConfig
    {
        public uint maxSize;
        public float threshold;
        public fixed byte name[64];     // Fixed-size array
    }

    [StructLayout(LayoutKind.Explicit)]   // For C unions
    public unsafe struct FooLibValue
    {
        [FieldOffset(0)] public int intValue;
        [FieldOffset(0)] public float floatValue;
    }
}
```

Source: `compilation.Classes` — filter `ClassKind == Struct && IsDefinition == true`.  
Use `LayoutKind.Explicit` + `[FieldOffset(0)]` for unions.  
Use `fixed` keyword for `CppArrayType` fields.  
Handle anonymous unions inside structs by flattening (take last field).

### Functions (`Functions.cs`)

```csharp
namespace Evergine.Bindings.FooLib
{
    public static unsafe partial class FooLibNative
    {
        /// <summary>
        /// Initialize the library.
        /// </summary>
        [DllImport("foolib", CallingConvention = CallingConvention.Cdecl)]
        public static extern uint foolib_init(FooLibConfig* config);

        [DllImport("foolib", CallingConvention = CallingConvention.Cdecl)]
        public static extern void foolib_destroy(void* handle);
    }
}
```

Source: `compilation.Functions` — skip `FunctionTemplate` and `Inline` functions.  
DllImport library name must match the native binary filename (without extension).  
Always use `CallingConvention.Cdecl`.

### Handles (`Handles.cs`) — Optional

For opaque pointer typedefs (e.g., `typedef struct FooObj_T* FooObj;`), generate type-safe wrapper structs:

```csharp
public partial struct FooObj : IEquatable<FooObj>
{
    public readonly IntPtr Handle;
    public FooObj(IntPtr existingHandle) { Handle = existingHandle; }
    public static FooObj Null => new FooObj(IntPtr.Zero);
    public static implicit operator FooObj(IntPtr handle) => new FooObj(handle);
    public static bool operator ==(FooObj left, FooObj right) => left.Handle == right.Handle;
    public static bool operator !=(FooObj left, FooObj right) => left.Handle != right.Handle;
    public bool Equals(FooObj h) => Handle == h.Handle;
    public override bool Equals(object o) => o is FooObj h && Equals(h);
    public override int GetHashCode() => Handle.GetHashCode();
}
```

Source: Typedefs where `ElementType is CppPointerType` and inner type is NOT a function.

---

## Binding Library Project (`Evergine.Bindings.FooLib.csproj`)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AllowUnsafeBlocks>True</AllowUnsafeBlocks>
    <Copyright>Copyright (c) Evergine 2025</Copyright>
    <Authors>Evergine Team</Authors>
    <Company>Plain Concepts</Company>
    <Description>Low-level bindings for FooLib used in Evergine</Description>
    <RepositoryUrl>https://github.com/EvergineTeam/FooLib.NET</RepositoryUrl>
    <PackageIcon>icon.png</PackageIcon>
    <PackageReadmeFile>README.md</PackageReadmeFile>
  </PropertyGroup>

  <ItemGroup>
    <Folder Include="Generated\" />
  </ItemGroup>

  <ItemGroup>
    <Content Include="runtimes\**" PackagePath="runtimes"
             Visible="true" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

  <ItemGroup>
    <None Include="..\..\icon.png" Pack="true" PackagePath="\" Visible="false" />
    <None Include="..\..\README.md" Pack="true" PackagePath="\" Visible="false" />
  </ItemGroup>
</Project>
```

### Native Library Placement

Place pre-built native shared libraries under `runtimes/`:

```
runtimes/
  win-x64/native/foolib.dll
  win-arm64/native/foolib.dll
  linux-x64/native/libfoolib.so
  linux-arm64/native/libfoolib.so
  osx-arm64/native/libfoolib.dylib
```

The `[DllImport("foolib")]` name must match the library filename (without `lib` prefix on Linux/macOS and without extension).

---

## Setup Commands

```bash
# Restore and build the entire solution
cd FooLibGen
dotnet restore FooLibGen.sln
dotnet build FooLibGen.sln

# Run the generator to produce C# bindings
dotnet run --project FooLibGen/FooLibGen.csproj

# Build only the binding library
dotnet build Evergine.Bindings.FooLib/Evergine.Bindings.FooLib.csproj

# Pack for NuGet
dotnet pack Evergine.Bindings.FooLib/Evergine.Bindings.FooLib.csproj -c Release
```

---

## Development Workflow

1. **Place the C header** in `FooLibGen/Headers/foolib.h`
2. **Run the generator**: `dotnet run --project FooLibGen/FooLibGen.csproj`
3. **Verify generated files** in `Evergine.Bindings.FooLib/Generated/`
4. **Build the binding**: `dotnet build Evergine.Bindings.FooLib/`
5. **Run tests**: `dotnet run --project Test/Test.csproj`
6. To update bindings after a header change, repeat from step 1

---

## Testing Instructions

- Build the solution to verify generated code compiles: `dotnet build FooLibGen.sln`
- Run the test/example project if present: `dotnet run --project Test/Test.csproj`
- Verify no compiler warnings related to missing P/Invoke signatures
- Ensure `AllowUnsafeBlocks` is enabled in the binding project
- Check that fixed-size arrays use the `fixed` keyword correctly
- Verify bool fields in structs use `byte` (not `bool`) for blittable layout, or use `[MarshalAs(UnmanagedType.I1)]`

---

## Code Style Guidelines

- **Base namespace**: `Evergine.Bindings.{LibraryName}` (PascalCase)
- **Per-header sub-namespaces**: when the API is split across multiple header files, each header maps to a sub-namespace, e.g. `cesium_geospatial.h` → `Evergine.Bindings.CesiumNative.Geospatial`. The `GetNamespaceForFile(filePath)` helper in `CsCodeGenerator` drives this mapping.
- **Cross-namespace `using` statements**: generated files that span multiple namespaces add `using` directives for all sub-namespaces at the top of the file so types resolve across namespace boundaries without requiring the consumer to add extra `using` statements.
- **Native class**: `{LibraryName}Native` — static partial class holding `[DllImport]` methods and constants; when functions are split by header, each namespace gets its own `partial class` block with the same class name.
- **Keep original C names** for functions, struct fields, and enum members (do not rename to PascalCase)
- **Use `unsafe`** for all pointer types — never wrap in `IntPtr` unless it's an opaque handle
- **Use `partial` classes** so hand-written extensions can coexist with generated code
- **One file per category**: `Enums.cs`, `Structs.cs`, `Functions.cs`, `Constants.cs`, `Delegates.cs`, `Handles.cs` — each file may contain multiple namespace blocks when types come from different headers
- **XML doc comments** are auto-generated from C header comments using `/// <summary>` format
- **Tabs for indentation** (one tab per indentation level, matching the existing codebases)

---

## CppAst Parsing Reference

The generator uses [CppAst](https://github.com/xoofx/CppAst.NET) to parse C headers. Key types from the parsed `CppCompilation`:

| CppAst Collection | C Construct | Generated Output |
|---|---|---|
| `compilation.Macros` | `#define NAME value` | `public const` fields |
| `compilation.Enums` | `enum { ... }` | `public enum` types |
| `compilation.Classes` | `struct { ... }` / `union { ... }` | `public struct` with `[StructLayout]` |
| `compilation.Functions` | `returntype name(params)` | `[DllImport] static extern` methods |
| `compilation.Typedefs` | `typedef ... name` | Delegates, handles, or type aliases |

### CppParserOptions Guidance

```csharp
var options = new CppParserOptions
{
    ParseMacros = true,           // Always true — needed for constants
    Defines = { "_WIN32" },       // Add platform defines if header needs them
};
```

---

## Common Issues and Troubleshooting

### `RuntimeIdentifier` build error
The `.csproj` needs the ClangSharp workaround:
```xml
<RuntimeIdentifier Condition="'$(RuntimeIdentifier)' == '' AND '$(PackAsTool)' != 'true'">$(NETCoreSdkRuntimeIdentifier)</RuntimeIdentifier>
```

### Bool marshalling in structs
C `bool` is 1 byte in some ABIs, 4 bytes in others. For struct fields use `byte` or `[MarshalAs(UnmanagedType.I1)] bool`. For function parameters use `[MarshalAs(UnmanagedType.Bool)] bool`.

### `size_t` mapping
`size_t` varies by platform (4 bytes on 32-bit, 8 bytes on 64-bit). Map to `nuint` (or `UIntPtr`) for correctness. Some projects use `uint` as a simplification when only targeting 64-bit.

### Inline / Template functions
Skip functions with `CppFunctionFlags.Inline` or `CppFunctionFlags.FunctionTemplate` — they have no exported symbol.

### Anonymous enums
Filter out with `!e.IsAnonymous` — anonymous enums often contain standalone constants that should go in `Constants.cs` instead.

### Opaque handles vs function pointers
Both are `CppTypedef` pointing to `CppPointerType`. Distinguish by checking if the inner type is `CppTypeKind.Function`:
- Function → generate delegate
- Non-function → generate handle struct or map to `IntPtr`

### C++ only headers
If the library only provides a C++ header, you need to create a C-compatible wrapper (`extern "C"`) header exposing the API. See XAtlas.NET's `xatlas_c.h` for an example.

---

## README File

Every binding repository must include a `README.md` at the root. Follow the structure from [Meshoptimizer.NET](https://github.com/EvergineTeam/Meshoptimizer.NET/blob/main/README.md):

```markdown
# FooLib.NET

This repository contains low-level bindings for [FooLib](https://github.com/upstream/foolib) used in [Evergine](https://evergine.com/).
This binding is generated from the FooLib release:
[https://github.com/upstream/foolib/releases/tag/vX.Y](https://github.com/upstream/foolib/releases/tag/vX.Y)

[![CI](https://github.com/EvergineTeam/FooLib.NET/actions/workflows/CI.yml/badge.svg)](https://github.com/EvergineTeam/FooLib.NET/actions/workflows/CI.yml)
[![CD](https://github.com/EvergineTeam/FooLib.NET/actions/workflows/CD.yml/badge.svg)](https://github.com/EvergineTeam/FooLib.NET/actions/workflows/CD.yml)
[![Nuget](https://img.shields.io/nuget/v/Evergine.Bindings.FooLib?logo=nuget)](https://www.nuget.org/packages/Evergine.Bindings.FooLib)

## Purpose

Brief description of what the native library does and why .NET bindings are useful.
Link to the original upstream repository for more details.

## Features

- **Feature 1** — short description
- **Feature 2** — short description

## Supported Platforms

- [x] Windows x64, ARM64
- [x] Linux x64, ARM64
- [x] MacOS ARM64

## Related Evergine Bindings

- [WebGPU.NET](https://github.com/EvergineTeam/WebGPU.NET) — Bindings for WebGPU
- [Meshoptimizer.NET](https://github.com/EvergineTeam/Meshoptimizer.NET) — Bindings for meshoptimizer
- [RenderDoc.NET](https://github.com/EvergineTeam/RenderDoc.NET) — Bindings for RenderDoc
- [XAtlas.NET](https://github.com/EvergineTeam/XAtlas.NET) — Bindings for xatlas
```

Key points:
- Include **CI, CD, and NuGet** badge links at the top
- The **Purpose** section explains the native library and links upstream
- **Features** lists the main capabilities of the binding
- **Supported Platforms** uses checkboxes for the target RIDs
- **Related Evergine Bindings** links to sibling binding repositories

---

## CI/CD Pipeline & GitHub Actions Workflows

Every binding repository must include three workflow files under `.github/workflows/`.
All workflows use shared reusable workflows from the `EvergineTeam/evergine-standards` repository.

### CI.yml — Continuous Integration

Triggers on push/PR to `main` and manual dispatch. Builds the generator, runs it, and builds the binding library.

```yaml
# Binding CI - Simple Template
name: CI

on:
  workflow_dispatch:
    inputs:
      publish-artifacts:
        description: 'Publish artifacts'
        required: false
        type: boolean
        default: false
  push:
    branches: [ "main" ]
  pull_request:
    branches: [ "main" ]

jobs:
  ci:
    uses: EvergineTeam/evergine-standards/.github/workflows/binding-common-ci.yml@v2
    with:
      generator-project: "FooLibGen/FooLibGen.csproj"      # Path to generator .csproj
      generator-name: "FooLibGen"                           # Generator executable name
      binding-project: "Evergine.Bindings.FooLib/Evergine.Bindings.FooLib.csproj"
      target-framework: "net8.0"
      dotnet-version: "8.x"
      runtime-identifier: "linux-x64"
      build-configuration: "Release"
      nuget-artifacts: ${{ inputs.publish-artifacts || false }}
      revision: ${{ github.run_number }}
```

### CD.yml — Continuous Delivery / NuGet Publish

Manual dispatch only. Builds, packs, and publishes the NuGet package to nuget.org.

```yaml
# Binding Simple CD - Template
name: CD

on:
  workflow_dispatch:
    inputs:
      skip-assets-publishing:
        description: 'Skip assets publishing'
        required: false
        type: boolean
        default: false

jobs:
  cd:
    if: github.event_name != 'schedule' || github.ref == 'refs/heads/main'
    uses: EvergineTeam/evergine-standards/.github/workflows/binding-simple-cd.yml@v2
    with:
      generator-project: "FooLibGen/FooLibGen.csproj"
      generator-name: "FooLibGen"
      binding-project: "Evergine.Bindings.FooLib/Evergine.Bindings.FooLib.csproj"
      target-framework: "net8.0"
      dotnet-version: "8.x"
      nuget-version: "6.x"
      runtime-identifier: "linux-x64"
      build-configuration: "Release"
      revision: ${{ github.run_number }}
      publish-enabled: ${{ !inputs.skip-assets-publishing }}
      enable-email-notifications: true
    secrets:
      NUGET_UPLOAD_TOKEN: ${{ secrets.EVERGINE_NUGETORG_TOKEN }}
      WAVE_SENDGRID_TOKEN: ${{ secrets.WAVE_SENDGRID_TOKEN }}
      EVERGINE_EMAILREPORT_LIST: ${{ secrets.EVERGINE_EMAILREPORT_LIST }}
      EVERGINE_EMAIL: ${{ secrets.EVERGINE_EMAIL }}
```

### sync-standards.yml — Standards Sync

Runs monthly (1st of each month at 01:00 UTC) or on manual dispatch. Syncs standard configuration files from the shared `evergine-standards` repository.

```yaml
name: Sync standards

on:
  workflow_dispatch:
    inputs:
      org:
        description: "Standards org"
        default: "EvergineTeam"
        required: false
      repo:
        description: "Standards repo"
        default: "evergine-standards"
        required: false
      ref:
        description: "Branch/tag/commit of standards"
        default: "v2"
        required: false
      target_branch:
        description: "Target branch to apply changes"
        default: "main"
        required: false
      script_path:
        description: "Path where to download the sync script"
        default: "sync-standards.ps1"
        required: false
      commit_message:
        description: "Commit message"
        required: false
      mode:
        description: "auto (push then PR if needed) or pr (always PR)"
        default: "auto"
        required: false
      dry_run:
        description: "Dry run mode - show what would be done without making changes"
        type: boolean
        default: false
        required: false

  schedule:
    - cron: "0 1 1 * *"

jobs:
  sync:
    if: github.event_name != 'schedule' || github.ref == 'refs/heads/main'
    permissions:
      contents: write
      pull-requests: write
    uses: EvergineTeam/evergine-standards/.github/workflows/_sync-standards-reusable.yml@v2
    with:
      org:  ${{ github.event.inputs.org  || 'EvergineTeam' }}
      repo: ${{ github.event.inputs.repo || 'evergine-standards' }}
      ref:  ${{ github.event.inputs.ref  || 'main' }}
      target_branch: ${{ github.event.inputs.target_branch || 'main' }}
      script_path: ${{ github.event.inputs.script_path || 'sync-standards.ps1' }}
      commit_message: "${{ github.event.inputs.commit_message || vars.STANDARDS_COMMIT_MESSAGE || 'auto: sync standard files [skip ci]' }}"
      mode: ${{ github.event.inputs.mode || 'auto' }}
      dry_run: ${{ github.event.inputs.dry_run == 'true' }}
    secrets: inherit
```

### Customization Notes

- Replace `FooLibGen` / `Evergine.Bindings.FooLib` with the actual project names
- The `generator-project` and `binding-project` paths are relative to the repository root
- The `generator-name` is the executable name (without `.exe`)
- `runtime-identifier` should be `linux-x64` (the CI runner platform)
- The `sync-standards.yml` file is **identical** across all binding repositories — copy it as-is
- Required repository secrets: `EVERGINE_NUGETORG_TOKEN`, `WAVE_SENDGRID_TOKEN`, `EVERGINE_EMAILREPORT_LIST`, `EVERGINE_EMAIL`

---

## Adapting For a New Library — Checklist

- [ ] Identify the C header file (pure C API, not C++)
- [ ] Create solution directory structure per convention above
- [ ] Create `FooLibGen.csproj` with CppAst dependency
- [ ] Write `Program.cs` — set header path and parser options (`Defines` if needed)
- [ ] Write `Helpers.cs` — copy from reference project, adjust type mappings for library-specific typedefs
- [ ] Write `CsCodeGenerator.cs` — implement generators per construct type:
  - [ ] Constants (from `compilation.Macros`) — skip empty/internal macros
  - [ ] Enums (from `compilation.Enums`) — detect `[Flags]` via `*Flags` typedef
  - [ ] Delegates (from `compilation.Typedefs` with function pointers)
  - [ ] Structs (from `compilation.Classes`) — handle unions, fixed arrays, anonymous unions
  - [ ] Functions (from `compilation.Functions`) — skip inline/template, set correct DllImport name
  - [ ] Handles (optional, from non-function pointer typedefs)
- [ ] Create `Evergine.Bindings.FooLib.csproj` with `AllowUnsafeBlocks`
- [ ] Place native libraries in `runtimes/` per platform
- [ ] Run generator and verify the output compiles
- [ ] Create a test project to validate basic functionality
- [ ] Create `README.md` with badges (CI/CD/NuGet), purpose, features, platforms, related bindings
- [ ] Create `.github/workflows/CI.yml` — adapt paths to generator and binding projects
- [ ] Create `.github/workflows/CD.yml` — adapt paths, configure NuGet publish secrets
- [ ] Create `.github/workflows/sync-standards.yml` — copy as-is from template
