using FireCharts.Layout;
using FireCharts.Models;

namespace FireCharts.Tests;

/// <summary>
/// Boundary tests for the pure geometry strategies extracted behind
/// <see cref="IBarLayoutStrategy"/>. These exercise the layout math directly — the first time
/// it is testable without rendering a component.
/// </summary>
public sealed class BarLayoutStrategyTests
{
    // Left=50, Top=10, Right=590, Bottom=360 => Width=540, Height=350.
    private static readonly PlotArea Area = new(600, 400, 50, 10, 590, 360);

    private static readonly BarLayoutOptions VerticalOptions = new(
        Horizontal: false,
        BarWidthRatio: 0.72,
        GroupSpacing: 0.18,
        SeriesSpacing: 0.12);

    private static IReadOnlyList<IReadOnlyList<double>> Grouped(params double[][] groups) =>
        groups;

    [Fact]
    public void StackedRawMaxValueIsLargestStackTotal()
    {
        var layout = new StackedBarLayout();

        // 67 + 8 stack to 75 (the headline stacked number).
        var single = layout.ComputeRawMaxValue(Grouped([67, 8]));
        Assert.Equal(75, single);

        // Across groups the largest total wins: max(75, 40) = 75.
        var multi = layout.ComputeRawMaxValue(Grouped([67, 8], [30, 10]));
        Assert.Equal(75, multi);
    }

    [Fact]
    public void ClusteredRawMaxValueIsLargestSingleSegment()
    {
        var layout = new ClusteredBarLayout();

        // The largest individual segment is 67, never the sum.
        var single = layout.ComputeRawMaxValue(Grouped([67, 8]));
        Assert.Equal(67, single);

        var multi = layout.ComputeRawMaxValue(Grouped([67, 8], [40, 12]));
        Assert.Equal(67, multi);
    }

    [Fact]
    public void RawMaxValueForNoDataIsZero()
    {
        Assert.Equal(0, new StackedBarLayout().ComputeRawMaxValue(Grouped()));
        Assert.Equal(0, new ClusteredBarLayout().ComputeRawMaxValue(Grouped()));
    }

    [Fact]
    public void StackedGroupBoundsSpanFullPlotHeight()
    {
        var layout = new StackedBarLayout();

        var bounds = layout.GetGroupBounds(0, 2, Area, VerticalOptions);

        // Vertical stacked bars occupy the full plot height and 72% of a 270px slot.
        Assert.Equal(Area.Height, bounds.Height, 3);
        Assert.Equal(270 * 0.72, bounds.Width, 3);
        Assert.Equal(Area.Top, bounds.Y, 3);
    }

    [Fact]
    public void ClusteredGroupBoundsAreNarrowerThanStackedForSameRatio()
    {
        var stacked = new StackedBarLayout().GetGroupBounds(0, 2, Area, VerticalOptions);
        var clustered = new ClusteredBarLayout().GetGroupBounds(0, 2, Area, VerticalOptions);

        // Group spacing trims the cluster band; the stacked bar keeps the full ratio width.
        Assert.True(clustered.Width < stacked.Width);
        Assert.Equal(270 * 0.72 * (1 - 0.18), clustered.Width, 3);
    }

    [Fact]
    public void StackedSegmentsRunFromTheBaselineUpward()
    {
        var layout = new StackedBarLayout();
        var bounds = layout.GetGroupBounds(0, 1, Area, VerticalOptions);

        var rects = layout.LayoutSegments(bounds, [30, 20], maxValue: 100, Area, VerticalOptions);

        Assert.Equal(2, rects.Count);
        // First segment sits on the baseline; heights scale to the axis max.
        Assert.Equal(Area.Bottom, rects[0].Y + rects[0].Height, 3);
        Assert.Equal(0.30 * Area.Height, rects[0].Height, 3);
        // The second segment stacks directly on top of the first (running offset).
        Assert.Equal(0.20 * Area.Height, rects[1].Height, 3);
        Assert.Equal(rects[0].Y, rects[1].Y + rects[1].Height, 3);
        Assert.Equal(rects[0].X, rects[1].X, 3);
    }

    [Fact]
    public void ClusteredSegmentsSitSideBySideOnTheBaseline()
    {
        var layout = new ClusteredBarLayout();
        var bounds = layout.GetGroupBounds(0, 1, Area, VerticalOptions);

        var rects = layout.LayoutSegments(bounds, [30, 20], maxValue: 100, Area, VerticalOptions);

        Assert.Equal(2, rects.Count);
        // Both bars share the baseline; heights scale to the axis max.
        Assert.Equal(Area.Bottom, rects[0].Y + rects[0].Height, 3);
        Assert.Equal(Area.Bottom, rects[1].Y + rects[1].Height, 3);
        Assert.Equal(0.30 * Area.Height, rects[0].Height, 3);
        Assert.Equal(0.20 * Area.Height, rects[1].Height, 3);
        // Placed side by side (distinct X) with equal thickness.
        Assert.True(rects[1].X > rects[0].X);
        Assert.Equal(rects[0].Width, rects[1].Width, 3);
    }
}
