using Avalonia;
using Avalonia.Input;
using Avalonia.Rendering.Composition.Animations;

namespace SmoothScroll.Avalonia.Interaction;

internal sealed class IdleState : InteractionTrackerState
{
    private readonly bool _isInitialIdleState;
    private readonly int _requestId;

    internal override string Name => "IdleState";

    public IdleState(ServerInteractionTracker interactionTracker, int requestId, bool isInitialIdleState = false) : base(interactionTracker)
    {
        _requestId = requestId;
        _isInitialIdleState = isInitialIdleState;
        EnterState();
    }

    protected override void EnterState()
    {
        _interactionTracker.NotifyIdleStateEntered(_requestId, isFromBinding: false, _isInitialIdleState);
    }

    internal override void BeginUserManipulation(Point position, IPointer pointer)
    {
        _interactionTracker.ChangeState(new InteractingState(_interactionTracker));
    }

    internal override void CompleteUserManipulation()
    {
    }

    internal override void AddScaleVelocity(Point origin, double delta)
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

    internal override void ApplyManipulationDelta(Vector translationDelta)
    {
    }

    internal override void StartInertia(Vector linearVelocity)
    {
    }

    internal override void ApplyWheelDelta(Vector delta)
    {
        // Constant velocity for 250ms
        var velocityValue = delta / 0.25f;
        var velocity = new Vector3D(velocityValue.X, velocityValue.Y, 0);
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

    internal override void StartAnimation(CompositionAnimation animation, Vector3D? centerPoint = null)
    {
        _interactionTracker.ChangeState(new CustomAnimationState(_interactionTracker, animation, centerPoint));
    }
}
