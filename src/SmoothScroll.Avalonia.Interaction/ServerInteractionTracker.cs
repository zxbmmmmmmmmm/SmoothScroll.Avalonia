using Avalonia;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Rendering.Composition.Server;
using Avalonia.Rendering.Composition.Transport;

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
        if ((changed & InteractionTrackerChangedFields.ScaleAnimated) == InteractionTrackerChangedFields.ScaleAnimated)
            SetAnimatedValue(s_IdOfScaleProperty, ref _scale, committedAt, reader.ReadObject<IAnimationInstance>());
        else if ((changed & InteractionTrackerChangedFields.Scale) == InteractionTrackerChangedFields.Scale)
            Scale = reader.Read<double>();
        OnFieldsDeserialized(changed);
    }

    partial void OnFieldsDeserialized(InteractionTrackerChangedFields changed);
    internal static void SerializeAllChanges(BatchStreamWriter writer, Vector3D position, Vector3D minPosition, Vector3D maxPosition, double scale)
    {
        writer.Write(InteractionTrackerChangedFields.Position | InteractionTrackerChangedFields.MinPosition | InteractionTrackerChangedFields.MaxPosition | InteractionTrackerChangedFields.Scale);
        writer.Write(position);
        writer.Write(minPosition);
        writer.Write(maxPosition);
        writer.Write(scale);
    }
}
[System.Flags]
enum InteractionTrackerChangedFields : byte
{
    Position = 1,
    PositionAnimated = 2,
    MinPosition = 4,
    MinPositionAnimated = 8,
    MaxPosition = 16,
    MaxPositionAnimated = 32,
    Scale = 64,
    ScaleAnimated = 128
}