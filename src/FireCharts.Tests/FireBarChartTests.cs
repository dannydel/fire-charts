using System.Globalization;
using FireCharts.Components;
using FireCharts.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace FireCharts.Tests;

public sealed class FireBarChartTests : TestContext
{
    [Fact]
    public void RendersCustomEmptyStateWhenThereAreNoItems()
    {
        var cut = RenderComponent<FireBarChart<BarDatum>>(parameters => parameters
            .Add(component => component.Items, Array.Empty<BarDatum>())
            .Add(component => component.ValueSelector, item => item.Value)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.EmptyStateTemplate, (RenderFragment)(builder => builder.AddContent(0, "Nothing to chart"))));

        Assert.Contains("Nothing to chart", cut.Markup);
        Assert.Empty(cut.FindAll("rect.bar-rect"));
    }

    [Fact]
    public void SanitizesInvalidValuesAndRoundsMaxValueToNiceAxisTicks()
    {
        var data = new[]
        {
            new BarDatum("Positive", 67),
            new BarDatum("Negative", -4),
            new BarDatum("Invalid", double.NaN)
        };

        var cut = RenderBarChart(data);
        var heights = GetRectValues(cut, "height");
        var axisLabels = cut.FindAll("text.axis-label").Select(element => element.TextContent).ToList();

        Assert.Equal(3, heights.Count);
        Assert.True(heights[0] > 0);
        Assert.Equal(0, heights[1]);
        Assert.Equal(0, heights[2]);
        Assert.Contains("75", axisLabels);
    }

    [Fact]
    public void SupportsVerticalAndHorizontalLayouts()
    {
        var data = new[]
        {
            new BarDatum("One", 50),
            new BarDatum("Two", 50)
        };

        var vertical = RenderBarChart(data);
        var horizontal = RenderComponent<FireBarChart<BarDatum>>(parameters => parameters
            .Add(component => component.Items, data)
            .Add(component => component.ValueSelector, item => item.Value)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.Horizontal, true));

        var verticalRects = vertical.FindAll("rect.bar-rect");
        var horizontalRects = horizontal.FindAll("rect.bar-rect");

        Assert.NotEqual(verticalRects[0].GetAttribute("x"), verticalRects[1].GetAttribute("x"));
        Assert.Equal(horizontalRects[0].GetAttribute("x"), horizontalRects[1].GetAttribute("x"));
        Assert.NotEqual(horizontalRects[0].GetAttribute("y"), horizontalRects[1].GetAttribute("y"));
    }

    [Fact]
    public void CanHideGridAxisAndValueLabels()
    {
        var cut = RenderComponent<FireBarChart<BarDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.ValueSelector, item => item.Value)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.ShowGridLines, false)
            .Add(component => component.ShowAxisLabels, false)
            .Add(component => component.ShowValueLabels, false));

        Assert.Empty(cut.FindAll("line.grid-line"));
        Assert.Empty(cut.FindAll("text.axis-label"));
        Assert.Empty(cut.FindAll("text.bar-value"));
    }

    [Fact]
    public void AppliesCustomColorsValueTemplateAndTooltipTemplate()
    {
        var data = new[]
        {
            new BarDatum("Engine 1", 12, "#111111", "#333333"),
            new BarDatum("Engine 2", 18, "#222222", "#444444")
        };

        var cut = RenderComponent<FireBarChart<BarDatum>>(parameters => parameters
            .Add(component => component.Items, data)
            .Add(component => component.ValueSelector, item => item.Value)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.ColorSelector, item => item.Fill)
            .Add(component => component.HoverColorSelector, item => item.HoverFill)
            .Add(component => component.PointValueTemplate, (RenderFragment<BarChartPoint<BarDatum>>)(point => builder =>
            {
                builder.OpenElement(0, "text");
                builder.AddAttribute(1, "class", "custom-value");
                builder.AddContent(2, $"{point.Label} units");
                builder.CloseElement();
            }))
            .Add(component => component.TooltipTemplate, (RenderFragment<BarChartPoint<BarDatum>>)(point => builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "custom-tooltip");
                builder.AddContent(2, $"tooltip-{point.Label}");
                builder.CloseElement();
            })));

        cut.FindAll("g.bar-group")[0].MouseOver();

        cut.WaitForAssertion(() =>
        {
            var rect = cut.Find("rect.bar-rect");
            Assert.Contains("--bar-color: #111111; --bar-hover-color: #333333", rect.GetAttribute("style"));
            Assert.Contains("Engine 1 units", cut.Markup);
            Assert.Contains("tooltip-Engine 1", cut.Markup);
        });
    }

    [Fact]
    public void HoverFocusAndKeyboardSelectionUpdateInteractionState()
    {
        var data = new[]
        {
            new BarDatum("Engine 1", 12),
            new BarDatum("Engine 2", 18)
        };

        ChartPointInteraction<BarDatum>? hovered = null;
        ChartPointInteraction<BarDatum>? clicked = null;
        BarDatum? selected = null;

        var cut = RenderComponent<FireBarChart<BarDatum>>(parameters => parameters
            .Add(component => component.Items, data)
            .Add(component => component.ValueSelector, item => item.Value)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.SelectedItem, data[1])
            .Add(component => component.OnPointHoverChanged, (Action<ChartPointInteraction<BarDatum>>)(interaction => hovered = interaction))
            .Add(component => component.OnPointClick, (Action<ChartPointInteraction<BarDatum>>)(interaction => clicked = interaction))
            .Add(component => component.SelectedItemChanged, (Action<BarDatum?>)(item => selected = item)));

        cut.FindAll("g.bar-group")[0].MouseOver();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(hovered);
            Assert.Equal("Engine 1", hovered!.Label);
            Assert.Single(cut.FindAll(".chart-tooltip"));
        });

        cut.FindAll("g.bar-group")[0].Focus();
        cut.FindAll("g.bar-group")[0].MouseOut();
        cut.WaitForAssertion(() =>
        {
            var firstGroup = cut.FindAll("g.bar-group")[0];
            Assert.Contains("is-hovered", firstGroup.GetAttribute("class"));
            Assert.Contains("is-focused", firstGroup.GetAttribute("class"));
            Assert.Single(cut.FindAll(".chart-tooltip"));
        });

        cut.FindAll("g.bar-group")[0].Blur();
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".chart-tooltip"));
            Assert.DoesNotContain("is-focused", cut.FindAll("g.bar-group")[0].GetAttribute("class"));
        });

        cut.FindAll("g.bar-group")[0].KeyDown(new KeyboardEventArgs { Key = "Enter" });
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(selected);
            Assert.Equal("Engine 1", selected!.Label);
            Assert.NotNull(clicked);
            Assert.Equal(12, clicked!.Value);
            Assert.Contains("is-selected", cut.FindAll("g.bar-group")[0].GetAttribute("class"));
        });
    }

    [Fact]
    public async Task ResponsiveResizeUpdatesBarGeometry()
    {
        var runtime = CreateRuntime();
        Services.AddSingleton<IJSRuntime>(runtime);

        var cut = RenderComponent<FireBarChart<BarDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.ValueSelector, item => item.Value)
            .Add(component => component.LabelSelector, item => item.Label)
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

    private IRenderedComponent<FireBarChart<BarDatum>> RenderBarChart(IReadOnlyList<BarDatum> data) =>
        RenderComponent<FireBarChart<BarDatum>>(parameters => parameters
            .Add(component => component.Items, data)
            .Add(component => component.ValueSelector, item => item.Value)
            .Add(component => component.LabelSelector, item => item.Label));

    private static List<double> GetRectValues(IRenderedFragment cut, string attribute) =>
        cut.FindAll("rect.bar-rect")
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

    private static readonly BarDatum[] SampleData =
    [
        new("Engine 1", 12),
        new("Engine 2", 18),
        new("Truck 7", 9)
    ];

    private sealed record BarDatum(string Label, double Value, string Fill = "#4e79a7", string HoverFill = "#2e5a87");
}
