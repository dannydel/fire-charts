using FireCharts.Layout;
using FireCharts.Models;
using Microsoft.AspNetCore.Components;

namespace FireCharts.Components;

/// <summary>
/// Item-first clustered (grouped) bar chart. A thin public adapter over the internal
/// <see cref="CategoricalBarChartCore{TItem, TSegment}"/>: it owns the published API surface and
/// per-segment selection semantics, and projects the core's internal segment onto the public
/// <see cref="ClusteredBarChartSegment{TItem, TSegment}"/> record family for consumer templates.
/// </summary>
public partial class FireClusteredBarChart<TItem, TSegment> : ComponentBase
{
    private static readonly IBarLayoutStrategy LayoutStrategy = new ClusteredBarLayout();

    [Parameter] public string Title { get; set; } = "Clustered Bar Chart";
    [Parameter] public string Description { get; set; } = "";
    [Parameter] public IReadOnlyList<TItem>? Items { get; set; }
    [Parameter, EditorRequired] public Func<TItem, IReadOnlyList<TSegment>>? SegmentsSelector { get; set; }
    [Parameter, EditorRequired] public Func<TItem, string>? LabelSelector { get; set; }
    [Parameter, EditorRequired] public Func<TSegment, double>? SegmentValueSelector { get; set; }
    [Parameter, EditorRequired] public Func<TSegment, string>? SegmentLabelSelector { get; set; }
    [Parameter] public Func<TSegment, string>? SegmentColorSelector { get; set; }
    [Parameter] public Func<TSegment, string>? SegmentHoverColorSelector { get; set; }
    [Parameter] public ClusteredBarChartSegment<TItem, TSegment>? SelectedSegment { get; set; }
    [Parameter] public EventCallback<ClusteredBarChartSegment<TItem, TSegment>?> SelectedSegmentChanged { get; set; }
    [Parameter] public EventCallback<ClusteredBarChartSegmentInteraction<TItem, TSegment>> OnSegmentClick { get; set; }
    [Parameter] public EventCallback<ClusteredBarChartSegmentInteraction<TItem, TSegment>> OnSegmentHoverChanged { get; set; }
    [Parameter] public RenderFragment<ClusteredBarChartSegment<TItem, TSegment>>? TooltipTemplate { get; set; }
    [Parameter] public RenderFragment<ClusteredBarTooltipContext<TItem, TSegment>>? SharedTooltipTemplate { get; set; }
    [Parameter] public RenderFragment<ClusteredBarChartContext<TItem, TSegment>>? ChartContentTemplate { get; set; }
    [Parameter] public RenderFragment? EmptyStateTemplate { get; set; }
    [Parameter] public double Width { get; set; } = 640;
    [Parameter] public double Height { get; set; } = 420;
    [Parameter] public bool Responsive { get; set; }
    [Parameter] public bool Horizontal { get; set; }
    [Parameter] public bool ShowGridLines { get; set; } = true;
    [Parameter] public bool ShowAxisLabels { get; set; } = true;
    [Parameter] public bool ShowTooltip { get; set; } = true;
    [Parameter] public bool ConstrainTooltipToChartBounds { get; set; }
    [Parameter] public bool ShowLegend { get; set; } = true;
    [Parameter] public int GridLineCount { get; set; } = 5;
    [Parameter] public double BarWidthRatio { get; set; } = 0.72;
    [Parameter] public double GroupSpacing { get; set; } = 0.18;
    [Parameter] public double SeriesSpacing { get; set; } = 0.12;
    [Parameter] public string ValueFormat { get; set; } = "F0";
    [Parameter] public double? MaxValue { get; set; }
    [Parameter] public double CornerRadius { get; set; } = 3;
    [Parameter] public ChartLegendPlacement LegendPlacement { get; set; } = ChartLegendPlacement.Bottom;
    [Parameter] public BarTooltipInteractionMode TooltipInteractionMode { get; set; } = BarTooltipInteractionMode.Shared;

    private RenderFragment<CategoricalBarSegment<TItem, TSegment>>? CoreTooltipTemplate =>
        TooltipTemplate is null ? null : segment => TooltipTemplate(ProjectSegment(segment));

    private RenderFragment<CategoricalBarTooltipContext<TItem, TSegment>>? CoreSharedTooltipTemplate =>
        SharedTooltipTemplate is null ? null : context => SharedTooltipTemplate(ProjectTooltip(context));

    private RenderFragment<CategoricalBarChartContext<TItem, TSegment>>? CoreChartContentTemplate =>
        ChartContentTemplate is null ? null : context => ChartContentTemplate(ProjectChartContext(context));

    private bool IsSegmentSelectedCore(CategoricalBarSegment<TItem, TSegment> segment) =>
        string.Equals(SelectedSegment?.Key, segment.Key, StringComparison.Ordinal);

    private async Task HandleSegmentActivatedAsync(CategoricalBarSegment<TItem, TSegment> segment)
    {
        SelectedSegment = ProjectSegment(segment);
        await SelectedSegmentChanged.InvokeAsync(SelectedSegment);
        await OnSegmentClick.InvokeAsync(ToInteraction(segment));
    }

    private Task HandleSegmentHoveredAsync(CategoricalBarSegment<TItem, TSegment> segment) =>
        OnSegmentHoverChanged.InvokeAsync(ToInteraction(segment));

    private static ClusteredBarChartSegmentInteraction<TItem, TSegment> ToInteraction(CategoricalBarSegment<TItem, TSegment> segment) =>
        new(
            segment.Item,
            segment.Segment,
            segment.GroupIndex,
            segment.SegmentIndex,
            segment.GroupLabel,
            segment.SegmentLabel,
            segment.Value);

    private static ClusteredBarChartSegment<TItem, TSegment> ProjectSegment(CategoricalBarSegment<TItem, TSegment> segment) =>
        new(
            segment.Item,
            segment.Segment,
            segment.GroupIndex,
            segment.SegmentIndex,
            segment.GroupLabel,
            segment.SegmentLabel,
            segment.Value,
            segment.Fill,
            segment.HoverFill,
            segment.Rect,
            segment.AccessibleLabel,
            segment.IsSelected,
            segment.IsHovered,
            segment.IsFocused);

    private static ClusteredBarTooltipContext<TItem, TSegment> ProjectTooltip(CategoricalBarTooltipContext<TItem, TSegment> context) =>
        new(
            context.Item,
            context.GroupIndex,
            context.GroupLabel,
            context.TotalValue,
            context.Rows
                .Select(row => new ClusteredBarTooltipRow<TItem, TSegment>(
                    row.Item,
                    row.Segment,
                    row.GroupIndex,
                    row.SegmentIndex,
                    row.GroupLabel,
                    row.SegmentLabel,
                    row.Value,
                    row.TotalValue,
                    row.Fill,
                    row.IsActive))
                .ToList(),
            context.ActiveSegment is null ? null : ProjectSegment(context.ActiveSegment),
            context.Anchor,
            context.Horizontal);

    private static ClusteredBarChartContext<TItem, TSegment> ProjectChartContext(CategoricalBarChartContext<TItem, TSegment> context) =>
        new(
            context.Plot,
            context.Horizontal,
            context.MaxValue,
            context.Segments.Select(ProjectSegment).ToList());
}
