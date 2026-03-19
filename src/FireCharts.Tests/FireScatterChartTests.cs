using System.Globalization;
using FireCharts.Components;
using FireCharts.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace FireCharts.Tests;

public sealed class FireScatterChartTests : TestContext
{
    [Fact]
    public void RendersEmptyStateWhenAllPointsAreInvalid()
    {
        var points = new[]
        {
            new NumericPoint(double.NaN, 12, "Bad X"),
            new NumericPoint(2, double.PositiveInfinity, "Bad Y")
        };

        var cut = RenderComponent<FireScatterChart<NumericPoint>>(parameters => parameters
            .Add(component => component.Items, points)
            .Add(component => component.XValueSelector, point => LineChartXValue.FromNumber(point.X))
            .Add(component => component.YValueSelector, point => point.Y));

        Assert.Contains("No data", cut.Markup);
    }

    [Fact]
    public void SortsNumericPointsByXAxis()
    {
        var points = new[]
        {
            new NumericPoint(30, 12, "Thirty"),
            new NumericPoint(10, 8, "Ten"),
            new NumericPoint(20, 10, "Twenty")
        };

        var cut = RenderNumericChart(points);
        var xPositions = GetCirclePositions(cut, "cx");

        Assert.Equal(3, xPositions.Count);
        Assert.True(xPositions[0] < xPositions[1]);
        Assert.True(xPositions[1] < xPositions[2]);
    }

    [Fact]
    public void SortsDateTimePointsByXAxis()
    {
        var points = new[]
        {
            new DatePoint(new DateTime(2025, 2, 10), 18, "Second"),
            new DatePoint(new DateTime(2025, 2, 3), 14, "First"),
            new DatePoint(new DateTime(2025, 2, 17), 21, "Third")
        };

        var cut = RenderDateChart(points);
        var xPositions = GetCirclePositions(cut, "cx");

        Assert.Equal(3, xPositions.Count);
        Assert.True(xPositions[0] < xPositions[1]);
        Assert.True(xPositions[1] < xPositions[2]);
    }

    [Fact]
    public void RendersMultipleSeriesAndLegend()
    {
        var series = new[]
        {
            new ScatterChartSeries<NumericPoint>("Permits", new[]
            {
                new NumericPoint(1, 4, "One"),
                new NumericPoint(2, 6, "Two")
            }, "#d94f3d"),
            new ScatterChartSeries<NumericPoint>("Inspections", new[]
            {
                new NumericPoint(1, 5, "One"),
                new NumericPoint(2, 7, "Two")
            }, "#198f8c")
        };

        var cut = RenderComponent<FireScatterChart<NumericPoint>>(parameters => parameters
            .Add(component => component.Series, series)
            .Add(component => component.XValueSelector, point => LineChartXValue.FromNumber(point.X))
            .Add(component => component.YValueSelector, point => point.Y)
            .Add(component => component.LabelSelector, point => point.Label));

        Assert.Equal(4, cut.FindAll("circle.scatter-point").Count);
        Assert.Equal(2, cut.FindAll(".scatter-legend__item").Count);
    }

    [Fact]
    public void LegendHoverAndFocusMuteInactiveSeries()
    {
        var cut = RenderChartWithTwoSeries();
        cut.FindAll(".scatter-legend__item")[0].MouseOver();

        cut.WaitForAssertion(() =>
        {
            var legendItems = cut.FindAll(".scatter-legend__item");
            var seriesGroups = cut.FindAll("g.scatter-series-group");
            Assert.Contains("is-active", legendItems[0].GetAttribute("class"));
            Assert.Contains("is-muted", legendItems[1].GetAttribute("class"));
            Assert.Contains("is-active", seriesGroups[0].GetAttribute("class"));
            Assert.Contains("is-muted", seriesGroups[1].GetAttribute("class"));
        });

        cut.FindAll(".scatter-legend__item")[1].Focus();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("is-active", cut.FindAll(".scatter-legend__item")[1].GetAttribute("class"));
            Assert.Contains("is-muted", cut.FindAll(".scatter-legend__item")[0].GetAttribute("class"));
        });
    }

    [Fact]
    public void HoverFocusKeyboardAndClickCallbacksReturnPointDetails()
    {
        var points = new[]
        {
            new NumericPoint(1, 5, "One"),
            new NumericPoint(2, 7, "Two")
        };

        ScatterChartPointInteraction<NumericPoint>? hovered = null;
        ScatterChartPoint<NumericPoint>? selected = null;
        ScatterChartPointInteraction<NumericPoint>? clicked = null;

        var cut = RenderComponent<FireScatterChart<NumericPoint>>(parameters => parameters
            .Add(component => component.Items, points)
            .Add(component => component.XValueSelector, point => LineChartXValue.FromNumber(point.X))
            .Add(component => component.YValueSelector, point => point.Y)
            .Add(component => component.LabelSelector, point => point.Label)
            .Add(component => component.OnPointHoverChanged, (Action<ScatterChartPointInteraction<NumericPoint>>)(interaction => hovered = interaction))
            .Add(component => component.OnPointClick, (Action<ScatterChartPointInteraction<NumericPoint>>)(interaction => clicked = interaction))
            .Add(component => component.SelectedPointChanged, (Action<ScatterChartPoint<NumericPoint>?>)(point => selected = point)));

        cut.FindAll("g.scatter-point-group")[0].MouseOver();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(hovered);
            Assert.Equal("One", hovered!.Label);
            Assert.Single(cut.FindAll(".chart-tooltip"));
        });

        cut.FindAll("g.scatter-point-group")[0].Focus();
        cut.FindAll("g.scatter-point-group")[0].MouseOut();
        cut.WaitForAssertion(() =>
        {
            var firstGroup = cut.FindAll("g.scatter-point-group")[0];
            Assert.Contains("is-hovered", firstGroup.GetAttribute("class"));
            Assert.Contains("is-focused", firstGroup.GetAttribute("class"));
            Assert.Single(cut.FindAll(".chart-tooltip"));
        });

        cut.FindAll("g.scatter-point-group")[0].Blur();
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".chart-tooltip"));
            Assert.DoesNotContain("is-focused", cut.FindAll("g.scatter-point-group")[0].GetAttribute("class"));
        });

        cut.FindAll("g.scatter-point-group")[1].KeyDown(new KeyboardEventArgs { Key = "Enter" });
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(selected);
            Assert.Equal("Two", selected!.Label);
            Assert.NotNull(clicked);
            Assert.Equal(7, clicked!.Y);
            Assert.Contains("is-selected", cut.FindAll("g.scatter-point-group")[1].GetAttribute("class"));
        });
    }

    [Fact]
    public void AppliesCustomColorsAndTooltipTemplate()
    {
        var data = new[]
        {
            new ScatterDatum("Engine 1", 12, 5, "#111111", "#333333"),
            new ScatterDatum("Engine 2", 18, 7, "#222222", "#444444")
        };

        var cut = RenderComponent<FireScatterChart<ScatterDatum>>(parameters => parameters
            .Add(component => component.Items, data)
            .Add(component => component.XValueSelector, item => LineChartXValue.FromNumber(item.X))
            .Add(component => component.YValueSelector, item => item.Y)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.ColorSelector, item => item.Fill)
            .Add(component => component.HoverColorSelector, item => item.HoverFill)
            .Add(component => component.TooltipTemplate, (RenderFragment<ScatterChartPoint<ScatterDatum>>)(point => builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "custom-scatter-tooltip");
                builder.AddContent(2, $"tooltip-{point.Label}");
                builder.CloseElement();
            })));

        cut.FindAll("g.scatter-point-group")[0].MouseOver();

        cut.WaitForAssertion(() =>
        {
            var point = cut.Find("circle.scatter-point");
            Assert.Contains("--point-color: #111111; --point-hover-color: #333333", point.GetAttribute("style"));
            Assert.Contains("tooltip-Engine 1", cut.Markup);
        });
    }

    [Fact]
    public async Task ResponsiveResizeUpdatesPointGeometry()
    {
        var runtime = CreateRuntime();
        Services.AddSingleton<IJSRuntime>(runtime);

        var cut = RenderComponent<FireScatterChart<NumericPoint>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.XValueSelector, point => LineChartXValue.FromNumber(point.X))
            .Add(component => component.YValueSelector, point => point.Y)
            .Add(component => component.LabelSelector, point => point.Label)
            .Add(component => component.Responsive, true)
            .Add(component => component.Width, 600));

        var originalWidth = double.Parse(cut.Find("svg").GetAttribute("width")!, CultureInfo.InvariantCulture);
        var originalX = double.Parse(cut.FindAll("circle.scatter-point")[1].GetAttribute("cx")!, CultureInfo.InvariantCulture);

        await cut.FindComponent<ChartSurface>().Instance.OnContainerWidthChanged(760);

        cut.WaitForAssertion(() =>
        {
            var updatedWidth = double.Parse(cut.Find("svg").GetAttribute("width")!, CultureInfo.InvariantCulture);
            var updatedX = double.Parse(cut.FindAll("circle.scatter-point")[1].GetAttribute("cx")!, CultureInfo.InvariantCulture);

            Assert.True(updatedWidth > originalWidth);
            Assert.True(updatedX > originalX);
        });
    }

    [Fact]
    public void ThrowsWhenMixingNumericAndDateTimeXValues()
    {
        var items = new object[]
        {
            new NumericPoint(1, 5, "Numeric"),
            new DatePoint(new DateTime(2025, 2, 3), 7, "Date")
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RenderComponent<FireScatterChart<object>>(parameters => parameters
                .Add(component => component.Items, items)
                .Add(component => component.XValueSelector, item => item switch
                {
                    NumericPoint numeric => LineChartXValue.FromNumber(numeric.X),
                    DatePoint date => LineChartXValue.FromDateTime(date.X),
                    _ => throw new InvalidOperationException()
                })
                .Add(component => component.YValueSelector, item => item switch
                {
                    NumericPoint numeric => numeric.Y,
                    DatePoint date => date.Y,
                    _ => 0
                })));

        Assert.Contains("cannot mix numeric and DateTime X values", exception.Message);
    }

    private IRenderedComponent<FireScatterChart<NumericPoint>> RenderNumericChart(IReadOnlyList<NumericPoint> points) =>
        RenderComponent<FireScatterChart<NumericPoint>>(parameters => parameters
            .Add(component => component.Items, points)
            .Add(component => component.XValueSelector, point => LineChartXValue.FromNumber(point.X))
            .Add(component => component.YValueSelector, point => point.Y)
            .Add(component => component.LabelSelector, point => point.Label));

    private IRenderedComponent<FireScatterChart<DatePoint>> RenderDateChart(IReadOnlyList<DatePoint> points) =>
        RenderComponent<FireScatterChart<DatePoint>>(parameters => parameters
            .Add(component => component.Items, points)
            .Add(component => component.XValueSelector, point => LineChartXValue.FromDateTime(point.X))
            .Add(component => component.YValueSelector, point => point.Y)
            .Add(component => component.LabelSelector, point => point.Label));

    private IRenderedComponent<FireScatterChart<NumericPoint>> RenderChartWithTwoSeries()
    {
        var series = new[]
        {
            new ScatterChartSeries<NumericPoint>("Permits", new[]
            {
                new NumericPoint(1, 4, "One"),
                new NumericPoint(2, 6, "Two")
            }, "#d94f3d"),
            new ScatterChartSeries<NumericPoint>("Inspections", new[]
            {
                new NumericPoint(1, 5, "One"),
                new NumericPoint(2, 7, "Two")
            }, "#198f8c")
        };

        return RenderComponent<FireScatterChart<NumericPoint>>(parameters => parameters
            .Add(component => component.Series, series)
            .Add(component => component.XValueSelector, point => LineChartXValue.FromNumber(point.X))
            .Add(component => component.YValueSelector, point => point.Y)
            .Add(component => component.LabelSelector, point => point.Label));
    }

    private static List<double> GetCirclePositions(IRenderedFragment cut, string attribute) =>
        cut.FindAll("circle.scatter-point")
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

    private static readonly NumericPoint[] SampleData =
    [
        new(12, 5, "One"),
        new(18, 7, "Two"),
        new(25, 9, "Three")
    ];

    private sealed record NumericPoint(double X, double Y, string Label);
    private sealed record DatePoint(DateTime X, double Y, string Label);
    private sealed record ScatterDatum(string Label, double X, double Y, string Fill, string HoverFill);
}
