using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Globalization;
using FireCharts.Models;

namespace FireCharts.Components;

public partial class ChartSurface : ComponentBase, IAsyncDisposable
{
    private const double DefaultMinWidth = 280;
    private const double DefaultMinHeight = 180;

    private readonly string _titleId = $"chart-title-{Guid.NewGuid():N}";
    private readonly string _descId = $"chart-desc-{Guid.NewGuid():N}";
    private DotNetObjectReference<ChartSurface>? _dotNetRef;
    private IJSObjectReference? _module;
    private IJSObjectReference? _observer;
    private ElementReference _hostElement;
    private double? _observedWidth;

    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter] public string Title { get; set; } = "Chart";
    [Parameter] public string Description { get; set; } = "";
    [Parameter] public double Width { get; set; } = 600;
    [Parameter] public double Height { get; set; } = 400;
    [Parameter] public bool Responsive { get; set; }
    [Parameter] public string HostCssClass { get; set; } = "";
    [Parameter] public string SvgCssClass { get; set; } = "";
    [Parameter] public RenderFragment<ChartSurfaceContext>? ChildContent { get; set; }
    [Parameter] public RenderFragment? OverlayContent { get; set; }

    internal ChartSurfaceContext Context => new(ResolvedWidth, ResolvedHeight);

    internal double ResolvedWidth =>
        Math.Max(Responsive && _observedWidth is > 0 ? _observedWidth.Value : Width, DefaultMinWidth);

    internal double ResolvedHeight => Math.Max(Height, DefaultMinHeight);

    private string HostStyle => Responsive ? "width: 100%;" : $"width: {Fmt(ResolvedWidth)}px;";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!Responsive || !firstRender)
        {
            return;
        }

        _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            "./_content/FireCharts/chartResizeObserver.js");

        _dotNetRef = DotNetObjectReference.Create(this);
        _observer = await _module.InvokeAsync<IJSObjectReference>(
            "observeElementSize",
            _hostElement,
            _dotNetRef);
    }

    [JSInvokable]
    public Task OnContainerWidthChanged(double width)
    {
        var clampedWidth = Math.Max(width, DefaultMinWidth);
        if (Math.Abs(clampedWidth - (_observedWidth ?? 0)) < 0.5)
        {
            return Task.CompletedTask;
        }

        _observedWidth = clampedWidth;
        return InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        if (_observer is not null)
        {
            try
            {
                await _observer.InvokeVoidAsync("dispose");
                await _observer.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }

        if (_module is not null)
        {
            try
            {
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
            ? value.ToString("F1", CultureInfo.InvariantCulture)
            : "0.0";
}
