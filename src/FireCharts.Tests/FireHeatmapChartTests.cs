using System.Globalization;
using Bunit;
using FireCharts.Components;
using FireCharts.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace FireCharts.Tests;

public sealed class FireHeatmapChartTests : TestContext
{
    [Fact]
    public void RendersEmptyStateWhenAllValuesAreInvalid()
    {
        var items = new[]
        {
            new HeatDatum("mon", "Mon", 0, "06", "6a", 6, double.NaN),
            new HeatDatum("tue", "Tue", 1, "07", "7a", 7, double.PositiveInfinity)
        };

        var cut = RenderHeatmap(items);

        Assert.Contains("No data", cut.Markup);
        Assert.Empty(cut.FindAll("g.heatmap-cell-group"));
    }

    [Fact]
    public void OrdersRowsAndColumnsUsingOrderSelectors()
    {
        var items = new[]
        {
            new HeatDatum("wed", "Wed", 2, "09", "9a", 9, 15),
            new HeatDatum("mon", "Mon", 0, "18", "6p", 18, 28),
            new HeatDatum("mon", "Mon", 0, "09", "9a", 9, 18),
            new HeatDatum("wed", "Wed", 2, "18", "6p", 18, 22)
        };

        var cut = RenderHeatmap(items);
        var rowLabels = cut.FindAll("text.heatmap-row-label").Select(label => label.TextContent.Trim()).ToList();
        var columnLabels = cut.FindAll("text.heatmap-column-label").Select(label => label.TextContent.Trim()).ToList();

        Assert.Equal(["Mon", "Wed"], rowLabels);
        Assert.Equal(["9a", "6p"], columnLabels);
    }

    [Fact]
    public void RendersPlaceholderCellsForMissingCombinations()
    {
        var items = new[]
        {
            new HeatDatum("mon", "Mon", 0, "06", "6a", 6, 12),
            new HeatDatum("mon", "Mon", 0, "07", "7a", 7, 18),
            new HeatDatum("tue", "Tue", 1, "06", "6a", 6, 14)
        };

        var cut = RenderHeatmap(items);

        Assert.Equal(3, cut.FindAll("g.heatmap-cell-group").Count);
        Assert.Single(cut.FindAll("rect.heatmap-cell--placeholder"));
    }

    [Fact]
    public void HoverFocusKeyboardAndClickCallbacksReturnCellDetails()
    {
        var items = new[]
        {
            new HeatDatum("mon", "Mon", 0, "06", "6a", 6, 12),
            new HeatDatum("tue", "Tue", 1, "07", "7a", 7, 20)
        };

        HeatmapCellInteraction<HeatDatum>? hovered = null;
        HeatDatum? selected = null;
        HeatmapCellInteraction<HeatDatum>? clicked = null;

        var cut = RenderComponent<FireHeatmapChart<HeatDatum>>(parameters => parameters
            .Add(component => component.Items, items)
            .Add(component => component.RowKeySelector, item => item.RowKey)
            .Add(component => component.RowLabelSelector, item => item.RowLabel)
            .Add(component => component.RowOrderSelector, item => item.RowOrder)
            .Add(component => component.ColumnKeySelector, item => item.ColumnKey)
            .Add(component => component.ColumnLabelSelector, item => item.ColumnLabel)
            .Add(component => component.ColumnOrderSelector, item => item.ColumnOrder)
            .Add(component => component.ValueSelector, item => item.Value)
            .Add(component => component.OnCellHoverChanged, (Action<HeatmapCellInteraction<HeatDatum>>)(interaction => hovered = interaction))
            .Add(component => component.OnCellClick, (Action<HeatmapCellInteraction<HeatDatum>>)(interaction => clicked = interaction))
            .Add(component => component.SelectedItemChanged, (Action<HeatDatum?>)(item => selected = item)));

        cut.FindAll("g.heatmap-cell-group")[0].MouseOver();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(hovered);
            Assert.Equal("Mon", hovered!.RowLabel);
            Assert.Single(cut.FindAll(".chart-tooltip"));
        });

        cut.FindAll("g.heatmap-cell-group")[0].Focus();
        cut.FindAll("g.heatmap-cell-group")[0].MouseOut();

        cut.WaitForAssertion(() =>
        {
            var firstGroup = cut.FindAll("g.heatmap-cell-group")[0];
            Assert.Contains("is-hovered", firstGroup.GetAttribute("class"));
            Assert.Contains("is-focused", firstGroup.GetAttribute("class"));
            Assert.Single(cut.FindAll(".chart-tooltip"));
        });

        cut.FindAll("g.heatmap-cell-group")[0].Blur();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".chart-tooltip"));
            Assert.DoesNotContain("is-focused", cut.FindAll("g.heatmap-cell-group")[0].GetAttribute("class"));
        });

        cut.FindAll("g.heatmap-cell-group")[1].KeyDown(new KeyboardEventArgs { Key = "Enter" });

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(selected);
            Assert.Equal("Tue", selected!.RowLabel);
            Assert.NotNull(clicked);
            Assert.Equal("7a", clicked!.ColumnLabel);
            Assert.Contains("is-selected", cut.FindAll("g.heatmap-cell-group")[1].GetAttribute("class"));
        });
    }

    [Fact]
    public void AppliesInterpolatedAndCustomColorsAndTooltipTemplate()
    {
        var items = new[]
        {
            new HeatDatum("mon", "Mon", 0, "06", "6a", 6, 0),
            new HeatDatum("tue", "Tue", 1, "06", "6a", 6, 10, "#123456")
        };

        var cut = RenderComponent<FireHeatmapChart<HeatDatum>>(parameters => parameters
            .Add(component => component.Items, items)
            .Add(component => component.RowKeySelector, item => item.RowKey)
            .Add(component => component.RowLabelSelector, item => item.RowLabel)
            .Add(component => component.RowOrderSelector, item => item.RowOrder)
            .Add(component => component.ColumnKeySelector, item => item.ColumnKey)
            .Add(component => component.ColumnLabelSelector, item => item.ColumnLabel)
            .Add(component => component.ColumnOrderSelector, item => item.ColumnOrder)
            .Add(component => component.ValueSelector, item => item.Value)
            .Add(component => component.LowColor, "#eeeeee")
            .Add(component => component.HighColor, "#ff0000")
            .Add(component => component.ColorSelector, item => item.Fill)
            .Add(component => component.TooltipTemplate, (RenderFragment<HeatmapCell<HeatDatum>>)(cell => builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "custom-heat-tooltip");
                builder.AddContent(2, $"tooltip-{cell.RowLabel}-{cell.ColumnLabel}");
                builder.CloseElement();
            })));

        var rects = cut.FindAll("g.heatmap-cell-group rect.heatmap-cell");
        Assert.Equal("#EEEEEE", rects[0].GetAttribute("fill"));
        Assert.Equal("#123456", rects[1].GetAttribute("fill"));

        cut.FindAll("g.heatmap-cell-group")[1].MouseOver();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("tooltip-Tue-6a", cut.Markup);
        });
    }

    [Fact]
    public void ClampsNegativeValuesToZeroAndRendersLegend()
    {
        var items = new[]
        {
            new HeatDatum("mon", "Mon", 0, "06", "6a", 6, -4),
            new HeatDatum("tue", "Tue", 1, "06", "6a", 6, 12)
        };

        var cut = RenderComponent<FireHeatmapChart<HeatDatum>>(parameters => parameters
            .Add(component => component.Items, items)
            .Add(component => component.RowKeySelector, item => item.RowKey)
            .Add(component => component.RowLabelSelector, item => item.RowLabel)
            .Add(component => component.RowOrderSelector, item => item.RowOrder)
            .Add(component => component.ColumnKeySelector, item => item.ColumnKey)
            .Add(component => component.ColumnLabelSelector, item => item.ColumnLabel)
            .Add(component => component.ColumnOrderSelector, item => item.ColumnOrder)
            .Add(component => component.ValueSelector, item => item.Value)
            .Add(component => component.LowColor, "#eeeeee")
            .Add(component => component.HighColor, "#ff0000")
            .Add(component => component.ValueFormat, "F0"));

        var rects = cut.FindAll("g.heatmap-cell-group rect.heatmap-cell");

        Assert.Equal("#EEEEEE", rects[0].GetAttribute("fill"));
        Assert.Equal("0", cut.Find(".heatmap-legend__value--min").TextContent.Trim());
        Assert.Equal("12", cut.Find(".heatmap-legend__value--max").TextContent.Trim());
    }

    [Fact]
    public async Task ResponsiveResizeUpdatesCellGeometry()
    {
        var runtime = CreateRuntime();
        Services.AddSingleton<IJSRuntime>(runtime);

        var cut = RenderComponent<FireHeatmapChart<HeatDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.RowKeySelector, item => item.RowKey)
            .Add(component => component.RowLabelSelector, item => item.RowLabel)
            .Add(component => component.RowOrderSelector, item => item.RowOrder)
            .Add(component => component.ColumnKeySelector, item => item.ColumnKey)
            .Add(component => component.ColumnLabelSelector, item => item.ColumnLabel)
            .Add(component => component.ColumnOrderSelector, item => item.ColumnOrder)
            .Add(component => component.ValueSelector, item => item.Value)
            .Add(component => component.Responsive, true)
            .Add(component => component.Width, 600));

        var originalWidth = double.Parse(cut.Find("svg").GetAttribute("width")!, CultureInfo.InvariantCulture);
        var originalX = double.Parse(cut.FindAll("g.heatmap-cell-group rect.heatmap-cell")[1].GetAttribute("x")!, CultureInfo.InvariantCulture);

        await cut.FindComponent<ChartSurface>().Instance.OnContainerWidthChanged(760);

        cut.WaitForAssertion(() =>
        {
            var updatedWidth = double.Parse(cut.Find("svg").GetAttribute("width")!, CultureInfo.InvariantCulture);
            var updatedX = double.Parse(cut.FindAll("g.heatmap-cell-group rect.heatmap-cell")[1].GetAttribute("x")!, CultureInfo.InvariantCulture);

            Assert.True(updatedWidth > originalWidth);
            Assert.True(updatedX > originalX);
        });
    }

    private IRenderedComponent<FireHeatmapChart<HeatDatum>> RenderHeatmap(IReadOnlyList<HeatDatum> items) =>
        RenderComponent<FireHeatmapChart<HeatDatum>>(parameters => parameters
            .Add(component => component.Items, items)
            .Add(component => component.RowKeySelector, item => item.RowKey)
            .Add(component => component.RowLabelSelector, item => item.RowLabel)
            .Add(component => component.RowOrderSelector, item => item.RowOrder)
            .Add(component => component.ColumnKeySelector, item => item.ColumnKey)
            .Add(component => component.ColumnLabelSelector, item => item.ColumnLabel)
            .Add(component => component.ColumnOrderSelector, item => item.ColumnOrder)
            .Add(component => component.ValueSelector, item => item.Value));

    private static RecordingJsRuntime CreateRuntime()
    {
        var observer = new RecordingJsObjectReference();
        observer.SetupHandler("dispose", _ => null!);

        var module = new RecordingJsObjectReference();
        module.SetupResult("observeElementSize", observer);

        return new RecordingJsRuntime(module);
    }

    private static readonly HeatDatum[] SampleData =
    [
        new("mon", "Mon", 0, "06", "6a", 6, 8),
        new("mon", "Mon", 0, "07", "7a", 7, 12),
        new("tue", "Tue", 1, "06", "6a", 6, 14),
        new("tue", "Tue", 1, "07", "7a", 7, 16)
    ];

    private sealed record HeatDatum(
        string RowKey,
        string RowLabel,
        int RowOrder,
        string ColumnKey,
        string ColumnLabel,
        int ColumnOrder,
        double Value,
        string? Fill = null);
}
