using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
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
}
