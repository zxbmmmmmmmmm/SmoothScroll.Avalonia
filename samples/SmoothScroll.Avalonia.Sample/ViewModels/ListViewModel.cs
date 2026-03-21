using System.Collections.ObjectModel;
using System.Linq;

namespace SmoothScroll.Avalonia.Sample.ViewModels;

public class ListViewModel : ViewModelBase
{
    public ObservableCollection<int> Items { get; } = new(Enumerable.Range(1, 1000));
}
