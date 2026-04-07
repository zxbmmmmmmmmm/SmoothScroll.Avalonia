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

## ScrollPresenter 

You can replace `ScrollContentPresenter` with `ScrollPresenter` to enable smooth scrolling for `ScrollViewer`

```XAML
<ControlTheme x:Key="{x:Type ScrollViewer}" TargetType="ScrollViewer">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="Template">
        <ControlTemplate>
            <Panel>
                <controls:ScrollPresenter
                    Name="PART_ContentPresenter"
                    Grid.Row="0"
                    Grid.Column="0"
                    Padding="{TemplateBinding Padding}"
                    Background="{TemplateBinding Background}"
                    HorizontalSnapPointsAlignment="{TemplateBinding HorizontalSnapPointsAlignment}"
                    HorizontalSnapPointsType="{TemplateBinding HorizontalSnapPointsType}"
                    ScrollViewer.IsScrollInertiaEnabled="{TemplateBinding IsScrollInertiaEnabled}"
                    VerticalSnapPointsAlignment="{TemplateBinding VerticalSnapPointsAlignment}"
                    VerticalSnapPointsType="{TemplateBinding VerticalSnapPointsType}"
                    ZIndex="1" />
                <ScrollBar
                    Name="PART_HorizontalScrollBar"
                    Grid.Row="1"
                    VerticalAlignment="Bottom"
                    Orientation="Horizontal"
                    ZIndex="1" />
                <ScrollBar
                    Name="PART_VerticalScrollBar"
                    Grid.Column="1"
                    HorizontalAlignment="Right"
                    Orientation="Vertical"
                    ZIndex="1" />
                <Panel
                    x:Name="PART_ScrollBarsSeparator"
                    Background="{DynamicResource ScrollViewerScrollBarsSeparatorBackground}"
                    Opacity="0">
                    <Panel.Transitions>
                        <Transitions>
                            <DoubleTransition Property="Opacity" Duration="0:0:0.1" />
                        </Transitions>
                    </Panel.Transitions>
                </Panel>
            </Panel>
        </ControlTemplate>
    </Setter>
    <Style Selector="^[IsExpanded=true] /template/ Panel#PART_ScrollBarsSeparator">
        <Setter Property="Opacity" Value="1" />
    </Style>
    <Style Selector="^[AllowAutoHide=True] /template/ ScrollContentPresenter#PART_ContentPresenter">
        <Setter Property="Grid.ColumnSpan" Value="2" />
        <Setter Property="Grid.RowSpan" Value="2" />
    </Style>
</ControlTheme>
```



## ScrollView

A standalone control that provides smooth scroll, with panning and zooming support.

First, add a default style for `ScrollView` to your application resources:
```xaml
<ControlTheme x:Key="{x:Type controls:ScrollView}" TargetType="controls:ScrollView">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="Template">
        <ControlTemplate>
            <Grid ColumnDefinitions="*,Auto" RowDefinitions="*,Auto">
                <controls:ScrollPresenter
                    Name="PART_ContentPresenter"
                    Grid.Row="0"
                    Grid.Column="0"
                    Padding="{TemplateBinding Padding}"
                    Background="{TemplateBinding Background}"
                    HorizontalSnapPointsAlignment="{TemplateBinding HorizontalSnapPointsAlignment}"
                    HorizontalSnapPointsType="{TemplateBinding HorizontalSnapPointsType}"
                    IsZoomEnabled="{TemplateBinding IsZoomEnabled}"
                    MaxZoomFactor="{TemplateBinding MaxZoomFactor}"
                    MinZoomFactor="{TemplateBinding MinZoomFactor}"
                    ScrollViewer.IsScrollInertiaEnabled="{TemplateBinding IsScrollInertiaEnabled}"
                    VerticalSnapPointsAlignment="{TemplateBinding VerticalSnapPointsAlignment}"
                    VerticalSnapPointsType="{TemplateBinding VerticalSnapPointsType}"
                    ZoomFactor="{TemplateBinding ZoomFactor,
                                                 Mode=TwoWay}" />
                <ScrollBar
                    Name="PART_HorizontalScrollBar"
                    Grid.Row="1"
                    Orientation="Horizontal"
                    ZIndex="1" />
                <ScrollBar
                    Name="PART_VerticalScrollBar"
                    Grid.Column="1"
                    Orientation="Vertical"
                    ZIndex="1" />
                <Panel
                    x:Name="PART_ScrollBarsSeparator"
                    Grid.Row="1"
                    Grid.Column="1"
                    Background="{DynamicResource ScrollViewerScrollBarsSeparatorBackground}"
                    Opacity="0">
                    <Panel.Transitions>
                        <Transitions>
                            <DoubleTransition Property="Opacity" Duration="0:0:0.1" />
                        </Transitions>
                    </Panel.Transitions>
                </Panel>
            </Grid>
        </ControlTemplate>
    </Setter>
    <Style Selector="^[IsExpanded=true] /template/ Panel#PART_ScrollBarsSeparator">
        <Setter Property="Opacity" Value="1" />
    </Style>
    <Style Selector="^[AllowAutoHide=True] /template/ ScrollContentPresenter#PART_ContentPresenter">
        <Setter Property="Grid.ColumnSpan" Value="2" />
        <Setter Property="Grid.RowSpan" Value="2" />
    </Style>
</ControlTheme>
```

Now you can use `ScrollView` like this:

```xaml
<controls:ScrollView
    HorizontalContentAlignment="Center"
    VerticalContentAlignment="Center"
    HorizontalScrollBarVisibility="Hidden"
    IsZoomEnabled="True"
    VerticalScrollBarVisibility="Hidden">
    <Image Source="avares://SmoothScroll.Avalonia.Sample/Assets/Images/4074.bmp" Stretch="UniformToFill" />
</controls:ScrollView>
```



## Credits

[Meloman19/CompositionScroll](https://github.com/Meloman19/CompositionScroll)

[unoplatform/uno](https://github.com/unoplatform/uno)
