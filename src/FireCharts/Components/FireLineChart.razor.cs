using System.Collections.ObjectModel;
using System.Globalization;
using FireCharts.Interaction;
using FireCharts.Models;
using FireCharts.Scales;
using Microsoft.AspNetCore.Components;

namespace FireCharts.Components;

public partial class FireLineChart<TItem> : ComponentBase
{
    private readonly record struct PointKey(int SeriesIndex, int PointIndex);

    private sealed record InputSeries(
        int Index,
        string Name,
        IReadOnlyList<TItem> Items,
        string Stroke,
        string HoverStroke,
        string Fill,
        double StrokeWidth,
        double AreaOpacity);

    private sealed record RawPoint(
        TItem Item,
        int SeriesIndex,
        string SeriesName,
        int PointIndex,
        string Label,
        LineChartXValue X,
        double Y,
        bool IsHighlighted,
        string Stroke,
        string HoverStroke,
        string Fill,
        string AccessibleLabel);

    private sealed record AxisTick(double Value, string Label, double Position);

    private sealed class RenderedSeries
    {
        public required int Index { get; init; }
        public required string Name { get; init; }
        public required string Stroke { get; init; }
        public required string HoverStroke { get; init; }
        public required string Fill { get; init; }
        public required double StrokeWidth { get; init; }
        public required double AreaOpacity { get; init; }
        public required string LinePathData { get; init; }
        public required string AreaPathData { get; init; }
        public required IReadOnlyList<LineChartPoint<TItem>> Points { get; init; }
        public required IReadOnlyList<LineChartPoint<TItem>> VisiblePoints { get; init; }

        public string Key => $"{Index}:{Name}";
    }

    private IReadOnlyList<RenderedSeries> _seriesStates = Array.Empty<RenderedSeries>();
    private IReadOnlyList<AxisTick> _xTicks = Array.Empty<AxisTick>();
    private IReadOnlyList<AxisTick> _yTicks = Array.Empty<AxisTick>();
    private ChartInteraction<LineChartPoint<TItem>, PointKey> _interaction = default!;
    private int? _legendSeriesIndex;
    private PlotArea _plot;
    private LineChartXValueKind _xAxisKind = LineChartXValueKind.Number;

    [Parameter] public string Title { get; set; } = "Line Chart";
    [Parameter] public string Description { get; set; } = "";
    [Parameter] public IReadOnlyList<TItem>? Items { get; set; }
    [Parameter] public IReadOnlyList<LineChartSeries<TItem>>? Series { get; set; }
    [Parameter, EditorRequired] public Func<TItem, LineChartXValue>? XValueSelector { get; set; }
    [Parameter, EditorRequired] public Func<TItem, double>? YValueSelector { get; set; }
    [Parameter] public Func<TItem, string>? LabelSelector { get; set; }
    [Parameter] public Func<TItem, string>? TooltipTextSelector { get; set; }
    [Parameter] public Func<TItem, bool>? HighlightSelector { get; set; }
    [Parameter] public LineChartPoint<TItem>? SelectedPoint { get; set; }
    [Parameter] public EventCallback<LineChartPoint<TItem>?> SelectedPointChanged { get; set; }
    [Parameter] public EventCallback<LineChartPointInteraction<TItem>> OnPointClick { get; set; }
    [Parameter] public EventCallback<LineChartPointInteraction<TItem>> OnPointHoverChanged { get; set; }
    [Parameter] public RenderFragment<LineChartPoint<TItem>>? TooltipTemplate { get; set; }
    [Parameter] public RenderFragment? EmptyStateTemplate { get; set; }
    [Parameter] public double Width { get; set; } = 640;
    [Parameter] public double Height { get; set; } = 400;
    [Parameter] public bool Responsive { get; set; }
    [Parameter] public LineChartVariant Variant { get; set; } = LineChartVariant.Line;
    [Parameter] public LineInterpolationMode Interpolation { get; set; } = LineInterpolationMode.Smooth;
    [Parameter] public double CurveTension { get; set; } = 0.85;
    [Parameter] public double StrokeWidth { get; set; } = 4;
    [Parameter] public double AreaOpacity { get; set; } = 0.18;
    [Parameter] public bool ShowGridLines { get; set; } = true;
    [Parameter] public bool ShowAxisLabels { get; set; } = true;
    [Parameter] public bool ShowLegend { get; set; } = true;
    [Parameter] public bool ShowTooltip { get; set; } = true;
    [Parameter] public bool ConstrainTooltipToChartBounds { get; set; }
    [Parameter] public LinePointDisplayMode PointDisplayMode { get; set; } = LinePointDisplayMode.HighlightedOnly;
    [Parameter] public int XAxisTickCount { get; set; } = 5;
    [Parameter] public int YAxisTickCount { get; set; } = 5;
    [Parameter] public string LineColor { get; set; } = "#d94f3d";
    [Parameter] public string HoverColor { get; set; } = "#8f2f1a";
    [Parameter] public string FillColor { get; set; } = "#d94f3d";
    [Parameter] public string ValueFormat { get; set; } = "F0";

    private ChartPadding Padding => new(
        Top: 18,
        Right: 18,
        Bottom: ShowAxisLabels ? 48 : 18,
        Left: ShowAxisLabels ? 64 : 18);

    private IReadOnlyList<RenderedSeries> SeriesStates => _seriesStates;
    private IReadOnlyList<AxisTick> XTicks => _xTicks;
    private IReadOnlyList<AxisTick> YTicks => _yTicks;
    private LineChartPoint<TItem>? HoveredPoint => _interaction.Hovered;

    private double SafeCurveTension => Math.Clamp(CurveTension, 0.1, 1);
    private double SafeStrokeWidth => Math.Clamp(StrokeWidth, 1.5, 8);
    private double SafeAreaOpacity => Math.Clamp(AreaOpacity, 0.05, 0.65);
    private int SafeXAxisTickCount => Math.Max(XAxisTickCount, 2);
    private int SafeYAxisTickCount => Math.Max(YAxisTickCount, 2);
    private int? ActiveSeriesIndex => _legendSeriesIndex
        ?? _interaction.Active?.SeriesIndex
        ?? SelectedPoint?.SeriesIndex;

    protected override void OnInitialized()
    {
        _interaction = new ChartInteraction<LineChartPoint<TItem>, PointKey>(new ChartInteractionOptions<LineChartPoint<TItem>, PointKey>
        {
            KeySelector = point => new PointKey(point.SeriesIndex, point.PointIndex),
            RequestRender = () => InvokeAsync(StateHasChanged),
            OnActiveChanged = point => OnPointHoverChanged.InvokeAsync(ToInteraction(point)),
            OnActivate = async point =>
            {
                SelectedPoint = point;
                await InvokeAsync(StateHasChanged);
                await SelectedPointChanged.InvokeAsync(point);
                await OnPointClick.InvokeAsync(ToInteraction(point));
            }
        });
    }

    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(XValueSelector);
        ArgumentNullException.ThrowIfNull(YValueSelector);

        Width = Math.Max(Width, 1);
        Height = Math.Max(Height, 1);
        _plot = PlotArea.FromInset(Width, Height, Padding);

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
            _interaction.SetElements(Array.Empty<LineChartPoint<TItem>>());
            return;
        }

        var allPoints = rawSeries.SelectMany(series => series).ToList();
        var xDomain = GetXDomain(allPoints);
        var yScale = AxisScale.FromValues(allPoints.Select(point => point.Y), SafeYAxisTickCount, _plot.Bottom, _plot.Top);
        var baselineY = yScale.ToPixel(GetAreaBaselineValue(yScale.Min, yScale.Max));

        _xTicks = BuildXTicks(allPoints, xDomain.Min, xDomain.Max);
        _yTicks = yScale.Ticks
            .Select(tick => new AxisTick(tick.Value, tick.Value.ToString(ValueFormat, CultureInfo.InvariantCulture), tick.Pixel))
            .ToArray();

        var states = new List<RenderedSeries>(rawSeries.Count);

        foreach (var series in rawSeries)
        {
            var points = series
                .Select(point =>
                {
                    var key = new PointKey(point.SeriesIndex, point.PointIndex);
                    return new LineChartPoint<TItem>(
                        point.Item,
                        point.SeriesIndex,
                        point.SeriesName,
                        point.PointIndex,
                        point.Label,
                        point.X,
                        point.Y,
                        new SvgPoint(
                            MapX(point.X.NumericValue, xDomain.Min, xDomain.Max),
                            yScale.ToPixel(point.Y)),
                        point.Stroke,
                        point.HoverStroke,
                        point.Fill,
                        point.IsHighlighted,
                        IsSelected(point),
                        false,
                        false,
                        point.AccessibleLabel);
                })
                .ToList();

            var linePath = BuildLinePath(points);
            var areaPath = Variant == LineChartVariant.Area ? BuildAreaPath(points, baselineY) : string.Empty;

            states.Add(new RenderedSeries
            {
                Index = points[0].SeriesIndex,
                Name = points[0].SeriesName,
                Stroke = points[0].Stroke,
                HoverStroke = points[0].HoverStroke,
                Fill = points[0].Fill,
                StrokeWidth = inputSeries[points[0].SeriesIndex].StrokeWidth,
                AreaOpacity = inputSeries[points[0].SeriesIndex].AreaOpacity,
                LinePathData = linePath,
                AreaPathData = areaPath,
                Points = new ReadOnlyCollection<LineChartPoint<TItem>>(points),
                VisiblePoints = new ReadOnlyCollection<LineChartPoint<TItem>>(points)
            });
        }

        _seriesStates = new ReadOnlyCollection<RenderedSeries>(states);
        _interaction.SetElements(states.SelectMany(series => series.Points).ToList());
    }

    private List<InputSeries> BuildInputSeries()
    {
        var series = new List<InputSeries>();

        if (Series is { Count: > 0 })
        {
            for (var i = 0; i < Series.Count; i++)
            {
                var definition = Series[i];
                var stroke = definition.StrokeColor ?? definition.Color;
                series.Add(new InputSeries(
                    i,
                    string.IsNullOrWhiteSpace(definition.Name) ? $"Series {i + 1}" : definition.Name,
                    definition.Items ?? Array.Empty<TItem>(),
                    stroke,
                    definition.HoverColor ?? ChartColor.Darken(stroke),
                    definition.FillColor ?? definition.Color,
                    Math.Clamp(definition.StrokeWidth ?? SafeStrokeWidth, 1.25, 8),
                    Math.Clamp(definition.AreaOpacity ?? SafeAreaOpacity, 0.05, 0.65)));
            }

            return series;
        }

        series.Add(new InputSeries(
            0,
            Title,
            Items ?? Array.Empty<TItem>(),
            LineColor,
            HoverColor,
            FillColor,
            SafeStrokeWidth,
            SafeAreaOpacity));

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
                    throw new InvalidOperationException("FireLineChart cannot mix numeric and DateTime X values in the same chart.");
                }

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
                    HighlightSelector?.Invoke(item) ?? false,
                    series.Stroke,
                    series.HoverStroke,
                    series.Fill,
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

    private Task OnPlotAreaChanged(PlotArea plot)
    {
        _plot = plot;
        RebuildChart();
        return Task.CompletedTask;
    }

    private void HandleLegendEnter(int seriesIndex)
    {
        _legendSeriesIndex = seriesIndex;
    }

    private void HandleLegendLeave()
    {
        _legendSeriesIndex = null;
    }

    private bool ShouldRenderPoint(LineChartPoint<TItem> point) =>
        PointDisplayMode switch
        {
            LinePointDisplayMode.None => false,
            LinePointDisplayMode.All => true,
            _ => point.IsHighlighted || IsSelected(point) || IsHovered(point) || IsFocused(point)
        };

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

        var scale = AxisScale.FromValues(
            new[] { min, max },
            _plot.Left,
            _plot.Right,
            new AxisScaleOptions { TickCount = SafeXAxisTickCount, Baseline = AxisBaseline.DataExtent });
        return scale.Ticks
            .Select(tick => new AxisTick(tick.Value, FormatNumber(tick.Value), tick.Pixel))
            .ToList();
    }

    private string BuildLinePath(IReadOnlyList<LineChartPoint<TItem>> points)
    {
        if (points.Count == 0)
        {
            return string.Empty;
        }

        return Interpolation switch
        {
            LineInterpolationMode.Linear => BuildLinearPath(points),
            LineInterpolationMode.Step => BuildStepPath(points),
            _ => BuildSmoothPath(points)
        };
    }

    private string BuildAreaPath(IReadOnlyList<LineChartPoint<TItem>> points, double baselineY)
    {
        if (points.Count == 0)
        {
            return string.Empty;
        }

        var linePath = BuildLinePath(points);
        var first = points[0].Coordinates;
        var last = points[^1].Coordinates;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{linePath} L {Fmt(last.X)} {Fmt(baselineY)} L {Fmt(first.X)} {Fmt(baselineY)} Z");
    }

    private string BuildLinearPath(IReadOnlyList<LineChartPoint<TItem>> points)
    {
        var segments = new List<string> { $"M {Fmt(points[0].Coordinates.X)} {Fmt(points[0].Coordinates.Y)}" };

        for (var i = 1; i < points.Count; i++)
        {
            segments.Add($"L {Fmt(points[i].Coordinates.X)} {Fmt(points[i].Coordinates.Y)}");
        }

        return string.Join(" ", segments);
    }

    private string BuildStepPath(IReadOnlyList<LineChartPoint<TItem>> points)
    {
        var segments = new List<string> { $"M {Fmt(points[0].Coordinates.X)} {Fmt(points[0].Coordinates.Y)}" };

        for (var i = 1; i < points.Count; i++)
        {
            segments.Add($"L {Fmt(points[i].Coordinates.X)} {Fmt(points[i - 1].Coordinates.Y)}");
            segments.Add($"L {Fmt(points[i].Coordinates.X)} {Fmt(points[i].Coordinates.Y)}");
        }

        return string.Join(" ", segments);
    }

    private string BuildSmoothPath(IReadOnlyList<LineChartPoint<TItem>> points)
    {
        if (points.Count < 3)
        {
            return BuildLinearPath(points);
        }

        var segments = new List<string> { $"M {Fmt(points[0].Coordinates.X)} {Fmt(points[0].Coordinates.Y)}" };
        var tension = SafeCurveTension;

        for (var i = 0; i < points.Count - 1; i++)
        {
            var p0 = i == 0 ? points[i].Coordinates : points[i - 1].Coordinates;
            var p1 = points[i].Coordinates;
            var p2 = points[i + 1].Coordinates;
            var p3 = i + 2 < points.Count ? points[i + 2].Coordinates : points[i + 1].Coordinates;

            var controlPoint1 = new SvgPoint(
                p1.X + ((p2.X - p0.X) / 6d * tension),
                p1.Y + ((p2.Y - p0.Y) / 6d * tension));

            var controlPoint2 = new SvgPoint(
                p2.X - ((p3.X - p1.X) / 6d * tension),
                p2.Y - ((p3.Y - p1.Y) / 6d * tension));

            segments.Add(
                $"C {Fmt(controlPoint1.X)} {Fmt(controlPoint1.Y)} {Fmt(controlPoint2.X)} {Fmt(controlPoint2.Y)} {Fmt(p2.X)} {Fmt(p2.Y)}");
        }

        return string.Join(" ", segments);
    }

    private double MapX(double value, double min, double max)
    {
        var ratio = (value - min) / Math.Max(max - min, 0.000001);
        return _plot.Left + (Math.Clamp(ratio, 0, 1) * _plot.Width);
    }

    private static double GetAreaBaselineValue(double min, double max)
    {
        if (min <= 0 && max >= 0)
        {
            return 0;
        }

        return max < 0 ? 0 : min;
    }

    private string GetTooltipStyle(LineChartPoint<TItem> point) =>
        $"left: {Fmt(point.Coordinates.X)}px; top: {Fmt(point.Coordinates.Y)}px;";

    private IReadOnlyList<LineChartPoint<TItem>> GetVisiblePoints(RenderedSeries series) =>
        PointDisplayMode == LinePointDisplayMode.All
            ? series.Points
            : series.Points.Where(ShouldRenderPoint).ToArray();

    private string GetSeriesClasses(RenderedSeries series)
    {
        var classes = new List<string> { "line-series-group" };
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

    private string GetPointClasses(LineChartPoint<TItem> point)
    {
        var classes = new List<string> { "line-point-group" };
        if (point.IsHighlighted) classes.Add("is-highlighted");
        if (IsHovered(point)) classes.Add("is-hovered");
        if (IsFocused(point)) classes.Add("is-focused");
        if (IsSelected(point)) classes.Add("is-selected");
        return string.Join(" ", classes);
    }

    private string GetLegendItemClasses(RenderedSeries series)
    {
        var classes = new List<string> { "line-legend__item" };
        if (ActiveSeriesIndex == series.Index) classes.Add("is-active");
        if (ActiveSeriesIndex.HasValue && ActiveSeriesIndex != series.Index) classes.Add("is-muted");
        return string.Join(" ", classes);
    }

    private string GetPathStyle(RenderedSeries series) =>
        $"--series-color: {series.Stroke}; --series-hover-color: {series.HoverStroke}; --series-stroke-width: {Fmt(series.StrokeWidth)};";

    private string GetAreaStyle(RenderedSeries series) =>
        $"--series-fill: {series.Fill}; --series-area-opacity: {series.AreaOpacity.ToString("0.###", CultureInfo.InvariantCulture)};";

    private static string GetPointStyle(LineChartPoint<TItem> point) =>
        $"--point-color: {point.Stroke}; --point-hover-color: {point.HoverStroke};";

    private double GetPointRadius(LineChartPoint<TItem> point) =>
        IsSelected(point) ? 5.1 :
        IsHovered(point) || IsFocused(point) ? 4.6 :
        point.IsHighlighted ? 4 : 3.4;

    private bool IsSelected(LineChartPoint<TItem> point) =>
        SelectedPoint is not null &&
        SelectedPoint.SeriesIndex == point.SeriesIndex &&
        SelectedPoint.PointIndex == point.PointIndex;

    private bool IsHovered(LineChartPoint<TItem> point) => _interaction.IsHovered(point);

    private bool IsFocused(LineChartPoint<TItem> point) => _interaction.IsFocused(point);

    private LineChartPointInteraction<TItem> ToInteraction(LineChartPoint<TItem> point) =>
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

    private static string Fmt(double value) => ChartFormat.Fmt(value);

}
