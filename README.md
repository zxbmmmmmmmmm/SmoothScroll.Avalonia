# SmoothScroll.Avalonia 

Implement WinUI's `InteractionTracker` and `ScrollView` in Avalonia.

**Features:**

- Smooth scroll
- Panning and zooming 
- Multi-touch support
- Physics-based overscroll bounce animations


https://github.com/user-attachments/assets/927a8c80-ac2b-4d50-b86b-8b2fe853ce5d


> [!WARNING]
> This is an **experimental** project.
> 
> The implemention contains some hack of Avalonia composition renderer.

## ScrollViewer 

Add `ScrollViewerSmoothTheme` to your application's styles to enable smooth scrolling for default `ScrollViewer` control:

```XAML
<Application
    xmlns:smoothScroll="using:SmoothScroll.Avalonia.Controls">

    <Application.Styles>
        <... />
        <smoothScroll:ScrollViewerSmoothTheme />
    </Application.Styles>
</Application>

```



## ScrollView

A standalone control that provides smooth scroll, with panning and zooming support.

First, add `ScrollViewDefaultTheme` to styles:
```xaml
<Application
    xmlns:smoothScroll="using:SmoothScroll.Avalonia.Controls">

    <Application.Styles>
        <... />
        <smoothScroll:ScrollViewDefaultTheme />
    </Application.Styles>
</Application>

```

Now you can use `ScrollView` like this:

```xaml
<smoothScroll:ScrollView
    HorizontalContentAlignment="Center"
    VerticalContentAlignment="Center"
    HorizontalScrollBarVisibility="Hidden"
    IsZoomEnabled="True"
    VerticalScrollBarVisibility="Hidden">
    <Image Source="avares://SmoothScroll.Avalonia.Sample/Assets/Images/4074.bmp" Stretch="UniformToFill" />
</smoothScroll:ScrollView>
```



## Credits

[Meloman19/CompositionScroll](https://github.com/Meloman19/CompositionScroll)

[unoplatform/uno](https://github.com/unoplatform/uno)
