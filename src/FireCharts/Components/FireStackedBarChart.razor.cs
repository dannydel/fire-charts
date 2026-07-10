using FireCharts.Interaction;
using FireCharts.Models;
using Microsoft.AspNetCore.Components;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FireCharts.Components;

public partial class FireStackedBarChart<TItem, TSegment> : ComponentBase
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

    private IReadOnlyList<StackedBarChartSegment<TItem, TSegment>> _segments = Array.Empty<StackedBarChartSegment<TItem, TSegment>>();
    private IReadOnlyList<BarState> _bars = Array.Empty<BarState>();
    private IReadOnlyList<LegendItem> _legendItems = Array.Empty<LegendItem>();
    private Dictionary<string, int> _segmentIndexByKey = [];
    private ChartInteraction<StackedBarChartSegment<TItem, TSegment>, int> _interaction = default!;
    private double _computedMaxValue = 100;
    private int? _hoveredBarIndex;
    private double? _renderWidth;
    private double? _renderHeight;
    private string? _hoveredLegendLabel;

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

    private double PaddingTop => 10;
    private double PaddingRight => 10;
    private double PaddingBottom => ShowAxisLabels ? 40 : 10;
    private double PaddingLeft => ShowAxisLabels ? (Horizontal ? 90 : 50) : 10;

    internal IReadOnlyList<StackedBarChartSegment<TItem, TSegment>> Segments => _segments;
    internal IReadOnlyList<BarState> Bars => _bars;
    internal IReadOnlyList<LegendItem> LegendItems => _legendItems;
    internal StackedBarChartSegment<TItem, TSegment>? HoveredSegment => _interaction.Hovered;
    internal bool UsesSharedTooltip => TooltipInteractionMode == BarTooltipInteractionMode.Shared;
    internal double SafeWidth => Math.Max(_renderWidth ?? Width, 1);
    internal double SafeHeight => Math.Max(_renderHeight ?? Height, 1);
    internal double ChartAreaLeft => PaddingLeft;
    internal double ChartAreaTop => PaddingTop;
    internal double ChartAreaRight => SafeWidth - PaddingRight;
    internal double ChartAreaBottom => SafeHeight - PaddingBottom;
    internal double ChartAreaWidth => Math.Max(ChartAreaRight - ChartAreaLeft, 1);
    internal double ChartAreaHeight => Math.Max(ChartAreaBottom - ChartAreaTop, 1);
    internal int SafeGridLineCount => Math.Max(GridLineCount, 1);
    internal bool HasLegendHover => !string.IsNullOrWhiteSpace(_hoveredLegendLabel);
    internal bool ShouldRenderLegendBeforeChart => LegendPlacement is ChartLegendPlacement.Top or ChartLegendPlacement.Left or ChartLegendPlacement.Start;
    internal string ShellCssClasses => string.Join(" ", GetShellClasses());
    internal int? TooltipBarIndex => UsesSharedTooltip
        ? _hoveredBarIndex ?? HoveredSegment?.BarIndex ?? FocusedSegment?.BarIndex
        : HoveredSegment?.BarIndex;
    internal BarState? TooltipBar => TooltipBarIndex is int index
        ? index >= 0 && index < _bars.Count ? _bars[index] : null
        : null;
    internal StackedBarChartSegment<TItem, TSegment>? FocusedSegment => _interaction.Focused;
    internal StackedBarChartSegment<TItem, TSegment>? TooltipActiveSegment
    {
        get
        {
            var ownerIndex = TooltipBarIndex;
            if (ownerIndex is null)
            {
                return UsesSharedTooltip ? null : HoveredSegment;
            }

            if (HoveredSegment?.BarIndex == ownerIndex)
            {
                return HoveredSegment;
            }

            return FocusedSegment?.BarIndex == ownerIndex ? FocusedSegment : null;
        }
    }
    internal StackedBarTooltipContext<TItem, TSegment>? SharedTooltipContext =>
        !UsesSharedTooltip || TooltipBar is null
            ? null
            : CreateSharedTooltipContext(TooltipBar);
    internal StackedBarChartContext<TItem, TSegment> ChartContext =>
        new(
            SafeWidth,
            SafeHeight,
            ChartAreaLeft,
            ChartAreaTop,
            ChartAreaRight,
            ChartAreaBottom,
            ChartAreaWidth,
            ChartAreaHeight,
            Horizontal,
            ComputedMaxValue,
            Segments);

    internal double ComputedMaxValue => _computedMaxValue;

    protected override void OnInitialized()
    {
        _interaction = new ChartInteraction<StackedBarChartSegment<TItem, TSegment>, int>(
            new ChartInteractionOptions<StackedBarChartSegment<TItem, TSegment>, int>
            {
                KeySelector = FindSegmentIndex,
                RequestRender = () => InvokeAsync(StateHasChanged),
                OnActiveChanged = segment => OnPointHoverChanged.InvokeAsync(ToInteraction(segment)),
                OnActivate = async segment =>
                {
                    SelectedItem = segment.Item;
                    await InvokeAsync(StateHasChanged);
                    await SelectedItemChanged.InvokeAsync(segment.Item);
                    await OnPointClick.InvokeAsync(ToInteraction(segment));
                }
            });
    }

    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(SegmentsSelector);
        ArgumentNullException.ThrowIfNull(LabelSelector);
        ArgumentNullException.ThrowIfNull(SegmentValueSelector);
        ArgumentNullException.ThrowIfNull(SegmentLabelSelector);

        Width = Math.Max(Width, 1);
        Height = Math.Max(Height, 1);
        _renderWidth ??= Width;
        _renderHeight ??= Height;
        RebuildChartState();
    }

    private void RebuildChartState()
    {
        var items = Items ?? Array.Empty<TItem>();
        var comparer = EqualityComparer<TItem>.Default;
        var selectedItem = SelectedItem;
        var maxValue = ResolveComputedMaxValue(items);
        var safeBarWidthRatio = Math.Clamp(BarWidthRatio, 0.1, 1.0);
        var bars = new List<BarState>(items.Count);
        var segments = new List<StackedBarChartSegment<TItem, TSegment>>();
        var segmentIndexByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        var legendMap = new Dictionary<string, LegendItem>(StringComparer.Ordinal);

        _computedMaxValue = maxValue;

        for (var barIndex = 0; barIndex < items.Count; barIndex++)
        {
            var item = items[barIndex];
            var label = LabelSelectorOrThrow(item);
            var isSelected = comparer.Equals(item, selectedItem);
            var itemSegments = SegmentsSelectorOrThrow(item) ?? Array.Empty<TSegment>();
            var sanitizedValues = new double[itemSegments.Count];
            var totalValue = 0d;

            for (var segmentIndex = 0; segmentIndex < itemSegments.Count; segmentIndex++)
            {
                var value = SanitizeValue(SegmentValueSelectorOrThrow(itemSegments[segmentIndex]));
                sanitizedValues[segmentIndex] = value;
                totalValue += value;
            }

            var barRect = GetBarBounds(barIndex, items.Count, safeBarWidthRatio);
            var hoverRect = GetBarHoverBounds(barIndex, items.Count);
            var barSegments = new List<StackedBarChartSegment<TItem, TSegment>>();

            var runningOffset = 0d;
            for (var segmentIndex = 0; segmentIndex < itemSegments.Count; segmentIndex++)
            {
                var segment = itemSegments[segmentIndex];
                var value = sanitizedValues[segmentIndex];
                var segmentLabel = SegmentLabelSelectorOrThrow(segment);
                var fill = legendMap.TryGetValue(segmentLabel, out var existingLegendItem)
                    ? existingLegendItem.Fill
                    : SegmentColorSelector?.Invoke(segment) ?? DefaultPalette[legendMap.Count % DefaultPalette.Length];
                var hoverFill = SegmentHoverColorSelector?.Invoke(segment) ?? Darken(fill);

                if (!legendMap.ContainsKey(segmentLabel))
                {
                    legendMap[segmentLabel] = new LegendItem(segmentLabel, fill);
                }

                if (value <= 0 || maxValue <= 0)
                {
                    continue;
                }

                var rect = GetSegmentRect(barRect, runningOffset, value, maxValue);
                var chartSegment = new StackedBarChartSegment<TItem, TSegment>(
                    item,
                    segment,
                    barIndex,
                    segmentIndex,
                    label,
                    segmentLabel,
                    value,
                    totalValue,
                    fill,
                    hoverFill,
                    rect,
                    $"{label}, {segmentLabel}: {value.ToString(ValueFormat, CultureInfo.InvariantCulture)}",
                    isSelected,
                    false,
                    false);

                segments.Add(chartSegment);
                segmentIndexByKey[chartSegment.Key] = segments.Count - 1;
                barSegments.Add(chartSegment);
                runningOffset += value;
            }

            bars.Add(new BarState(
                barIndex,
                item,
                label,
                totalValue,
                barRect,
                hoverRect,
                Horizontal
                    ? new SvgPoint(ChartAreaLeft - 8, barRect.Y + barRect.Height / 2)
                    : new SvgPoint(barRect.X + barRect.Width / 2, ChartAreaBottom + 20),
                new ReadOnlyCollection<StackedBarChartSegment<TItem, TSegment>>(barSegments)));
        }

        _bars = new ReadOnlyCollection<BarState>(bars);
        _segments = new ReadOnlyCollection<StackedBarChartSegment<TItem, TSegment>>(segments);
        _segmentIndexByKey = segmentIndexByKey;
        _legendItems = new ReadOnlyCollection<LegendItem>(legendMap.Values.ToList());
        _interaction.SetElements(_segments);

        if (_hoveredBarIndex is int hoveredBar && !_bars.Any(bar => bar.Index == hoveredBar && bar.Segments.Count > 0))
        {
            _hoveredBarIndex = null;
        }

        if (_hoveredLegendLabel is not null && !_legendItems.Any(item => item.Label == _hoveredLegendLabel))
        {
            _hoveredLegendLabel = null;
        }
    }

    private SvgRect GetBarHoverBounds(int barIndex, int itemCount)
    {
        if (itemCount <= 0)
        {
            return new SvgRect(0, 0, 0, 0);
        }

        if (Horizontal)
        {
            var step = ChartAreaHeight / itemCount;
            return new SvgRect(ChartAreaLeft, ChartAreaTop + barIndex * step, ChartAreaWidth, step);
        }

        var widthStep = ChartAreaWidth / itemCount;
        return new SvgRect(ChartAreaLeft + barIndex * widthStep, ChartAreaTop, widthStep, ChartAreaHeight);
    }

    private SvgRect GetBarBounds(int barIndex, int itemCount, double safeBarWidthRatio)
    {
        if (itemCount <= 0)
        {
            return new SvgRect(0, 0, 0, 0);
        }

        if (Horizontal)
        {
            var step = ChartAreaHeight / itemCount;
            var barHeight = Math.Max(step * safeBarWidthRatio, 1);
            var y = ChartAreaTop + barIndex * step + (step - barHeight) / 2;
            return new SvgRect(ChartAreaLeft, y, ChartAreaWidth, barHeight);
        }

        var widthStep = ChartAreaWidth / itemCount;
        var barWidth = Math.Max(widthStep * safeBarWidthRatio, 1);
        var x = ChartAreaLeft + barIndex * widthStep + (widthStep - barWidth) / 2;
        return new SvgRect(x, ChartAreaTop, barWidth, ChartAreaHeight);
    }

    private SvgRect GetSegmentRect(SvgRect barRect, double runningOffset, double value, double maxValue)
    {
        var scale = maxValue > 0 ? value / maxValue : 0;
        var offsetScale = maxValue > 0 ? runningOffset / maxValue : 0;
        scale = Math.Clamp(scale, 0, 1);
        offsetScale = Math.Clamp(offsetScale, 0, 1);

        if (Horizontal)
        {
            var width = Math.Max(scale * ChartAreaWidth, 0);
            var x = ChartAreaLeft + offsetScale * ChartAreaWidth;
            return new SvgRect(x, barRect.Y, width, barRect.Height);
        }

        var height = Math.Max(scale * ChartAreaHeight, 0);
        var topOffset = offsetScale * ChartAreaHeight;
        var y = ChartAreaBottom - topOffset - height;
        return new SvgRect(barRect.X, y, barRect.Width, height);
    }

    private void UpdateSurfaceSize(ChartSurfaceContext surface)
    {
        var widthChanged = Math.Abs((_renderWidth ?? 0) - surface.Width) > 0.5;
        var heightChanged = Math.Abs((_renderHeight ?? 0) - surface.Height) > 0.5;
        if (!widthChanged && !heightChanged)
        {
            return;
        }

        _renderWidth = surface.Width;
        _renderHeight = surface.Height;
        RebuildChartState();
    }

    /// <summary>
    /// Individual segment mouse-out is suppressed while the shared tooltip is active: the bar-level
    /// rollup (<see cref="HandleBarLeaveAsync"/>) owns clearing hover in that mode.
    /// </summary>
    private Task HandleSegmentHoverLeaveAsync(StackedBarChartSegment<TItem, TSegment> segment) =>
        UsesSharedTooltip ? Task.CompletedTask : _interaction.HoverLeaveAsync(segment);

    private async Task HandleBarEnterAsync(BarState bar)
    {
        if (!UsesSharedTooltip || bar.Segments.Count == 0 || _hoveredBarIndex == bar.Index)
        {
            return;
        }

        _hoveredBarIndex = bar.Index;

        if (HoveredSegment is not null && HoveredSegment.BarIndex != bar.Index)
        {
            await _interaction.HoverLeaveKeyAsync(_interaction.HoveredKey!.Value);
        }

        await RefreshChartAsync();
    }

    private async Task HandleBarLeaveAsync(BarState bar)
    {
        if (!UsesSharedTooltip || _hoveredBarIndex != bar.Index)
        {
            return;
        }

        _hoveredBarIndex = null;

        if (HoveredSegment?.BarIndex == bar.Index)
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

    private SvgPoint GetTooltipAnchor(StackedBarChartSegment<TItem, TSegment> segment)
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

    private string GetTooltipStyle(StackedBarChartSegment<TItem, TSegment> segment)
    {
        var anchor = GetTooltipAnchor(segment);
        return $"left: {Fmt(anchor.X)}px; top: {Fmt(anchor.Y)}px;";
    }

    private string GetTooltipStyle(StackedBarTooltipContext<TItem, TSegment> tooltip) =>
        $"left: {Fmt(tooltip.Anchor.X)}px; top: {Fmt(tooltip.Anchor.Y)}px;";

    private string GetSegmentClasses(StackedBarChartSegment<TItem, TSegment> segment)
    {
        var classes = new List<string> { "stacked-segment" };
        if (IsHovered(segment)) classes.Add("is-hovered");
        if (IsFocused(segment)) classes.Add("is-focused");
        if (IsSelected(segment)) classes.Add("is-selected");
        if (IsLegendMatch(segment.SegmentLabel)) classes.Add("is-legend-active");
        return string.Join(" ", classes);
    }

    private StackedBarTooltipContext<TItem, TSegment> CreateSharedTooltipContext(BarState bar)
    {
        var activeSegment = TooltipActiveSegment;
        var rows = bar.Segments
            .Select(segment => new StackedBarTooltipRow<TItem, TSegment>(
                segment.Item,
                segment.Segment,
                segment.BarIndex,
                segment.SegmentIndex,
                segment.BarLabel,
                segment.SegmentLabel,
                segment.Value,
                segment.TotalValue,
                segment.Fill,
                string.Equals(segment.Key, activeSegment?.Key, StringComparison.Ordinal)))
            .ToList();

        return new StackedBarTooltipContext<TItem, TSegment>(
            bar.Item,
            bar.Index,
            bar.Label,
            bar.TotalValue,
            rows,
            activeSegment,
            GetSharedTooltipAnchor(bar),
            Horizontal);
    }

    private SvgPoint GetSharedTooltipAnchor(BarState bar)
    {
        if (bar.Segments.Count == 0)
        {
            return new SvgPoint(bar.HoverRect.X + bar.HoverRect.Width / 2, bar.HoverRect.Y);
        }

        if (Horizontal)
        {
            var right = bar.Segments.Max(segment => segment.Rect.X + segment.Rect.Width);
            var centerY = bar.HoverRect.Y + bar.HoverRect.Height / 2;
            return new SvgPoint(Math.Min(right + 12, ChartAreaRight - 8), centerY);
        }

        if (ConstrainTooltipToChartBounds)
        {
            var activeSegment = TooltipActiveSegment;
            if (activeSegment is not null)
            {
                return GetTooltipAnchor(activeSegment);
            }

            var right = bar.HoverRect.X + bar.HoverRect.Width;
            var cornerTop = bar.Segments.Min(segment => segment.Rect.Y);
            return new SvgPoint(
                Math.Min(right, ChartAreaRight - 8),
                Math.Max(cornerTop + 12, 8));
        }

        var left = bar.HoverRect.X + bar.HoverRect.Width / 2;
        var top = bar.Segments.Min(segment => segment.Rect.Y);
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

    private string GetLegendItemClasses(LegendItem item)
    {
        var classes = new List<string> { "stacked-bar-legend__item" };
        if (IsLegendMatch(item.Label)) classes.Add("is-active");
        return string.Join(" ", classes);
    }

    private bool IsLegendMatch(string label) =>
        !string.IsNullOrWhiteSpace(_hoveredLegendLabel) &&
        string.Equals(_hoveredLegendLabel, label, StringComparison.Ordinal);

    private int FindSegmentIndex(StackedBarChartSegment<TItem, TSegment> segment)
        => _segmentIndexByKey.GetValueOrDefault(segment.Key, -1);

    private IEnumerable<string> GetShellClasses()
    {
        yield return "fire-stacked-bar-chart-shell";
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

    private ChartPointInteraction<TItem> ToInteraction(StackedBarChartSegment<TItem, TSegment> segment) =>
        new(segment.Item, segment.BarIndex, $"{segment.BarLabel} - {segment.SegmentLabel}", segment.Value);

    private bool IsSelected(StackedBarChartSegment<TItem, TSegment> segment) =>
        EqualityComparer<TItem>.Default.Equals(segment.Item, SelectedItem);

    private bool IsHovered(StackedBarChartSegment<TItem, TSegment> segment) =>
        UsesSharedTooltip
            ? TooltipBarIndex == segment.BarIndex
            : _interaction.IsHovered(segment);

    private bool IsFocused(StackedBarChartSegment<TItem, TSegment> segment) =>
        _interaction.IsFocused(segment);

    private IReadOnlyList<TSegment> SegmentsSelectorOrThrow(TItem item) => SegmentsSelector!(item);

    private string LabelSelectorOrThrow(TItem item) => LabelSelector!(item);

    private double SegmentValueSelectorOrThrow(TSegment segment) => SegmentValueSelector!(segment);

    private string SegmentLabelSelectorOrThrow(TSegment segment) => SegmentLabelSelector!(segment);

    private static double SanitizeValue(double value) =>
        double.IsFinite(value) ? Math.Max(value, 0) : 0;

    private static string Fmt(double value) =>
        double.IsFinite(value)
            ? value.ToString("F1", CultureInfo.InvariantCulture)
            : "0.0";

    private static double GetNiceMax(double max)
    {
        if (max <= 0 || !double.IsFinite(max))
        {
            return 100;
        }

        var log = Math.Log10(max);
        if (!double.IsFinite(log))
        {
            return 100;
        }

        var magnitude = Math.Pow(10, Math.Floor(log));
        if (magnitude <= 0 || !double.IsFinite(magnitude))
        {
            return 100;
        }

        var normalized = max / magnitude;

        double nice;
        if (normalized <= 1) nice = 1;
        else if (normalized <= 1.5) nice = 1.5;
        else if (normalized <= 2) nice = 2;
        else if (normalized <= 3) nice = 3;
        else if (normalized <= 5) nice = 5;
        else if (normalized <= 7.5) nice = 7.5;
        else nice = 10;

        var result = nice * magnitude;
        return double.IsFinite(result) && result > 0 ? result : 100;
    }

    private static string Darken(string hex)
    {
        if (hex.Length != 7 || !hex.StartsWith('#'))
        {
            return hex;
        }

        if (!int.TryParse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) ||
            !int.TryParse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) ||
            !int.TryParse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return hex;
        }

        return $"#{Math.Max((int)(r * 0.78), 0):X2}{Math.Max((int)(g * 0.78), 0):X2}{Math.Max((int)(b * 0.78), 0):X2}";
    }

    private double ResolveComputedMaxValue(IReadOnlyList<TItem> items)
    {
        if (MaxValue.HasValue && MaxValue.Value > 0 && double.IsFinite(MaxValue.Value))
        {
            return MaxValue.Value;
        }

        var max = items
            .Select(item => (SegmentsSelectorOrThrow(item) ?? Array.Empty<TSegment>())
                .Select(SegmentValueSelectorOrThrow)
                .Select(SanitizeValue)
                .Sum())
            .DefaultIfEmpty(0)
            .Max();

        return GetNiceMax(max);
    }

    internal sealed record BarState(
        int Index,
        TItem Item,
        string Label,
        double TotalValue,
        SvgRect Rect,
        SvgRect HoverRect,
        SvgPoint AxisLabelPoint,
        IReadOnlyList<StackedBarChartSegment<TItem, TSegment>> Segments);

    internal sealed record LegendItem(
        string Label,
        string Fill);
}
