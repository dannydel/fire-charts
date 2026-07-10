namespace FireCharts.Models;

public sealed record ClusteredBarTooltipRow<TItem, TSegment>(
    TItem Item,
    TSegment Segment,
    int CategoryIndex,
    int SegmentIndex,
    string CategoryLabel,
    string SeriesLabel,
    double Value,
    double TotalValue,
    string Fill,
    bool IsActive);

public sealed record ClusteredBarTooltipContext<TItem, TSegment>(
    TItem Item,
    int CategoryIndex,
    string CategoryLabel,
    double TotalValue,
    IReadOnlyList<ClusteredBarTooltipRow<TItem, TSegment>> Rows,
    ClusteredBarChartSegment<TItem, TSegment>? ActiveSegment,
    SvgPoint Anchor,
    bool Horizontal);
