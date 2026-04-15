using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Controls.ApplicationLifetimes;

namespace SmoothScroll.Avalonia.Sample.Views;

public partial class MainView : ContentPage
{
    public MainView()
    {
        InitializeComponent();
    }

    private void Button_Click(object? sender, RoutedEventArgs e)
    {
        var launcher = TopLevel.GetTopLevel(this)?.Launcher;
        launcher?.LaunchUriAsync(new Uri("https://github.com/zxbmmmmmmmmm/SmoothScroll.Avalonia"));
    }
}
