namespace FireCharts.Models;

public sealed record ClusteredBarChartSegmentInteraction<TItem, TSegment>(
    TItem Item,
    TSegment Segment,
    int CategoryIndex,
    int SegmentIndex,
    string CategoryLabel,
    string SeriesLabel,
    double Value);
