using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;

namespace SmoothScroll.Avalonia.Sample.ViewModels;

public class ListViewModel : ViewModelBase
{
    public ObservableCollection<int> Items { get; } = new(Enumerable.Range(1, OperatingSystem.IsBrowser() ? 100 : 1000));
}
