using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FireCharts.Components;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed partial class ChartTooltip : ComponentBase, IAsyncDisposable
{
    private const double DefaultGutter = 8;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private IJSObjectReference? _module;
    private ElementReference _tooltipElement;
    private TooltipSnapshot _lastMeasuredSnapshot;
    private bool _pendingMeasurement;
    private bool _hasPendingStateUpdate;
    private bool _awaitingMeasurementFrame;
    private string? _resolvedPlacementClass;

    [CascadingParameter] private ChartSurface? Surface { get; set; }

    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter] public double AnchorX { get; set; }
    [Parameter] public double AnchorY { get; set; }
    [Parameter] public bool ConstrainToBounds { get; set; }
    [Parameter] public object? MeasurementKey { get; set; }
    [Parameter] public ChartTooltipPlacement PreferredPlacement { get; set; } = ChartTooltipPlacement.Above;
    [Parameter] public string LegacyStyle { get; set; } = "";
    [Parameter] public string LegacyPlacementClass { get; set; } = "";
    [Parameter] public bool Wrap { get; set; } = true;
    [Parameter] public double Offset { get; set; } = 8;
    [Parameter] public double Gutter { get; set; } = DefaultGutter;
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private string CssClasses
    {
        get
        {
            var classes = new List<string> { "chart-tooltip" };

            if (ConstrainToBounds)
            {
                classes.Add("chart-tooltip--contained");
                if (!string.IsNullOrWhiteSpace(_resolvedPlacementClass))
                {
                    classes.Add(_resolvedPlacementClass);
                }
            }
            else if (!string.IsNullOrWhiteSpace(LegacyPlacementClass))
            {
                classes.Add(LegacyPlacementClass);
            }

            if (!Wrap)
            {
                classes.Add("chart-tooltip--nowrap");
            }

            return string.Join(" ", classes);
        }
    }

    private string CurrentStyle { get; set; } = "";

    protected override void OnParametersSet()
    {
        if (!ConstrainToBounds)
        {
            CurrentStyle = LegacyStyle;
            _resolvedPlacementClass = null;
            _pendingMeasurement = false;
            return;
        }

        var snapshot = new TooltipSnapshot(
            AnchorX,
            AnchorY,
            PreferredPlacement,
            Offset,
            Gutter,
            MeasurementKey);

        if (_lastMeasuredSnapshot != snapshot)
        {
            CurrentStyle = $"{FormatPositionStyle(AnchorX, AnchorY)} visibility: hidden;";
            _resolvedPlacementClass = GetPlacementClass(PreferredPlacement);
            _pendingMeasurement = true;
            _awaitingMeasurementFrame = true;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!ConstrainToBounds || !_pendingMeasurement)
        {
            return;
        }

        if (Surface is null)
        {
            return;
        }

        if (_awaitingMeasurementFrame)
        {
            _awaitingMeasurementFrame = false;
            _hasPendingStateUpdate = true;
            await InvokeAsync(StateHasChanged);
            return;
        }

        _module ??= await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            "./_content/FireCharts/chartTooltip.js");

        var resultJson = await _module.InvokeAsync<string>(
            "resolveTooltipPosition",
            Surface.HostElement,
            _tooltipElement,
            AnchorX,
            AnchorY,
            GetPlacementValue(PreferredPlacement),
            Offset,
            Gutter);

        var result = JsonSerializer.Deserialize<TooltipResolution>(resultJson, _jsonOptions);
        if (result is null)
        {
            return;
        }

        CurrentStyle = FormatPositionStyle(result.Left, result.Top);
        _resolvedPlacementClass = GetPlacementClass(result.Placement);
        _lastMeasuredSnapshot = new TooltipSnapshot(
            AnchorX,
            AnchorY,
            PreferredPlacement,
            Offset,
            Gutter,
            MeasurementKey);
        _pendingMeasurement = false;
        _hasPendingStateUpdate = true;

        await InvokeAsync(StateHasChanged);
    }

    protected override bool ShouldRender()
    {
        if (_hasPendingStateUpdate)
        {
            _hasPendingStateUpdate = false;
            return true;
        }

        return true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private static string FormatPositionStyle(double left, double top) =>
        $"left: {Format(left)}px; top: {Format(top)}px;";

    private static string Format(double value) =>
        double.IsFinite(value)
            ? value.ToString("F1", CultureInfo.InvariantCulture)
            : "0.0";

    private static string GetPlacementValue(ChartTooltipPlacement placement) =>
        placement switch
        {
            ChartTooltipPlacement.Above => "above",
            ChartTooltipPlacement.Below => "below",
            ChartTooltipPlacement.Left => "left",
            ChartTooltipPlacement.Right => "right",
            _ => "above"
        };

    private static string GetPlacementClass(ChartTooltipPlacement placement) =>
        placement switch
        {
            ChartTooltipPlacement.Above => "chart-tooltip--placement-above",
            ChartTooltipPlacement.Below => "chart-tooltip--placement-below",
            ChartTooltipPlacement.Left => "chart-tooltip--placement-left",
            ChartTooltipPlacement.Right => "chart-tooltip--placement-right",
            _ => "chart-tooltip--placement-above"
        };

    private static string GetPlacementClass(string? placement) =>
        placement switch
        {
            "above" => "chart-tooltip--placement-above",
            "below" => "chart-tooltip--placement-below",
            "left" => "chart-tooltip--placement-left",
            "right" => "chart-tooltip--placement-right",
            _ => "chart-tooltip--placement-above"
        };

    private readonly record struct TooltipSnapshot(
        double AnchorX,
        double AnchorY,
        ChartTooltipPlacement PreferredPlacement,
        double Offset,
        double Gutter,
        object? MeasurementKey);

    private sealed record TooltipResolution(double Left, double Top, string Placement);
}
