using FireCharts.Models;

namespace FireCharts.Tests;

public sealed class ChartPaddingTests
{
    [Fact]
    public void ZeroHasNoInset()
    {
        var padding = ChartPadding.Zero;

        Assert.Equal((0d, 0d, 0d, 0d), (padding.Top, padding.Right, padding.Bottom, padding.Left));
    }

    [Fact]
    public void AllAppliesSameValueToEverySide()
    {
        var padding = ChartPadding.All(12);

        Assert.Equal((12d, 12d, 12d, 12d), (padding.Top, padding.Right, padding.Bottom, padding.Left));
    }

    [Fact]
    public void SymmetricSplitsVerticalAndHorizontal()
    {
        var padding = ChartPadding.Symmetric(vertical: 10, horizontal: 24);

        Assert.Equal((10d, 24d, 10d, 24d), (padding.Top, padding.Right, padding.Bottom, padding.Left));
    }

    [Fact]
    public void PositionalConstructorOrdersTopRightBottomLeft()
    {
        var padding = new ChartPadding(Top: 1, Right: 2, Bottom: 3, Left: 4);

        Assert.Equal((1d, 2d, 3d, 4d), (padding.Top, padding.Right, padding.Bottom, padding.Left));
    }

    [Fact]
    public void ValueEqualityHoldsForIdenticalPadding()
    {
        Assert.Equal(new ChartPadding(10, 10, 40, 50), new ChartPadding(10, 10, 40, 50));
        Assert.NotEqual(new ChartPadding(10, 10, 40, 50), new ChartPadding(10, 10, 40, 90));
    }
}
