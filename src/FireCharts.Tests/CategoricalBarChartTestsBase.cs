using System.Globalization;
using FireCharts.Components;
using FireCharts.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace FireCharts.Tests;

/// <summary>
/// Shared bUnit coverage for the two categorical bar charts (stacked and clustered), which render
/// through the same <c>CategoricalBarChartCore</c>. Behavior that is identical modulo CSS prefix
/// and layout indices lives here once and runs for both concrete charts; layout- and
/// selection-specific facts stay in the two subclasses.
/// </summary>
public abstract class CategoricalBarChartTestsBase : TestContext
{
    /// <summary>The CSS class prefix for the concrete chart (<c>stacked</c> / <c>clustered</c>).</summary>
    protected abstract string Prefix { get; }

    /// <summary>The shell root selector, e.g. <c>.fire-stacked-bar-chart-shell</c>.</summary>
    protected string ShellSelector => $".fire-{Prefix}-bar-chart-shell";

    protected string SegmentGroupSelector => $"g.{Prefix}-segment";
    protected string SegmentRectSelector => $"rect.{Prefix}-segment-rect";
    protected string LegendItemSelector => $".{Prefix}-bar-legend__item";
    protected string LegendButtonSelector => $"button.{Prefix}-bar-legend__item";

    /// <summary>
    /// Two rect indices that resolve to different groups (stacked) or different series within a
    /// group (clustered) — enough to distinguish vertical from horizontal layout.
    /// </summary>
    protected abstract (int First, int Second) OrientationRectIndices { get; }

    /// <summary>Renders the concrete chart with the shared sample data and the given options.</summary>
    protected abstract IRenderedFragment RenderChart(IReadOnlyList<BarDatum> data, BarChartTestOptions options);

    protected IRenderedFragment RenderSample(BarChartTestOptions? options = null) =>
        RenderChart(SampleData, options ?? new BarChartTestOptions());

    [Fact]
    public void RendersCustomEmptyStateWhenNoPositiveSegmentsRemain()
    {
        var data = new[]
        {
            new BarDatum("North", [new BarSegmentDatum("Open", 0)]),
            new BarDatum("South", [new BarSegmentDatum("Closed", double.NaN)])
        };

        var cut = RenderChart(data, new BarChartTestOptions
        {
            EmptyStateTemplate = builder => builder.AddContent(0, "Nothing here")
        });

        Assert.Contains("Nothing here", cut.Markup);
        Assert.Empty(cut.FindAll(SegmentRectSelector));
    }

    [Fact]
    public void SupportsVerticalAndHorizontalLayouts()
    {
        var data = new[]
        {
            new BarDatum("North", [new BarSegmentDatum("Open", 30), new BarSegmentDatum("Closed", 20)]),
            new BarDatum("South", [new BarSegmentDatum("Open", 18), new BarSegmentDatum("Closed", 12)])
        };

        var vertical = RenderChart(data, new BarChartTestOptions());
        var horizontal = RenderChart(data, new BarChartTestOptions { Horizontal = true });

        var (a, b) = OrientationRectIndices;
        var verticalRects = vertical.FindAll(SegmentRectSelector);
        var horizontalRects = horizontal.FindAll(SegmentRectSelector);

        Assert.NotEqual(verticalRects[a].GetAttribute("x"), verticalRects[b].GetAttribute("x"));
        Assert.Equal(horizontalRects[a].GetAttribute("x"), horizontalRects[b].GetAttribute("x"));
        Assert.NotEqual(horizontalRects[a].GetAttribute("y"), horizontalRects[b].GetAttribute("y"));
    }

    [Fact]
    public void CornerRadiusChangesRenderedSegmentRounding()
    {
        var narrow = RenderSample(new BarChartTestOptions { CornerRadius = 2 });
        Assert.Equal("2.0", narrow.Find(SegmentRectSelector).GetAttribute("rx"));

        var wide = RenderSample(new BarChartTestOptions { CornerRadius = 9 });
        Assert.Equal("9.0", wide.Find(SegmentRectSelector).GetAttribute("rx"));
        Assert.Equal("9.0", wide.Find(SegmentRectSelector).GetAttribute("ry"));
    }

    [Theory]
    [InlineData(ChartLegendPlacement.Top, "legend-top")]
    [InlineData(ChartLegendPlacement.Bottom, "legend-bottom")]
    [InlineData(ChartLegendPlacement.Left, "legend-left")]
    [InlineData(ChartLegendPlacement.Right, "legend-right")]
    [InlineData(ChartLegendPlacement.Start, "legend-start")]
    [InlineData(ChartLegendPlacement.End, "legend-end")]
    public void LegendRendersUniqueItemsAndPlacementClass(ChartLegendPlacement placement, string expectedClass)
    {
        var cut = RenderSample(new BarChartTestOptions
        {
            UseColorSelector = true,
            LegendPlacement = placement
        });

        Assert.Contains(expectedClass, cut.Find(ShellSelector).GetAttribute("class"));
        Assert.Equal(3, cut.FindAll(LegendItemSelector).Count);
    }

    [Fact]
    public void SegmentTooltipModePreservesLegacyMouseoutBehavior()
    {
        var cut = RenderSample(new BarChartTestOptions
        {
            TooltipInteractionMode = BarTooltipInteractionMode.Segment
        });

        cut.FindAll(SegmentGroupSelector)[0].MouseOver();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".chart-tooltip")));

        cut.FindAll(SegmentGroupSelector)[0].MouseOut();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".chart-tooltip")));
    }

    [Fact]
    public async Task ResponsiveResizeUpdatesSegmentGeometry()
    {
        var runtime = CreateRuntime();
        Services.AddSingleton<IJSRuntime>(runtime);

        var cut = RenderSample(new BarChartTestOptions
        {
            Responsive = true,
            Width = 600
        });

        var originalWidth = double.Parse(cut.Find("svg").GetAttribute("width")!, CultureInfo.InvariantCulture);
        var originalBarWidth = GetRectValues(cut, "width")[0];

        await cut.FindComponent<ChartSurface>().Instance.OnContainerWidthChanged(760);

        cut.WaitForAssertion(() =>
        {
            var updatedWidth = double.Parse(cut.Find("svg").GetAttribute("width")!, CultureInfo.InvariantCulture);
            var updatedBarWidth = GetRectValues(cut, "width")[0];

            Assert.True(updatedWidth > originalWidth);
            Assert.True(updatedBarWidth > originalBarWidth);
        });
    }

    protected List<double> GetRectValues(IRenderedFragment cut, string attribute) =>
        cut.FindAll(SegmentRectSelector)
            .Select(element => double.Parse(element.GetAttribute(attribute)!, CultureInfo.InvariantCulture))
            .ToList();

    private static RecordingJsRuntime CreateRuntime()
    {
        var observer = new RecordingJsObjectReference();
        observer.SetupHandler("dispose", _ => null!);

        var module = new RecordingJsObjectReference();
        module.SetupResult("observeElementSize", observer);

        return new RecordingJsRuntime(module);
    }

    protected static readonly BarDatum[] SampleData =
    [
        new("North",
        [
            new BarSegmentDatum("Open", 24, "#d94f3d", "#a73728"),
            new BarSegmentDatum("Closed", 12, "#198f8c", "#11696c"),
            new BarSegmentDatum("Deferred", 8, "#8a7cf6", "#6257c4")
        ]),
        new("South",
        [
            new BarSegmentDatum("Open", 18, "#d94f3d", "#a73728"),
            new BarSegmentDatum("Closed", 15, "#198f8c", "#11696c"),
            new BarSegmentDatum("Deferred", 6, "#8a7cf6", "#6257c4")
        ])
    ];

    /// <summary>Shared category datum (formerly the parallel <c>StackedDatum</c>/<c>ClusteredDatum</c>).</summary>
    public sealed record BarDatum(string Label, IReadOnlyList<BarSegmentDatum> Segments);

    /// <summary>Shared series datum (formerly the two identical <c>SegmentDatum</c> records).</summary>
    public sealed record BarSegmentDatum(string Label, double Value, string Fill = "#4e79a7", string HoverFill = "#2e5a87");

    /// <summary>High-level render knobs shared by both charts, mapped to parameters per subclass.</summary>
    protected sealed record BarChartTestOptions
    {
        public bool Horizontal { get; init; }
        public bool Responsive { get; init; }
        public double? Width { get; init; }
        public double? CornerRadius { get; init; }
        public double? BarWidthRatio { get; init; }
        public ChartLegendPlacement? LegendPlacement { get; init; }
        public BarTooltipInteractionMode? TooltipInteractionMode { get; init; }
        public bool UseColorSelector { get; init; }
        public RenderFragment? EmptyStateTemplate { get; init; }
    }
}
