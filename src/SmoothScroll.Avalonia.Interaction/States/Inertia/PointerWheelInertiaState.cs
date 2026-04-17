using Avalonia;
using Avalonia.Utilities;

namespace SmoothScroll.Avalonia.Interaction;

internal sealed class PointerWheelInertiaState : InertiaState
{

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
}
