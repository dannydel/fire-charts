namespace FireCharts.Models;

internal sealed record HeatmapCanvasRenderRect(
    int RowIndex,
    int ColumnIndex,
    double X,
    double Y,
    double Width,
    double Height,
    string Fill);

internal sealed record HeatmapCanvasRenderRequest(
    double Width,
    double Height,
    double CornerRadius,
    IReadOnlyList<HeatmapCanvasRenderRect> PlaceholderCells,
    IReadOnlyList<HeatmapCanvasRenderRect> Cells);

internal sealed record HeatmapCanvasInteractionState(
    string? SelectedCellKey,
    string? HoveredCellKey,
    string? FocusedCellKey);
