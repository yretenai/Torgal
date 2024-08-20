namespace Torgal.Pac;

public enum PackFileCompressionType : byte {
	None = 0,
	TileStream = 1,
	LargeTileStream = 2,
	SharedTileStream = 3,
}
