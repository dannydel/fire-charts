using System.Collections.ObjectModel;
using System.Globalization;
using FireCharts.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FireCharts.Components;

public partial class FireScatterChart<TItem> : ComponentBase
{
    private readonly record struct PointKey(int SeriesIndex, int PointIndex);
    private sealed record InputSeries(int Index, string Name, IReadOnlyList<TItem> Items, string Fill, string HoverFill, double Radius);
    private sealed record RawPoint(
        TItem Item,
        int SeriesIndex,
        string SeriesName,
        int PointIndex,
        string Label,
        LineChartXValue X,
        double Y,
        string Fill,
        string HoverFill,
        double Radius,
        string AccessibleLabel);
    private sealed record AxisTick(double Value, string Label, double Position);

    private sealed class RenderedSeries
    {
        public required int Index { get; init; }
        public required string Name { get; init; }
        public required string Fill { get; init; }
        public required string HoverFill { get; init; }
        public required IReadOnlyList<ScatterChartPoint<TItem>> Points { get; init; }
        public string Key => $"{Index}:{Name}";
    }

    private IReadOnlyList<RenderedSeries> _seriesStates = Array.Empty<RenderedSeries>();
    private IReadOnlyList<AxisTick> _xTicks = Array.Empty<AxisTick>();
    private IReadOnlyList<AxisTick> _yTicks = Array.Empty<AxisTick>();
    private PointKey? _hoveredPointKey;
    private PointKey? _focusedPointKey;
    private int? _legendSeriesIndex;
    private double? _renderWidth;
    private double? _renderHeight;
    private LineChartXValueKind _xAxisKind = LineChartXValueKind.Number;

    [Parameter] public string Title { get; set; } = "Scatter Chart";
    [Parameter] public string Description { get; set; } = "";
    [Parameter] public IReadOnlyList<TItem>? Items { get; set; }
    [Parameter] public IReadOnlyList<ScatterChartSeries<TItem>>? Series { get; set; }
    [Parameter, EditorRequired] public Func<TItem, LineChartXValue>? XValueSelector { get; set; }
    [Parameter, EditorRequired] public Func<TItem, double>? YValueSelector { get; set; }
    [Parameter] public Func<TItem, string>? LabelSelector { get; set; }
    [Parameter] public Func<TItem, string>? TooltipTextSelector { get; set; }
    [Parameter] public Func<TItem, string>? ColorSelector { get; set; }
    [Parameter] public Func<TItem, string>? HoverColorSelector { get; set; }
    [Parameter] public ScatterChartPoint<TItem>? SelectedPoint { get; set; }
    [Parameter] public EventCallback<ScatterChartPoint<TItem>?> SelectedPointChanged { get; set; }
    [Parameter] public EventCallback<ScatterChartPointInteraction<TItem>> OnPointClick { get; set; }
    [Parameter] public EventCallback<ScatterChartPointInteraction<TItem>> OnPointHoverChanged { get; set; }
    [Parameter] public RenderFragment<ScatterChartPoint<TItem>>? TooltipTemplate { get; set; }
    [Parameter] public RenderFragment? EmptyStateTemplate { get; set; }
    [Parameter] public double Width { get; set; } = 640;
    [Parameter] public double Height { get; set; } = 400;
    [Parameter] public bool Responsive { get; set; }
    [Parameter] public bool ShowGridLines { get; set; } = true;
    [Parameter] public bool ShowAxisLabels { get; set; } = true;
    [Parameter] public bool ShowLegend { get; set; } = true;
    [Parameter] public bool ShowTooltip { get; set; } = true;
    [Parameter] public int XAxisTickCount { get; set; } = 5;
    [Parameter] public int YAxisTickCount { get; set; } = 5;
    [Parameter] public string PointColor { get; set; } = "#d94f3d";
    [Parameter] public string HoverColor { get; set; } = "#8f2f1a";
    [Parameter] public string ValueFormat { get; set; } = "F0";
    [Parameter] public double MarkerRadius { get; set; } = 4.8;

    private double PaddingTop => 18;
    private double PaddingRight => 18;
    private double PaddingBottom => ShowAxisLabels ? 48 : 18;
    private double PaddingLeft => ShowAxisLabels ? 64 : 18;

    private IReadOnlyList<RenderedSeries> SeriesStates => _seriesStates;
    private IReadOnlyList<AxisTick> XTicks => _xTicks;
    private IReadOnlyList<AxisTick> YTicks => _yTicks;
    private ScatterChartPoint<TItem>? HoveredPoint => FindPoint(_hoveredPointKey);
    private double SafeWidth => Math.Max(_renderWidth ?? Width, 1);
    private double SafeHeight => Math.Max(_renderHeight ?? Height, 1);
    private double ChartAreaLeft => PaddingLeft;
    private double ChartAreaTop => PaddingTop;
    private double ChartAreaRight => SafeWidth - PaddingRight;
    private double ChartAreaBottom => SafeHeight - PaddingBottom;
    private double ChartAreaWidth => Math.Max(ChartAreaRight - ChartAreaLeft, 1);
    private double ChartAreaHeight => Math.Max(ChartAreaBottom - ChartAreaTop, 1);
    private int SafeXAxisTickCount => Math.Max(XAxisTickCount, 2);
    private int SafeYAxisTickCount => Math.Max(YAxisTickCount, 2);
    private double SafeMarkerRadius => Math.Clamp(MarkerRadius, 2.5, 12);
    private int? ActiveSeriesIndex => _legendSeriesIndex
        ?? HoveredPoint?.SeriesIndex
        ?? FindPoint(_focusedPointKey)?.SeriesIndex
        ?? SelectedPoint?.SeriesIndex;
    private bool HasSeriesDefinitions => Series is { Count: > 0 };

    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(XValueSelector);
        ArgumentNullException.ThrowIfNull(YValueSelector);

        Width = Math.Max(Width, 1);
        Height = Math.Max(Height, 1);
        _renderWidth ??= Width;
        _renderHeight ??= Height;

        RebuildChart();
    }

    private void RebuildChart()
    {
        var inputSeries = BuildInputSeries();
        var rawSeries = BuildRawSeries(inputSeries);

        if (rawSeries.Count == 0)
        {
            _seriesStates = Array.Empty<RenderedSeries>();
            _xTicks = Array.Empty<AxisTick>();
            _yTicks = Array.Empty<AxisTick>();
            return;
        }

        var allPoints = rawSeries.SelectMany(series => series).ToList();
        var xDomain = GetXDomain(allPoints);
        var yScale = GetYScale(allPoints.Select(point => point.Y).ToList(), SafeYAxisTickCount);

        _xTicks = BuildXTicks(allPoints, xDomain.Min, xDomain.Max);
        _yTicks = BuildYTicks(yScale.Min, yScale.Max);

        var states = new List<RenderedSeries>(rawSeries.Count);

        foreach (var series in rawSeries)
        {
            var points = series.Select(point =>
            {
                var key = new PointKey(point.SeriesIndex, point.PointIndex);
                return new ScatterChartPoint<TItem>(
                    point.Item,
                    point.SeriesIndex,
                    point.SeriesName,
                    point.PointIndex,
                    point.Label,
                    point.X,
                    point.Y,
                    new SvgPoint(
                        MapX(point.X.NumericValue, xDomain.Min, xDomain.Max),
                        MapY(point.Y, yScale.Min, yScale.Max)),
                    point.Fill,
                    point.HoverFill,
                    point.Radius,
                    IsSelected(point),
                    _hoveredPointKey == key,
                    _focusedPointKey == key,
                    point.AccessibleLabel);
            }).ToList();

            states.Add(new RenderedSeries
            {
                Index = points[0].SeriesIndex,
                Name = points[0].SeriesName,
                Fill = points[0].Fill,
                HoverFill = points[0].HoverFill,
                Points = new ReadOnlyCollection<ScatterChartPoint<TItem>>(points)
            });
        }

        _seriesStates = new ReadOnlyCollection<RenderedSeries>(states);
        NormalizeInteractionState();
    }

    private List<InputSeries> BuildInputSeries()
    {
        var series = new List<InputSeries>();

        if (Series is { Count: > 0 })
        {
            for (var i = 0; i < Series.Count; i++)
            {
                var definition = Series[i];
                var fill = definition.Color;
                series.Add(new InputSeries(
                    i,
                    string.IsNullOrWhiteSpace(definition.Name) ? $"Series {i + 1}" : definition.Name,
                    definition.Items ?? Array.Empty<TItem>(),
                    fill,
                    definition.HoverColor ?? Darken(fill),
                    Math.Clamp(definition.MarkerRadius ?? SafeMarkerRadius, 2.5, 12)));
            }

            return series;
        }

        series.Add(new InputSeries(
            0,
            Title,
            Items ?? Array.Empty<TItem>(),
            PointColor,
            HoverColor,
            SafeMarkerRadius));

        return series;
    }

    private List<List<RawPoint>> BuildRawSeries(IReadOnlyList<InputSeries> inputSeries)
    {
        var rawSeries = new List<List<RawPoint>>();
        LineChartXValueKind? detectedKind = null;

        foreach (var series in inputSeries)
        {
            var points = new List<RawPoint>();

            for (var itemIndex = 0; itemIndex < series.Items.Count; itemIndex++)
            {
                var item = series.Items[itemIndex];
                var x = XValueSelector!(item);
                var y = YValueSelector!(item);

                if (!double.IsFinite(x.NumericValue) || !double.IsFinite(y))
                {
                    continue;
                }

                if (detectedKind is null)
                {
                    detectedKind = x.Kind;
                }
                else if (detectedKind.Value != x.Kind)
                {
                    throw new InvalidOperationException("FireScatterChart cannot mix numeric and DateTime X values in the same chart.");
                }

                var fill = HasSeriesDefinitions
                    ? series.Fill
                    : ColorSelector?.Invoke(item) ?? series.Fill;
                var hoverFill = HasSeriesDefinitions
                    ? series.HoverFill
                    : HoverColorSelector?.Invoke(item) ?? Darken(fill);
                var label = LabelSelector?.Invoke(item) ?? FormatXValue(x);
                var accessibleText = TooltipTextSelector?.Invoke(item)
                    ?? $"{series.Name} {label}: {y.ToString(ValueFormat, CultureInfo.InvariantCulture)}";

                points.Add(new RawPoint(
                    item,
                    series.Index,
                    series.Name,
                    itemIndex,
                    label,
                    x,
                    y,
                    fill,
                    hoverFill,
                    series.Radius,
                    accessibleText));
            }

            points.Sort(static (left, right) =>
            {
                var compare = left.X.NumericValue.CompareTo(right.X.NumericValue);
                return compare != 0 ? compare : left.PointIndex.CompareTo(right.PointIndex);
            });

            if (points.Count > 0)
            {
                rawSeries.Add(points);
            }
        }

        _xAxisKind = detectedKind ?? LineChartXValueKind.Number;
        return rawSeries;
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
        RebuildChart();
    }

    private async Task HandleHoverAsync(ScatterChartPoint<TItem> point)
    {
        var key = new PointKey(point.SeriesIndex, point.PointIndex);
        if (_hoveredPointKey == key)
        {
            return;
        }

        _hoveredPointKey = key;
        await RefreshPointsAsync();

        if (HoveredPoint is not null)
        {
            await OnPointHoverChanged.InvokeAsync(ToInteraction(HoveredPoint));
        }
    }

    private async Task HandleHoverLeaveAsync(ScatterChartPoint<TItem> point)
    {
        var key = new PointKey(point.SeriesIndex, point.PointIndex);
        if (_hoveredPointKey != key || _focusedPointKey == key)
        {
            return;
        }

        _hoveredPointKey = null;
        await RefreshPointsAsync();
    }

    private async Task HandleFocusAsync(ScatterChartPoint<TItem> point)
    {
        var key = new PointKey(point.SeriesIndex, point.PointIndex);
        _focusedPointKey = key;
        _hoveredPointKey = key;
        await RefreshPointsAsync();

        if (HoveredPoint is not null)
        {
            await OnPointHoverChanged.InvokeAsync(ToInteraction(HoveredPoint));
        }
    }

    private async Task HandleBlurAsync(ScatterChartPoint<TItem> point)
    {
        var key = new PointKey(point.SeriesIndex, point.PointIndex);
        if (_focusedPointKey == key)
        {
            _focusedPointKey = null;
        }

        if (_hoveredPointKey == key)
        {
            _hoveredPointKey = null;
        }

        await RefreshPointsAsync();
    }

    private async Task HandleSelectAsync(ScatterChartPoint<TItem> point)
    {
        SelectedPoint = point;
        await RefreshPointsAsync();
        await SelectedPointChanged.InvokeAsync(point);
        await OnPointClick.InvokeAsync(ToInteraction(point));
    }

    private async Task HandleKeyDownAsync(KeyboardEventArgs args, ScatterChartPoint<TItem> point)
    {
        if (args.Key is "Enter" or " ")
        {
            await HandleSelectAsync(point);
        }
    }

    private void HandleLegendEnter(int seriesIndex)
    {
        _legendSeriesIndex = seriesIndex;
    }

    private void HandleLegendLeave()
    {
        _legendSeriesIndex = null;
    }

    private async Task RefreshPointsAsync()
    {
        RebuildChart();
        await InvokeAsync(StateHasChanged);
    }

    private ScatterChartPoint<TItem>? FindPoint(PointKey? key)
    {
        if (key is null)
        {
            return null;
        }

        foreach (var series in _seriesStates)
        {
            var point = series.Points.FirstOrDefault(candidate =>
                candidate.SeriesIndex == key.Value.SeriesIndex &&
                candidate.PointIndex == key.Value.PointIndex);

            if (point is not null)
            {
                return point;
            }
        }

        return null;
    }

    private void NormalizeInteractionState()
    {
        if (FindPoint(_hoveredPointKey) is null)
        {
            _hoveredPointKey = null;
        }

        if (FindPoint(_focusedPointKey) is null)
        {
            _focusedPointKey = null;
        }
    }

    private bool IsSelected(RawPoint point) =>
        SelectedPoint is not null &&
        SelectedPoint.SeriesIndex == point.SeriesIndex &&
        SelectedPoint.PointIndex == point.PointIndex;

    private static (double Min, double Max) GetXDomain(IReadOnlyList<RawPoint> points)
    {
        var min = points.Min(point => point.X.NumericValue);
        var max = points.Max(point => point.X.NumericValue);

        if (Math.Abs(max - min) < 0.000001)
        {
            var padding = points[0].X.IsDateTime ? 1d : Math.Max(Math.Abs(min) * 0.2, 1d);
            min -= padding;
            max += padding;
        }

        return (min, max);
    }

    private static (double Min, double Max, double Step) GetYScale(IReadOnlyList<double> values, int tickCount)
    {
        var min = values.Min();
        var max = values.Max();

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

            if (max <= 0)
            {
                max = 0;
            }

            if (min >= 0)
            {
                min = 0;
            }
        }

        return GetNiceScale(min, max, tickCount);
    }

    private IReadOnlyList<AxisTick> BuildXTicks(IReadOnlyList<RawPoint> points, double min, double max)
    {
        if (_xAxisKind == LineChartXValueKind.DateTime)
        {
            var values = points
                .Select(point => point.X.NumericValue)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();

            var count = Math.Min(values.Length, SafeXAxisTickCount);
            if (count == 0)
            {
                return Array.Empty<AxisTick>();
            }

            var selected = new SortedSet<double>();
            for (var i = 0; i < count; i++)
            {
                var index = count == 1 ? 0 : (int)Math.Round(i * (values.Length - 1d) / (count - 1d));
                selected.Add(values[index]);
            }

            return new ReadOnlyCollection<AxisTick>(selected
                .Select(value => new AxisTick(value, FormatDateTick(value, min, max), MapX(value, min, max)))
                .ToList());
        }

        var scale = GetNiceScale(min, max, SafeXAxisTickCount);
        return BuildTicks(scale.Min, scale.Max, scale.Step, FormatNumber, value => MapX(value, scale.Min, scale.Max));
    }

    private IReadOnlyList<AxisTick> BuildYTicks(double min, double max)
    {
        var scale = GetNiceScale(min, max, SafeYAxisTickCount);
        return BuildTicks(scale.Min, scale.Max, scale.Step, value => value.ToString(ValueFormat, CultureInfo.InvariantCulture), value => MapY(value, scale.Min, scale.Max));
    }

    private static ReadOnlyCollection<AxisTick> BuildTicks(
        double min,
        double max,
        double step,
        Func<double, string> labelFactory,
        Func<double, double> positionFactory)
    {
        var ticks = new List<AxisTick>();
        var value = min;
        var guard = 0;

        while (value <= max + (step * 0.5) && guard < 100)
        {
            var normalized = NormalizeZero(value);
            ticks.Add(new AxisTick(normalized, labelFactory(normalized), positionFactory(normalized)));
            value += step;
            guard++;
        }

        return new ReadOnlyCollection<AxisTick>(ticks);
    }

    private static (double Min, double Max, double Step) GetNiceScale(double min, double max, int tickCount)
    {
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

    private double MapX(double value, double min, double max)
    {
        var ratio = (value - min) / Math.Max(max - min, 0.000001);
        return ChartAreaLeft + (Math.Clamp(ratio, 0, 1) * ChartAreaWidth);
    }

    private double MapY(double value, double min, double max)
    {
        var ratio = (value - min) / Math.Max(max - min, 0.000001);
        return ChartAreaBottom - (Math.Clamp(ratio, 0, 1) * ChartAreaHeight);
    }

    private string GetTooltipStyle(ScatterChartPoint<TItem> point) =>
        $"left: {Fmt(point.Coordinates.X)}px; top: {Fmt(point.Coordinates.Y)}px;";

    private string GetSeriesClasses(RenderedSeries series)
    {
        var classes = new List<string> { "scatter-series-group" };
        if (ActiveSeriesIndex == series.Index)
        {
            classes.Add("is-active");
        }
        else if (ActiveSeriesIndex.HasValue)
        {
            classes.Add("is-muted");
        }

        return string.Join(" ", classes);
    }

    private static string GetPointClasses(ScatterChartPoint<TItem> point)
    {
        var classes = new List<string> { "scatter-point-group" };
        if (point.IsHovered) classes.Add("is-hovered");
        if (point.IsFocused) classes.Add("is-focused");
        if (point.IsSelected) classes.Add("is-selected");
        return string.Join(" ", classes);
    }

    private string GetLegendItemClasses(RenderedSeries series)
    {
        var classes = new List<string> { "scatter-legend__item" };
        if (ActiveSeriesIndex == series.Index) classes.Add("is-active");
        if (ActiveSeriesIndex.HasValue && ActiveSeriesIndex != series.Index) classes.Add("is-muted");
        return string.Join(" ", classes);
    }

    private static string GetPointStyle(ScatterChartPoint<TItem> point) =>
        $"--point-color: {point.Fill}; --point-hover-color: {point.HoverFill};";

    private static double GetPointRadius(ScatterChartPoint<TItem> point) =>
        point.IsSelected ? point.Radius + 1.8 :
        point.IsHovered || point.IsFocused ? point.Radius + 1.1 :
        point.Radius;

    private ScatterChartPointInteraction<TItem> ToInteraction(ScatterChartPoint<TItem> point) =>
        new(point.Item, point.SeriesIndex, point.SeriesName, point.PointIndex, point.Label, point.X, point.Y);

    private string FormatXValue(LineChartXValue value) =>
        value.Kind == LineChartXValueKind.DateTime
            ? FormatDateTick(value.NumericValue, value.NumericValue, value.NumericValue + 1)
            : FormatNumber(value.NumericValue);

    private static string FormatNumber(double value)
    {
        if (Math.Abs(value % 1) < 0.000001)
        {
            return value.ToString("F0", CultureInfo.InvariantCulture);
        }

        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string FormatDateTick(double value, double min, double max)
    {
        var date = DateTime.FromOADate(value);
        var span = TimeSpan.FromDays(Math.Abs(max - min));

        if (span.TotalDays >= 365 * 2)
        {
            return date.ToString("MMM yyyy", CultureInfo.InvariantCulture);
        }

        if (span.TotalDays >= 3)
        {
            return date.ToString("MMM d", CultureInfo.InvariantCulture);
        }

        if (span.TotalHours >= 12)
        {
            return date.ToString("MMM d HH:mm", CultureInfo.InvariantCulture);
        }

        return date.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    private static string Fmt(double value) =>
        double.IsFinite(value)
            ? value.ToString("F1", CultureInfo.InvariantCulture)
            : "0.0";

    private static double NormalizeZero(double value) =>
        Math.Abs(value) < 0.000001 ? 0 : value;

    private static string Darken(string hex)
    {
        if (hex.Length != 7 || !hex.StartsWith('#'))
        {
            return "#8f2f1a";
        }

        var r = Convert.ToInt32(hex[1..3], 16);
        var g = Convert.ToInt32(hex[3..5], 16);
        var b = Convert.ToInt32(hex[5..7], 16);

        return $"#{Math.Max(r - 28, 0):X2}{Math.Max(g - 28, 0):X2}{Math.Max(b - 28, 0):X2}";
    }
}
