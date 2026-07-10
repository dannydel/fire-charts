using System.ComponentModel;
using FireCharts.Models;

namespace FireCharts.Components;

// These records back the internal CategoricalBarChartCore composition. They would be `internal`,
// but the .NET Razor source generator forces the core component to be `public`, which in turn
// forces the types on its public parameter signatures to be public too. They are hidden from
// IntelliSense with [EditorBrowsable(Never)] and are NOT part of the supported public API — the
// supported surface is FireStackedBarChart / FireClusteredBarChart and their record families.

/// <summary>
/// The unified segment shared by the stacked and clustered cores. Superset of both public
/// segment records; the wrappers project it to <c>StackedBarChartSegment</c> or
/// <c>ClusteredBarChartSegment</c>. <see cref="Key"/> intentionally matches both public
/// records' key format so <c>@key</c> identity and selection stay stable.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed record CategoricalBarSegment<TItem, TSegment>(
    TItem Item,
    TSegment Segment,
    int GroupIndex,
    int SegmentIndex,
    string GroupLabel,
    string SegmentLabel,
    double Value,
    double TotalValue,
    string Fill,
    string HoverFill,
    SvgRect Rect,
    string AccessibleLabel,
    bool IsSelected,
    bool IsHovered,
    bool IsFocused)
{
    public string Key => $"{GroupIndex}:{SegmentIndex}:{GroupLabel}:{SegmentLabel}";
}

/// <summary>
/// The unified internal category/bar container (replaces the parallel <c>BarState</c> and
/// <c>CategoryState</c> records).
/// </summary>
internal sealed record CategoricalBarGroup<TItem, TSegment>(
    int Index,
    TItem Item,
    string Label,
    double TotalValue,
    SvgRect Rect,
    SvgRect HoverRect,
    SvgPoint AxisLabelPoint,
    IReadOnlyList<CategoricalBarSegment<TItem, TSegment>> Segments);

/// <summary>A legend entry: a unique series label and its resolved fill color.</summary>
internal sealed record CategoricalBarLegendItem(string Label, string Fill);

/// <summary>One row of a shared (whole-group) tooltip.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed record CategoricalBarTooltipRow<TItem, TSegment>(
    TItem Item,
    TSegment Segment,
    int GroupIndex,
    int SegmentIndex,
    string GroupLabel,
    string SegmentLabel,
    double Value,
    double TotalValue,
    string Fill,
    bool IsActive);

/// <summary>The shared tooltip context for a whole group.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed record CategoricalBarTooltipContext<TItem, TSegment>(
    TItem Item,
    int GroupIndex,
    string GroupLabel,
    double TotalValue,
    IReadOnlyList<CategoricalBarTooltipRow<TItem, TSegment>> Rows,
    CategoricalBarSegment<TItem, TSegment>? ActiveSegment,
    SvgPoint Anchor,
    bool Horizontal);

/// <summary>The overlay chart-content context handed to the core's content template seam.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed record CategoricalBarChartContext<TItem, TSegment>(
    PlotArea Plot,
    bool Horizontal,
    double MaxValue,
    IReadOnlyList<CategoricalBarSegment<TItem, TSegment>> Segments);
