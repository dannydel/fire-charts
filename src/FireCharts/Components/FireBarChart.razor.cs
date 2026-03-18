using FireCharts.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FireCharts.Components;

public partial class FireBarChart<TItem> : ComponentBase
{
    private IReadOnlyList<BarChartPoint<TItem>> _points = Array.Empty<BarChartPoint<TItem>>();
    private int? _hoveredIndex;
    private int? _focusedIndex;
    private double? _renderWidth;
    private double? _renderHeight;

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
    [Parameter] public int GridLineCount { get; set; } = 5;
    [Parameter] public string BarColor { get; set; } = "#4e79a7";
    [Parameter] public string HoverColor { get; set; } = "#2e5a87";
    [Parameter] public double BarSpacing { get; set; } = 0.2;
    [Parameter] public string ValueFormat { get; set; } = "F0";
    [Parameter] public double? MaxValue { get; set; }
    [Parameter] public double FontSize { get; set; } = 12;
    [Parameter] public double CornerRadius { get; set; } = 2;

    private double PaddingTop => 10;
    private double PaddingRight => 10;
    private double PaddingBottom => ShowAxisLabels ? 40 : 10;
    private double PaddingLeft => ShowAxisLabels ? (Horizontal ? 90 : 50) : 10;

    internal IReadOnlyList<BarChartPoint<TItem>> Points => _points;
    internal BarChartPoint<TItem>? HoveredPoint => _hoveredIndex is int index && index >= 0 && index < _points.Count ? _points[index] : null;

    internal double SafeWidth => Math.Max(_renderWidth ?? Width, 1);
    internal double SafeHeight => Math.Max(_renderHeight ?? Height, 1);
    internal double ChartAreaLeft => PaddingLeft;
    internal double ChartAreaTop => PaddingTop;
    internal double ChartAreaRight => SafeWidth - PaddingRight;
    internal double ChartAreaBottom => SafeHeight - PaddingBottom;
    internal double ChartAreaWidth => Math.Max(ChartAreaRight - ChartAreaLeft, 1);
    internal double ChartAreaHeight => Math.Max(ChartAreaBottom - ChartAreaTop, 1);
    internal int SafeGridLineCount => Math.Max(GridLineCount, 1);
    internal double SafeBarSpacing => Math.Clamp(BarSpacing, 0, 0.9);

    internal double ComputedMaxValue
    {
        get
        {
            if (MaxValue.HasValue && MaxValue.Value > 0 && double.IsFinite(MaxValue.Value))
            {
                return MaxValue.Value;
            }

            var max = Items is null
                ? 0
                : Items
                    .Select(ValueSelectorOrThrow)
                    .Where(double.IsFinite)
                    .DefaultIfEmpty(0)
                    .Max();

            if (max <= 0)
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
    }

    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(ValueSelector);
        ArgumentNullException.ThrowIfNull(LabelSelector);

        Width = Math.Max(Width, 1);
        Height = Math.Max(Height, 1);
        _renderWidth ??= Width;
        _renderHeight ??= Height;
        RebuildPoints();
    }

    private void RebuildPoints()
    {
        var items = Items ?? Array.Empty<TItem>();
        var comparer = EqualityComparer<TItem>.Default;
        var selectedItem = SelectedItem;

        _points = new ReadOnlyCollection<BarChartPoint<TItem>>(items
            .Select((item, index) => CreatePoint(item, index, comparer.Equals(item, selectedItem)))
            .ToList());

        if (_hoveredIndex is int hovered && hovered >= _points.Count)
        {
            _hoveredIndex = null;
        }

        if (_focusedIndex is int focused && focused >= _points.Count)
        {
            _focusedIndex = null;
        }
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
        RebuildPoints();
    }

    private BarChartPoint<TItem> CreatePoint(TItem item, int index, bool isSelected)
    {
        var rect = GetBarRect(index, item);
        var label = LabelSelectorOrThrow(item);
        var value = SanitizeValue(ValueSelectorOrThrow(item));
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
            _hoveredIndex == index,
            _focusedIndex == index);
    }

    private SvgRect GetBarRect(int index, TItem item)
    {
        var itemCount = Items?.Count ?? 0;
        if (itemCount == 0 || index < 0 || index >= itemCount)
        {
            return new SvgRect(0, 0, 0, 0);
        }

        var barValue = SanitizeValue(ValueSelectorOrThrow(item));
        var maxVal = ComputedMaxValue;
        var scale = maxVal > 0 ? barValue / maxVal : 0;
        scale = Math.Clamp(scale, 0, 1);

        if (Horizontal)
        {
            var step = ChartAreaHeight / itemCount;
            var barHeight = Math.Max(step * (1 - SafeBarSpacing), 1);
            var y = ChartAreaTop + index * step + (step - barHeight) / 2;
            var barWidth = Math.Max(scale * ChartAreaWidth, 0);
            return new SvgRect(ChartAreaLeft, y, barWidth, barHeight);
        }

        var widthStep = ChartAreaWidth / itemCount;
        var barWidthVertical = Math.Max(widthStep * (1 - SafeBarSpacing), 1);
        var x = ChartAreaLeft + index * widthStep + (widthStep - barWidthVertical) / 2;
        var barHeightVertical = Math.Max(scale * ChartAreaHeight, 0);
        var barY = ChartAreaBottom - barHeightVertical;
        return new SvgRect(x, barY, barWidthVertical, barHeightVertical);
    }

    private async Task HandleHoverAsync(BarChartPoint<TItem> point)
    {
        if (_hoveredIndex == point.Index)
        {
            return;
        }

        _hoveredIndex = point.Index;
        await RefreshPointsAsync();
        await OnPointHoverChanged.InvokeAsync(ToInteraction(HoveredPoint!));
    }

    private async Task HandleHoverLeaveAsync(BarChartPoint<TItem> point)
    {
        if (_hoveredIndex != point.Index || _focusedIndex == point.Index)
        {
            return;
        }

        _hoveredIndex = null;
        await RefreshPointsAsync();
    }

    private async Task HandleFocusAsync(BarChartPoint<TItem> point)
    {
        _focusedIndex = point.Index;
        _hoveredIndex = point.Index;
        await RefreshPointsAsync();
        await OnPointHoverChanged.InvokeAsync(ToInteraction(HoveredPoint!));
    }

    private async Task HandleBlurAsync(BarChartPoint<TItem> point)
    {
        if (_focusedIndex == point.Index)
        {
            _focusedIndex = null;
        }

        if (_hoveredIndex == point.Index)
        {
            _hoveredIndex = null;
        }

        await RefreshPointsAsync();
    }

    private async Task HandleSelectAsync(BarChartPoint<TItem> point)
    {
        SelectedItem = point.Item;
        await RefreshPointsAsync();
        await SelectedItemChanged.InvokeAsync(point.Item);
        await OnPointClick.InvokeAsync(ToInteraction(point));
    }

    private async Task HandleKeyDownAsync(KeyboardEventArgs args, BarChartPoint<TItem> point)
    {
        if (args.Key is "Enter" or " ")
        {
            await HandleSelectAsync(point);
        }
    }

    private string GetTooltipStyle(BarChartPoint<TItem> point)
    {
        var centerX = point.Rect.X + point.Rect.Width / 2;
        var top = Math.Max(point.Rect.Y - 12, 8);
        return $"left: {Fmt(centerX)}px; top: {Fmt(top)}px;";
    }

    private string GetPointClasses(BarChartPoint<TItem> point)
    {
        var classes = new List<string> { "bar-group" };
        if (point.IsHovered) classes.Add("is-hovered");
        if (point.IsFocused) classes.Add("is-focused");
        if (point.IsSelected) classes.Add("is-selected");
        return string.Join(" ", classes);
    }

    private async Task RefreshPointsAsync()
    {
        RebuildPoints();
        await InvokeAsync(StateHasChanged);
    }

    private ChartPointInteraction<TItem> ToInteraction(BarChartPoint<TItem> point) =>
        new(point.Item, point.Index, point.Label, point.Value);

    private double ValueSelectorOrThrow(TItem item) => ValueSelector!(item);

    private string LabelSelectorOrThrow(TItem item) => LabelSelector!(item);

    private static string Fmt(double value) =>
        double.IsFinite(value)
            ? value.ToString("F1", CultureInfo.InvariantCulture)
            : "0.0";

    private static double SanitizeValue(double value) =>
        double.IsFinite(value) ? Math.Max(value, 0) : 0;
}
