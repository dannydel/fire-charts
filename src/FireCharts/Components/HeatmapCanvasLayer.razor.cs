using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FireCharts.Components;

public partial class HeatmapCanvasLayer : ComponentBase, IAsyncDisposable
{
    private ElementReference _canvasElement;
    private DotNetObjectReference<HeatmapCanvasLayer>? _dotNetRef;
    private IJSObjectReference? _module;
    private string? _lastRenderRequestJson;
    private string? _lastInteractionStateJson;

    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter, EditorRequired] public string? RenderRequestJson { get; set; }
    [Parameter, EditorRequired] public string? InteractionStateJson { get; set; }
    [Parameter] public double Width { get; set; }
    [Parameter] public double Height { get; set; }
    [Parameter] public double Left { get; set; }
    [Parameter] public double Top { get; set; }
    [Parameter] public EventCallback<(int RowIndex, int ColumnIndex)> OnPointerMove { get; set; }
    [Parameter] public EventCallback OnPointerLeave { get; set; }
    [Parameter] public EventCallback<(int RowIndex, int ColumnIndex)> OnClick { get; set; }

    private string CanvasStyle =>
        $"left: {Fmt(Left)}px; top: {Fmt(Top)}px; width: {Fmt(Width)}px; height: {Fmt(Height)}px;";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (string.IsNullOrWhiteSpace(RenderRequestJson))
        {
            return;
        }

        _module ??= await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            "./_content/FireCharts/heatmapCanvas.js");
        _dotNetRef ??= DotNetObjectReference.Create(this);

        if (!string.Equals(_lastRenderRequestJson, RenderRequestJson, StringComparison.Ordinal))
        {
            await _module.InvokeVoidAsync(
                "upsertHeatmap",
                _canvasElement,
                _dotNetRef,
                RenderRequestJson,
                InteractionStateJson);

            _lastRenderRequestJson = RenderRequestJson;
            _lastInteractionStateJson = InteractionStateJson;
            return;
        }

        if (!string.Equals(_lastInteractionStateJson, InteractionStateJson, StringComparison.Ordinal))
        {
            await _module.InvokeVoidAsync(
                "updateHeatmapState",
                _canvasElement,
                InteractionStateJson);

            _lastInteractionStateJson = InteractionStateJson;
        }
    }

    [JSInvokable]
    public Task NotifyPointerMove(int rowIndex, int columnIndex) =>
        InvokeAsync(() => OnPointerMove.InvokeAsync((rowIndex, columnIndex)));

    [JSInvokable]
    public Task NotifyPointerLeave() =>
        InvokeAsync(() => OnPointerLeave.InvokeAsync());

    [JSInvokable]
    public Task NotifyClick(int rowIndex, int columnIndex) =>
        InvokeAsync(() => OnClick.InvokeAsync((rowIndex, columnIndex)));

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("disposeHeatmap", _canvasElement);
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }

        _dotNetRef?.Dispose();
    }

    private static string Fmt(double value) =>
        double.IsFinite(value)
            ? value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
            : "0.0";
}
