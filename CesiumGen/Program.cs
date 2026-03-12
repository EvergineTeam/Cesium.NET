using CppAst;
using System;
using System.IO;

namespace CesiumGen
{
	class Program
	{
		static void Main(string[] args)
		{
			var headerFile = Path.Combine(AppContext.BaseDirectory, "Headers", "cesium-native-api.h");
			var headersDir = Path.Combine(AppContext.BaseDirectory, "Headers");

			var options = new CppParserOptions
			{
				ParseMacros = true,
				IncludeFolders = { headersDir },
			};

			Console.WriteLine($"Parsing header: {headerFile}");
			var compilation = CppParser.ParseFile(headerFile, options);

			if (compilation.HasErrors)
			{
				foreach (var message in compilation.Diagnostics.Messages)
				{
					if (message.Type == CppLogMessageType.Error)
						Console.Error.WriteLine($"ERROR: {message}");
					else
						Console.WriteLine($"  {message}");
				}
			}

			string outputPath = Path.Combine(
				AppContext.BaseDirectory, "..", "..", "..", "..", "..",
				"Evergine.Bindings.CesiumNative", "Generated");

			outputPath = Path.GetFullPath(outputPath);

			if (!Directory.Exists(outputPath))
				Directory.CreateDirectory(outputPath);

			Console.WriteLine($"Output path: {outputPath}");
			CsCodeGenerator.Instance.Generate(compilation, outputPath);
			Console.WriteLine("Generation complete!");
		}
	}
}
