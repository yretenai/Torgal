using System.Runtime.CompilerServices;

namespace Torgal;

public static class MethodConstants {
	public const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;
#if DEBUG
	public const MethodImplOptions Optimize = 0;
#else
	public const MethodImplOptions Optimize = MethodImplOptions.AggressiveOptimization;
#endif
	public const MethodImplOptions InlineAndOptimize = Inline | Optimize;
}
