using System.Globalization;
using FireCharts.Components;
using FireCharts.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace FireCharts.Tests;

/// <summary>
/// Stacked-specific coverage. Shared behavior lives in <see cref="CategoricalBarChartTestsBase"/>;
/// this class adds the stack-total axis math, bar-width geometry, whole-item selection semantics,
/// and the corner-anchored contained tooltip.
/// </summary>
public sealed class FireStackedBarChartTests : CategoricalBarChartTestsBase
{
    private const string GroupSelector = "g.stacked-bar";

    protected override string Prefix => "stacked";

    protected override (int First, int Second) OrientationRectIndices => (0, 2);

    protected override IRenderedFragment RenderChart(IReadOnlyList<BarDatum> data, BarChartTestOptions options) =>
        RenderComponent<FireStackedBarChart<BarDatum, BarSegmentDatum>>(parameters =>
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
    public void SanitizesInvalidValuesAndRoundsMaxValueFromStackTotals()
    {
        var data = new[]
        {
            new BarDatum("North", [new BarSegmentDatum("Open", 67), new BarSegmentDatum("Closed", 8)]),
            new BarDatum("South", [new BarSegmentDatum("Open", -4), new BarSegmentDatum("Closed", double.NaN)])
        };

        var cut = RenderStackedChart(data);
        var heights = GetRectValues(cut, "height");
        var axisLabels = cut.FindAll("text.axis-label").Select(element => element.TextContent).ToList();

        Assert.Equal(2, heights.Count);
        Assert.All(heights, height => Assert.True(height > 0));
        Assert.Contains("80", axisLabels);
    }

    [Fact]
    public void BarWidthRatioChangesRenderedBarThickness()
    {
        var narrow = RenderSample(new BarChartTestOptions { BarWidthRatio = 0.35 });
        var narrowWidth = double.Parse(narrow.Find(SegmentRectSelector).GetAttribute("width")!, CultureInfo.InvariantCulture);

        var wide = RenderSample(new BarChartTestOptions { BarWidthRatio = 0.9 });
        var wideWidth = double.Parse(wide.Find(SegmentRectSelector).GetAttribute("width")!, CultureInfo.InvariantCulture);

        Assert.True(wideWidth > narrowWidth);
    }

    [Fact]
    public void LegendHoverHighlightsMatchingSegmentsAndChartContentTemplateReceivesContext()
    {
        var cut = RenderComponent<FireStackedBarChart<BarDatum, BarSegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.ChartContentTemplate, (RenderFragment<StackedBarChartContext<BarDatum, BarSegmentDatum>>)(context => builder =>
            {
                builder.OpenElement(0, "text");
                builder.AddAttribute(1, "class", "custom-chart-content");
                builder.AddContent(2, $"{context.Segments.Count}:{context.MaxValue.ToString("F0", CultureInfo.InvariantCulture)}");
                builder.CloseElement();
            })));

        cut.FindAll("button.stacked-bar-legend__item")[0].MouseOver();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, cut.FindAll("g.stacked-segment.is-legend-active").Count);
            Assert.Contains("6:50", cut.Markup);
        });
    }

    [Fact]
    public void SharedTooltipShowsAllRowsAndPersistsUntilLeavingBarOwner()
    {
        ChartPointInteraction<BarDatum>? hovered = null;

        var cut = RenderComponent<FireStackedBarChart<BarDatum, BarSegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.OnPointHoverChanged, (Action<ChartPointInteraction<BarDatum>>)(interaction => hovered = interaction)));

        var firstBar = cut.FindAll(GroupSelector)[0];
        firstBar.TriggerEvent("onmouseenter", new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindAll(".chart-tooltip"));
            Assert.Contains("North", cut.Markup);
            Assert.Contains("Total 44", cut.Markup);
            Assert.Contains("Closed", cut.Markup);
            Assert.Null(hovered);
        });

        cut.FindAll(SegmentGroupSelector)[0].MouseOver();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(hovered);
            Assert.Equal("North - Open", hovered!.Label);
            Assert.Equal(3, cut.FindAll("g.stacked-segment.is-hovered").Count);
        });

        cut.FindAll(SegmentGroupSelector)[0].MouseOut();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".chart-tooltip")));

        cut.FindAll(GroupSelector)[0].TriggerEvent("onmouseleave", new MouseEventArgs());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".chart-tooltip")));
    }

    [Fact]
    public void SharedTooltipTemplateTakesPrecedenceOverLegacyTemplate()
    {
        var cut = RenderComponent<FireStackedBarChart<BarDatum, BarSegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.TooltipTemplate, (RenderFragment<StackedBarChartSegment<BarDatum, BarSegmentDatum>>)(segment => builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "legacy-tooltip");
                builder.AddContent(2, $"legacy-{segment.SegmentLabel}");
                builder.CloseElement();
            }))
            .Add(component => component.SharedTooltipTemplate, (RenderFragment<StackedBarTooltipContext<BarDatum, BarSegmentDatum>>)(context => builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "shared-tooltip");
                builder.AddContent(2, $"shared-{context.BarLabel}-{context.Rows.Count}");
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
    public void ContainedVerticalSharedTooltipUsesCornerAnchoredSidePlacement()
    {
        // Large host so the preferred "right" placement fits; the engine keeps the
        // requested side placement, which verifies the chart asks for it.
        var measurer = new FakeTooltipMeasurer(new TooltipMeasurement(5000, 5000, 80, 40));
        Services.AddSingleton<ITooltipMeasurer>(measurer);

        var cut = RenderComponent<FireStackedBarChart<BarDatum, BarSegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.ConstrainTooltipToChartBounds, true));

        cut.FindAll(GroupSelector)[0].TriggerEvent("onmouseenter", new MouseEventArgs());

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
        ChartPointInteraction<BarDatum>? hovered = null;
        ChartPointInteraction<BarDatum>? clicked = null;
        BarDatum? selected = null;

        var cut = RenderComponent<FireStackedBarChart<BarDatum, BarSegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.SelectedItem, SampleData[1])
            .Add(component => component.OnPointHoverChanged, (Action<ChartPointInteraction<BarDatum>>)(interaction => hovered = interaction))
            .Add(component => component.OnPointClick, (Action<ChartPointInteraction<BarDatum>>)(interaction => clicked = interaction))
            .Add(component => component.SelectedItemChanged, (Action<BarDatum?>)(item => selected = item)));

        cut.FindAll(SegmentGroupSelector)[0].MouseOver();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(hovered);
            Assert.Equal("North - Open", hovered!.Label);
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
            Assert.Equal("North", selected!.Label);
            Assert.NotNull(clicked);
            Assert.Equal(24, clicked!.Value);
            Assert.Contains("is-selected", cut.FindAll(SegmentGroupSelector)[0].GetAttribute("class"));
        });
    }

    private IRenderedComponent<FireStackedBarChart<BarDatum, BarSegmentDatum>> RenderStackedChart(IReadOnlyList<BarDatum> data) =>
        RenderComponent<FireStackedBarChart<BarDatum, BarSegmentDatum>>(parameters => parameters
            .Add(component => component.Items, data)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.SegmentColorSelector, segment => segment.Fill)
            .Add(component => component.SegmentHoverColorSelector, segment => segment.HoverFill));
}
