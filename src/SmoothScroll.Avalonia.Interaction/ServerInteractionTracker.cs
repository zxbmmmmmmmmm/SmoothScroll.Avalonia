using System.Diagnostics;
using Avalonia;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Rendering.Composition.Server;
using Avalonia.Rendering.Composition.Transport;
using Avalonia.Styling;
using Avalonia.Utilities;

namespace SmoothScroll.Avalonia.Interaction;

internal partial class ServerInteractionTracker
{
    partial void Initialize()
    {
        _scale = 1;
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
        return base.GetCompositionProperty(name);
    }

    public void StartPositionAnimation(CompositionAnimation animation)
    {
        var instance = animation.CreateInstance(this, null);
        GetOrCreateAnimations().OnSetAnimatedValue(s_IdOfPositionProperty,
            ref _position, Compositor.Clock.Elapsed, instance);
    }
}

internal class InteractionTrackerScaleAnimationHandler : ServerObject, IServerClockItem
{
    private readonly IAnimationInstance _animation;
    private readonly InteractionTracker _interactionTracker;
    private Vector3D _initialPosition;
    private double _initialScale;
    private readonly Vector3D _centerPoint;

    public InteractionTrackerScaleAnimationHandler(
        InteractionTracker interactionTracker,
        CompositionAnimation animation,
        Vector3D centerPoint,
        ServerCompositor compositor) : base(compositor)
    {
        _interactionTracker = interactionTracker;
        _animation = animation.CreateInstance(interactionTracker.Server, null);
        _centerPoint = centerPoint;
    }


    public void Initialize()
    {
        _initialPosition = _interactionTracker.Position;
        _initialScale = _interactionTracker.Scale;
        _animation.Initialize(Compositor.Clock.Elapsed, _initialScale, ServerInteractionTracker.s_IdOfScaleProperty);
        Compositor.Animations.AddToClock(this);
        this.Activate();
    }

    public void Stop()
    {
        Compositor.Animations.RemoveFromClock(this);
        this.Deactivate();
    }

    public void OnTick()
    {
        var scale = _animation.Evaluate(Compositor.Clock.Elapsed, _interactionTracker.Scale).Double;
        Debug.WriteLine(scale);
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
