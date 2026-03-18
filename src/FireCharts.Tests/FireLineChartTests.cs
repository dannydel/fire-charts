using System.Globalization;
using FireCharts.Components;
using FireCharts.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

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
    public void RendersCustomEmptyStateTemplate()
    {
        var points = new[]
        {
            new NumericPoint(double.NaN, 12, "Bad X")
        };

        var cut = RenderComponent<FireLineChart<NumericPoint>>(parameters => parameters
            .Add(component => component.Items, points)
            .Add(component => component.XValueSelector, point => LineChartXValue.FromNumber(point.X))
            .Add(component => component.YValueSelector, point => point.Y)
            .Add(component => component.EmptyStateTemplate, (RenderFragment)(builder => builder.AddContent(0, "No incidents"))));

        Assert.Contains("No incidents", cut.Markup);
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
    public void LegendHoverAndFocusMuteInactiveSeries()
    {
        var cut = RenderChartWithTwoSeries();
        cut.FindAll(".line-legend__item")[0].MouseOver();
        cut.WaitForAssertion(() =>
        {
            var legendItems = cut.FindAll(".line-legend__item");
            var seriesGroups = cut.FindAll("g.line-series-group");
            Assert.Contains("is-active", legendItems[0].GetAttribute("class"));
            Assert.Contains("is-muted", legendItems[1].GetAttribute("class"));
            Assert.Contains("is-active", seriesGroups[0].GetAttribute("class"));
            Assert.Contains("is-muted", seriesGroups[1].GetAttribute("class"));
        });

        cut.FindAll(".line-legend__item")[0].MouseOut();
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("is-active", cut.FindAll(".line-legend__item")[0].GetAttribute("class"));
            Assert.DoesNotContain("is-muted", cut.FindAll(".line-legend__item")[1].GetAttribute("class"));
        });

        cut.FindAll(".line-legend__item")[1].Focus();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("is-active", cut.FindAll(".line-legend__item")[1].GetAttribute("class"));
            Assert.Contains("is-muted", cut.FindAll(".line-legend__item")[0].GetAttribute("class"));
        });
    }

    [Fact]
    public void UsesHighlightedOnlyMarkersByDefaultAndSupportsNoneDisplayMode()
    {
        var points = new[]
        {
            new NumericPoint(1, 5, "One"),
            new NumericPoint(2, 7, "Two", true),
            new NumericPoint(3, 6, "Three")
        };

        var highlightedOnly = RenderNumericChart(points, LinePointDisplayMode.HighlightedOnly);
        var noPoints = RenderNumericChart(points, LinePointDisplayMode.None);
        var allPoints = RenderNumericChart(points, LinePointDisplayMode.All);

        Assert.Single(highlightedOnly.FindAll("circle.line-point"));
        Assert.Empty(noPoints.FindAll("circle.line-point"));
        Assert.Equal(3, allPoints.FindAll("circle.line-point").Count);
    }

    [Fact]
    public void HoverFocusKeyboardAndClickCallbacksReturnPointDetails()
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

        cut.FindAll("g.line-point-group")[0].MouseOver();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(hovered);
            Assert.Equal("One", hovered!.Label);
            Assert.Single(cut.FindAll(".chart-tooltip"));
        });

        cut.FindAll("g.line-point-group")[0].Focus();
        cut.FindAll("g.line-point-group")[0].MouseOut();
        cut.WaitForAssertion(() =>
        {
            var firstGroup = cut.FindAll("g.line-point-group")[0];
            Assert.Contains("is-hovered", firstGroup.GetAttribute("class"));
            Assert.Contains("is-focused", firstGroup.GetAttribute("class"));
            Assert.Single(cut.FindAll(".chart-tooltip"));
        });

        cut.FindAll("g.line-point-group")[0].Blur();
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".chart-tooltip"));
            Assert.DoesNotContain("is-focused", cut.FindAll("g.line-point-group")[0].GetAttribute("class"));
        });

        cut.FindAll("g.line-point-group")[1].KeyDown(new KeyboardEventArgs { Key = "Enter" });
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(selected);
            Assert.Equal("Two", selected!.Label);
            Assert.NotNull(clicked);
            Assert.Equal(7, clicked!.Y);
            Assert.Contains("is-selected", cut.FindAll("g.line-point-group")[1].GetAttribute("class"));
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
    public void AreaVariantRendersFillPathAndSeriesStyleOverrides()
    {
        var series = new[]
        {
            new LineChartSeries<NumericPoint>(
                "Permits",
                new[]
                {
                    new NumericPoint(1, 3, "One", true),
                    new NumericPoint(2, 6, "Two", true),
                    new NumericPoint(3, 5, "Three", true)
                },
                "#d94f3d",
                HoverColor: "#7f2018",
                StrokeColor: "#123456",
                FillColor: "#abcdef",
                StrokeWidth: 6.5,
                AreaOpacity: 0.42)
        };

        var cut = RenderComponent<FireLineChart<NumericPoint>>(parameters => parameters
            .Add(component => component.Series, series)
            .Add(component => component.XValueSelector, point => LineChartXValue.FromNumber(point.X))
            .Add(component => component.YValueSelector, point => point.Y)
            .Add(component => component.LabelSelector, point => point.Label)
            .Add(component => component.HighlightSelector, point => point.Highlight)
            .Add(component => component.PointDisplayMode, LinePointDisplayMode.All)
            .Add(component => component.Variant, LineChartVariant.Area));

        var area = cut.Find("path.line-area");
        var line = cut.Find("path.line-path");

        Assert.Equal("#abcdef", area.GetAttribute("fill"));
        Assert.Contains("--series-area-opacity: 0.42", area.GetAttribute("style"));
        Assert.Equal("#123456", line.GetAttribute("stroke"));
        Assert.Equal("6.5", line.GetAttribute("stroke-width"));
        Assert.Contains("--series-hover-color: #7f2018", line.GetAttribute("style"));
    }

    [Fact]
    public void ShowsCustomTooltipForHoveredHighlightedPoint()
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
            .Add(component => component.HighlightSelector, point => point.Highlight)
            .Add(component => component.TooltipTemplate, (RenderFragment<LineChartPoint<NumericPoint>>)(point => builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "custom-line-tooltip");
                builder.AddContent(2, $"tooltip-{point.Label}");
                builder.CloseElement();
            })));

        cut.Find("g.line-point-group").MouseOver();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("tooltip-One", cut.Markup);
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
            RenderComponent<FireLineChart<object>>(parameters => parameters
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

    [Fact]
    public void SinglePointDataPadsTheDomainAwayFromTheAxes()
    {
        var point = new[] { new NumericPoint(10, 25, "Only", true) };

        var cut = RenderNumericChart(point, LinePointDisplayMode.All);
        var circle = cut.Find("circle.line-point");
        var x = double.Parse(circle.GetAttribute("cx")!, CultureInfo.InvariantCulture);

        Assert.True(x > 120);
        Assert.True(x < 520);
        Assert.True(cut.FindAll("g.category-axis-labels text").Count >= 2);
    }

    [Fact]
    public void RendersNegativeOnlyYAxisWithZeroIncluded()
    {
        var points = new[]
        {
            new NumericPoint(1, -9, "One", true),
            new NumericPoint(2, -3, "Two", true),
            new NumericPoint(3, -6, "Three", true)
        };

        var cut = RenderNumericChart(points, LinePointDisplayMode.All);
        var labels = cut.FindAll("g.value-axis-labels text").Select(element => element.TextContent).ToList();

        Assert.Contains("0", labels);
        Assert.Contains("-10", labels);
    }

    [Fact]
    public void RendersMixedSignYAxisLabels()
    {
        var points = new[]
        {
            new NumericPoint(1, -5, "One", true),
            new NumericPoint(2, 10, "Two", true)
        };

        var cut = RenderNumericChart(points, LinePointDisplayMode.All);
        var labels = cut.FindAll("g.value-axis-labels text").Select(element => element.TextContent).ToList();

        Assert.Contains("-5", labels);
        Assert.Contains("0", labels);
        Assert.Contains("10", labels);
    }

    [Fact]
    public void FormatsShortDateRangesWithTimes()
    {
        var points = new[]
        {
            new DatePoint(new DateTime(2025, 3, 15, 8, 0, 0), 12, "Start"),
            new DatePoint(new DateTime(2025, 3, 15, 16, 0, 0), 18, "End")
        };

        var cut = RenderDateChart(points);

        Assert.Contains(cut.FindAll("g.category-axis-labels text").Select(element => element.TextContent), label => label.Contains(':'));
    }

    [Fact]
    public void FormatsMediumDateRangesWithMonthAndDay()
    {
        var points = new[]
        {
            new DatePoint(new DateTime(2025, 3, 1), 12, "Start"),
            new DatePoint(new DateTime(2025, 3, 12), 18, "End")
        };

        var cut = RenderDateChart(points);
        var labels = cut.FindAll("g.category-axis-labels text").Select(element => element.TextContent).ToList();

        Assert.Contains(labels, label => label.StartsWith("Mar ", StringComparison.Ordinal) && !label.Contains(':'));
    }

    [Fact]
    public void FormatsLongDateRangesWithMonthAndYear()
    {
        var points = new[]
        {
            new DatePoint(new DateTime(2024, 1, 1), 12, "Start"),
            new DatePoint(new DateTime(2027, 6, 1), 18, "End")
        };

        var cut = RenderDateChart(points);
        var labels = cut.FindAll("g.category-axis-labels text").Select(element => element.TextContent).ToList();

        Assert.Contains(labels, label => label.Contains("2024", StringComparison.Ordinal) || label.Contains("2027", StringComparison.Ordinal));
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

    private IRenderedComponent<FireLineChart<DatePoint>> RenderDateChart(IReadOnlyList<DatePoint> points) =>
        RenderComponent<FireLineChart<DatePoint>>(parameters => parameters
            .Add(component => component.Items, points)
            .Add(component => component.XValueSelector, point => LineChartXValue.FromDateTime(point.X))
            .Add(component => component.YValueSelector, point => point.Y)
            .Add(component => component.LabelSelector, point => point.Label)
            .Add(component => component.PointDisplayMode, LinePointDisplayMode.All));

    private IRenderedComponent<FireLineChart<NumericPoint>> RenderChartWithTwoSeries()
    {
        var series = new[]
        {
            new LineChartSeries<NumericPoint>("Permits", new[]
            {
                new NumericPoint(1, 4, "One", true),
                new NumericPoint(2, 6, "Two", true)
            }, "#d94f3d"),
            new LineChartSeries<NumericPoint>("Inspections", new[]
            {
                new NumericPoint(1, 5, "One", true),
                new NumericPoint(2, 7, "Two", true)
            }, "#198f8c")
        };

        return RenderComponent<FireLineChart<NumericPoint>>(parameters => parameters
            .Add(component => component.Series, series)
            .Add(component => component.XValueSelector, point => LineChartXValue.FromNumber(point.X))
            .Add(component => component.YValueSelector, point => point.Y)
            .Add(component => component.LabelSelector, point => point.Label)
            .Add(component => component.HighlightSelector, point => point.Highlight)
            .Add(component => component.PointDisplayMode, LinePointDisplayMode.All));
    }

    private static List<double> GetCirclePositions(IRenderedFragment cut, string attribute) =>
        cut.FindAll("circle.line-point")
            .Select(element => double.Parse(element.GetAttribute(attribute)!, CultureInfo.InvariantCulture))
            .ToList();

    private sealed record NumericPoint(double X, double Y, string Label, bool Highlight = false);

    private sealed record DatePoint(DateTime X, double Y, string Label);
}
