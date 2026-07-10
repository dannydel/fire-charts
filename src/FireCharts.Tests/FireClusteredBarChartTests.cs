using System.Globalization;
using FireCharts.Components;
using FireCharts.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace FireCharts.Tests;

/// <summary>
/// Clustered-specific coverage. Shared behavior lives in <see cref="CategoricalBarChartTestsBase"/>;
/// this class adds the largest-segment axis math, group/series spacing geometry, per-segment
/// selection semantics, explicit max value, and the measured side tooltip.
/// </summary>
public sealed class FireClusteredBarChartTests : CategoricalBarChartTestsBase
{
    private const string GroupSelector = "g.clustered-category";

    protected override string Prefix => "clustered";

    protected override (int First, int Second) OrientationRectIndices => (0, 1);

    protected override IRenderedFragment RenderChart(IReadOnlyList<BarDatum> data, BarChartTestOptions options) =>
        RenderComponent<FireClusteredBarChart<BarDatum, BarSegmentDatum>>(parameters =>
        {
            parameters
                .Add(component => component.Items, data)
                .Add(component => component.SegmentsSelector, item => item.Segments)
                .Add(component => component.LabelSelector, item => item.Label)
                .Add(component => component.SegmentValueSelector, segment => segment.Value)
                .Add(component => component.SegmentLabelSelector, segment => segment.Label)
                .Add(component => component.Horizontal, options.Horizontal)
                .Add(component => component.Responsive, options.Responsive);

            if (options.Width is { } width) parameters.Add(component => component.Width, width);
            if (options.CornerRadius is { } cornerRadius) parameters.Add(component => component.CornerRadius, cornerRadius);
            if (options.BarWidthRatio is { } barWidthRatio) parameters.Add(component => component.BarWidthRatio, barWidthRatio);
            if (options.LegendPlacement is { } legendPlacement) parameters.Add(component => component.LegendPlacement, legendPlacement);
            if (options.TooltipInteractionMode is { } mode) parameters.Add(component => component.TooltipInteractionMode, mode);
            if (options.UseColorSelector) parameters.Add(component => component.SegmentColorSelector, segment => segment.Fill);
            if (options.EmptyStateTemplate is { } emptyState) parameters.Add(component => component.EmptyStateTemplate, emptyState);
        });

    [Fact]
    public void SanitizesInvalidValuesAndRoundsMaxValueFromLargestSegment()
    {
        var data = new[]
        {
            new BarDatum("North", [new BarSegmentDatum("Open", 67), new BarSegmentDatum("Closed", 8)]),
            new BarDatum("South", [new BarSegmentDatum("Open", -4), new BarSegmentDatum("Closed", double.NaN)])
        };

        var cut = RenderClusteredChart(data);
        var heights = GetRectValues(cut, "height");
        var axisLabels = cut.FindAll("text.axis-label").Select(element => element.TextContent).ToList();

        Assert.Equal(2, heights.Count);
        Assert.All(heights, height => Assert.True(height > 0));
        Assert.Contains("80", axisLabels);
    }

    [Fact]
    public void GroupAndSeriesSpacingChangeRenderedGeometry()
    {
        var tight = RenderComponent<FireClusteredBarChart<BarDatum, BarSegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.GroupSpacing, 0)
            .Add(component => component.SeriesSpacing, 0));

        var initialGap = GetGapBetweenRects(tight, 0, 1, "x", "width");

        var loose = RenderComponent<FireClusteredBarChart<BarDatum, BarSegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.GroupSpacing, 0.25)
            .Add(component => component.SeriesSpacing, 0.25));

        var updatedGap = GetGapBetweenRects(loose, 0, 1, "x", "width");

        Assert.True(updatedGap > initialGap);
    }

    [Fact]
    public void LegendHoverHighlightsMatchingSegmentsAndChartContentTemplateReceivesContext()
    {
        var cut = RenderComponent<FireClusteredBarChart<BarDatum, BarSegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.ChartContentTemplate, (RenderFragment<ClusteredBarChartContext<BarDatum, BarSegmentDatum>>)(context => builder =>
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
        ClusteredBarChartSegmentInteraction<BarDatum, BarSegmentDatum>? hovered = null;

        var cut = RenderComponent<FireClusteredBarChart<BarDatum, BarSegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.OnSegmentHoverChanged, (Action<ClusteredBarChartSegmentInteraction<BarDatum, BarSegmentDatum>>)(interaction => hovered = interaction)));

        var firstCategory = cut.FindAll(GroupSelector)[0];
        firstCategory.TriggerEvent("onmouseenter", new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindAll(".chart-tooltip"));
            Assert.Contains("North", cut.Markup);
            Assert.Contains("Total 44", cut.Markup);
            Assert.Contains("Deferred", cut.Markup);
            Assert.Null(hovered);
        });

        cut.FindAll(SegmentGroupSelector)[0].MouseOver();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(hovered);
            Assert.Equal("North", hovered!.CategoryLabel);
            Assert.Equal("Open", hovered.SeriesLabel);
            Assert.Equal(3, cut.FindAll("g.clustered-segment.is-hovered").Count);
        });

        cut.FindAll(GroupSelector)[0].TriggerEvent("onmouseleave", new MouseEventArgs());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".chart-tooltip")));
    }

    [Fact]
    public void SharedTooltipTemplateTakesPrecedenceOverLegacyTemplate()
    {
        var cut = RenderComponent<FireClusteredBarChart<BarDatum, BarSegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.TooltipTemplate, (RenderFragment<ClusteredBarChartSegment<BarDatum, BarSegmentDatum>>)(segment => builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "legacy-tooltip");
                builder.AddContent(2, $"legacy-{segment.SeriesLabel}");
                builder.CloseElement();
            }))
            .Add(component => component.SharedTooltipTemplate, (RenderFragment<ClusteredBarTooltipContext<BarDatum, BarSegmentDatum>>)(context => builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "shared-tooltip");
                builder.AddContent(2, $"shared-{context.CategoryLabel}-{context.Rows.Count}");
                builder.CloseElement();
            })));

        cut.FindAll(GroupSelector)[0].TriggerEvent("onmouseenter", new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("shared-North-3", cut.Markup);
            Assert.DoesNotContain("legacy-", cut.Markup);
        });
    }

    [Fact]
    public void ContainedHorizontalTooltipUsesMeasuredSidePlacement()
    {
        // Large host so the preferred "right" placement fits; the engine keeps the
        // requested side placement, which verifies the chart asks for it.
        var measurer = new FakeTooltipMeasurer(new TooltipMeasurement(5000, 5000, 80, 40));
        Services.AddSingleton<ITooltipMeasurer>(measurer);

        var cut = RenderComponent<FireClusteredBarChart<BarDatum, BarSegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.Horizontal, true)
            .Add(component => component.TooltipInteractionMode, BarTooltipInteractionMode.Segment)
            .Add(component => component.ConstrainTooltipToChartBounds, true));

        cut.FindAll(SegmentGroupSelector)[0].MouseOver();

        cut.WaitForAssertion(() =>
        {
            var tooltip = cut.Find(".chart-tooltip");
            Assert.Contains("chart-tooltip--contained", tooltip.GetAttribute("class"));
            Assert.Contains("chart-tooltip--placement-right", tooltip.GetAttribute("class"));
            var style = tooltip.GetAttribute("style")!;
            Assert.Contains("left:", style);
            Assert.Contains("top:", style);
            Assert.DoesNotContain("visibility: hidden", style);
        });

        Assert.True(measurer.MeasureCount >= 1);
    }

    [Fact]
    public void HoverFocusAndKeyboardSelectionUpdateInteractionState()
    {
        ClusteredBarChartSegmentInteraction<BarDatum, BarSegmentDatum>? hovered = null;
        ClusteredBarChartSegmentInteraction<BarDatum, BarSegmentDatum>? clicked = null;
        ClusteredBarChartSegment<BarDatum, BarSegmentDatum>? selected = null;

        var cut = RenderComponent<FireClusteredBarChart<BarDatum, BarSegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.SelectedSegment, new ClusteredBarChartSegment<BarDatum, BarSegmentDatum>(SampleData[1], SampleData[1].Segments[0], 1, 0, "South", "Open", 18, "#d94f3d", "#a73728", new SvgRect(0, 0, 0, 0), "South, Open: 18", true, false, false))
            .Add(component => component.OnSegmentHoverChanged, (Action<ClusteredBarChartSegmentInteraction<BarDatum, BarSegmentDatum>>)(interaction => hovered = interaction))
            .Add(component => component.OnSegmentClick, (Action<ClusteredBarChartSegmentInteraction<BarDatum, BarSegmentDatum>>)(interaction => clicked = interaction))
            .Add(component => component.SelectedSegmentChanged, (Action<ClusteredBarChartSegment<BarDatum, BarSegmentDatum>?>)(segment => selected = segment)));

        cut.FindAll(SegmentGroupSelector)[0].MouseOver();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(hovered);
            Assert.Equal("North", hovered!.CategoryLabel);
            Assert.Equal("Open", hovered.SeriesLabel);
            Assert.Single(cut.FindAll(".chart-tooltip"));
        });

        cut.FindAll(SegmentGroupSelector)[0].Focus();
        cut.FindAll(SegmentGroupSelector)[0].MouseOut();
        cut.WaitForAssertion(() =>
        {
            var firstGroup = cut.FindAll(SegmentGroupSelector)[0];
            Assert.Contains("is-hovered", firstGroup.GetAttribute("class"));
            Assert.Contains("is-focused", firstGroup.GetAttribute("class"));
        });

        cut.FindAll(SegmentGroupSelector)[0].Blur();
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".chart-tooltip"));
            Assert.DoesNotContain("is-focused", cut.FindAll(SegmentGroupSelector)[0].GetAttribute("class"));
        });

        cut.FindAll(SegmentGroupSelector)[0].KeyDown(new KeyboardEventArgs { Key = "Enter" });
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(selected);
            Assert.Equal("North", selected!.CategoryLabel);
            Assert.NotNull(clicked);
            Assert.Equal(24, clicked!.Value);
            Assert.Contains("is-selected", cut.FindAll(SegmentGroupSelector)[0].GetAttribute("class"));
        });
    }

    [Fact]
    public void UsesExplicitMaxValueWhenProvided()
    {
        var cut = RenderComponent<FireClusteredBarChart<BarDatum, BarSegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.MaxValue, 60d));

        Assert.Contains("60", cut.FindAll("text.axis-label").Select(element => element.TextContent));
    }

    private IRenderedComponent<FireClusteredBarChart<BarDatum, BarSegmentDatum>> RenderClusteredChart(IReadOnlyList<BarDatum> data) =>
        RenderComponent<FireClusteredBarChart<BarDatum, BarSegmentDatum>>(parameters => parameters
            .Add(component => component.Items, data)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.SegmentColorSelector, segment => segment.Fill)
            .Add(component => component.SegmentHoverColorSelector, segment => segment.HoverFill));

    private static double GetGapBetweenRects(IRenderedFragment cut, int firstIndex, int secondIndex, string startAttribute, string sizeAttribute)
    {
        var rects = cut.FindAll("rect.clustered-segment-rect");
        var firstStart = double.Parse(rects[firstIndex].GetAttribute(startAttribute)!, CultureInfo.InvariantCulture);
        var firstSize = double.Parse(rects[firstIndex].GetAttribute(sizeAttribute)!, CultureInfo.InvariantCulture);
        var secondStart = double.Parse(rects[secondIndex].GetAttribute(startAttribute)!, CultureInfo.InvariantCulture);
        return secondStart - (firstStart + firstSize);
    }
}
