using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using FireCharts.Interaction;
using FireCharts.Layout;
using FireCharts.Models;
using FireCharts.Scales;
using Microsoft.AspNetCore.Components;

namespace FireCharts.Components;

/// <summary>
/// Composed core shared by <see cref="FireStackedBarChart{TItem, TSegment}"/> and
/// <see cref="FireClusteredBarChart{TItem, TSegment}"/>. Owns the shared-tooltip subsystem, the
/// hover/focus/keyboard state machine (via <see cref="ChartInteraction{TElement, TKey}"/>), the
/// legend, axes/grid/empty-state markup, resize, and layout dispatch through an
/// <see cref="IBarLayoutStrategy"/>. Selection and public record projection stay with the wrappers,
/// injected through <see cref="IsSegmentSelected"/> and <see cref="OnSegmentActivated"/>.
/// </summary>
/// <remarks>
/// This is an internal composition detail. It is declared <c>public</c> only because the .NET
/// Razor source generator emits component classes as <c>public</c> (an <c>internal</c> code-behind
/// modifier is rejected with CS0262); it is hidden from IntelliSense with
/// <see cref="EditorBrowsableAttribute"/> and is not part of the supported public API. Consumers
/// use the two wrapper components, never this type.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public partial class CategoricalBarChartCore<TItem, TSegment> : ComponentBase
{
    private static readonly string[] DefaultPalette =
    [
        "#d94f3d",
        "#e79b21",
        "#198f8c",
        "#8a7cf6",
        "#f05f3b",
        "#4d89ff"
    ];

    private IReadOnlyList<CategoricalBarSegment<TItem, TSegment>> _segments = Array.Empty<CategoricalBarSegment<TItem, TSegment>>();
    private IReadOnlyList<CategoricalBarGroup<TItem, TSegment>> _groups = Array.Empty<CategoricalBarGroup<TItem, TSegment>>();
    private IReadOnlyList<CategoricalBarLegendItem> _legendItems = Array.Empty<CategoricalBarLegendItem>();
    private Dictionary<string, int> _segmentIndexByKey = [];
    private IReadOnlyList<ScaleTick> _valueAxisTicks = Array.Empty<ScaleTick>();
    private double _computedMaxValue = 100;
    private int? _hoveredGroupIndex;
    private PlotArea _plot;
    private ChartInteraction<CategoricalBarSegment<TItem, TSegment>, int> _interaction = default!;
    private string? _hoveredLegendLabel;

    [Parameter] public string Title { get; set; } = "";
    [Parameter] public string Description { get; set; } = "";
    [Parameter] public IReadOnlyList<TItem>? Items { get; set; }
    [Parameter, EditorRequired] public Func<TItem, IReadOnlyList<TSegment>>? SegmentsSelector { get; set; }
    [Parameter, EditorRequired] public Func<TItem, string>? LabelSelector { get; set; }
    [Parameter, EditorRequired] public Func<TSegment, double>? SegmentValueSelector { get; set; }
    [Parameter, EditorRequired] public Func<TSegment, string>? SegmentLabelSelector { get; set; }
    [Parameter] public Func<TSegment, string>? SegmentColorSelector { get; set; }
    [Parameter] public Func<TSegment, string>? SegmentHoverColorSelector { get; set; }
    [Parameter] public RenderFragment<CategoricalBarSegment<TItem, TSegment>>? TooltipTemplate { get; set; }
    [Parameter] public RenderFragment<CategoricalBarTooltipContext<TItem, TSegment>>? SharedTooltipTemplate { get; set; }
    [Parameter] public RenderFragment<CategoricalBarChartContext<TItem, TSegment>>? ChartContentTemplate { get; set; }
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

    /// <summary>The geometry seam. A stateless singleton supplied by the wrapper.</summary>
    [Parameter, EditorRequired] public IBarLayoutStrategy Layout { get; set; } = default!;

    /// <summary>The CSS class prefix (<c>"stacked"</c> or <c>"clustered"</c>). Keeps class names verbatim.</summary>
    [Parameter, EditorRequired] public string CssPrefix { get; set; } = "stacked";

    /// <summary>Selection predicate injected by the wrapper (item-equality vs key-equality).</summary>
    [Parameter] public Func<CategoricalBarSegment<TItem, TSegment>, bool>? IsSegmentSelected { get; set; }

    /// <summary>Raised on click / Enter / Space. The wrapper owns selection and public event emission.</summary>
    [Parameter] public EventCallback<CategoricalBarSegment<TItem, TSegment>> OnSegmentActivated { get; set; }

    /// <summary>Raised when the active (hover/focus) segment changes. The wrapper emits the public hover event.</summary>
    [Parameter] public EventCallback<CategoricalBarSegment<TItem, TSegment>> OnSegmentHovered { get; set; }

    private ChartPadding Padding => new(
        Top: 10,
        Right: 10,
        Bottom: ShowAxisLabels ? 40 : 10,
        Left: ShowAxisLabels ? (Horizontal ? 90 : 50) : 10);

    internal IReadOnlyList<CategoricalBarSegment<TItem, TSegment>> Segments => _segments;
    internal IReadOnlyList<CategoricalBarGroup<TItem, TSegment>> Groups => _groups;
    internal IReadOnlyList<CategoricalBarLegendItem> LegendItems => _legendItems;
    internal CategoricalBarSegment<TItem, TSegment>? HoveredSegment => _interaction.Hovered;
    internal CategoricalBarSegment<TItem, TSegment>? FocusedSegment => _interaction.Focused;
    internal bool UsesSharedTooltip => TooltipInteractionMode == BarTooltipInteractionMode.Shared;
    internal int SafeGridLineCount => Math.Max(GridLineCount, 1);
    internal bool HasLegendHover => !string.IsNullOrWhiteSpace(_hoveredLegendLabel);
    internal bool ShouldRenderLegendBeforeChart => LegendPlacement is ChartLegendPlacement.Top or ChartLegendPlacement.Left or ChartLegendPlacement.Start;
    internal string ShellCssClasses => string.Join(" ", GetShellClasses());

    internal string ShellRootCssClass => $"fire-{CssPrefix}-bar-chart-shell";
    internal string HostCssClass => $"fire-{CssPrefix}-bar-chart-host";
    internal string SvgCssClass => $"fire-{CssPrefix}-bar-chart";
    internal string SeriesCssClass => $"{CssPrefix}-bar-series";
    internal string GroupCssClass => CssPrefix == "clustered" ? "clustered-category" : "stacked-bar";
    internal string HoverTargetCssClass => $"{CssPrefix}-bar-hover-target";
    internal string SegmentRectCssClass => $"{CssPrefix}-segment-rect";
    internal string ChartContentCssClass => $"{CssPrefix}-chart-content";
    internal string LegendCssClass => $"{CssPrefix}-bar-legend";

    internal int? TooltipGroupIndex => UsesSharedTooltip
        ? _hoveredGroupIndex ?? HoveredSegment?.GroupIndex ?? FocusedSegment?.GroupIndex
        : HoveredSegment?.GroupIndex;
    internal CategoricalBarGroup<TItem, TSegment>? TooltipGroup => TooltipGroupIndex is int index
        ? index >= 0 && index < _groups.Count ? _groups[index] : null
        : null;
    internal CategoricalBarSegment<TItem, TSegment>? TooltipActiveSegment
    {
        get
        {
            var ownerIndex = TooltipGroupIndex;
            if (ownerIndex is null)
            {
                return UsesSharedTooltip ? null : HoveredSegment;
            }

            if (HoveredSegment?.GroupIndex == ownerIndex)
            {
                return HoveredSegment;
            }

            return FocusedSegment?.GroupIndex == ownerIndex ? FocusedSegment : null;
        }
    }
    internal CategoricalBarTooltipContext<TItem, TSegment>? SharedTooltipContext =>
        !UsesSharedTooltip || TooltipGroup is null
            ? null
            : CreateSharedTooltipContext(TooltipGroup);
    internal CategoricalBarChartContext<TItem, TSegment> ChartContext =>
        new(
            _plot,
            Horizontal,
            ComputedMaxValue,
            Segments);

    internal double ComputedMaxValue => _computedMaxValue;
    internal IReadOnlyList<ScaleTick> ValueAxisTicks => _valueAxisTicks;

    protected override void OnInitialized()
    {
        _interaction = new ChartInteraction<CategoricalBarSegment<TItem, TSegment>, int>(
            new ChartInteractionOptions<CategoricalBarSegment<TItem, TSegment>, int>
            {
                KeySelector = FindSegmentIndex,
                RequestRender = () => InvokeAsync(StateHasChanged),
                OnActiveChanged = segment => OnSegmentHovered.InvokeAsync(segment),
                OnActivate = segment => OnSegmentActivated.InvokeAsync(segment)
            });
    }

    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(SegmentsSelector);
        ArgumentNullException.ThrowIfNull(LabelSelector);
        ArgumentNullException.ThrowIfNull(SegmentValueSelector);
        ArgumentNullException.ThrowIfNull(SegmentLabelSelector);
        ArgumentNullException.ThrowIfNull(Layout);

        Width = Math.Max(Width, 1);
        Height = Math.Max(Height, 1);
        _plot = PlotArea.FromInset(Width, Height, Padding);
        RebuildChartState();
    }

    private BarLayoutOptions LayoutOptions => new(
        Horizontal,
        Math.Clamp(BarWidthRatio, 0.1, 1.0),
        Math.Clamp(GroupSpacing, 0, 0.8),
        Math.Clamp(SeriesSpacing, 0, 0.8));

    private void RebuildChartState()
    {
        var items = Items ?? Array.Empty<TItem>();
        var options = LayoutOptions;
        var grouped = new List<IReadOnlyList<double>>(items.Count);
        foreach (var item in items)
        {
            var itemSegments = SegmentsSelectorOrThrow(item) ?? Array.Empty<TSegment>();
            var values = new double[itemSegments.Count];
            for (var i = 0; i < itemSegments.Count; i++)
            {
                values[i] = ChartValues.Sanitize(SegmentValueSelectorOrThrow(itemSegments[i]));
            }

            grouped.Add(values);
        }

        var scale = BuildValueScale(grouped);
        var maxValue = scale.Max;
        var groups = new List<CategoricalBarGroup<TItem, TSegment>>(items.Count);
        var segments = new List<CategoricalBarSegment<TItem, TSegment>>();
        var segmentIndexByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        var legendMap = new Dictionary<string, CategoricalBarLegendItem>(StringComparer.Ordinal);

        _computedMaxValue = maxValue;
        _valueAxisTicks = scale.Ticks;

        for (var groupIndex = 0; groupIndex < items.Count; groupIndex++)
        {
            var item = items[groupIndex];
            var label = LabelSelectorOrThrow(item);
            var itemSegments = SegmentsSelectorOrThrow(item) ?? Array.Empty<TSegment>();
            var sanitizedValues = grouped[groupIndex];
            var totalValue = 0d;
            foreach (var value in sanitizedValues)
            {
                totalValue += value;
            }

            var groupBounds = Layout.GetGroupBounds(groupIndex, items.Count, _plot, options);
            var hoverRect = GetGroupHoverBounds(groupIndex, items.Count);

            // Legend membership: iterate ALL declared segments so every series is registered
            // even when its value is zero (unifies on the former stacked behavior).
            var visibleEntries = new List<VisibleSegment>(itemSegments.Count);
            for (var segmentIndex = 0; segmentIndex < itemSegments.Count; segmentIndex++)
            {
                var segment = itemSegments[segmentIndex];
                var segmentLabel = SegmentLabelSelectorOrThrow(segment);
                var value = sanitizedValues[segmentIndex];
                var fill = legendMap.TryGetValue(segmentLabel, out var existingLegendItem)
                    ? existingLegendItem.Fill
                    : SegmentColorSelector?.Invoke(segment) ?? DefaultPalette[legendMap.Count % DefaultPalette.Length];

                if (!legendMap.ContainsKey(segmentLabel))
                {
                    legendMap[segmentLabel] = new CategoricalBarLegendItem(segmentLabel, fill);
                }

                if (value <= 0 || maxValue <= 0)
                {
                    continue;
                }

                var hoverFill = SegmentHoverColorSelector?.Invoke(segment) ?? ChartColor.DarkenByFactor(fill);
                visibleEntries.Add(new VisibleSegment(segment, segmentIndex, segmentLabel, value, fill, hoverFill));
            }

            var visibleValues = new double[visibleEntries.Count];
            for (var i = 0; i < visibleEntries.Count; i++)
            {
                visibleValues[i] = visibleEntries[i].Value;
            }

            var rects = Layout.LayoutSegments(groupBounds, visibleValues, maxValue, _plot, options);
            var groupSegments = new List<CategoricalBarSegment<TItem, TSegment>>(visibleEntries.Count);

            for (var visibleIndex = 0; visibleIndex < visibleEntries.Count; visibleIndex++)
            {
                var entry = visibleEntries[visibleIndex];
                var rect = rects[visibleIndex];
                var accessibleLabel = $"{label}, {entry.Label}: {entry.Value.ToString(ValueFormat, CultureInfo.InvariantCulture)}";
                var segment = new CategoricalBarSegment<TItem, TSegment>(
                    item,
                    entry.Segment,
                    groupIndex,
                    entry.Index,
                    label,
                    entry.Label,
                    entry.Value,
                    totalValue,
                    entry.Fill,
                    entry.HoverFill,
                    rect,
                    accessibleLabel,
                    false,
                    false,
                    false);
                segment = segment with { IsSelected = IsSegmentSelected?.Invoke(segment) ?? false };

                segments.Add(segment);
                segmentIndexByKey[segment.Key] = segments.Count - 1;
                groupSegments.Add(segment);
            }

            groups.Add(new CategoricalBarGroup<TItem, TSegment>(
                groupIndex,
                item,
                label,
                totalValue,
                groupBounds,
                hoverRect,
                Horizontal
                    ? new SvgPoint(_plot.Left - 8, groupBounds.Y + groupBounds.Height / 2)
                    : new SvgPoint(groupBounds.X + groupBounds.Width / 2, _plot.Bottom + 20),
                new ReadOnlyCollection<CategoricalBarSegment<TItem, TSegment>>(groupSegments)));
        }

        _groups = new ReadOnlyCollection<CategoricalBarGroup<TItem, TSegment>>(groups);
        _segments = new ReadOnlyCollection<CategoricalBarSegment<TItem, TSegment>>(segments);
        _segmentIndexByKey = segmentIndexByKey;
        _legendItems = new ReadOnlyCollection<CategoricalBarLegendItem>(legendMap.Values.ToList());
        _interaction.SetElements(_segments);

        if (_hoveredGroupIndex is int hoveredGroup &&
            !_groups.Any(group => group.Index == hoveredGroup && group.Segments.Count > 0))
        {
            _hoveredGroupIndex = null;
        }

        if (_hoveredLegendLabel is not null && !_legendItems.Any(item => item.Label == _hoveredLegendLabel))
        {
            _hoveredLegendLabel = null;
        }
    }

    private SvgRect GetGroupHoverBounds(int groupIndex, int itemCount)
    {
        if (itemCount <= 0)
        {
            return new SvgRect(0, 0, 0, 0);
        }

        if (Horizontal)
        {
            var step = _plot.Height / itemCount;
            return new SvgRect(_plot.Left, _plot.Top + groupIndex * step, _plot.Width, step);
        }

        var widthStep = _plot.Width / itemCount;
        return new SvgRect(_plot.Left + groupIndex * widthStep, _plot.Top, widthStep, _plot.Height);
    }

    private Task OnPlotAreaChanged(PlotArea plot)
    {
        _plot = plot;
        RebuildChartState();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Individual segment mouse-out is suppressed while the shared tooltip is active: the group-level
    /// rollup (<see cref="HandleGroupLeaveAsync"/>) owns clearing hover in that mode.
    /// </summary>
    private Task HandleSegmentHoverLeaveAsync(CategoricalBarSegment<TItem, TSegment> segment) =>
        UsesSharedTooltip ? Task.CompletedTask : _interaction.HoverLeaveAsync(segment);

    private async Task HandleGroupEnterAsync(CategoricalBarGroup<TItem, TSegment> group)
    {
        if (!UsesSharedTooltip || group.Segments.Count == 0 || _hoveredGroupIndex == group.Index)
        {
            return;
        }

        _hoveredGroupIndex = group.Index;

        if (HoveredSegment is not null && HoveredSegment.GroupIndex != group.Index)
        {
            await _interaction.HoverLeaveKeyAsync(_interaction.HoveredKey!.Value);
        }

        await RefreshChartAsync();
    }

    private async Task HandleGroupLeaveAsync(CategoricalBarGroup<TItem, TSegment> group)
    {
        if (!UsesSharedTooltip || _hoveredGroupIndex != group.Index)
        {
            return;
        }

        _hoveredGroupIndex = null;

        if (HoveredSegment?.GroupIndex == group.Index)
        {
            await _interaction.HoverLeaveKeyAsync(_interaction.HoveredKey!.Value);
        }

        await RefreshChartAsync();
    }

    private async Task HandleLegendEnter(string label)
    {
        if (string.Equals(_hoveredLegendLabel, label, StringComparison.Ordinal))
        {
            return;
        }

        _hoveredLegendLabel = label;
        await InvokeAsync(StateHasChanged);
    }

    private Task HandleLegendLeave()
    {
        if (_hoveredLegendLabel is null)
        {
            return Task.CompletedTask;
        }

        _hoveredLegendLabel = null;
        return InvokeAsync(StateHasChanged);
    }

    private async Task RefreshChartAsync()
        => await InvokeAsync(StateHasChanged);

    private SvgPoint GetTooltipAnchor(CategoricalBarSegment<TItem, TSegment> segment)
    {
        if (Horizontal)
        {
            return new SvgPoint(
                segment.Rect.X + segment.Rect.Width / 2,
                segment.Rect.Y + segment.Rect.Height / 2);
        }

        if (ConstrainTooltipToChartBounds)
        {
            var cornerY = segment.Rect.Y + Math.Min(segment.Rect.Height * 0.35, 14);
            return new SvgPoint(segment.Rect.X + segment.Rect.Width, Math.Max(cornerY, 8));
        }

        var left = segment.Rect.X + segment.Rect.Width / 2;
        var top = Math.Max(segment.Rect.Y - 12, 8);

        return new(left, top);
    }

    private string GetTooltipStyle(CategoricalBarSegment<TItem, TSegment> segment)
    {
        var anchor = GetTooltipAnchor(segment);
        return $"left: {Fmt(anchor.X)}px; top: {Fmt(anchor.Y)}px;";
    }

    private string GetTooltipStyle(CategoricalBarTooltipContext<TItem, TSegment> tooltip) =>
        $"left: {Fmt(tooltip.Anchor.X)}px; top: {Fmt(tooltip.Anchor.Y)}px;";

    private string GetSegmentClasses(CategoricalBarSegment<TItem, TSegment> segment)
    {
        var classes = new List<string> { $"{CssPrefix}-segment" };
        if (IsHovered(segment)) classes.Add("is-hovered");
        if (IsFocused(segment)) classes.Add("is-focused");
        if (IsSelected(segment)) classes.Add("is-selected");
        if (IsLegendMatch(segment.SegmentLabel)) classes.Add("is-legend-active");
        return string.Join(" ", classes);
    }

    private CategoricalBarTooltipContext<TItem, TSegment> CreateSharedTooltipContext(CategoricalBarGroup<TItem, TSegment> group)
    {
        var activeSegment = TooltipActiveSegment;
        var rows = group.Segments
            .Select(segment => new CategoricalBarTooltipRow<TItem, TSegment>(
                segment.Item,
                segment.Segment,
                segment.GroupIndex,
                segment.SegmentIndex,
                segment.GroupLabel,
                segment.SegmentLabel,
                segment.Value,
                group.TotalValue,
                segment.Fill,
                string.Equals(segment.Key, activeSegment?.Key, StringComparison.Ordinal)))
            .ToList();

        return new CategoricalBarTooltipContext<TItem, TSegment>(
            group.Item,
            group.Index,
            group.Label,
            group.TotalValue,
            rows,
            activeSegment,
            GetSharedTooltipAnchor(group),
            Horizontal);
    }

    private SvgPoint GetSharedTooltipAnchor(CategoricalBarGroup<TItem, TSegment> group)
    {
        if (group.Segments.Count == 0)
        {
            return new SvgPoint(group.HoverRect.X + group.HoverRect.Width / 2, group.HoverRect.Y);
        }

        if (Horizontal)
        {
            var right = group.Segments.Max(segment => segment.Rect.X + segment.Rect.Width);
            var centerY = group.HoverRect.Y + group.HoverRect.Height / 2;
            return new SvgPoint(Math.Min(right + 12, _plot.Right - 8), centerY);
        }

        if (ConstrainTooltipToChartBounds)
        {
            var activeSegment = TooltipActiveSegment;
            if (activeSegment is not null)
            {
                return GetTooltipAnchor(activeSegment);
            }

            var right = group.HoverRect.X + group.HoverRect.Width;
            var cornerTop = group.Segments.Min(segment => segment.Rect.Y);
            return new SvgPoint(
                Math.Min(right, _plot.Right - 8),
                Math.Max(cornerTop + 12, 8));
        }

        var left = group.HoverRect.X + group.HoverRect.Width / 2;
        var top = group.Segments.Min(segment => segment.Rect.Y);
        return new SvgPoint(left, Math.Max(top - 12, 8));
    }

    private ChartTooltipPlacement GetSegmentTooltipPlacement() =>
        Horizontal || ConstrainTooltipToChartBounds
            ? ChartTooltipPlacement.Right
            : ChartTooltipPlacement.Above;

    private string GetSegmentTooltipLegacyPlacementClass() =>
        Horizontal
            ? "chart-tooltip--side"
            : ConstrainTooltipToChartBounds
                ? "chart-tooltip--corner"
                : "chart-tooltip--top";

    private ChartTooltipPlacement GetSharedTooltipPlacement(bool horizontalAnchor) =>
        horizontalAnchor || ConstrainTooltipToChartBounds
            ? ChartTooltipPlacement.Right
            : ChartTooltipPlacement.Above;

    private string GetSharedTooltipLegacyPlacementClass(bool horizontalAnchor) =>
        horizontalAnchor
            ? "chart-tooltip--side"
            : ConstrainTooltipToChartBounds
                ? "chart-tooltip--corner"
                : "chart-tooltip--top";

    private string GetLegendItemClasses(CategoricalBarLegendItem item)
    {
        var classes = new List<string> { $"{CssPrefix}-bar-legend__item" };
        if (IsLegendMatch(item.Label)) classes.Add("is-active");
        return string.Join(" ", classes);
    }

    private bool IsLegendMatch(string label) =>
        !string.IsNullOrWhiteSpace(_hoveredLegendLabel) &&
        string.Equals(_hoveredLegendLabel, label, StringComparison.Ordinal);

    private int FindSegmentIndex(CategoricalBarSegment<TItem, TSegment> segment)
        => _segmentIndexByKey.GetValueOrDefault(segment.Key, -1);

    private IEnumerable<string> GetShellClasses()
    {
        yield return ShellRootCssClass;
        yield return LegendPlacement switch
        {
            ChartLegendPlacement.Top => "legend-top",
            ChartLegendPlacement.Bottom => "legend-bottom",
            ChartLegendPlacement.Left => "legend-left",
            ChartLegendPlacement.Right => "legend-right",
            ChartLegendPlacement.Start => "legend-start",
            ChartLegendPlacement.End => "legend-end",
            _ => "legend-bottom"
        };

        if (HasLegendHover)
        {
            yield return "has-legend-hover";
        }
    }

    private bool IsSelected(CategoricalBarSegment<TItem, TSegment> segment) =>
        IsSegmentSelected?.Invoke(segment) ?? false;

    private bool IsHovered(CategoricalBarSegment<TItem, TSegment> segment) =>
        UsesSharedTooltip
            ? TooltipGroupIndex == segment.GroupIndex
            : _interaction.IsHovered(segment);

    private bool IsFocused(CategoricalBarSegment<TItem, TSegment> segment) =>
        _interaction.IsFocused(segment);

    private IReadOnlyList<TSegment> SegmentsSelectorOrThrow(TItem item) => SegmentsSelector!(item);

    private string LabelSelectorOrThrow(TItem item) => LabelSelector!(item);

    private double SegmentValueSelectorOrThrow(TSegment segment) => SegmentValueSelector!(segment);

    private string SegmentLabelSelectorOrThrow(TSegment segment) => SegmentLabelSelector!(segment);

    private static string Fmt(double value) => ChartFormat.Fmt(value);

    private AxisScale BuildValueScale(IReadOnlyList<IReadOnlyList<double>> grouped)
    {
        var rawMax = Layout.ComputeRawMaxValue(grouped);
        var scaleValues = grouped.Count > 0 ? new[] { rawMax } : Array.Empty<double>();
        var (pixelStart, pixelEnd) = Horizontal
            ? (_plot.Left, _plot.Right)
            : (_plot.Bottom, _plot.Top);

        return AxisScale.FromValues(scaleValues, pixelStart, pixelEnd, new AxisScaleOptions
        {
            TickCount = SafeGridLineCount,
            Baseline = AxisBaseline.IncludeZero,
            ForcedMax = MaxValue is > 0 && double.IsFinite(MaxValue.Value) ? MaxValue : null,
            EmptyFallbackMax = 100
        });
    }

    private readonly record struct VisibleSegment(
        TSegment Segment,
        int Index,
        string Label,
        double Value,
        string Fill,
        string HoverFill);
}
