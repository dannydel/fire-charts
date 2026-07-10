namespace FireCharts.Models;

public sealed record WaterfallChartPoint<TItem>(
    TItem Item,
    int Index,
    string Label,
    double Value,
    double StartValue,
    double EndValue,
    double DisplayValue,
    WaterfallStepType StepType,
    SvgRect Rect,
    string Fill,
    string HoverFill,
    bool IsIncrease,
    bool IsSelected,
    bool IsHovered,
    bool IsFocused,
    string AccessibleLabel)
{
    public string Key => $"{Index}:{Label}:{StepType}";
}
