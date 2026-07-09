using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FireCharts.Components;

/// <summary>
/// Production <see cref="ITooltipMeasurer"/> backed by JS interop. Owns the lazy module
/// import, the module handle lifetime, and <see cref="JSDisconnectedException"/>-swallowing
/// disposal.
/// </summary>
internal sealed class JsTooltipMeasurer : ITooltipMeasurer, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;

    public JsTooltipMeasurer(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async ValueTask<TooltipMeasurement?> MeasureAsync(
        ElementReference host,
        ElementReference tooltip,
        CancellationToken ct = default)
    {
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            ct,
            "./_content/FireCharts/chartTooltip.js");

        return await _module.InvokeAsync<TooltipMeasurement?>("measure", ct, host, tooltip);
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
}
