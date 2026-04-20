using Avalonia;
using Avalonia.Input;

namespace SmoothScroll.Avalonia.Interaction;

internal sealed class InteractingState : InteractionTrackerState
{
    private const double ReferenceRange = 2000;
    private const double Tension = 0.5;

    internal override string Name => "InteractingState";

    private double _previousScale;
    private Point _previousOrigin;
    private Vector3D _position;
    public InteractingState(ServerInteractionTracker interactionTracker) : base(interactionTracker)
    {
        _previousScale = interactionTracker.Scale;
        EnterState(interactionTracker.Owner);
        _position = GetOriginalPoint(interactionTracker.Position, _interactionTracker.MinPosition, _interactionTracker.MaxPosition);
    }

    protected override void EnterState(IInteractionTrackerOwner? owner)
    {
        owner?.InteractingStateEntered(_interactionTracker, new InteractionTrackerInteractingStateEnteredArgs(requestId: 0, isFromBinding: false));
    }

    internal override void StartUserManipulation(Point position, IPointer pointer)
    {
        // This probably shouldn't happen.
        // We ignore.
        //if (this.Log().IsEnabled(LogLevel.Error))
        //{
        //    this.Log().Error("Unexpected StartUserManipulation while in interacting state");
        //}
    }

    internal override void CompleteUserManipulation()
    {
        _interactionTracker.ChangeState(new ActiveInputInertiaState(_interactionTracker, default, requestId: 0));
    }

    internal override void ReceiveScaleDelta(Point origin, double scaleDelta)
    {
        if (scaleDelta <= 0 || double.IsNaN(scaleDelta) || double.IsInfinity(scaleDelta))
        {
            return;
        }

        var currentPosition = _position;

        // Treat origin movement as translation (e.g. two fingers moving together while pinching).
        // PinchGestureRecognizer origin is the midpoint of fingers, so delta(origin) is a natural pan signal.
        if (_previousOrigin != default)
        {
            var originDelta = origin - _previousOrigin;
            if (originDelta != default)
            {
                currentPosition = new Vector3D(
                    currentPosition.X - (float)originDelta.X,
                    currentPosition.Y - (float)originDelta.Y,
                    currentPosition.Z);
            }
        }

        var targetScale = _previousScale * scaleDelta;
        var clampedScale = Math.Clamp(targetScale, _interactionTracker.MinScale, _interactionTracker.MaxScale);

        var scaleChanged = Math.Abs(clampedScale - _previousScale) > double.Epsilon;
        if (scaleChanged)
        {
            var scaleRatio = clampedScale / _previousScale;

            // Keep the content under origin stationary while scaling.
            var deltaX = (origin.X - (-currentPosition.X)) * (1 - scaleRatio);
            var deltaY = (origin.Y - (-currentPosition.Y)) * (1 - scaleRatio);

            currentPosition = new Vector3D(
                currentPosition.X - (float)deltaX,
                currentPosition.Y - (float)deltaY,
                currentPosition.Z);
        }

        _position = currentPosition;

        currentPosition = GetElasticPoint(_position, _interactionTracker.MinPosition, _interactionTracker.MaxPosition);

        _interactionTracker.SetPosition(currentPosition, 0);

        if (scaleChanged)
        {
            _interactionTracker.SetScale(clampedScale, 0);
            _previousScale = clampedScale;
        }

        _previousOrigin = origin;
    }

    internal override void ReceiveManipulationDelta(Point translationDelta)
    {
        _position += new Vector3D((float)translationDelta.X, (float)translationDelta.Y, 0);
        var modifiedPosition = GetElasticPoint(_position, _interactionTracker.MinPosition, _interactionTracker.MaxPosition);
        _interactionTracker.SetPosition(modifiedPosition, requestId: 0);
    }

    internal override void ReceiveInertiaStarting(Point linearVelocity)
    {
        _interactionTracker.ChangeState(new ActiveInputInertiaState(
            _interactionTracker,
            new Vector3D((float)linearVelocity.X, (float)linearVelocity.Y, 0),
            requestId: 0));
    }

    internal override void ReceivePointerWheel(double delta, bool isHorizontal)
    {
    }

    internal override void TryUpdatePositionWithAdditionalVelocity(Vector3D velocityInPixelsPerSecond, int requestId)
    {
        _interactionTracker.Owner?.RequestIgnored(_interactionTracker, new InteractionTrackerRequestIgnoredArgs(requestId));
    }

    internal override void TryUpdatePosition(Vector3D value, InteractionTrackerClampingOption option, int requestId)
    {
        _interactionTracker.Owner?.RequestIgnored(_interactionTracker, new InteractionTrackerRequestIgnoredArgs(requestId));
    }

    internal override void ReceiveBoundsUpdate()
    {
    }

    public static Vector3D GetElasticPoint(Vector3D current, Vector3D min, Vector3D max, double tension = Tension)
    {
        var resX = current.X;
        var resY = current.Y;
        var resZ = current.Z;

        if (current.X < min.X)
        {
            resX = min.X - CalculateOffset(min.X - current.X, tension);
        }
        else if (current.X > max.X)
        {
            resX = max.X + CalculateOffset(current.X - max.X, tension);
        }

        if (current.Y < min.Y)
        {
            resY = min.Y - CalculateOffset(min.Y - current.Y, tension);
        }
        else if (current.Y > max.Y)
        {
            resY = max.Y + CalculateOffset(current.Y - max.Y, tension);
        }

        if (current.Z < min.Z)
        {
            resZ = min.Z - CalculateOffset(min.Z - current.Z, tension);
        }
        else if (current.Y > max.Y)
        {
            resZ = max.Z + CalculateOffset(current.Z - max.Z, tension);
        }

        return new Vector3D(resX, resY, resZ);
    }

    private static double CalculateOffset(double distance, double tension)
    {

        double elasticFactor = (distance * ReferenceRange) / (distance + ReferenceRange);

        return elasticFactor * tension;
    }
    public static Vector3D GetOriginalPoint(Vector3D elasticPoint, Vector3D min, Vector3D max, double tension = Tension)
    {
        var originX = elasticPoint.X;
        var originY = elasticPoint.Y;
        var originZ = elasticPoint.Z;

        if (elasticPoint.X < min.X)
        {
            double resultOffset = min.X - elasticPoint.X;
            originX = min.X - CalculateInverseOffset(resultOffset, tension);
        }
        else if (elasticPoint.X > max.X)
        {
            double resultOffset = elasticPoint.X - max.X;
            originX = max.X + CalculateInverseOffset(resultOffset, tension);
        }

        if (elasticPoint.Y < min.Y)
        {
            double resultOffset = min.Y - elasticPoint.Y;
            originY = min.Y - CalculateInverseOffset(resultOffset, tension);
        }
        else if (elasticPoint.Y > max.Y)
        {
            double resultOffset = elasticPoint.Y - max.Y;
            originY = max.Y + CalculateInverseOffset(resultOffset, tension);
        }

        if (elasticPoint.Z < min.Z)
        {
            double resultOffset = min.Z - elasticPoint.Z;
            originZ = min.Y - CalculateInverseOffset(resultOffset, tension);
        }
        else if (elasticPoint.Z > max.Z)
        {
            double resultOffset = elasticPoint.Z - max.Z;
            originZ = max.Z + CalculateInverseOffset(resultOffset, tension);
        }

        return new Vector3D(originX, originY, originZ);
    }
    
    private static double CalculateInverseOffset(double resultOffset, double tension)
    {
        double limit = ReferenceRange * tension;

        if (resultOffset >= limit)
        {
            return double.MaxValue; 
        }
        return (resultOffset * ReferenceRange) / (limit - resultOffset);
    }
}
