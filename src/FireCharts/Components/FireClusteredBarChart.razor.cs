using System.Collections.ObjectModel;
using System.Globalization;
using FireCharts.Models;
using FireCharts.Scales;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FireCharts.Components;

public partial class FireClusteredBarChart<TItem, TSegment> : ComponentBase
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

    private IReadOnlyList<ClusteredBarChartSegment<TItem, TSegment>> _segments = Array.Empty<ClusteredBarChartSegment<TItem, TSegment>>();
    private IReadOnlyList<CategoryState> _categories = Array.Empty<CategoryState>();
    private IReadOnlyList<LegendItem> _legendItems = Array.Empty<LegendItem>();
    private Dictionary<string, int> _segmentIndexByKey = [];
    private IReadOnlyList<ScaleTick> _valueAxisTicks = Array.Empty<ScaleTick>();
    private double _computedMaxValue = 100;
    private int? _hoveredCategoryIndex;
    private int? _hoveredSegmentIndex;
    private int? _focusedSegmentIndex;
    private PlotArea _plot;
    private string? _hoveredLegendLabel;

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

    private ChartPadding Padding => new(
        Top: 10,
        Right: 10,
        Bottom: ShowAxisLabels ? 40 : 10,
        Left: ShowAxisLabels ? (Horizontal ? 90 : 50) : 10);

    internal IReadOnlyList<ClusteredBarChartSegment<TItem, TSegment>> Segments => _segments;
    internal IReadOnlyList<CategoryState> Categories => _categories;
    internal IReadOnlyList<LegendItem> LegendItems => _legendItems;
    internal ClusteredBarChartSegment<TItem, TSegment>? HoveredSegment => _hoveredSegmentIndex is int index && index >= 0 && index < _segments.Count ? _segments[index] : null;
    internal ClusteredBarChartSegment<TItem, TSegment>? FocusedSegment => _focusedSegmentIndex is int index && index >= 0 && index < _segments.Count ? _segments[index] : null;
    internal bool UsesSharedTooltip => TooltipInteractionMode == BarTooltipInteractionMode.Shared;
    internal int SafeGridLineCount => Math.Max(GridLineCount, 1);
    internal bool HasLegendHover => !string.IsNullOrWhiteSpace(_hoveredLegendLabel);
    internal bool ShouldRenderLegendBeforeChart => LegendPlacement is ChartLegendPlacement.Top or ChartLegendPlacement.Left or ChartLegendPlacement.Start;
    internal string ShellCssClasses => string.Join(" ", GetShellClasses());
    internal int? TooltipCategoryIndex => UsesSharedTooltip
        ? _hoveredCategoryIndex ?? HoveredSegment?.CategoryIndex ?? FocusedSegment?.CategoryIndex
        : HoveredSegment?.CategoryIndex;
    internal CategoryState? TooltipCategory => TooltipCategoryIndex is int index
        ? index >= 0 && index < _categories.Count ? _categories[index] : null
        : null;
    internal ClusteredBarChartSegment<TItem, TSegment>? TooltipActiveSegment
    {
        get
        {
            var ownerIndex = TooltipCategoryIndex;
            if (ownerIndex is null)
            {
                return UsesSharedTooltip ? null : HoveredSegment;
            }

            if (HoveredSegment?.CategoryIndex == ownerIndex)
            {
                return HoveredSegment;
            }

            return FocusedSegment?.CategoryIndex == ownerIndex ? FocusedSegment : null;
        }
    }
    internal ClusteredBarTooltipContext<TItem, TSegment>? SharedTooltipContext =>
        !UsesSharedTooltip || TooltipCategory is null
            ? null
            : CreateSharedTooltipContext(TooltipCategory);
    internal ClusteredBarChartContext<TItem, TSegment> ChartContext =>
        new(
            _plot,
            Horizontal,
            ComputedMaxValue,
            Segments);

    internal double ComputedMaxValue => _computedMaxValue;
    internal IReadOnlyList<ScaleTick> ValueAxisTicks => _valueAxisTicks;

    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(SegmentsSelector);
        ArgumentNullException.ThrowIfNull(LabelSelector);
        ArgumentNullException.ThrowIfNull(SegmentValueSelector);
        ArgumentNullException.ThrowIfNull(SegmentLabelSelector);

        Width = Math.Max(Width, 1);
        Height = Math.Max(Height, 1);
        _plot = PlotArea.FromInset(Width, Height, Padding);
        RebuildChartState();
    }

    private void RebuildChartState()
    {
        var items = Items ?? Array.Empty<TItem>();
        var scale = BuildValueScale(items);
        var maxValue = scale.Max;
        var safeBarWidthRatio = Math.Clamp(BarWidthRatio, 0.1, 1.0);
        var safeGroupSpacing = Math.Clamp(GroupSpacing, 0, 0.8);
        var safeSeriesSpacing = Math.Clamp(SeriesSpacing, 0, 0.8);
        var selectedKey = SelectedSegment?.Key;
        var categories = new List<CategoryState>(items.Count);
        var segments = new List<ClusteredBarChartSegment<TItem, TSegment>>();
        var segmentIndexByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        var legendMap = new Dictionary<string, LegendItem>(StringComparer.Ordinal);

        _computedMaxValue = maxValue;
        _valueAxisTicks = scale.Ticks;

        for (var categoryIndex = 0; categoryIndex < items.Count; categoryIndex++)
        {
            var item = items[categoryIndex];
            var label = LabelSelectorOrThrow(item);
            var itemSegments = SegmentsSelectorOrThrow(item) ?? Array.Empty<TSegment>();
            var visibleSegments = new List<SegmentLayout>(itemSegments.Count);
            for (var segmentIndex = 0; segmentIndex < itemSegments.Count; segmentIndex++)
            {
                var segment = itemSegments[segmentIndex];
                var value = ChartValues.Sanitize(SegmentValueSelectorOrThrow(segment));
                if (value <= 0)
                {
                    continue;
                }

                visibleSegments.Add(new SegmentLayout(
                    segment,
                    segmentIndex,
                    SegmentLabelSelectorOrThrow(segment),
                    value));
            }

            var clusterRect = GetClusterBounds(categoryIndex, items.Count, safeBarWidthRatio, safeGroupSpacing);
            var hoverRect = GetCategoryHoverBounds(categoryIndex, items.Count);
            var categorySegments = new List<ClusteredBarChartSegment<TItem, TSegment>>();

            if (visibleSegments.Count == 0)
            {
                categories.Add(new CategoryState(
                    categoryIndex,
                    item,
                    label,
                    clusterRect,
                    hoverRect,
                    Horizontal
                        ? new SvgPoint(_plot.Left - 8, clusterRect.Y + clusterRect.Height / 2)
                        : new SvgPoint(clusterRect.X + clusterRect.Width / 2, _plot.Bottom + 20),
                    new ReadOnlyCollection<ClusteredBarChartSegment<TItem, TSegment>>(categorySegments)));
                continue;
            }

            var seriesSpacingPixels = GetSeriesSpacing(clusterRect, visibleSegments.Count, safeSeriesSpacing, Horizontal);
            var segmentThickness = GetSegmentThickness(clusterRect, visibleSegments.Count, seriesSpacingPixels, Horizontal);

            for (var visibleIndex = 0; visibleIndex < visibleSegments.Count; visibleIndex++)
            {
                var segmentLayout = visibleSegments[visibleIndex];
                var fill = legendMap.TryGetValue(segmentLayout.Label, out var existingLegendItem)
                    ? existingLegendItem.Fill
                    : SegmentColorSelector?.Invoke(segmentLayout.Segment) ?? DefaultPalette[legendMap.Count % DefaultPalette.Length];
                var hoverFill = SegmentHoverColorSelector?.Invoke(segmentLayout.Segment) ?? ChartColor.DarkenByFactor(fill);

                if (!legendMap.ContainsKey(segmentLayout.Label))
                {
                    legendMap[segmentLayout.Label] = new LegendItem(segmentLayout.Label, fill);
                }

                var rect = GetSegmentRect(clusterRect, visibleIndex, visibleSegments.Count, segmentThickness, seriesSpacingPixels, segmentLayout.Value, maxValue);
                var accessibleLabel = $"{label}, {segmentLayout.Label}: {segmentLayout.Value.ToString(ValueFormat, CultureInfo.InvariantCulture)}";
                var segment = new ClusteredBarChartSegment<TItem, TSegment>(
                    item,
                    segmentLayout.Segment,
                    categoryIndex,
                    segmentLayout.Index,
                    label,
                    segmentLayout.Label,
                    segmentLayout.Value,
                    fill,
                    hoverFill,
                    rect,
                    accessibleLabel,
                    string.Equals(selectedKey, $"{categoryIndex}:{segmentLayout.Index}:{label}:{segmentLayout.Label}", StringComparison.Ordinal),
                    false,
                    false);

                segments.Add(segment);
                segmentIndexByKey[segment.Key] = segments.Count - 1;
                categorySegments.Add(segment);
            }

            categories.Add(new CategoryState(
                categoryIndex,
                item,
                label,
                clusterRect,
                hoverRect,
                Horizontal
                    ? new SvgPoint(_plot.Left - 8, clusterRect.Y + clusterRect.Height / 2)
                    : new SvgPoint(clusterRect.X + clusterRect.Width / 2, _plot.Bottom + 20),
                new ReadOnlyCollection<ClusteredBarChartSegment<TItem, TSegment>>(categorySegments)));
        }

        _categories = new ReadOnlyCollection<CategoryState>(categories);
        _segments = new ReadOnlyCollection<ClusteredBarChartSegment<TItem, TSegment>>(segments);
        _segmentIndexByKey = segmentIndexByKey;
        _legendItems = new ReadOnlyCollection<LegendItem>(legendMap.Values.ToList());

        if (_hoveredCategoryIndex is int hoveredCategory &&
            !_categories.Any(category => category.Index == hoveredCategory && category.Segments.Count > 0))
        {
            _hoveredCategoryIndex = null;
        }

        if (_hoveredSegmentIndex is int hovered && (hovered < 0 || hovered >= _segments.Count))
        {
            _hoveredSegmentIndex = null;
        }

        if (_focusedSegmentIndex is int focused && (focused < 0 || focused >= _segments.Count))
        {
            _focusedSegmentIndex = null;
        }

        if (_hoveredLegendLabel is not null && !_legendItems.Any(item => item.Label == _hoveredLegendLabel))
        {
            _hoveredLegendLabel = null;
        }
    }

    private SvgRect GetCategoryHoverBounds(int categoryIndex, int itemCount)
    {
        if (itemCount <= 0)
        {
            return new SvgRect(0, 0, 0, 0);
        }

        if (Horizontal)
        {
            var step = _plot.Height / itemCount;
            return new SvgRect(_plot.Left, _plot.Top + categoryIndex * step, _plot.Width, step);
        }

        var widthStep = _plot.Width / itemCount;
        return new SvgRect(_plot.Left + categoryIndex * widthStep, _plot.Top, widthStep, _plot.Height);
    }

    private SvgRect GetClusterBounds(int categoryIndex, int itemCount, double safeBarWidthRatio, double safeGroupSpacing)
    {
        if (itemCount <= 0)
        {
            return new SvgRect(0, 0, 0, 0);
        }

        if (Horizontal)
        {
            var step = _plot.Height / itemCount;
            var clusterHeight = Math.Max(step * safeBarWidthRatio * (1 - safeGroupSpacing), 1);
            var y = _plot.Top + categoryIndex * step + (step - clusterHeight) / 2;
            return new SvgRect(_plot.Left, y, _plot.Width, clusterHeight);
        }

        var widthStep = _plot.Width / itemCount;
        var clusterWidth = Math.Max(widthStep * safeBarWidthRatio * (1 - safeGroupSpacing), 1);
        var x = _plot.Left + categoryIndex * widthStep + (widthStep - clusterWidth) / 2;
        return new SvgRect(x, _plot.Top, clusterWidth, _plot.Height);
    }

    private static double GetSeriesSpacing(SvgRect clusterRect, int segmentCount, double safeSeriesSpacing, bool horizontal)
    {
        if (segmentCount <= 1)
        {
            return 0;
        }

        var dimension = horizontal ? clusterRect.Height : clusterRect.Width;
        var baseStep = dimension / segmentCount;
        return Math.Max(baseStep * safeSeriesSpacing, 0);
    }

    private static double GetSegmentThickness(SvgRect clusterRect, int segmentCount, double seriesSpacingPixels, bool horizontal)
    {
        var totalSpacing = seriesSpacingPixels * Math.Max(segmentCount - 1, 0);
        var dimension = horizontal ? clusterRect.Height : clusterRect.Width;
        var available = Math.Max(dimension - totalSpacing, 1);
        return Math.Max(available / segmentCount, 1);
    }

    private SvgRect GetSegmentRect(
        SvgRect clusterRect,
        int visibleIndex,
        int segmentCount,
        double segmentThickness,
        double seriesSpacingPixels,
        double value,
        double maxValue)
    {
        var scale = maxValue > 0 ? Math.Clamp(value / maxValue, 0, 1) : 0;
        var offset = visibleIndex * (segmentThickness + seriesSpacingPixels);

        if (Horizontal)
        {
            var width = Math.Max(scale * _plot.Width, 0);
            var segmentY = clusterRect.Y + offset;
            return new SvgRect(_plot.Left, segmentY, width, segmentThickness);
        }

        var height = Math.Max(scale * _plot.Height, 0);
        var x = clusterRect.X + offset;
        var segmentTop = _plot.Bottom - height;
        return new SvgRect(x, segmentTop, segmentThickness, height);
    }

    private Task OnPlotAreaChanged(PlotArea plot)
    {
        _plot = plot;
        RebuildChartState();
        return Task.CompletedTask;
    }

    private async Task HandleHoverAsync(ClusteredBarChartSegment<TItem, TSegment> segment)
    {
        var index = FindSegmentIndex(segment);
        if (index < 0 || _hoveredSegmentIndex == index)
        {
            return;
        }

        _hoveredSegmentIndex = index;
        await RefreshChartAsync();
        await OnSegmentHoverChanged.InvokeAsync(ToInteraction(HoveredSegment!));
    }

    private async Task HandleHoverLeaveAsync(ClusteredBarChartSegment<TItem, TSegment> segment)
    {
        if (UsesSharedTooltip)
        {
            return;
        }

        var index = FindSegmentIndex(segment);
        if (index < 0)
        {
            return;
        }

        if (_hoveredSegmentIndex != index || _focusedSegmentIndex == index)
        {
            return;
        }

        _hoveredSegmentIndex = null;
        await RefreshChartAsync();
    }

    private async Task HandleCategoryEnterAsync(CategoryState category)
    {
        if (!UsesSharedTooltip || category.Segments.Count == 0 || _hoveredCategoryIndex == category.Index)
        {
            return;
        }

        _hoveredCategoryIndex = category.Index;

        if (HoveredSegment is not null &&
            HoveredSegment.CategoryIndex != category.Index &&
            FocusedSegment?.Key != HoveredSegment.Key)
        {
            _hoveredSegmentIndex = null;
        }

        await RefreshChartAsync();
    }

    private async Task HandleCategoryLeaveAsync(CategoryState category)
    {
        if (!UsesSharedTooltip || _hoveredCategoryIndex != category.Index)
        {
            return;
        }

        _hoveredCategoryIndex = null;

        if (HoveredSegment?.CategoryIndex == category.Index &&
            FocusedSegment?.Key != HoveredSegment.Key)
        {
            _hoveredSegmentIndex = null;
        }

        await RefreshChartAsync();
    }

    private async Task HandleFocusAsync(ClusteredBarChartSegment<TItem, TSegment> segment)
    {
        var index = FindSegmentIndex(segment);
        if (index < 0)
        {
            return;
        }

        _focusedSegmentIndex = index;
        _hoveredSegmentIndex = index;
        await RefreshChartAsync();
        await OnSegmentHoverChanged.InvokeAsync(ToInteraction(HoveredSegment!));
    }

    private async Task HandleBlurAsync(ClusteredBarChartSegment<TItem, TSegment> segment)
    {
        var index = FindSegmentIndex(segment);
        if (index < 0)
        {
            return;
        }

        if (_focusedSegmentIndex == index)
        {
            _focusedSegmentIndex = null;
        }

        if (_hoveredSegmentIndex == index)
        {
            _hoveredSegmentIndex = null;
        }

        await RefreshChartAsync();
    }

    private async Task HandleSelectAsync(ClusteredBarChartSegment<TItem, TSegment> segment)
    {
        SelectedSegment = segment;
        await RefreshChartAsync();
        await SelectedSegmentChanged.InvokeAsync(segment);
        await OnSegmentClick.InvokeAsync(ToInteraction(segment));
    }

    private async Task HandleKeyDownAsync(KeyboardEventArgs args, ClusteredBarChartSegment<TItem, TSegment> segment)
    {
        if (args.Key is "Enter" or " ")
        {
            await HandleSelectAsync(segment);
        }
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

    private SvgPoint GetTooltipAnchor(ClusteredBarChartSegment<TItem, TSegment> segment)
    {
        var left = segment.Rect.X + segment.Rect.Width / 2;
        var top = Horizontal
            ? segment.Rect.Y + segment.Rect.Height / 2
            : Math.Max(segment.Rect.Y - 12, 8);

        return new(left, top);
    }

    private string GetTooltipStyle(ClusteredBarChartSegment<TItem, TSegment> segment)
    {
        var anchor = GetTooltipAnchor(segment);
        return $"left: {Fmt(anchor.X)}px; top: {Fmt(anchor.Y)}px;";
    }

    private string GetTooltipStyle(ClusteredBarTooltipContext<TItem, TSegment> tooltip) =>
        $"left: {Fmt(tooltip.Anchor.X)}px; top: {Fmt(tooltip.Anchor.Y)}px;";

    private string GetSegmentClasses(ClusteredBarChartSegment<TItem, TSegment> segment)
    {
        var classes = new List<string> { "clustered-segment" };
        if (IsHovered(segment)) classes.Add("is-hovered");
        if (IsFocused(segment)) classes.Add("is-focused");
        if (IsSelected(segment)) classes.Add("is-selected");
        if (IsLegendMatch(segment.SeriesLabel)) classes.Add("is-legend-active");
        return string.Join(" ", classes);
    }

    private ClusteredBarTooltipContext<TItem, TSegment> CreateSharedTooltipContext(CategoryState category)
    {
        var activeSegment = TooltipActiveSegment;
        var totalValue = category.Segments.Sum(segment => segment.Value);
        var rows = category.Segments
            .Select(segment => new ClusteredBarTooltipRow<TItem, TSegment>(
                segment.Item,
                segment.Segment,
                segment.CategoryIndex,
                segment.SegmentIndex,
                segment.CategoryLabel,
                segment.SeriesLabel,
                segment.Value,
                totalValue,
                segment.Fill,
                string.Equals(segment.Key, activeSegment?.Key, StringComparison.Ordinal)))
            .ToList();

        return new ClusteredBarTooltipContext<TItem, TSegment>(
            category.Item,
            category.Index,
            category.Label,
            totalValue,
            rows,
            activeSegment,
            GetSharedTooltipAnchor(category),
            Horizontal);
    }

    private SvgPoint GetSharedTooltipAnchor(CategoryState category)
    {
        if (category.Segments.Count == 0)
        {
            return new SvgPoint(category.HoverRect.X + category.HoverRect.Width / 2, category.HoverRect.Y);
        }

        if (Horizontal)
        {
            var right = category.Segments.Max(segment => segment.Rect.X + segment.Rect.Width);
            var centerY = category.HoverRect.Y + category.HoverRect.Height / 2;
            return new SvgPoint(Math.Min(right + 12, _plot.Right - 8), centerY);
        }

        var left = category.HoverRect.X + category.HoverRect.Width / 2;
        var top = category.Segments.Min(segment => segment.Rect.Y);
        return new SvgPoint(left, Math.Max(top - 12, 8));
    }

    private string GetLegendItemClasses(LegendItem item)
    {
        var classes = new List<string> { "clustered-bar-legend__item" };
        if (IsLegendMatch(item.Label)) classes.Add("is-active");
        return string.Join(" ", classes);
    }

    private bool IsLegendMatch(string label) =>
        !string.IsNullOrWhiteSpace(_hoveredLegendLabel) &&
        string.Equals(_hoveredLegendLabel, label, StringComparison.Ordinal);

    private int FindSegmentIndex(ClusteredBarChartSegment<TItem, TSegment> segment)
        => _segmentIndexByKey.GetValueOrDefault(segment.Key, -1);

    private IEnumerable<string> GetShellClasses()
    {
        yield return "fire-clustered-bar-chart-shell";
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

    private IReadOnlyList<TSegment> SegmentsSelectorOrThrow(TItem item) => SegmentsSelector!(item);

    private string LabelSelectorOrThrow(TItem item) => LabelSelector!(item);

    private double SegmentValueSelectorOrThrow(TSegment segment) => SegmentValueSelector!(segment);

    private string SegmentLabelSelectorOrThrow(TSegment segment) => SegmentLabelSelector!(segment);

    private ClusteredBarChartSegmentInteraction<TItem, TSegment> ToInteraction(ClusteredBarChartSegment<TItem, TSegment> segment) =>
        new(segment.Item, segment.Segment, segment.CategoryIndex, segment.SegmentIndex, segment.CategoryLabel, segment.SeriesLabel, segment.Value);

    private bool IsSelected(ClusteredBarChartSegment<TItem, TSegment> segment) =>
        string.Equals(SelectedSegment?.Key, segment.Key, StringComparison.Ordinal);

    private bool IsHovered(ClusteredBarChartSegment<TItem, TSegment> segment) =>
        UsesSharedTooltip
            ? TooltipCategoryIndex == segment.CategoryIndex
            : _hoveredSegmentIndex is int index &&
              index >= 0 &&
              index < _segments.Count &&
              string.Equals(_segments[index].Key, segment.Key, StringComparison.Ordinal);

    private bool IsFocused(ClusteredBarChartSegment<TItem, TSegment> segment) =>
        _focusedSegmentIndex is int index &&
        index >= 0 &&
        index < _segments.Count &&
        string.Equals(_segments[index].Key, segment.Key, StringComparison.Ordinal);

    private static string Fmt(double value) => ChartFormat.Fmt(value);

    private AxisScale BuildValueScale(IReadOnlyList<TItem> items)
    {
        var values = items
            .SelectMany(item => (SegmentsSelectorOrThrow(item) ?? Array.Empty<TSegment>())
                .Select(SegmentValueSelectorOrThrow)
                .Select(ChartValues.Sanitize));
        var (pixelStart, pixelEnd) = Horizontal
            ? (_plot.Left, _plot.Right)
            : (_plot.Bottom, _plot.Top);

        return AxisScale.FromValues(values, pixelStart, pixelEnd, new AxisScaleOptions
        {
            TickCount = SafeGridLineCount,
            Baseline = AxisBaseline.IncludeZero,
            ForcedMax = MaxValue is > 0 && double.IsFinite(MaxValue.Value) ? MaxValue : null,
            EmptyFallbackMax = 100
        });
    }

    private sealed record SegmentLayout(
        TSegment Segment,
        int Index,
        string Label,
        double Value);

    internal sealed record CategoryState(
        int Index,
        TItem Item,
        string Label,
        SvgRect Rect,
        SvgRect HoverRect,
        SvgPoint AxisLabelPoint,
        IReadOnlyList<ClusteredBarChartSegment<TItem, TSegment>> Segments);

    internal sealed record LegendItem(
        string Label,
        string Fill);
}
