using System.Globalization;
using System.Text.Json;
using FireCharts.Interaction;
using FireCharts.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FireCharts.Components;

public partial class FireHeatmapChart<TItem> : ComponentBase
{
    private static readonly JsonSerializerOptions CanvasSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly record struct CellKey(int RowIndex, int ColumnIndex);
    private readonly record struct AxisGroup(string Key, string Label, double Order);
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

    private HeatmapRenderData<TItem> _renderData = HeatmapRenderData<TItem>.Empty;
    private PlotArea _plot;
    private ChartInteraction<HeatmapCell<TItem>, CellKey> _interaction = default!;

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
    [Parameter] public bool ConstrainTooltipToChartBounds { get; set; }
    [Parameter] public bool ShowAxisLabels { get; set; } = true;
    [Parameter] public bool ShowLegend { get; set; } = true;
    [Parameter] public HeatmapRenderMode RenderMode { get; set; } = HeatmapRenderMode.Svg;
    [Parameter] public int AutoCanvasCellThreshold { get; set; } = 400;
    [Parameter] public double CellGap { get; set; } = 3;
    [Parameter] public double CornerRadius { get; set; } = 4;
    [Parameter] public string LowColor { get; set; } = "#fff0d7";
    [Parameter] public string HighColor { get; set; } = "#d94f3d";
    [Parameter] public string MissingCellColor { get; set; } = "#f5ece4";
    [Parameter] public string ValueFormat { get; set; } = "F0";

    private ChartPadding Padding => new(
        Top: 18,
        Right: 18,
        Bottom: ShowAxisLabels ? 62 : 18,
        Left: ShowAxisLabels ? 92 : 18);

    private IReadOnlyList<HeatmapCell<TItem>> Cells => _renderData.Cells;
    private IReadOnlyList<HeatmapPlaceholderCell> PlaceholderCells => _renderData.PlaceholderCells;
    private IReadOnlyList<HeatmapRowDefinition> Rows => _renderData.Rows;
    private IReadOnlyList<HeatmapColumnDefinition> Columns => _renderData.Columns;
    private HeatmapCell<TItem>? HoveredCell => _interaction.Hovered;
    private double SafeCellGap => Math.Clamp(CellGap, 0, 12);
    private double SafeCornerRadius => Math.Clamp(CornerRadius, 0, 16);
    private int MatrixCellCount => Rows.Count * Columns.Count;
    private double EstimatedCellExtent => Rows.Count == 0 || Columns.Count == 0
        ? 0
        : Math.Min(
            Math.Max((_plot.Width / Columns.Count) - SafeCellGap, 1),
            Math.Max((_plot.Height / Rows.Count) - SafeCellGap, 1));
    private bool UseDenseCanvasFallback => ActiveRenderMode is HeatmapRenderMode.Canvas && EstimatedCellExtent <= 6;
    private double CanvasCornerRadius => UseDenseCanvasFallback ? 0 : SafeCornerRadius;
    private HeatmapRenderMode ActiveRenderMode => ResolveRenderMode();
    private string CanvasRenderRequestJson => JsonSerializer.Serialize(
        new HeatmapCanvasRenderRequest(
            _plot.Width,
            _plot.Height,
            CanvasCornerRadius,
            UseDenseCanvasFallback
                ? Array.Empty<HeatmapCanvasRenderRect>()
                : PlaceholderCells.Select(cell => new HeatmapCanvasRenderRect(
                    -1,
                    -1,
                    cell.Rect.X - _plot.Left,
                    cell.Rect.Y - _plot.Top,
                    cell.Rect.Width,
                    cell.Rect.Height,
                    cell.Fill)).ToArray(),
            Cells.Select(cell => new HeatmapCanvasRenderRect(
                cell.RowIndex,
                cell.ColumnIndex,
                cell.Rect.X - _plot.Left,
                cell.Rect.Y - _plot.Top,
                cell.Rect.Width,
                cell.Rect.Height,
                cell.Fill)).ToArray()),
        CanvasSerializerOptions);
    private string CanvasInteractionStateJson => JsonSerializer.Serialize(
        new HeatmapCanvasInteractionState(
            ToCanvasKey(GetSelectedCellKey()),
            ToCanvasKey(_interaction.HoveredKey),
            ToCanvasKey(_interaction.FocusedKey)),
        CanvasSerializerOptions);
    private string CanvasInteractionLayerStyle =>
        $"left: {Fmt(_plot.Left)}px; top: {Fmt(_plot.Top)}px; width: {Fmt(_plot.Width)}px; height: {Fmt(_plot.Height)}px;";
    private string CanvasAriaLabel => (_interaction.Focused ?? _interaction.Hovered ?? Cells.FirstOrDefault())?.AccessibleLabel ?? Title;
    private string LegendMinLabel => _renderData.MinValue.ToString(ValueFormat, CultureInfo.InvariantCulture);
    private string LegendMaxLabel => _renderData.MaxValue.ToString(ValueFormat, CultureInfo.InvariantCulture);
    private string LegendScaleStyle => $"--legend-low: {LowColor}; --legend-high: {HighColor};";

    protected override void OnInitialized()
    {
        _interaction = new ChartInteraction<HeatmapCell<TItem>, CellKey>(new ChartInteractionOptions<HeatmapCell<TItem>, CellKey>
        {
            KeySelector = cell => new CellKey(cell.RowIndex, cell.ColumnIndex),
            RequestRender = () => InvokeAsync(StateHasChanged),
            OnActiveChanged = cell => OnCellHoverChanged.InvokeAsync(ToInteraction(cell)),
            OnActivate = async cell =>
            {
                SelectedItem = cell.Item;
                await InvokeAsync(StateHasChanged);
                await SelectedItemChanged.InvokeAsync(cell.Item);
                await OnCellClick.InvokeAsync(ToInteraction(cell));
            },
            Navigator = (key, direction) => FindAdjacentKey(key, direction)
        });
    }

    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(ColumnKeySelector);
        ArgumentNullException.ThrowIfNull(ColumnLabelSelector);
        ArgumentNullException.ThrowIfNull(RowKeySelector);
        ArgumentNullException.ThrowIfNull(RowLabelSelector);
        ArgumentNullException.ThrowIfNull(ValueSelector);

        Width = Math.Max(Width, 1);
        Height = Math.Max(Height, 1);
        _plot = PlotArea.FromInset(Width, Height, Padding);

        RebuildChart();
    }

    private void RebuildChart()
    {
        var rawCells = BuildRawCells();
        if (rawCells.Count == 0)
        {
            _renderData = HeatmapRenderData<TItem>.Empty;
            _interaction.SetElements(Array.Empty<HeatmapCell<TItem>>());
            return;
        }

        _renderData = BuildRenderData(rawCells);
        _interaction.SetElements(_renderData.Cells);
    }

    private HeatmapRenderData<TItem> BuildRenderData(IReadOnlyList<RawCell> rawCells)
    {
        var minValue = rawCells.Min(cell => cell.Value);
        var maxValue = rawCells.Max(cell => cell.Value);
        var rows = BuildRows(rawCells);
        var columns = BuildColumns(rawCells);
        var rawCellsByKey = new Dictionary<(string RowKey, string ColumnKey), RawCell>();

        foreach (var cell in rawCells)
        {
            rawCellsByKey[(cell.RowKey, cell.ColumnKey)] = cell;
        }

        var placeholderCells = new List<HeatmapPlaceholderCell>(rows.Count * columns.Count);
        var renderedCells = new List<HeatmapCell<TItem>>(rawCells.Count);
        var comparer = EqualityComparer<TItem>.Default;

        foreach (var row in rows)
        {
            foreach (var column in columns)
            {
                var rect = GetCellRect(row, column);
                if (!rawCellsByKey.TryGetValue((row.Key, column.Key), out var rawCell))
                {
                    placeholderCells.Add(new HeatmapPlaceholderCell(rect, MissingCellColor));
                    continue;
                }

                var key = new CellKey(row.Index, column.Index);
                var fill = rawCell.ExplicitColor ?? InterpolateColor(rawCell.Value, minValue, maxValue, LowColor, HighColor);
                var cell = new HeatmapCell<TItem>(
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
                    _interaction.HoveredKey == key,
                    _interaction.FocusedKey == key,
                    rawCell.AccessibleLabel);
                renderedCells.Add(cell);
            }
        }

        return new HeatmapRenderData<TItem>(
            renderedCells.ToArray(),
            placeholderCells.ToArray(),
            rows.ToArray(),
            columns.ToArray(),
            minValue,
            maxValue);
    }

    private List<RawCell> BuildRawCells()
    {
        var items = Items ?? Array.Empty<TItem>();
        var rawCells = new List<RawCell>(items.Count);

        foreach (var item in items)
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

    private List<HeatmapRowDefinition> BuildRows(IReadOnlyList<RawCell> rawCells)
    {
        var groupedMap = new Dictionary<string, AxisGroup>(StringComparer.Ordinal);
        foreach (var cell in rawCells)
        {
            if (groupedMap.TryGetValue(cell.RowKey, out var existing))
            {
                if (cell.RowOrder < existing.Order)
                {
                    groupedMap[cell.RowKey] = existing with { Order = cell.RowOrder };
                }

                continue;
            }

            groupedMap[cell.RowKey] = new AxisGroup(cell.RowKey, cell.RowLabel, cell.RowOrder);
        }

        var grouped = groupedMap.Values.ToList();
        grouped.Sort(static (left, right) =>
        {
            var orderComparison = left.Order.CompareTo(right.Order);
            return orderComparison != 0
                ? orderComparison
                : StringComparer.Ordinal.Compare(left.Label, right.Label);
        });

        var rowHeight = _plot.Height / grouped.Count;
        var rows = new List<HeatmapRowDefinition>(grouped.Count);
        for (var index = 0; index < grouped.Count; index++)
        {
            var row = grouped[index];
            rows.Add(new HeatmapRowDefinition(
                row.Key,
                row.Label,
                row.Order,
                index,
                _plot.Top + (index * rowHeight),
                rowHeight));
        }

        return rows;
    }

    private List<HeatmapColumnDefinition> BuildColumns(IReadOnlyList<RawCell> rawCells)
    {
        var groupedMap = new Dictionary<string, AxisGroup>(StringComparer.Ordinal);
        foreach (var cell in rawCells)
        {
            if (groupedMap.TryGetValue(cell.ColumnKey, out var existing))
            {
                if (cell.ColumnOrder < existing.Order)
                {
                    groupedMap[cell.ColumnKey] = existing with { Order = cell.ColumnOrder };
                }

                continue;
            }

            groupedMap[cell.ColumnKey] = new AxisGroup(cell.ColumnKey, cell.ColumnLabel, cell.ColumnOrder);
        }

        var grouped = groupedMap.Values.ToList();
        grouped.Sort(static (left, right) =>
        {
            var orderComparison = left.Order.CompareTo(right.Order);
            return orderComparison != 0
                ? orderComparison
                : StringComparer.Ordinal.Compare(left.Label, right.Label);
        });

        var columnWidth = _plot.Width / grouped.Count;
        var columns = new List<HeatmapColumnDefinition>(grouped.Count);
        for (var index = 0; index < grouped.Count; index++)
        {
            var column = grouped[index];
            columns.Add(new HeatmapColumnDefinition(
                column.Key,
                column.Label,
                column.Order,
                index,
                _plot.Left + (index * columnWidth),
                columnWidth));
        }

        return columns;
    }

    private SvgRect GetCellRect(HeatmapRowDefinition row, HeatmapColumnDefinition column)
    {
        var gap = SafeCellGap;
        var x = column.X + (gap / 2);
        var y = row.Y + (gap / 2);
        var width = Math.Max(column.Width - gap, 1);
        var height = Math.Max(row.Height - gap, 1);
        return new SvgRect(x, y, width, height);
    }

    private Task OnPlotAreaChanged(PlotArea plot)
    {
        _plot = plot;
        RebuildChart();
        return Task.CompletedTask;
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

        if (IsHovered(cell))
        {
            classes.Add("is-hovered");
        }

        if (IsFocused(cell))
        {
            classes.Add("is-focused");
        }

        if (IsSelected(cell))
        {
            classes.Add("is-selected");
        }

        return string.Join(" ", classes);
    }

    private static string GetCellStyle(HeatmapCell<TItem> cell) =>
        $"--cell-color: {cell.Fill};";

    private SvgPoint GetTooltipAnchor(HeatmapCell<TItem> cell) =>
        new(cell.Rect.X + (cell.Rect.Width / 2), Math.Max(cell.Rect.Y, 0));

    private string GetTooltipStyle(HeatmapCell<TItem> cell)
    {
        var anchor = GetTooltipAnchor(cell);
        return $"left: {Fmt(anchor.X)}px; top: {Fmt(anchor.Y)}px;";
    }

    private static double SanitizeOrder(double? value, int fallback)
    {
        if (value.HasValue && double.IsFinite(value.Value))
        {
            return value.Value;
        }

        return fallback;
    }

    private HeatmapRenderMode ResolveRenderMode() =>
        RenderMode switch
        {
            HeatmapRenderMode.Canvas => HeatmapRenderMode.Canvas,
            HeatmapRenderMode.Auto when MatrixCellCount >= Math.Max(AutoCanvasCellThreshold, 1) => HeatmapRenderMode.Canvas,
            HeatmapRenderMode.Auto => HeatmapRenderMode.Svg,
            _ => HeatmapRenderMode.Svg
        };

    private Task HandleCanvasPointerMoveAsync((int RowIndex, int ColumnIndex) location)
    {
        var key = new CellKey(location.RowIndex, location.ColumnIndex);
        return _interaction.Resolve(key) is not null
            ? _interaction.HoverKeyAsync(key)
            : Task.CompletedTask;
    }

    private Task HandleCanvasPointerLeaveAsync() => _interaction.HoverLeaveSurfaceAsync();

    private async Task HandleCanvasClickAsync((int RowIndex, int ColumnIndex) location)
    {
        var key = new CellKey(location.RowIndex, location.ColumnIndex);
        if (_interaction.Resolve(key) is null)
        {
            return;
        }

        await _interaction.FocusKeyAsync(key);
        await _interaction.ActivateKeyAsync(key);
    }

    private Task HandleCanvasFocusAsync() => _interaction.FocusEntryAsync(GetSelectedCellKey());

    private Task HandleCanvasBlurAsync() => _interaction.BlurSurfaceAsync();

    private Task HandleCanvasKeyDownAsync(KeyboardEventArgs args) =>
        _interaction.KeyDownSurfaceAsync(args, GetSelectedCellKey());

    private CellKey FindAdjacentKey(CellKey origin, ChartArrowDirection direction)
    {
        var (rowDelta, columnDelta) = direction switch
        {
            ChartArrowDirection.Left => (0, -1),
            ChartArrowDirection.Right => (0, 1),
            ChartArrowDirection.Up => (-1, 0),
            ChartArrowDirection.Down => (1, 0),
            _ => (0, 0)
        };

        var rowIndex = origin.RowIndex + rowDelta;
        var columnIndex = origin.ColumnIndex + columnDelta;

        while (rowIndex >= 0 && rowIndex < Rows.Count && columnIndex >= 0 && columnIndex < Columns.Count)
        {
            var candidate = new CellKey(rowIndex, columnIndex);
            if (_interaction.Resolve(candidate) is not null)
            {
                return candidate;
            }

            rowIndex += rowDelta;
            columnIndex += columnDelta;
        }

        return origin;
    }

    private HeatmapCell<TItem>? GetSelectedCell()
    {
        var comparer = EqualityComparer<TItem>.Default;
        return Cells.FirstOrDefault(cell => comparer.Equals(cell.Item, SelectedItem));
    }

    private CellKey? GetSelectedCellKey()
    {
        var cell = GetSelectedCell();
        return cell is null ? null : new CellKey(cell.RowIndex, cell.ColumnIndex);
    }

    private bool IsHovered(HeatmapCell<TItem> cell) => _interaction.IsHovered(cell);

    private bool IsFocused(HeatmapCell<TItem> cell) => _interaction.IsFocused(cell);

    private bool IsSelected(HeatmapCell<TItem> cell) =>
        EqualityComparer<TItem>.Default.Equals(cell.Item, SelectedItem);

    private static string? ToCanvasKey(CellKey? key) =>
        key is null ? null : $"{key.Value.RowIndex}:{key.Value.ColumnIndex}";

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

    private static string Fmt(double value) => ChartFormat.Fmt(value);
}
