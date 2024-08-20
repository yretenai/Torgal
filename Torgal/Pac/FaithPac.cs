using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Torgal.Pac;

public sealed class FaithPac : IDisposable, IAsyncDisposable {
	public FaithPac(Stream stream) {
		BaseStream = stream;

		PacHeader header = default;
		stream.ReadExactly(MemoryMarshal.AsBytes(new Span<PacHeader>(ref header)));

		if (!header.Magic.Equals("PACK"u8)) {
			throw new FileLoadException("Not a PAC file!", (stream as FileStream)?.Name);
		}

		var rootPath = header.RootPath;
		Span<byte> key = stackalloc byte[64];
		FastSIMD.Expand(rootPath[^8..], key);
		FastSIMD.Xor(rootPath, key);

		Header = header with {
			RootPath = rootPath,
		};

		Debug.Assert(header.FileNameTableCount < int.MaxValue);

		var fileCount = (int) header.FileCount;
		FileBuffer = MemoryPool<PacFileEntry>.Shared.Rent(fileCount);
		stream.ReadExactly(MemoryMarshal.AsBytes(FileBuffer.Memory.Span[..fileCount]));

		var fileNameSize = (int) header.FileNameTableCount;
		FileNameBuffer = MemoryPool<byte>.Shared.Rent(fileNameSize);
		if (header.FileNameTableOffset > 0) {
			BaseStream.Position = header.FileNameTableOffset;
			var buffer = FileNameBuffer.Memory.Span[..fileNameSize];
			stream.ReadExactly(buffer);

			var alignedSize = fileNameSize - fileNameSize % 8; // :)
			FastSIMD.Xor(buffer[..alignedSize], key);

			var remain = fileNameSize - alignedSize;
			if (remain >= 4) {
				buffer[^remain--] ^= key[0];
				buffer[^remain--] ^= key[1];
				buffer[^remain--] ^= key[2];
				buffer[^remain--] ^= key[3];
			}

			if (remain >= 2) {
				buffer[^remain--] ^= key[0];
				buffer[^remain--] ^= key[1];
			}

			if (remain >= 1) {
				buffer[^remain] ^= key[0];
			}
		}

		var tileStreamCount = (int) header.TileStreamCount;
		TileStreams = MemoryPool<PacTileStreamHeader>.Shared.Rent(tileStreamCount);
		if (header.TileStreamArrayOffset > 0) {
			BaseStream.Position = header.TileStreamArrayOffset;
			stream.ReadExactly(MemoryMarshal.AsBytes(TileStreams.Memory.Span[..tileStreamCount]));
		}

		for (var index = 0; index < FileBuffer.Memory.Span[..fileCount].Length; index++) {
			var file = FileBuffer.Memory.Span[..fileCount][index];
			var fileNameOffset = file.FileNameOffset - header.FileNameTableOffset;
			Debug.Assert(fileNameOffset < int.MaxValue);
			Debug.Assert(fileNameOffset >= 0);

			var slice = FileNameBuffer.Memory.Span[..fileNameSize][(int) fileNameOffset..];
			var nullByte = slice.IndexOf((byte) 0);
			switch (nullByte) {
				case 0:
					throw new InvalidOperationException();
				case -1:
					nullByte = slice.Length;
					break;
			}

			var str = Encoding.UTF8.GetString(slice[..nullByte]);
			FileNameIndex[str] = index;
		}
	}

	public PacHeader Header { get; }
	public string RootPath => Header.RootPath.AsString();
	private IMemoryOwner<PacTileStreamHeader> TileStreams { get; }
	private IMemoryOwner<PacFileEntry> FileBuffer { get; }
	private IMemoryOwner<byte> FileNameBuffer { get; }
	private Dictionary<string, int> FileNameIndex { get; } = [];
	public IEnumerable<string> Paths => FileNameIndex.Keys;
	public IEnumerable<(string Path, PacFileEntry Entry)> FileEntries => FileNameIndex.Select(x => (x.Key, FileBuffer.Memory.Span[x.Value]));
	public Stream BaseStream { get; }

	public void Dispose() {
		BaseStream.Dispose();
		TileStreams.Dispose();
		FileBuffer.Dispose();
		FileNameBuffer.Dispose();
	}

	public async ValueTask DisposeAsync() {
		await BaseStream.DisposeAsync();
		TileStreams.Dispose();
		FileBuffer.Dispose();
		FileNameBuffer.Dispose();
	}
}
