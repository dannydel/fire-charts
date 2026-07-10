using System.Collections.ObjectModel;
using System.Globalization;
using FireCharts.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FireCharts.Components;

public partial class FireWaterfallChart<TItem> : ComponentBase
{
    private sealed record AxisTick(double Value, double Position);
    private sealed record Connector(double X1, double X2, double Y);
    private sealed record RawPoint(
        TItem Item,
        int Index,
        string Label,
        double RawValue,
        WaterfallStepType StepType,
        string? ExplicitFill,
        string? ExplicitHoverFill,
        string AccessibleLabel);

    private IReadOnlyList<WaterfallChartPoint<TItem>> _points = Array.Empty<WaterfallChartPoint<TItem>>();
    private IReadOnlyList<AxisTick> _yAxisTicks = Array.Empty<AxisTick>();
    private IReadOnlyList<Connector> _connectors = Array.Empty<Connector>();
    private int? _hoveredIndex;
    private int? _focusedIndex;
    private double? _renderWidth;
    private double? _renderHeight;
    private double _scaleMin;
    private double _scaleMax;

    [Parameter] public string Title { get; set; } = "Waterfall Chart";
    [Parameter] public string Description { get; set; } = "";
    [Parameter] public IReadOnlyList<TItem>? Items { get; set; }
    [Parameter, EditorRequired] public Func<TItem, string>? LabelSelector { get; set; }
    [Parameter, EditorRequired] public Func<TItem, double>? ValueSelector { get; set; }
    [Parameter, EditorRequired] public Func<TItem, WaterfallStepType>? StepTypeSelector { get; set; }
    [Parameter] public Func<TItem, string>? TooltipTextSelector { get; set; }
    [Parameter] public Func<TItem, string>? ColorSelector { get; set; }
    [Parameter] public Func<TItem, string>? HoverColorSelector { get; set; }
    [Parameter] public TItem? SelectedItem { get; set; }
    [Parameter] public EventCallback<TItem?> SelectedItemChanged { get; set; }
    [Parameter] public EventCallback<WaterfallChartPointInteraction<TItem>> OnPointClick { get; set; }
    [Parameter] public EventCallback<WaterfallChartPointInteraction<TItem>> OnPointHoverChanged { get; set; }
    [Parameter] public RenderFragment<WaterfallChartPoint<TItem>>? TooltipTemplate { get; set; }
    [Parameter] public RenderFragment? EmptyStateTemplate { get; set; }
    [Parameter] public double Width { get; set; } = 720;
    [Parameter] public double Height { get; set; } = 420;
    [Parameter] public bool Responsive { get; set; }
    [Parameter] public bool ShowGridLines { get; set; } = true;
    [Parameter] public bool ShowAxisLabels { get; set; } = true;
    [Parameter] public bool ShowValueLabels { get; set; } = true;
    [Parameter] public bool ShowTooltip { get; set; } = true;
    [Parameter] public bool ConstrainTooltipToChartBounds { get; set; }
    [Parameter] public bool ShowConnectors { get; set; } = true;
    [Parameter] public int GridLineCount { get; set; } = 5;
    [Parameter] public string ValueFormat { get; set; } = "F0";
    [Parameter] public double CornerRadius { get; set; } = 3;
    [Parameter] public string IncreaseColor { get; set; } = "#198f8c";
    [Parameter] public string IncreaseHoverColor { get; set; } = "#11696c";
    [Parameter] public string DecreaseColor { get; set; } = "#d94f3d";
    [Parameter] public string DecreaseHoverColor { get; set; } = "#a73728";
    [Parameter] public string TotalColor { get; set; } = "#4d89ff";
    [Parameter] public string TotalHoverColor { get; set; } = "#245ec7";
    [Parameter] public string ConnectorColor { get; set; } = "#7d6b76";

    private double PaddingTop => 12;
    private double PaddingRight => 10;
    private double PaddingBottom => ShowAxisLabels ? 44 : 10;
    private double PaddingLeft => ShowAxisLabels ? 62 : 10;

    private IReadOnlyList<WaterfallChartPoint<TItem>> Points => _points;
    private IReadOnlyList<AxisTick> YAxisTicks => _yAxisTicks;
    private IReadOnlyList<Connector> Connectors => _connectors;
    private WaterfallChartPoint<TItem>? HoveredPoint => _hoveredIndex is int index && index >= 0 && index < _points.Count ? _points[index] : null;
    private double SafeWidth => Math.Max(_renderWidth ?? Width, 1);
    private double SafeHeight => Math.Max(_renderHeight ?? Height, 1);
    private double ChartAreaLeft => PaddingLeft;
    private double ChartAreaTop => PaddingTop;
    private double ChartAreaRight => SafeWidth - PaddingRight;
    private double ChartAreaBottom => SafeHeight - PaddingBottom;
    private double ChartAreaWidth => Math.Max(ChartAreaRight - ChartAreaLeft, 1);
    private double ChartAreaHeight => Math.Max(ChartAreaBottom - ChartAreaTop, 1);
    private int SafeGridLineCount => Math.Max(GridLineCount, 2);
    private double SafeCornerRadius => Math.Clamp(CornerRadius, 0, 16);
    private double ZeroLineY => MapY(0, _scaleMin, _scaleMax);

    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(LabelSelector);
        ArgumentNullException.ThrowIfNull(ValueSelector);
        ArgumentNullException.ThrowIfNull(StepTypeSelector);

        Width = Math.Max(Width, 1);
        Height = Math.Max(Height, 1);
        _renderWidth ??= Width;
        _renderHeight ??= Height;
        RebuildPoints();
    }

    private void RebuildPoints()
    {
        var rawPoints = BuildRawPoints();
        if (rawPoints.Count == 0)
        {
            _points = Array.Empty<WaterfallChartPoint<TItem>>();
            _yAxisTicks = Array.Empty<AxisTick>();
            _connectors = Array.Empty<Connector>();
            _hoveredIndex = null;
            _focusedIndex = null;
            _scaleMin = 0;
            _scaleMax = 0;
            return;
        }

        var segments = BuildSegments(rawPoints);
        var allValues = segments
            .SelectMany(point => new[] { point.StartValue, point.EndValue, 0d })
            .ToList();
        (_scaleMin, _scaleMax, var step) = GetNiceScale(allValues.Min(), allValues.Max(), SafeGridLineCount);
        _yAxisTicks = BuildTicks(_scaleMin, _scaleMax, step);

        var renderedPoints = new List<WaterfallChartPoint<TItem>>(segments.Count);
        var connectors = new List<Connector>(Math.Max(segments.Count - 1, 0));
        var comparer = EqualityComparer<TItem>.Default;
        var itemCount = segments.Count;
        var stepWidth = ChartAreaWidth / itemCount;
        var barWidth = Math.Max(stepWidth * 0.62, 1);

        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            var x = ChartAreaLeft + index * stepWidth + ((stepWidth - barWidth) / 2);
            var yTop = MapY(Math.Max(segment.StartValue, segment.EndValue), _scaleMin, _scaleMax);
            var yBottom = MapY(Math.Min(segment.StartValue, segment.EndValue), _scaleMin, _scaleMax);
            var height = Math.Max(yBottom - yTop, 1);
            var rect = new SvgRect(x, yTop, barWidth, height);
            var (fill, hoverFill) = ResolveColors(segment);

            renderedPoints.Add(new WaterfallChartPoint<TItem>(
                segment.Item,
                segment.Index,
                segment.Label,
                segment.RawValue,
                segment.StartValue,
                segment.EndValue,
                segment.DisplayValue,
                segment.StepType,
                rect,
                fill,
                hoverFill,
                segment.EndValue >= segment.StartValue,
                comparer.Equals(segment.Item, SelectedItem),
                _hoveredIndex == segment.Index,
                _focusedIndex == segment.Index,
                segment.AccessibleLabel));

            if (ShowConnectors && index < segments.Count - 1)
            {
                var connectorY = MapY(segment.EndValue, _scaleMin, _scaleMax);
                connectors.Add(new Connector(
                    x + barWidth,
                    ChartAreaLeft + ((index + 1) * stepWidth) + ((stepWidth - barWidth) / 2),
                    connectorY));
            }
        }

        _points = new ReadOnlyCollection<WaterfallChartPoint<TItem>>(renderedPoints);
        _connectors = new ReadOnlyCollection<Connector>(connectors);

        if (_hoveredIndex is int hovered && (hovered < 0 || hovered >= _points.Count))
        {
            _hoveredIndex = null;
        }

        if (_focusedIndex is int focused && (focused < 0 || focused >= _points.Count))
        {
            _focusedIndex = null;
        }
    }

    private List<RawPoint> BuildRawPoints()
    {
        var points = new List<RawPoint>();
        var items = Items ?? Array.Empty<TItem>();

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var rawValue = ValueSelector!(item);
            if (!double.IsFinite(rawValue))
            {
                continue;
            }

            var label = LabelSelector!(item);
            var stepType = StepTypeSelector!(item);
            var accessibleLabel = TooltipTextSelector?.Invoke(item)
                ?? $"{label}: {rawValue.ToString(ValueFormat, CultureInfo.InvariantCulture)}";

            points.Add(new RawPoint(
                item,
                index,
                label,
                rawValue,
                stepType,
                ColorSelector?.Invoke(item),
                HoverColorSelector?.Invoke(item),
                accessibleLabel));
        }

        return points;
    }

    private List<SegmentState> BuildSegments(IReadOnlyList<RawPoint> rawPoints)
    {
        var segments = new List<SegmentState>(rawPoints.Count);
        var runningTotal = 0d;

        foreach (var point in rawPoints)
        {
            double startValue;
            double endValue;
            double displayValue;

            switch (point.StepType)
            {
                case WaterfallStepType.Start:
                    startValue = 0;
                    endValue = point.RawValue;
                    runningTotal = endValue;
                    displayValue = endValue;
                    break;
                case WaterfallStepType.Change:
                    startValue = runningTotal;
                    endValue = runningTotal + point.RawValue;
                    runningTotal = endValue;
                    displayValue = point.RawValue;
                    break;
                case WaterfallStepType.Subtotal:
                case WaterfallStepType.Total:
                    startValue = 0;
                    endValue = runningTotal;
                    displayValue = runningTotal;
                    break;
                default:
                    startValue = 0;
                    endValue = point.RawValue;
                    runningTotal = endValue;
                    displayValue = endValue;
                    break;
            }

            segments.Add(new SegmentState(
                point.Item,
                point.Index,
                point.Label,
                point.RawValue,
                startValue,
                endValue,
                displayValue,
                point.StepType,
                point.ExplicitFill,
                point.ExplicitHoverFill,
                point.AccessibleLabel));
        }

        return segments;
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

    private async Task HandleHoverAsync(WaterfallChartPoint<TItem> point)
    {
        if (_hoveredIndex == point.Index)
        {
            return;
        }

        _hoveredIndex = point.Index;
        await RefreshPointsAsync();
        await OnPointHoverChanged.InvokeAsync(ToInteraction(HoveredPoint!));
    }

    private async Task HandleHoverLeaveAsync(WaterfallChartPoint<TItem> point)
    {
        if (_hoveredIndex != point.Index || _focusedIndex == point.Index)
        {
            return;
        }

        _hoveredIndex = null;
        await RefreshPointsAsync();
    }

    private async Task HandleFocusAsync(WaterfallChartPoint<TItem> point)
    {
        _focusedIndex = point.Index;
        _hoveredIndex = point.Index;
        await RefreshPointsAsync();
        await OnPointHoverChanged.InvokeAsync(ToInteraction(HoveredPoint!));
    }

    private async Task HandleBlurAsync(WaterfallChartPoint<TItem> point)
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

    private async Task HandleSelectAsync(WaterfallChartPoint<TItem> point)
    {
        SelectedItem = point.Item;
        await RefreshPointsAsync();
        await SelectedItemChanged.InvokeAsync(point.Item);
        await OnPointClick.InvokeAsync(ToInteraction(point));
    }

    private async Task HandleKeyDownAsync(KeyboardEventArgs args, WaterfallChartPoint<TItem> point)
    {
        if (args.Key is "Enter" or " ")
        {
            await HandleSelectAsync(point);
        }
    }

    private async Task RefreshPointsAsync()
        => await InvokeAsync(StateHasChanged);

    private string GetPointClasses(WaterfallChartPoint<TItem> point)
    {
        var classes = new List<string> { "waterfall-group" };
        if (_hoveredIndex == point.Index) classes.Add("is-hovered");
        if (_focusedIndex == point.Index) classes.Add("is-focused");
        if (IsSelected(point)) classes.Add("is-selected");
        classes.Add(point.StepType switch
        {
            WaterfallStepType.Change when point.IsIncrease => "is-increase",
            WaterfallStepType.Change => "is-decrease",
            _ => "is-total"
        });
        return string.Join(" ", classes);
    }

    private string GetPointStyle(WaterfallChartPoint<TItem> point) =>
        $"--waterfall-color: {point.Fill}; --waterfall-hover-color: {point.HoverFill};";

    private string GetConnectorStyle() => $"--connector-color: {ConnectorColor};";

    private SvgPoint GetTooltipAnchor(WaterfallChartPoint<TItem> point) =>
        new(GetBarCenterX(point), Math.Max(point.Rect.Y - 12, 8));

    private string GetTooltipStyle(WaterfallChartPoint<TItem> point)
    {
        var anchor = GetTooltipAnchor(point);
        return $"left: {Fmt(anchor.X)}px; top: {Fmt(anchor.Y)}px;";
    }

    private double GetBarCenterX(WaterfallChartPoint<TItem> point) => point.Rect.X + (point.Rect.Width / 2);

    private bool IsSelected(WaterfallChartPoint<TItem> point) =>
        EqualityComparer<TItem>.Default.Equals(point.Item, SelectedItem);

    private WaterfallChartPointInteraction<TItem> ToInteraction(WaterfallChartPoint<TItem> point) =>
        new(point.Item, point.Index, point.Label, point.Value, point.StartValue, point.EndValue, point.DisplayValue, point.StepType);

    private (string Fill, string HoverFill) ResolveColors(SegmentState point)
    {
        if (!string.IsNullOrWhiteSpace(point.ExplicitFill))
        {
            return (
                point.ExplicitFill!,
                point.ExplicitHoverFill ?? point.ExplicitFill!);
        }

        return point.StepType switch
        {
            WaterfallStepType.Change when point.EndValue < point.StartValue => (DecreaseColor, DecreaseHoverColor),
            WaterfallStepType.Change => (IncreaseColor, IncreaseHoverColor),
            _ => (TotalColor, TotalHoverColor)
        };
    }

    private double MapY(double value, double min, double max)
    {
        var range = max - min;
        if (Math.Abs(range) < 0.000001)
        {
            return ChartAreaBottom;
        }

        var normalized = (value - min) / range;
        return ChartAreaBottom - (normalized * ChartAreaHeight);
    }

    private IReadOnlyList<AxisTick> BuildTicks(double min, double max, double step)
    {
        var ticks = new List<AxisTick>();
        var value = min;
        var guard = 0;

        while (value <= max + (step * 0.5) && guard < 100)
        {
            var normalized = NormalizeZero(value);
            ticks.Add(new AxisTick(normalized, MapY(normalized, min, max)));
            value += step;
            guard++;
        }

        return new ReadOnlyCollection<AxisTick>(ticks);
    }

    private static (double Min, double Max, double Step) GetNiceScale(double min, double max, int tickCount)
    {
        if (min >= 0)
        {
            min = 0;
        }

        if (max <= 0)
        {
            max = 0;
        }

        if (Math.Abs(max - min) < 0.000001)
        {
            var padding = Math.Max(Math.Abs(max) * 0.2, 1d);
            min -= padding;
            max += padding;

            if (min >= 0)
            {
                min = 0;
            }

            if (max <= 0)
            {
                max = 0;
            }
        }

        var safeTickCount = Math.Max(tickCount, 2);
        var range = NiceNumber(max - min, false);
        var step = NiceNumber(range / (safeTickCount - 1), true);
        var niceMin = Math.Floor(min / step) * step;
        var niceMax = Math.Ceiling(max / step) * step;
        return (niceMin, niceMax, step);
    }

    private static double NiceNumber(double range, bool round)
    {
        if (range <= 0 || !double.IsFinite(range))
        {
            return 1;
        }

        var exponent = Math.Floor(Math.Log10(range));
        var fraction = range / Math.Pow(10, exponent);
        double niceFraction;

        if (round)
        {
            if (fraction < 1.5) niceFraction = 1;
            else if (fraction < 3) niceFraction = 2;
            else if (fraction < 7) niceFraction = 5;
            else niceFraction = 10;
        }
        else
        {
            if (fraction <= 1) niceFraction = 1;
            else if (fraction <= 2) niceFraction = 2;
            else if (fraction <= 5) niceFraction = 5;
            else niceFraction = 10;
        }

        return niceFraction * Math.Pow(10, exponent);
    }

    private static double NormalizeZero(double value) => Math.Abs(value) < 0.000001 ? 0 : value;

    private static string Fmt(double value) =>
        double.IsFinite(value)
            ? value.ToString("F1", CultureInfo.InvariantCulture)
            : "0.0";

    private sealed record SegmentState(
        TItem Item,
        int Index,
        string Label,
        double RawValue,
        double StartValue,
        double EndValue,
        double DisplayValue,
        WaterfallStepType StepType,
        string? ExplicitFill,
        string? ExplicitHoverFill,
        string AccessibleLabel);
}
