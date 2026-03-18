namespace FireCharts.Models;

public sealed record LineChartSeries<TItem>(
    string Name,
    IReadOnlyList<TItem> Items,
    string Color,
    string? HoverColor = null,
    string? StrokeColor = null,
    string? FillColor = null,
    double? StrokeWidth = null,
    double? AreaOpacity = null);
