using FireCharts.Interaction;
using FireCharts.Models;
using FireCharts.Scales;
using Microsoft.AspNetCore.Components;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FireCharts.Components;

public partial class FireBarChart<TItem> : ComponentBase
{
    private IReadOnlyList<BarChartPoint<TItem>> _points = Array.Empty<BarChartPoint<TItem>>();
    private IReadOnlyList<ScaleTick> _valueAxisTicks = Array.Empty<ScaleTick>();
    private PlotArea _plot;
    private ChartInteraction<BarChartPoint<TItem>, int> _interaction = default!;
    private double _computedMaxValue = 100;

    [Parameter] public string Title { get; set; } = "Bar Chart";
    [Parameter] public string Description { get; set; } = "";
    [Parameter] public IReadOnlyList<TItem>? Items { get; set; }
    [Parameter, EditorRequired] public Func<TItem, double>? ValueSelector { get; set; }
    [Parameter, EditorRequired] public Func<TItem, string>? LabelSelector { get; set; }
    [Parameter] public Func<TItem, string>? TooltipTextSelector { get; set; }
    [Parameter] public Func<TItem, string>? ColorSelector { get; set; }
    [Parameter] public Func<TItem, string>? HoverColorSelector { get; set; }
    [Parameter] public TItem? SelectedItem { get; set; }
    [Parameter] public EventCallback<TItem?> SelectedItemChanged { get; set; }
    [Parameter] public EventCallback<ChartPointInteraction<TItem>> OnPointClick { get; set; }
    [Parameter] public EventCallback<ChartPointInteraction<TItem>> OnPointHoverChanged { get; set; }
    [Parameter] public RenderFragment<BarChartPoint<TItem>>? PointValueTemplate { get; set; }
    [Parameter] public RenderFragment<BarChartPoint<TItem>>? TooltipTemplate { get; set; }
    [Parameter] public RenderFragment? EmptyStateTemplate { get; set; }
    [Parameter] public double Width { get; set; } = 600;
    [Parameter] public double Height { get; set; } = 400;
    [Parameter] public bool Responsive { get; set; }
    [Parameter] public bool Horizontal { get; set; }
    [Parameter] public bool ShowGridLines { get; set; } = true;
    [Parameter] public bool ShowAxisLabels { get; set; } = true;
    [Parameter] public bool ShowValueLabels { get; set; } = true;
    [Parameter] public bool ShowTooltip { get; set; } = true;
    [Parameter] public bool ConstrainTooltipToChartBounds { get; set; }
    [Parameter] public int GridLineCount { get; set; } = 5;
    [Parameter] public string BarColor { get; set; } = "#4e79a7";
    [Parameter] public string HoverColor { get; set; } = "#2e5a87";
    [Parameter] public double BarSpacing { get; set; } = 0.2;
    [Parameter] public string ValueFormat { get; set; } = "F0";
    [Parameter] public double? MaxValue { get; set; }
    [Parameter] public double FontSize { get; set; } = 12;
    [Parameter] public double CornerRadius { get; set; } = 2;

    private ChartPadding Padding => new(
        Top: 10,
        Right: 10,
        Bottom: ShowAxisLabels ? 40 : 10,
        Left: ShowAxisLabels ? (Horizontal ? 90 : 50) : 10);

    internal IReadOnlyList<BarChartPoint<TItem>> Points => _points;
    internal BarChartPoint<TItem>? HoveredPoint => _interaction.Hovered;
    internal int SafeGridLineCount => Math.Max(GridLineCount, 1);
    internal double SafeBarSpacing => Math.Clamp(BarSpacing, 0, 0.9);

    internal double ComputedMaxValue => _computedMaxValue;
    internal IReadOnlyList<ScaleTick> ValueAxisTicks => _valueAxisTicks;

    protected override void OnInitialized()
    {
        _interaction = new ChartInteraction<BarChartPoint<TItem>, int>(new ChartInteractionOptions<BarChartPoint<TItem>, int>
        {
            KeySelector = point => point.Index,
            RequestRender = () => InvokeAsync(StateHasChanged),
            OnActiveChanged = point => OnPointHoverChanged.InvokeAsync(ToInteraction(point)),
            OnActivate = async point =>
            {
                SelectedItem = point.Item;
                await InvokeAsync(StateHasChanged);
                await SelectedItemChanged.InvokeAsync(point.Item);
                await OnPointClick.InvokeAsync(ToInteraction(point));
            }
        });
    }

    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(ValueSelector);
        ArgumentNullException.ThrowIfNull(LabelSelector);

        Width = Math.Max(Width, 1);
        Height = Math.Max(Height, 1);
        _plot = PlotArea.FromInset(Width, Height, Padding);
        RebuildPoints();
    }

    private void RebuildPoints()
    {
        var items = Items ?? Array.Empty<TItem>();
        var comparer = EqualityComparer<TItem>.Default;
        var selectedItem = SelectedItem;
        var scale = BuildValueScale(items);
        var computedMaxValue = scale.Max;

        _computedMaxValue = computedMaxValue;
        _valueAxisTicks = scale.Ticks;

        _points = new ReadOnlyCollection<BarChartPoint<TItem>>(items
            .Select((item, index) => CreatePoint(item, index, comparer.Equals(item, selectedItem), computedMaxValue))
            .ToList());

        _interaction.SetElements(_points);
    }

    private Task OnPlotAreaChanged(PlotArea plot)
    {
        _plot = plot;
        RebuildPoints();
        return Task.CompletedTask;
    }

    private BarChartPoint<TItem> CreatePoint(TItem item, int index, bool isSelected, double computedMaxValue)
    {
        var rect = GetBarRect(index, item, computedMaxValue);
        var label = LabelSelectorOrThrow(item);
        var value = ChartValues.Sanitize(ValueSelectorOrThrow(item));
        var tooltipText = TooltipTextSelector?.Invoke(item);

        return new BarChartPoint<TItem>(
            item,
            index,
            label,
            value,
            ColorSelector?.Invoke(item) ?? BarColor,
            HoverColorSelector?.Invoke(item) ?? HoverColor,
            rect,
            tooltipText ?? $"{label}: {value.ToString(ValueFormat, CultureInfo.InvariantCulture)}",
            isSelected,
            _interaction.HoveredKey == index,
            _interaction.FocusedKey == index);
    }

    private SvgRect GetBarRect(int index, TItem item, double computedMaxValue)
    {
        var itemCount = Items?.Count ?? 0;
        if (itemCount == 0 || index < 0 || index >= itemCount)
        {
            return new SvgRect(0, 0, 0, 0);
        }

        var barValue = ChartValues.Sanitize(ValueSelectorOrThrow(item));
        var scale = computedMaxValue > 0 ? barValue / computedMaxValue : 0;
        scale = Math.Clamp(scale, 0, 1);

        if (Horizontal)
        {
            var step = _plot.Height / itemCount;
            var barHeight = Math.Max(step * (1 - SafeBarSpacing), 1);
            var y = _plot.Top + index * step + (step - barHeight) / 2;
            var barWidth = Math.Max(scale * _plot.Width, 0);
            return new SvgRect(_plot.Left, y, barWidth, barHeight);
        }

        var widthStep = _plot.Width / itemCount;
        var barWidthVertical = Math.Max(widthStep * (1 - SafeBarSpacing), 1);
        var x = _plot.Left + index * widthStep + (widthStep - barWidthVertical) / 2;
        var barHeightVertical = Math.Max(scale * _plot.Height, 0);
        var barY = _plot.Bottom - barHeightVertical;
        return new SvgRect(x, barY, barWidthVertical, barHeightVertical);
    }

    private SvgPoint GetTooltipAnchor(BarChartPoint<TItem> point) =>
        new(point.Rect.X + (point.Rect.Width / 2), Math.Max(point.Rect.Y - 12, 8));

    private string GetTooltipStyle(BarChartPoint<TItem> point)
    {
        var anchor = GetTooltipAnchor(point);
        return $"left: {Fmt(anchor.X)}px; top: {Fmt(anchor.Y)}px;";
    }

    private string GetPointClasses(BarChartPoint<TItem> point)
    {
        var classes = new List<string> { "bar-group" };
        if (_interaction.IsHovered(point)) classes.Add("is-hovered");
        if (_interaction.IsFocused(point)) classes.Add("is-focused");
        if (point.IsSelected || IsSelected(point)) classes.Add("is-selected");
        return string.Join(" ", classes);
    }

    private ChartPointInteraction<TItem> ToInteraction(BarChartPoint<TItem> point) =>
        new(point.Item, point.Index, point.Label, point.Value);

    private bool IsSelected(BarChartPoint<TItem> point) =>
        EqualityComparer<TItem>.Default.Equals(point.Item, SelectedItem);

    private double ValueSelectorOrThrow(TItem item) => ValueSelector!(item);

    private string LabelSelectorOrThrow(TItem item) => LabelSelector!(item);

    private static string Fmt(double value) => ChartFormat.Fmt(value);

    private AxisScale BuildValueScale(IReadOnlyList<TItem> items)
    {
        var values = items.Select(item => ChartValues.Sanitize(ValueSelectorOrThrow(item)));
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
}
