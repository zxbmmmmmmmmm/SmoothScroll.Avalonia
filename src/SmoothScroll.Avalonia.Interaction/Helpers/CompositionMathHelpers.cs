using System.Runtime.CompilerServices;

namespace SmoothScroll.Avalonia.Interaction;

public static class CompositionMathHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCloseReal(double a, double b, double epsilon = double.Epsilon)
        => Math.Abs(a - b) <= epsilon;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCloseRealZero(double a, double epsilon = double.Epsilon)
        => Math.Abs(a) < epsilon;
}
