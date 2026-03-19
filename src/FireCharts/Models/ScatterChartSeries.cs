namespace FireCharts.Models;

public sealed record ScatterChartSeries<TItem>(
    string Name,
    IReadOnlyList<TItem> Items,
    string Color,
    string? HoverColor = null,
    double? MarkerRadius = null);
