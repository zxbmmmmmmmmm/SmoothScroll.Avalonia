using Avalonia;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Platform;

namespace SmoothScroll.Avalonia.InteractionTracker;

public class InputElementInteractionSource : IDisposable
{
    /// <summary>
    /// Defines how interactions are processed for an <see cref="InputElementInteractionSource"/> on the scale axis.
    /// This property must be enabled to allow the <see cref="InputElementInteractionSource"/> to send scale data to <see cref="InteractionTracker"/>.
    /// </summary>
    public InteractionSourceMode ScaleSourceMode { get; set; } = InteractionSourceMode.Disabled;

    /// <summary>
    /// Source mode for the X-axis.
    /// The <see cref="PositionXSourceMode"/> property defines how interactions are processed for a <see cref="InputElementInteractionSource"/> on the X-axis.
    /// This property must be enabled to allow the <see cref="InputElementInteractionSource"/> to send X-axis data to <see cref="InteractionTracker"/>.
    /// </summary>
    public InteractionSourceMode PositionXSourceMode { get; set; } = InteractionSourceMode.EnabledWithInertia;

    /// <summary>
    /// Source mode for the Y-axis.
    /// The <see cref="PositionYSourceMode"/> property defines how interactions are processed for a <see cref="InputElementInteractionSource"/> on the Y-axis.
    /// This property must be enabled to allow the <see cref="InputElementInteractionSource"/> to send Y-axis data to <see cref="InteractionTracker"/>.
    /// </summary>
    public InteractionSourceMode PositionYSourceMode { get; set; } = InteractionSourceMode.EnabledWithInertia;

    private readonly InteractionTracker _tracker; // TODO: Support multiple trackers
    private readonly InputElement _inputElement;
    private readonly double _manipulationStartDistance;
    private IPointer? _firstContact;
    private Point _firstPosition;
    private IPointer? _secondContact;
    private Point _secondPosition;
    private double _previousDistance;
    private Point _previousCenter;
    private bool _isInteracting;

    private Point _pressedPosition;
    private VelocityTracker? _velocityTracker;

    public InputElementInteractionSource(InputElement inputElement, InteractionTracker tracker)
    {
        _inputElement = inputElement;
        _inputElement.PointerPressed += OnPointerPressed;
        _inputElement.PointerMoved += OnPointerMoved;
        _inputElement.PointerReleased += OnPointerReleased;
        _inputElement.PointerCaptureLost += OnPointerCaptureLost;
        _inputElement.PointerWheelChanged += OnPointerWheelChanged;
        _tracker = tracker;

        var tapSize = AvaloniaLocator.Current?.GetService<IPlatformSettings>()?.GetTapSize(PointerType.Touch);
        _manipulationStartDistance = (tapSize?.Height ?? 10) / 2.0;
    }

    private bool IsTranslationEnabled =>
        PositionXSourceMode is not InteractionSourceMode.Disabled ||
        PositionYSourceMode is not InteractionSourceMode.Disabled;

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (ScaleSourceMode is not InteractionSourceMode.Disabled &&
            e.Delta.Y != 0)
        {
            var origin = e.GetPosition(_inputElement);
            var scaleDelta = Math.Pow(1.2, e.Delta.Y);
            _tracker.ReceiveScaleDelta(origin, scaleDelta);
            e.Handled = true;
            return;
        }

        if (e.Delta.Y != 0)
        {
            if (PositionYSourceMode is InteractionSourceMode.Disabled)
            {
                if (PositionXSourceMode is not InteractionSourceMode.Disabled)
                {
                    _tracker.ReceivePointerWheel((int)e.Delta.Y, true);
                    e.Handled = true;
                }
                return;
            }

            _tracker.ReceivePointerWheel((int)e.Delta.Y, false);
            e.Handled = true;
        }
        else
        {
            if (PositionXSourceMode is InteractionSourceMode.Disabled)
            {
                return;
            }

            _tracker.ReceivePointerWheel((int)e.Delta.X, true);
            e.Handled = true;
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsTranslationEnabled && ScaleSourceMode is InteractionSourceMode.Disabled)
        {
            return;
        }

        var position = e.GetPosition(_inputElement);

        if (_firstContact is not null)
        {
            if (ScaleSourceMode is InteractionSourceMode.Disabled)
            {
                return;
            }

            _secondContact = e.Pointer;
            _secondPosition = position;
            _previousDistance = GetDistance(_firstPosition, _secondPosition);
            _previousCenter = GetCenter(_firstPosition, _secondPosition);

            if (!_isInteracting)
            {
                TryStartInteraction(e, _firstPosition);
            }

            CapturePointer(_firstContact);
            CapturePointer(_secondContact);

            e.PreventGestureRecognition();
            e.Handled = true;
            return;
        }

        _firstContact = e.Pointer;
        _pressedPosition = position;
        _firstPosition = _pressedPosition;
        _velocityTracker = new VelocityTracker();
        _velocityTracker.AddPosition(TimeSpan.FromMilliseconds(e.Timestamp), default);

        if (e.Pointer.Type is not PointerType.Touch and not PointerType.Pen)
        {
            TryStartInteraction(e, position);
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var position = e.GetPosition(_inputElement);

        if (_secondContact is not null)
        {
            if (e.Pointer == _firstContact)
            {
                _firstPosition = position;
            }
            else if (e.Pointer == _secondContact)
            {
                _secondPosition = position;
            }

            if (!_isInteracting && _firstContact is not null)
            {
                TryStartInteraction(e, _firstPosition);
            }

            var currentDistance = GetDistance(_firstPosition, _secondPosition);
            var currentCenter = GetCenter(_firstPosition, _secondPosition);

            if (_isInteracting && _previousDistance > 0)
            {
                var scaleRatio = currentDistance / _previousDistance;
                _tracker.ReceiveScaleDelta(currentCenter, scaleRatio);
                e.Handled = true;
            }

            _previousDistance = currentDistance;
            e.PreventGestureRecognition();
        }
        else if (_firstContact is not null && e.Pointer == _firstContact)
        {
            if (!_isInteracting && ShouldStartManipulation(position - _pressedPosition, e.Pointer.Type))
            {
                if (!TryStartInteraction(e, _pressedPosition))
                {
                    return;
                }

                _firstPosition = position;
            }

            if (!_isInteracting)
            {
                return;
            }

            var delta = position - _firstPosition;
            if (PositionXSourceMode is InteractionSourceMode.Disabled)
            {
                delta = delta.WithX(0);
            }

            if (PositionYSourceMode is InteractionSourceMode.Disabled)
            {
                delta = delta.WithY(0);
            }

            if (delta != default)
            {
                _tracker.ReceiveManipulationDelta(delta);
                _velocityTracker?.AddPosition(TimeSpan.FromMilliseconds(e.Timestamp), position - _pressedPosition);
                _firstPosition = position;
            }

            e.Handled = true;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer == _secondContact)
        {
            _secondContact = null;
            _previousDistance = 0;
            if (_isInteracting)
            {
                e.Handled = true;
            }

            return;
        }

        if (_firstContact != e.Pointer)
        {
            return;
        }

        if (_secondContact is not null)
        {
            if (!_isInteracting)
            {
                TryStartInteraction(e, _firstPosition);
            }

            _firstContact = _secondContact;
            _firstPosition = _secondPosition;
            _secondContact = null;
            _previousDistance = 0;
            _pressedPosition = _firstPosition;
            _velocityTracker = new VelocityTracker();
            CapturePointer(_firstContact);
            e.Handled = true;
            return;
        }

        if (!_isInteracting)
        {
            ResetContacts();
            return;
        }

        var velocity = _velocityTracker?.GetFlingVelocity().PixelsPerSecond ?? Vector.Zero;
        if (PositionXSourceMode is InteractionSourceMode.Disabled)
        {
            velocity = velocity.WithX(0);
        }

        if (PositionYSourceMode is InteractionSourceMode.Disabled)
        {
            velocity = velocity.WithY(0);
        }

        if (velocity != Vector.Zero)
        {
            _tracker.ReceiveInertiaStarting(new Point(velocity.X, velocity.Y));
        }
        else
        {
            _tracker.CompleteUserManipulation();
        }

        _firstContact?.Capture(null);
        ResetContacts();
        e.Handled = true;
    }

    private void ResetContacts()
    {
        _firstContact = null;
        _secondContact = null;
        _velocityTracker = null;
        _pressedPosition = default;
        _firstPosition = default;
        _secondPosition = default;
        _previousDistance = 0;
        _previousCenter = default;
        _isInteracting = false;
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!_isInteracting)
        {
            ResetContacts();
            return;
        }

        if (e.Pointer == _firstContact || e.Pointer == _secondContact)
        {
            _tracker.CompleteUserManipulation();
            ResetContacts();
        }
    }

    private static double GetDistance(Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static Point GetCenter(Point a, Point b)
    {
        return new Point((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
    }

    private bool ShouldStartManipulation(Vector delta, PointerType pointerType)
    {
        if (!IsTranslationEnabled)
        {
            return false;
        }

        if (pointerType is not PointerType.Touch and not PointerType.Pen)
        {
            return true;
        }

        var xDistance = PositionXSourceMode is InteractionSourceMode.Disabled ? 0 : Math.Abs(delta.X);
        var yDistance = PositionYSourceMode is InteractionSourceMode.Disabled ? 0 : Math.Abs(delta.Y);
        return xDistance > _manipulationStartDistance || yDistance > _manipulationStartDistance;
    }

    private bool TryStartInteraction(PointerEventArgs e, Point position)
    {
        if (_isInteracting)
        {
            return true;
        }

        if (!IsTranslationEnabled && ScaleSourceMode is InteractionSourceMode.Disabled)
        {
            return false;
        }

        var pointer = _firstContact ?? e.Pointer;
        _isInteracting = true;
        _tracker.StartUserManipulation(position, pointer);
        CapturePointer(pointer);
        if (pointer != e.Pointer)
        {
            CapturePointer(e.Pointer);
        }

        e.PreventGestureRecognition();
        e.Handled = true;
        return true;
    }

    private void CapturePointer(IPointer? pointer)
    {
        pointer?.Capture(_inputElement);
    }

    public void Dispose()
    {
        _inputElement.PointerPressed -= OnPointerPressed;
        _inputElement.PointerMoved -= OnPointerMoved;
        _inputElement.PointerReleased -= OnPointerReleased;
        _inputElement.PointerCaptureLost -= OnPointerCaptureLost;
        _inputElement.PointerWheelChanged -= OnPointerWheelChanged;
    }
}

public enum InteractionSourceMode
{
    /// <summary>
    /// Interaction is disabled.
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// Interaction is enabled with inertia.
    /// </summary>
    EnabledWithInertia = 1,

    /// <summary>
    /// Interaction is enabled without inertia.
    /// </summary>
    EnabledWithoutInertia = 2,
}
