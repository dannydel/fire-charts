using System.Collections.ObjectModel;
using System.Globalization;
using FireCharts.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FireCharts.Components;

public partial class FireHeatmapChart<TItem> : ComponentBase
{
    private readonly record struct CellKey(int RowIndex, int ColumnIndex);
    private sealed record RawCell(
        TItem Item,
        string RowKey,
        string RowLabel,
        double RowOrder,
        string ColumnKey,
        string ColumnLabel,
        double ColumnOrder,
        double Value,
        string? ExplicitColor,
        string AccessibleLabel);
    private sealed record RowDefinition(string Key, string Label, double Order, int Index, double Y, double Height)
    {
        public double CenterY => Y + (Height / 2);
    }
    private sealed record ColumnDefinition(string Key, string Label, double Order, int Index, double X, double Width)
    {
        public double CenterX => X + (Width / 2);
    }
    private sealed record PlaceholderCell(SvgRect Rect, string Fill);

    private IReadOnlyList<HeatmapCell<TItem>> _cells = Array.Empty<HeatmapCell<TItem>>();
    private IReadOnlyList<PlaceholderCell> _placeholderCells = Array.Empty<PlaceholderCell>();
    private IReadOnlyList<RowDefinition> _rows = Array.Empty<RowDefinition>();
    private IReadOnlyList<ColumnDefinition> _columns = Array.Empty<ColumnDefinition>();
    private CellKey? _hoveredCellKey;
    private CellKey? _focusedCellKey;
    private double? _renderWidth;
    private double? _renderHeight;
    private double _minValue;
    private double _maxValue;

    [Parameter] public string Title { get; set; } = "Heatmap Chart";
    [Parameter] public string Description { get; set; } = "";
    [Parameter] public IReadOnlyList<TItem>? Items { get; set; }
    [Parameter, EditorRequired] public Func<TItem, string>? ColumnKeySelector { get; set; }
    [Parameter, EditorRequired] public Func<TItem, string>? ColumnLabelSelector { get; set; }
    [Parameter, EditorRequired] public Func<TItem, string>? RowKeySelector { get; set; }
    [Parameter, EditorRequired] public Func<TItem, string>? RowLabelSelector { get; set; }
    [Parameter, EditorRequired] public Func<TItem, double>? ValueSelector { get; set; }
    [Parameter] public Func<TItem, double>? ColumnOrderSelector { get; set; }
    [Parameter] public Func<TItem, double>? RowOrderSelector { get; set; }
    [Parameter] public Func<TItem, string>? TooltipTextSelector { get; set; }
    [Parameter] public Func<TItem, string>? ColorSelector { get; set; }
    [Parameter] public TItem? SelectedItem { get; set; }
    [Parameter] public EventCallback<TItem?> SelectedItemChanged { get; set; }
    [Parameter] public EventCallback<HeatmapCellInteraction<TItem>> OnCellClick { get; set; }
    [Parameter] public EventCallback<HeatmapCellInteraction<TItem>> OnCellHoverChanged { get; set; }
    [Parameter] public RenderFragment<HeatmapCell<TItem>>? TooltipTemplate { get; set; }
    [Parameter] public RenderFragment? EmptyStateTemplate { get; set; }
    [Parameter] public double Width { get; set; } = 760;
    [Parameter] public double Height { get; set; } = 420;
    [Parameter] public bool Responsive { get; set; }
    [Parameter] public bool ShowTooltip { get; set; } = true;
    [Parameter] public bool ShowAxisLabels { get; set; } = true;
    [Parameter] public bool ShowLegend { get; set; } = true;
    [Parameter] public double CellGap { get; set; } = 3;
    [Parameter] public double CornerRadius { get; set; } = 4;
    [Parameter] public string LowColor { get; set; } = "#fff0d7";
    [Parameter] public string HighColor { get; set; } = "#d94f3d";
    [Parameter] public string MissingCellColor { get; set; } = "#f5ece4";
    [Parameter] public string ValueFormat { get; set; } = "F0";

    private double PaddingTop => 18;
    private double PaddingRight => 18;
    private double PaddingBottom => ShowAxisLabels ? 62 : 18;
    private double PaddingLeft => ShowAxisLabels ? 92 : 18;

    private IReadOnlyList<HeatmapCell<TItem>> Cells => _cells;
    private IReadOnlyList<PlaceholderCell> PlaceholderCells => _placeholderCells;
    private IReadOnlyList<RowDefinition> Rows => _rows;
    private IReadOnlyList<ColumnDefinition> Columns => _columns;
    private HeatmapCell<TItem>? HoveredCell => FindCell(_hoveredCellKey);
    private double SafeWidth => Math.Max(_renderWidth ?? Width, 1);
    private double SafeHeight => Math.Max(_renderHeight ?? Height, 1);
    private double ChartAreaLeft => PaddingLeft;
    private double ChartAreaTop => PaddingTop;
    private double ChartAreaRight => SafeWidth - PaddingRight;
    private double ChartAreaBottom => SafeHeight - PaddingBottom;
    private double ChartAreaWidth => Math.Max(ChartAreaRight - ChartAreaLeft, 1);
    private double ChartAreaHeight => Math.Max(ChartAreaBottom - ChartAreaTop, 1);
    private double SafeCellGap => Math.Clamp(CellGap, 0, 12);
    private double SafeCornerRadius => Math.Clamp(CornerRadius, 0, 16);
    private string LegendMinLabel => _minValue.ToString(ValueFormat, CultureInfo.InvariantCulture);
    private string LegendMaxLabel => _maxValue.ToString(ValueFormat, CultureInfo.InvariantCulture);
    private string LegendScaleStyle => $"--legend-low: {LowColor}; --legend-high: {HighColor};";

    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(ColumnKeySelector);
        ArgumentNullException.ThrowIfNull(ColumnLabelSelector);
        ArgumentNullException.ThrowIfNull(RowKeySelector);
        ArgumentNullException.ThrowIfNull(RowLabelSelector);
        ArgumentNullException.ThrowIfNull(ValueSelector);

        Width = Math.Max(Width, 1);
        Height = Math.Max(Height, 1);
        _renderWidth ??= Width;
        _renderHeight ??= Height;

        RebuildChart();
    }

    private void RebuildChart()
    {
        var rawCells = BuildRawCells();
        if (rawCells.Count == 0)
        {
            _cells = Array.Empty<HeatmapCell<TItem>>();
            _placeholderCells = Array.Empty<PlaceholderCell>();
            _rows = Array.Empty<RowDefinition>();
            _columns = Array.Empty<ColumnDefinition>();
            _minValue = 0;
            _maxValue = 0;
            _hoveredCellKey = null;
            _focusedCellKey = null;
            return;
        }

        _minValue = rawCells.Min(cell => cell.Value);
        _maxValue = rawCells.Max(cell => cell.Value);

        var rows = BuildRows(rawCells);
        var columns = BuildColumns(rawCells);
        var cellsByKey = new Dictionary<(string RowKey, string ColumnKey), RawCell>();

        foreach (var cell in rawCells)
        {
            cellsByKey[(cell.RowKey, cell.ColumnKey)] = cell;
        }

        var placeholderCells = new List<PlaceholderCell>(rows.Count * columns.Count);
        var renderedCells = new List<HeatmapCell<TItem>>(rawCells.Count);
        var comparer = EqualityComparer<TItem>.Default;

        foreach (var row in rows)
        {
            foreach (var column in columns)
            {
                var rect = GetCellRect(row, column);
                if (!cellsByKey.TryGetValue((row.Key, column.Key), out var rawCell))
                {
                    placeholderCells.Add(new PlaceholderCell(rect, MissingCellColor));
                    continue;
                }

                var key = new CellKey(row.Index, column.Index);
                var fill = rawCell.ExplicitColor ?? InterpolateColor(rawCell.Value, _minValue, _maxValue, LowColor, HighColor);
                renderedCells.Add(new HeatmapCell<TItem>(
                    rawCell.Item,
                    row.Index,
                    column.Index,
                    row.Key,
                    row.Label,
                    column.Key,
                    column.Label,
                    rawCell.Value,
                    rect,
                    fill,
                    comparer.Equals(rawCell.Item, SelectedItem),
                    _hoveredCellKey == key,
                    _focusedCellKey == key,
                    rawCell.AccessibleLabel));
            }
        }

        _rows = new ReadOnlyCollection<RowDefinition>(rows);
        _columns = new ReadOnlyCollection<ColumnDefinition>(columns);
        _placeholderCells = new ReadOnlyCollection<PlaceholderCell>(placeholderCells);
        _cells = new ReadOnlyCollection<HeatmapCell<TItem>>(renderedCells);
        NormalizeInteractionState();
    }

    private List<RawCell> BuildRawCells()
    {
        var rawCells = new List<RawCell>();

        foreach (var item in Items ?? Array.Empty<TItem>())
        {
            var rawValue = ValueSelector!(item);
            if (!double.IsFinite(rawValue))
            {
                continue;
            }

            var value = Math.Max(0, rawValue);
            var rowKey = RowKeySelector!(item) ?? string.Empty;
            var rowLabel = RowLabelSelector!(item) ?? rowKey;
            var columnKey = ColumnKeySelector!(item) ?? string.Empty;
            var columnLabel = ColumnLabelSelector!(item) ?? columnKey;
            var accessibleLabel = TooltipTextSelector?.Invoke(item)
                ?? $"{rowLabel}, {columnLabel}: {value.ToString(ValueFormat, CultureInfo.InvariantCulture)}";

            rawCells.Add(new RawCell(
                item,
                rowKey,
                rowLabel,
                SanitizeOrder(RowOrderSelector?.Invoke(item), rawCells.Count),
                columnKey,
                columnLabel,
                SanitizeOrder(ColumnOrderSelector?.Invoke(item), rawCells.Count),
                value,
                ColorSelector?.Invoke(item),
                accessibleLabel));
        }

        return rawCells;
    }

    private List<RowDefinition> BuildRows(IReadOnlyList<RawCell> rawCells)
    {
        var grouped = rawCells
            .GroupBy(cell => cell.RowKey, StringComparer.Ordinal)
            .Select(group =>
            {
                var first = group.First();
                return new
                {
                    first.RowKey,
                    first.RowLabel,
                    Order = group.Min(item => item.RowOrder)
                };
            })
            .OrderBy(entry => entry.Order)
            .ThenBy(entry => entry.RowLabel, StringComparer.Ordinal)
            .ToList();

        var rowHeight = ChartAreaHeight / grouped.Count;
        var rows = new List<RowDefinition>(grouped.Count);
        for (var index = 0; index < grouped.Count; index++)
        {
            var row = grouped[index];
            rows.Add(new RowDefinition(
                row.RowKey,
                row.RowLabel,
                row.Order,
                index,
                ChartAreaTop + (index * rowHeight),
                rowHeight));
        }

        return rows;
    }

    private List<ColumnDefinition> BuildColumns(IReadOnlyList<RawCell> rawCells)
    {
        var grouped = rawCells
            .GroupBy(cell => cell.ColumnKey, StringComparer.Ordinal)
            .Select(group =>
            {
                var first = group.First();
                return new
                {
                    first.ColumnKey,
                    first.ColumnLabel,
                    Order = group.Min(item => item.ColumnOrder)
                };
            })
            .OrderBy(entry => entry.Order)
            .ThenBy(entry => entry.ColumnLabel, StringComparer.Ordinal)
            .ToList();

        var columnWidth = ChartAreaWidth / grouped.Count;
        var columns = new List<ColumnDefinition>(grouped.Count);
        for (var index = 0; index < grouped.Count; index++)
        {
            var column = grouped[index];
            columns.Add(new ColumnDefinition(
                column.ColumnKey,
                column.ColumnLabel,
                column.Order,
                index,
                ChartAreaLeft + (index * columnWidth),
                columnWidth));
        }

        return columns;
    }

    private SvgRect GetCellRect(RowDefinition row, ColumnDefinition column)
    {
        var gap = SafeCellGap;
        var x = column.X + (gap / 2);
        var y = row.Y + (gap / 2);
        var width = Math.Max(column.Width - gap, 1);
        var height = Math.Max(row.Height - gap, 1);
        return new SvgRect(x, y, width, height);
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

    private async Task HandleHoverAsync(HeatmapCell<TItem> cell)
    {
        var key = new CellKey(cell.RowIndex, cell.ColumnIndex);
        if (_hoveredCellKey == key)
        {
            return;
        }

        _hoveredCellKey = key;
        await RefreshCellsAsync();

        if (HoveredCell is not null)
        {
            await OnCellHoverChanged.InvokeAsync(ToInteraction(HoveredCell));
        }
    }

    private async Task HandleHoverLeaveAsync(HeatmapCell<TItem> cell)
    {
        var key = new CellKey(cell.RowIndex, cell.ColumnIndex);
        if (_hoveredCellKey != key || _focusedCellKey == key)
        {
            return;
        }

        _hoveredCellKey = null;
        await RefreshCellsAsync();
    }

    private async Task HandleFocusAsync(HeatmapCell<TItem> cell)
    {
        var key = new CellKey(cell.RowIndex, cell.ColumnIndex);
        _focusedCellKey = key;
        _hoveredCellKey = key;
        await RefreshCellsAsync();

        if (HoveredCell is not null)
        {
            await OnCellHoverChanged.InvokeAsync(ToInteraction(HoveredCell));
        }
    }

    private async Task HandleBlurAsync(HeatmapCell<TItem> cell)
    {
        var key = new CellKey(cell.RowIndex, cell.ColumnIndex);
        if (_focusedCellKey == key)
        {
            _focusedCellKey = null;
        }

        if (_hoveredCellKey == key)
        {
            _hoveredCellKey = null;
        }

        await RefreshCellsAsync();
    }

    private async Task HandleSelectAsync(HeatmapCell<TItem> cell)
    {
        SelectedItem = cell.Item;
        await RefreshCellsAsync();
        await SelectedItemChanged.InvokeAsync(cell.Item);
        await OnCellClick.InvokeAsync(ToInteraction(cell));
    }

    private async Task HandleKeyDownAsync(KeyboardEventArgs args, HeatmapCell<TItem> cell)
    {
        if (args.Key is "Enter" or " ")
        {
            await HandleSelectAsync(cell);
        }
    }

    private async Task RefreshCellsAsync()
    {
        RebuildChart();
        await InvokeAsync(StateHasChanged);
    }

    private HeatmapCell<TItem>? FindCell(CellKey? key)
    {
        if (key is null)
        {
            return null;
        }

        return _cells.FirstOrDefault(cell =>
            cell.RowIndex == key.Value.RowIndex &&
            cell.ColumnIndex == key.Value.ColumnIndex);
    }

    private void NormalizeInteractionState()
    {
        if (FindCell(_hoveredCellKey) is null)
        {
            _hoveredCellKey = null;
        }

        if (FindCell(_focusedCellKey) is null)
        {
            _focusedCellKey = null;
        }
    }

    private HeatmapCellInteraction<TItem> ToInteraction(HeatmapCell<TItem> cell) =>
        new(
            cell.Item,
            cell.RowIndex,
            cell.ColumnIndex,
            cell.RowKey,
            cell.RowLabel,
            cell.ColumnKey,
            cell.ColumnLabel,
            cell.Value);

    private string GetCellClasses(HeatmapCell<TItem> cell)
    {
        var classes = new List<string> { "heatmap-cell-group" };

        if (cell.IsHovered)
        {
            classes.Add("is-hovered");
        }

        if (cell.IsFocused)
        {
            classes.Add("is-focused");
        }

        if (cell.IsSelected)
        {
            classes.Add("is-selected");
        }

        return string.Join(" ", classes);
    }

    private static string GetCellStyle(HeatmapCell<TItem> cell) =>
        $"--cell-color: {cell.Fill};";

    private string GetTooltipStyle(HeatmapCell<TItem> cell)
    {
        var left = cell.Rect.X + (cell.Rect.Width / 2);
        var top = Math.Max(cell.Rect.Y, 0);
        return $"left: {Fmt(left)}px; top: {Fmt(top)}px;";
    }

    private static double SanitizeOrder(double? value, int fallback)
    {
        if (value.HasValue && double.IsFinite(value.Value))
        {
            return value.Value;
        }

        return fallback;
    }

    private static string InterpolateColor(double value, double min, double max, string lowColor, string highColor)
    {
        if (!TryParseColor(lowColor, out var low) || !TryParseColor(highColor, out var high))
        {
            return highColor;
        }

        var ratio = Math.Abs(max - min) < 0.000001
            ? 1
            : Math.Clamp((value - min) / (max - min), 0, 1);

        var r = (int)Math.Round((low.R * (1 - ratio)) + (high.R * ratio));
        var g = (int)Math.Round((low.G * (1 - ratio)) + (high.G * ratio));
        var b = (int)Math.Round((low.B * (1 - ratio)) + (high.B * ratio));
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static bool TryParseColor(string color, out (int R, int G, int B) rgb)
    {
        rgb = default;
        if (string.IsNullOrWhiteSpace(color))
        {
            return false;
        }

        var normalized = color.Trim();
        if (normalized.StartsWith('#'))
        {
            normalized = normalized[1..];
        }

        if (normalized.Length == 3)
        {
            normalized = string.Concat(normalized.Select(ch => $"{ch}{ch}"));
        }

        if (normalized.Length != 6)
        {
            return false;
        }

        if (!int.TryParse(normalized.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) ||
            !int.TryParse(normalized.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) ||
            !int.TryParse(normalized.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return false;
        }

        rgb = (r, g, b);
        return true;
    }

    private static string Fmt(double value) =>
        double.IsFinite(value)
            ? value.ToString("F1", CultureInfo.InvariantCulture)
            : "0.0";
}
