using System.Diagnostics;
using Avalonia;
using Avalonia.Rendering.Composition.Server;

namespace SmoothScroll.Avalonia.Interaction;

internal class PointerWheelInertiaHandler : ServerObject, IServerClockItem, IInteractionTrackerInertiaHandler
{
    // InteractionTracker works at 60 FPS, per documentation
    // https://learn.microsoft.com/en-us/windows/uwp/composition/interaction-tracker-manipulations#why-use-interactiontracker
    // > InteractionTracker was built to utilize the new Animation engine that operates on an independent thread at 60 FPS,resulting in smooth motion.
    //private const int IntervalInMilliseconds = 17; // Ceiling of 1000/60

    private const double StopVelocityThreshold = 2.0;
    private const double HalfLifeSeconds = 0.08; // Time for velocity to halve; yields ~95% decay in ~0.4s
    private const double MaxDurationSeconds = 1.0;
    private const double Epsilon = 0.0001;

    private readonly Vector3D _initialVelocity;
    private readonly Vector3D _initialPosition;
    private readonly Vector3D _calculatedFinalPosition;
    private readonly double _timeConstantSeconds;

    private Stopwatch? _stopwatch;
    private readonly ServerInteractionTracker _interactionTracker;
    private int _stopRequested;

    public PointerWheelInertiaHandler(
        ServerInteractionTracker interactionTracker,
        Vector3D translationVelocities
    )
        : base(interactionTracker.Compositor)
    {
        _interactionTracker = interactionTracker;
        _initialPosition = _interactionTracker.Position;

        _timeConstantSeconds = HalfLifeSeconds / Math.Log(2.0);

        _initialVelocity = translationVelocities;
        Velocity = translationVelocities;

        // Natural final (unclamped) resting position for exponential decay.
        _calculatedFinalPosition = _initialPosition + _initialVelocity * _timeConstantSeconds;
    }

    public Vector3D InitialVelocity => _initialVelocity;

    public Vector3D Velocity { get; private set; }

    public Vector3D FinalPosition => _calculatedFinalPosition;

    public Vector3D FinalModifiedPosition => Vector3D.Clamp(_calculatedFinalPosition, _interactionTracker.MinPosition, _interactionTracker.MaxPosition);

    public double FinalModifiedScale => _interactionTracker.Scale;

    public void Start()
    {
        if (Volatile.Read(ref _stopRequested) != 0)
        {
            return;
        }

        Compositor.Animations.AddToClock(this);
        _stopwatch = Stopwatch.StartNew();
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopRequested, 1) != 0)
        {
            return;
        }

        StopCore();
    }

    public void OnTick()
    {
        if (Volatile.Read(ref _stopRequested) != 0)
        {
            StopCore();
            return;
        }

        var elapsedSeconds = _stopwatch!.ElapsedMilliseconds / 1000.0;

        // Exponential decay: v(t) = v0 * e^(-t/τ); x(t) = x0 + v0 * τ * (1 - e^(-t/τ))
        var decay = Math.Exp(-elapsedSeconds / _timeConstantSeconds);
        var currentVelocity = _initialVelocity * decay;
        Velocity = currentVelocity;

        var newPosition = _initialPosition + _initialVelocity * _timeConstantSeconds * (1 - decay);
        var clampedNewPosition = Vector3D.Clamp(newPosition, _interactionTracker.MinPosition, _interactionTracker.MaxPosition);

        _interactionTracker.SetPosition(clampedNewPosition, requestId: 0);

        var hasStoppedByVelocity = Math.Abs(currentVelocity.Length) <= StopVelocityThreshold;
        var hasReachedTarget = Vector3D.DistanceSquared(clampedNewPosition, FinalModifiedPosition) < Epsilon;
        var hasTimedOut = elapsedSeconds >= MaxDurationSeconds;

        if (hasStoppedByVelocity || hasReachedTarget || hasTimedOut)
        {
            _interactionTracker.ChangeState(new IdleState(_interactionTracker, requestId: 0));
            StopCore();
        }
    }

    private void StopCore()
    {
        Compositor.Animations.RemoveFromClock(this);
        _stopwatch?.Stop();
    }
}
