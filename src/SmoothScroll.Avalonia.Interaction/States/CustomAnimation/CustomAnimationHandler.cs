using Avalonia;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Rendering.Composition.Expressions;
using Avalonia.Rendering.Composition.Server;
using Avalonia.Utilities;

namespace SmoothScroll.Avalonia.Interaction;

internal abstract class CustomAnimationHandler : ServerObject, IServerClockItem
{
    private IAnimationInstance? _animationInstance;
    private readonly CompositionAnimation _animation;
    private TimeSpan _startTime;
    private readonly TimeSpan? _duration;
    protected readonly ServerInteractionTracker InteractionTracker;

    protected CustomAnimationHandler(
        ServerInteractionTracker interactionTracker,
        CompositionAnimation animation) : base(interactionTracker.Compositor)
    {
        InteractionTracker = interactionTracker;
        _animation = animation;
        if (animation is KeyFrameAnimation keyFrameAnimation)
        {
            _duration = keyFrameAnimation.Duration;
        }
    }

    public virtual void Start()
    {
        var targetProperty = InteractionTracker.GetCompositionProperty(_animation.Target!)!;
        var getVariant = targetProperty.GetVariant ?? throw new InvalidOperationException($"Unable to resolve composition property '{_animation.Target}'.");
        _animationInstance = _animation.CreateInstance(InteractionTracker, null);
        _animationInstance.Initialize(Compositor.Clock.Elapsed,
            getVariant(InteractionTracker), targetProperty);
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
            InteractionTracker.ChangeState(new ScaleInertiaState(InteractionTracker, default, 0, requestId: 0));
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

    public ScaleAnimationHandler(
        ServerInteractionTracker interactionTracker,
        CompositionAnimation animation, 
        Vector3D centerPoint) 
        : base(interactionTracker, animation)
    {
        _centerPoint = centerPoint;
    }

    public override void Start()
    {
        base.Start();
    }

    protected override void Evaluate(ExpressionVariant animationValue)
    {
        var scale = animationValue.Double;

        var modifiedScale = Math.Clamp(scale, InteractionTracker.MinScale, InteractionTracker.MaxScale);
        

        if (!MathUtilities.AreClose(modifiedScale, InteractionTracker.MinScale)
            && !MathUtilities.AreClose(modifiedScale, InteractionTracker.MaxScale))
        {
            InteractionTracker.SetScale(modifiedScale, _centerPoint , 0);
        }
    }
}

internal class PositionAnimationHandler : CustomAnimationHandler
{
    public PositionAnimationHandler(
        ServerInteractionTracker interactionTracker,
        CompositionAnimation animation)
    : base(interactionTracker, animation)
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
