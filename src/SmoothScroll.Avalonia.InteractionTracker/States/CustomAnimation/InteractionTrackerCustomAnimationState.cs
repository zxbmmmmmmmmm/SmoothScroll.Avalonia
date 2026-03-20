using Avalonia;
using Avalonia.Input;

namespace SmoothScroll.Avalonia.InteractionTracker;

internal sealed class InteractionTrackerCustomAnimationState : InteractionTrackerState
{
    internal override string Name => "CustomAnimationState";

    public InteractionTrackerCustomAnimationState(InteractionTracker interactionTracker) : base(interactionTracker)
    {
        EnterState(interactionTracker.Owner);
    }

    protected override void EnterState(IInteractionTrackerOwner? owner)
    {
        // TODO: Args.
        owner?.CustomAnimationStateEntered(_interactionTracker, new());
    }

    internal override void StartUserManipulation(Point position, IPointer pointer)
    {
        _interactionTracker.ChangeState(new InteractionTrackerInteractingState(_interactionTracker));
    }

    internal override void CompleteUserManipulation()
    {
    }

    internal override void ReceiveScaleDelta(Point origin, double scale)
    {
    }

    internal override void ReceiveManipulationDelta(Point translationDelta)
    {
    }

    internal override void ReceiveInertiaStarting(Point linearVelocity)
    {
    }

    internal override void ReceivePointerWheel(int delta, bool isHorizontal)
    {
    }

    internal override void TryUpdatePositionWithAdditionalVelocity(Vector3D velocityInPixelsPerSecond, int requestId)
    {
        // TODO: Stop current animation. Currently, the TryUpdate[Position|Scale]WithAnimation methods are not implemented.

        // State changes to inertia with inertia modifiers evaluated using requested velocity as initial velocity.
        // TODO: inertia modifiers not yet implemented.
        _interactionTracker.ChangeState(new InteractionTrackerInertiaState(
            _interactionTracker,
            velocityInPixelsPerSecond,
            default,
            0,
            requestId,
            isFromPointerWheel: false));
    }

    internal override void TryUpdatePosition(Vector3D value, InteractionTrackerClampingOption option, int requestId)
    {
        if (option == InteractionTrackerClampingOption.Auto)
        {
            value = Vector3D.Clamp(value, _interactionTracker.MinPosition, _interactionTracker.MaxPosition);
        }

        _interactionTracker.SetPosition(value, requestId);
        _interactionTracker.ChangeState(new InteractionTrackerIdleState(_interactionTracker, requestId));
    }
}
