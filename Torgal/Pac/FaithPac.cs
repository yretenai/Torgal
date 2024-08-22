using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using GDeflateNet;

namespace Torgal.Pac;

public sealed class FaithPac : IDisposable, IAsyncDisposable {
	public FaithPac(Stream stream, string filename) {
		BaseStream = stream;

		PacHeader header = default;
		stream.ReadExactly(MemoryMarshal.AsBytes(new Span<PacHeader>(ref header)));

		if (!header.Magic.Equals("PACK"u8)) {
			throw new FileLoadException("Not a PAC file!", (stream as FileStream)?.Name);
		}

		var period = filename.IndexOf('.', StringComparison.Ordinal);
		if (period > -1) {
			Language = filename[(period + 1)..];
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
		TileStreams = MemoryPool<PacSharedTileStreamHeader>.Shared.Rent(tileStreamCount);
		if (header.GlobalTileStreamOffset > 0) {
			BaseStream.Position = header.GlobalTileStreamOffset;
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

			Debug.Assert(file.UncompressedSize < int.MaxValue);
		}
	}

	public PacHeader Header { get; }
	public string RootPath => Header.RootPath.AsString();
	public string Language { get; } = string.Empty;
	private IMemoryOwner<PacSharedTileStreamHeader> TileStreams { get; }
	private IMemoryOwner<PacFileEntry> FileBuffer { get; }
	private IMemoryOwner<byte> FileNameBuffer { get; }
	private Dictionary<string, int> FileNameIndex { get; } = [];
	public IEnumerable<string> Paths => FileNameIndex.Keys;
	public IEnumerable<(string Path, PacFileEntry Entry)> FileEntries => FileNameIndex.Select(x => (x.Key, FileBuffer.Memory.Span[x.Value])).OrderBy(x => x.Item2.TileStreamOffset);
	public Stream BaseStream { get; }
	private IMemoryOwner<byte>? LastSharedStream { get; set; }
	private int LastSharedStreamIndex { get; set; }

	public async ValueTask DisposeAsync() {
		await BaseStream.DisposeAsync();
		TileStreams.Dispose();
		FileBuffer.Dispose();
		FileNameBuffer.Dispose();
	}

	public void Dispose() {
		BaseStream.Dispose();
		TileStreams.Dispose();
		FileBuffer.Dispose();
		FileNameBuffer.Dispose();
	}

	public IMemoryOwner<byte> OpenRead(PacFileEntry file) {
		Debug.Assert(file.UncompressedSize < int.MaxValue);
		const int FaithBlockSize = 0x80000;
		const int TileStreamBlockSize = 0x10000;

		// align buffer size to TileStreamBlockSize just in case.
		// the TileStream decompressor assumes the buffer to be a multiple of TileStreamBlockSize.
		// which it is unless it's the last tile.
		var bufferSize = (int) file.UncompressedSize;
		var alignedSize = (bufferSize + (TileStreamBlockSize - 1)) & ~(TileStreamBlockSize - 1);
		var buffer = MemoryPool<byte>.Shared.Rent(alignedSize);
		var realBuffer = buffer.Memory[..bufferSize];

		switch (file.CompressionType) {
			case PackFileCompressionType.None: {
				BaseStream.Position = file.OffsetInBuffer;
				BaseStream.ReadExactly(realBuffer.Span);
				break;
			}
			case PackFileCompressionType.TileStream: {
				OpenTileStream(file.OffsetInBuffer, file.CompressedSize, realBuffer);
				break;
			}
			case PackFileCompressionType.LargeTileStream: {
				Span<byte> info = stackalloc byte[(int) file.TileStreamInfoSize];
				BaseStream.Position = file.TileStreamOffset;
				BaseStream.ReadExactly(info);
				var header = MemoryMarshal.Read<PacTileStreamHeader>(info);

				var decompressedOffset = 0;
				var lastBlockSize = header.Size - header.Size / FaithBlockSize * FaithBlockSize;
				for (var i = 0; i < header.Count; ++i) {
					var offset = MemoryMarshal.Read<int>(info[(Unsafe.SizeOf<PacTileStreamHeader>() + (i << 2))..]);
					var size = lastBlockSize;
					if (i < header.Count - 1) {
						var next = MemoryMarshal.Read<int>(info[(Unsafe.SizeOf<PacTileStreamHeader>() + ((i + 1) << 2))..]);
						size = next - offset;
					}

					OpenTileStream(file.OffsetInBuffer + offset, size, realBuffer[decompressedOffset..]);
					decompressedOffset += FaithBlockSize;
				}

				break;
			}
			case PackFileCompressionType.SharedTileStream: {
				var index = (int) ((file.TileStreamOffset - Header.GlobalTileStreamOffset) / Unsafe.SizeOf<PacSharedTileStreamHeader>());
				if (LastSharedStreamIndex != index || LastSharedStream == null) {
					LastSharedStream?.Dispose();
					LastSharedStreamIndex = index;

					var tileStreamInfo = TileStreams.Memory.Span[index];
					Debug.Assert(tileStreamInfo.UncompressedSize < int.MaxValue);

					LastSharedStream = MemoryPool<byte>.Shared.Rent((int) tileStreamInfo.UncompressedSize);
					OpenTileStream(tileStreamInfo.TileStreamOffset, tileStreamInfo.CompressedSize, LastSharedStream.Memory);
				}

				LastSharedStream.Memory.Span.Slice((int) file.OffsetInBuffer, (int) file.UncompressedSize).CopyTo(realBuffer.Span);
				break;
			}
			default: throw new ArgumentOutOfRangeException();
		}

		return buffer;
	}

	private void OpenTileStream(long offset, int size, Memory<byte> data) {
		BaseStream.Position = offset;
		using var buffer = MemoryPool<byte>.Shared.Rent(size);
		BaseStream.ReadExactly(buffer.Memory.Span[..size]);
		if (!GDeflate.Decompress(buffer.Memory[..size], data, 1)) {
			throw new InvalidOperationException("Failed to decompress!");
		}
	}
}
