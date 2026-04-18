using System;
using System.Linq;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Rendering.Composition;
using Avalonia.VisualTree;
using SmoothScroll.Avalonia.Controls;
using SmoothScroll.Avalonia.Sample.ViewModels;

namespace SmoothScroll.Avalonia.Sample.Views.Pages;

public partial class ListPage : ContentPage
{
    public ListPage()
    {
        InitializeComponent();
        this.DataContext = new ListViewModel();
    }

    public void BringRandomItemIntoView()
    {
        var index = Random.Shared.Next(MyListBox.ItemCount - 1);
        MyListBox.SelectedIndex = index;
        MyListBox.ScrollIntoView(index);
    }

    private void BringIntoViewButton_Click(object? sender, RoutedEventArgs e)
    {
        BringRandomItemIntoView();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {        
        base.OnLoaded(e);
        var scrollPresenter = MyListBox.GetVisualDescendants().OfType<ScrollPresenter>().FirstOrDefault();
        scrollPresenter?.ScrollAnimationStarting += OnScrollAnimationStarting;
    }

    private void OnScrollAnimationStarting(object? sender, ScrollAnimationStartingEventArgs e)
    {
        var animationType = (DataContext as ListViewModel)?.AnimationType;
        if (animationType is ScrollAnimationType.Teleportation)
        {
            var compositor = e.Animation.Compositor;
            var targetVerticalPosition = e.EndPosition.Y;

            // Create a new Vector3KeyFrameAnimation for custom animation.
            var teleportationAnimation = compositor.CreateVector3DKeyFrameAnimation();

            // Calculate the difference between the current and target vertical positions.
            var deltaVerticalPosition = targetVerticalPosition - e.StartingPosition.Y;

            // Define easing functions for smooth transitions.
            // Start easing function with cubic Bezier curve for a quick start.
            var cubicBezierStart = new CreateCubicBezierEasing(
                new Vector(1.0f, 0.0f), // Control point 1
                new Vector(1.0f, 0.0f)); // Control point 2

            // Define step easing function for a sudden change in animation.
            var step = new StepEasing();

            // End easing function with cubic Bezier curve for a smooth end.
            var cubicBezierEnd = new CreateCubicBezierEasing(
                new Vector(0.0, 1.0), // Control point 1
                new Vector(0.0, 1.0)); // Control point 2

            // Insert keyframes into the custom animation.
            // First keyframe near the midpoint of the animation with a quick dip.
            teleportationAnimation.InsertKeyFrame(
                0.499999f, // Time progress for the keyframe (almost halfway)
                new Vector3D(e.StartingPosition.X, targetVerticalPosition - 0.9f * deltaVerticalPosition, 0.0f),
                cubicBezierStart); // Easing function for start

            // Second keyframe exactly at halfway with a sudden step change.
            teleportationAnimation.InsertKeyFrame(
                0.5f, // Time progress for the keyframe (exactly halfway)
                new Vector3D(e.StartingPosition.X, targetVerticalPosition - 0.1f * deltaVerticalPosition, 0.0f),
                step); // Easing function for sudden change

            // Final keyframe at the end of the animation.
            teleportationAnimation.InsertKeyFrame(
                1.0f, // Time progress for the keyframe (end)
                new Vector3D(e.EndPosition.X, targetVerticalPosition, 0.0f),
                cubicBezierEnd); // Easing function for end

            // Set the duration of the custom animation.
            teleportationAnimation.Duration = TimeSpan.FromMilliseconds(1500);

            // Replace the default animation with the custom animation.
            e.Animation = teleportationAnimation;

        }
    }
}


public class CreateCubicBezierEasing : Easing
{
    private const double SolveEpsilon = 1e-6;
    private const int NewtonIterations = 8;

    private readonly double _ax;
    private readonly double _bx;
    private readonly double _cx;
    private readonly double _ay;
    private readonly double _by;
    private readonly double _cy;

    public CreateCubicBezierEasing(Vector controlPoint1, Vector controlPoint2)
    {
        if (controlPoint1.X is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(controlPoint1), "The X coordinate must be in the [0, 1] range.");

        if (controlPoint2.X is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(controlPoint2), "The X coordinate must be in the [0, 1] range.");

        _cx = 3 * controlPoint1.X;
        _bx = 3 * (controlPoint2.X - controlPoint1.X) - _cx;
        _ax = 1 - _cx - _bx;

        _cy = 3 * controlPoint1.Y;
        _by = 3 * (controlPoint2.Y - controlPoint1.Y) - _cy;
        _ay = 1 - _cy - _by;
    }

    public override double Ease(double progress)
    {
        if (progress <= 0)
            return 0;

        if (progress >= 1)
            return 1;

        var parameter = SolveCurveX(progress);
        return SampleCurveY(parameter);
    }

    private double SampleCurveX(double t)
    {
        return ((_ax * t + _bx) * t + _cx) * t;
    }

    private double SampleCurveY(double t)
    {
        return ((_ay * t + _by) * t + _cy) * t;
    }

    private double SampleCurveDerivativeX(double t)
    {
        return (3 * _ax * t + 2 * _bx) * t + _cx;
    }

    private double SolveCurveX(double x)
    {
        var t = x;
        for (var i = 0; i < NewtonIterations; i++)
        {
            var currentX = SampleCurveX(t) - x;
            if (Math.Abs(currentX) < SolveEpsilon)
                return t;

            var derivative = SampleCurveDerivativeX(t);
            if (Math.Abs(derivative) < SolveEpsilon)
                break;

            t -= currentX / derivative;
        }

        var lower = 0d;
        var upper = 1d;
        t = x;

        while (lower < upper)
        {
            var currentX = SampleCurveX(t);
            if (Math.Abs(currentX - x) < SolveEpsilon)
                return t;

            if (x > currentX)
                lower = t;
            else
                upper = t;

            var next = (lower + upper) / 2;
            if (Math.Abs(next - t) < SolveEpsilon)
                return next;

            t = next;
        }

        return t;
    }
}

public class StepEasing : Easing
{
    public override double Ease(double progress)
    {
        return progress < 0.5 ? 0 : 1;
    }
}
