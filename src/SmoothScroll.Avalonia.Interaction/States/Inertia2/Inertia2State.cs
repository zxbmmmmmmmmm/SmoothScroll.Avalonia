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
    private const double DecayRate = 0.95;
    internal override string Name => "InertiaState";

    private List<ServerInteractionTrackerInertiaModifier> _positionXInertiaModifiers;
    private List<ServerInteractionTrackerInertiaModifier> _positionYInertiaModifiers;
    private List<ServerInteractionTrackerInertiaModifier> _scaleInertiaModifiers;

    private readonly ServerCompositor _compositor;
    private readonly Vector3D _targetPosition;
    private readonly double? _targetScale;
    private readonly Vector3D? _scaleCenterPoint;
    private readonly Stopwatch _stopwatch = new ();

    public Inertia2State(ServerInteractionTracker tracker,
        Vector3D targetPosition,
        double? targetScale,
        Vector3D? scaleCenterPoint,
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
        _targetScale = targetScale;
        _scaleCenterPoint = scaleCenterPoint;
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
        
    }

    private void Stop()
    {
        _stopwatch.Stop();
        _compositor.Animations.RemoveFromClock(this);
        _interactionTracker.ChangeState(new IdleState(_interactionTracker, 0));
    }


    public void OnTick()
    {
        var currentPosition = _interactionTracker.Position;
        var currentScale = _interactionTracker.Scale;

        if(_targetScale is not null)
        {

            var newScale = Evaluate(currentScale, _targetScale!.Value, _stopwatch.ElapsedMilliseconds / 1000.0, DecayRate);
            _interactionTracker.SetScale(newScale, _scaleCenterPoint!.Value, 0);
            if (MathUtilities.AreClose(currentScale, newScale))
            {
                Stop();
            }
        }
        else
        {
            var x = CalculateAxis(currentPosition.X, _targetPosition.X, _positionXInertiaModifiers);
            var y = CalculateAxis(currentPosition.Y, _targetPosition.Y, _positionYInertiaModifiers);
            var newPosition = new Vector3D(x, y, currentPosition.Z);
            _interactionTracker.SetPosition(newPosition, 0);
            if (MathUtilities.AreClose(x, currentPosition.X) && MathUtilities.AreClose(y, currentPosition.Y))
            {
                Stop();
            }
        }

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
            updatedValue = Evaluate(currentValue, targetValue, elapsedSeconds, DecayRate);
        }
        
        return updatedValue;
    }

    private double Evaluate(double currentValue, double targetValue, double elapsedSeconds, double decayRate)
    {
        return Lerp(currentValue, targetValue, (1.0f - decayRate) * elapsedSeconds * 10f);
    }

    private static double Lerp(double start, double end, double t)
    {
        return start + (end - start) * t;
    }
}
