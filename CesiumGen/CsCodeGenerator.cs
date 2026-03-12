using CppAst;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CesiumGen
{
	public class CsCodeGenerator
	{
		public static readonly CsCodeGenerator Instance = new CsCodeGenerator();

		private const string Namespace = "Evergine.Bindings.CesiumNative";
		private const string DllName = "CesiumNativeC";
		private const string NativeClass = "CesiumNativeAPI";

		public void Generate(CppCompilation compilation, string outputPath)
		{
			// Collect defined struct names
			var definedStructNames = compilation.Classes
				.Where(c => c.ClassKind == CppClassKind.Struct && c.IsDefinition)
				.Select(c => c.Name)
				.ToHashSet();

			// Collect opaque handle types (forward-declared structs with no definition)
			Helpers.OpaqueHandleTypes = compilation.Classes
				.Where(c => c.ClassKind == CppClassKind.Struct
					&& !c.IsDefinition
					&& !string.IsNullOrEmpty(c.Name)
					&& !definedStructNames.Contains(c.Name))
				.Select(c => c.Name)
				.Distinct()
				.ToList();

			// Collect delegate names (function pointer typedefs)
			Helpers.DelegateNames = compilation.Typedefs
				.Where(t => IsFunctionPointerTypedef(t))
				.Select(t => t.Name)
				.ToList();

			Console.WriteLine($"Found {Helpers.OpaqueHandleTypes.Count} opaque handle types:");
			foreach (var h in Helpers.OpaqueHandleTypes)
				Console.WriteLine($"  {h}");

			Console.WriteLine($"Found {Helpers.DelegateNames.Count} delegates:");
			foreach (var d in Helpers.DelegateNames)
				Console.WriteLine($"  {d}");

			GenerateEnums(compilation, outputPath);
			GenerateDelegates(compilation, outputPath);
			GenerateStructs(compilation, outputPath);
			GenerateFunctions(compilation, outputPath);
			GenerateHandles(compilation, outputPath);
		}

		private bool IsFunctionPointerTypedef(CppTypedef typedef)
		{
			var elementType = typedef.ElementType;
			if (elementType is CppPointerType pt)
				return pt.ElementType is CppFunctionType;
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

			using var writer = new StreamWriter(Path.Combine(outputPath, "Enums.cs"));
			WriteHeader(writer);
			writer.WriteLine("using System;");
			writer.WriteLine();
			writer.WriteLine($"namespace {Namespace}");
			writer.WriteLine("{");

			for (int i = 0; i < enums.Count; i++)
			{
				var e = enums[i];

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

				if (i < enums.Count - 1)
					writer.WriteLine();
			}

			writer.WriteLine("}");
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

			using var writer = new StreamWriter(Path.Combine(outputPath, "Delegates.cs"));
			WriteHeader(writer);
			writer.WriteLine("using System;");
			writer.WriteLine("using System.Runtime.InteropServices;");
			writer.WriteLine();
			writer.WriteLine($"namespace {Namespace}");
			writer.WriteLine("{");

			for (int i = 0; i < delegates.Count; i++)
			{
				var d = delegates[i];
				var ptrType = (CppPointerType)d.ElementType;
				var funcType = (CppFunctionType)ptrType.ElementType;

				Helpers.PrintComments(writer, d.Comment, "\t");
				writer.WriteLine("\t[UnmanagedFunctionPointer(CallingConvention.Cdecl)]");

				var returnType = Helpers.ConvertToCSharpType(funcType.ReturnType);
				var parameters = BuildDelegateParameters(funcType);

				writer.Write($"\tpublic unsafe delegate {returnType} {d.Name}(");
				writer.Write(string.Join(", ", parameters));
				writer.WriteLine(");");

				if (i < delegates.Count - 1)
					writer.WriteLine();
			}

			writer.WriteLine("}");
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

			using var writer = new StreamWriter(Path.Combine(outputPath, "Structs.cs"));
			WriteHeader(writer);
			writer.WriteLine("using System;");
			writer.WriteLine("using System.Runtime.InteropServices;");
			writer.WriteLine();
			writer.WriteLine($"namespace {Namespace}");
			writer.WriteLine("{");

			for (int i = 0; i < structs.Count; i++)
			{
				var s = structs[i];
				WriteStruct(writer, s, "\t");

				if (i < structs.Count - 1)
					writer.WriteLine();
			}

			writer.WriteLine("}");
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

			writer.WriteLine($"{indent}public unsafe struct {structName}");
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

			using var writer = new StreamWriter(Path.Combine(outputPath, "Functions.cs"));
			WriteHeader(writer);
			writer.WriteLine("using System;");
			writer.WriteLine("using System.Runtime.InteropServices;");
			writer.WriteLine();
			writer.WriteLine($"namespace {Namespace}");
			writer.WriteLine("{");
			writer.WriteLine($"\tpublic static unsafe partial class {NativeClass}");
			writer.WriteLine("\t{");

			for (int i = 0; i < functions.Count; i++)
			{
				var f = functions[i];

				Helpers.PrintComments(writer, f.Comment, "\t\t");
				writer.WriteLine($"\t\t[DllImport(\"{DllName}\", CallingConvention = CallingConvention.Cdecl)]");

				var returnType = Helpers.ConvertToCSharpType(f.ReturnType);
				var parameters = BuildFunctionParameters(f);

				writer.Write($"\t\tpublic static extern {returnType} {f.Name}(");
				writer.Write(string.Join(", ", parameters));
				writer.WriteLine(");");

				if (i < functions.Count - 1)
					writer.WriteLine();
			}

			writer.WriteLine("\t}");
			writer.WriteLine("}");
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
		// Handles (opaque pointer wrappers)
		// =====================================================================

		private void GenerateHandles(CppCompilation compilation, string outputPath)
		{
			if (Helpers.OpaqueHandleTypes.Count == 0) return;

			Console.WriteLine($"Generating {Helpers.OpaqueHandleTypes.Count} handles...");

			using var writer = new StreamWriter(Path.Combine(outputPath, "Handles.cs"));
			WriteHeader(writer);
			writer.WriteLine("using System;");
			writer.WriteLine();
			writer.WriteLine($"namespace {Namespace}");
			writer.WriteLine("{");

			for (int i = 0; i < Helpers.OpaqueHandleTypes.Count; i++)
			{
				var name = Helpers.OpaqueHandleTypes[i];

				writer.WriteLine($"\tpublic partial struct {name} : IEquatable<{name}>");
				writer.WriteLine("\t{");
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
				writer.WriteLine("\t}");

				if (i < Helpers.OpaqueHandleTypes.Count - 1)
					writer.WriteLine();
			}

			writer.WriteLine("}");
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
	}
}
