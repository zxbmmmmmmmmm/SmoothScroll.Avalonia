using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Rendering.Composition.Server;

namespace SmoothScroll.Avalonia.Interaction;
internal sealed partial class InteractionTrackerActiveInputInertiaHandler : ServerObject, IServerClockItem, IInteractionTrackerInertiaHandler
{
    private readonly InteractionTracker _interactionTracker;
    private readonly AxisHelper _xHelper;
    private readonly AxisHelper _yHelper;
    private readonly AxisHelper _zHelper;
    private readonly int _requestId;

    private Stopwatch? _stopwatch;

    // InteractionTracker works at 60 FPS, per documentation
    // https://learn.microsoft.com/en-us/windows/uwp/composition/interaction-tracker-manipulations#why-use-interactiontracker
    // > InteractionTracker was built to utilize the new Animation engine that operates on an independent thread at 60 FPS,resulting in smooth motion.
    //private const int IntervalInMilliseconds = 17; // Ceiling of 1000/60

    public Vector3D InitialVelocity => new Vector3D(_xHelper.InitialVelocity, _yHelper.InitialVelocity, _zHelper.InitialVelocity);
    public Vector3D FinalPosition => new Vector3D(_xHelper.FinalValue, _yHelper.FinalValue, _zHelper.FinalValue);
    public Vector3D FinalModifiedPosition => new Vector3D(_xHelper.FinalModifiedValue, _yHelper.FinalModifiedValue, _zHelper.FinalModifiedValue);
    public double FinalModifiedScale => _interactionTracker.Scale; // TODO: Scale not yet implemented

    public InteractionTrackerActiveInputInertiaHandler(ServerCompositor serverCompositor, InteractionTracker interactionTracker, Vector3D translationVelocities, int requestId)
        : base(serverCompositor)
    {
        _interactionTracker = interactionTracker;
        _xHelper = new AxisHelper(this, translationVelocities, Axis.X);
        _yHelper = new AxisHelper(this, translationVelocities, Axis.Y);
        _zHelper = new AxisHelper(this, translationVelocities, Axis.Z);
        _requestId = requestId;
    }

    public void Start()
    {
        Compositor.Animations.AddToClock(this);
        _stopwatch = Stopwatch.StartNew();

    }

    public void Stop()
    {
        Compositor.Animations.RemoveFromClock(this);
        _stopwatch?.Stop();
    }

    public void ReceiveBoundsUpdate()
    {
        if (_stopwatch == null)
        {
            return;
        }

        var currentElapsedInSeconds = _stopwatch.ElapsedMilliseconds / 1000.0f;
        _xHelper.PositionBoundsChanged(currentElapsedInSeconds);
        _yHelper.PositionBoundsChanged(currentElapsedInSeconds);
        _zHelper.PositionBoundsChanged(currentElapsedInSeconds);
    }

    public void OnTick()
    {
        var currentElapsedInSeconds = _stopwatch!.ElapsedMilliseconds / 1000.0f;

        if (_xHelper.HasCompleted && _yHelper.HasCompleted && _zHelper.HasCompleted)
        {
            _interactionTracker.SetPosition(FinalModifiedPosition, _requestId);
            _interactionTracker.ChangeState(new InteractionTrackerIdleState(_interactionTracker, _requestId));
            Stop();
            return;
        }

        var newPosition = new Vector3D(
            _xHelper.GetPosition(currentElapsedInSeconds),
            _yHelper.GetPosition(currentElapsedInSeconds),
            _zHelper.GetPosition(currentElapsedInSeconds));

        _interactionTracker.SetPosition(newPosition, _requestId);
    }
    private enum Axis
    {
        X,
        Y,
        Z
    }

    private sealed class AxisHelper
    {
        private float? _dampingStateTimeInSeconds;
        private double? _dampingStatePosition;
        private double? _initialDampingVelocity;

        internal InteractionTrackerActiveInputInertiaHandler Handler { get; }
        internal double DecayRate { get; }
        internal double InitialVelocity { get; }
        internal double InitialValue { get; }
        internal double FinalValue { get; }
        internal double FinalModifiedValue => Math.Clamp(FinalValue, GetValue(Handler._interactionTracker.MinPosition), GetValue(Handler._interactionTracker.MaxPosition));
        internal double TimeToMinimumVelocity { get; }
        internal Axis Axis { get; }

        internal bool HasCompleted { get; private set; }

        public AxisHelper(InteractionTrackerActiveInputInertiaHandler handler, Vector3D velocities, Axis axis)
        {
            Axis = axis;
            Handler = handler;
            InitialVelocity = GetValue(velocities);
            DecayRate = 1.0 - GetValue(Handler._interactionTracker.PositionInertiaDecayRate ?? new(0.95, 0.95, 0.95));
            InitialValue = GetValue(Handler._interactionTracker.Position);

            TimeToMinimumVelocity = GetTimeToMinimumVelocity();

            var min = GetValue(Handler._interactionTracker.MinPosition);
            var max = GetValue(Handler._interactionTracker.MaxPosition);

            if (InitialValue < min || InitialValue > max)
            {

                double wn = -Math.Log(DecayRate);
                double settlingTimeBasedOnDecay = 10 / 2 / wn;

                if (TimeToMinimumVelocity < settlingTimeBasedOnDecay)
                {
                    TimeToMinimumVelocity = settlingTimeBasedOnDecay;
                }
            }

            var deltaPosition = CalculateDeltaPosition(TimeToMinimumVelocity);

            FinalValue = InitialValue + deltaPosition;
        }

        private double GetValue(Vector3D vector)
        {
            return Axis switch
            {
                Axis.X => vector.X,
                Axis.Y => vector.Y,
                Axis.Z => vector.Z,
                _ => throw new ArgumentException("Invalid value for axis.")
            };
        }

        private double GetTimeToMinimumVelocity()
        {

            var minimumVelocity = 30.0f;

            return TimeToMinimumVelocityCore(Math.Abs(InitialVelocity), DecayRate, InitialValue);

            double TimeToMinimumVelocityCore(double initialVelocity, double decayRate, double initialPosition)
            {
                var time = 0.0;
                if (initialVelocity > minimumVelocity)
                {
                    if (!CompositionMathHelpers.IsCloseReal(decayRate, 1.0))
                    {
                        if (CompositionMathHelpers.IsCloseRealZero(decayRate) /*|| !_isInertiaEnabled*/)
                        {
                            return 0.0f;
                        }
                        else
                        {
                            return (MathF.Log(minimumVelocity) - Math.Log(initialVelocity)) / Math.Log(decayRate);
                        }
                    }

                    time = (Math.Sign(initialVelocity) * double.MaxValue - initialPosition) / initialVelocity;

                    if (time < 0.0f)
                    {
                        return 0.0f;
                    }
                }

                return time;
            }
        }

        private double CalculateDeltaPosition(double time)
        {
            if (CompositionMathHelpers.IsCloseReal(DecayRate, 1.0f))
            {
                return InitialVelocity * time;
            }
            else if (CompositionMathHelpers.IsCloseRealZero(DecayRate) /*|| !_isInertiaEnabled*/)
            {
                return 0.0f;
            }
            else
            {
                double val = Math.Pow(DecayRate, time);
                return ((val - 1.0f) * InitialVelocity) / Math.Log(DecayRate);
            }
        }

        public double GetPosition(float currentElapsedInSeconds)
        {
            if (currentElapsedInSeconds >= TimeToMinimumVelocity)
            {
                HasCompleted = true;
                return FinalModifiedValue;
            }


            if (_dampingStateTimeInSeconds.HasValue)
            {
                var settlingTime = TimeToMinimumVelocity - _dampingStateTimeInSeconds.Value;
                var wn = 5.8335 * settlingTime;
                var elapsedInDamping = currentElapsedInSeconds - _dampingStateTimeInSeconds.Value;

                // It seems WinUI can use an underdamped animation in some cases. For now we only use critically damped animation.

                var target = FinalModifiedValue;
                var y0 = _dampingStatePosition!.Value - target;
                var currentOffset = DampingHelper.SolveCriticallyDampedWithVelocity(y0, _initialDampingVelocity!.Value, wn, elapsedInDamping);
                var finalOffset = DampingHelper.SolveCriticallyDampedWithVelocity(y0, _initialDampingVelocity!.Value, wn, settlingTime);
                return target + currentOffset - finalOffset;
            }

            var currentPosition = InitialValue + CalculateDeltaPosition(currentElapsedInSeconds);
            UpdateDampingAnimation(currentPosition, currentElapsedInSeconds);
            return currentPosition;
        }

        public void PositionBoundsChanged(float currentElapsedInSeconds)
        {
            if (_dampingStateTimeInSeconds.HasValue || HasCompleted)
            {
                return;
            }

            var currentPosition = GetValue(Handler._interactionTracker.Position);
            UpdateDampingAnimation(currentPosition, currentElapsedInSeconds);
        }

        private void UpdateDampingAnimation(double currentPosition, float currentElapsedInSeconds)
        {
            var minPosition = GetValue(Handler._interactionTracker.MinPosition);
            var maxPosition = GetValue(Handler._interactionTracker.MaxPosition);
            if (currentPosition < minPosition || currentPosition > maxPosition)
            {
                // This is an overpan from Interacting state. Use damping animation.
                _dampingStateTimeInSeconds = currentElapsedInSeconds;
                _dampingStatePosition = currentPosition;
                _initialDampingVelocity = InitialVelocity * Math.Pow(DecayRate, currentElapsedInSeconds);
            }
        }
    }
}


// Equations from https://docs.google.com/presentation/d/152lQqvO6ImEGW2k98w-E5Dh8stBeRxBd/edit#slide=id.p51

internal static class DampingHelper
{
    // Settling time is 4 / (zeta * wd)
    public static double SolveUnderdamped(double zeta, double wn, double wd, double t)
    {
        if (zeta >= 1)
        {
            throw new ArgumentException($"Damping ratio '{zeta}' is invalid. It must be less than 1 for underdamped systems.");
        }

        return 1 - Math.Exp(-zeta * wn * t) * (Math.Cos(wd * t) + (zeta / Math.Sqrt(1 - zeta * zeta)) * Math.Sin(wd * t));
    }

    // Ts (settling time) = 5.8335 / wn
    // wn = 5.8335 / Ts
    public static double SolveCriticallyDamped(double wn, double t)
    {
        return 1 - Math.Exp(-wn * t) * (1 + wn * t);
    }

    public static double SolveCriticallyDampedWithVelocity(double y0, double v0, double wn, double t)
    {
        // A = y0
        // B = v0 + wn * y0
        double exponent = Math.Exp(-wn * t);
        
        // y(t) = (A + B*t) * e^(-wn * t)
        return (y0 + (v0 + wn * y0) * t) * exponent;
    }
}
