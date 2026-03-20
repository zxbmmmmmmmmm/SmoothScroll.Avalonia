using System.Runtime.CompilerServices;

namespace SmoothScroll.Avalonia.InteractionTracker;

internal static class CompositionMathHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsCloseReal(double a, double b, double epsilon = double.Epsilon)
        => Math.Abs(a - b) <= epsilon;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsCloseRealZero(double a, double epsilon = double.Epsilon)
        => Math.Abs(a) < epsilon;
}
