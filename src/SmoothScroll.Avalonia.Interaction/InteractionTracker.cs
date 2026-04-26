using Avalonia;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Utilities;

namespace SmoothScroll.Avalonia.Interaction;

public partial class InteractionTracker : CompositionObject
{
    private int _requestId = 0;

    internal new ServerInteractionTracker Server { get; }

    internal InteractionTracker(Compositor compositor, ServerInteractionTracker server) : base(compositor, server)
    {
        Server = server;
        RunOnServerThread(serverTracker =>
        {
            serverTracker.InitializeTracker(new InteractionTrackerNotification(this));
        });
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
            RunOnServerThread(serverTracker => serverTracker.UpdateMinScale(value));
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
            RunOnServerThread(serverTracker => serverTracker.UpdateMaxScale(value));
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
            RunOnServerThread(serverTracker => serverTracker.PositionInertiaDecayRate = value);
        }
    }

    public Vector3D Position { get; internal set; }

    public double Scale { get; internal set; } = 1.0;

    public int TryUpdatePosition(Vector3D value)
    => TryUpdatePosition(value, InteractionTrackerClampingOption.Auto);

    public int TryUpdatePositionBy(Vector3D amount)
        => TryUpdatePosition(Position + amount);

    public int TryUpdatePosition(Vector3D value, InteractionTrackerClampingOption option)
    {
        var id = Interlocked.Increment(ref _requestId);
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
        RunOnServerThread(serverTracker => serverTracker.TryUpdateScale(scale, centerPoint, id));
    }

    public void TryUpdatePositionWithAnimation(CompositionAnimation animation)
    {
        if (animation is not Vector3DKeyFrameAnimation and not ExpressionAnimation)
        {
            throw new ArgumentException("Only Vector3DKeyFrameAnimation and ExpressionAnimation are supported.", nameof(animation));
        }

        animation.Target = nameof(Server.Position);
        RunOnServerThread(serverTracker => serverTracker.TryUpdatePositionWithAnimation(animation));
    }

    public void TryUpdateScaleWithAnimation(CompositionAnimation animation, Vector3D centerPoint)
    {
        if (animation is not DoubleKeyFrameAnimation and not ExpressionAnimation)
        {
            throw new ArgumentException("Only DoubleKeyFrameAnimation and ExpressionAnimation are supported.", nameof(animation));
        }

        animation.Target = nameof(Server.Scale);
        RunOnServerThread(serverTracker => serverTracker.TryUpdateScaleWithAnimation(animation, centerPoint));
    }

    internal void StartUserManipulation(Point position)
    {
        RunOnServerThread(serverTracker => serverTracker.StartUserManipulation(position));
    }

    internal void CompleteUserManipulation()
    {
        RunOnServerThread(static serverTracker => serverTracker.CompleteUserManipulation());
    }

    internal void ReceiveManipulationDelta(Point translationDelta)
    {
        RunOnServerThread(serverTracker => serverTracker.ReceiveManipulationDelta(-translationDelta));
    }

    internal void ReceiveInertiaStarting(Point linearVelocity)
    {
        RunOnServerThread(serverTracker => serverTracker.ReceiveInertiaStarting(-linearVelocity));
    }

    internal void ReceiveScaleDelta(Point origin, double delta)
    {
        RunOnServerThread(serverTracker => serverTracker.ReceiveScaleDelta(origin, delta));
    }

    internal void ReceivePointerWheel(double delta, bool isHorizontal)
    {
        RunOnServerThread(serverTracker => serverTracker.ReceivePointerWheel(-delta, isHorizontal));
    }

    internal void RunOnServerThread(Action<ServerInteractionTracker> action)
    {
        if (Server.Compositor.CheckAccess())
        {
            action(Server);
            return;
        }

        Compositor.Loop.Wakeup();
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
    }
}
