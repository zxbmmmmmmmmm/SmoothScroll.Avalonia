using Avalonia;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Rendering.Composition.Server;
using Avalonia.Utilities;

namespace SmoothScroll.Avalonia.Interaction;

internal class CustomAnimationHandler : ServerObject, IServerClockItem
{
    private IAnimationInstance? _animationInstance = null;
    private readonly CompositionAnimation _animation;
    private readonly InteractionTracker _interactionTracker;
    private Vector3D _initialPosition;
    private double _initialScale;
    private readonly Vector3D _centerPoint;

    public CustomAnimationHandler(
        InteractionTracker interactionTracker,
        CompositionAnimation animation,
        Vector3D centerPoint,
        ServerCompositor compositor) : base(compositor)
    {
        _interactionTracker = interactionTracker;
        _animation = animation;
        _centerPoint = centerPoint;
    }

    public void Initialize()
    {
        _initialPosition = _interactionTracker.Position;
        _initialScale = _interactionTracker.Scale;
        var targetProperty = _interactionTracker.Server.GetCompositionProperty(_animation.Target!)!;
        _animationInstance = _animation.CreateInstance(_interactionTracker.Server, null);
        _animationInstance.Initialize(Compositor.Clock.Elapsed,
            targetProperty.GetVariant!(_interactionTracker.Server), targetProperty);
        Compositor.Animations.AddToClock(this);
        Activate();
    }

    public void Stop()
    {
        Compositor.Animations.RemoveFromClock(this);
        Deactivate();
    }

    public void OnTick()
    {
        if(_animationInstance is null) return;
        if(_animation.Target == nameof(InteractionTracker.Scale))
        {
            var scale = _animationInstance.Evaluate(Compositor.Clock.Elapsed, _interactionTracker.Scale).Double;

            var scaleRatio = scale / _initialScale;
            var currentPosition = _initialPosition;
            var deltaX = (_centerPoint.X - (-currentPosition.X)) * (1 - scaleRatio);
            var deltaY = (_centerPoint.Y - (-currentPosition.Y)) * (1 - scaleRatio);

            var scaledNewPosition = new Vector3D(
                currentPosition.X - deltaX,
                currentPosition.Y - deltaY,
                currentPosition.Z);

            var modifiedScale = Math.Clamp(scale, _interactionTracker.MinScale, _interactionTracker.MaxScale);


            if (!MathUtilities.AreClose(modifiedScale, _interactionTracker.MinScale)
                && !MathUtilities.AreClose(modifiedScale, _interactionTracker.MaxScale))
            {
                _interactionTracker.SetPositionAndScale(scaledNewPosition, modifiedScale, 0);
            }
        }
        else if (_animation.Target == nameof(InteractionTracker.Position))
        {
            var position = _animationInstance.Evaluate(Compositor.Clock.Elapsed, _interactionTracker.Position).Vector3;
            var modifiedPosition = new Vector3D(
                Math.Clamp(position.X, _interactionTracker.MinPosition.X, _interactionTracker.MaxPosition.X),
                Math.Clamp(position.Y, _interactionTracker.MinPosition.Y, _interactionTracker.MaxPosition.Y),
                Math.Clamp(position.Z, _interactionTracker.MinPosition.Z, _interactionTracker.MaxPosition.Z));
            
            _interactionTracker.SetPosition(modifiedPosition, 0);
        }

    }
}
