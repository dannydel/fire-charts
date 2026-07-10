using FireCharts.Models;

namespace FireCharts.Tests;

public sealed class PlotAreaTests
{
    [Fact]
    public void FromInsetSubtractsPaddingFromEachEdge()
    {
        var plot = PlotArea.FromInset(600, 400, new ChartPadding(Top: 10, Right: 10, Bottom: 40, Left: 50));

        Assert.Equal(600d, plot.SurfaceWidth);
        Assert.Equal(400d, plot.SurfaceHeight);
        Assert.Equal(50d, plot.Left);
        Assert.Equal(10d, plot.Top);
        Assert.Equal(590d, plot.Right);
        Assert.Equal(360d, plot.Bottom);
        Assert.Equal(540d, plot.Width);
        Assert.Equal(350d, plot.Height);
    }

    [Fact]
    public void FromInsetWithZeroPaddingFillsSurface()
    {
        var plot = PlotArea.FromInset(520, 420, ChartPadding.Zero);

        Assert.Equal(0d, plot.Left);
        Assert.Equal(0d, plot.Top);
        Assert.Equal(520d, plot.Right);
        Assert.Equal(420d, plot.Bottom);
        Assert.Equal(520d, plot.Width);
        Assert.Equal(420d, plot.Height);
    }

    [Fact]
    public void WidthAndHeightFloorAtOneWhenPaddingExceedsSurface()
    {
        var plot = PlotArea.FromInset(40, 30, ChartPadding.All(100));

        Assert.Equal(1d, plot.Width);
        Assert.Equal(1d, plot.Height);
    }

    [Fact]
    public void FromInsetFloorsSurfaceDimensionsAtOne()
    {
        var plot = PlotArea.FromInset(0, 0, ChartPadding.Zero);

        Assert.Equal(1d, plot.SurfaceWidth);
        Assert.Equal(1d, plot.SurfaceHeight);
    }

    [Fact]
    public void CenterIsTheMidpointOfThePlotRect()
    {
        var plot = PlotArea.FromInset(600, 400, new ChartPadding(Top: 10, Right: 20, Bottom: 30, Left: 40));

        Assert.Equal((40 + 580) / 2d, plot.Center.X);
        Assert.Equal((10 + 370) / 2d, plot.Center.Y);
    }

    [Fact]
    public void CenterMatchesSurfaceCenterWithZeroPadding()
    {
        var plot = PlotArea.FromInset(520, 420, ChartPadding.Zero);

        Assert.Equal(260d, plot.Center.X);
        Assert.Equal(210d, plot.Center.Y);
    }

    [Fact]
    public void RadiusReproducesPieChartValueWithMarginAndFloor()
    {
        // FirePieChart: Math.Max(Math.Min(SafeWidth, SafeHeight) / 2 - 26, 40)
        var plot = PlotArea.FromInset(520, 420, ChartPadding.Zero);

        // Min(520, 420) / 2 - 26 = 210 - 26 = 184, above the floor.
        Assert.Equal(184d, plot.Radius(26, 40));
    }

    [Fact]
    public void RadiusHonoursTheMinimumFloor()
    {
        // Small surface where Min/2 - 26 drops below 40 -> clamped to the floor.
        var plot = PlotArea.FromInset(120, 120, ChartPadding.Zero);

        // Min(120, 120) / 2 - 26 = 34 < 40 -> 40.
        Assert.Equal(40d, plot.Radius(26, 40));
    }

    [Fact]
    public void RadiusDefaultsToNoMarginAndUnitFloor()
    {
        var plot = PlotArea.FromInset(200, 100, ChartPadding.Zero);

        Assert.Equal(50d, plot.Radius());
    }

    [Fact]
    public void FromInsetPreservesWaterfallAsymmetricTopPadding()
    {
        // FireWaterfallChart: Top=12, Right=10, Bottom=44, Left=62 (axis labels shown).
        var plot = PlotArea.FromInset(720, 420, new ChartPadding(Top: 12, Right: 10, Bottom: 44, Left: 62));

        Assert.Equal(12d, plot.Top);
        Assert.Equal(62d, plot.Left);
        Assert.Equal(710d, plot.Right);
        Assert.Equal(376d, plot.Bottom);
        Assert.Equal(648d, plot.Width);
        Assert.Equal(364d, plot.Height);
    }

    [Fact]
    public void ValueEqualityHoldsForIdenticalPlotAreas()
    {
        var padding = new ChartPadding(10, 10, 40, 50);
        var first = PlotArea.FromInset(600, 400, padding);
        var second = PlotArea.FromInset(600, 400, padding);
        var different = PlotArea.FromInset(760, 400, padding);

        Assert.Equal(first, second);
        Assert.NotEqual(first, different);
    }
}
