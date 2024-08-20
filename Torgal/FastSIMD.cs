using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Torgal;

public static class FastSIMD {
	[MethodImpl(MethodConstants.InlineAndOptimize)]
	public static void Expand(Span<byte> src, Span<byte> dest) {
		if (dest.Length % src.Length > 0) {
			throw new InvalidOperationException("Source is not a multiple of destination");
		}

		for (var i = 0; i < dest.Length; i += src.Length) {
			src.CopyTo(dest[i..]);
		}
	}

	[MethodImpl(MethodConstants.InlineAndOptimize)]
	public static void Xor(Span<byte> data, ReadOnlySpan<byte> key) {
		switch (key.Length) {
			case >= 64 when Vector512.IsHardwareAccelerated && data.Length >= 64:
				Xor512(data, key);
				return;
			case >= 32 when Vector256.IsHardwareAccelerated && data.Length >= 32:
				Xor256(data, key);
				return;
			case >= 16 when Vector128.IsHardwareAccelerated && data.Length >= 16:
				Xor128(data, key);
				return;
		}

		SoftwareXor(data, key);
	}

	[MethodImpl(MethodConstants.InlineAndOptimize)]
	public static unsafe void Xor128(Span<byte> data, ReadOnlySpan<byte> key) {
		var vectorSize = Vector128<byte>.Count;
		var dataSize = data.Length;
		var dataSizeVector = dataSize - dataSize % vectorSize;
		fixed (byte* dataPtr = data) {
			fixed (byte* keyPtr = key) {
				for (var i = 0; i < dataSizeVector; i += vectorSize) {
					var dataVec = Unsafe.Read<Vector128<byte>>(dataPtr + i);
					var keyVec = Unsafe.Read<Vector128<byte>>(keyPtr + i % key.Length);
					Unsafe.Write(dataPtr + i, Vector128.Xor(dataVec, keyVec));
				}

				for (var i = dataSizeVector; i < dataSize; i++) {
					dataPtr[i] ^= keyPtr[i % key.Length];
				}
			}
		}
	}

	[MethodImpl(MethodConstants.InlineAndOptimize)]
	public static unsafe void Xor256(Span<byte> data, ReadOnlySpan<byte> key) {
		var vectorSize = Vector256<byte>.Count;
		var dataSize = data.Length;
		var dataSizeVector = dataSize - dataSize % vectorSize;
		fixed (byte* dataPtr = data) {
			fixed (byte* keyPtr = key) {
				for (var i = 0; i < dataSizeVector; i += vectorSize) {
					var dataVec = Unsafe.Read<Vector256<byte>>(dataPtr + i);
					var keyVec = Unsafe.Read<Vector256<byte>>(keyPtr + i % key.Length);
					Unsafe.Write(dataPtr + i, Vector256.Xor(dataVec, keyVec));
				}

				for (var i = dataSizeVector; i < dataSize; i++) {
					dataPtr[i] ^= keyPtr[i % key.Length];
				}
			}
		}
	}

	[MethodImpl(MethodConstants.InlineAndOptimize)]
	public static unsafe void Xor512(Span<byte> data, ReadOnlySpan<byte> key) {
		var vectorSize = Vector512<byte>.Count;
		var dataSize = data.Length;
		var dataSizeVector = dataSize - dataSize % vectorSize;
		fixed (byte* dataPtr = data) {
			fixed (byte* keyPtr = key) {
				for (var i = 0; i < dataSizeVector; i += vectorSize) {
					var dataVec = Unsafe.Read<Vector512<byte>>(dataPtr + i);
					var keyVec = Unsafe.Read<Vector512<byte>>(keyPtr + i % key.Length);
					Unsafe.Write(dataPtr + i, Vector512.Xor(dataVec, keyVec));
				}

				for (var i = dataSizeVector; i < dataSize; i++) {
					dataPtr[i] ^= keyPtr[i % key.Length];
				}
			}
		}
	}

	[MethodImpl(MethodConstants.InlineAndOptimize)]
	public static void SoftwareXor(Span<byte> data, ReadOnlySpan<byte> key) {
		var dataSize = data.Length;
		for (var i = 0; i < dataSize; i++) {
			data[i] ^= key[i % key.Length];
		}
	}
}
