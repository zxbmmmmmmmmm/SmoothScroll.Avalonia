using Avalonia;

namespace SmoothScroll.Avalonia.InteractionTracker;

public partial class InteractionTrackerInertiaStateEnteredArgs
{
    internal InteractionTrackerInertiaStateEnteredArgs()
    {
    }

    public required Vector3D? ModifiedRestingPosition { get; init; }

    public required double? ModifiedRestingScale { get; init; }

    public required Vector3D NaturalRestingPosition { get; init; }

    public required double NaturalRestingScale { get; init; }

    public required Vector3D PositionVelocityInPixelsPerSecond { get; init; }

    public required int RequestId { get; init; }

    public required float ScaleVelocityInPercentPerSecond { get; init; }

    public required bool IsInertiaFromImpulse { get; init; }

    public required bool IsFromBinding { get; init; }
}
