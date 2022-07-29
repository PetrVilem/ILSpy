using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.Util;

using ILSpy.BamlDecompiler;

namespace XamlExtractor
{
	internal class Program
	{
		static int Main(string[] args)
		{
			if (args.Length != 2)
			{
				Console.Error.WriteLine("Invalid input - two parameters (assembly path and output folder path) are expected");
				return 1;
			}

			var assemblyPath = args[0];
			if (!File.Exists(assemblyPath))
			{
				Console.Error.WriteLine($"Invalid input - file '{assemblyPath}' not found");
				return 1;
			}

			try
			{
				var outputDir = args[1];
				if (!Directory.Exists(outputDir))
				{
					Directory.CreateDirectory(outputDir);
				}

				var module = new PEFile(assemblyPath);
				var frameworkId = DotNetCorePathFinderExtensions.DetectTargetFrameworkId(module);
				var resolver = new UniversalAssemblyResolver(assemblyPath, false, frameworkId);
				var typeSystem = new BamlDecompilerTypeSystem(module, resolver);
				var decompiler = new XamlDecompiler(typeSystem, new BamlDecompilerSettings());

				foreach (var moduleResource in module.Resources.Where(x => x.ResourceType == ResourceType.Embedded))
				{
					ResourcesFile? resources = null;
					try
					{
						resources = new ResourcesFile(moduleResource.TryOpenStream());
					}
					catch { }
					if (resources == null)
					{
						Console.Error.WriteLine($"Failed to open '{moduleResource.Name}'");
						continue;
					}

					foreach (var item in resources
						.Where(x => x.Key.ToLower().EndsWith(".baml"))
						.OrderBy(x => x.Key))
					{
						var resourceStream = GetResStream(item.Value);
						var document = decompiler.Decompile(resourceStream).Xaml;

						var targetFilename = Path.ChangeExtension(item.Key, ".xaml");
						Console.WriteLine($"{moduleResource.Name}: {targetFilename}");

						var targetPath = Path.Combine(outputDir, targetFilename);
						var dir = Path.GetDirectoryName(targetPath);
						Directory.CreateDirectory(dir);

						document.Save(targetPath);
					}
				}
				return 0;
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine(ex);
				return 2;
			}
		}

		private static Stream GetResStream(object val)
		{
			if (val is Stream)
				return (Stream)val;
			if (val is byte[])
				return new MemoryStream((byte[])val);
			throw new InvalidDataException($"Unable to process {val.GetType().FullName}");
		}
	}
}