using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace SmoothScroll.Avalonia.Sample.Views.Pages;

public partial class ZoomPage : ContentPage
{
    public ZoomPage()
    {
        InitializeComponent();
    }
    private void ZoomInButton_Click(object? sender, RoutedEventArgs e)
    {
        ScrollView.ZoomTo(ScrollView.ZoomFactor * 1.2);
    }
    private void ZoomOutButton_Click(object? sender, RoutedEventArgs e)
    {
        ScrollView.ZoomTo(ScrollView.ZoomFactor / 1.2);
    }
}
