using System.Runtime.InteropServices;

namespace Torgal.Pac;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 0x38)]
public record struct PacFileEntry {
	public int CompressedSize { get; set; }
	public bool IsCompressed { get; set; }
	public PackFileCompressionType CompressionType { get; set; }
	public ushort Flags { get; set; } // Engine reserved, should always be zero.
	public long UncompressedSize { get; set; }
	public long OffsetInBuffer { get; set; }
	public long TileStreamOffset { get; set; }
	public long FileNameOffset { get; set; }
	public uint FileNameHash { get; set; } // FNV1(FileName)
	public uint Checksum { get; set; } // CRC32(Data)
	public uint Reserved { get; set; } // Engine reserved, should always be zero.
	public uint TileStreamInfoSize { get; set; } // 0x18 for type 3, ?? for type 2, 0 for all other types.
}
