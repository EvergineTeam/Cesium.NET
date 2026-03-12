using CppAst;
using System.Collections.Generic;
using System.IO;

namespace CesiumGen
{
	public static class Helpers
	{
		public static List<string> OpaqueHandleTypes = new List<string>();
		public static List<string> DelegateNames = new List<string>();

		private static readonly Dictionary<string, string> csNameMappings = new()
		{
			{ "bool", "bool" },
			{ "uint8_t", "byte" },
			{ "uint16_t", "ushort" },
			{ "uint32_t", "uint" },
			{ "uint64_t", "ulong" },
			{ "int8_t", "sbyte" },
			{ "int16_t", "short" },
			{ "int32_t", "int" },
			{ "int64_t", "long" },
			{ "char", "byte" },
			{ "size_t", "nuint" },
			{ "intptr_t", "nint" },
			{ "uintptr_t", "nuint" },
		};

		private static readonly HashSet<string> csReservedKeywords = new()
		{
			"abstract", "as", "base", "bool", "break", "byte", "case", "catch",
			"char", "checked", "class", "const", "continue", "decimal", "default",
			"delegate", "do", "double", "else", "enum", "event", "explicit",
			"extern", "false", "finally", "fixed", "float", "for", "foreach",
			"goto", "if", "implicit", "in", "int", "interface", "internal", "is",
			"lock", "long", "namespace", "new", "null", "object", "operator",
			"out", "override", "params", "private", "protected", "public",
			"readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof",
			"stackalloc", "static", "string", "struct", "switch", "this", "throw",
			"true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe",
			"ushort", "using", "virtual", "void", "volatile", "while",
		};

		public static string ConvertToCSharpType(CppType type)
		{
			switch (type)
			{
				case CppPrimitiveType primitive:
					return ConvertPrimitive(primitive);

				case CppQualifiedType qualified:
					return ConvertToCSharpType(qualified.ElementType);

				case CppEnum enumType:
					return enumType.Name;

				case CppTypedef typedef:
					return ConvertTypedef(typedef);

				case CppClass classType:
					return classType.Name;

				case CppPointerType pointer:
					return ConvertPointerType(pointer);

				case CppArrayType array:
					return ConvertToCSharpType(array.ElementType);

				default:
					return "IntPtr";
			}
		}

		private static string ConvertPrimitive(CppPrimitiveType primitive)
		{
			return primitive.Kind switch
			{
				CppPrimitiveKind.Void => "void",
				CppPrimitiveKind.Bool => "bool",
				CppPrimitiveKind.Char => "byte",
				CppPrimitiveKind.Short => "short",
				CppPrimitiveKind.Int => "int",
				CppPrimitiveKind.Long => "int",
				CppPrimitiveKind.LongLong => "long",
				CppPrimitiveKind.UnsignedChar => "byte",
				CppPrimitiveKind.UnsignedShort => "ushort",
				CppPrimitiveKind.UnsignedInt => "uint",
				CppPrimitiveKind.UnsignedLong => "uint",
				CppPrimitiveKind.UnsignedLongLong => "ulong",
				CppPrimitiveKind.Float => "float",
				CppPrimitiveKind.Double => "double",
				CppPrimitiveKind.WChar => "char",
				_ => "IntPtr",
			};
		}

		private static string ConvertTypedef(CppTypedef typedef)
		{
			var name = typedef.Name;

			if (csNameMappings.TryGetValue(name, out var mapped))
				return mapped;

			if (OpaqueHandleTypes.Contains(name))
				return name;

			if (DelegateNames.Contains(name))
				return name;

			return ConvertToCSharpType(typedef.ElementType);
		}

		private static string ConvertPointerType(CppPointerType pointer)
		{
			var elementType = pointer.ElementType;

			// Unwrap const/volatile qualifiers
			while (elementType is CppQualifiedType qt)
				elementType = qt.ElementType;

			// void* → void*
			if (elementType is CppPrimitiveType voidPrim && voidPrim.Kind == CppPrimitiveKind.Void)
				return "void*";

			// char* / const char* → byte*
			if (elementType is CppPrimitiveType charPrim && charPrim.Kind == CppPrimitiveKind.Char)
				return "byte*";

			// Function pointer → IntPtr
			if (elementType is CppFunctionType)
				return "IntPtr";

			// Pointer to opaque handle struct → IntPtr
			if (elementType is CppClass cls && OpaqueHandleTypes.Contains(cls.Name))
				return "IntPtr";

			// Pointer to typedef of opaque handle → IntPtr
			if (elementType is CppTypedef td && OpaqueHandleTypes.Contains(td.Name))
				return "IntPtr";

			// For other pointer types, recurse
			var inner = ConvertToCSharpType(elementType);

			if (inner == "IntPtr")
				return "IntPtr*";

			return inner + "*";
		}

		/// <summary>
		/// Checks if a CppType represents const char* or char* (for string marshalling).
		/// </summary>
		public static bool IsConstCharPointer(CppType type)
		{
			if (type is CppPointerType pt)
			{
				var elem = pt.ElementType;

				// const char*
				if (elem is CppQualifiedType qt)
				{
					var inner = qt.ElementType;
					if (inner is CppPrimitiveType prim && prim.Kind == CppPrimitiveKind.Char)
						return true;
				}

				// plain char*
				if (elem is CppPrimitiveType charPrim && charPrim.Kind == CppPrimitiveKind.Char)
					return true;
			}

			return false;
		}

		public static string EscapeReservedKeyword(string name)
		{
			if (csReservedKeywords.Contains(name))
				return "@" + name;
			return name;
		}

		public static void PrintComments(StreamWriter writer, CppComment comment, string tabs)
		{
			if (comment == null) return;

			var text = comment.ToString();
			if (string.IsNullOrWhiteSpace(text)) return;

			writer.WriteLine($"{tabs}/// <summary>");
			foreach (var line in text.Split('\n'))
			{
				var trimmed = line.Trim();
				if (!string.IsNullOrEmpty(trimmed))
					writer.WriteLine($"{tabs}/// {trimmed}");
			}
			writer.WriteLine($"{tabs}/// </summary>");
		}
	}
}
