using FireCharts.Interaction;
using FireCharts.Layout;
using FireCharts.Models;
using Microsoft.AspNetCore.Components;

namespace FireCharts.Components;

/// <summary>
/// Item-first stacked bar chart. A thin public adapter over the internal
/// <see cref="CategoricalBarChartCore{TItem, TSegment}"/>: it owns the published API surface and
/// whole-item selection semantics, and projects the core's internal segment onto the public
/// <see cref="StackedBarChartSegment{TItem, TSegment}"/> record family for consumer templates.
/// </summary>
public partial class FireStackedBarChart<TItem, TSegment> : ComponentBase
{
    private static readonly IBarLayoutStrategy LayoutStrategy = new StackedBarLayout();

    [Parameter] public string Title { get; set; } = "Stacked Bar Chart";
    [Parameter] public string Description { get; set; } = "";
    [Parameter] public IReadOnlyList<TItem>? Items { get; set; }
    [Parameter, EditorRequired] public Func<TItem, IReadOnlyList<TSegment>>? SegmentsSelector { get; set; }
    [Parameter, EditorRequired] public Func<TItem, string>? LabelSelector { get; set; }
    [Parameter, EditorRequired] public Func<TSegment, double>? SegmentValueSelector { get; set; }
    [Parameter, EditorRequired] public Func<TSegment, string>? SegmentLabelSelector { get; set; }
    [Parameter] public Func<TSegment, string>? SegmentColorSelector { get; set; }
    [Parameter] public Func<TSegment, string>? SegmentHoverColorSelector { get; set; }
    [Parameter] public TItem? SelectedItem { get; set; }
    [Parameter] public EventCallback<TItem?> SelectedItemChanged { get; set; }
    [Parameter] public EventCallback<ChartPointInteraction<TItem>> OnPointClick { get; set; }
    [Parameter] public EventCallback<ChartPointInteraction<TItem>> OnPointHoverChanged { get; set; }
    [Parameter] public RenderFragment<StackedBarChartSegment<TItem, TSegment>>? TooltipTemplate { get; set; }
    [Parameter] public RenderFragment<StackedBarTooltipContext<TItem, TSegment>>? SharedTooltipTemplate { get; set; }
    [Parameter] public RenderFragment<StackedBarChartContext<TItem, TSegment>>? ChartContentTemplate { get; set; }
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
        EqualityComparer<TItem>.Default.Equals(segment.Item, SelectedItem);

    private async Task HandleSegmentActivatedAsync(CategoricalBarSegment<TItem, TSegment> segment)
    {
        SelectedItem = segment.Item;
        await SelectedItemChanged.InvokeAsync(segment.Item);
        await OnPointClick.InvokeAsync(ToInteraction(segment));
    }

    private Task HandleSegmentHoveredAsync(CategoricalBarSegment<TItem, TSegment> segment) =>
        OnPointHoverChanged.InvokeAsync(ToInteraction(segment));

    private static ChartPointInteraction<TItem> ToInteraction(CategoricalBarSegment<TItem, TSegment> segment) =>
        new(segment.Item, segment.GroupIndex, $"{segment.GroupLabel} - {segment.SegmentLabel}", segment.Value);

    private static StackedBarChartSegment<TItem, TSegment> ProjectSegment(CategoricalBarSegment<TItem, TSegment> segment) =>
        new(
            segment.Item,
            segment.Segment,
            segment.GroupIndex,
            segment.SegmentIndex,
            segment.GroupLabel,
            segment.SegmentLabel,
            segment.Value,
            segment.TotalValue,
            segment.Fill,
            segment.HoverFill,
            segment.Rect,
            segment.AccessibleLabel,
            segment.IsSelected,
            segment.IsHovered,
            segment.IsFocused);

    private static StackedBarTooltipContext<TItem, TSegment> ProjectTooltip(CategoricalBarTooltipContext<TItem, TSegment> context) =>
        new(
            context.Item,
            context.GroupIndex,
            context.GroupLabel,
            context.TotalValue,
            context.Rows
                .Select(row => new StackedBarTooltipRow<TItem, TSegment>(
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

    private static StackedBarChartContext<TItem, TSegment> ProjectChartContext(CategoricalBarChartContext<TItem, TSegment> context) =>
        new(
            context.Plot,
            context.Horizontal,
            context.MaxValue,
            context.Segments.Select(ProjectSegment).ToList());
}
