using System.Runtime.InteropServices;

namespace Torgal.Pac;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 0x18)]
public record struct PacTileStreamHeader {
	public long TileStreamOffset { get; set; }
	public int CompressedSize { get; set; }
	public long UncompressedSize { get; set; }
	public ushort LocalIndex { get; set; }
	public ushort BufferCount { get; set; }
}
