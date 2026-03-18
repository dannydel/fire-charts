using FireCharts.Components;
using FireCharts.Models;

namespace FireCharts.Tests;

public sealed class LibraryHelperTests : TestContext
{
    [Fact]
    public void LineChartXValueFactoriesSupportNumberAndDateTimeFormatting()
    {
        var numeric = LineChartXValue.FromNumber(12.5);
        var date = new DateTime(2025, 3, 15, 14, 30, 0, DateTimeKind.Utc);
        var dateValue = LineChartXValue.FromDateTime(date);

        Assert.Equal(LineChartXValueKind.Number, numeric.Kind);
        Assert.False(numeric.IsDateTime);
        Assert.Equal("12.5", numeric.ToString());

        Assert.Equal(LineChartXValueKind.DateTime, dateValue.Kind);
        Assert.True(dateValue.IsDateTime);
        Assert.Equal(date, dateValue.ToDateTime());
        Assert.Equal("2025-03-15 14:30:00Z", dateValue.ToString());
    }

    [Fact]
    public void SvgTextEncodesContentAndFallsBackForInvalidCoordinates()
    {
        var cut = RenderComponent<SvgText>(parameters => parameters
            .Add(component => component.X, double.NaN)
            .Add(component => component.Y, double.PositiveInfinity)
            .Add(component => component.TextAnchor, "middle")
            .Add(component => component.DominantBaseline, "middle")
            .Add(component => component.CssClass, "label")
            .Add(component => component.Content, "<Heat & Smoke>"));

        var text = cut.Find("text");

        Assert.Equal("0.0", text.GetAttribute("x"));
        Assert.Equal("0.0", text.GetAttribute("y"));
        Assert.Equal("middle", text.GetAttribute("text-anchor"));
        Assert.Equal("middle", text.GetAttribute("dominant-baseline"));
        Assert.Equal("label", text.GetAttribute("class"));
        Assert.Equal("<Heat & Smoke>", text.TextContent);
        Assert.Contains("&lt;Heat &amp; Smoke&gt;", cut.Markup);
    }

    [Fact]
    public void ModelTypesExposeIdentityAndInteractionData()
    {
        var rect = new SvgRect(10, 20, 30, 40);
        var point = new SvgPoint(12, 24);
        var xValue = LineChartXValue.FromNumber(5);

        var barPoint = new BarChartPoint<string>("Permits", 2, "March", 17, "#123456", "#234567", rect, "March: 17", true, false, true);
        var piePoint = new PieChartPoint<string>("Calls", 1, "Suppression", 12, 0.4, "#abcdef", "#bcdef0", "M 0 0", point, point, point, true, "Suppression: 12", false, true, false);
        var linePoint = new LineChartPoint<string>("Inspections", 0, "Series A", 3, "Q3", xValue, 42, point, "#111111", "#222222", "#333333", true, false, true, false, "Series A Q3: 42");
        var pointInteraction = new ChartPointInteraction<string>("Incidents", 4, "April", 8);
        var lineInteraction = new LineChartPointInteraction<string>("Incidents", 1, "Series B", 2, "April", xValue, 8);
        var series = new LineChartSeries<string>("Series B", ["A", "B"], "#444444", "#555555", "#666666", "#777777", 3.5, 0.25);
        var surface = new ChartSurfaceContext(640, 320);

        Assert.Equal((10d, 20d, 30d, 40d), (rect.X, rect.Y, rect.Width, rect.Height));
        Assert.Equal((12d, 24d), (point.X, point.Y));

        Assert.Equal("2:March", barPoint.Key);
        Assert.Equal(("Permits", 17d, true, true), (barPoint.Item, barPoint.Value, barPoint.IsSelected, barPoint.IsFocused));

        Assert.Equal("1:Suppression", piePoint.Key);
        Assert.Equal(("Calls", 0.4d, true), (piePoint.Item, piePoint.Percentage, piePoint.IsHovered));

        Assert.Equal("0:3:Q3", linePoint.Key);
        Assert.Equal(("Series A", 42d, true), (linePoint.SeriesName, linePoint.Y, linePoint.IsHighlighted));

        Assert.Equal(("Incidents", 4, "April", 8d), (pointInteraction.Item, pointInteraction.Index, pointInteraction.Label, pointInteraction.Value));
        Assert.Equal(("Series B", 2, "April", 8d), (lineInteraction.SeriesName, lineInteraction.PointIndex, lineInteraction.Label, lineInteraction.Y));
        Assert.Equal(("Series B", "#666666", "#777777", 3.5d, 0.25d), (series.Name, series.StrokeColor, series.FillColor, series.StrokeWidth, series.AreaOpacity));
        Assert.Equal((640d, 320d), (surface.Width, surface.Height));
    }
}
