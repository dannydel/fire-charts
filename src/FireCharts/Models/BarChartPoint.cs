namespace FireCharts.Models;

public sealed record BarChartPoint<TItem>(
    TItem Item,
    int Index,
    string Label,
    double Value,
    string Fill,
    string HoverFill,
    SvgRect Rect,
    string AccessibleLabel,
    bool IsSelected,
    bool IsHovered,
    bool IsFocused)
{
    public string Key => $"{Index}:{Label}";
}
