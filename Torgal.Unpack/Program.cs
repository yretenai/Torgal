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

	public static void Unpack(string data, string output) { }

	public static void List(string data) {
		foreach (var pac in EnumeratePacs(data)) {
			var root = pac.RootPath;
			if (root.Length > 0) {
				root = "/" + root;
			}

			foreach (var (file, info) in pac.FileEntries) {
				Console.WriteLine($"{root}/{file}, {info.FileNameHash:x8}, {info.Checksum:x8}, {info.IsCompressed}, {info.CompressionType:G}, {info.Flags:b16}, {info.TileStreamStride:x32}, {info.CompressedSize}, {info.UncompressedSize}, {info.OffsetInBuffer}, {info.Reserved}");
			}
		}
	}

	public static IEnumerable<FaithPac> EnumeratePacs(string data) {
		foreach (var file in Directory.EnumerateFiles(Path.GetFullPath(data), "*.pac", SearchOption.TopDirectoryOnly)) {
			using var pac = new FaithPac(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
			yield return pac;
		}
	}
}
