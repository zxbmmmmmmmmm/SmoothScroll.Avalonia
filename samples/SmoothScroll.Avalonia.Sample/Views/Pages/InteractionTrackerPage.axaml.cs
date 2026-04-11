using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Rendering.Composition;
using SmoothScroll.Avalonia.Interaction;

namespace SmoothScroll.Avalonia.Sample.Views.Pages;

public partial class InteractionTrackerPage : ContentPage
{
    public InteractionTrackerPage()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        AttachInteraction();
    }

    private void AttachInteraction()
    {
        var sourceBorderVisual = ElementComposition.GetElementVisual(SourceBorder);
        var border2Visual = ElementComposition.GetElementVisual(border2);
        var border3Visual = ElementComposition.GetElementVisual(border3);
        var compositor = sourceBorderVisual!.Compositor;
        var tracker = compositor.CreateInteractionTracker(null);

        tracker.MinPosition = new Vector3D(0, 0, 0);
        tracker.MaxPosition = new Vector3D(interactionCanvas.DesiredSize.Width - border2.DesiredSize.Width, interactionCanvas.DesiredSize.Height - border2.DesiredSize.Height, 0);

        // On non-Skia (e.g, Android), the Visual CompositionTarget is set from XamlRoot.
        // So, we need to call GetElementVisual on Loaded. Otherwise, it won't work.
        // NOTE: The sample still doesn't work on platforms other than Skia
        var interactionSource = new InputElementInteractionSource(SourceBorder, tracker);

        //_interactionSource.ManipulationRedirectionMode = VisualInteractionSourceRedirectionMode.CapableTouchpadAndPointerWheel;
        //_interactionSource.PositionXSourceMode = InteractionSourceMode.EnabledWithInertia;
        //_interactionSource.PositionYSourceMode = InteractionSourceMode.EnabledWithInertia;

        var animation = compositor.CreateExpressionAnimation("Vector3(tracker.Position.X, tracker.Position.Y, tracker.Position.Z)");
        var animation2 = compositor.CreateExpressionAnimation("Vector3(tracker.Position.Y, tracker.Position.X, tracker.Position.Z)");
        animation.SetReferenceParameter("tracker", tracker);
        animation2.SetReferenceParameter("tracker", tracker);
        border2Visual!.StartAnimation("Translation", animation);
        border3Visual!.StartAnimation("Translation", animation2);
    }
}
