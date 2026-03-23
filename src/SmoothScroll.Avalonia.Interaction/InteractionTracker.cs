using System.Diagnostics;
using Avalonia;
using Avalonia.Input;
using Avalonia.Rendering.Composition;

namespace SmoothScroll.Avalonia.Interaction;

public partial class InteractionTracker : CompositionObject
{
    private int _requestId = 0;
    public IInteractionTrackerOwner? Owner { get; init; }


    private InteractionTrackerState _state;

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

    private int _count = 0;

    internal new ServerInteractionTracker Server { get; }

    internal InteractionTracker(Compositor compositor, ServerInteractionTracker server) : base(compositor, server)
    {
        Server = server;
        Server.Activate();
        _state = new InteractionTrackerIdleState(this, 0, isInitialIdleState: true);
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
        if (CompositionMathHelpers.IsCloseReal(Scale, newScale))
            return;
        Compositor.Loop.Wakeup();

        Server.Scale = newScale;
        Owner?.ValuesChanged(this, new InteractionTrackerValuesChangedArgs(Position, newScale, requestId));
    }

    internal void SetPositionAndScale(Vector3D newPosition, double newScale, int requestId)
    {
        if (CompositionMathHelpers.IsCloseReal(Scale, newScale) && Position == newPosition)
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

    [Conditional("DEBUG")]
    private static void WriteStateTransition(int count, string previousState, string newState)
    {
        Debug.WriteLine($"{count}:{previousState} -> {newState}");
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

    internal void ReceivePointerWheel(int mouseWheelTicks, bool isHorizontal)
    {
        // On WinUI, this depends on mouse setting "how many lines to scroll each time"
        // The default Windows setting is 3 lines, and each line is 16px.
        // Note: the value for each line may vary depending on scaling.
        // For now, we just use 16*3=48.
        var delta = mouseWheelTicks * 48;
        _state.ReceivePointerWheel(-delta, isHorizontal);
    }

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

    public void TryUpdateScale(double scale)
    {
        SetScale(scale, 0);
    }

    public void TryUpdatePositionAndScale(Vector3D newPosition, double newScale) => SetPositionAndScale(newPosition, newScale, 0);
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
