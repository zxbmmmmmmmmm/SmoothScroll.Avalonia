using Avalonia;
namespace SmoothScroll.Avalonia.Interaction;

internal sealed class PointerWheelInertiaState : InertiaState
{

    public PointerWheelInertiaState(
        ServerInteractionTracker interactionTracker,
        Vector3D translationVelocities,
        int requestId) : base(interactionTracker, requestId)
    {
        Handler = new PointerWheelInertiaHandler(
            interactionTracker,
            translationVelocities);
        EnterState();
    }
}
