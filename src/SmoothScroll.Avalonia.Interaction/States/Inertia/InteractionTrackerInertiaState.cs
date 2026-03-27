using Avalonia;
using Avalonia.Input;
using Avalonia.Utilities;

namespace SmoothScroll.Avalonia.Interaction;

internal sealed class InteractionTrackerInertiaState : InteractionTrackerState
{
    private readonly IInteractionTrackerInertiaHandler _handler;
    private readonly int _requestId;

    internal override string Name => "InertiaState";

    public InteractionTrackerInertiaState(
        InteractionTracker interactionTracker,
        Vector3D translationVelocities,
        Point scaleOrigin,
        double scaleVelocity,
        int requestId, 
        bool isFromPointerWheel) : base(interactionTracker)
    {
        _requestId = requestId;

        if (isFromPointerWheel)
        {
            if (MathUtilities.IsZero(scaleVelocity))
            {
                _handler = new InteractionTrackerPointerWheelInertiaHandler(
                    interactionTracker.Server!.Compositor,
                    interactionTracker,
                    translationVelocities);
            }
            else
            {
                _handler = new InteractionTrackerScaleInertiaHandler(
                    interactionTracker.Server!.Compositor,
                    interactionTracker,
                    scaleOrigin,
                    scaleVelocity);
            }
        }
        else
        {
            _handler = new InteractionTrackerActiveInputInertiaHandler(
                interactionTracker.Server!.Compositor,
                interactionTracker,
                translationVelocities,
                _requestId);
        }

        EnterState(interactionTracker.Owner);
    }

    protected override void EnterState(IInteractionTrackerOwner? owner)
    {
        owner?.InertiaStateEntered(_interactionTracker, new InteractionTrackerInertiaStateEnteredArgs()
        {
            IsFromBinding = false, /* TODO */
            IsInertiaFromImpulse = false, /* TODO */
            ModifiedRestingPosition = _handler.FinalModifiedPosition,
            ModifiedRestingScale = Math.Clamp(_handler.FinalModifiedScale, _interactionTracker.MinScale, _interactionTracker.MaxScale),
            NaturalRestingPosition = _handler.FinalPosition,
            NaturalRestingScale = _handler.FinalModifiedScale,
            PositionVelocityInPixelsPerSecond = _handler.InitialVelocity,
            RequestId = _requestId,
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
        //var position = _interactionTracker.Position;
        //_interactionTracker.MinPosition = Vector3.Min(_interactionTracker.MinPosition, position);
        //_interactionTracker.MaxPosition = Vector3.Max(_interactionTracker.MaxPosition, position);

        _handler.Start();
    }

    internal override void StartUserManipulation(Point position, IPointer pointer)
    {
        _interactionTracker.ChangeState(new InteractionTrackerInteractingState(_interactionTracker));
        _handler.Stop();
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

        var inputVelocity = Math.Log(delta) / 0.25;

        var accumulatedVelocity = inputVelocity;
        if (_handler is InteractionTrackerScaleInertiaHandler pw)
        {
            var isOpposite = (pw.ScaleVelocity > 0 && inputVelocity < 0) || (pw.ScaleVelocity < 0 && inputVelocity > 0);

            accumulatedVelocity = isOpposite
                ? inputVelocity
                : pw.ScaleVelocity + inputVelocity;
        }

        _interactionTracker.ChangeState(new InteractionTrackerInertiaState(
            _interactionTracker,
            translationVelocities: _handler.InitialVelocity,
            scaleOrigin: origin,
            scaleVelocity: accumulatedVelocity,
            requestId: 0,
            isFromPointerWheel: true));

        _handler.Stop();
    }

    internal override void ReceiveManipulationDelta(Point translationDelta)
    {
    }

    internal override void ReceiveInertiaStarting(Point linearVelocity)
    {
    }

    internal override void ReceivePointerWheel(int delta, bool isHorizontal)
    {
        var newDelta = isHorizontal ? new Vector3D(delta, 0, 0) : new Vector3D(0, delta, 0);
        var totalDelta = (_handler.FinalModifiedPosition - _interactionTracker.Position) + newDelta;
        var targetVelocity = Vector3D.Divide(totalDelta, 0.25);
        Vector3D velocity;

        if (_handler is InteractionTrackerPointerWheelInertiaHandler pw)
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

        _interactionTracker.ChangeState(new InteractionTrackerInertiaState(
            _interactionTracker, 
            velocity,
            default,
            0,
            requestId: 0, 
            isFromPointerWheel: true));
        _handler.Stop();
    }

    internal override void TryUpdatePositionWithAdditionalVelocity(Vector3D velocityInPixelsPerSecond, int requestId)
    {
        // Inertia is restarted (state re-enters inertia) and inertia modifiers are evaluated with requested velocity added to current velocity
        _interactionTracker.ChangeState(new InteractionTrackerInertiaState(
            _interactionTracker, 
            _handler.InitialVelocity + velocityInPixelsPerSecond,
            default,
            0,
            requestId,
            isFromPointerWheel: false));
        _handler.Stop();
    }

    internal override void TryUpdatePosition(Vector3D value, InteractionTrackerClampingOption option, int requestId)
    {
        if (option == InteractionTrackerClampingOption.Auto)
        {
            value = Vector3D.Clamp(value, _interactionTracker.MinPosition, _interactionTracker.MaxPosition);
        }

        _interactionTracker.SetPosition(value, requestId);
        _interactionTracker.ChangeState(new InteractionTrackerIdleState(_interactionTracker, requestId));
        _handler.Stop();
    }

    internal override void ReceiveBoundsUpdate()
    {
        if(_handler is InteractionTrackerActiveInputInertiaHandler activeInputInertiaHandler)
        {
            activeInputInertiaHandler.ReceiveBoundsUpdate();
        }
    }

}
