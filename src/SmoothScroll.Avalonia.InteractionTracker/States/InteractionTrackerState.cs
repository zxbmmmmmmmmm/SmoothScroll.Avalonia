using Avalonia;
using Avalonia.Input;

namespace SmoothScroll.Avalonia.InteractionTracker;

internal abstract class InteractionTrackerState
{
    private protected InteractionTracker _interactionTracker;

    internal abstract string Name { get; }

    protected InteractionTrackerState(InteractionTracker interactionTracker)
    {
        _interactionTracker = interactionTracker;
    }

    protected abstract void EnterState(IInteractionTrackerOwner? owner);
    internal abstract void StartUserManipulation(Point position, IPointer pointer);
    internal abstract void CompleteUserManipulation();
    internal abstract void ReceiveScaleDelta(Point origin, double delta);
    internal abstract void ReceiveManipulationDelta(Point translationDelta);
    internal abstract void ReceiveInertiaStarting(Point linearVelocity);
    internal abstract void ReceivePointerWheel(int delta, bool isHorizontal);
    internal abstract void TryUpdatePositionWithAdditionalVelocity(Vector3D velocityInPixelsPerSecond, int requestId);
    internal abstract void TryUpdatePosition(Vector3D value, InteractionTrackerClampingOption option, int requestId);
}
