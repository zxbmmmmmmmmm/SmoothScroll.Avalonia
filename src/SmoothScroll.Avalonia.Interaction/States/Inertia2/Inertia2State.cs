using System.Diagnostics;
using System.Timers;
using Avalonia;
using Avalonia.Input;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Expressions;
using Avalonia.Rendering.Composition.Server;
using Avalonia.Utilities;
using SmoothScroll.Avalonia.Interaction.Modifier;

namespace SmoothScroll.Avalonia.Interaction.Inertia2;

internal sealed class Inertia2State : InteractionTrackerState, IServerClockItem
{
    internal override string Name => "InertiaState";

    private List<ServerInteractionTrackerInertiaModifier> _positionXInertiaModifiers;
    private List<ServerInteractionTrackerInertiaModifier> _positionYInertiaModifiers;
    private List<ServerInteractionTrackerInertiaModifier> _scaleInertiaModifiers;

    private readonly ServerCompositor _compositor;
    private readonly Vector3D _targetPosition;
    private readonly Stopwatch _stopwatch = new ();

    public Inertia2State(ServerInteractionTracker tracker,
        Vector3D targetPosition,
        List<ServerInteractionTrackerInertiaModifier> positionXInertiaModifiers,
        List<ServerInteractionTrackerInertiaModifier> positionYInertiaModifiers,
        List<ServerInteractionTrackerInertiaModifier> scaleInertiaModifiers) : base(tracker)
    {
        _compositor = tracker.Compositor;
        _stopwatch.Start();
        _positionXInertiaModifiers = positionXInertiaModifiers;
        _positionYInertiaModifiers = positionYInertiaModifiers;
        _scaleInertiaModifiers = scaleInertiaModifiers;
        _targetPosition = targetPosition;
    }

    protected override void EnterState()
    {
        _compositor.Animations.AddToClock(this);
    }

    internal override void CompleteUserManipulation()
    {
        throw new NotImplementedException();
    }

    internal override void ReceiveBoundsUpdate()
    {
        throw new NotImplementedException();
    }

    internal override void ReceiveInertiaStarting(Point linearVelocity)
    {
        throw new NotImplementedException();
    }

    internal override void ReceiveManipulationDelta(Point translationDelta)
    {
        throw new NotImplementedException();
    }

    internal override void ReceivePointerWheel(double delta, bool isHorizontal)
    {
        throw new NotImplementedException();
    }

    internal override void ReceiveScaleDelta(Point origin, double delta)
    {
        throw new NotImplementedException();
    }

    internal override void StartUserManipulation(Point position, IPointer pointer)
    {
        throw new NotImplementedException();
    }

    internal override void TryUpdatePosition(Vector3D value, InteractionTrackerClampingOption option, int requestId)
    {
        throw new NotImplementedException();
    }

    internal override void TryUpdatePositionWithAdditionalVelocity(Vector3D velocityInPixelsPerSecond, int requestId)
    {
        throw new NotImplementedException();
    }

    private void Stop()
    {
        _stopwatch.Stop();
        _compositor.Animations.RemoveFromClock(this);
    }


    public void OnTick()
    {
        var currentPosition = _interactionTracker.Position;

        var x = CalculateAxis(currentPosition.X, _targetPosition.X, _positionXInertiaModifiers);
        var y = CalculateAxis(currentPosition.Y, _targetPosition.Y ,_positionYInertiaModifiers);
        if (MathUtilities.AreClose(x, currentPosition.X) && MathUtilities.AreClose(y, currentPosition.Y))
        {
            Stop();
        }
        var newPosition = new Vector3D(x, y, currentPosition.Z);

        _interactionTracker.SetPosition(newPosition, 0);
    }

    private double CalculateAxis(double currentValue, double targetValue, IEnumerable<ServerInteractionTrackerInertiaModifier> modifiers)
    {
        var updatedValue = currentValue;
        var isModifierSelected = false;
        foreach (var modifier in modifiers)
        {
            if (modifier.Condition.Evaluate(_stopwatch.Elapsed, currentValue).Boolean)
            {
                if (modifier is ServerInteractionTrackerInertiaMotion motion)
                {
                    updatedValue = motion.Motion.Evaluate(_stopwatch.Elapsed, currentValue).Double ;
                }
                isModifierSelected = true;
                break;
            }
        }

        if (!isModifierSelected)
        {
            var elapsedSeconds = _stopwatch!.ElapsedMilliseconds / 1000.0;
            const double decayRate = 0.95;
            updatedValue = Lerp(currentValue, targetValue, (1.0f - decayRate) * elapsedSeconds * 10f);
        }
        
        return updatedValue;
    }

    private static double Lerp(double start, double end, double t)
    {
        return start + (end - start) * t;
    }
}
