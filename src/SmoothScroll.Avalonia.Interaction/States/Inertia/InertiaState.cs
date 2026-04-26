using Avalonia;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Utilities;

namespace SmoothScroll.Avalonia.Interaction;

internal abstract class InertiaState : InteractionTrackerState
{
    private const double MaxPointerWheelVelocity = 8000.0;

    protected IInteractionTrackerInertiaHandler Handler = null!;

    protected readonly int RequestId;

    internal override string Name => "InertiaState";

    protected InertiaState(
        ServerInteractionTracker interactionTracker,
        int requestId) : base(interactionTracker)
    {
        RequestId = requestId;
    }

    protected sealed override void EnterState()
    {
        _interactionTracker.NotifyInertiaStateEntered(new InteractionTrackerInertiaStateEnteredArgs()
        {
            IsFromBinding = false, /* TODO */
            IsInertiaFromImpulse = false, /* TODO */
            ModifiedRestingPosition = Handler.FinalModifiedPosition,
            ModifiedRestingScale = Math.Clamp(Handler.FinalModifiedScale, _interactionTracker.MinScale, _interactionTracker.MaxScale),
            NaturalRestingPosition = Handler.FinalPosition,
            NaturalRestingScale = Handler.FinalModifiedScale,
            PositionVelocityInPixelsPerSecond = Handler.InitialVelocity,
            RequestId = RequestId,
            ScaleVelocityInPercentPerSecond = 0.0f, /* TODO: Scale not yet implemented */
        });

        // If TryUpdatePosition is called with clamping option disabled, the position set can go outside the [MinPosition..MaxPosition] range.
        // We adjust MinPosition/MaxPosition when we enter idle.
        // Docs around InteractionTrackerClampingOption.Disabled: https://learn.microsoft.com/uwp/api/windows.ui.composition.interactions.interactiontrackerclampingoption
        // > If the input value is greater (or less) than the max (or min) value, it is not immediately
        // > clamped. Instead, the max/min is enforced to the newly input value
        // > of Position (and potentially clamped) the next time InteractionTracker enters
        // > the Inertia state.
        // TODO: Commented out for now. It's wrong to do this when transitioning from interacting to inertia.
       
        //var position = InteractionTracker.Position;
        //InteractionTracker.MinPosition = Vector3.Min(InteractionTracker.MinPosition, position);
        //InteractionTracker.MaxPosition = Vector3.Max(InteractionTracker.MaxPosition, position);

        Handler.Start();
    }

    internal override void StartUserManipulation(Point position)
    {
        Handler.Stop();
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

        Handler.Stop();

        var inputVelocity = Math.Log(delta) / 0.2;

        var accumulatedVelocity = inputVelocity;
        if (Handler is ScaleInertiaHandler pw)
        {
            var isOpposite = (pw.ScaleVelocity > 0 && inputVelocity < 0) || (pw.ScaleVelocity < 0 && inputVelocity > 0);

            accumulatedVelocity = isOpposite
                ? inputVelocity
                : pw.ScaleVelocity + inputVelocity;
        }

        _interactionTracker.ChangeState(new ScaleInertiaState(
            _interactionTracker,
            origin,
            accumulatedVelocity,
            0));
    }

    internal override void ReceiveManipulationDelta(Point translationDelta)
    {
    }

    internal override void ReceiveInertiaStarting(Point linearVelocity)
    {
    }

    internal override void ReceivePointerWheel(double delta, bool isHorizontal)
    {
        var newDelta = isHorizontal ? new Vector3D(delta, 0, 0) : new Vector3D(0, delta, 0);
        var totalDelta = (Handler.FinalModifiedPosition - _interactionTracker.Position) + newDelta;
        var targetVelocity = Vector3D.Divide(totalDelta, 0.25);

        Vector3D velocity;

        if (Handler is PointerWheelInertiaHandler pw)
        {
            var isOpposite = Vector3D.Dot(newDelta, pw.Velocity) < 0;

            velocity = isOpposite
                ? targetVelocity
                : Vector3D.Add(pw.Velocity, targetVelocity);
        }
        else
        {
            velocity = targetVelocity;
        }

        if (velocity.Length > MaxPointerWheelVelocity)
        {
            velocity = Vector3D.Multiply(velocity, MaxPointerWheelVelocity / velocity.Length);
        }

        _interactionTracker.ChangeState(new PointerWheelInertiaState(
            _interactionTracker,
            velocity,
            requestId: 0));
        Handler.Stop();
    }

    internal override void TryUpdatePositionWithAdditionalVelocity(Vector3D velocityInPixelsPerSecond, int requestId)
    {
        // Inertia is restarted (state re-enters inertia) and inertia modifiers are evaluated with requested velocity added to current velocity
        _interactionTracker.ChangeState(new ActiveInputInertiaState(
            _interactionTracker, 
            Handler.InitialVelocity + velocityInPixelsPerSecond,
            requestId));
        Handler.Stop();
    }

    internal override void TryUpdatePosition(Vector3D value, InteractionTrackerClampingOption option, int requestId)
    {
        if (option == InteractionTrackerClampingOption.Auto)
        {
            value = Vector3D.Clamp(value, _interactionTracker.MinPosition, _interactionTracker.MaxPosition);
        }

        _interactionTracker.SetPosition(value, requestId);
        _interactionTracker.ChangeState(new IdleState(_interactionTracker, requestId));
        Handler.Stop();
    }

    internal override void ReceiveBoundsUpdate()
    {
        if(Handler is ActiveInputInertiaHandler activeInputInertiaHandler)
        {
            activeInputInertiaHandler.ReceiveBoundsUpdate();
        }
    }

    internal override void ReceiveAnimationStarting(CompositionAnimation animation, Vector3D? scaleCenterPoint = null)
    {
        Handler.Stop();
        _interactionTracker.ChangeState(new CustomAnimationState(_interactionTracker, animation, scaleCenterPoint));
    }

}
