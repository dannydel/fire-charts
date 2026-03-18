namespace FireCharts.Models;

public sealed record PieChartPoint<TItem>(
    TItem Item,
    int Index,
    string Label,
    double Value,
    double Percentage,
    string Fill,
    string HoverFill,
    string PathData,
    SvgPoint LabelPosition,
    SvgPoint TooltipAnchor,
    SvgPoint Offset,
    bool UseDarkLabelText,
    string AccessibleLabel,
    bool IsSelected,
    bool IsHovered,
    bool IsFocused)
{
    public string Key => $"{Index}:{Label}";
}
