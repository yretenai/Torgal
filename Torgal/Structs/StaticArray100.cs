using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Torgal.Structs;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 1), InlineArray(0x100)]
public struct StaticArray100 : IEquatable<StaticArray100> {
	private byte Value;

	public string AsString() {
		Span<byte> span = this;
		var nullByte = span.IndexOf((byte) 0);
		switch (nullByte) {
			case 0:
				return string.Empty;
			case -1:
				nullByte = span.Length;
				break;
		}

		return Encoding.ASCII.GetString(span[..nullByte]);
	}

	public override bool Equals(object? obj) {
		if (obj is StaticArray100 arr) {
			return Equals(arr);
		}

		return false;
	}

	public bool Equals(StaticArray100 obj) => ((Span<byte>) this).SequenceEqual(obj);

	public bool Equals(ReadOnlySpan<byte> span) {
		var self = (Span<byte>) this;
		return span.Length == self.Length && self.SequenceEqual(span);
	}

	public bool Equals(Span<byte> span) {
		var self = (Span<byte>) this;
		return span.Length == self.Length && self.SequenceEqual(span);
	}

	public override int GetHashCode() {
		HashCode hashCode = default;
		hashCode.AddBytes((Span<byte>) this);
		return hashCode.ToHashCode();
	}

	public static bool operator ==(StaticArray100 left, StaticArray100 right) => left.Equals(right);

	public static bool operator !=(StaticArray100 left, StaticArray100 right) => !(left == right);
}
