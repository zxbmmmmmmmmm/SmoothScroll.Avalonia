using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace SmoothScroll.Avalonia.Sample.Views.Pages;

public partial class ZoomPage : UserControl
{
    public ZoomPage()
    {
        InitializeComponent();
    }
    private void ZoomInButton_Click(object? sender, RoutedEventArgs e)
    {
        ScrollView.ZoomBy(0.1);
    }
    private void ZoomOutButton_Click(object? sender, RoutedEventArgs e)
    {
        ScrollView.ZoomBy(-0.1);
    }
}
