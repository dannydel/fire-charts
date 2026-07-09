using System.ComponentModel;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace FireCharts.Components;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed partial class ChartTooltip : ComponentBase, IAsyncDisposable
{
    private const double DefaultGutter = 8;

    private ITooltipMeasurer? _measurer;
    private bool _ownsMeasurer;
    private ElementReference _tooltipElement;
    private TooltipSnapshot _lastMeasuredSnapshot;
    private bool _pendingMeasurement;
    private bool _hasPendingStateUpdate;
    private bool _awaitingMeasurementFrame;
    private string? _resolvedPlacementClass;

    [CascadingParameter] private ChartSurface? Surface { get; set; }

    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    [Inject] private IServiceProvider ServiceProvider { get; set; } = default!;

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

        var measurement = await ResolveMeasurer().MeasureAsync(Surface.HostElement, _tooltipElement);
        if (measurement is null)
        {
            return;
        }

        var layout = TooltipPlacementEngine.Resolve(
            new TooltipLayoutRequest(AnchorX, AnchorY, PreferredPlacement, Offset, Gutter),
            measurement.Value);

        CurrentStyle = FormatPositionStyle(layout.Left, layout.Top);
        _resolvedPlacementClass = GetPlacementClass(layout.Placement);
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
        if (_ownsMeasurer && _measurer is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync();
        }
    }

    private ITooltipMeasurer ResolveMeasurer()
    {
        if (_measurer is not null)
        {
            return _measurer;
        }

        var injected = ServiceProvider.GetService<ITooltipMeasurer>();
        if (injected is not null)
        {
            _measurer = injected;
            _ownsMeasurer = false;
        }
        else
        {
            _measurer = new JsTooltipMeasurer(JSRuntime);
            _ownsMeasurer = true;
        }

        return _measurer;
    }

    private static string FormatPositionStyle(double left, double top) =>
        $"left: {Format(left)}px; top: {Format(top)}px;";

    private static string Format(double value) =>
        double.IsFinite(value)
            ? value.ToString("F1", CultureInfo.InvariantCulture)
            : "0.0";

    private static string GetPlacementClass(ChartTooltipPlacement placement) =>
        placement switch
        {
            ChartTooltipPlacement.Above => "chart-tooltip--placement-above",
            ChartTooltipPlacement.Below => "chart-tooltip--placement-below",
            ChartTooltipPlacement.Left => "chart-tooltip--placement-left",
            ChartTooltipPlacement.Right => "chart-tooltip--placement-right",
            _ => "chart-tooltip--placement-above"
        };

    private readonly record struct TooltipSnapshot(
        double AnchorX,
        double AnchorY,
        ChartTooltipPlacement PreferredPlacement,
        double Offset,
        double Gutter,
        object? MeasurementKey);
}
