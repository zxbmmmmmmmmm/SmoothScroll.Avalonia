using System.Diagnostics;
using Avalonia;
using Avalonia.Input;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
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

    public Vector3D? PositionInertiaDecayRate { get; set; }

    partial void Initialize()
    {
        _scale = 1;
    }

    internal void InitializeValues(Vector3D position, double scale)
    {
        _position = position;
        _scale = scale;
    }

    public void AttachClient(InteractionTracker client)
    {
        _client = client;
        Activate();
        _state ??= new IdleState(this, requestId: 0, isInitialIdleState: true);
    }

    public override CompositionProperty? GetCompositionProperty(string name)
    {
        if (name == "Position")
            return s_IdOfPositionProperty;
        if (name == "Scale")
            return s_IdOfScaleProperty;
        if (name == "MinPosition")
            return s_IdOfMinPositionProperty;
        if (name == "MaxPosition")
            return s_IdOfMaxPositionProperty;
        if (name == "MinScale")
            return s_IdOfMinScaleProperty;
        if (name == "MaxScale")
            return s_IdOfMaxScaleProperty;
        return base.GetCompositionProperty(name);
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

    public void TryUpdatePosition(Vector3D value, InteractionTrackerClampingOption option, int requestId)
        => State.TryUpdatePosition(value, option, requestId);

    public void TryUpdateScale(double scale, Vector3D centerPoint, int requestId)
        => SetScale(scale, centerPoint, requestId);

    public void StartUserManipulation(Point position, IPointer pointer)
        => State.StartUserManipulation(position, pointer);

    public void CompleteUserManipulation()
        => State.CompleteUserManipulation();

    public void ReceiveManipulationDelta(Point translationDelta)
        => State.ReceiveManipulationDelta(translationDelta);

    public void ReceiveInertiaStarting(Point linearVelocity)
        => State.ReceiveInertiaStarting(linearVelocity);

    public void ReceiveScaleDelta(Point origin, double delta)
        => State.ReceiveScaleDelta(origin, delta);

    public void ReceivePointerWheel(double delta, bool isHorizontal)
        => State.ReceivePointerWheel(delta, isHorizontal);

    public void ReceiveAnimationStarting(CompositionAnimation animation, Vector3D? scaleCenterPoint = null)
        => State.ReceiveAnimationStarting(animation, scaleCenterPoint);

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

    public void StartPositionAnimation(CompositionAnimation animation)
    {
        var instance = animation.CreateInstance(this, null);
        GetOrCreateAnimations().OnSetAnimatedValue(s_IdOfPositionProperty,
            ref _position, Compositor.Clock.Elapsed, instance);
    }

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
}

/// <summary>
/// Generated code.
/// </summary>
partial class ServerInteractionTracker : ServerObject
{
    internal ServerInteractionTracker(ServerCompositor compositor) : base(compositor)
    {
        Initialize();
    }

    partial void Initialize();
    partial void DeserializeChangesExtra(BatchStreamReader c);
    Vector3D _position;
    public Vector3D Position { get => _position; set => SetAnimatedValue(s_IdOfPositionProperty, out _position, value); }

    internal readonly static CompositionProperty<Vector3D> s_IdOfPositionProperty = CompositionProperty.Register<ServerInteractionTracker, Vector3D>("Position", obj => ((ServerInteractionTracker)obj)._position, (obj, v) => ((ServerInteractionTracker)obj)._position = v, obj => ((ServerInteractionTracker)obj)._position);
    Vector3D _minPosition;
    public Vector3D MinPosition { get => _minPosition; set => SetAnimatedValue(s_IdOfMinPositionProperty, out _minPosition, value); }

    internal readonly static CompositionProperty<Vector3D> s_IdOfMinPositionProperty = CompositionProperty.Register<ServerInteractionTracker, Vector3D>("MinPosition", obj => ((ServerInteractionTracker)obj)._minPosition, (obj, v) => ((ServerInteractionTracker)obj)._minPosition = v, obj => ((ServerInteractionTracker)obj)._minPosition);
    Vector3D _maxPosition;
    public Vector3D MaxPosition { get => _maxPosition; set => SetAnimatedValue(s_IdOfMaxPositionProperty, out _maxPosition, value); }

    internal readonly static CompositionProperty<Vector3D> s_IdOfMaxPositionProperty = CompositionProperty.Register<ServerInteractionTracker, Vector3D>("MaxPosition", obj => ((ServerInteractionTracker)obj)._maxPosition, (obj, v) => ((ServerInteractionTracker)obj)._maxPosition = v, obj => ((ServerInteractionTracker)obj)._maxPosition);
    double _minScale;
    public double MinScale { get => _minScale; set => SetAnimatedValue(s_IdOfMinScaleProperty, out _minScale, value); }

    internal readonly static CompositionProperty<double> s_IdOfMinScaleProperty = CompositionProperty.Register<ServerInteractionTracker, double>("MinScale", obj => ((ServerInteractionTracker)obj)._minScale, (obj, v) => ((ServerInteractionTracker)obj)._minScale = v, obj => ((ServerInteractionTracker)obj)._minScale);
    double _maxScale;
    public double MaxScale { get => _maxScale; set => SetAnimatedValue(s_IdOfMaxScaleProperty, out _maxScale, value); }

    internal readonly static CompositionProperty<double> s_IdOfMaxScaleProperty = CompositionProperty.Register<ServerInteractionTracker, double>("MaxScale", obj => ((ServerInteractionTracker)obj)._maxScale, (obj, v) => ((ServerInteractionTracker)obj)._maxScale = v, obj => ((ServerInteractionTracker)obj)._maxScale);
    double _scale;
    public double Scale { get => _scale; set => SetAnimatedValue(s_IdOfScaleProperty, out _scale, value); }

    internal readonly static CompositionProperty<double> s_IdOfScaleProperty = CompositionProperty.Register<ServerInteractionTracker, double>("Scale", obj => ((ServerInteractionTracker)obj)._scale, (obj, v) => ((ServerInteractionTracker)obj)._scale = v, obj => ((ServerInteractionTracker)obj)._scale);
    protected override void DeserializeChangesCore(BatchStreamReader reader, TimeSpan committedAt)
    {
        base.DeserializeChangesCore(reader, committedAt);
        DeserializeChangesExtra(reader);
        var changed = reader.Read<InteractionTrackerChangedFields>();
        if ((changed & InteractionTrackerChangedFields.PositionAnimated) == InteractionTrackerChangedFields.PositionAnimated)
            SetAnimatedValue(s_IdOfPositionProperty, ref _position, committedAt, reader.ReadObject<IAnimationInstance>());
        else if ((changed & InteractionTrackerChangedFields.Position) == InteractionTrackerChangedFields.Position)
            Position = reader.Read<Vector3D>();
        if ((changed & InteractionTrackerChangedFields.MinPositionAnimated) == InteractionTrackerChangedFields.MinPositionAnimated)
            SetAnimatedValue(s_IdOfMinPositionProperty, ref _minPosition, committedAt, reader.ReadObject<IAnimationInstance>());
        else if ((changed & InteractionTrackerChangedFields.MinPosition) == InteractionTrackerChangedFields.MinPosition)
            MinPosition = reader.Read<Vector3D>();
        if ((changed & InteractionTrackerChangedFields.MaxPositionAnimated) == InteractionTrackerChangedFields.MaxPositionAnimated)
            SetAnimatedValue(s_IdOfMaxPositionProperty, ref _maxPosition, committedAt, reader.ReadObject<IAnimationInstance>());
        else if ((changed & InteractionTrackerChangedFields.MaxPosition) == InteractionTrackerChangedFields.MaxPosition)
            MaxPosition = reader.Read<Vector3D>();
        if ((changed & InteractionTrackerChangedFields.MinScaleAnimated) == InteractionTrackerChangedFields.MinScaleAnimated)
            SetAnimatedValue(s_IdOfMinScaleProperty, ref _minScale, committedAt, reader.ReadObject<IAnimationInstance>());
        else if ((changed & InteractionTrackerChangedFields.MinScale) == InteractionTrackerChangedFields.MinScale)
            MinScale = reader.Read<double>();
        if ((changed & InteractionTrackerChangedFields.MaxScaleAnimated) == InteractionTrackerChangedFields.MaxScaleAnimated)
            SetAnimatedValue(s_IdOfMaxScaleProperty, ref _maxScale, committedAt, reader.ReadObject<IAnimationInstance>());
        else if ((changed & InteractionTrackerChangedFields.MaxScale) == InteractionTrackerChangedFields.MaxScale)
            MaxScale = reader.Read<double>();
        if ((changed & InteractionTrackerChangedFields.ScaleAnimated) == InteractionTrackerChangedFields.ScaleAnimated)
            SetAnimatedValue(s_IdOfScaleProperty, ref _scale, committedAt, reader.ReadObject<IAnimationInstance>());
        else if ((changed & InteractionTrackerChangedFields.Scale) == InteractionTrackerChangedFields.Scale)
            Scale = reader.Read<double>();
        OnFieldsDeserialized(changed);
    }

    partial void OnFieldsDeserialized(InteractionTrackerChangedFields changed);
    internal static void SerializeAllChanges(BatchStreamWriter writer, Vector3D position, Vector3D minPosition, Vector3D maxPosition, double minScale, double maxScale, double scale)
    {
        writer.Write(InteractionTrackerChangedFields.Position | InteractionTrackerChangedFields.MinPosition | InteractionTrackerChangedFields.MaxPosition | InteractionTrackerChangedFields.MinScale | InteractionTrackerChangedFields.MaxScale | InteractionTrackerChangedFields.Scale);
        writer.Write(position);
        writer.Write(minPosition);
        writer.Write(maxPosition);
        writer.Write(minScale);
        writer.Write(maxScale);
        writer.Write(scale);
    }
}
[System.Flags]
enum InteractionTrackerChangedFields : ushort
{
    Position = 1,
    PositionAnimated = 2,
    MinPosition = 4,
    MinPositionAnimated = 8,
    MaxPosition = 16,
    MaxPositionAnimated = 32,
    MinScale = 64,
    MinScaleAnimated = 128,
    MaxScale = 256,
    MaxScaleAnimated = 512,
    Scale = 1024,
    ScaleAnimated = 2048
}
