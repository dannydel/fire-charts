namespace FireCharts.Models;

public sealed record HeatmapCell<TItem>(
    TItem Item,
    int RowIndex,
    int ColumnIndex,
    string RowKey,
    string RowLabel,
    string ColumnKey,
    string ColumnLabel,
    double Value,
    SvgRect Rect,
    string Fill,
    bool IsSelected,
    bool IsHovered,
    bool IsFocused,
    string AccessibleLabel)
{
    public string Key => $"{RowIndex}:{ColumnIndex}:{RowKey}:{ColumnKey}";
}
