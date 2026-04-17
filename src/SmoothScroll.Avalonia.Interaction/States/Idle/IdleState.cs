using Avalonia;
using Avalonia.Input;
using Avalonia.Rendering.Composition.Animations;

namespace SmoothScroll.Avalonia.Interaction;

internal sealed class IdleState : InteractionTrackerState
{
    private readonly bool _isInitialIdleState;
    private readonly int _requestId;

    internal override string Name => "IdleState";

    public IdleState(InteractionTracker interactionTracker, int requestId, bool isInitialIdleState = false) : base(interactionTracker)
    {
        _requestId = requestId;
        _isInitialIdleState = isInitialIdleState;
        EnterState(interactionTracker.Owner);
    }

    protected override void EnterState(IInteractionTrackerOwner? owner)
    {
        if (!_isInitialIdleState)
        {
            owner?.IdleStateEntered(_interactionTracker, new InteractionTrackerIdleStateEnteredArgs(requestId: _requestId, isFromBinding: false));
        }
    }

    internal override void StartUserManipulation(Point position, IPointer pointer)
    {
        _interactionTracker.ChangeState(new InteractingState(_interactionTracker));
    }

    internal override void CompleteUserManipulation()
    {
    }

    internal override void ReceiveScaleDelta(Point origin, double delta)
    {
        if (delta <= 0 || double.IsNaN(delta) || double.IsInfinity(delta))
        {
            return;
        }

        var scaleVelocity = Math.Log(delta) / 0.2;

        _interactionTracker.ChangeState(new ScaleInertiaState(
            _interactionTracker,
            requestId: 0,
            scaleVelocity: scaleVelocity,
            scaleOrigin: origin));
    }

    internal override void ReceiveManipulationDelta(Point translationDelta)
    {
    }

    internal override void ReceiveInertiaStarting(Point linearVelocity)
    {
    }

    internal override void ReceivePointerWheel(double delta, bool isHorizontal)
    {
        // Constant velocity for 250ms
        var velocityValue = delta / 0.25f;
        var velocity = isHorizontal ? new Vector3D(velocityValue, 0, 0) : new Vector3D(0, velocityValue, 0);
        _interactionTracker.ChangeState(new PointerWheelInertiaState(_interactionTracker, velocity, requestId: 0));
    }

    internal override void TryUpdatePositionWithAdditionalVelocity(Vector3D velocityInPixelsPerSecond, int requestId)
    {
        // State changes to inertia and inertia modifiers are evaluated with requested velocity as initial velocity
        // TODO: inertia modifiers not yet implemented.
        _interactionTracker.ChangeState(new ActiveInputInertiaState(_interactionTracker, velocityInPixelsPerSecond, requestId));
    }

    internal override void TryUpdatePosition(Vector3D value, InteractionTrackerClampingOption option, int requestId)
    {
        if (option == InteractionTrackerClampingOption.Auto)
        {
            value = Vector3D.Clamp(value, _interactionTracker.MinPosition, _interactionTracker.MaxPosition);
        }

        _interactionTracker.SetPosition(value, requestId);
    }

    internal override void ReceiveBoundsUpdate()
    {
        var position = _interactionTracker.Position;
        var clampedPosition = Vector3D.Clamp(position, _interactionTracker.MinPosition, _interactionTracker.MaxPosition);
        _interactionTracker.SetPosition(clampedPosition, 0);
    }

    internal override void ReceiveAnimationStarting(CompositionAnimation animation, Vector3D? centerPoint = null)
    {
        _interactionTracker.ChangeState(new CustomAnimationState(_interactionTracker, animation, centerPoint));
    }
}
