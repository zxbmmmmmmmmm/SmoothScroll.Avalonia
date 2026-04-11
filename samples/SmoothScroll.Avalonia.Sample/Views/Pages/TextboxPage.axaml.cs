using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SmoothScroll.Avalonia.Sample.ViewModels;

namespace SmoothScroll.Avalonia.Sample.Views.Pages;

public partial class TextboxPage : ContentPage
{
    public TextboxPage()
    {
        InitializeComponent();
        this.DataContext = new TextBoxViewModel();

    }
}
