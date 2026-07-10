using System.Globalization;
using FireCharts.Components;
using FireCharts.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace FireCharts.Tests;

public sealed class FireClusteredBarChartTests : TestContext
{
    [Fact]
    public void RendersCustomEmptyStateWhenNoPositiveSegmentsRemain()
    {
        var data = new[]
        {
            new ClusteredDatum("North", [new SegmentDatum("Open", 0)]),
            new ClusteredDatum("South", [new SegmentDatum("Closed", double.NaN)])
        };

        var cut = RenderComponent<FireClusteredBarChart<ClusteredDatum, SegmentDatum>>(parameters => parameters
            .Add(component => component.Items, data)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.EmptyStateTemplate, (RenderFragment)(builder => builder.AddContent(0, "Nothing clustered"))));

        Assert.Contains("Nothing clustered", cut.Markup);
        Assert.Empty(cut.FindAll("rect.clustered-segment-rect"));
    }

    [Fact]
    public void SanitizesInvalidValuesAndRoundsMaxValueFromLargestSegment()
    {
        var data = new[]
        {
            new ClusteredDatum("North", [new SegmentDatum("Open", 67), new SegmentDatum("Closed", 8)]),
            new ClusteredDatum("South", [new SegmentDatum("Open", -4), new SegmentDatum("Closed", double.NaN)])
        };

        var cut = RenderClusteredChart(data);
        var heights = GetRectValues(cut, "height");
        var axisLabels = cut.FindAll("text.axis-label").Select(element => element.TextContent).ToList();

        Assert.Equal(2, heights.Count);
        Assert.All(heights, height => Assert.True(height > 0));
        Assert.Contains("80", axisLabels);
    }

    [Fact]
    public void SupportsVerticalAndHorizontalLayouts()
    {
        var data = new[]
        {
            new ClusteredDatum("North", [new SegmentDatum("Open", 30), new SegmentDatum("Closed", 20)]),
            new ClusteredDatum("South", [new SegmentDatum("Open", 18), new SegmentDatum("Closed", 12)])
        };

        var vertical = RenderClusteredChart(data);
        var horizontal = RenderComponent<FireClusteredBarChart<ClusteredDatum, SegmentDatum>>(parameters => parameters
            .Add(component => component.Items, data)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.Horizontal, true));

        var verticalRects = vertical.FindAll("rect.clustered-segment-rect");
        var horizontalRects = horizontal.FindAll("rect.clustered-segment-rect");

        Assert.NotEqual(verticalRects[0].GetAttribute("x"), verticalRects[1].GetAttribute("x"));
        Assert.Equal(horizontalRects[0].GetAttribute("x"), horizontalRects[1].GetAttribute("x"));
        Assert.NotEqual(horizontalRects[0].GetAttribute("y"), horizontalRects[1].GetAttribute("y"));
    }

    [Fact]
    public void GroupAndSeriesSpacingChangeRenderedGeometry()
    {
        var cut = RenderComponent<FireClusteredBarChart<ClusteredDatum, SegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.GroupSpacing, 0)
            .Add(component => component.SeriesSpacing, 0));

        var initialGap = GetGapBetweenRects(cut, 0, 1, "x", "width");

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.GroupSpacing, 0.25)
            .Add(component => component.SeriesSpacing, 0.25));

        var updatedGap = GetGapBetweenRects(cut, 0, 1, "x", "width");

        Assert.True(updatedGap > initialGap);
    }

    [Fact]
    public void CornerRadiusChangesRenderedSegmentRounding()
    {
        var cut = RenderComponent<FireClusteredBarChart<ClusteredDatum, SegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.CornerRadius, 2));

        Assert.Equal("2.0", cut.Find("rect.clustered-segment-rect").GetAttribute("rx"));

        cut.SetParametersAndRender(parameters => parameters.Add(component => component.CornerRadius, 9));

        Assert.Equal("9.0", cut.Find("rect.clustered-segment-rect").GetAttribute("rx"));
        Assert.Equal("9.0", cut.Find("rect.clustered-segment-rect").GetAttribute("ry"));
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
        var cut = RenderComponent<FireClusteredBarChart<ClusteredDatum, SegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.SegmentColorSelector, segment => segment.Fill)
            .Add(component => component.LegendPlacement, placement));

        Assert.Contains(expectedClass, cut.Find(".fire-clustered-bar-chart-shell").GetAttribute("class"));
        Assert.Equal(3, cut.FindAll(".clustered-bar-legend__item").Count);
    }

    [Fact]
    public void LegendHoverHighlightsMatchingSegmentsAndChartContentTemplateReceivesContext()
    {
        var cut = RenderComponent<FireClusteredBarChart<ClusteredDatum, SegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.ChartContentTemplate, (RenderFragment<ClusteredBarChartContext<ClusteredDatum, SegmentDatum>>)(context => builder =>
            {
                builder.OpenElement(0, "text");
                builder.AddAttribute(1, "class", "custom-chart-content");
                builder.AddContent(2, $"{context.Segments.Count}:{context.MaxValue.ToString("F0", CultureInfo.InvariantCulture)}");
                builder.CloseElement();
            })));

        cut.FindAll("button.clustered-bar-legend__item")[0].MouseOver();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, cut.FindAll("g.clustered-segment.is-legend-active").Count);
            Assert.Contains("6:30", cut.Markup);
        });
    }

    [Fact]
    public void SharedTooltipShowsCategoryRowsAndSupportsOwnerOnlyHover()
    {
        ClusteredBarChartSegmentInteraction<ClusteredDatum, SegmentDatum>? hovered = null;

        var cut = RenderComponent<FireClusteredBarChart<ClusteredDatum, SegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.OnSegmentHoverChanged, (Action<ClusteredBarChartSegmentInteraction<ClusteredDatum, SegmentDatum>>)(interaction => hovered = interaction)));

        var firstCategory = cut.FindAll("g.clustered-category")[0];
        firstCategory.TriggerEvent("onmouseenter", new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindAll(".chart-tooltip"));
            Assert.Contains("North", cut.Markup);
            Assert.Contains("Total 44", cut.Markup);
            Assert.Contains("Deferred", cut.Markup);
            Assert.Null(hovered);
        });

        cut.FindAll("g.clustered-segment")[0].MouseOver();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(hovered);
            Assert.Equal("North", hovered!.CategoryLabel);
            Assert.Equal("Open", hovered.SeriesLabel);
            Assert.Equal(3, cut.FindAll("g.clustered-segment.is-hovered").Count);
        });

        cut.FindAll("g.clustered-category")[0].TriggerEvent("onmouseleave", new MouseEventArgs());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".chart-tooltip")));
    }

    [Fact]
    public void SharedTooltipTemplateTakesPrecedenceOverLegacyTemplate()
    {
        var cut = RenderComponent<FireClusteredBarChart<ClusteredDatum, SegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.TooltipTemplate, (RenderFragment<ClusteredBarChartSegment<ClusteredDatum, SegmentDatum>>)(segment => builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "legacy-tooltip");
                builder.AddContent(2, $"legacy-{segment.SeriesLabel}");
                builder.CloseElement();
            }))
            .Add(component => component.SharedTooltipTemplate, (RenderFragment<ClusteredBarTooltipContext<ClusteredDatum, SegmentDatum>>)(context => builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "shared-tooltip");
                builder.AddContent(2, $"shared-{context.CategoryLabel}-{context.Rows.Count}");
                builder.CloseElement();
            })));

        cut.FindAll("g.clustered-category")[0].TriggerEvent("onmouseenter", new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("shared-North-3", cut.Markup);
            Assert.DoesNotContain("legacy-", cut.Markup);
        });
    }

    [Fact]
    public void ContainedHorizontalTooltipUsesMeasuredSidePlacement()
    {
        var module = new RecordingJsObjectReference();
        module.SetupHandler("resolveTooltipPosition", _ => """{"left":52,"top":78,"placement":"right"}""");

        var runtime = new RecordingJsRuntime(module);
        Services.AddSingleton<IJSRuntime>(runtime);

        var cut = RenderComponent<FireClusteredBarChart<ClusteredDatum, SegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.Horizontal, true)
            .Add(component => component.TooltipInteractionMode, BarTooltipInteractionMode.Segment)
            .Add(component => component.ConstrainTooltipToChartBounds, true));

        cut.FindAll("g.clustered-segment")[0].MouseOver();

        cut.WaitForAssertion(() =>
        {
            var tooltip = cut.Find(".chart-tooltip");
            Assert.Contains("chart-tooltip--contained", tooltip.GetAttribute("class"));
            Assert.Contains("chart-tooltip--placement-right", tooltip.GetAttribute("class"));
            Assert.Equal("left: 52.0px; top: 78.0px;", tooltip.GetAttribute("style"));
        });

        var invocation = Assert.Single(module.Invocations, call => call.Identifier == "resolveTooltipPosition");
        Assert.Equal("right", invocation.Arguments[4]);
    }

    [Fact]
    public void SegmentTooltipModePreservesLegacyMouseoutBehavior()
    {
        var cut = RenderComponent<FireClusteredBarChart<ClusteredDatum, SegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.TooltipInteractionMode, BarTooltipInteractionMode.Segment));

        cut.FindAll("g.clustered-segment")[0].MouseOver();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".chart-tooltip")));

        cut.FindAll("g.clustered-segment")[0].MouseOut();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".chart-tooltip")));
    }

    [Fact]
    public void HoverFocusAndKeyboardSelectionUpdateInteractionState()
    {
        ClusteredBarChartSegmentInteraction<ClusteredDatum, SegmentDatum>? hovered = null;
        ClusteredBarChartSegmentInteraction<ClusteredDatum, SegmentDatum>? clicked = null;
        ClusteredBarChartSegment<ClusteredDatum, SegmentDatum>? selected = null;

        var cut = RenderComponent<FireClusteredBarChart<ClusteredDatum, SegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.SelectedSegment, new ClusteredBarChartSegment<ClusteredDatum, SegmentDatum>(SampleData[1], SampleData[1].Segments[0], 1, 0, "South", "Open", 18, "#d94f3d", "#a73728", new SvgRect(0, 0, 0, 0), "South, Open: 18", true, false, false))
            .Add(component => component.OnSegmentHoverChanged, (Action<ClusteredBarChartSegmentInteraction<ClusteredDatum, SegmentDatum>>)(interaction => hovered = interaction))
            .Add(component => component.OnSegmentClick, (Action<ClusteredBarChartSegmentInteraction<ClusteredDatum, SegmentDatum>>)(interaction => clicked = interaction))
            .Add(component => component.SelectedSegmentChanged, (Action<ClusteredBarChartSegment<ClusteredDatum, SegmentDatum>?>)(segment => selected = segment)));

        cut.FindAll("g.clustered-segment")[0].MouseOver();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(hovered);
            Assert.Equal("North", hovered!.CategoryLabel);
            Assert.Equal("Open", hovered.SeriesLabel);
            Assert.Single(cut.FindAll(".chart-tooltip"));
        });

        cut.FindAll("g.clustered-segment")[0].Focus();
        cut.FindAll("g.clustered-segment")[0].MouseOut();
        cut.WaitForAssertion(() =>
        {
            var firstGroup = cut.FindAll("g.clustered-segment")[0];
            Assert.Contains("is-hovered", firstGroup.GetAttribute("class"));
            Assert.Contains("is-focused", firstGroup.GetAttribute("class"));
        });

        cut.FindAll("g.clustered-segment")[0].Blur();
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".chart-tooltip"));
            Assert.DoesNotContain("is-focused", cut.FindAll("g.clustered-segment")[0].GetAttribute("class"));
        });

        cut.FindAll("g.clustered-segment")[0].KeyDown(new KeyboardEventArgs { Key = "Enter" });
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(selected);
            Assert.Equal("North", selected!.CategoryLabel);
            Assert.NotNull(clicked);
            Assert.Equal(24, clicked!.Value);
            Assert.Contains("is-selected", cut.FindAll("g.clustered-segment")[0].GetAttribute("class"));
        });
    }

    [Fact]
    public void UsesExplicitMaxValueWhenProvided()
    {
        var cut = RenderComponent<FireClusteredBarChart<ClusteredDatum, SegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.MaxValue, 60d));

        Assert.Contains("60", cut.FindAll("text.axis-label").Select(element => element.TextContent));
    }

    [Fact]
    public async Task ResponsiveResizeUpdatesSegmentGeometry()
    {
        var runtime = CreateRuntime();
        Services.AddSingleton<IJSRuntime>(runtime);

        var cut = RenderComponent<FireClusteredBarChart<ClusteredDatum, SegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.Responsive, true)
            .Add(component => component.Width, 600));

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

    private IRenderedComponent<FireClusteredBarChart<ClusteredDatum, SegmentDatum>> RenderClusteredChart(IReadOnlyList<ClusteredDatum> data) =>
        RenderComponent<FireClusteredBarChart<ClusteredDatum, SegmentDatum>>(parameters => parameters
            .Add(component => component.Items, data)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.SegmentColorSelector, segment => segment.Fill)
            .Add(component => component.SegmentHoverColorSelector, segment => segment.HoverFill));

    private static List<double> GetRectValues(IRenderedFragment cut, string attribute) =>
        cut.FindAll("rect.clustered-segment-rect")
            .Select(element => double.Parse(element.GetAttribute(attribute)!, CultureInfo.InvariantCulture))
            .ToList();

    private static double GetGapBetweenRects(IRenderedFragment cut, int firstIndex, int secondIndex, string startAttribute, string sizeAttribute)
    {
        var rects = cut.FindAll("rect.clustered-segment-rect");
        var firstStart = double.Parse(rects[firstIndex].GetAttribute(startAttribute)!, CultureInfo.InvariantCulture);
        var firstSize = double.Parse(rects[firstIndex].GetAttribute(sizeAttribute)!, CultureInfo.InvariantCulture);
        var secondStart = double.Parse(rects[secondIndex].GetAttribute(startAttribute)!, CultureInfo.InvariantCulture);
        return secondStart - (firstStart + firstSize);
    }

    private static RecordingJsRuntime CreateRuntime()
    {
        var observer = new RecordingJsObjectReference();
        observer.SetupHandler("dispose", _ => null!);

        var module = new RecordingJsObjectReference();
        module.SetupResult("observeElementSize", observer);

        return new RecordingJsRuntime(module);
    }

    private static readonly ClusteredDatum[] SampleData =
    [
        new("North",
        [
            new SegmentDatum("Open", 24, "#d94f3d", "#a73728"),
            new SegmentDatum("Closed", 12, "#198f8c", "#11696c"),
            new SegmentDatum("Deferred", 8, "#8a7cf6", "#6257c4")
        ]),
        new("South",
        [
            new SegmentDatum("Open", 18, "#d94f3d", "#a73728"),
            new SegmentDatum("Closed", 15, "#198f8c", "#11696c"),
            new SegmentDatum("Deferred", 6, "#8a7cf6", "#6257c4")
        ])
    ];

    public sealed record ClusteredDatum(string Label, IReadOnlyList<SegmentDatum> Segments);

    public sealed record SegmentDatum(string Label, double Value, string Fill = "#4e79a7", string HoverFill = "#2e5a87");
}
