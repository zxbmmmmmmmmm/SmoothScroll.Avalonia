using System.Numerics;
using Avalonia.Rendering.Composition.Animations;
using Vector = Avalonia.Vector;

namespace SmoothScroll.Avalonia.Controls;

public sealed class ScrollAnimationStartingEventArgs
{
    public CompositionAnimation Animation { get; set; }

    public Vector EndPosition { get; init; }

    public Vector StartingPosition { get; init; }

    internal ScrollAnimationStartingEventArgs(CompositionAnimation animation, Vector startingPosition, Vector endPosition)
    {
        Animation = animation;
        StartingPosition = startingPosition;
        EndPosition = endPosition;
    }
}
