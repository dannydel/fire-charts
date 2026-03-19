using System.Globalization;
using FireCharts.Components;
using FireCharts.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace FireCharts.Tests;

public sealed class FireStackedBarChartTests : TestContext
{
    [Fact]
    public void RendersCustomEmptyStateWhenNoPositiveSegmentsRemain()
    {
        var data = new[]
        {
            new StackedDatum("North", [new SegmentDatum("Open", 0)]),
            new StackedDatum("South", [new SegmentDatum("Closed", double.NaN)])
        };

        var cut = RenderComponent<FireStackedBarChart<StackedDatum, SegmentDatum>>(parameters => parameters
            .Add(component => component.Items, data)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.EmptyStateTemplate, (RenderFragment)(builder => builder.AddContent(0, "Nothing stacked"))));

        Assert.Contains("Nothing stacked", cut.Markup);
        Assert.Empty(cut.FindAll("rect.stacked-segment-rect"));
    }

    [Fact]
    public void SanitizesInvalidValuesAndRoundsMaxValueFromStackTotals()
    {
        var data = new[]
        {
            new StackedDatum("North", [new SegmentDatum("Open", 67), new SegmentDatum("Closed", 8)]),
            new StackedDatum("South", [new SegmentDatum("Open", -4), new SegmentDatum("Closed", double.NaN)])
        };

        var cut = RenderStackedChart(data);
        var heights = GetRectValues(cut, "height");
        var axisLabels = cut.FindAll("text.axis-label").Select(element => element.TextContent).ToList();

        Assert.Equal(2, heights.Count);
        Assert.All(heights, height => Assert.True(height > 0));
        Assert.Contains("75", axisLabels);
    }

    [Fact]
    public void SupportsVerticalAndHorizontalLayouts()
    {
        var data = new[]
        {
            new StackedDatum("North", [new SegmentDatum("Open", 30), new SegmentDatum("Closed", 20)]),
            new StackedDatum("South", [new SegmentDatum("Open", 18), new SegmentDatum("Closed", 12)])
        };

        var vertical = RenderStackedChart(data);
        var horizontal = RenderComponent<FireStackedBarChart<StackedDatum, SegmentDatum>>(parameters => parameters
            .Add(component => component.Items, data)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.Horizontal, true));

        var verticalRects = vertical.FindAll("rect.stacked-segment-rect");
        var horizontalRects = horizontal.FindAll("rect.stacked-segment-rect");

        Assert.NotEqual(verticalRects[0].GetAttribute("x"), verticalRects[2].GetAttribute("x"));
        Assert.Equal(horizontalRects[0].GetAttribute("x"), horizontalRects[2].GetAttribute("x"));
        Assert.NotEqual(horizontalRects[0].GetAttribute("y"), horizontalRects[2].GetAttribute("y"));
    }

    [Fact]
    public void BarWidthRatioChangesRenderedBarThickness()
    {
        var cut = RenderComponent<FireStackedBarChart<StackedDatum, SegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.BarWidthRatio, 0.35));

        var narrowWidth = double.Parse(cut.Find("rect.stacked-segment-rect").GetAttribute("width")!, CultureInfo.InvariantCulture);

        cut.SetParametersAndRender(parameters => parameters.Add(component => component.BarWidthRatio, 0.9));

        var wideWidth = double.Parse(cut.Find("rect.stacked-segment-rect").GetAttribute("width")!, CultureInfo.InvariantCulture);

        Assert.True(wideWidth > narrowWidth);
    }

    [Fact]
    public void CornerRadiusChangesRenderedSegmentRounding()
    {
        var cut = RenderComponent<FireStackedBarChart<StackedDatum, SegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.CornerRadius, 2));

        Assert.Equal("2.0", cut.Find("rect.stacked-segment-rect").GetAttribute("rx"));

        cut.SetParametersAndRender(parameters => parameters.Add(component => component.CornerRadius, 9));

        Assert.Equal("9.0", cut.Find("rect.stacked-segment-rect").GetAttribute("rx"));
        Assert.Equal("9.0", cut.Find("rect.stacked-segment-rect").GetAttribute("ry"));
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
        var cut = RenderComponent<FireStackedBarChart<StackedDatum, SegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.SegmentColorSelector, segment => segment.Fill)
            .Add(component => component.LegendPlacement, placement));

        Assert.Contains(expectedClass, cut.Find(".fire-stacked-bar-chart-shell").GetAttribute("class"));
        Assert.Equal(3, cut.FindAll(".stacked-bar-legend__item").Count);
    }

    [Fact]
    public void LegendHoverHighlightsMatchingSegmentsAndChartContentTemplateReceivesContext()
    {
        var cut = RenderComponent<FireStackedBarChart<StackedDatum, SegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.ChartContentTemplate, (RenderFragment<StackedBarChartContext<StackedDatum, SegmentDatum>>)(context => builder =>
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
    public void HoverFocusAndKeyboardSelectionUpdateInteractionState()
    {
        ChartPointInteraction<StackedDatum>? hovered = null;
        ChartPointInteraction<StackedDatum>? clicked = null;
        StackedDatum? selected = null;

        var cut = RenderComponent<FireStackedBarChart<StackedDatum, SegmentDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.SelectedItem, SampleData[1])
            .Add(component => component.OnPointHoverChanged, (Action<ChartPointInteraction<StackedDatum>>)(interaction => hovered = interaction))
            .Add(component => component.OnPointClick, (Action<ChartPointInteraction<StackedDatum>>)(interaction => clicked = interaction))
            .Add(component => component.SelectedItemChanged, (Action<StackedDatum?>)(item => selected = item)));

        cut.FindAll("g.stacked-segment")[0].MouseOver();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(hovered);
            Assert.Equal("North - Open", hovered!.Label);
            Assert.Single(cut.FindAll(".chart-tooltip"));
        });

        cut.FindAll("g.stacked-segment")[0].Focus();
        cut.FindAll("g.stacked-segment")[0].MouseOut();
        cut.WaitForAssertion(() =>
        {
            var firstGroup = cut.FindAll("g.stacked-segment")[0];
            Assert.Contains("is-hovered", firstGroup.GetAttribute("class"));
            Assert.Contains("is-focused", firstGroup.GetAttribute("class"));
        });

        cut.FindAll("g.stacked-segment")[0].Blur();
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".chart-tooltip"));
            Assert.DoesNotContain("is-focused", cut.FindAll("g.stacked-segment")[0].GetAttribute("class"));
        });

        cut.FindAll("g.stacked-segment")[0].KeyDown(new KeyboardEventArgs { Key = "Enter" });
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(selected);
            Assert.Equal("North", selected!.Label);
            Assert.NotNull(clicked);
            Assert.Equal(24, clicked!.Value);
            Assert.Contains("is-selected", cut.FindAll("g.stacked-segment")[0].GetAttribute("class"));
        });
    }

    [Fact]
    public async Task ResponsiveResizeUpdatesSegmentGeometry()
    {
        var runtime = CreateRuntime();
        Services.AddSingleton<IJSRuntime>(runtime);

        var cut = RenderComponent<FireStackedBarChart<StackedDatum, SegmentDatum>>(parameters => parameters
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

    private IRenderedComponent<FireStackedBarChart<StackedDatum, SegmentDatum>> RenderStackedChart(IReadOnlyList<StackedDatum> data) =>
        RenderComponent<FireStackedBarChart<StackedDatum, SegmentDatum>>(parameters => parameters
            .Add(component => component.Items, data)
            .Add(component => component.SegmentsSelector, item => item.Segments)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SegmentValueSelector, segment => segment.Value)
            .Add(component => component.SegmentLabelSelector, segment => segment.Label)
            .Add(component => component.SegmentColorSelector, segment => segment.Fill)
            .Add(component => component.SegmentHoverColorSelector, segment => segment.HoverFill));

    private static List<double> GetRectValues(IRenderedFragment cut, string attribute) =>
        cut.FindAll("rect.stacked-segment-rect")
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

    private static readonly StackedDatum[] SampleData =
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

    public sealed record StackedDatum(string Label, IReadOnlyList<SegmentDatum> Segments);

    public sealed record SegmentDatum(string Label, double Value, string Fill = "#4e79a7", string HoverFill = "#2e5a87");
}
