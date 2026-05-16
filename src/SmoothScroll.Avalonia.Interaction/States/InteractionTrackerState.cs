using Avalonia;
using Avalonia.Input;
using Avalonia.Rendering.Composition.Animations;

namespace SmoothScroll.Avalonia.Interaction;

internal abstract class InteractionTrackerState
{
    private protected ServerInteractionTracker _interactionTracker;

    internal abstract string Name { get; }

    protected InteractionTrackerState(ServerInteractionTracker interactionTracker)
    {
        _interactionTracker = interactionTracker;
    }

    protected abstract void EnterState();
    internal abstract void BeginUserManipulation(Point position, IPointer pointer);
    internal abstract void CompleteUserManipulation();
    internal abstract void AddScaleVelocity(Point origin, double delta);
    internal abstract void ApplyManipulationDelta(Vector translationDelta);
    internal abstract void StartInertia(Vector linearVelocity);
    internal abstract void ApplyWheelDelta(Vector delta);
    internal abstract void ReceiveBoundsUpdate();
    internal abstract void TryUpdatePositionWithAdditionalVelocity(Vector3D velocityInPixelsPerSecond, int requestId);
    internal abstract void TryUpdatePosition(Vector3D value, InteractionTrackerClampingOption option, int requestId);
    internal virtual void StartAnimation(CompositionAnimation animation, Vector3D? scaleCenterPoint = null)
    {

    }
}
