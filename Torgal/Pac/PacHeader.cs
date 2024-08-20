using System.Runtime.InteropServices;
using Torgal.Structs;

namespace Torgal.Pac;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 0x400)]
public record struct PacHeader {
	public StaticArray4 Magic { get; set; }
	public uint HeaderSize { get; set; }
	public uint FileCount { get; set; }
	public bool IsStreamed { get; set; }
	public bool IsEncrypted { get; set; }
	public ushort TileStreamCount { get; set; }
	public long Size { get; set; }
	public StaticArray100 RootPath { get; set; }
	public long TileStreamArrayOffset { get; set; }
	public long FileNameTableOffset { get; set; }
	public long FileNameTableCount { get; set; }
}
