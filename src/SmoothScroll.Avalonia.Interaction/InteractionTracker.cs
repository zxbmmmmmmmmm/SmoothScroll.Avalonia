using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Threading;
using Avalonia.Utilities;

namespace SmoothScroll.Avalonia.Interaction;

public partial class InteractionTracker : CompositionObject
{
    private int _requestId = 0;
    internal new ServerInteractionTracker Server { get; }

    internal InteractionTracker(Compositor compositor, ServerInteractionTracker server) : base(compositor, server)
    {
        Server = server;
        RunOnServerThread(static serverTracker => serverTracker.Activate());
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
            RunOnServerThread((serverTracker) => serverTracker.MinScale = value);
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
            RunOnServerThread((serverTracker) => serverTracker.MaxScale = value);
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
            RunOnServerThread((serverTracker) => serverTracker.MinPosition = value);
            _state.ReceiveBoundsUpdate();
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
            RunOnServerThread((serverTracker) => serverTracker.MaxPosition = value);
            _state.ReceiveBoundsUpdate();
        }
    }

    public Vector3D? PositionInertiaDecayRate { get; set; }

    public Vector3D Position { get; private set; }

    public double Scale { get; private set; } = 1.0;

    public int TryUpdatePosition(Vector3D value)
    => TryUpdatePosition(value, InteractionTrackerClampingOption.Auto);

    public int TryUpdatePositionBy(Vector3D amount)
        => TryUpdatePosition(Position + amount);

    public int TryUpdatePosition(Vector3D value, InteractionTrackerClampingOption option)
    {
        var id = Interlocked.Increment(ref _requestId);
        _state.TryUpdatePosition(value, option, id);
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
        var scaleFactor = scale / currentScale;
        var adjustedPosition = centerPoint + ((Position - centerPoint) * scaleFactor);
        SetPositionAndScale(adjustedPosition, scale, id);
    }

    public void TryUpdatePositionWithAnimation(CompositionAnimation animation)
    {
        if (animation is not Vector3DKeyFrameAnimation and not ExpressionAnimation)
        {
            throw new ArgumentException("Only Vector3DKeyFrameAnimation and ExpressionAnimation are supported.", nameof(animation));
        }

        animation.Target = nameof(Server.Position);
        _state.ReceiveAnimationStarting(animation);
    }

    public void TryUpdateScaleWithAnimation(CompositionAnimation animation, Vector3D centerPoint)
    {
        if (animation is not DoubleKeyFrameAnimation and not ExpressionAnimation)
        {
            throw new ArgumentException("Only DoubleKeyFrameAnimation and ExpressionAnimation are supported.", nameof(animation));
        }

        animation.Target = nameof(Server.Scale);
        _state.ReceiveAnimationStarting(animation, centerPoint);
    }

    internal void SetPosition(Vector3D newPosition, int requestId)
    {
        if (Position == newPosition)
            return;

        Position = newPosition;
        var scale = Scale;
        Compositor.Loop.Wakeup();
        RunOnServerThread((serverTracker) => serverTracker.Position = newPosition);
        NotifyValuesChanged(newPosition, scale, requestId);
    }

    internal void SetScale(double newScale, int requestId)
    {
        if (MathUtilities.AreClose(Scale, newScale))
            return;

        var position = Position;
        Scale = newScale;
        Compositor.Loop.Wakeup();
        RunOnServerThread((serverTracker) => serverTracker.Scale = newScale);
        NotifyValuesChanged(position, newScale, requestId);
    }

    internal void SetPositionAndScale(Vector3D newPosition, double newScale, int requestId)
    {
        var positionChanged = Position != newPosition;
        var scaleChanged = !MathUtilities.AreClose(Scale, newScale);
        if (positionChanged)
        {
            Position = newPosition;
        }

        if (scaleChanged)
        {
            Scale = newScale;
        }


        if (!positionChanged && !scaleChanged)
        {
            return;
        }

        Compositor.Loop.Wakeup();
        RunOnServerThread((serverTracker) =>
        {
            serverTracker.Position = newPosition;
            serverTracker.Scale = newScale;
        });
        NotifyValuesChanged(newPosition, newScale, requestId);
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


    private void NotifyValuesChanged(Vector3D position, double scale, int requestId)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            RaiseValuesChanged();
            return;
        }

        Dispatcher.UIThread.Post(RaiseValuesChanged, DispatcherPriority.Render);

        return;

        void RaiseValuesChanged()
            => Owner?.ValuesChanged(this, new InteractionTrackerValuesChangedArgs(position, scale, requestId));
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

    private readonly record struct PositionScaleState(Vector3D Position, double Scale);
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
