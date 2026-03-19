namespace FireCharts.Models;

public sealed record HeatmapCellInteraction<TItem>(
    TItem Item,
    int RowIndex,
    int ColumnIndex,
    string RowKey,
    string RowLabel,
    string ColumnKey,
    string ColumnLabel,
    double Value);
