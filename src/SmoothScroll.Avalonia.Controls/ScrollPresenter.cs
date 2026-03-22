using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Reactive;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Threading;
using Avalonia.Utilities;
using Avalonia.VisualTree;
using PropertyGenerator.Avalonia;
using SmoothScroll.Avalonia.Interaction;
using Vector = Avalonia.Vector;

namespace SmoothScroll.Avalonia.Controls;

[Flags]
public enum ScrollFeaturesEnum
{
    None = 0,
    MousePressedScroll = 1,
    MousePressedScrollEnertia = 2,
    WheelSwapDirections = 4,
}

/// <summary>
/// Presents a scrolling view of content inside a <see cref="ScrollViewer"/>.
/// </summary>
public sealed partial class ScrollPresenter : ContentPresenter, IScrollable, IScrollAnchorProvider, IInteractionTrackerOwner
{
    private const double EdgeDetectionTolerance = 0.1;
    private const int ArrangeThrottleMs = 50;

    public static readonly AttachedProperty<ScrollFeaturesEnum> ScrollFeaturesProperty =
        AvaloniaProperty.RegisterAttached<ScrollPresenter, Control, ScrollFeaturesEnum>("ScrollFeatures", defaultValue: ScrollFeaturesEnum.None);

    /// <summary>
    /// Defines the <see cref="CanHorizontallyScroll"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> CanHorizontallyScrollProperty =
        AvaloniaProperty.Register<ScrollPresenter, bool>(nameof(CanHorizontallyScroll));

    /// <summary>
    /// Defines the <see cref="CanVerticallyScroll"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> CanVerticallyScrollProperty =
        AvaloniaProperty.Register<ScrollPresenter, bool>(nameof(CanVerticallyScroll));

    /// <summary>
    /// Defines the <see cref="Extent"/> property.
    /// </summary>
    public static readonly DirectProperty<ScrollPresenter, Size> ExtentProperty =
        ScrollViewer.ExtentProperty.AddOwner<ScrollPresenter>(
            o => o.Extent);

    /// <summary>
    /// Defines the <see cref="Offset"/> property.
    /// </summary>
    public static readonly StyledProperty<Vector> OffsetProperty =
        ScrollViewer.OffsetProperty.AddOwner<ScrollPresenter>(new(coerce: ScrollViewer.CoerceOffset));

    /// <summary>
    /// Defines the <see cref="Viewport"/> property.
    /// </summary>
    public static readonly DirectProperty<ScrollPresenter, Size> ViewportProperty =
        ScrollViewer.ViewportProperty.AddOwner<ScrollPresenter>(
            o => o.Viewport);

    /// <summary>
    /// Defines the <see cref="HorizontalSnapPointsType"/> property.
    /// </summary>
    public static readonly StyledProperty<SnapPointsType> HorizontalSnapPointsTypeProperty =
        ScrollViewer.HorizontalSnapPointsTypeProperty.AddOwner<ScrollPresenter>();

    /// <summary>
    /// Defines the <see cref="VerticalSnapPointsType"/> property.
    /// </summary>
    public static readonly StyledProperty<SnapPointsType> VerticalSnapPointsTypeProperty =
        ScrollViewer.VerticalSnapPointsTypeProperty.AddOwner<ScrollPresenter>();

    /// <summary>
    /// Defines the <see cref="HorizontalSnapPointsAlignment"/> property.
    /// </summary>
    public static readonly StyledProperty<SnapPointsAlignment> HorizontalSnapPointsAlignmentProperty =
        ScrollViewer.HorizontalSnapPointsAlignmentProperty.AddOwner<ScrollPresenter>();

    /// <summary>
    /// Defines the <see cref="VerticalSnapPointsAlignment"/> property.
    /// </summary>
    public static readonly StyledProperty<SnapPointsAlignment> VerticalSnapPointsAlignmentProperty =
        ScrollViewer.VerticalSnapPointsAlignmentProperty.AddOwner<ScrollPresenter>();

    /// <summary>
    /// Defines the <see cref="IsScrollChainingEnabled"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsScrollChainingEnabledProperty =
        ScrollViewer.IsScrollChainingEnabledProperty.AddOwner<ScrollPresenter>();



    //private ScrollFeaturesEnum _scrollFeatures = ScrollFeaturesEnum.None;
    private Interaction.InteractionTracker? _interactionTracker;
    private InputElementInteractionSource? _interactionSource;
    private CompositionAnimationGroup? _animationGroup;
    private bool _compositionUpdate;
    private bool _scaleChanged;
    private long? requestId;
    private bool _arranging;
    private Size _extent;
    private Size _viewport;
    private HashSet<Control>? _anchorCandidates;
    private Control? _anchorElement;
    private Rect _anchorElementBounds;
    private bool _isAnchorElementDirty;
    private bool _areVerticalSnapPointsRegular;
    private bool _areHorizontalSnapPointsRegular;
    private IReadOnlyList<double>? _horizontalSnapPoints;
    private double _horizontalSnapPoint;
    private IReadOnlyList<double>? _verticalSnapPoints;
    private double _verticalSnapPoint;
    private double _verticalSnapPointOffset;
    private double _horizontalSnapPointOffset;
    private CompositeDisposable? _ownerSubscriptions;
    private ScrollViewer? _owner;
    private IScrollSnapPointsInfo? _scrollSnapPointsInfo;
    private bool _isSnapPointsUpdated;
    private InteractionTrackerInertiaStateEnteredArgs? _inertiaArgs;
    private Stopwatch _throttleStopWatch = new();
    /// <summary>
    /// Initializes static members of the <see cref="ScrollPresenter"/> class.
    /// </summary>
    static ScrollPresenter()
    {
        ClipToBoundsProperty.OverrideDefaultValue(typeof(ScrollPresenter), true);
        AffectsMeasure<ScrollContentPresenter>(CanHorizontallyScrollProperty, CanVerticallyScrollProperty);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ScrollPresenter"/> class.
    /// </summary>
    public ScrollPresenter()
    {
        AddHandler(RequestBringIntoViewEvent, BringIntoViewRequested);
        _throttleStopWatch.Start();
    }

    public static ScrollFeaturesEnum GetScrollFeatures(Control element)
    {
        return element.GetValue(ScrollFeaturesProperty);
    }

    public static void SetScrollFeatures(Control element, ScrollFeaturesEnum value)
    {
        element.SetValue(ScrollFeaturesProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the content can be scrolled horizontally.
    /// </summary>
    public bool CanHorizontallyScroll
    {
        get => GetValue(CanHorizontallyScrollProperty);
        set => SetValue(CanHorizontallyScrollProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the content can be scrolled horizontally.
    /// </summary>
    public bool CanVerticallyScroll
    {
        get => GetValue(CanVerticallyScrollProperty);
        set => SetValue(CanVerticallyScrollProperty, value);
    }

    /// <summary>
    /// Gets the extent of the scrollable content.
    /// </summary>
    public Size Extent
    {
        get => _extent;
        private set => SetAndRaise(ExtentProperty, ref _extent, value);
    }

    /// <summary>
    /// Gets or sets the current scroll offset.
    /// </summary>
    public Vector Offset
    {
        get => GetValue(OffsetProperty);
        set => SetValue(OffsetProperty, value);
    }

    /// <summary>
    /// Gets the size of the viewport on the scrollable content.
    /// </summary>
    public Size Viewport
    {
        get => _viewport;
        private set => SetAndRaise(ViewportProperty, ref _viewport, value);
    }

    /// <summary>
    /// Gets or sets how scroll gesture reacts to the snap points along the horizontal axis.
    /// </summary>
    public SnapPointsType HorizontalSnapPointsType
    {
        get => GetValue(HorizontalSnapPointsTypeProperty);
        set => SetValue(HorizontalSnapPointsTypeProperty, value);
    }

    /// <summary>
    /// Gets or sets how scroll gesture reacts to the snap points along the vertical axis.
    /// </summary>
    public SnapPointsType VerticalSnapPointsType
    {
        get => GetValue(VerticalSnapPointsTypeProperty);
        set => SetValue(VerticalSnapPointsTypeProperty, value);
    }

    /// <summary>
    /// Gets or sets how the existing snap points are horizontally aligned versus the initial viewport.
    /// </summary>
    public SnapPointsAlignment HorizontalSnapPointsAlignment
    {
        get => GetValue(HorizontalSnapPointsAlignmentProperty);
        set => SetValue(HorizontalSnapPointsAlignmentProperty, value);
    }

    /// <summary>
    /// Gets or sets how the existing snap points are vertically aligned versus the initial viewport.
    /// </summary>
    public SnapPointsAlignment VerticalSnapPointsAlignment
    {
        get => GetValue(VerticalSnapPointsAlignmentProperty);
        set => SetValue(VerticalSnapPointsAlignmentProperty, value);
    }

    /// <summary>
    ///  Gets or sets if scroll chaining is enabled. The default value is true.
    /// </summary>
    /// <remarks>
    ///  After a user hits a scroll limit on an element that has been nested within another scrollable element,
    /// you can specify whether that parent element should continue the scrolling operation begun in its child element.
    /// This is called scroll chaining.
    /// </remarks>
    public bool IsScrollChainingEnabled
    {
        get => GetValue(IsScrollChainingEnabledProperty);
        set => SetValue(IsScrollChainingEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the minimum zoom factor.
    /// </summary>
    [GeneratedStyledProperty(0.1)]
    public partial double MinZoomFactor { get; set; }

    /// <summary>
    /// Gets or sets the maximum zoom factor.
    /// </summary>
    [GeneratedStyledProperty(10)]
    public partial double MaxZoomFactor { get; set; }

    /// <summary>
    /// Gets or sets the current zoom factor.
    /// </summary>
    [GeneratedStyledProperty(1)]
    public partial double ZoomFactor { get; set; }

    /// <summary>
    /// Gets or sets whether zooming is enabled.
    /// </summary>
    [GeneratedStyledProperty]
    public partial bool IsZoomEnabled { get; set; }

    /// <inheritdoc/>
    Control? IScrollAnchorProvider.CurrentAnchor
    {
        get
        {
            EnsureAnchorElementSelection();
            return _anchorElement;
        }
    }

    /// <summary>
    /// Attempts to bring a portion of the target visual into view by scrolling the content.
    /// </summary>
    /// <param name="target">The target visual.</param>
    /// <param name="targetRect">The portion of the target visual to bring into view.</param>
    /// <returns>True if the scroll offset was changed; otherwise false.</returns>
    public bool BringDescendantIntoView(Visual target, Rect targetRect)
    {
        if (Child?.IsEffectivelyVisible != true)
        {
            return false;
        }

        var control = target as Control;

        var transform = target.TransformToVisual(Child);

        if (transform == null)
        {
            return false;
        }

        var rectangle = targetRect.TransformToAABB(transform.Value).Deflate(new Thickness(Child.Margin.Left, Child.Margin.Top, 0, 0));
        Rect viewport = new Rect(Offset.X, Offset.Y, Viewport.Width, Viewport.Height);

        double minX = ComputeScrollOffsetWithMinimalScroll(viewport.Left, viewport.Right, rectangle.Left, rectangle.Right);
        double minY = ComputeScrollOffsetWithMinimalScroll(viewport.Top, viewport.Bottom, rectangle.Top, rectangle.Bottom);
        var offset = new Vector(minX, minY);

        if (Offset.NearlyEquals(offset))
        {
            return false;
        }

        var oldOffset = Offset;
        SetCurrentValue(OffsetProperty, offset);

        // It's possible that the Offset coercion has changed the offset back to its previous value,
        // this is common for floating point rounding errors.
        return !Offset.NearlyEquals(oldOffset);
    }

    /// <summary>
    /// Computes the closest offset to ensure most of the child is visible in the viewport along an axis.
    /// </summary>
    /// <param name="viewportStart">The left or top of the viewport</param>
    /// <param name="viewportEnd">The right or bottom of the viewport</param>
    /// <param name="childStart">The left or top of the child</param>
    /// <param name="childEnd">The right or bottom of the child</param>
    /// <returns></returns>
    internal static double ComputeScrollOffsetWithMinimalScroll(
        double viewportStart,
        double viewportEnd,
        double childStart,
        double childEnd)
    {
        // If child is at least partially above viewport, i.e. top of child is above viewport top and bottom of child is above viewport bottom.
        bool isChildAbove = MathUtilities.LessThan(childStart, viewportStart) && MathUtilities.LessThan(childEnd, viewportEnd);

        // If child is at least partially below viewport, i.e. top of child is below viewport top and bottom of child is below viewport bottom.
        bool isChildBelow = MathUtilities.GreaterThan(childEnd, viewportEnd) && MathUtilities.GreaterThan(childStart, viewportStart);
        bool isChildLarger = (childEnd - childStart) > (viewportEnd - viewportStart);

        // Value if no updates is needed. The child is fully visible in the viewport, or the viewport is completely within the child's bounds
        var res = viewportStart;

        // The child is above the viewport and is smaller than the viewport, or if the child's top is below the viewport top
        // and is larger than the viewport, we align the child top to the top of the viewport
        if ((isChildAbove && !isChildLarger)
            || (isChildBelow && isChildLarger))
        {
            res = childStart;
        }
        // The child is above the viewport and is larger than the viewport, or if the child's smaller but is below the viewport,
        // we align the child's bottom to the bottom of the viewport
        else if (isChildAbove || isChildBelow)
        {
            res = (childEnd - (viewportEnd - viewportStart));
        }

        return res;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AttachToScrollViewer();

        var compositionVisual = ElementComposition.GetElementVisual(this);
        _interactionTracker = compositionVisual!.Compositor.CreateInteractionTracker(this);
        //_interactionTracker.SetScale(ZoomFactor,0);
        _interactionTracker.MinScale = MinZoomFactor;
        _interactionTracker.MaxScale = MaxZoomFactor;
        _interactionSource = new InputElementInteractionSource(this, _interactionTracker);
        UpdateInteractionOptions();

        Child?.AttachedToVisualTree += OnChildAttachedToVisualTree;
    }

    private void OnChildAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        SyncInteractionTrackerState();
        // 生成器生成出的 CompositionVisual 只在 Client 端设置了默认值，但 Server 端没有
        // 这会导致 Scale 被初始化为 0，导致内容不可见，需要滚动一次后触发 ExpressionAnimation 更新才能看到内容
        // 强制初始化 Scale 以确保元素可见
        // 这似乎只在有动画时会出现这种问题，具体原因有待探究
        var childVisual = ElementComposition.GetElementVisual(Child!);
        var scale = new Vector3D(_interactionTracker.Scale, _interactionTracker.Scale, _interactionTracker.Scale);
        childVisual!.Server.Scale = scale;
        UpdateScrollAnimation();

    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Child?.AttachedToVisualTree -= OnChildAttachedToVisualTree;
        _interactionTracker?.Dispose();
        _interactionTracker = null;
        _interactionSource?.Dispose();
        _interactionSource = null;
        _animationGroup?.Dispose();
        _animationGroup = null;
    }

    /// <summary>
    /// Locates the first <see cref="ScrollViewer"/> ancestor and binds to it. Properties which have been set through other means are not bound.
    /// </summary>
    /// <remarks>
    /// This method is automatically called when the control is attached to a visual tree.
    /// </remarks>
    internal void AttachToScrollViewer()
    {
        var owner = this.FindAncestorOfType<ScrollViewer>();

        if (owner == null)
        {
            _owner = null;
            _ownerSubscriptions?.Dispose();
            _ownerSubscriptions = null;
            return;
        }

        if (owner == _owner)
        {
            return;
        }

        _ownerSubscriptions?.Dispose();
        _owner = owner;

        var subscriptionDisposables = new IDisposable?[]
        {
            IfUnset(CanHorizontallyScrollProperty, p => Bind(p, owner.GetObservable(ScrollViewer.HorizontalScrollBarVisibilityProperty, NotDisabled), BindingPriority.Template)),
            IfUnset(CanVerticallyScrollProperty, p => Bind(p, owner.GetObservable(ScrollViewer.VerticalScrollBarVisibilityProperty, NotDisabled), BindingPriority.Template)),
            IfUnset(OffsetProperty, p => Bind(p, owner.GetBindingObservable(ScrollViewer.OffsetProperty), BindingPriority.Template)),
            IfUnset(HorizontalContentAlignmentProperty, p => Bind(p, owner.GetBindingObservable(ContentControl.HorizontalContentAlignmentProperty), BindingPriority.Template)),
            IfUnset(VerticalContentAlignmentProperty, p => Bind(p, owner.GetBindingObservable(ContentControl.VerticalContentAlignmentProperty), BindingPriority.Template)),
            IfUnset(IsScrollChainingEnabledProperty, p => Bind(p, owner.GetBindingObservable(ScrollViewer.IsScrollChainingEnabledProperty), BindingPriority.Template)),
            IfUnset(ContentProperty, p => Bind(p, owner.GetBindingObservable(ContentProperty), BindingPriority.Template)),
        }.Where(d => d != null).Cast<IDisposable>().ToArray();

        _ownerSubscriptions = new CompositeDisposable(subscriptionDisposables);

        static bool NotDisabled(ScrollBarVisibility v) => v != ScrollBarVisibility.Disabled;

        IDisposable? IfUnset<T>(T property, Func<T, IDisposable> func) where T : AvaloniaProperty => IsSet(property) ? null : func(property);
    }

    /// <inheritdoc/>
    void IScrollAnchorProvider.RegisterAnchorCandidate(Control element)
    {
        if (!this.IsVisualAncestorOf(element))
        {
            throw new InvalidOperationException(
                "An anchor control must be a visual descendent of the ScrollContentPresenter.");
        }

        _anchorCandidates ??= new();
        _anchorCandidates.Add(element);
        _isAnchorElementDirty = true;
    }

    /// <inheritdoc/>
    void IScrollAnchorProvider.UnregisterAnchorCandidate(Control element)
    {
        _anchorCandidates?.Remove(element);
        _isAnchorElementDirty = true;

        if (_anchorElement == element)
        {
            _anchorElement = null;
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child == null)
        {
            return base.MeasureOverride(availableSize);
        }

        var availableWithPadding = availableSize.Deflate(Padding);
        var constraint = IsZoomEnabled
            ? new Size(double.PositiveInfinity, double.PositiveInfinity)
            : new Size(
            CanHorizontallyScroll ? double.PositiveInfinity : availableWithPadding.Width,
            CanVerticallyScroll ? double.PositiveInfinity : availableWithPadding.Height);

        Child.Measure(constraint);

        if (!_isSnapPointsUpdated)
        {
            _isSnapPointsUpdated = true;
            UpdateSnapPoints();
        }

        return Child.DesiredSize.Inflate(Padding).Constrain(availableSize);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child == null)
        {
            return base.ArrangeOverride(finalSize);
        }
        return ArrangeWithAnchoring(finalSize);
    }

    private Size ArrangeWithAnchoring(Size finalSize)
    {
        _arranging = true;
        try
        {
            var size = IsZoomEnabled
                ? finalSize
                : new Size(
    CanHorizontallyScroll ? Math.Max(Child!.DesiredSize.Inflate(Padding).Width, finalSize.Width) : finalSize.Width,
    CanVerticallyScroll ? Math.Max(Child!.DesiredSize.Inflate(Padding).Height, finalSize.Height) : finalSize.Height);

            var isAnchoring = Offset.X >= EdgeDetectionTolerance || Offset.Y >= EdgeDetectionTolerance;

            if (isAnchoring)
            {
                // Calculate the new anchor element if necessary.
                EnsureAnchorElementSelection();

                // Do the arrange.
                ArrangeOverrideImpl(size, -Offset);

                // If the anchor moved during the arrange, we need to adjust the offset and do another arrange.
                var anchorShift = TrackAnchor();

                if (anchorShift != default)
                {
                    var newOffset = Offset + anchorShift;
                    var newExtent = Extent;
                    var maxOffset = new Vector(Extent.Width - Viewport.Width, Extent.Height - Viewport.Height);

                    if (newOffset.X > maxOffset.X)
                    {
                        newExtent = newExtent.WithWidth(newOffset.X + Viewport.Width);
                    }

                    if (newOffset.Y > maxOffset.Y)
                    {
                        newExtent = newExtent.WithHeight(newOffset.Y + Viewport.Height);
                    }

                    Extent = newExtent;

                    try
                    {
                        _compositionUpdate = true;
                        _interactionTracker?.TryUpdatePositionBy(new Vector3D(anchorShift.X, anchorShift.Y, 0));
                        SetCurrentValue(OffsetProperty, newOffset);
                    }
                    finally
                    {
                        _compositionUpdate = false;
                    }
                }

                ArrangeOverrideImpl(size, -Offset);
            }
            else
            {
                if (IsZoomEnabled)
                {
                    ArrangeOverrideImpl(size, -Offset - new Point(_interactionTracker.Position.X , _interactionTracker.Position.Y));
                }
                else
                {
                    ArrangeOverrideImpl(size, -Offset);
                }
            }

            Viewport = finalSize;
            _isAnchorElementDirty = true;

            UpdateScrollableAreaForScale(_interactionTracker?.Scale ?? 1.0);
        }
        finally
        {
            _arranging = false;

        }

        return finalSize;
    }

    public new Size ArrangeOverrideImpl(Size finalSize, Vector offset)
    {
        if (this.Child == null)
            return finalSize;
        bool useLayoutRounding = this.UseLayoutRounding;
        double layoutScale = LayoutHelper.GetLayoutScale((Layoutable)this);
        Thickness thickness1 = this.Padding;
        Thickness thickness2 = this.BorderThickness;
        if (useLayoutRounding)
        {
            thickness1 = LayoutHelper.RoundLayoutThickness(thickness1, layoutScale);
            thickness2 = LayoutHelper.RoundLayoutThickness(thickness2, layoutScale);
        }
        Thickness thickness3 = thickness1 + thickness2;
        HorizontalAlignment contentAlignment1 = this.HorizontalContentAlignment;
        VerticalAlignment contentAlignment2 = this.VerticalContentAlignment;
        Size size1 = finalSize;
        Size size2 = size1;
        double x = offset.X;
        double y = offset.Y;
        if (contentAlignment1 != HorizontalAlignment.Stretch)
            size2 = size2.WithWidth(Math.Min(size2.Width, this.DesiredSize.Width));
        if (contentAlignment2 != VerticalAlignment.Stretch)
            size2 = size2.WithHeight(Math.Min(size2.Height, this.DesiredSize.Height));
        if (useLayoutRounding)
        {
            size2 = LayoutHelper.RoundLayoutSizeUp(size2, layoutScale);
            size1 = LayoutHelper.RoundLayoutSizeUp(size1, layoutScale);
        }
        if (!IsZoomEnabled || CompositionMathHelpers.IsCloseReal(ZoomFactor, 1))
        {
            switch (contentAlignment1)
            {
                case HorizontalAlignment.Center:
                    x += (size1.Width - size2.Width) / 2.0;
                    break;
                case HorizontalAlignment.Right:
                    x += size1.Width - size2.Width;
                    break;
            }
            switch (contentAlignment2)
            {
                case VerticalAlignment.Center:
                    y += (size1.Height - size2.Height) / 2.0;
                    break;
                case VerticalAlignment.Bottom:
                    y += size1.Height - size2.Height;
                    break;
            }
        }

        Point point = new Point(x, y);
        if (useLayoutRounding)
            point = LayoutHelper.RoundLayoutPoint(point, layoutScale);
        this.Child.Arrange(new Rect(point, size2).Deflate(thickness3));
        return finalSize;
    }

    private void SyncInteractionTrackerState()
    {
        if (_interactionTracker == null)
        {
            return;
        }

        try
        {
            _compositionUpdate = true;

            _interactionTracker.SetPositionAndScale(new Vector3D(Offset.X, Offset.Y, 0), ZoomFactor, 0);

        }
        finally
        {
            _compositionUpdate = false;
        }
    }

    private void RequestArrangeThrottled()
    {
        if (_throttleStopWatch.ElapsedMilliseconds <= ArrangeThrottleMs) return;
        InvalidateArrange();
        _throttleStopWatch.Reset();
        _throttleStopWatch.Start();
    }


    partial void OnPropertyChangedOverride(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == OffsetProperty)
        {
            if (!_arranging && !_scaleChanged)
            {
                if (_compositionUpdate)
                {
                    RequestArrangeThrottled();
                }
                else
                {
                    InvalidateArrange();
                }
            }

            if (!_scaleChanged && !_compositionUpdate)
            {
                var offset = change.GetNewValue<Vector>();
                requestId = _interactionTracker!.TryUpdatePosition(new Vector3D(offset.X, offset.Y, 0));
            }
            else
            {
                requestId = null;
            }

            _owner?.SetCurrentValue(OffsetProperty, change.GetNewValue<Vector>());
        }
        else if (change.Property == ChildProperty)
        {
            ChildChanged(change);
        }
        else if (change.Property == HorizontalSnapPointsAlignmentProperty ||
                 change.Property == VerticalSnapPointsAlignmentProperty)
        {
            UpdateSnapPoints();
        }
        else if (change.Property == ExtentProperty)
        {
            if (_owner != null)
            {
                _owner.Extent = change.GetNewValue<Size>();
            }
            if (!_scaleChanged)
                CoerceValue(OffsetProperty);
        }
        else if (change.Property == ViewportProperty)
        {
            if (_owner != null)
            {
                _owner.Viewport = change.GetNewValue<Size>();
            }
            CoerceValue(OffsetProperty);
        }
        else if (change.Property == PaddingProperty)
        {
            _animationGroup = null;
            UpdateScrollAnimation();
        }
        else if (change.Property == ScrollFeaturesProperty ||
                change.Property == CanVerticallyScrollProperty ||
                change.Property == CanHorizontallyScrollProperty ||
                change.Property == IsZoomEnabledProperty)
            UpdateInteractionOptions();
        else if (change.Property == ZoomFactorProperty)
        {
            if (!_compositionUpdate && _interactionTracker != null)
            {
                var scale = change.GetNewValue<double>();
                _interactionTracker.TryUpdateScale(scale);
            }
        }
        else if (change.Property == MinZoomFactorProperty)
        {
            if (_interactionTracker != null)
            {
                _interactionTracker.MinScale = change.GetNewValue<double>();
            }
        }
        else if (change.Property == MaxZoomFactorProperty)
        {
            if (_interactionTracker != null)
            {
                _interactionTracker.MaxScale = change.GetNewValue<double>();
            }
        }

        base.OnPropertyChanged(change);
    }

    private void ScrollSnapPointsInfoSnapPointsChanged(object? sender, RoutedEventArgs e)
    {
        UpdateSnapPoints();
    }

    private void BringIntoViewRequested(object? sender, RequestBringIntoViewEventArgs e)
    {
        if (e.TargetObject is not null)
            e.Handled = BringDescendantIntoView(e.TargetObject, e.TargetRect);
    }

    private void ChildChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue != null)
        {
            SetCurrentValue(OffsetProperty, default);
            var compositionVisual = ElementComposition.GetElementVisual((e.OldValue as Control)!);
            if (compositionVisual != null)
            {
                compositionVisual.ImplicitAnimations = null;
            }
        }

        UpdateScrollAnimation();
    }

    private void EnsureAnchorElementSelection()
    {
        if (!_isAnchorElementDirty || _anchorCandidates is null)
        {
            return;
        }

        _anchorElement = null;
        _anchorElementBounds = default;
        _isAnchorElementDirty = false;

        var bestCandidate = default(Control);
        var bestCandidateDistance = double.MaxValue;

        // Find the anchor candidate that is scrolled closest to the top-left of this
        // ScrollContentPresenter.
        foreach (var element in _anchorCandidates)
        {
            if (element.IsVisible && GetViewportBounds(element, out var bounds))
            {
                var distance = (Vector)bounds.Position;
                var candidateDistance = Math.Abs(distance.Length);

                if (candidateDistance < bestCandidateDistance)
                {
                    bestCandidate = element;
                    bestCandidateDistance = candidateDistance;
                }
            }
        }

        if (bestCandidate != null)
        {
            // We have a candidate, calculate its bounds relative to Child. Because these
            // bounds aren't relative to the ScrollContentPresenter itself, if they change
            // then we know it wasn't just due to scrolling.
            var unscrolledBounds = TranslateBounds(bestCandidate, Child!);
            _anchorElement = bestCandidate;
            _anchorElementBounds = unscrolledBounds;
        }
    }

    private Vector TrackAnchor()
    {
        // If we have an anchor and its position relative to Child has changed during the
        // arrange then that change wasn't just due to scrolling (as scrolling doesn't adjust
        // relative positions within Child).
        if (_anchorElement != null &&
            TranslateBounds(_anchorElement, Child!, out var updatedBounds) &&
            updatedBounds.Position != _anchorElementBounds.Position)
        {
            var offset = updatedBounds.Position - _anchorElementBounds.Position;
            return offset;
        }

        return default;
    }

    private bool GetViewportBounds(Control element, out Rect bounds)
    {
        if (TranslateBounds(element, Child!, out var childBounds))
        {
            // We want the bounds relative to the new Offset, regardless of whether the child
            // control has actually been arranged to this offset yet, so translate first to the
            // child control and then apply Offset rather than translating directly to this
            // control.
            var thisBounds = new Rect(Bounds.Size);
            bounds = new Rect(childBounds.Position - Offset, childBounds.Size);
            return bounds.Intersects(thisBounds);
        }

        bounds = default;
        return false;
    }

    private Rect TranslateBounds(Control control, Control to)
    {
        if (TranslateBounds(control, to, out var bounds))
        {
            return bounds;
        }

        throw new InvalidOperationException("The control's bounds could not be translated to the requested control.");
    }

    private bool TranslateBounds(Control control, Control to, out Rect bounds)
    {
        if (!control.IsVisible)
        {
            bounds = default;
            return false;
        }

        var p = control.TranslatePoint(default, to);
        bounds = p.HasValue ? new Rect(p.Value, control.Bounds.Size) : default;
        return p.HasValue;
    }

    private void UpdateSnapPoints()
    {
        var scrollable = GetScrollSnapPointsInfo(Content);

        if (scrollable is IScrollSnapPointsInfo scrollSnapPointsInfo)
        {
            _areVerticalSnapPointsRegular = scrollSnapPointsInfo.AreVerticalSnapPointsRegular;
            _areHorizontalSnapPointsRegular = scrollSnapPointsInfo.AreHorizontalSnapPointsRegular;

            if (!_areVerticalSnapPointsRegular)
            {
                _verticalSnapPoints = scrollSnapPointsInfo.GetIrregularSnapPoints(Orientation.Vertical, VerticalSnapPointsAlignment);
            }
            else
            {
                _verticalSnapPoints = new List<double>();
                _verticalSnapPoint = scrollSnapPointsInfo.GetRegularSnapPoints(Orientation.Vertical, VerticalSnapPointsAlignment, out _verticalSnapPointOffset);

            }

            if (!_areHorizontalSnapPointsRegular)
            {
                _horizontalSnapPoints = scrollSnapPointsInfo.GetIrregularSnapPoints(Orientation.Horizontal, HorizontalSnapPointsAlignment);
            }
            else
            {
                _horizontalSnapPoints = new List<double>();
                _horizontalSnapPoint = scrollSnapPointsInfo.GetRegularSnapPoints(Orientation.Horizontal, HorizontalSnapPointsAlignment, out _horizontalSnapPointOffset);
            }
        }
        else
        {
            _horizontalSnapPoints = new List<double>();
            _verticalSnapPoints = new List<double>();
        }

        UpdateScrollModified();
    }

    private void UpdateScrollModified()
    {
        if (_inertiaArgs == null)
            return;

        var pos = new Vector(_inertiaArgs.NaturalRestingPosition.X, _inertiaArgs.NaturalRestingPosition.Y);

        Vector snapPoint;
        if (_inertiaArgs.IsInertiaFromImpulse)
        {
            var vel = new Vector(-_inertiaArgs.PositionVelocityInPixelsPerSecond.X, -_inertiaArgs.PositionVelocityInPixelsPerSecond.Y);
            snapPoint = SnapOffset(pos, vel, true);
        }
        else
        {
            snapPoint = SnapOffset(pos);
        }

        if (snapPoint == pos)
            return;

        _interactionTracker!.TryUpdatePosition(new Vector3D(snapPoint.X, snapPoint.Y, 0));
    }

    private Vector SnapOffset(Vector offset, Vector direction = default, bool snapToNext = false)
    {
        var scrollable = GetScrollSnapPointsInfo(Content);

        if (scrollable is null || (VerticalSnapPointsType == SnapPointsType.None && HorizontalSnapPointsType == SnapPointsType.None))
            return offset;

        var diff = GetAlignmentDiff();

        if (VerticalSnapPointsType != SnapPointsType.None && (_areVerticalSnapPointsRegular || _verticalSnapPoints?.Count > 0) && (!snapToNext || snapToNext && direction.Y != 0))
        {
            var estimatedOffset = new Vector(offset.X, offset.Y + diff.Y);
            double previousSnapPoint = 0, nextSnapPoint = 0, midPoint = 0;

            if (_areVerticalSnapPointsRegular)
            {
                previousSnapPoint = (int)(estimatedOffset.Y / _verticalSnapPoint) * _verticalSnapPoint + _verticalSnapPointOffset;
                nextSnapPoint = previousSnapPoint + _verticalSnapPoint;
                midPoint = (previousSnapPoint + nextSnapPoint) / 2;
            }
            else if (_verticalSnapPoints?.Count > 0)
            {
                (previousSnapPoint, nextSnapPoint) = FindNearestSnapPoint(_verticalSnapPoints, estimatedOffset.Y);
                midPoint = (previousSnapPoint + nextSnapPoint) / 2;
            }

            var nearestSnapPoint = snapToNext ? (direction.Y > 0 ? previousSnapPoint : nextSnapPoint) :
                estimatedOffset.Y < midPoint ? previousSnapPoint : nextSnapPoint;

            offset = new Vector(offset.X, nearestSnapPoint - diff.Y);
        }

        if (HorizontalSnapPointsType != SnapPointsType.None && (_areHorizontalSnapPointsRegular || _horizontalSnapPoints?.Count > 0) && (!snapToNext || snapToNext && direction.X != 0))
        {
            var estimatedOffset = new Vector(offset.X + diff.X, offset.Y);
            double previousSnapPoint = 0, nextSnapPoint = 0, midPoint = 0;

            if (_areHorizontalSnapPointsRegular)
            {
                previousSnapPoint = (int)(estimatedOffset.X / _horizontalSnapPoint) * _horizontalSnapPoint + _horizontalSnapPointOffset;
                nextSnapPoint = previousSnapPoint + _horizontalSnapPoint;
                midPoint = (previousSnapPoint + nextSnapPoint) / 2;
            }
            else if (_horizontalSnapPoints?.Count > 0)
            {
                (previousSnapPoint, nextSnapPoint) = FindNearestSnapPoint(_horizontalSnapPoints, estimatedOffset.X);
                midPoint = (previousSnapPoint + nextSnapPoint) / 2;
            }

            var nearestSnapPoint = snapToNext ? (direction.X > 0 ? previousSnapPoint : nextSnapPoint) :
                estimatedOffset.X < midPoint ? previousSnapPoint : nextSnapPoint;

            offset = new Vector(nearestSnapPoint - diff.X, offset.Y);
        }

        Vector GetAlignmentDiff()
        {
            var vector = default(Vector);

            switch (VerticalSnapPointsAlignment)
            {
                case SnapPointsAlignment.Center:
                    vector += new Vector(0, Viewport.Height / 2);
                    break;
                case SnapPointsAlignment.Far:
                    vector += new Vector(0, Viewport.Height);
                    break;
            }

            switch (HorizontalSnapPointsAlignment)
            {
                case SnapPointsAlignment.Center:
                    vector += new Vector(Viewport.Width / 2, 0);
                    break;
                case SnapPointsAlignment.Far:
                    vector += new Vector(Viewport.Width, 0);
                    break;
            }

            return vector;
        }

        return offset;
    }

    private static (double previous, double next) FindNearestSnapPoint(IReadOnlyList<double> snapPoints, double value)
    {
        var point = snapPoints.BinarySearch(value, Comparer<double>.Default);

        double previousSnapPoint, nextSnapPoint;

        if (point < 0)
        {
            point = ~point;

            previousSnapPoint = snapPoints[Math.Max(0, point - 1)];
            nextSnapPoint = point >= snapPoints.Count ? snapPoints.Last() : snapPoints[Math.Max(0, point)];
        }
        else
        {
            previousSnapPoint = nextSnapPoint = snapPoints[Math.Max(0, point)];
        }

        return (previousSnapPoint, nextSnapPoint);
    }

    private IScrollSnapPointsInfo? GetScrollSnapPointsInfo(object? content)
    {
        var scrollable = content;

        if (Content is ItemsControl itemsControl)
            scrollable = itemsControl.Presenter?.Panel;

        if (Content is ItemsPresenter itemsPresenter)
            scrollable = itemsPresenter.Panel;

        var snapPointsInfo = scrollable as IScrollSnapPointsInfo;

        if (snapPointsInfo != _scrollSnapPointsInfo)
        {
            if (_scrollSnapPointsInfo != null)
            {
                _scrollSnapPointsInfo.VerticalSnapPointsChanged -= ScrollSnapPointsInfoSnapPointsChanged;
                _scrollSnapPointsInfo.HorizontalSnapPointsChanged -= ScrollSnapPointsInfoSnapPointsChanged;
            }

            _scrollSnapPointsInfo = snapPointsInfo;

            if (_scrollSnapPointsInfo != null)
            {
                _scrollSnapPointsInfo.VerticalSnapPointsChanged += ScrollSnapPointsInfoSnapPointsChanged;
                _scrollSnapPointsInfo.HorizontalSnapPointsChanged += ScrollSnapPointsInfoSnapPointsChanged;
            }
        }

        return snapPointsInfo;
    }

    public void RequestIgnored(InteractionTracker sender, InteractionTrackerRequestIgnoredArgs args)
    {
    }

    public void ValuesChanged(InteractionTracker sender, InteractionTrackerValuesChangedArgs args)
    {

        var position = new Vector(args.Position.X, args.Position.Y);
        var scale = args.Scale;


        void ApplyValues()
        {
            if (_interactionTracker != sender)
            {
                return;
            }

            try
            {
                _compositionUpdate = true;
                _scaleChanged = !CompositionMathHelpers.IsCloseReal(scale, ZoomFactor);

                UpdateScrollableAreaForScale(scale);

                SetCurrentValue(OffsetProperty, position);
                SetCurrentValue(ZoomFactorProperty, scale);
            }
            finally
            {
                _compositionUpdate = false;
                _scaleChanged = false;
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyValues();
        }
        else
        {
            Dispatcher.UIThread.Post(ApplyValues, DispatcherPriority.Render);
        }
    }



    public void CustomAnimationStateEntered(InteractionTracker sender, InteractionTrackerCustomAnimationStateEnteredArgs args)
    {
    }

    public void IdleStateEntered(InteractionTracker sender, InteractionTrackerIdleStateEnteredArgs args)
    {
        _inertiaArgs = null;
        Dispatcher.UIThread.Post(InvalidateArrange);
    }

    public void InertiaStateEntered(InteractionTracker sender, InteractionTrackerInertiaStateEnteredArgs args)
    {
        _inertiaArgs = args;
        UpdateScrollAnimation();
        UpdateScrollModified();
    }

    public void InteractingStateEntered(InteractionTracker sender, InteractionTrackerInteractingStateEnteredArgs args)
    {
        _inertiaArgs = null;
        UpdateScrollAnimation();
    }

    private void UpdateScrollableAreaForScale(double scale)
    {
        if (_interactionTracker == null || Child == null || _interactionSource == null)
        {
            return;
        }

        var childMargin = Child.Margin + Padding;
        if (Child.UseLayoutRounding)
        {
            var layoutScale = LayoutHelper.GetLayoutScale(Child);
            childMargin = LayoutHelper.RoundLayoutThickness(childMargin, layoutScale);
        }

        var baseExtent = Child.Bounds.Size.Inflate(childMargin);
        var scaledExtent = new Size(baseExtent.Width * scale, baseExtent.Height * scale);

        Extent = scaledExtent;

        var minPosition = ComputeMinPositionForAlignment(baseExtent, scale);
        var maxPosition = ComputeMaxPositionForAlignment(baseExtent, scale);

        _interactionTracker.UpdatePositionBounds(new Vector3D(minPosition.X, minPosition.Y, 0), new Vector3D(maxPosition.X, maxPosition.Y, 0));

        var range = maxPosition - minPosition;

        _interactionSource.PositionXSourceMode = MathUtilities.IsZero(range.X) && !CanHorizontallyScroll
            ? InteractionSourceMode.Disabled
            : InteractionSourceMode.EnabledWithInertia;

        _interactionSource.PositionYSourceMode = MathUtilities.IsZero(range.Y) && !CanVerticallyScroll
            ? InteractionSourceMode.Disabled
            : InteractionSourceMode.EnabledWithInertia;
    }





    private Vector ComputeMinPositionForAlignment(Size unscaledExtent, double scale)
    {
        var scaledWidthMinusViewport = (unscaledExtent.Width * scale) - Viewport.Width;
        var scaledHeightMinusViewport = (unscaledExtent.Height * scale) - Viewport.Height;

        var minX = 0.0;
        var minY = 0.0;

        if (Child is { HorizontalAlignment: HorizontalAlignment.Center or HorizontalAlignment.Stretch })
            minX = Math.Min(0.0, scaledWidthMinusViewport / 2.0);
        else if (Child is { HorizontalAlignment: HorizontalAlignment.Right })
            minX = Math.Min(0.0, scaledWidthMinusViewport);

        if (Child is { VerticalAlignment: VerticalAlignment.Center or VerticalAlignment.Stretch })
            minY = Math.Min(0.0, scaledHeightMinusViewport / 2.0);
        else if (Child is { VerticalAlignment: VerticalAlignment.Bottom })
            minY = Math.Min(0.0, scaledHeightMinusViewport);

        return new Vector(minX, minY);
    }

    private Vector ComputeMaxPositionForAlignment(Size unscaledExtent, double scale)
    {
        var scaledWidthMinusViewport = (unscaledExtent.Width * scale) - Viewport.Width;
        var scaledHeightMinusViewport = (unscaledExtent.Height * scale) - Viewport.Height;

        var maxX =  scaledWidthMinusViewport;
        var maxY = scaledHeightMinusViewport;

        if (Child is { HorizontalAlignment: HorizontalAlignment.Center or HorizontalAlignment.Stretch })
        {
            if (maxX < 0.0)
                maxX /= 2.0;
            else
                maxX = scaledWidthMinusViewport;
        }
        else if (Child is { HorizontalAlignment: HorizontalAlignment.Right })
        {
            if (scaledWidthMinusViewport < 0.0)
                maxX = -scaledWidthMinusViewport;
            else
                maxX = scaledWidthMinusViewport;
        }

        if (Child is { VerticalAlignment: VerticalAlignment.Center or VerticalAlignment.Stretch })
        {
            if (maxY < 0.0)
                maxY /= 2.0;
            else
                maxY = scaledHeightMinusViewport;
        }
        else if (Child is { VerticalAlignment: VerticalAlignment.Bottom })
        {
            if (scaledHeightMinusViewport < 0.0)
                maxY = -scaledHeightMinusViewport;
            else
                maxY = scaledHeightMinusViewport;
        }

        return new Vector(maxX, maxY);
    }

    private void UpdateScrollAnimation()
    {
        if (Child == null)
            return;

        var vis = ElementComposition.GetElementVisual(Child);
        if (vis == null)
            return;

        EnsureScrollAnimation();
        if (_animationGroup == null)
            return;

        vis.StartAnimationGroup(_animationGroup);
    }

    private void EnsureScrollAnimation()
    {
        if (_interactionTracker == null)
        {
            return;
        }

        if (_animationGroup == null && Child is not null)
        {
            var compositionVisual = ElementComposition.GetElementVisual(Child);

            var scrollAnimation = compositionVisual!.Compositor.CreateExpressionAnimation();

            // 甚至没法直接使用 Tracker.Position ，一定要重新创建一个Vector3..解释器罪大恶极
            // this.Target 的问题需要等待修复
            scrollAnimation.Expression =
                "Vector3(Margin.X, Margin.Y, 0) - Vector3(Tracker.Position.X, Tracker.Position.Y, Tracker.Position.Z) + Vector3(this.Target.Offset.X, this.Target.Offset.Y, this.Target.Offset.Z)";
            scrollAnimation.Target = "Translation";
            scrollAnimation.SetReferenceParameter("Tracker", _interactionTracker);
            scrollAnimation.SetReferenceParameter("vis", compositionVisual);

            var margin = Child!.Margin + Padding;
            scrollAnimation.SetVector2Parameter("Margin", new Vector2((float)margin.Left, (float)margin.Top));

            var scaleAnimation = compositionVisual!.Compositor.CreateExpressionAnimation();
            scaleAnimation.Expression = "Vector3(Tracker.Scale, Tracker.Scale, Tracker.Scale)";
            scaleAnimation.SetReferenceParameter("Tracker", _interactionTracker);
            scaleAnimation.Target = "Scale";

            _animationGroup = compositionVisual!.Compositor.CreateAnimationGroup();
            _animationGroup.Add(scrollAnimation);
            _animationGroup.Add(scaleAnimation);
        }
    }

    private void UpdateInteractionOptions()
    {
        if (_interactionTracker == null || _interactionSource == null)
            return;

        _interactionSource.ScaleSourceMode = IsZoomEnabled
            ? InteractionSourceMode.EnabledWithInertia
            : InteractionSourceMode.Disabled;
        //source.CanVerticallyScroll = CanVerticallyScroll;
        //source.CanHorizontallyScroll = CanHorizontallyScroll;
        //source.IsScrollInertiaEnabled = ScrollViewer.GetIsScrollInertiaEnabled(this);
        //source.ScrollFeatures = GetScrollFeatures(this);
    }

    //public ScrollPropertiesSource GetScrollPropertiesSource() => _scrollPropertiesSource ?? CreateScrollPropertiesSource();

    //private ScrollPropertiesSource CreateScrollPropertiesSource()
    //{
    //    if (_scrollPropertiesSource == null &&
    //        CompositionVisual != null &&
    //        _interactionTracker != null)
    //    {
    //        _scrollPropertiesSource = ScrollPropertiesSource.Create(this, _interactionTracker);
    //    }


    //    return _scrollPropertiesSource;
    //}
}
