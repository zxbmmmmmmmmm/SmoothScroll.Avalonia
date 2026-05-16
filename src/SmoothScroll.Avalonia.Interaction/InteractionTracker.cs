using Avalonia;
using Avalonia.Input;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Rendering.Composition.Expressions;
using Avalonia.Rendering.Composition.Server;
using Avalonia.Rendering.Composition.Transport;
using Avalonia.Utilities;

namespace SmoothScroll.Avalonia.Interaction;

public partial class InteractionTracker : CompositionObject
{
    private int _requestId = 0;

    private readonly List<InteractionTrackerRequest> _pendingRequests = [];

    internal new ServerInteractionTracker Server { get; }


    internal InteractionTracker(
        Compositor compositor,
        ServerInteractionTracker server) : base(compositor, server)
    {
        Server = server;
        RunOnServerThread(serverTracker => serverTracker.AttachClient(this));
    }


    public IInteractionTrackerOwner? Owner { get; init; }

    public int TryUpdatePosition(Vector3D value)
        => TryUpdatePosition(value, InteractionTrackerClampingOption.Auto);

    public int TryUpdatePositionBy(Vector3D amount)
        => TryUpdatePosition(Position + amount);

    public int TryUpdatePosition(Vector3D value, InteractionTrackerClampingOption option)
    {
        var id = NextRequestId();
        QueueRequest(new TryUpdatePositionRequest(id, value, option));
        return id;
    }

    public int TryUpdatePositionBy(Vector3D amount, InteractionTrackerClampingOption option)
        => TryUpdatePosition(Position + amount, option);

    public int TryUpdateScale(double scale, Vector3D centerPoint)
    {
        var id = NextRequestId();
        QueueRequest(new TryUpdateScaleRequest(id, Scale, centerPoint ));
        return id;
    }

    public int TryUpdatePositionWithAnimation(CompositionAnimation animation)
    {
        if (animation is not Vector3DKeyFrameAnimation and not ExpressionAnimation)
        {
            throw new ArgumentException("Only Vector3DKeyFrameAnimation and ExpressionAnimation are supported.", nameof(animation));
        }

        animation.Target = nameof(Server.Position);
        var id = NextRequestId();
        QueueRequest(new StartAnimationRequest(id, animation, null));
        return id;
    }

    public int TryUpdateScaleWithAnimation(CompositionAnimation animation, Vector3D centerPoint)
    {
        if (animation is not DoubleKeyFrameAnimation and not ExpressionAnimation)
        {
            throw new ArgumentException("Only DoubleKeyFrameAnimation and ExpressionAnimation are supported.", nameof(animation));
        }

        animation.Target = nameof(Server.Scale);
        var id = NextRequestId();
        QueueRequest(new StartAnimationRequest(id,animation, centerPoint));
        return id;
    }


    internal void BeginUserManipulation(Point position, IPointer pointer)
    {
        var id = NextRequestId();
        QueueRequest(new BeginUserManipulationRequest(id, position, pointer));
    }

    internal void CompleteUserManipulation()
    {
        var id = NextRequestId();
        QueueRequest(new CompleteManipulationRequest(id));
    }

    internal void ApplyManipulationDelta(Vector translationDelta)
    {
        var id = NextRequestId();
        QueueRequest(new ApplyManipulationDeltaRequest(id, translationDelta));
    }

    internal void StartInertia(Point linearVelocity)
    {
        var id = NextRequestId();
        QueueRequest(new StartInertiaRequest(id, linearVelocity));
    }

    internal void AddScaleVelocity(Point origin, double delta)
    {
        var id = NextRequestId();
        QueueRequest(new AddScaleVelocityRequest(id, origin, delta));
    }

    internal void ApplyWheelDelta(Vector delta)
    {
        var id = NextRequestId();
        QueueRequest(new ApplyWheelDeltaRequest(id, delta));
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
    private int NextRequestId() => Interlocked.Increment(ref _requestId);

    private void QueueRequest(InteractionTrackerRequest request)
    {
        _pendingRequests.Add(request);
        RegisterForSerialization();
    }

    partial void SerializeRequests(BatchStreamWriter writer)
    {
        writer.Write(_pendingRequests.Count);
        foreach(var request in _pendingRequests)
        {
            writer.WriteObject(request);
        }
        _pendingRequests.Clear();
    }
    
}

internal record InteractionTrackerRequest(int RequestId);
internal record TryUpdatePositionRequest(int RequestId, Vector3D Position, InteractionTrackerClampingOption ClampingOption) : InteractionTrackerRequest(RequestId);
internal record TryUpdateScaleRequest(int RequestId, double Scale, Vector3D CenterPoint) : InteractionTrackerRequest(RequestId);
internal record StartAnimationRequest(int RequestId, CompositionAnimation Animation, Vector3D? ScaleCenterPoint) : InteractionTrackerRequest(RequestId);
internal record BeginUserManipulationRequest(int RequestId, Point Position, IPointer Pointer) : InteractionTrackerRequest(RequestId);
internal record CompleteManipulationRequest(int RequestId) : InteractionTrackerRequest(RequestId);
internal record ApplyManipulationDeltaRequest(int RequestId, Vector TranslationDelta) : InteractionTrackerRequest(RequestId);
internal record StartInertiaRequest(int RequestId, Point LinearVelocity) : InteractionTrackerRequest(RequestId);
internal record AddScaleVelocityRequest(int RequestId, Point Origin, double Delta) : InteractionTrackerRequest(RequestId);
internal record ApplyWheelDeltaRequest(int RequestId, Vector Delta) : InteractionTrackerRequest(RequestId);


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
