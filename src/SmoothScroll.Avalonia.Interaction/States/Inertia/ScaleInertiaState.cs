using Avalonia;
using Avalonia.Utilities;

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
            interactionTracker.Compositor,
            interactionTracker,
            scaleOrigin,
            scaleVelocity);
        EnterState();
    }
}
