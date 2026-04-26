using Avalonia;
namespace SmoothScroll.Avalonia.Interaction;

internal class ScaleInertiaState : InertiaState
{
    public ScaleInertiaState(
        ServerInteractionTracker interactionTracker,
        Point scaleOrigin,
        double scaleVelocity,
        int requestId) : base(interactionTracker, requestId)
    {
        Handler = new ScaleInertiaHandler(
            interactionTracker,
            scaleOrigin,
            scaleVelocity);
        EnterState();
    }
}
