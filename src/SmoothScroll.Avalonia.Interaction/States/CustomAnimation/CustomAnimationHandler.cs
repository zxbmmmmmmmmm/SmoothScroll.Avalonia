using Avalonia;
using Avalonia.Animation;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Rendering.Composition.Expressions;
using Avalonia.Rendering.Composition.Server;
using Avalonia.Threading;
using Avalonia.Utilities;

namespace SmoothScroll.Avalonia.Interaction;

internal abstract class CustomAnimationHandler : ServerObject, IServerClockItem
{
    private IAnimationInstance? _animationInstance = null;
    private readonly CompositionAnimation _animation;
    protected readonly InteractionTracker _interactionTracker;
    private TimeSpan _startTime;
    private TimeSpan? _duration;

    public CustomAnimationHandler(
        InteractionTracker interactionTracker,
        CompositionAnimation animation,
        ServerCompositor compositor) : base(compositor)
    {
        _interactionTracker = interactionTracker;
        _animation = animation;
        if(animation is KeyFrameAnimation keyFrameAnimation)
        {
            _duration = keyFrameAnimation.Duration;
        }
    }

    public virtual void Start()
    {
        var targetProperty = _interactionTracker.Server.GetCompositionProperty(_animation.Target!)!;
        _animationInstance = _animation.CreateInstance(_interactionTracker.Server, null);
        _animationInstance.Initialize(Compositor.Clock.Elapsed,
            targetProperty.GetVariant!(_interactionTracker.Server), targetProperty);
        _startTime = Compositor.Clock.Elapsed;
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
        var elapsed = Compositor.Clock.Elapsed;
        if (_duration is not null && elapsed - _startTime > _duration)
        {
            Stop();
            Dispatcher.UIThread.Post(() => {
                _interactionTracker.ChangeState(new InteractionTrackerInertiaState(
                    _interactionTracker,
                    default, default, 0
                    , requestId: 0, false));
            }, priority:
            DispatcherPriority.Render);
        }
        var value = _animationInstance.Evaluate(elapsed, _interactionTracker.Position);
        Evaluate(value);
    }

    protected abstract void Evaluate(ExpressionVariant animationValue);
}

internal class ScaleAnimationHandler: CustomAnimationHandler
{
    private readonly Vector3D _centerPoint;
    private Vector3D _initialPosition;
    private double _initialScale;

    public ScaleAnimationHandler(InteractionTracker interactionTracker, CompositionAnimation animation, Vector3D centerPoint, ServerCompositor compositor) 
        : base(interactionTracker, animation, compositor)
    {
        _centerPoint = centerPoint;
    }

    public override void Start()
    {
        _initialPosition = _interactionTracker.Position;
        _initialScale = _interactionTracker.Scale;
        base.Start();
    }

    protected override void Evaluate(ExpressionVariant animationValue)
    {
        var scale = animationValue.Double;

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
}

internal class PositionAnimatinoHandler : CustomAnimationHandler
{
    public PositionAnimatinoHandler(InteractionTracker interactionTracker, CompositionAnimation animation, ServerCompositor compositor)
    : base(interactionTracker, animation, compositor)
    {
    }

    protected override void Evaluate(ExpressionVariant animationValue)
    {
        var position = animationValue.Vector3;
        var modifiedPosition = new Vector3D(
            Math.Clamp(position.X, _interactionTracker.MinPosition.X, _interactionTracker.MaxPosition.X),
            Math.Clamp(position.Y, _interactionTracker.MinPosition.Y, _interactionTracker.MaxPosition.Y),
            Math.Clamp(position.Z, _interactionTracker.MinPosition.Z, _interactionTracker.MaxPosition.Z));

        _interactionTracker.SetPosition(modifiedPosition, 0);
    }
}
