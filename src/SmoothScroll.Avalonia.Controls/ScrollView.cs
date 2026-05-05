using Avalonia.Controls;
using PropertyGenerator.Avalonia;

namespace SmoothScroll.Avalonia.Controls;

public partial class ScrollView : ScrollViewer
{
    /// <summary>
    /// Gets or sets the minimum zoom factor.
    /// </summary>
    [GeneratedStyledProperty(0.1)]
    public partial double MinZoomFactor { get; set; }

    /// <summary>
    /// Gets or sets the maximum zoom factor.
    /// </summary>
    [GeneratedStyledProperty(10)]
    public partial double MaxZoomFactor { get; set; }

    /// <summary>
    /// Gets the current zoom factor.
    /// </summary>
    [GeneratedDirectProperty(1)]
    public partial double ZoomFactor { get; private set; }

    /// <summary>
    /// Gets or sets whether zooming is enabled.
    /// </summary>
    [GeneratedStyledProperty]
    public partial bool IsZoomEnabled { get; set; }

    public void ZoomTo(double zoomFactor, bool isAnimated)
    {
        var presenter = Presenter as ScrollPresenter;
        presenter?.ZoomTo(zoomFactor, isAnimated);
    }

    public void ZoomBy(double zoomFactorDelta, bool isAnimated)
    {
        var presenter = Presenter as ScrollPresenter;
        presenter?.ZoomBy(zoomFactorDelta, isAnimated);
    }
}
