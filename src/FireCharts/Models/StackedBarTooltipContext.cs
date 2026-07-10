namespace FireCharts.Models;

public sealed record StackedBarTooltipRow<TItem, TSegment>(
    TItem Item,
    TSegment Segment,
    int BarIndex,
    int SegmentIndex,
    string BarLabel,
    string SegmentLabel,
    double Value,
    double TotalValue,
    string Fill,
    bool IsActive);

public sealed record StackedBarTooltipContext<TItem, TSegment>(
    TItem Item,
    int BarIndex,
    string BarLabel,
    double TotalValue,
    IReadOnlyList<StackedBarTooltipRow<TItem, TSegment>> Rows,
    StackedBarChartSegment<TItem, TSegment>? ActiveSegment,
    SvgPoint Anchor,
    bool Horizontal);
