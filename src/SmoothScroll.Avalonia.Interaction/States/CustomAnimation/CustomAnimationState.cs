using Avalonia;
using Avalonia.Input;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Styling;

namespace SmoothScroll.Avalonia.Interaction;

internal sealed class CustomAnimationState : InteractionTrackerState
{
    internal override string Name => "CustomAnimationState";

    private readonly CustomAnimationHandler _animationHandler;

    public CustomAnimationState(
        InteractionTracker interactionTracker,
        CompositionAnimation animation,
        Vector3D? scaleCenterPoint = null) : base(interactionTracker)
    {
        EnterState(interactionTracker.Owner);
        if(scaleCenterPoint is null)
        {
            _animationHandler = new PositionAnimationHandler(interactionTracker, animation, interactionTracker.Server.Compositor);
        }
        else
        {
            _animationHandler = new ScaleAnimationHandler(interactionTracker, animation, scaleCenterPoint.Value, interactionTracker.Server.Compositor);
        }
        _animationHandler.Start();
    }

    protected override void EnterState(IInteractionTrackerOwner? owner)
    {
        // TODO: Args.
        owner?.CustomAnimationStateEntered(_interactionTracker, new());
    }

    internal override void StartUserManipulation(Point position, IPointer pointer)
    {
        _animationHandler.Stop();
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
        _animationHandler.Stop();

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
    }

    internal override void ReceiveAnimationStarting(CompositionAnimation animation, Vector3D? scaleCenterPoint = null)
    {
        _animationHandler.Stop();
        _interactionTracker.ChangeState(new CustomAnimationState(_interactionTracker, animation, scaleCenterPoint));
    }

    internal override void TryUpdatePositionWithAdditionalVelocity(Vector3D velocityInPixelsPerSecond, int requestId)
    {
        _animationHandler.Stop();
        // State changes to inertia with inertia modifiers evaluated using requested velocity as initial velocity.
        // TODO: inertia modifiers not yet implemented.
        _interactionTracker.ChangeState(new ActiveInputInertiaState(
            _interactionTracker,
            velocityInPixelsPerSecond,
            requestId));
    }

    internal override void TryUpdatePosition(Vector3D value, InteractionTrackerClampingOption option, int requestId)
    {
        _animationHandler.Stop();
        if (option == InteractionTrackerClampingOption.Auto)
        {
            value = Vector3D.Clamp(value, _interactionTracker.MinPosition, _interactionTracker.MaxPosition);
        }

        _interactionTracker.SetPosition(value, requestId);
        _interactionTracker.ChangeState(new IdleState(_interactionTracker, requestId));
    }

    internal override void ReceiveBoundsUpdate()
    {
    }
}
