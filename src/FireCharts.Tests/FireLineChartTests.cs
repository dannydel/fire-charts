using System.Globalization;
using AngleSharp.Dom;
using FireCharts.Components;
using FireCharts.Models;
using Microsoft.AspNetCore.Components;

namespace FireCharts.Tests;

public sealed class FireLineChartTests : TestContext
{
    [Fact]
    public void RendersEmptyStateWhenAllPointsAreInvalid()
    {
        var points = new[]
        {
            new NumericPoint(double.NaN, 12, "Bad X"),
            new NumericPoint(2, double.PositiveInfinity, "Bad Y")
        };

        var cut = RenderComponent<FireLineChart<NumericPoint>>(parameters => parameters
            .Add(component => component.Items, points)
            .Add(component => component.XValueSelector, point => LineChartXValue.FromNumber(point.X))
            .Add(component => component.YValueSelector, point => point.Y)
            .Add(component => component.PointDisplayMode, LinePointDisplayMode.All));

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

        var cut = RenderNumericChart(points, LinePointDisplayMode.All);
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

        var cut = RenderComponent<FireLineChart<DatePoint>>(parameters => parameters
            .Add(component => component.Items, points)
            .Add(component => component.XValueSelector, point => LineChartXValue.FromDateTime(point.X))
            .Add(component => component.YValueSelector, point => point.Y)
            .Add(component => component.LabelSelector, point => point.Label)
            .Add(component => component.PointDisplayMode, LinePointDisplayMode.All));

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
            new LineChartSeries<NumericPoint>("Permits", new[]
            {
                new NumericPoint(1, 4, "One"),
                new NumericPoint(2, 6, "Two")
            }, "#d94f3d"),
            new LineChartSeries<NumericPoint>("Inspections", new[]
            {
                new NumericPoint(1, 5, "One"),
                new NumericPoint(2, 7, "Two")
            }, "#198f8c")
        };

        var cut = RenderComponent<FireLineChart<NumericPoint>>(parameters => parameters
            .Add(component => component.Series, series)
            .Add(component => component.XValueSelector, point => LineChartXValue.FromNumber(point.X))
            .Add(component => component.YValueSelector, point => point.Y)
            .Add(component => component.LabelSelector, point => point.Label)
            .Add(component => component.PointDisplayMode, LinePointDisplayMode.All));

        Assert.Equal(2, cut.FindAll("path.line-path").Count);
        Assert.Equal(2, cut.FindAll(".line-legend__item").Count);
    }

    [Fact]
    public void UsesHighlightedOnlyMarkersByDefaultAndCanRenderAllPoints()
    {
        var points = new[]
        {
            new NumericPoint(1, 5, "One"),
            new NumericPoint(2, 7, "Two", true),
            new NumericPoint(3, 6, "Three")
        };

        var highlightedOnly = RenderNumericChart(points, LinePointDisplayMode.HighlightedOnly);
        var allPoints = RenderNumericChart(points, LinePointDisplayMode.All);

        Assert.Single(highlightedOnly.FindAll("circle.line-point"));
        Assert.Equal(3, allPoints.FindAll("circle.line-point").Count);
    }

    [Fact]
    public void HoverAndClickCallbacksReturnPointDetails()
    {
        var points = new[]
        {
            new NumericPoint(1, 5, "One", true),
            new NumericPoint(2, 7, "Two", true)
        };

        LineChartPointInteraction<NumericPoint>? hovered = null;
        LineChartPoint<NumericPoint>? selected = null;
        LineChartPointInteraction<NumericPoint>? clicked = null;

        var cut = RenderComponent<FireLineChart<NumericPoint>>(parameters => parameters
            .Add(component => component.Items, points)
            .Add(component => component.XValueSelector, point => LineChartXValue.FromNumber(point.X))
            .Add(component => component.YValueSelector, point => point.Y)
            .Add(component => component.LabelSelector, point => point.Label)
            .Add(component => component.HighlightSelector, point => point.Highlight)
            .Add(component => component.OnPointHoverChanged, (Action<LineChartPointInteraction<NumericPoint>>)(interaction => hovered = interaction))
            .Add(component => component.OnPointClick, (Action<LineChartPointInteraction<NumericPoint>>)(interaction => clicked = interaction))
            .Add(component => component.SelectedPointChanged, (Action<LineChartPoint<NumericPoint>?>)(point => selected = point)));

        var pointGroups = cut.FindAll("g.line-point-group");
        pointGroups[0].MouseOver();
        cut.FindAll("g.line-point-group")[1].Click();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(hovered);
            Assert.Equal("One", hovered!.Label);
            Assert.NotNull(selected);
            Assert.Equal("Two", selected!.Label);
            Assert.NotNull(clicked);
            Assert.Equal(7, clicked!.Y);
        });
    }

    [Fact]
    public void BuildsDistinctPathsForEachInterpolationMode()
    {
        var points = new[]
        {
            new NumericPoint(1, 2, "One", true),
            new NumericPoint(2, 8, "Two", true),
            new NumericPoint(3, 4, "Three", true),
            new NumericPoint(4, 9, "Four", true)
        };

        var linear = RenderNumericChart(points, LinePointDisplayMode.All, LineInterpolationMode.Linear)
            .Find("path.line-path").GetAttribute("d");
        var smooth = RenderNumericChart(points, LinePointDisplayMode.All, LineInterpolationMode.Smooth)
            .Find("path.line-path").GetAttribute("d");
        var step = RenderNumericChart(points, LinePointDisplayMode.All, LineInterpolationMode.Step)
            .Find("path.line-path").GetAttribute("d");

        Assert.Contains(" L ", linear);
        Assert.DoesNotContain(" C ", linear);
        Assert.Contains(" C ", smooth);
        Assert.DoesNotContain(" C ", step);
        Assert.True(step!.Split(" L ", StringSplitOptions.RemoveEmptyEntries).Length > linear!.Split(" L ", StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void AreaVariantRendersFillPathAndKeepsPointInteraction()
    {
        var points = new[]
        {
            new NumericPoint(1, 3, "One", true),
            new NumericPoint(2, 6, "Two", true),
            new NumericPoint(3, 5, "Three", true)
        };

        LineChartPointInteraction<NumericPoint>? clicked = null;
        var cut = RenderComponent<FireLineChart<NumericPoint>>(parameters => parameters
            .Add(component => component.Items, points)
            .Add(component => component.XValueSelector, point => LineChartXValue.FromNumber(point.X))
            .Add(component => component.YValueSelector, point => point.Y)
            .Add(component => component.LabelSelector, point => point.Label)
            .Add(component => component.HighlightSelector, point => point.Highlight)
            .Add(component => component.PointDisplayMode, LinePointDisplayMode.All)
            .Add(component => component.Variant, LineChartVariant.Area)
            .Add(component => component.OnPointClick, (Action<LineChartPointInteraction<NumericPoint>>)(interaction => clicked = interaction)));

        Assert.Single(cut.FindAll("path.line-area"));

        cut.FindAll("g.line-point-group")[0].Click();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(clicked);
            Assert.Equal("One", clicked!.Label);
        });
    }

    [Fact]
    public void ShowsTooltipForHoveredHighlightedPoint()
    {
        var points = new[]
        {
            new NumericPoint(1, 5, "One", true),
            new NumericPoint(2, 7, "Two", false)
        };

        var cut = RenderComponent<FireLineChart<NumericPoint>>(parameters => parameters
            .Add(component => component.Items, points)
            .Add(component => component.XValueSelector, point => LineChartXValue.FromNumber(point.X))
            .Add(component => component.YValueSelector, point => point.Y)
            .Add(component => component.LabelSelector, point => point.Label)
            .Add(component => component.HighlightSelector, point => point.Highlight));

        cut.Find("g.line-point-group").MouseOver();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("One", cut.Find(".chart-tooltip").TextContent);
        });
    }

    private IRenderedComponent<FireLineChart<NumericPoint>> RenderNumericChart(
        IReadOnlyList<NumericPoint> points,
        LinePointDisplayMode pointDisplayMode,
        LineInterpolationMode interpolation = LineInterpolationMode.Smooth) =>
        RenderComponent<FireLineChart<NumericPoint>>(parameters => parameters
            .Add(component => component.Items, points)
            .Add(component => component.XValueSelector, point => LineChartXValue.FromNumber(point.X))
            .Add(component => component.YValueSelector, point => point.Y)
            .Add(component => component.LabelSelector, point => point.Label)
            .Add(component => component.HighlightSelector, point => point.Highlight)
            .Add(component => component.PointDisplayMode, pointDisplayMode)
            .Add(component => component.Interpolation, interpolation));

    private static List<double> GetCirclePositions(IRenderedFragment cut, string attribute) =>
        cut.FindAll("circle.line-point")
            .Select(element => double.Parse(element.GetAttribute(attribute)!, CultureInfo.InvariantCulture))
            .ToList();

    private sealed record NumericPoint(double X, double Y, string Label, bool Highlight = false);

    private sealed record DatePoint(DateTime X, double Y, string Label);
}
