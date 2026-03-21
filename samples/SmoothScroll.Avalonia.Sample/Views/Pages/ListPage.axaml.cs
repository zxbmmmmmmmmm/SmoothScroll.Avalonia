using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SmoothScroll.Avalonia.Sample.ViewModels;

namespace SmoothScroll.Avalonia.Sample.Views.Pages;

public partial class ListPage : UserControl
{
    public ListPage()
    {
        InitializeComponent();
        this.DataContext = new ListViewModel();
    }
}
