using System.Runtime.InteropServices;

namespace Torgal.Pac;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 0x8)]
public record struct PacTileStreamHeader {
	public int Count { get; set; }
	public int Size { get; set; }
}
