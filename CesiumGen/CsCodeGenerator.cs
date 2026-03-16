using CppAst;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CesiumGen
{
	// =====================================================================
	// Function classification model
	// =====================================================================

	public enum FunctionRole
	{
		Constructor,
		Destructor,
		PropertyGetter,
		PropertySetter,
		InstanceMethod,
		StaticMethod,
		GlobalFunction,
	}

	public class FunctionInfo
	{
		public CppFunction CppFunction { get; set; }
		public FunctionRole Role { get; set; }

		/// <summary>The opaque handle type or value struct this function belongs to, or null for global.</summary>
		public string OwnerType { get; set; }

		/// <summary>The cleaned C# method name (PascalCase, owner prefix stripped).</summary>
		public string MethodName { get; set; }

		/// <summary>For property getters/setters, the property name (PascalCase).</summary>
		public string PropertyName { get; set; }

		/// <summary>The snake_case portion after stripping the owner prefix.</summary>
		public string StrippedSnakeName { get; set; }

		/// <summary>Whether this function returns a boolean (int that represents true/false).</summary>
		public bool IsBoolReturn { get; set; }

		/// <summary>Whether this function returns a string (const char*).</summary>
		public bool IsStringReturn { get; set; }

		/// <summary>Whether the property this getter/setter belongs to is boolean.</summary>
		public bool IsBoolProperty { get; set; }
	}

	public class PropertyInfo
	{
		public string Name { get; set; }
		public FunctionInfo Getter { get; set; }
		public FunctionInfo Setter { get; set; }
		public bool IsBool { get; set; }
		public bool IsString { get; set; }
		public string CSharpType { get; set; }
	}

	public class CsCodeGenerator
	{
		public static readonly CsCodeGenerator Instance = new CsCodeGenerator();

		private const string BaseNamespace = "Evergine.Bindings.CesiumNative";
		private const string DllName = "CesiumNativeC";
		private const string NativeClass = "CesiumAPI";

		private List<(string Name, string File)> _opaqueHandles = new();
		private List<FunctionInfo> _analyzedFunctions = new();

		// Handle types that are borrowed (no destroy function) — do NOT get IDisposable
		private HashSet<string> _borrowedHandleTypes = new();
		// Handle types that have a destroy function — get IDisposable
		private HashSet<string> _ownableHandleTypes = new();

		private static readonly string[] SubNamespaceUsings = new[]
		{
			"Evergine.Bindings.CesiumNative.Common",
			"Evergine.Bindings.CesiumNative.Geospatial",
			"Evergine.Bindings.CesiumNative.Gltf",
			"Evergine.Bindings.CesiumNative.Ion",
			"Evergine.Bindings.CesiumNative.RasterOverlays",
			"Evergine.Bindings.CesiumNative.Tileset",
		};

		private string GetNamespaceForFile(string filePath)
		{
			var fileName = Path.GetFileNameWithoutExtension(filePath ?? "").ToLowerInvariant();
			return fileName switch
			{
				"cesium_common" => $"{BaseNamespace}.Common",
				"cesium_geospatial" => $"{BaseNamespace}.Geospatial",
				"cesium_gltf" => $"{BaseNamespace}.Gltf",
				"cesium_ion" => $"{BaseNamespace}.Ion",
				"cesium_raster_overlays" => $"{BaseNamespace}.RasterOverlays",
				"cesium_tileset" => $"{BaseNamespace}.Tileset",
				_ => BaseNamespace,
			};
		}

		public void Generate(CppCompilation compilation, string outputPath)
		{
			// Collect defined struct names
			var definedStructNames = compilation.Classes
				.Where(c => c.ClassKind == CppClassKind.Struct && c.IsDefinition)
				.Select(c => c.Name)
				.ToHashSet();

			Helpers.DefinedStructNames = definedStructNames.ToList();

			// Collect opaque handle types (forward-declared structs with no definition)
			_opaqueHandles = compilation.Classes
				.Where(c => c.ClassKind == CppClassKind.Struct
					&& !c.IsDefinition
					&& !string.IsNullOrEmpty(c.Name)
					&& !definedStructNames.Contains(c.Name))
				.GroupBy(c => c.Name)
				.Select(g => (Name: g.Key, File: g.First().Span.Start.File))
				.ToList();

			Helpers.OpaqueHandleTypes = _opaqueHandles.Select(h => h.Name).ToList();

			// Collect delegate names (function pointer typedefs)
			Helpers.DelegateNames = compilation.Typedefs
				.Where(t => IsFunctionPointerTypedef(t))
				.Select(t => t.Name)
				.ToList();

			Console.WriteLine($"Found {_opaqueHandles.Count} opaque handle types:");
			foreach (var h in _opaqueHandles)
				Console.WriteLine($"  {h.Name} ({Path.GetFileName(h.File)})");

			Console.WriteLine($"Found {Helpers.DelegateNames.Count} delegates:");
			foreach (var d in Helpers.DelegateNames)
				Console.WriteLine($"  {d}");

			// Analyze functions before generating
			var functions = compilation.Functions
				.Where(f => !f.Flags.HasFlag(CppFunctionFlags.Inline)
					&& !f.Flags.HasFlag(CppFunctionFlags.FunctionTemplate))
				.ToList();

			_analyzedFunctions = AnalyzeFunctions(functions, definedStructNames);

			Console.WriteLine($"Analyzed {_analyzedFunctions.Count} functions:");
			foreach (var role in Enum.GetValues(typeof(FunctionRole)).Cast<FunctionRole>())
			{
				var count = _analyzedFunctions.Count(f => f.Role == role);
				if (count > 0) Console.WriteLine($"  {role}: {count}");
			}

			GenerateEnums(compilation, outputPath);
			GenerateDelegates(compilation, outputPath);
			GenerateStructs(compilation, outputPath);
			GenerateFunctions(compilation, outputPath);
			GenerateHandles(outputPath);
			GenerateStructMethods(outputPath);
		}

		private bool IsFunctionPointerTypedef(CppTypedef typedef)
		{
			var elementType = typedef.ElementType;
			if (elementType is CppPointerType pt)
				return pt.ElementType is CppFunctionType;
			return false;
		}

		// =====================================================================
		// Function Analysis
		// =====================================================================

		private List<FunctionInfo> AnalyzeFunctions(List<CppFunction> functions, HashSet<string> definedStructNames)
		{
			var result = new List<FunctionInfo>();

			// Phase 1: Derive C function prefixes from actual function names
			// (solves naming mismatches like CesiumCGltfReader → cesium_gltf_reader_)
			var prefixMap = DeriveTypePrefixes(functions, definedStructNames);

			Console.WriteLine("Derived type prefixes:");
			foreach (var kv in prefixMap.OrderBy(kv => kv.Key))
				Console.WriteLine($"  {kv.Key} → \"{kv.Value}\"");

			// Sort prefixes by length descending so longest match wins
			var sortedPrefixes = prefixMap
				.OrderByDescending(kv => kv.Value.Length)
				.ToList();

			// Track which handle types have destroy functions
			_ownableHandleTypes = new HashSet<string>();
			_borrowedHandleTypes = new HashSet<string>();

			// Phase 2: Classify each function using prefix matching
			foreach (var f in functions)
			{
				var info = new FunctionInfo { CppFunction = f };
				var cName = f.Name;

				// Find the longest matching prefix
				string ownerType = null;
				string stripped = null;

				foreach (var kv in sortedPrefixes)
				{
					if (cName.StartsWith(kv.Value))
					{
						ownerType = kv.Key;
						stripped = cName.Substring(kv.Value.Length);
						break;
					}
				}

				info.OwnerType = ownerType;
				info.StrippedSnakeName = stripped ?? cName;
				info.IsStringReturn = Helpers.IsConstCharPointerReturn(f.ReturnType);

				if (ownerType == null)
				{
					// Phase 3: Unmatched function — check if it returns a handle type (factory)
					var returnHandleType = GetReturnedHandleType(f);
					if (returnHandleType != null)
					{
						info.OwnerType = returnHandleType;
						info.Role = FunctionRole.Constructor;
						// Derive method name: strip "cesium_" prefix and convert to PascalCase
						info.MethodName = Helpers.ClearFunctionName(cName);
						// Try to strip the handle prefix for a cleaner name
						var handlePrefix = prefixMap.GetValueOrDefault(returnHandleType);
						if (handlePrefix != null && cName.StartsWith(handlePrefix))
							info.MethodName = Helpers.SnakeToPascalCase(cName.Substring(handlePrefix.Length));
					}
					else
					{
						info.Role = FunctionRole.GlobalFunction;
						info.MethodName = Helpers.ClearFunctionName(cName);
					}
				}
				else
				{
					bool isHandleType = Helpers.OpaqueHandleTypes.Contains(ownerType);

					if (stripped == "destroy")
					{
						info.Role = FunctionRole.Destructor;
						info.MethodName = "Destroy";
						if (isHandleType) _ownableHandleTypes.Add(ownerType);
					}
					else if (stripped.StartsWith("create") || IsFactoryFunction(f, ownerType))
					{
						info.Role = FunctionRole.Constructor;
						info.MethodName = Helpers.SnakeToPascalCase(stripped);
					}
					else if (stripped.StartsWith("get_") && IsSimpleGetter(f, ownerType))
					{
						info.Role = FunctionRole.PropertyGetter;
						var propSnake = stripped.Substring(4);
						info.PropertyName = Helpers.SnakeToPascalCase(propSnake);
						info.MethodName = Helpers.SnakeToPascalCase(stripped);
						info.IsBoolProperty = Helpers.IsBooleanProperty(propSnake);
					}
					else if (stripped.StartsWith("set_") && IsSimpleSetter(f, ownerType))
					{
						info.Role = FunctionRole.PropertySetter;
						var propSnake = stripped.Substring(4);
						info.PropertyName = Helpers.SnakeToPascalCase(propSnake);
						info.MethodName = Helpers.SnakeToPascalCase(stripped);
						info.IsBoolProperty = Helpers.IsBooleanProperty(propSnake);
					}
					else
					{
						if (f.Parameters.Count > 0 && IsParameterOfType(f.Parameters[0], ownerType))
						{
							info.Role = FunctionRole.InstanceMethod;
						}
						else
						{
							info.Role = FunctionRole.StaticMethod;
						}
						info.MethodName = Helpers.SnakeToPascalCase(stripped);
					}

					// Detect bool return for instance methods
					if (info.Role == FunctionRole.InstanceMethod && !info.IsStringReturn)
					{
						var retType = Helpers.ConvertToCSharpType(f.ReturnType);
						if (retType == "int" && Helpers.IsBooleanFunction(stripped))
							info.IsBoolReturn = true;
					}
				}

				result.Add(info);
			}

			// Determine borrowed handles
			foreach (var h in _opaqueHandles)
			{
				if (!_ownableHandleTypes.Contains(h.Name))
					_borrowedHandleTypes.Add(h.Name);
			}

			Console.WriteLine($"Ownable handles: {string.Join(", ", _ownableHandleTypes)}");
			Console.WriteLine($"Borrowed handles: {string.Join(", ", _borrowedHandleTypes)}");

			return result;
		}

		/// <summary>
		/// Derives the C function prefix for each handle/struct type by inspecting
		/// actual function names (parameter and return types), rather than relying
		/// on PascalCase-to-snake_case name conversion which can fail for names
		/// like CesiumCGltfReader → cesium_gltf_reader_ (not cesium_c_gltf_reader_).
		/// </summary>
		private Dictionary<string, string> DeriveTypePrefixes(List<CppFunction> functions, HashSet<string> definedStructNames)
		{
			var result = new Dictionary<string, string>();

			// For opaque handle types: derive prefix from their _destroy function (most reliable)
			foreach (var h in _opaqueHandles)
			{
				var destroyFunc = functions.FirstOrDefault(f =>
					f.Name.EndsWith("_destroy")
					&& f.Parameters.Count >= 1
					&& IsParameterOfType(f.Parameters[0], h.Name));

				if (destroyFunc != null)
				{
					// prefix = function name minus "destroy"
					// e.g. "cesium_gltf_reader_destroy" → "cesium_gltf_reader_"
					result[h.Name] = destroyFunc.Name.Substring(0, destroyFunc.Name.Length - "destroy".Length);
					continue;
				}

				// For borrowed handles (no _destroy): find a _get_ function with this type as first param
				var getFunc = functions.FirstOrDefault(f =>
					f.Name.Contains("_get_")
					&& f.Parameters.Count >= 1
					&& IsParameterOfType(f.Parameters[0], h.Name));

				if (getFunc != null)
				{
					var idx = getFunc.Name.IndexOf("_get_");
					result[h.Name] = getFunc.Name.Substring(0, idx + 1); // include trailing _
					continue;
				}

				// For borrowed handles with non-get functions (e.g. _add, _remove)
				var anyFunc = functions.FirstOrDefault(f =>
					f.Parameters.Count >= 1
					&& IsParameterOfType(f.Parameters[0], h.Name));

				if (anyFunc != null)
				{
					// Find the type prefix by looking for the last common segment
					var allFuncs = functions.Where(f =>
						f.Parameters.Count >= 1
						&& IsParameterOfType(f.Parameters[0], h.Name))
						.Select(f => f.Name)
						.ToList();

					result[h.Name] = FindCommonPrefix(allFuncs);
					continue;
				}

				// Fallback: PascalCase-to-snake_case conversion
				result[h.Name] = Helpers.GetCFunctionPrefix(h.Name);
			}

			// For value-type structs: PascalCase-to-snake_case works correctly
			foreach (var s in definedStructNames)
			{
				if (!result.ContainsKey(s))
					result[s] = Helpers.GetCFunctionPrefix(s);
			}

			return result;
		}

		/// <summary>
		/// Finds the longest common prefix of a list of strings, trimmed to the last underscore.
		/// </summary>
		private string FindCommonPrefix(List<string> names)
		{
			if (names.Count == 0) return "";
			if (names.Count == 1)
			{
				// Find prefix up to last meaningful segment
				var name = names[0];
				var lastUnderscore = name.LastIndexOf('_');
				return lastUnderscore > 0 ? name.Substring(0, lastUnderscore + 1) : name;
			}

			var prefix = names[0];
			foreach (var name in names.Skip(1))
			{
				int len = Math.Min(prefix.Length, name.Length);
				int common = 0;
				for (int i = 0; i < len; i++)
				{
					if (prefix[i] != name[i]) break;
					common = i + 1;
				}
				prefix = prefix.Substring(0, common);
			}

			// Trim to last underscore
			var lastUnder = prefix.LastIndexOf('_');
			return lastUnder > 0 ? prefix.Substring(0, lastUnder + 1) : prefix;
		}

		/// <summary>
		/// Returns the handle type name if the function returns a pointer to an opaque handle.
		/// Used to assign unmatched factory functions to their return handle type.
		/// </summary>
		private string GetReturnedHandleType(CppFunction f)
		{
			var retType = f.ReturnType;
			if (retType is CppPointerType pt)
			{
				var elem = pt.ElementType;
				while (elem is CppQualifiedType qt) elem = qt.ElementType;
				if (elem is CppClass cls && Helpers.OpaqueHandleTypes.Contains(cls.Name))
					return cls.Name;
				if (elem is CppTypedef td && Helpers.OpaqueHandleTypes.Contains(td.Name))
					return td.Name;
			}
			return null;
		}

		/// <summary>
		/// Checks if a function is a factory (returns the owner handle type but doesn't take it as first param).
		/// This handles cases like cesium_ion_raster_overlay_create which creates a CesiumRasterOverlay.
		/// </summary>
		private bool IsFactoryFunction(CppFunction f, string ownerType)
		{
			if (!Helpers.OpaqueHandleTypes.Contains(ownerType))
				return false;

			// Check if return type is a pointer to the owner type
			var retType = f.ReturnType;
			if (retType is CppPointerType pt)
			{
				var elem = pt.ElementType;
				while (elem is CppQualifiedType qt) elem = qt.ElementType;
				if (elem is CppClass cls && cls.Name == ownerType)
					return true;
				if (elem is CppTypedef td && td.Name == ownerType)
					return true;
			}

			return false;
		}

		/// <summary>
		/// A "simple getter" is a function with only the owner handle/struct as parameter
		/// (no additional index or other params), suitable for a C# property.
		/// </summary>
		private bool IsSimpleGetter(CppFunction f, string ownerType)
		{
			if (f.Parameters.Count != 1) return false;
			return IsParameterOfType(f.Parameters[0], ownerType);
		}

		/// <summary>
		/// A "simple setter" is a function with exactly (owner, value) — 2 params.
		/// Setters with more params (e.g., callback + userData) are treated as instance methods.
		/// </summary>
		private bool IsSimpleSetter(CppFunction f, string ownerType)
		{
			if (f.Parameters.Count != 2) return false;
			return IsParameterOfType(f.Parameters[0], ownerType);
		}

		private bool IsParameterOfType(CppParameter param, string typeName)
		{
			var type = param.Type;

			// Unwrap pointer
			if (type is CppPointerType pt)
			{
				var elem = pt.ElementType;
				while (elem is CppQualifiedType qt) elem = qt.ElementType;

				if (elem is CppClass cls && cls.Name == typeName) return true;
				if (elem is CppTypedef td && td.Name == typeName) return true;
			}

			// Direct struct value
			if (type is CppClass directCls && directCls.Name == typeName) return true;
			if (type is CppTypedef directTd && directTd.Name == typeName) return true;

			return false;
		}

		// =====================================================================
		// Enums
		// =====================================================================

		private void GenerateEnums(CppCompilation compilation, string outputPath)
		{
			var enums = compilation.Enums
				.Where(e => e.Items.Count > 0 && !e.IsAnonymous)
				.ToList();

			if (enums.Count == 0) return;

			Console.WriteLine($"Generating {enums.Count} enums...");

			var groups = enums
				.GroupBy(e => GetNamespaceForFile(e.Span.Start.File))
				.OrderBy(g => g.Key)
				.ToList();

			using var writer = new StreamWriter(Path.Combine(outputPath, "Enums.cs"));
			WriteHeader(writer);
			writer.WriteLine("using System;");
			writer.WriteLine();

			for (int gi = 0; gi < groups.Count; gi++)
			{
				var group = groups[gi];
				if (gi > 0) writer.WriteLine();

				writer.WriteLine($"namespace {group.Key}");
				writer.WriteLine("{");

				var groupList = group.ToList();
				for (int i = 0; i < groupList.Count; i++)
				{
					var e = groupList[i];

					Helpers.PrintComments(writer, e.Comment, "\t");

					// Detect [Flags] by checking for a typedef named EnumNameFlags
					bool isFlags = compilation.Typedefs.Any(t => t.Name == e.Name + "Flags");
					if (isFlags)
						writer.WriteLine("\t[Flags]");

					writer.WriteLine($"\tpublic enum {e.Name}");
					writer.WriteLine("\t{");

					foreach (var item in e.Items)
					{
						Helpers.PrintComments(writer, item.Comment, "\t\t");
						writer.WriteLine($"\t\t{item.Name} = {item.Value},");
					}

					writer.WriteLine("\t}");

					if (i < groupList.Count - 1)
						writer.WriteLine();
				}

				writer.WriteLine("}");
			}
		}

		// =====================================================================
		// Delegates
		// =====================================================================

		private void GenerateDelegates(CppCompilation compilation, string outputPath)
		{
			var delegates = compilation.Typedefs
				.Where(t => IsFunctionPointerTypedef(t))
				.ToList();

			if (delegates.Count == 0) return;

			Console.WriteLine($"Generating {delegates.Count} delegates...");

			var groups = delegates
				.GroupBy(d => GetNamespaceForFile(d.Span.Start.File))
				.OrderBy(g => g.Key)
				.ToList();

			using var writer = new StreamWriter(Path.Combine(outputPath, "Delegates.cs"));
			WriteHeader(writer);
			writer.WriteLine("using System;");
			writer.WriteLine("using System.Runtime.InteropServices;");
			WriteSubNamespaceUsings(writer);
			writer.WriteLine();

			for (int gi = 0; gi < groups.Count; gi++)
			{
				var group = groups[gi];
				if (gi > 0) writer.WriteLine();

				writer.WriteLine($"namespace {group.Key}");
				writer.WriteLine("{");

				var groupList = group.ToList();
				for (int i = 0; i < groupList.Count; i++)
				{
					var d = groupList[i];
					var ptrType = (CppPointerType)d.ElementType;
					var funcType = (CppFunctionType)ptrType.ElementType;

					Helpers.PrintComments(writer, d.Comment, "\t");
					writer.WriteLine("\t[UnmanagedFunctionPointer(CallingConvention.Cdecl)]");

					var returnType = Helpers.ConvertToCSharpType(funcType.ReturnType);
					var parameters = BuildDelegateParameters(funcType);

					writer.Write($"\tpublic unsafe delegate {returnType} {d.Name}(");
					writer.Write(string.Join(", ", parameters));
					writer.WriteLine(");");

					if (i < groupList.Count - 1)
						writer.WriteLine();
				}

				writer.WriteLine("}");
			}
		}

		private List<string> BuildDelegateParameters(CppFunctionType funcType)
		{
			var parameters = new List<string>();

			for (int j = 0; j < funcType.Parameters.Count; j++)
			{
				var param = funcType.Parameters[j];
				var paramType = Helpers.ConvertToCSharpType(param.Type);
				var paramName = string.IsNullOrEmpty(param.Name) ? $"arg{j}" : param.Name;
				paramName = Helpers.EscapeReservedKeyword(paramName);
				parameters.Add($"{paramType} {paramName}");
			}

			return parameters;
		}

		// =====================================================================
		// Structs
		// =====================================================================

		private void GenerateStructs(CppCompilation compilation, string outputPath)
		{
			var structs = compilation.Classes
				.Where(c => c.IsDefinition
					&& c.Fields.Count > 0
					&& !string.IsNullOrEmpty(c.Name)
					&& (c.ClassKind == CppClassKind.Struct || c.ClassKind == CppClassKind.Union))
				.ToList();

			if (structs.Count == 0) return;

			Console.WriteLine($"Generating {structs.Count} structs...");

			var groups = structs
				.GroupBy(s => GetNamespaceForFile(s.Span.Start.File))
				.OrderBy(g => g.Key)
				.ToList();

			using var writer = new StreamWriter(Path.Combine(outputPath, "Structs.cs"));
			WriteHeader(writer);
			writer.WriteLine("using System;");
			writer.WriteLine("using System.Runtime.InteropServices;");
			WriteSubNamespaceUsings(writer);
			writer.WriteLine();

			for (int gi = 0; gi < groups.Count; gi++)
			{
				var group = groups[gi];
				if (gi > 0) writer.WriteLine();

				writer.WriteLine($"namespace {group.Key}");
				writer.WriteLine("{");

				var groupList = group.ToList();
				for (int i = 0; i < groupList.Count; i++)
				{
					var s = groupList[i];
					WriteStruct(writer, s, "\t");

					if (i < groupList.Count - 1)
						writer.WriteLine();
				}

				writer.WriteLine("}");
			}
		}

		private void WriteStruct(StreamWriter writer, CppClass s, string indent)
		{
			bool isUnion = s.ClassKind == CppClassKind.Union;
			string structName = s.Name;

			Helpers.PrintComments(writer, s.Comment, indent);

			if (isUnion)
				writer.WriteLine($"{indent}[StructLayout(LayoutKind.Explicit)]");
			else
				writer.WriteLine($"{indent}[StructLayout(LayoutKind.Sequential)]");

			writer.WriteLine($"{indent}public unsafe partial struct {structName}");
			writer.WriteLine($"{indent}{{");

			foreach (var field in s.Fields)
			{
				var fieldType = UnwrapQualified(field.Type);

				// Anonymous nested struct or union field
				if (fieldType is CppClass nestedClass
					&& string.IsNullOrEmpty(nestedClass.Name)
					&& nestedClass.Fields.Count > 0
					&& (nestedClass.ClassKind == CppClassKind.Struct || nestedClass.ClassKind == CppClassKind.Union))
				{
					string nestedName = $"{structName}_{field.Name}";

					// Write the nested type inline
					WriteAnonymousStructAsNamed(writer, nestedClass, nestedName, indent + "\t");
					writer.WriteLine();

					if (isUnion)
						writer.WriteLine($"{indent}\t[FieldOffset(0)]");

					Helpers.PrintComments(writer, field.Comment, indent + "\t");
					writer.WriteLine($"{indent}\tpublic {nestedName} {field.Name};");
				}
				else
				{
					WriteStructField(writer, field, isUnion, indent + "\t");
				}
			}

			writer.WriteLine($"{indent}}}");
		}

		private void WriteAnonymousStructAsNamed(StreamWriter writer, CppClass cls, string name, string indent)
		{
			bool isUnion = cls.ClassKind == CppClassKind.Union;

			if (isUnion)
				writer.WriteLine($"{indent}[StructLayout(LayoutKind.Explicit)]");
			else
				writer.WriteLine($"{indent}[StructLayout(LayoutKind.Sequential)]");

			writer.WriteLine($"{indent}public unsafe struct {name}");
			writer.WriteLine($"{indent}{{");

			foreach (var field in cls.Fields)
			{
				WriteStructField(writer, field, isUnion, indent + "\t");
			}

			writer.WriteLine($"{indent}}}");
		}

		private void WriteStructField(StreamWriter writer, CppField field, bool parentIsUnion, string indent)
		{
			if (parentIsUnion)
				writer.WriteLine($"{indent}[FieldOffset(0)]");

			Helpers.PrintComments(writer, field.Comment, indent);

			// Handle fixed-size arrays
			if (field.Type is CppArrayType arrayType && arrayType.Size > 0)
			{
				var elementCsType = Helpers.ConvertToCSharpType(arrayType.ElementType);
				writer.WriteLine($"{indent}public fixed {elementCsType} {field.Name}[{arrayType.Size}];");
			}
			else
			{
				var csType = Helpers.ConvertToCSharpType(field.Type);

				// Bool fields in structs use byte for blittable layout
				if (csType == "bool")
					csType = "byte";

				writer.WriteLine($"{indent}public {csType} {field.Name};");
			}
		}

		private CppType UnwrapQualified(CppType type)
		{
			while (type is CppQualifiedType qt)
				type = qt.ElementType;
			return type;
		}

		// =====================================================================
		// Functions
		// =====================================================================

		private void GenerateFunctions(CppCompilation compilation, string outputPath)
		{
			var functions = compilation.Functions
				.Where(f => !f.Flags.HasFlag(CppFunctionFlags.Inline)
					&& !f.Flags.HasFlag(CppFunctionFlags.FunctionTemplate))
				.ToList();

			if (functions.Count == 0) return;

			Console.WriteLine($"Generating {functions.Count} functions...");

			var groups = functions
				.GroupBy(f => GetNamespaceForFile(f.Span.Start.File))
				.OrderBy(g => g.Key)
				.ToList();

			using var writer = new StreamWriter(Path.Combine(outputPath, "Functions.cs"));
			WriteHeader(writer);
			writer.WriteLine("using System;");
			writer.WriteLine("using System.Runtime.InteropServices;");
			WriteSubNamespaceUsings(writer);
			writer.WriteLine();

			for (int gi = 0; gi < groups.Count; gi++)
			{
				var group = groups[gi];
				if (gi > 0) writer.WriteLine();

				writer.WriteLine($"namespace {group.Key}");
				writer.WriteLine("{");
				writer.WriteLine($"\tinternal static unsafe partial class {NativeClass}");
				writer.WriteLine("\t{");

				var groupList = group.ToList();
				for (int i = 0; i < groupList.Count; i++)
				{
					var f = groupList[i];

					Helpers.PrintComments(writer, f.Comment, "\t\t");
					writer.WriteLine($"\t\t[DllImport(\"{DllName}\", CallingConvention = CallingConvention.Cdecl, EntryPoint = \"{f.Name}\")]");

					var returnType = Helpers.ConvertToCSharpType(f.ReturnType);
					var parameters = BuildFunctionParameters(f);

					var cleanedFunctionName = Helpers.ClearFunctionName(f.Name);

					writer.Write($"\t\tpublic static extern {returnType} {cleanedFunctionName}(");
					writer.Write(string.Join(", ", parameters));
					writer.WriteLine(");");

					if (i < groupList.Count - 1)
						writer.WriteLine();
				}

				writer.WriteLine("\t}");
				writer.WriteLine("}");
			}
		}

		private List<string> BuildFunctionParameters(CppFunction function)
		{
			var parameters = new List<string>();

			for (int j = 0; j < function.Parameters.Count; j++)
			{
				var param = function.Parameters[j];
				var paramName = string.IsNullOrEmpty(param.Name) ? $"arg{j}" : param.Name;
				paramName = Helpers.EscapeReservedKeyword(paramName);

				// const char* parameters → [MarshalAs(UnmanagedType.LPStr)] string
				if (Helpers.IsConstCharPointer(param.Type))
				{
					parameters.Add($"[MarshalAs(UnmanagedType.LPStr)] string {paramName}");
				}
				else
				{
					var paramType = Helpers.ConvertToCSharpType(param.Type);
					parameters.Add($"{paramType} {paramName}");
				}
			}

			return parameters;
		}

		// =====================================================================
		// Handles (opaque pointer wrappers with idiomatic C# API)
		// =====================================================================

		private void GenerateHandles(string outputPath)
		{
			if (_opaqueHandles.Count == 0) return;

			Console.WriteLine($"Generating {_opaqueHandles.Count} handles...");

			var groups = _opaqueHandles
				.GroupBy(h => GetNamespaceForFile(h.File))
				.OrderBy(g => g.Key)
				.ToList();

			using var writer = new StreamWriter(Path.Combine(outputPath, "Handles.cs"));
			WriteHeader(writer);
			writer.WriteLine("using System;");
			writer.WriteLine("using System.Runtime.InteropServices;");
			WriteSubNamespaceUsings(writer);
			writer.WriteLine();

			for (int gi = 0; gi < groups.Count; gi++)
			{
				var group = groups[gi];
				if (gi > 0) writer.WriteLine();

				writer.WriteLine($"namespace {group.Key}");
				writer.WriteLine("{");

				var groupList = group.ToList();
				for (int i = 0; i < groupList.Count; i++)
				{
					var name = groupList[i].Name;
					bool isOwnable = _ownableHandleTypes.Contains(name);
					var interfaces = isOwnable ? $"IEquatable<{name}>, IDisposable" : $"IEquatable<{name}>";

					writer.WriteLine($"\tpublic unsafe partial struct {name} : {interfaces}");
					writer.WriteLine("\t{");

					// Core handle boilerplate
					writer.WriteLine($"\t\tpublic readonly IntPtr Handle;");
					writer.WriteLine($"\t\tpublic {name}(IntPtr existingHandle) {{ Handle = existingHandle; }}");
					writer.WriteLine($"\t\tpublic static {name} Null => new {name}(IntPtr.Zero);");
					writer.WriteLine($"\t\tpublic static implicit operator {name}(IntPtr handle) => new {name}(handle);");
					writer.WriteLine($"\t\tpublic static implicit operator IntPtr({name} handle) => handle.Handle;");
					writer.WriteLine($"\t\tpublic static bool operator ==({name} left, {name} right) => left.Handle == right.Handle;");
					writer.WriteLine($"\t\tpublic static bool operator !=({name} left, {name} right) => left.Handle != right.Handle;");
					writer.WriteLine($"\t\tpublic bool Equals({name} h) => Handle == h.Handle;");
					writer.WriteLine($"\t\tpublic override bool Equals(object o) => o is {name} h && Equals(h);");
					writer.WriteLine($"\t\tpublic override int GetHashCode() => Handle.GetHashCode();");
					writer.WriteLine($"\t\tpublic override string ToString() => $\"{name}[0x{{Handle:x}}]\";");

					// Get all functions belonging to this handle type
					var handleFunctions = _analyzedFunctions
						.Where(f => f.OwnerType == name)
						.ToList();

					// --- IDisposable ---
					if (isOwnable)
					{
						var destroyFunc = handleFunctions.FirstOrDefault(f => f.Role == FunctionRole.Destructor);
						if (destroyFunc != null)
						{
							var apiRef = GetApiRef(destroyFunc.CppFunction);
							writer.WriteLine();
							writer.WriteLine($"\t\t/// <summary>Releases the native resource.</summary>");
							writer.WriteLine($"\t\tpublic void Dispose() => {apiRef}.{Helpers.ClearFunctionName(destroyFunc.CppFunction.Name)}(this);");
						}
					}

					// --- Static factory methods (constructors) ---
					var constructors = handleFunctions.Where(f => f.Role == FunctionRole.Constructor).ToList();
					foreach (var ctor in constructors)
					{
						writer.WriteLine();
						WriteHandleStaticFactory(writer, ctor, name, "\t\t");
					}

					// --- Properties ---
					var getters = handleFunctions.Where(f => f.Role == FunctionRole.PropertyGetter).ToList();
					var setters = handleFunctions.Where(f => f.Role == FunctionRole.PropertySetter).ToList();
					var properties = BuildProperties(getters, setters);
					foreach (var prop in properties)
					{
						writer.WriteLine();
						WriteProperty(writer, prop, "\t\t");
					}

					// --- Instance methods ---
					var instanceMethods = handleFunctions
						.Where(f => f.Role == FunctionRole.InstanceMethod)
						.ToList();
					foreach (var method in instanceMethods)
					{
						writer.WriteLine();
						WriteInstanceMethod(writer, method, name, "\t\t");
					}

					// --- Static methods (not constructors) ---
					var staticMethods = handleFunctions
						.Where(f => f.Role == FunctionRole.StaticMethod)
						.ToList();
					foreach (var method in staticMethods)
					{
						writer.WriteLine();
						WriteStaticMethod(writer, method, "\t\t");
					}

					writer.WriteLine("\t}");

					if (i < groupList.Count - 1)
						writer.WriteLine();
				}

				writer.WriteLine("}");
			}

			// Also generate a public class for global functions
			var globalFuncs = _analyzedFunctions.Where(f => f.Role == FunctionRole.GlobalFunction).ToList();
			if (globalFuncs.Count > 0)
			{
				writer.WriteLine();
				writer.WriteLine($"namespace {BaseNamespace}");
				writer.WriteLine("{");
				writer.WriteLine($"\tpublic static unsafe class CesiumNativeApi");
				writer.WriteLine("\t{");

				for (int i = 0; i < globalFuncs.Count; i++)
				{
					var gf = globalFuncs[i];
					if (i > 0) writer.WriteLine();
					WriteGlobalFunction(writer, gf, "\t\t");
				}

				writer.WriteLine("\t}");
				writer.WriteLine("}");
			}
		}

		/// <summary>Returns the fully-qualified CesiumAPI reference for a given C function.</summary>
		private string GetApiRef(CppFunction f)
		{
			var ns = GetNamespaceForFile(f.Span.Start.File);
			return $"{ns}.{NativeClass}";
		}

		private void WriteHandleStaticFactory(StreamWriter writer, FunctionInfo info, string handleName, string indent)
		{
			var f = info.CppFunction;
			var cleanedName = Helpers.ClearFunctionName(f.Name);
			var apiRef = GetApiRef(f);

			Helpers.PrintComments(writer, f.Comment, indent);

			// Build parameter list (skip none — constructors don't take the handle as first param)
			var csParams = BuildWrapperParameters(f, skipFirstParam: false);
			var callArgs = BuildWrapperCallArgs(f, skipFirstParam: false);

			writer.WriteLine($"{indent}public static {handleName} {info.MethodName}({string.Join(", ", csParams)})");
			writer.WriteLine($"{indent}\t=> {apiRef}.{cleanedName}({string.Join(", ", callArgs)});");
		}

		private void WriteInstanceMethod(StreamWriter writer, FunctionInfo info, string handleName, string indent)
		{
			var f = info.CppFunction;
			var cleanedName = Helpers.ClearFunctionName(f.Name);
			var apiRef = GetApiRef(f);

			Helpers.PrintComments(writer, f.Comment, indent);

			// Skip the first parameter (the handle itself — we pass 'this')
			var csParams = BuildWrapperParameters(f, skipFirstParam: true);
			var callArgs = BuildWrapperCallArgs(f, skipFirstParam: true, prependThis: true);

			var returnType = Helpers.ConvertToCSharpType(f.ReturnType);

			// Bool return conversion
			if (info.IsBoolReturn)
			{
				writer.WriteLine($"{indent}public bool {info.MethodName}({string.Join(", ", csParams)})");
				writer.WriteLine($"{indent}\t=> {apiRef}.{cleanedName}({string.Join(", ", callArgs)}) != 0;");
			}
			// String return conversion
			else if (info.IsStringReturn)
			{
				writer.WriteLine($"{indent}public string {info.MethodName}({string.Join(", ", csParams)})");
				writer.WriteLine($"{indent}\t=> Marshal.PtrToStringUTF8((IntPtr){apiRef}.{cleanedName}({string.Join(", ", callArgs)}));");
			}
			else if (returnType == "void")
			{
				writer.WriteLine($"{indent}public void {info.MethodName}({string.Join(", ", csParams)})");
				writer.WriteLine($"{indent}\t=> {apiRef}.{cleanedName}({string.Join(", ", callArgs)});");
			}
			else
			{
				writer.WriteLine($"{indent}public {returnType} {info.MethodName}({string.Join(", ", csParams)})");
				writer.WriteLine($"{indent}\t=> {apiRef}.{cleanedName}({string.Join(", ", callArgs)});");
			}
		}

		private void WriteStaticMethod(StreamWriter writer, FunctionInfo info, string indent)
		{
			var f = info.CppFunction;
			var cleanedName = Helpers.ClearFunctionName(f.Name);
			var apiRef = GetApiRef(f);

			Helpers.PrintComments(writer, f.Comment, indent);

			var csParams = BuildWrapperParameters(f, skipFirstParam: false);
			var callArgs = BuildWrapperCallArgs(f, skipFirstParam: false);

			var returnType = Helpers.ConvertToCSharpType(f.ReturnType);

			if (info.IsStringReturn)
			{
				writer.WriteLine($"{indent}public static string {info.MethodName}({string.Join(", ", csParams)})");
				writer.WriteLine($"{indent}\t=> Marshal.PtrToStringUTF8((IntPtr){apiRef}.{cleanedName}({string.Join(", ", callArgs)}));");
			}
			else if (returnType == "void")
			{
				writer.WriteLine($"{indent}public static void {info.MethodName}({string.Join(", ", csParams)})");
				writer.WriteLine($"{indent}\t=> {apiRef}.{cleanedName}({string.Join(", ", callArgs)});");
			}
			else
			{
				writer.WriteLine($"{indent}public static {returnType} {info.MethodName}({string.Join(", ", csParams)})");
				writer.WriteLine($"{indent}\t=> {apiRef}.{cleanedName}({string.Join(", ", callArgs)});");
			}
		}

		private void WriteGlobalFunction(StreamWriter writer, FunctionInfo info, string indent)
		{
			var f = info.CppFunction;
			var cleanedName = Helpers.ClearFunctionName(f.Name);
			// Global functions are in Common namespace but we call via fully qualified name
			var ns = GetNamespaceForFile(f.Span.Start.File);

			Helpers.PrintComments(writer, f.Comment, indent);

			var csParams = BuildWrapperParameters(f, skipFirstParam: false);
			var callArgs = BuildWrapperCallArgs(f, skipFirstParam: false);

			var returnType = Helpers.ConvertToCSharpType(f.ReturnType);

			if (info.IsStringReturn)
			{
				writer.WriteLine($"{indent}public static string {info.MethodName}({string.Join(", ", csParams)})");
				writer.WriteLine($"{indent}\t=> Marshal.PtrToStringUTF8((IntPtr){ns}.{NativeClass}.{cleanedName}({string.Join(", ", callArgs)}));");
			}
			else if (returnType == "void")
			{
				writer.WriteLine($"{indent}public static void {info.MethodName}({string.Join(", ", csParams)})");
				writer.WriteLine($"{indent}\t=> {ns}.{NativeClass}.{cleanedName}({string.Join(", ", callArgs)});");
			}
			else
			{
				writer.WriteLine($"{indent}public static {returnType} {info.MethodName}({string.Join(", ", csParams)})");
				writer.WriteLine($"{indent}\t=> {ns}.{NativeClass}.{cleanedName}({string.Join(", ", callArgs)});");
			}
		}

		private List<PropertyInfo> BuildProperties(List<FunctionInfo> getters, List<FunctionInfo> setters)
		{
			var result = new List<PropertyInfo>();
			var setterMap = setters.ToDictionary(s => s.PropertyName, s => s);

			foreach (var getter in getters)
			{
				var prop = new PropertyInfo
				{
					Name = getter.PropertyName,
					Getter = getter,
				};

				if (setterMap.TryGetValue(getter.PropertyName, out var setter))
				{
					prop.Setter = setter;
					setterMap.Remove(getter.PropertyName);
				}

				// Determine C# type
				prop.IsString = getter.IsStringReturn;
				prop.IsBool = getter.IsBoolProperty;

				if (prop.IsString)
					prop.CSharpType = "string";
				else if (prop.IsBool)
					prop.CSharpType = "bool";
				else
					prop.CSharpType = Helpers.ConvertToCSharpType(getter.CppFunction.ReturnType);

				result.Add(prop);
			}

			return result;
		}

		private void WriteProperty(StreamWriter writer, PropertyInfo prop, string indent)
		{
			var getterCleanName = Helpers.ClearFunctionName(prop.Getter.CppFunction.Name);
			var getterApiRef = GetApiRef(prop.Getter.CppFunction);

			Helpers.PrintComments(writer, prop.Getter.CppFunction.Comment, indent);

			if (prop.Setter != null)
			{
				var setterCleanName = Helpers.ClearFunctionName(prop.Setter.CppFunction.Name);
				var setterApiRef = GetApiRef(prop.Setter.CppFunction);

				writer.WriteLine($"{indent}public {prop.CSharpType} {prop.Name}");
				writer.WriteLine($"{indent}{{");

				// Getter
				if (prop.IsString)
					writer.WriteLine($"{indent}\tget => Marshal.PtrToStringUTF8((IntPtr){getterApiRef}.{getterCleanName}(this));");
				else if (prop.IsBool)
					writer.WriteLine($"{indent}\tget => {getterApiRef}.{getterCleanName}(this) != 0;");
				else
					writer.WriteLine($"{indent}\tget => {getterApiRef}.{getterCleanName}(this);");

				// Setter
				if (prop.IsBool)
					writer.WriteLine($"{indent}\tset => {setterApiRef}.{setterCleanName}(this, value ? 1 : 0);");
				else
					writer.WriteLine($"{indent}\tset => {setterApiRef}.{setterCleanName}(this, value);");

				writer.WriteLine($"{indent}}}");
			}
			else
			{
				// Read-only property
				if (prop.IsString)
				{
					writer.WriteLine($"{indent}public {prop.CSharpType} {prop.Name}");
					writer.WriteLine($"{indent}\t=> Marshal.PtrToStringUTF8((IntPtr){getterApiRef}.{getterCleanName}(this));");
				}
				else if (prop.IsBool)
				{
					writer.WriteLine($"{indent}public {prop.CSharpType} {prop.Name}");
					writer.WriteLine($"{indent}\t=> {getterApiRef}.{getterCleanName}(this) != 0;");
				}
				else
				{
					writer.WriteLine($"{indent}public {prop.CSharpType} {prop.Name}");
					writer.WriteLine($"{indent}\t=> {getterApiRef}.{getterCleanName}(this);");
				}
			}
		}

		/// <summary>Builds the C# parameter list for a wrapper method.</summary>
		private List<string> BuildWrapperParameters(CppFunction f, bool skipFirstParam)
		{
			var result = new List<string>();
			int start = skipFirstParam ? 1 : 0;

			for (int i = start; i < f.Parameters.Count; i++)
			{
				var param = f.Parameters[i];
				var paramName = string.IsNullOrEmpty(param.Name) ? $"arg{i}" : param.Name;
				paramName = Helpers.EscapeReservedKeyword(paramName);

				if (Helpers.IsConstCharPointer(param.Type))
				{
					result.Add($"string {paramName}");
				}
				else
				{
					var paramType = Helpers.ConvertToCSharpType(param.Type);
					result.Add($"{paramType} {paramName}");
				}
			}

			return result;
		}

		/// <summary>Builds the argument list for calling the internal CesiumAPI method.</summary>
		private List<string> BuildWrapperCallArgs(CppFunction f, bool skipFirstParam, bool prependThis = false)
		{
			var result = new List<string>();

			if (prependThis)
				result.Add("this");

			int start = skipFirstParam ? 1 : 0;
			for (int i = start; i < f.Parameters.Count; i++)
			{
				var param = f.Parameters[i];
				var paramName = string.IsNullOrEmpty(param.Name) ? $"arg{i}" : param.Name;
				paramName = Helpers.EscapeReservedKeyword(paramName);
				result.Add(paramName);
			}

			return result;
		}

		// =====================================================================
		// Struct value-type methods
		// =====================================================================

		private void GenerateStructMethods(string outputPath)
		{
			// Find functions that belong to value-type structs (not opaque handles)
			var structFunctions = _analyzedFunctions
				.Where(f => f.OwnerType != null
					&& !Helpers.OpaqueHandleTypes.Contains(f.OwnerType)
					&& Helpers.DefinedStructNames.Contains(f.OwnerType))
				.GroupBy(f => f.OwnerType)
				.ToList();

			if (structFunctions.Count == 0) return;

			Console.WriteLine($"Generating struct methods for {structFunctions.Count} value types...");

			using var writer = new StreamWriter(Path.Combine(outputPath, "StructExtensions.cs"));
			WriteHeader(writer);
			writer.WriteLine("using System;");
			writer.WriteLine("using System.Runtime.InteropServices;");
			WriteSubNamespaceUsings(writer);
			writer.WriteLine();

			// Group by namespace
			var nsGroups = structFunctions
				.GroupBy(g =>
				{
					// Find where this struct was defined to determine namespace
					var firstFunc = g.First();
					return GetNamespaceForFile(firstFunc.CppFunction.Span.Start.File);
				})
				.OrderBy(g => g.Key)
				.ToList();

			for (int nsi = 0; nsi < nsGroups.Count; nsi++)
			{
				var nsGroup = nsGroups[nsi];
				if (nsi > 0) writer.WriteLine();

				writer.WriteLine($"namespace {nsGroup.Key}");
				writer.WriteLine("{");

				var structGroups = nsGroup.ToList();
				for (int si = 0; si < structGroups.Count; si++)
				{
					var structGroup = structGroups[si];
					var structName = structGroup.Key;

					if (si > 0) writer.WriteLine();
					writer.WriteLine($"\tpublic unsafe partial struct {structName}");
					writer.WriteLine("\t{");

					var funcs = structGroup.ToList();
					for (int fi = 0; fi < funcs.Count; fi++)
					{
						var func = funcs[fi];
						if (fi > 0) writer.WriteLine();

						var f = func.CppFunction;
						var cleanedName = Helpers.ClearFunctionName(f.Name);
						var apiRef = GetApiRef(f);

						// Determine if this is a static factory or an instance method
						bool isFactory = func.Role == FunctionRole.Constructor;
						bool isInstanceMethod = func.Role == FunctionRole.InstanceMethod
							&& f.Parameters.Count > 0
							&& IsParameterOfType(f.Parameters[0], structName);

						Helpers.PrintComments(writer, f.Comment, "\t\t");

						if (isFactory)
						{
							var csParams = BuildWrapperParameters(f, skipFirstParam: false);
							var callArgs = BuildWrapperCallArgs(f, skipFirstParam: false);

							writer.WriteLine($"\t\tpublic static {structName} {func.MethodName}({string.Join(", ", csParams)})");
							writer.WriteLine($"\t\t\t=> {apiRef}.{cleanedName}({string.Join(", ", callArgs)});");
						}
						else if (isInstanceMethod)
						{
							var csParams = BuildWrapperParameters(f, skipFirstParam: true);
							var callArgs = BuildWrapperCallArgs(f, skipFirstParam: true, prependThis: true);

							var returnType = Helpers.ConvertToCSharpType(f.ReturnType);

							// Bool return
							if (func.IsBoolReturn || (returnType == "int" && Helpers.IsBooleanFunction(func.StrippedSnakeName)))
							{
								writer.WriteLine($"\t\tpublic bool {func.MethodName}({string.Join(", ", csParams)})");
								writer.WriteLine($"\t\t\t=> {apiRef}.{cleanedName}({string.Join(", ", callArgs)}) != 0;");
							}
							else if (func.IsStringReturn)
							{
								writer.WriteLine($"\t\tpublic string {func.MethodName}({string.Join(", ", csParams)})");
								writer.WriteLine($"\t\t\t=> Marshal.PtrToStringUTF8((IntPtr){apiRef}.{cleanedName}({string.Join(", ", callArgs)}));");
							}
							else if (returnType == "void")
							{
								writer.WriteLine($"\t\tpublic void {func.MethodName}({string.Join(", ", csParams)})");
								writer.WriteLine($"\t\t\t=> {apiRef}.{cleanedName}({string.Join(", ", callArgs)});");
							}
							else
							{
								writer.WriteLine($"\t\tpublic {returnType} {func.MethodName}({string.Join(", ", csParams)})");
								writer.WriteLine($"\t\t\t=> {apiRef}.{cleanedName}({string.Join(", ", callArgs)});");
							}
						}
						else
						{
							// Static method on the struct
							var csParams = BuildWrapperParameters(f, skipFirstParam: false);
							var callArgs = BuildWrapperCallArgs(f, skipFirstParam: false);

							var returnType = Helpers.ConvertToCSharpType(f.ReturnType);

							if (func.IsStringReturn)
							{
								writer.WriteLine($"\t\tpublic static string {func.MethodName}({string.Join(", ", csParams)})");
								writer.WriteLine($"\t\t\t=> Marshal.PtrToStringUTF8((IntPtr){apiRef}.{cleanedName}({string.Join(", ", callArgs)}));");
							}
							else
							{
								writer.WriteLine($"\t\tpublic static {returnType} {func.MethodName}({string.Join(", ", csParams)})");
								writer.WriteLine($"\t\t\t=> {apiRef}.{cleanedName}({string.Join(", ", callArgs)});");
							}
						}
					}

					writer.WriteLine("\t}");
				}

				writer.WriteLine("}");
			}
		}

		// =====================================================================
		// Utility
		// =====================================================================

		private void WriteHeader(StreamWriter writer)
		{
			writer.WriteLine("// -------------------------------------------------------------------------------------------------");
			writer.WriteLine("// This file was auto-generated by CesiumGen. Do not edit manually.");
			writer.WriteLine("// -------------------------------------------------------------------------------------------------");
		}

		private void WriteSubNamespaceUsings(StreamWriter writer)
		{
			foreach (var ns in SubNamespaceUsings)
				writer.WriteLine($"using {ns};");
		}
	}
}
