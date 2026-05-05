using Avalonia;
using Avalonia.Input;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Utilities;

namespace SmoothScroll.Avalonia.Interaction;

public partial class InteractionTracker : CompositionObject
{
    private int _requestId = 0;

    internal new ServerInteractionTracker Server { get; }

    internal InteractionTracker(Compositor compositor, ServerInteractionTracker server)
        : this(compositor, server, default, 1.0)
    {
    }

    internal InteractionTracker(
        Compositor compositor,
        ServerInteractionTracker server,
        Vector3D initialPosition,
        double initialScale) : base(compositor, server)
    {
        Server = server;
        Position = initialPosition;
        Scale = initialScale;
        server.InitializeValues(initialPosition, initialScale);
        RunOnServerThread(serverTracker => serverTracker.AttachClient(this));
    }


    public IInteractionTrackerOwner? Owner { get; init; }

    public double MinScale
    {
        get;
        set
        {
            if (MathUtilities.AreClose(field, value))
                return;

            field = value;
            Compositor.Loop.Wakeup();
            RunOnServerThread(serverTracker => serverTracker.MinScale = value);
        }
    } = 1.0;

    public double MaxScale
    {
        get;
        set
        {
            if (MathUtilities.AreClose(field, value))
                return;

            field = value;
            Compositor.Loop.Wakeup();
            RunOnServerThread(serverTracker => serverTracker.MaxScale = value);
        }
    } = 1.0;


    public Vector3D MinPosition
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            Compositor.Loop.Wakeup();
            RunOnServerThread(serverTracker => serverTracker.UpdateMinPosition(value));
        }
    }

    public Vector3D MaxPosition
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            Compositor.Loop.Wakeup();
            RunOnServerThread(serverTracker => serverTracker.UpdateMaxPosition(value));
        }
    }

    public Vector3D? PositionInertiaDecayRate
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            Compositor.Loop.Wakeup();
            RunOnServerThread(serverTracker => serverTracker.PositionInertiaDecayRate = value);
        }
    }

    public Vector3D Position { get; private set; }

    public double Scale { get; private set; } = 1.0;

    public int TryUpdatePosition(Vector3D value)
    => TryUpdatePosition(value, InteractionTrackerClampingOption.Auto);

    public int TryUpdatePositionBy(Vector3D amount)
        => TryUpdatePosition(Position + amount);

    public int TryUpdatePosition(Vector3D value, InteractionTrackerClampingOption option)
    {
        var id = Interlocked.Increment(ref _requestId);
        Compositor.Loop.Wakeup();
        RunOnServerThread(serverTracker => serverTracker.TryUpdatePosition(value, option, id));
        return id;
    }

    public int TryUpdatePositionBy(Vector3D amount, InteractionTrackerClampingOption option)
        => TryUpdatePosition(Position + amount, option);

    public void TryUpdateScale(double scale, Vector3D centerPoint)
    {
        var currentScale = Scale;
        if (MathUtilities.AreClose(currentScale, scale))
            return;

        var id = Interlocked.Increment(ref _requestId);
        Compositor.Loop.Wakeup();
        RunOnServerThread(serverTracker => serverTracker.TryUpdateScale(scale, centerPoint, id));
    }

    public void TryUpdatePositionWithAnimation(CompositionAnimation animation)
    {
        if (animation is not Vector3DKeyFrameAnimation and not ExpressionAnimation)
        {
            throw new ArgumentException("Only Vector3DKeyFrameAnimation and ExpressionAnimation are supported.", nameof(animation));
        }

        animation.Target = nameof(Server.Position);
        Compositor.Loop.Wakeup();
        RunOnServerThread(serverTracker => serverTracker.ReceiveAnimationStarting(animation));
    }

    public void TryUpdateScaleWithAnimation(CompositionAnimation animation, Vector3D centerPoint)
    {
        if (animation is not DoubleKeyFrameAnimation and not ExpressionAnimation)
        {
            throw new ArgumentException("Only DoubleKeyFrameAnimation and ExpressionAnimation are supported.", nameof(animation));
        }

        animation.Target = nameof(Server.Scale);
        Compositor.Loop.Wakeup();
        RunOnServerThread(serverTracker => serverTracker.ReceiveAnimationStarting(animation, centerPoint));
    }


    internal void StartUserManipulation(Point position, IPointer pointer)
    {
        Compositor.Loop.Wakeup();
        RunOnServerThread(serverTracker => serverTracker.StartUserManipulation(position, pointer));
    }

    internal void CompleteUserManipulation()
    {
        Compositor.Loop.Wakeup();
        RunOnServerThread(static serverTracker => serverTracker.CompleteUserManipulation());
    }

    internal void ReceiveManipulationDelta(Point translationDelta)
    {
        Compositor.Loop.Wakeup();
        RunOnServerThread(serverTracker => serverTracker.ReceiveManipulationDelta(-translationDelta));
    }

    internal void ReceiveInertiaStarting(Point linearVelocity)
    {
        Compositor.Loop.Wakeup();
        RunOnServerThread(serverTracker => serverTracker.ReceiveInertiaStarting(-linearVelocity));
    }

    internal void ReceiveScaleDelta(Point origin, double delta)
    {
        Compositor.Loop.Wakeup();
        RunOnServerThread(serverTracker => serverTracker.ReceiveScaleDelta(origin, delta));
    }

    internal void ReceivePointerWheel(double delta, bool isHorizontal)
    {
        Compositor.Loop.Wakeup();
        RunOnServerThread(serverTracker => serverTracker.ReceivePointerWheel(-delta, isHorizontal));
    }

    internal void RaiseValuesChanged(Vector3D position, double scale, int requestId)
    {
        Position = position;
        Scale = scale;
        Owner?.ValuesChanged(this, new InteractionTrackerValuesChangedArgs(position, scale, requestId));
    }

    internal void RaiseRequestIgnored(int requestId)
    {
        Owner?.RequestIgnored(this, new InteractionTrackerRequestIgnoredArgs(requestId));
    }

    internal void RaiseIdleStateEntered(int requestId, bool isFromBinding)
    {
        Owner?.IdleStateEntered(this, new InteractionTrackerIdleStateEnteredArgs(requestId, isFromBinding));
    }

    internal void RaiseInteractingStateEntered(int requestId, bool isFromBinding)
    {
        Owner?.InteractingStateEntered(this, new InteractionTrackerInteractingStateEnteredArgs(requestId, isFromBinding));
    }

    internal void RaiseCustomAnimationStateEntered()
    {
        Owner?.CustomAnimationStateEntered(this, new InteractionTrackerCustomAnimationStateEnteredArgs());
    }

    internal void RaiseInertiaStateEntered(
        Vector3D? modifiedRestingPosition,
        double? modifiedRestingScale,
        Vector3D naturalRestingPosition,
        double naturalRestingScale,
        Vector3D positionVelocityInPixelsPerSecond,
        int requestId,
        float scaleVelocityInPercentPerSecond,
        bool isInertiaFromImpulse,
        bool isFromBinding)
    {
        Owner?.InertiaStateEntered(this, new InteractionTrackerInertiaStateEnteredArgs()
        {
            ModifiedRestingPosition = modifiedRestingPosition,
            ModifiedRestingScale = modifiedRestingScale,
            NaturalRestingPosition = naturalRestingPosition,
            NaturalRestingScale = naturalRestingScale,
            PositionVelocityInPixelsPerSecond = positionVelocityInPixelsPerSecond,
            RequestId = requestId,
            ScaleVelocityInPercentPerSecond = scaleVelocityInPercentPerSecond,
            IsInertiaFromImpulse = isInertiaFromImpulse,
            IsFromBinding = isFromBinding,
        });
    }

    internal void RunOnServerThread(Action<ServerInteractionTracker> action)
    {
        if (Server.Compositor.CheckAccess())
        {
            action(Server);
            return;
        }

        Compositor.PostServerJob(() => action(Server), false);
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

        public InteractionTracker CreateInteractionTracker(
            IInteractionTrackerOwner? owner,
            Vector3D initialPosition,
            double initialScale) =>
            new(compositor, new ServerInteractionTracker(compositor.Server), initialPosition, initialScale)
            {
                Owner = owner
            };
    }
}
