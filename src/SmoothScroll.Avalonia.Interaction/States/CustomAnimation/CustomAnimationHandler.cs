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
    private IAnimationInstance? _animationInstance;
    private readonly CompositionAnimation _animation;
    private TimeSpan _startTime;
    private readonly TimeSpan? _duration;
    protected readonly InteractionTracker InteractionTracker;

    protected CustomAnimationHandler(
        InteractionTracker interactionTracker,
        CompositionAnimation animation,
        ServerCompositor compositor) : base(compositor)
    {
        InteractionTracker = interactionTracker;
        _animation = animation;
        if(animation is KeyFrameAnimation keyFrameAnimation)
        {
            _duration = keyFrameAnimation.Duration;
        }
    }

    public virtual void Start()
    {
        var targetProperty = InteractionTracker.Server.GetCompositionProperty(_animation.Target!)!;
        _animationInstance = _animation.CreateInstance(InteractionTracker.Server, null);
        _animationInstance.Initialize(Compositor.Clock.Elapsed,
            targetProperty.GetVariant!(InteractionTracker.Server), targetProperty);
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
                InteractionTracker.ChangeState(new ScaleInertiaState(
                    InteractionTracker,
                    default, 0
                    , requestId: 0));
            }, priority:
            DispatcherPriority.Render);
            return;
        }
        var value = _animationInstance.Evaluate(elapsed, InteractionTracker.Position);
        Evaluate(value);
    }

    protected abstract void Evaluate(ExpressionVariant animationValue);
}

internal class ScaleAnimationHandler: CustomAnimationHandler
{
    private readonly Vector3D _centerPoint;
    private Vector3D _initialPosition;
    private double _initialScale;

    public ScaleAnimationHandler(
        InteractionTracker interactionTracker,
        CompositionAnimation animation, 
        Vector3D centerPoint,
        ServerCompositor compositor) 
        : base(interactionTracker, animation, compositor)
    {
        _centerPoint = centerPoint;
    }

    public override void Start()
    {
        _initialPosition = InteractionTracker.Position;
        _initialScale = InteractionTracker.Scale;
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

        var modifiedScale = Math.Clamp(scale, InteractionTracker.MinScale, InteractionTracker.MaxScale);
        

        if (!MathUtilities.AreClose(modifiedScale, InteractionTracker.MinScale)
            && !MathUtilities.AreClose(modifiedScale, InteractionTracker.MaxScale))
        {
            InteractionTracker.SetPositionAndScale(scaledNewPosition, modifiedScale, 0);
        }
    }
}

internal class PositionAnimationHandler : CustomAnimationHandler
{
    public PositionAnimationHandler(
        InteractionTracker interactionTracker,
        CompositionAnimation animation, 
        ServerCompositor compositor)
    : base(interactionTracker, animation, compositor)
    {
    }

    protected override void Evaluate(ExpressionVariant animationValue)
    {
        var position = animationValue.Vector3D;
        var modifiedPosition = new Vector3D(
            Math.Clamp(position.X, InteractionTracker.MinPosition.X, InteractionTracker.MaxPosition.X),
            Math.Clamp(position.Y, InteractionTracker.MinPosition.Y, InteractionTracker.MaxPosition.Y),
            Math.Clamp(position.Z, InteractionTracker.MinPosition.Z, InteractionTracker.MaxPosition.Z));

        InteractionTracker.SetPosition(modifiedPosition, 0);
    }
}
