using System.Diagnostics;
using Avalonia;
using Avalonia.Input;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Rendering.Composition.Expressions;
using Avalonia.Rendering.Composition.Server;
using Avalonia.Rendering.Composition.Transport;
using Avalonia.Threading;
using Avalonia.Utilities;

namespace SmoothScroll.Avalonia.Interaction;

internal partial class ServerInteractionTracker
{
    private int _count;
    private InteractionTrackerState? _state;
    private InteractionTracker? _client;


    partial void Initialize()
    {
        _scale = 1;
        _positionInertiaDecayRate = new Vector3D(0.95, 0.95, 0.95);
    }

    public void AttachClient(InteractionTracker client)
    {
        _client = client;
        Activate();
        _state ??= new IdleState(this, requestId: 0, isInitialIdleState: true);
    }

    public void UpdateMinPosition(Vector3D value)
    {
        MinPosition = value;
        State.ReceiveBoundsUpdate();
    }

    public void UpdateMaxPosition(Vector3D value)
    {
        MaxPosition = value;
        State.ReceiveBoundsUpdate();
    }

    internal void SetPosition(Vector3D newPosition, int requestId)
    {
        if (Position == newPosition)
            return;
        Position = newPosition;
        NotifyValuesChanged(newPosition, Scale, requestId);
    }

    internal void SetScale(double newScale, Vector3D centerPoint, int requestId)
    {
        if (MathUtilities.AreClose(Scale, newScale))
            return;

        var scaleRatio = newScale / Scale;
        var currentPosition = Position;
        var deltaX = (centerPoint.X - (-currentPosition.X)) * (1 - scaleRatio);
        var deltaY = (centerPoint.Y - (-currentPosition.Y)) * (1 - scaleRatio);

        var scaledNewPosition = new Vector3D(
            currentPosition.X - deltaX,
            currentPosition.Y - deltaY,
            currentPosition.Z);

        Position = scaledNewPosition;
        Scale = newScale;

        NotifyValuesChanged(scaledNewPosition, newScale, requestId);
    }

    internal void ChangeState(InteractionTrackerState newState)
    {
        Interlocked.Increment(ref _count);
        WriteStateTransition(_count, _state?.Name ?? "<none>", newState.Name);
        _state = newState;
    }

    internal void NotifyIdleStateEntered(int requestId, bool isFromBinding, bool isInitialIdleState)
    {
        if (isInitialIdleState)
            return;

        PostToClient(client => client.RaiseIdleStateEntered(requestId, isFromBinding));
    }

    internal void NotifyInteractingStateEntered(int requestId, bool isFromBinding)
        => PostToClient(client => client.RaiseInteractingStateEntered(requestId, isFromBinding));

    internal void NotifyCustomAnimationStateEntered()
        => PostToClient(client => client.RaiseCustomAnimationStateEntered());

    internal void NotifyInertiaStateEntered(
        Vector3D modifiedRestingPosition,
        double modifiedRestingScale,
        Vector3D naturalRestingPosition,
        double naturalRestingScale,
        Vector3D positionVelocityInPixelsPerSecond,
        int requestId,
        float scaleVelocityInPercentPerSecond,
        bool isInertiaFromImpulse,
        bool isFromBinding)
    {
        PostToClient(client => client.RaiseInertiaStateEntered(
            modifiedRestingPosition,
            modifiedRestingScale,
            naturalRestingPosition,
            naturalRestingScale,
            positionVelocityInPixelsPerSecond,
            requestId,
            scaleVelocityInPercentPerSecond,
            isInertiaFromImpulse,
            isFromBinding));
    }

    internal void NotifyRequestIgnored(int requestId)
        => PostToClient(client => client.RaiseRequestIgnored(requestId));
    private InteractionTrackerState State => _state ??= new IdleState(this, requestId: 0, isInitialIdleState: true);

    private void NotifyValuesChanged(Vector3D position, double scale, int requestId)
        => PostToClient(client => client.RaiseValuesChanged(position, scale, requestId));

    private void PostToClient(Action<InteractionTracker> action)
    {
        var client = _client;
        if (client is null)
            return;

        Dispatcher.UIThread.Post(() => action(client), DispatcherPriority.Render);
    }

    [Conditional("INTERACTION_TRACKER_TRACE")]
    private static void WriteStateTransition(int count, string previousState, string newState)
    {
        Debug.WriteLine($"{count}:{previousState} -> {newState}");
    }

    partial void DeserializeRequests(BatchStreamReader reader)
    {
        var requestCount = reader.Read<int>();
        for (var i = 0; i < requestCount; i++)
        {
            var request = reader.ReadObject();
            switch (request)
            {
                case TryUpdatePositionRequest tryUpdatePositionRequest:
                    State.TryUpdatePosition(tryUpdatePositionRequest.Position, tryUpdatePositionRequest.ClampingOption, tryUpdatePositionRequest.RequestId);
                    break;
                case TryUpdateScaleRequest tryUpdateScaleRequest:
                    SetScale(tryUpdateScaleRequest.Scale, tryUpdateScaleRequest.CenterPoint, tryUpdateScaleRequest.RequestId);
                    break;
                case BeginUserManipulationRequest beginUserManipulationRequest:
                    State.BeginUserManipulation(beginUserManipulationRequest.Position, beginUserManipulationRequest.Pointer);
                    break;
                case CompleteManipulationRequest completeManipulationRequest:
                    State.CompleteUserManipulation();
                    break;
                case ApplyManipulationDeltaRequest applyManipulationDeltaRequest:
                    State.ApplyManipulationDelta(applyManipulationDeltaRequest.TranslationDelta);
                    break;
                case StartInertiaRequest startInertiaRequest:
                    State.StartInertia(startInertiaRequest.LinearVelocity);
                    break;
                case AddScaleVelocityRequest addScaleVelocityRequest:
                    State.AddScaleVelocity(addScaleVelocityRequest.Origin, addScaleVelocityRequest.Delta);
                    break;
                case ApplyWheelDeltaRequest applyWheelDeltaRequest:
                    State.ApplyWheelDelta(applyWheelDeltaRequest.Delta);
                    break;
                case StartAnimationRequest startAnimationRequest:
                    State.StartAnimation(startAnimationRequest.Animation, startAnimationRequest.ScaleCenterPoint);
                    break;
            }
        }
    }
}

