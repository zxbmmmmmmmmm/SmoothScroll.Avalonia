using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Utilities;

namespace SmoothScroll.Avalonia.Interaction;

public partial class InteractionTracker : CompositionObject
{
    private int _requestId = 0;

    private InteractionTrackerState _state;

    private int _count = 0;

    internal new ServerInteractionTracker Server { get; }

    internal InteractionTracker(Compositor compositor, ServerInteractionTracker server) : base(compositor, server)
    {
        Server = server;
        Server.Activate();
        _state = new InteractionTrackerIdleState(this, 0, isInitialIdleState: true);
    }


    public IInteractionTrackerOwner? Owner { get; init; }

    public double MinScale { get; set; } = 1.0;

    public double MaxScale { get; set; } = 1.0;


    public Vector3D MinPosition
    {
        get => Server.MinPosition;
        set {
            if (Server.MinPosition == value)
                return;
            Compositor.Loop.Wakeup();
            Server.MinPosition = value;
            _state.ReceiveBoundsUpdate();
        }
    }

    public Vector3D MaxPosition
    { 
        get => Server.MaxPosition;
        set
        {
            if (Server.MaxPosition == value)
                return;
            Compositor.Loop.Wakeup();
            Server.MaxPosition = value;
            _state.ReceiveBoundsUpdate();
        }
    }

    public Vector3D? PositionInertiaDecayRate { get; set; }

    public Vector3D Position => Server.Position;

    public double Scale => Server.Scale;

    public int TryUpdatePosition(Vector3D value)
    => TryUpdatePosition(value, InteractionTrackerClampingOption.Auto);

    public int TryUpdatePositionBy(Vector3D amount)
        => TryUpdatePosition(Server.Position + amount);

    public int TryUpdatePosition(Vector3D value, InteractionTrackerClampingOption option)
    {
        var id = Interlocked.Increment(ref _requestId);
        _state.TryUpdatePosition(value, option, id);
        return id;
    }

    public int TryUpdatePositionBy(Vector3D amount, InteractionTrackerClampingOption option)
        => TryUpdatePosition(Server.Position + amount, option);

    public void TryUpdateScale(double scale, Vector3D centerPoint)
    {
        SetScale(scale, 0);
    }

    public async Task TryUpdatePositionWithAnimation(CompositionAnimation animation)
    {
        if (_state is InteractionTrackerInteractingState)
        {
            // Ignored
            return;
        }
        if (animation is Vector3DKeyFrameAnimation keyFrameAnimation)
        {
            var duration = keyFrameAnimation.Duration;
            Server.StartPositionAnimation(keyFrameAnimation);
            var state = new InteractionTrackerCustomAnimationState(this);
            ChangeState(state);
            await Task.Delay(duration);
            if (_state == state)
            {
                ChangeState(new InteractionTrackerIdleState(this, 0));
            }

        }
        else if (animation is ExpressionAnimation expressionAnimation)
        {
            Server.StartPositionAnimation(expressionAnimation);
            ChangeState(new InteractionTrackerCustomAnimationState(this));
        }
        else
        {
            throw new ArgumentException("Only Vector3DKeyFrameAnimation and ExpressionAnimation are supported.", nameof(animation));
        }
    }

    public async Task TryUpdateScaleWithAnimation(CompositionAnimation animation, Vector3D centerPoint)
    {
        if (_state is InteractionTrackerInteractingState)
        {
            // Ignored
            return;
        }
        if (animation is DoubleKeyFrameAnimation keyFrameAnimation)
        {
            var duration = keyFrameAnimation.Duration;

            var handler = new InteractionTrackerScaleAnimationHandler(this, animation, centerPoint, Server.Compositor);
            handler.Initialize();

            var state = new InteractionTrackerCustomAnimationState(this);
            ChangeState(state);
            await Task.Delay(duration);
            if (_state == state)
            {
                ChangeState(new InteractionTrackerIdleState(this, 0));
            }
            handler.Stop();
        }
        else if (animation is ExpressionAnimation)
        {

            var handler = new InteractionTrackerScaleAnimationHandler(this, animation, centerPoint, Server.Compositor);
            handler.Initialize();

            ChangeState(new InteractionTrackerCustomAnimationState(this));
        }
        else
        {
            throw new ArgumentException("Only DoubleKeyFrameAnimation and ExpressionAnimation are supported.", nameof(animation));
        }
    }

    internal void SetPosition(Vector3D newPosition, int requestId)
    {
        if (Position == newPosition)
            return;
        Compositor.Loop.Wakeup();

        Server.Position = newPosition;
        Owner?.ValuesChanged(this, new InteractionTrackerValuesChangedArgs(newPosition, Scale, requestId));
    }

    internal void SetScale(double newScale, int requestId)
    {
        if (MathUtilities.AreClose(Scale, newScale))
            return;
        Compositor.Loop.Wakeup();

        Server.Scale = newScale;
        Owner?.ValuesChanged(this, new InteractionTrackerValuesChangedArgs(Position, newScale, requestId));
    }

    internal void SetPositionAndScale(Vector3D newPosition, double newScale, int requestId)
    {
        if (MathUtilities.AreClose(Scale, newScale) && Position == newPosition)
            return;
        Compositor.Loop.Wakeup();

        Server.Position = newPosition;
        Server.Scale = newScale;
        Owner?.ValuesChanged(this, new InteractionTrackerValuesChangedArgs(newPosition, newScale, requestId));
    }

    internal void ChangeState(InteractionTrackerState newState)
    {
        Interlocked.Increment(ref _count);
        WriteStateTransition(_count, _state.Name, newState.Name);
        _state = newState;
    }


    internal void StartUserManipulation(Point position, IPointer pointer)
    {
        _state.StartUserManipulation(position, pointer);
    }

    internal void CompleteUserManipulation()
    {
        _state.CompleteUserManipulation();
    }

    internal void ReceiveManipulationDelta(Point translationDelta)
    {
        _state.ReceiveManipulationDelta(-translationDelta);
    }

    internal void ReceiveInertiaStarting(Point linearVelocity)
    {
        _state.ReceiveInertiaStarting(-linearVelocity);
    }

    internal void ReceiveScaleDelta(Point origin, double delta)
    {
        _state.ReceiveScaleDelta(origin, delta);
    }

    internal void ReceivePointerWheel(double delta, bool isHorizontal)
    {
        _state.ReceivePointerWheel(-delta, isHorizontal);
    }


    [Conditional("INTERACTION_TRACKER_TRACE")]
    private static void WriteStateTransition(int count, string previousState, string newState)
    {
        Debug.WriteLine($"{count}:{previousState} -> {newState}");
    }

}

public static class CompositorExtensions
{
    extension(Compositor compositor)
    {
        public InteractionTracker CreateInteractionTracker(IInteractionTrackerOwner? owner) =>
            new(compositor, new ServerInteractionTracker(compositor.Server))
            {
                Owner = owner
            };
    }
}
