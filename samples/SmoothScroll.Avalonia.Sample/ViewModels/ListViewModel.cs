using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SmoothScroll.Avalonia.Sample.ViewModels;

public partial class ListViewModel : ViewModelBase
{
    public ObservableCollection<int> Items { get; } = new(Enumerable.Range(1, OperatingSystem.IsBrowser() ? 100 : 1000));

    [ObservableProperty]
    public partial ScrollAnimationType AnimationType { get; set; } = ScrollAnimationType.Default;

    public ScrollAnimationType[] AnimationTypeOptions { get; } = Enum.GetValues<ScrollAnimationType>();
}

public enum ScrollAnimationType
{
    Default,
    Teleportation,
}
