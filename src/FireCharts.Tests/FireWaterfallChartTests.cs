using System.Globalization;
using Bunit;
using FireCharts.Components;
using FireCharts.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace FireCharts.Tests;

public sealed class FireWaterfallChartTests : TestContext
{
    [Fact]
    public void RendersCustomEmptyStateWhenThereAreNoValidItems()
    {
        var data = new[]
        {
            new WaterfallDatum("Start", double.NaN, WaterfallStepType.Start),
            new WaterfallDatum("Change", double.PositiveInfinity, WaterfallStepType.Change)
        };

        var cut = RenderComponent<FireWaterfallChart<WaterfallDatum>>(parameters => parameters
            .Add(component => component.Items, data)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.ValueSelector, item => item.Value)
            .Add(component => component.StepTypeSelector, item => item.StepType)
            .Add(component => component.EmptyStateTemplate, (RenderFragment)(builder => builder.AddContent(0, "Nothing to chart"))));

        Assert.Contains("Nothing to chart", cut.Markup);
        Assert.Empty(cut.FindAll("rect.waterfall-bar"));
    }

    [Fact]
    public void ComputesRunningTotalsForStartChangeSubtotalAndTotal()
    {
        var cut = RenderWaterfall(SampleData);

        var bars = cut.FindAll("rect.waterfall-bar");
        var axisLabels = cut.FindAll("text.axis-label").Select(node => node.TextContent.Trim()).ToList();

        Assert.Equal(5, bars.Count);
        Assert.Contains("150", axisLabels);
        Assert.Contains("Start", cut.FindAll("g.waterfall-group")[0].GetAttribute("aria-label"));
        Assert.Contains("Subtotal", cut.FindAll("g.waterfall-group")[3].GetAttribute("aria-label"));
    }

    [Fact]
    public void AppliesIncreaseDecreaseAndTotalColors()
    {
        var cut = RenderWaterfall(SampleData);
        var bars = cut.FindAll("rect.waterfall-bar");

        Assert.Contains("--waterfall-color: #4d89ff; --waterfall-hover-color: #245ec7", bars[0].GetAttribute("style"));
        Assert.Contains("--waterfall-color: #198f8c; --waterfall-hover-color: #11696c", bars[1].GetAttribute("style"));
        Assert.Contains("--waterfall-color: #d94f3d; --waterfall-hover-color: #a73728", bars[2].GetAttribute("style"));
        Assert.Contains("--waterfall-color: #4d89ff; --waterfall-hover-color: #245ec7", bars[4].GetAttribute("style"));
    }

    [Fact]
    public void RendersSubtotalAndTotalUsingComputedRunningTotals()
    {
        var cut = RenderWaterfall(SampleData);
        var bars = cut.FindAll("rect.waterfall-bar");

        var subtotalHeight = double.Parse(bars[3].GetAttribute("height")!, CultureInfo.InvariantCulture);
        var totalHeight = double.Parse(bars[4].GetAttribute("height")!, CultureInfo.InvariantCulture);
        var increaseHeight = double.Parse(bars[1].GetAttribute("height")!, CultureInfo.InvariantCulture);

        Assert.True(subtotalHeight > increaseHeight);
        Assert.True(totalHeight > increaseHeight);
    }

    [Fact]
    public void AxisScaleIncludesZeroAndNegativeChanges()
    {
        var cut = RenderWaterfall(new[]
        {
            new WaterfallDatum("Start", 40, WaterfallStepType.Start),
            new WaterfallDatum("Loss", -90, WaterfallStepType.Change),
            new WaterfallDatum("End", 0, WaterfallStepType.Total)
        });

        var zeroLine = cut.Find("line.zero-line");
        var y = double.Parse(zeroLine.GetAttribute("y1")!, CultureInfo.InvariantCulture);

        Assert.True(y > 0);
        Assert.Contains("0", cut.FindAll("text.axis-label").Select(x => x.TextContent));
    }

    [Fact]
    public void RendersConnectorsAndCanDisableThem()
    {
        var cut = RenderWaterfall(SampleData);
        Assert.Equal(4, cut.FindAll("line.waterfall-connector").Count);

        var noConnectors = RenderComponent<FireWaterfallChart<WaterfallDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.ValueSelector, item => item.Value)
            .Add(component => component.StepTypeSelector, item => item.StepType)
            .Add(component => component.ShowConnectors, false));

        Assert.Empty(noConnectors.FindAll("line.waterfall-connector"));
    }

    [Fact]
    public void HoverFocusKeyboardAndClickCallbacksReturnPointDetails()
    {
        WaterfallChartPointInteraction<WaterfallDatum>? hovered = null;
        WaterfallChartPointInteraction<WaterfallDatum>? clicked = null;
        WaterfallDatum? selected = null;

        var cut = RenderComponent<FireWaterfallChart<WaterfallDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.ValueSelector, item => item.Value)
            .Add(component => component.StepTypeSelector, item => item.StepType)
            .Add(component => component.OnPointHoverChanged, (Action<WaterfallChartPointInteraction<WaterfallDatum>>)(interaction => hovered = interaction))
            .Add(component => component.OnPointClick, (Action<WaterfallChartPointInteraction<WaterfallDatum>>)(interaction => clicked = interaction))
            .Add(component => component.SelectedItemChanged, (Action<WaterfallDatum?>)(item => selected = item)));

        cut.FindAll("g.waterfall-group")[1].MouseOver();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(hovered);
            Assert.Equal("Up", hovered!.Label);
            Assert.Single(cut.FindAll(".chart-tooltip"));
        });

        cut.FindAll("g.waterfall-group")[1].Focus();
        cut.FindAll("g.waterfall-group")[1].MouseOut();

        cut.WaitForAssertion(() =>
        {
            var group = cut.FindAll("g.waterfall-group")[1];
            Assert.Contains("is-hovered", group.GetAttribute("class"));
            Assert.Contains("is-focused", group.GetAttribute("class"));
        });

        cut.FindAll("g.waterfall-group")[1].Blur();
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".chart-tooltip"));
        });

        cut.FindAll("g.waterfall-group")[2].KeyDown(new KeyboardEventArgs { Key = "Enter" });

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(selected);
            Assert.Equal("Down", selected!.Label);
            Assert.NotNull(clicked);
            Assert.Equal(WaterfallStepType.Change, clicked!.StepType);
            Assert.Contains("is-selected", cut.FindAll("g.waterfall-group")[2].GetAttribute("class"));
        });
    }

    [Fact]
    public void AppliesCustomColorsAndTooltipTemplate()
    {
        var data = new[]
        {
            new WaterfallDatum("Start", 100, WaterfallStepType.Start, "#111111", "#222222"),
            new WaterfallDatum("Change", 30, WaterfallStepType.Change, "#333333", "#444444"),
            new WaterfallDatum("Total", 0, WaterfallStepType.Total, "#555555", "#666666")
        };

        var cut = RenderComponent<FireWaterfallChart<WaterfallDatum>>(parameters => parameters
            .Add(component => component.Items, data)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.ValueSelector, item => item.Value)
            .Add(component => component.StepTypeSelector, item => item.StepType)
            .Add(component => component.ColorSelector, item => item.Fill!)
            .Add(component => component.HoverColorSelector, item => item.HoverFill!)
            .Add(component => component.TooltipTemplate, (RenderFragment<WaterfallChartPoint<WaterfallDatum>>)(point => builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "custom-waterfall-tooltip");
                builder.AddContent(2, $"tooltip-{point.Label}");
                builder.CloseElement();
            })));

        cut.FindAll("g.waterfall-group")[1].MouseOver();

        cut.WaitForAssertion(() =>
        {
            var rect = cut.FindAll("rect.waterfall-bar")[1];
            Assert.Contains("--waterfall-color: #333333; --waterfall-hover-color: #444444", rect.GetAttribute("style"));
            Assert.Contains("tooltip-Change", cut.Markup);
        });
    }

    [Fact]
    public async Task ResponsiveResizeUpdatesBarGeometry()
    {
        var runtime = CreateRuntime();
        Services.AddSingleton<IJSRuntime>(runtime);

        var cut = RenderComponent<FireWaterfallChart<WaterfallDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.ValueSelector, item => item.Value)
            .Add(component => component.StepTypeSelector, item => item.StepType)
            .Add(component => component.Responsive, true)
            .Add(component => component.Width, 600));

        var originalWidth = double.Parse(cut.Find("svg").GetAttribute("width")!, CultureInfo.InvariantCulture);
        var originalBarWidth = double.Parse(cut.FindAll("rect.waterfall-bar")[0].GetAttribute("width")!, CultureInfo.InvariantCulture);

        await cut.FindComponent<ChartSurface>().Instance.OnContainerWidthChanged(760);

        cut.WaitForAssertion(() =>
        {
            var updatedWidth = double.Parse(cut.Find("svg").GetAttribute("width")!, CultureInfo.InvariantCulture);
            var updatedBarWidth = double.Parse(cut.FindAll("rect.waterfall-bar")[0].GetAttribute("width")!, CultureInfo.InvariantCulture);

            Assert.True(updatedWidth > originalWidth);
            Assert.True(updatedBarWidth > originalBarWidth);
        });
    }

    private IRenderedComponent<FireWaterfallChart<WaterfallDatum>> RenderWaterfall(IReadOnlyList<WaterfallDatum> data) =>
        RenderComponent<FireWaterfallChart<WaterfallDatum>>(parameters => parameters
            .Add(component => component.Items, data)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.ValueSelector, item => item.Value)
            .Add(component => component.StepTypeSelector, item => item.StepType));

    private static RecordingJsRuntime CreateRuntime()
    {
        var observer = new RecordingJsObjectReference();
        observer.SetupHandler("dispose", _ => null!);

        var module = new RecordingJsObjectReference();
        module.SetupResult("observeElementSize", observer);

        return new RecordingJsRuntime(module);
    }

    private static readonly WaterfallDatum[] SampleData =
    [
        new("Start", 100, WaterfallStepType.Start),
        new("Up", 30, WaterfallStepType.Change),
        new("Down", -20, WaterfallStepType.Change),
        new("Subtotal", 999, WaterfallStepType.Subtotal),
        new("Total", 999, WaterfallStepType.Total)
    ];

    private sealed record WaterfallDatum(
        string Label,
        double Value,
        WaterfallStepType StepType,
        string? Fill = null,
        string? HoverFill = null);
}
