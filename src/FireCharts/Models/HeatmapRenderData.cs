namespace FireCharts.Models;

internal sealed record HeatmapRowDefinition(string Key, string Label, double Order, int Index, double Y, double Height)
{
    public double CenterY => Y + (Height / 2);
}

internal sealed record HeatmapColumnDefinition(string Key, string Label, double Order, int Index, double X, double Width)
{
    public double CenterX => X + (Width / 2);
}

internal sealed record HeatmapPlaceholderCell(SvgRect Rect, string Fill);

internal sealed record HeatmapRenderData<TItem>(
    IReadOnlyList<HeatmapCell<TItem>> Cells,
    IReadOnlyList<HeatmapPlaceholderCell> PlaceholderCells,
    IReadOnlyList<HeatmapRowDefinition> Rows,
    IReadOnlyList<HeatmapColumnDefinition> Columns,
    double MinValue,
    double MaxValue)
{
    public static HeatmapRenderData<TItem> Empty { get; } = new(
        Array.Empty<HeatmapCell<TItem>>(),
        Array.Empty<HeatmapPlaceholderCell>(),
        Array.Empty<HeatmapRowDefinition>(),
        Array.Empty<HeatmapColumnDefinition>(),
        0,
        0);
}
