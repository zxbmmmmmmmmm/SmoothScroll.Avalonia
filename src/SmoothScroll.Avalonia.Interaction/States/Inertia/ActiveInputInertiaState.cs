using Avalonia;
namespace SmoothScroll.Avalonia.Interaction;

internal sealed class ActiveInputInertiaState : InertiaState
{
    public ActiveInputInertiaState(
        ServerInteractionTracker interactionTracker,
        Vector3D translationVelocities,
        int requestId) : base(interactionTracker, requestId)
    {
        Handler = new ActiveInputInertiaHandler(
            interactionTracker,
            translationVelocities,
            RequestId);

        EnterState();
    }
}
