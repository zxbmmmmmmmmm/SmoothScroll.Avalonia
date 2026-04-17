using Avalonia;
using Avalonia.Utilities;

namespace SmoothScroll.Avalonia.Interaction;

internal sealed class PointerWheelInertiaState : InertiaState
{
    private const double MaxPointerWheelVelocity = 8000.0;

    public PointerWheelInertiaState(
        InteractionTracker interactionTracker,
        Vector3D translationVelocities,
        int requestId) : base(interactionTracker, requestId)
    {
        Handler = new PointerWheelInertiaHandler(
            interactionTracker.Server.Compositor,
            interactionTracker,
            translationVelocities);
        EnterState(interactionTracker.Owner);
    }

    internal override void ReceivePointerWheel(double delta, bool isHorizontal)
    {
        var newDelta = isHorizontal ? new Vector3D(delta, 0, 0) : new Vector3D(0, delta, 0);
        var totalDelta = (Handler.FinalModifiedPosition - _interactionTracker.Position) + newDelta;
        var targetVelocity = Vector3D.Divide(totalDelta, 0.25);

        var handler = (PointerWheelInertiaHandler)Handler;
        var isOpposite = Vector3D.Dot(newDelta, handler.Velocity) < 0;

        var velocity = isOpposite
            ? targetVelocity
            : Vector3D.Add(handler.Velocity, targetVelocity);

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
}
