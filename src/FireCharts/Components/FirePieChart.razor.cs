using FireCharts.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FireCharts.Components;

public partial class FirePieChart<TItem> : ComponentBase
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

    private IReadOnlyList<PieChartPoint<TItem>> _points = Array.Empty<PieChartPoint<TItem>>();
    private int? _hoveredIndex;
    private int? _focusedIndex;
    private double? _renderWidth;
    private double? _renderHeight;

    [Parameter] public string Title { get; set; } = "Pie Chart";
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
    [Parameter] public RenderFragment<PieChartPoint<TItem>>? PointLabelTemplate { get; set; }
    [Parameter] public RenderFragment<PieChartPoint<TItem>>? TooltipTemplate { get; set; }
    [Parameter] public RenderFragment? EmptyStateTemplate { get; set; }
    [Parameter] public double Width { get; set; } = 520;
    [Parameter] public double Height { get; set; } = 420;
    [Parameter] public bool Responsive { get; set; }
    [Parameter] public bool ShowSliceLabels { get; set; } = true;
    [Parameter] public PieChartLabelMode LabelMode { get; set; } = PieChartLabelMode.Overlay;
    [Parameter] public bool ShowTooltip { get; set; } = true;
    [Parameter] public bool ShowCenterLabel { get; set; } = true;
    [Parameter] public bool ShowGuideRing { get; set; } = true;
    [Parameter] public string SliceColor { get; set; } = "#d94f3d";
    [Parameter] public string HoverColor { get; set; } = "#a73728";
    [Parameter] public string ValueFormat { get; set; } = "F0";
    [Parameter] public double InnerRadiusRatio { get; set; } = 0;
    [Parameter] public double MinimumLabelPercentage { get; set; } = 0.08;
    [Parameter] public double ActiveOffset { get; set; } = 10;
    [Parameter] public string? CenterLabelTitle { get; set; }

    internal IReadOnlyList<PieChartPoint<TItem>> Points => _points;
    internal PieChartPoint<TItem>? HoveredPoint => _hoveredIndex is int index && index >= 0 && index < _points.Count ? _points[index] : null;
    internal double SafeWidth => Math.Max(_renderWidth ?? Width, 1);
    internal double SafeHeight => Math.Max(_renderHeight ?? Height, 1);
    internal SvgPoint Center => new(SafeWidth / 2, SafeHeight / 2);
    internal double Radius => Math.Max(Math.Min(SafeWidth, SafeHeight) / 2 - 26, 40);
    internal double InnerRadius => Radius * Math.Clamp(InnerRadiusRatio, 0, 0.75);
    internal bool HasInnerHole => InnerRadius > 0;
    internal double CenterLabelOuterRadius => Math.Max(InnerRadius > 0 ? InnerRadius - 8 : Radius * 0.34, 34);
    internal double CenterLabelInnerRadius => Math.Max(CenterLabelOuterRadius - 8, 26);
    internal string CenterLabelValue => HoveredPoint is null
        ? TotalValue.ToString(ValueFormat, CultureInfo.InvariantCulture)
        : HoveredPoint.Value.ToString(ValueFormat, CultureInfo.InvariantCulture);

    private double TotalValue =>
        (Items ?? Array.Empty<TItem>())
            .Select(ValueSelectorOrThrow)
            .Select(SanitizeValue)
            .DefaultIfEmpty(0)
            .Sum();

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
        var totalValue = items.Select(ValueSelectorOrThrow).Select(SanitizeValue).Sum();
        var comparer = EqualityComparer<TItem>.Default;
        var selectedItem = SelectedItem;
        var startAngle = -Math.PI / 2;

        var points = new List<PieChartPoint<TItem>>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var value = SanitizeValue(ValueSelectorOrThrow(item));
            if (value <= 0 || totalValue <= 0)
            {
                continue;
            }

            var sliceAngle = value / totalValue * Math.PI * 2;
            var endAngle = startAngle + sliceAngle;
            var label = LabelSelectorOrThrow(item);
            var isSelected = comparer.Equals(item, selectedItem);
            var isActive = isSelected || _hoveredIndex == i || _focusedIndex == i;
            var offset = GetOffset(startAngle, endAngle, isActive ? ActiveOffset : 0);
            var path = BuildSlicePath(startAngle, endAngle);
            var percentage = value / totalValue;
            var labelRadius = InnerRadius > 0
                ? (InnerRadius + Radius) / 2
                : Radius * 0.62;
            var labelPoint = PolarToPoint((startAngle + endAngle) / 2, labelRadius);
            var tooltipPoint = PolarToPoint((startAngle + endAngle) / 2, Radius + 8);
            var fill = ColorSelector?.Invoke(item) ?? DefaultPalette[i % DefaultPalette.Length];
            var useDarkLabelText = UseDarkText(fill);
            points.Add(new PieChartPoint<TItem>(
                item,
                i,
                label,
                value,
                percentage,
                fill,
                HoverColorSelector?.Invoke(item) ?? Darken(fill),
                path,
                new SvgPoint(labelPoint.X + offset.X, labelPoint.Y + offset.Y),
                new SvgPoint(tooltipPoint.X + offset.X, tooltipPoint.Y + offset.Y),
                offset,
                useDarkLabelText,
                $"{label}: {value.ToString(ValueFormat, CultureInfo.InvariantCulture)} ({percentage.ToString("P0", CultureInfo.InvariantCulture)})",
                isSelected,
                _hoveredIndex == i,
                _focusedIndex == i));

            startAngle = endAngle;
        }

        _points = new ReadOnlyCollection<PieChartPoint<TItem>>(points);

        if (_hoveredIndex is int hovered && _points.All(point => point.Index != hovered))
        {
            _hoveredIndex = null;
        }

        if (_focusedIndex is int focused && _points.All(point => point.Index != focused))
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

    private string BuildSlicePath(double startAngle, double endAngle)
    {
        var outerStart = PolarToPoint(startAngle, Radius);
        var outerEnd = PolarToPoint(endAngle, Radius);
        var largeArc = endAngle - startAngle > Math.PI ? 1 : 0;

        if (InnerRadius <= 0)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"M {Fmt(Center.X)} {Fmt(Center.Y)} L {Fmt(outerStart.X)} {Fmt(outerStart.Y)} A {Fmt(Radius)} {Fmt(Radius)} 0 {largeArc} 1 {Fmt(outerEnd.X)} {Fmt(outerEnd.Y)} Z");
        }

        var innerEnd = PolarToPoint(endAngle, InnerRadius);
        var innerStart = PolarToPoint(startAngle, InnerRadius);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"M {Fmt(outerStart.X)} {Fmt(outerStart.Y)} A {Fmt(Radius)} {Fmt(Radius)} 0 {largeArc} 1 {Fmt(outerEnd.X)} {Fmt(outerEnd.Y)} L {Fmt(innerEnd.X)} {Fmt(innerEnd.Y)} A {Fmt(InnerRadius)} {Fmt(InnerRadius)} 0 {largeArc} 0 {Fmt(innerStart.X)} {Fmt(innerStart.Y)} Z");
    }

    private SvgPoint PolarToPoint(double angle, double radius) =>
        new(
            Center.X + Math.Cos(angle) * radius,
            Center.Y + Math.Sin(angle) * radius);

    private static SvgPoint GetOffset(double startAngle, double endAngle, double distance)
    {
        if (distance <= 0)
        {
            return new SvgPoint(0, 0);
        }

        var midAngle = (startAngle + endAngle) / 2;
        return new SvgPoint(Math.Cos(midAngle) * distance, Math.Sin(midAngle) * distance);
    }

    private async Task HandleHoverAsync(PieChartPoint<TItem> point)
    {
        if (_hoveredIndex == point.Index)
        {
            return;
        }

        _hoveredIndex = point.Index;
        RebuildPoints();
        await InvokeAsync(StateHasChanged);
        await OnPointHoverChanged.InvokeAsync(ToInteraction(HoveredPoint!));
    }

    private async Task HandleHoverLeaveAsync(PieChartPoint<TItem> point)
    {
        if (_hoveredIndex != point.Index || _focusedIndex == point.Index)
        {
            return;
        }

        _hoveredIndex = null;
        RebuildPoints();
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleFocusAsync(PieChartPoint<TItem> point)
    {
        _focusedIndex = point.Index;
        _hoveredIndex = point.Index;
        RebuildPoints();
        await InvokeAsync(StateHasChanged);
        await OnPointHoverChanged.InvokeAsync(ToInteraction(HoveredPoint!));
    }

    private async Task HandleBlurAsync(PieChartPoint<TItem> point)
    {
        if (_focusedIndex == point.Index)
        {
            _focusedIndex = null;
        }

        if (_hoveredIndex == point.Index)
        {
            _hoveredIndex = null;
        }

        RebuildPoints();
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleSelectAsync(PieChartPoint<TItem> point)
    {
        SelectedItem = point.Item;
        RebuildPoints();
        await InvokeAsync(StateHasChanged);
        await SelectedItemChanged.InvokeAsync(point.Item);
        await OnPointClick.InvokeAsync(ToInteraction(point));
    }

    private async Task HandleKeyDownAsync(KeyboardEventArgs args, PieChartPoint<TItem> point)
    {
        if (args.Key is "Enter" or " ")
        {
            await HandleSelectAsync(point);
        }
    }

    private string GetTooltipStyle(PieChartPoint<TItem> point) =>
        $"left: {Fmt(point.TooltipAnchor.X)}px; top: {Fmt(point.TooltipAnchor.Y)}px;";

    private string GetPointClasses(PieChartPoint<TItem> point)
    {
        var classes = new List<string> { "pie-slice-group" };
        if (point.IsHovered) classes.Add("is-hovered");
        if (point.IsFocused) classes.Add("is-focused");
        if (point.IsSelected) classes.Add("is-selected");
        return string.Join(" ", classes);
    }

    private static string GetLegendItemClasses(PieChartPoint<TItem> point)
    {
        var classes = new List<string> { "pie-legend__item" };
        if (point.IsHovered || point.IsFocused) classes.Add("is-active");
        if (point.IsSelected) classes.Add("is-selected");
        return string.Join(" ", classes);
    }

    private ChartPointInteraction<TItem> ToInteraction(PieChartPoint<TItem> point) =>
        new(point.Item, point.Index, point.Label, point.Value);

    private static double GetOverlayLabelWidth(PieChartPoint<TItem> point)
    {
        var labelWidth = Math.Max(point.Label.Length * 6.4, 42);
        return Math.Clamp(labelWidth + 22, 66, 108);
    }

    private double ValueSelectorOrThrow(TItem item) => ValueSelector!(item);

    private string LabelSelectorOrThrow(TItem item) => LabelSelector!(item);

    private static double SanitizeValue(double value) =>
        double.IsFinite(value) ? Math.Max(value, 0) : 0;

    private static string Fmt(double value) =>
        double.IsFinite(value)
            ? value.ToString("F1", CultureInfo.InvariantCulture)
            : "0.0";

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

    private static bool UseDarkText(string hex)
    {
        if (hex.Length != 7 || !hex.StartsWith('#'))
        {
            return false;
        }

        var r = Convert.ToInt32(hex[1..3], 16) / 255d;
        var g = Convert.ToInt32(hex[3..5], 16) / 255d;
        var b = Convert.ToInt32(hex[5..7], 16) / 255d;

        var luminance = (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
        return luminance >= 0.62;
    }
}
