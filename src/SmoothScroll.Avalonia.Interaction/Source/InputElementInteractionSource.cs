using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Utilities;
using Avalonia.VisualTree;
using SmoothScroll.Avalonia.Interaction.Helpers;

namespace SmoothScroll.Avalonia.Interaction;

public class InputElementInteractionSource : IDisposable
{
    private const double TouchpadDeltaScale = 48;

    // On WinUI, this depends on mouse setting "how many lines to scroll each time"
    // The default Windows setting is 3 lines, and each line is 16px.
    // Note: the value for each line may vary depending on scaling.
    // For now, we just use 128.
    private const double MouseWheelDeltaScale = 128;

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

    /// <summary>The PositionXChainingMode property defines the chaining behavior for an InteractionSource in the X direction. There are three InteractionChainingMode types:
    /// 
    /// - Auto
    /// - Always
    /// - Never
    /// 
    /// When chaining in the X direction is enabled, input will flow to the nearest ancestor's VisualInteractionSource whenever the interaction (such as panning) would otherwise take InteractionTracker ’s position past its minimum or maximum X position.</summary>
    /// <returns>Chaining mode for the X-axis.</returns>
    public InteractionChainingMode PositionXChainingMode { get; set; } = InteractionChainingMode.Auto;

    /// <summary>The PositionYChainingMode property defines the chaining behavior for an InteractionSource in the Y direction. There are three types of InteractionChainingMode s:
    /// 
    /// - Auto
    /// - Always
    /// - Never
    /// 
    /// When chaining in the Y direction is enabled, input will flow to the nearest ancestor’s VisualInteractionSource whenever the interaction (such as panning) would otherwise take InteractionTracker ’s position past its minimum or maximum Y position.</summary>
    /// <returns>Chaining mode for the Y-axis.</returns>
    public InteractionChainingMode PositionYChainingMode { get; set; } = InteractionChainingMode.Auto;

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
    private bool _hasHorizontalChainingTarget;
    private bool _hasVerticalChainingTarget;

    public InputElementInteractionSource(InputElement inputElement, InteractionTracker tracker)
    {
        _inputElement = inputElement;
        _inputElement.PointerPressed += OnPointerPressed;
        _inputElement.PointerMoved += OnPointerMoved;
        _inputElement.PointerReleased += OnPointerReleased;
        _inputElement.PointerCaptureLost += OnPointerCaptureLost;
        _inputElement.PointerWheelChanged += OnPointerWheelChanged;
        _tracker = tracker;

        if (_inputElement is Visual visual)
        {
            visual.AttachedToVisualTree += OnAttachedToVisualTree;
            visual.DetachedFromVisualTree += OnDetachedFromVisualTree;
        }

        var tapSize = AvaloniaLocator.Current?.GetService<IPlatformSettings>()?.GetTapSize(PointerType.Touch);
        _manipulationStartDistance = (tapSize?.Height ?? 10) / 2.0;
        UpdateChainingTargets();
    }

    private bool IsTranslationEnabled =>
        PositionXSourceMode is not InteractionSourceMode.Disabled ||
        PositionYSourceMode is not InteractionSourceMode.Disabled;

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (IsPrecisionTouchpadScroll(e))
        {
            HandlePrecisionTouchpadScroll(e);
            return;
        }

        if (ScaleSourceMode is not InteractionSourceMode.Disabled &&
            e.Delta.Y != 0)
        {
            var origin = e.GetPosition(_inputElement);
            var scaleDelta = Math.Pow(1.2, e.Delta.Y);
            _tracker.ReceiveScaleDelta(origin, scaleDelta);
            e.Handled = true;
            return;
        }
        var deltaX = e.Delta.X * MouseWheelDeltaScale;
        var deltaY = e.Delta.Y * MouseWheelDeltaScale;
        if (deltaY != 0)
        {
            if (PositionYSourceMode is InteractionSourceMode.Disabled)
            {
                if (PositionXSourceMode is not InteractionSourceMode.Disabled)
                {
                    if (IsAtBoundaryForChaining(deltaY, _tracker.Position.X, _tracker.MinPosition.X, _tracker.MaxPosition.X, PositionXChainingMode, _hasHorizontalChainingTarget))
                        return;

                    _tracker.ReceivePointerWheel(deltaY, true);
                    e.Handled = true;
                }
                return;
            }

            if (IsAtBoundaryForChaining(deltaY, _tracker.Position.Y, _tracker.MinPosition.Y, _tracker.MaxPosition.Y, PositionYChainingMode, _hasVerticalChainingTarget))
                return;

            _tracker.ReceivePointerWheel(deltaY, false);
            e.Handled = true;
        }
        else
        {
            if (PositionXSourceMode is InteractionSourceMode.Disabled)
            {
                return;
            }

            if (IsAtBoundaryForChaining(deltaX, _tracker.Position.X, _tracker.MinPosition.X, _tracker.MaxPosition.X, PositionXChainingMode, _hasHorizontalChainingTarget))
                return;

            _tracker.ReceivePointerWheel(deltaX, true);
            e.Handled = true;
        }
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        UpdateChainingTargets();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _hasHorizontalChainingTarget = false;
        _hasVerticalChainingTarget = false;
    }

    private void UpdateChainingTargets()
    {
        if (_inputElement is not Visual visual)
        {
            _hasHorizontalChainingTarget = false;
            _hasVerticalChainingTarget = false;
            return;
        }

        var currentScrollViewer = visual as ScrollViewer ?? visual.FindAncestorOfType<ScrollViewer>();
        var ancestorScrollViewer = currentScrollViewer?.FindAncestorOfType<ScrollViewer>();

        _hasHorizontalChainingTarget = ancestorScrollViewer is not null && ancestorScrollViewer.HorizontalScrollBarVisibility is not ScrollBarVisibility.Disabled;
        _hasVerticalChainingTarget = ancestorScrollViewer is not null && ancestorScrollViewer.VerticalScrollBarVisibility is not ScrollBarVisibility.Disabled;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsTranslationEnabled && ScaleSourceMode is InteractionSourceMode.Disabled)
        {
            return;
        }

        if(e.Pointer.Type == PointerType.Mouse && ScaleSourceMode is InteractionSourceMode.Disabled)
        {
            return;
        }

        var position = e.GetPosition(_inputElement);

        if (_firstContact is not null && !_isInteracting && _firstContact.Captured != _inputElement)
        {
            ResetContacts();
        }

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
                if (ShouldChainDuringInteraction(delta) && ScaleSourceMode is not InteractionSourceMode.EnabledWithInertia)
                {
                    _firstContact?.Capture(null);
                    return;
                }

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

    private bool IsPrecisionTouchpadScroll(PointerWheelEventArgs e)
    {
        if (!IsTranslationEnabled)
        {
            return false;
        }

        return IsPrecisionTouchpadDelta(e.Delta.X) || IsPrecisionTouchpadDelta(e.Delta.Y);
    }

    private static bool IsPrecisionTouchpadDelta(double delta)
    {
        // There is no way to distinguish whether the mousewheel event is from precision touchpad or from mouse.
        // However, deltas from original mouse wheel is often integers,
        // so we can "distinguish" them by checking whether the delta's absolute value is close to an integer.
        var absoluteValue = Math.Abs(delta);
        return !MathUtilities.AreClose(absoluteValue, (int)absoluteValue) ;
    }

    private void HandlePrecisionTouchpadScroll(PointerWheelEventArgs e)
    {
        var translationDelta = new Point(
            PositionXSourceMode is InteractionSourceMode.Disabled ? 0 : e.Delta.X * TouchpadDeltaScale,
            PositionYSourceMode is InteractionSourceMode.Disabled ? 0 : e.Delta.Y * TouchpadDeltaScale);

        if (translationDelta == default)
        {
            return;
        }

        if (ShouldChainDuringInteraction(translationDelta) && ScaleSourceMode is not InteractionSourceMode.EnabledWithInertia)
        {
            return;
        }

        _tracker.StartUserManipulation(e.GetPosition(_inputElement), e.Pointer);
        _tracker.ReceiveManipulationDelta(translationDelta);
        _tracker.CompleteUserManipulation();
        e.Handled = true;
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

        if (pointerType is not PointerType.Touch and not PointerType.Pen || ScaleSourceMode is not InteractionSourceMode.Disabled)
        {
            return true;
        }

        var xDistance = PositionXSourceMode is InteractionSourceMode.Disabled ? 0 : Math.Abs(delta.X);
        var yDistance = PositionYSourceMode is InteractionSourceMode.Disabled ? 0 : Math.Abs(delta.Y);

        if (xDistance > 0 && IsAtBoundaryForChaining(delta.X, _tracker.Position.X, _tracker.MinPosition.X, _tracker.MaxPosition.X, PositionXChainingMode, _hasHorizontalChainingTarget))
            xDistance = 0;
        if (yDistance > 0 && IsAtBoundaryForChaining(delta.Y, _tracker.Position.Y, _tracker.MinPosition.Y, _tracker.MaxPosition.Y, PositionYChainingMode, _hasVerticalChainingTarget))
            yDistance = 0;

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

    /// <summary>
    /// Checks whether the tracker is at a boundary on the given axis and chaining should propagate
    /// the input to the parent. A positive <paramref name="userDelta"/> means the tracker position
    /// would decrease (toward <paramref name="min"/>); negative means it would increase (toward <paramref name="max"/>).
    /// </summary>
    private static bool IsAtBoundaryForChaining(double userDelta, double position, double min, double max, InteractionChainingMode chainingMode, bool hasChainingTarget)
    {
        if (!CanChain(chainingMode, hasChainingTarget))
            return false;

        const double tolerance = 0.5;

        if (userDelta > 0 && position <= min + tolerance)
            return true;
        if (userDelta < 0 && position >= max - tolerance)
            return true;

        return false;
    }

    private static bool CanChain(InteractionChainingMode chainingMode, bool hasChainingTarget)
    {
        return hasChainingTarget && chainingMode is not InteractionChainingMode.Never;
    }

    /// <summary>
    /// During an active interaction, determines whether all enabled axes are at boundary
    /// with chaining enabled, meaning the interaction should be handed off to the parent.
    /// </summary>
    private bool ShouldChainDuringInteraction(Point fingerDelta)
    {
        var xEnabled = PositionXSourceMode is not InteractionSourceMode.Disabled;
        var yEnabled = PositionYSourceMode is not InteractionSourceMode.Disabled;

        var xAtBoundary = !xEnabled || fingerDelta.X == 0 ||
            IsAtBoundaryForChaining(fingerDelta.X, _tracker.Position.X, _tracker.MinPosition.X, _tracker.MaxPosition.X, PositionXChainingMode, _hasHorizontalChainingTarget);
        var yAtBoundary = !yEnabled || fingerDelta.Y == 0 ||
            IsAtBoundaryForChaining(fingerDelta.Y, _tracker.Position.Y, _tracker.MinPosition.Y, _tracker.MaxPosition.Y, PositionYChainingMode, _hasVerticalChainingTarget);

        return xAtBoundary && yAtBoundary;
    }

    private void CapturePointer(IPointer? pointer)
    {
        pointer?.Capture(_inputElement);
    }

    public void Dispose()
    {
        if (_inputElement is Visual visual)
        {
            visual.AttachedToVisualTree -= OnAttachedToVisualTree;
            visual.DetachedFromVisualTree -= OnDetachedFromVisualTree;
        }

        _inputElement.PointerPressed -= OnPointerPressed;
        _inputElement.PointerMoved -= OnPointerMoved;
        _inputElement.PointerReleased -= OnPointerReleased;
        _inputElement.PointerCaptureLost -= OnPointerCaptureLost;
        _inputElement.PointerWheelChanged -= OnPointerWheelChanged;
    }
}
