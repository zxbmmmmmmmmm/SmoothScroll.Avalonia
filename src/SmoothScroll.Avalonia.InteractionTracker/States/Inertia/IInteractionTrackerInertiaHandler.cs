using Avalonia;

namespace SmoothScroll.Avalonia.InteractionTracker;

internal interface IInteractionTrackerInertiaHandler
{
    Vector3D InitialVelocity { get; }
    Vector3D FinalPosition { get; }
    Vector3D FinalModifiedPosition { get; }
    double FinalModifiedScale { get; }

    void Start();
    void Stop();
}
