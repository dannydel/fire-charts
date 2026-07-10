using FireCharts.Components;

namespace FireCharts.Tests;

/// <summary>
/// Characterization tests for <see cref="TooltipPlacementEngine"/>. Every expected value
/// below was produced by running the original <c>resolveTooltipPosition</c> JavaScript
/// (offset = 8, gutter = 8) against the same inputs, then locked in here before the port.
/// </summary>
public sealed class TooltipPlacementEngineTests
{
    private static TooltipLayout Resolve(
        double anchorX,
        double anchorY,
        ChartTooltipPlacement preferred,
        double hostWidth,
        double hostHeight,
        double tooltipWidth,
        double tooltipHeight) =>
        TooltipPlacementEngine.Resolve(
            new TooltipLayoutRequest(anchorX, anchorY, preferred),
            new TooltipMeasurement(hostWidth, hostHeight, tooltipWidth, tooltipHeight));

    [Fact]
    public void PrefersRequestedPlacementWhenItFits()
    {
        // above at (140,120) inside 420x240 host, 100x40 tooltip -> fits with zero overflow.
        var layout = Resolve(140, 120, ChartTooltipPlacement.Above, 420, 240, 100, 40);

        Assert.Equal(90, layout.Left);
        Assert.Equal(72, layout.Top);
        Assert.Equal(ChartTooltipPlacement.Above, layout.Placement);
    }

    [Fact]
    public void FlipsAboveToBelowAtTopEdge()
    {
        // above would overflow the top (36), below fits exactly.
        var layout = Resolve(140, 20, ChartTooltipPlacement.Above, 420, 240, 100, 40);

        Assert.Equal(90, layout.Left);
        Assert.Equal(28, layout.Top);
        Assert.Equal(ChartTooltipPlacement.Below, layout.Placement);
    }

    [Fact]
    public void FlipsBelowToAboveAtBottomEdge()
    {
        // below would overflow the bottom, above fits.
        var layout = Resolve(140, 230, ChartTooltipPlacement.Below, 420, 240, 100, 40);

        Assert.Equal(90, layout.Left);
        Assert.Equal(182, layout.Top);
        Assert.Equal(ChartTooltipPlacement.Above, layout.Placement);
    }

    [Fact]
    public void FlipsLeftToRightAtLeftEdge()
    {
        // left would overflow the left edge (86), right fits.
        var layout = Resolve(30, 120, ChartTooltipPlacement.Left, 420, 240, 100, 40);

        Assert.Equal(38, layout.Left);
        Assert.Equal(100, layout.Top);
        Assert.Equal(ChartTooltipPlacement.Right, layout.Placement);
    }

    [Fact]
    public void FlipsRightToLeftAtRightEdge()
    {
        // right would overflow the right edge (96), left fits.
        var layout = Resolve(400, 120, ChartTooltipPlacement.Right, 420, 240, 100, 40);

        Assert.Equal(292, layout.Left);
        Assert.Equal(100, layout.Top);
        Assert.Equal(ChartTooltipPlacement.Left, layout.Placement);
    }

    [Fact]
    public void PicksLeastOverflowWhenNothingFits()
    {
        // 100x80 tooltip in a short 420x100 host: neither above nor below fits.
        // above overflow = 56, below overflow = 36 -> below wins, then top clamps to gutter band.
        var layout = Resolve(210, 40, ChartTooltipPlacement.Above, 420, 100, 100, 80);

        Assert.Equal(160, layout.Left);
        Assert.Equal(12, layout.Top);
        Assert.Equal(ChartTooltipPlacement.Below, layout.Placement);
    }

    [Fact]
    public void ClampsToGutterOnLeftEdge()
    {
        // above fits vertically but the candidate left (-10) clamps to the gutter (8).
        var layout = Resolve(10, 120, ChartTooltipPlacement.Above, 420, 240, 40, 30);

        Assert.Equal(8, layout.Left);
        Assert.Equal(82, layout.Top);
        Assert.Equal(ChartTooltipPlacement.Above, layout.Placement);
    }

    [Fact]
    public void ClampsToGutterOnRightEdge()
    {
        // candidate left (390) exceeds maxLeft (372) and clamps back.
        var layout = Resolve(410, 120, ChartTooltipPlacement.Above, 420, 240, 40, 30);

        Assert.Equal(372, layout.Left);
        Assert.Equal(82, layout.Top);
        Assert.Equal(ChartTooltipPlacement.Above, layout.Placement);
    }

    [Fact]
    public void ClampsToGutterWhenTooltipLargerThanHost()
    {
        // Degenerate: 200x150 tooltip in a 100x80 host. maxLeft/maxTop collapse to the
        // gutter, so left/top clamp to (8,8) regardless of the candidate position.
        var layout = Resolve(50, 40, ChartTooltipPlacement.Above, 100, 80, 200, 150);

        Assert.Equal(8, layout.Left);
        Assert.Equal(8, layout.Top);
        Assert.Equal(ChartTooltipPlacement.Above, layout.Placement);
    }
}
