using System.Diagnostics;
using Avalonia;
using Avalonia.Rendering.Composition.Server;
using Avalonia.Utilities;

namespace SmoothScroll.Avalonia.Interaction;

internal class ScaleInertiaHandler : ServerObject, IInteractionTrackerInertiaHandler, IServerClockItem
{
    // InteractionTracker works at 60 FPS, per documentation
    // https://learn.microsoft.com/en-us/windows/uwp/composition/interaction-tracker-manipulations#why-use-interactiontracker
    // > InteractionTracker was built to utilize the new Animation engine that operates on an independent thread at 60 FPS,resulting in smooth motion.
    //private const int IntervalInMilliseconds = 17; // Ceiling of 1000/60

    private const double HalfLifeSeconds = 0.08; // Time for velocity to halve; yields ~95% decay in ~0.4s
    private const double MaxDurationSeconds = 1.0;
    private const double Epsilon = 0.0001;

    private readonly double _timeConstantSeconds;

    private readonly double _initialScale;
    private readonly double _initialScaleVelocity;
    private readonly Point _scaleOrigin;

    private Stopwatch? _stopwatch;
    private readonly ServerInteractionTracker _interactionTracker;
    private int _stopRequested;

    public Vector3D InitialVelocity { get; set; }
    public Vector3D FinalPosition { get; set; }
    public Vector3D FinalModifiedPosition { get; set; }

    public ScaleInertiaHandler(
        ServerInteractionTracker interactionTracker,
        Point scaleOrigin,
        double scaleVelocity
    )
        : base(interactionTracker.Compositor)
    {
        _interactionTracker = interactionTracker;
        _timeConstantSeconds = HalfLifeSeconds / Math.Log(2.0);


        _scaleOrigin = scaleOrigin;
        _initialScale = interactionTracker.Scale;

        ScaleVelocity = _initialScaleVelocity = scaleVelocity;
        var finalScale = _initialScale * Math.Exp(scaleVelocity * _timeConstantSeconds);
        FinalModifiedScale = Math.Clamp(finalScale, interactionTracker.MinScale, interactionTracker.MaxScale);
    }


    public double ScaleVelocity { get; private set; }

    public double FinalModifiedScale { get; init; }

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

        var scaleLogDelta = _initialScaleVelocity * _timeConstantSeconds * (1 - decay);
        ScaleVelocity = _initialScaleVelocity * decay;

        var scale = _initialScale * Math.Exp(scaleLogDelta);
        var modifiedScale = Math.Clamp(scale, _interactionTracker.MinScale, _interactionTracker.MaxScale);

        if (!MathUtilities.AreClose(modifiedScale, _interactionTracker.MinScale)
            && !MathUtilities.AreClose(modifiedScale, _interactionTracker.MaxScale))
        {
            _interactionTracker.SetScale(modifiedScale, new(_scaleOrigin.X,_scaleOrigin.Y,0), 0);
        }

        var hasStoppedByScaleVelocity = Math.Abs(ScaleVelocity) <= Epsilon;
        var hasReachedScaleTarget = MathUtilities.AreClose(scale, FinalModifiedScale, 0.001);
        var hasTimedOut = elapsedSeconds >= MaxDurationSeconds;

        if (hasStoppedByScaleVelocity || hasReachedScaleTarget || hasTimedOut)
        {
            // clamp position with inertia

            StopCore();
            _interactionTracker.ChangeState(new ActiveInputInertiaState(
                _interactionTracker,
                default,
                requestId: 0));
        }
    }

    private void StopCore()
    {
        Compositor.Animations.RemoveFromClock(this);
        _stopwatch?.Stop();
    }
}
