using Torgal.Pac;

namespace Torgal.Unpack;

internal static class Program {
	public static int Main(string[] args) {
		if (args.Length < 2) {
			goto Help;
		}

		switch (args[1].ToLower()) {
			case "unpack":
				if (args.Length < 3) {
					goto Help;
				}

				Unpack(args[0], args[2]);
				return 0;
			case "list":
				List(args[0]);
				return 0;
		}

	Help:
		Console.Error.WriteLine("Usage:");
		Console.Error.WriteLine("\tTorgal.Unpack path/to/data unpack path/to/extract");
		Console.Error.WriteLine("\tTorgal.Unpack path/to/data list");
		return 1;
	}

	public static void Unpack(string dataPath, string outputPath) {
		foreach (var pac in EnumeratePacs(dataPath)) {
			UnpackPac(pac, outputPath);
		}
	}

	private static void UnpackPac(FaithPac pac, string outputPath) {
		var root = pac.RootPath;
		if (root.Length > 0) {
			root += "/";
		}

		if (pac.Language.Length > 0) {
			root += $"{pac.Language}/";
		}

		foreach (var (file, info) in pac.FileEntries) {
			var filePath = root + file;
			filePath = filePath.TrimStart('.', '/');
			Console.WriteLine(filePath);

			using var data = pac.OpenRead(info);
			if (data.Memory.Length < info.UncompressedSize) {
				Console.Error.WriteLine($"Failed extracting {filePath}");
				continue;
			}

			if (file.EndsWith(".pac", StringComparison.OrdinalIgnoreCase) && data.Memory.Span[..4].SequenceEqual("PACK"u8)) {
				unsafe {
					using var pin = data.Memory.Pin();
					using var pacStream = new UnmanagedMemoryStream((byte*) pin.Pointer, info.UncompressedSize);
					using var nestedPac = new FaithPac(pacStream, Path.GetFileNameWithoutExtension(file));
					Console.WriteLine($"Processing nested pac {filePath}");
					UnpackPac(nestedPac, outputPath);
				}

				continue;
			}

			var path = Path.GetFullPath(Path.Combine(outputPath, filePath));
			var dir = Path.GetDirectoryName(path) ?? outputPath;
			Directory.CreateDirectory(dir);
			using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
			stream.Write(data.Memory.Span[..(int) info.UncompressedSize]);
		}
	}

	public static void List(string data) {
		foreach (var pac in EnumeratePacs(data)) {
			var root = pac.RootPath;
			if (root.Length > 0) {
				root += "/";
			}

			if (pac.Language.Length > 0) {
				root += $"{pac.Language}/";
			}

			foreach (var (file, info) in pac.FileEntries) {
				Console.WriteLine($"{root}{file}, {info.FileNameHash:x8}, {info.Checksum:x8}, {info.IsCompressed}, {info.CompressionType:G}, {info.Flags:b16}, {info.TileStreamInfoSize:x32}, {info.CompressedSize}, {info.UncompressedSize}, {info.OffsetInBuffer}, {info.Reserved}");
			}
		}
	}

	public static IEnumerable<FaithPac> EnumeratePacs(string dataPath) {
		foreach (var file in Directory.EnumerateFiles(Path.GetFullPath(dataPath), "*.pac", SearchOption.TopDirectoryOnly)) {
			using var pac = new FaithPac(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), Path.GetFileNameWithoutExtension(file));
			yield return pac;
		}
	}
}
