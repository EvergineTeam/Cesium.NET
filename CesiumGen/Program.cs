using CppAst;
using System;
using System.IO;

namespace CesiumGen
{
	class Program
	{
		static void Main(string[] args)
		{
			var headersDir = Path.Combine(AppContext.BaseDirectory, "Headers");
			var headerFile = Path.Combine(headersDir, "cesium-native-api.h");

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
					{
						Console.ForegroundColor = ConsoleColor.Red;
					}

					Console.WriteLine(message);
					Console.ResetColor();
				}

				// Fatal, because a header the parser could not read produces a smaller
				// binding rather than no binding: the missing declarations simply do not
				// appear, the build succeeds, and the package ships without them. Printing
				// and carrying on made a parse failure look like a successful run.
				//
				// This is not hypothetical here. cesium-native-api.h includes <stdint.h>
				// and <stddef.h>, and libclang cannot resolve those on a runner without a
				// C toolchain in the default search path -- which is why generation runs on
				// windows-latest even though the package targets linux-x64.
				Console.Error.WriteLine(
					"Parsing reported errors. Refusing to generate from an incomplete " +
					"compilation.");
				Environment.Exit(1);
			}

			Console.WriteLine($"Enums:     {compilation.Enums.Count}");
			Console.WriteLine($"Classes:   {compilation.Classes.Count}");
			Console.WriteLine($"Functions: {compilation.Functions.Count}");
			Console.WriteLine($"Typedefs:  {compilation.Typedefs.Count}");
			Console.WriteLine($"Macros:    {compilation.Macros.Count}");

			string outputPath = Path.Combine(
				FindRepositoryRoot(),
				"Evergine.Bindings.CesiumNative", "Generated");

			// Deliberately not created if missing. This used to be a CreateDirectory over a
			// path built by counting `..` up from AppContext.BaseDirectory, which is right
			// under `dotnet run` and one level short under `dotnet publish` -- and publish is
			// what CI and CD execute. The generator wrote its seven files into a phantom
			// directory under CesiumGen/, exited 0, and the pack used the committed output.
			if (!Directory.Exists(outputPath))
			{
				throw new DirectoryNotFoundException(
					$"Output directory not found: {outputPath}. It is committed to the " +
					"repository, so its absence means the path was resolved wrongly.");
			}

			Console.WriteLine($"Output path: {outputPath}");
			CsCodeGenerator.Instance.Generate(compilation, outputPath);
			Console.WriteLine("Generation complete!");
		}

		// Anchored on the manifest rather than on a fixed number of parent steps, so the
		// answer does not depend on how deep the build put the binaries.
		static string FindRepositoryRoot()
		{
			var dir = new DirectoryInfo(AppContext.BaseDirectory);

			while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "binding.yml")))
			{
				dir = dir.Parent;
			}

			if (dir is null)
			{
				throw new DirectoryNotFoundException(
					"Could not find binding.yml walking up from " +
					$"{AppContext.BaseDirectory}. The generator locates its output relative " +
					"to the manifest, so it cannot run outside the repository.");
			}

			return dir.FullName;
		}
	}
}
