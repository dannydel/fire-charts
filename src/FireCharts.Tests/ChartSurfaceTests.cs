using FireCharts.Components;
using FireCharts.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace FireCharts.Tests;

public sealed class ChartSurfaceTests : TestContext
{
    [Fact]
    public void RendersFixedDimensionsOverlayAndAccessibleMetadata()
    {
        var cut = RenderSurface(width: 240, height: 120, responsive: false);

        var host = cut.Find("div.chart-surface");
        var svg = cut.Find("svg.chart-svg");

        Assert.Contains("width: 280.0px;", host.GetAttribute("style"));
        Assert.Equal("280.0", svg.GetAttribute("width"));
        Assert.Equal("180.0", svg.GetAttribute("height"));
        Assert.Equal("0 0 280.0 180.0", svg.GetAttribute("viewBox"));
        Assert.Contains("Fire incidents by month", cut.Find("title").TextContent);
        Assert.Contains("Comparing recent monthly totals", cut.Find("desc").TextContent);
        Assert.Contains("child-280.0x180.0", cut.Markup);
        Assert.Contains("overlay-details", cut.Markup);
    }

    [Fact]
    public void ResponsiveSurfaceImportsObserverOnlyOnFirstRender()
    {
        var runtime = CreateRuntime();
        Services.AddSingleton<IJSRuntime>(runtime);

        var cut = RenderSurface(responsive: true);

        cut.SetParametersAndRender(parameters => parameters.Add(component => component.Title, "Updated title"));

        Assert.Equal(1, runtime.ImportCount);
        Assert.Single(runtime.Module.Invocations, invocation => invocation.Identifier == "observeElementSize");
        Assert.Equal("./_content/FireCharts/chartResizeObserver.js", runtime.Invocations.Single().Arguments[0]);
    }

    [Fact]
    public async Task OnContainerWidthChangedClampsValuesAndIgnoresSmallChanges()
    {
        var runtime = CreateRuntime();
        Services.AddSingleton<IJSRuntime>(runtime);

        var cut = RenderSurface(width: 600, height: 320, responsive: true);

        await cut.Instance.OnContainerWidthChanged(120);
        cut.WaitForAssertion(() =>
        {
            Assert.Equal("280.0", cut.Find("svg").GetAttribute("width"));
            Assert.Contains("child-280.0x320.0", cut.Markup);
        });

        await cut.Instance.OnContainerWidthChanged(280.3);
        cut.WaitForAssertion(() =>
        {
            Assert.Equal("280.0", cut.Find("svg").GetAttribute("width"));
            Assert.Contains("child-280.0x320.0", cut.Markup);
        });

        await cut.Instance.OnContainerWidthChanged(420);
        cut.WaitForAssertion(() =>
        {
            Assert.Equal("420.0", cut.Find("svg").GetAttribute("width"));
            Assert.Contains("child-420.0x320.0", cut.Markup);
        });
    }

    [Fact]
    public async Task DisposeAsyncSwallowsJsDisconnectedExceptions()
    {
        var observer = new RecordingJsObjectReference();
        observer.SetupHandler("dispose", _ => throw new JSDisconnectedException("observer disconnected"));

        var module = new RecordingJsObjectReference
        {
            DisposeException = new JSDisconnectedException("module disconnected")
        };
        module.SetupResult("observeElementSize", observer);

        Services.AddSingleton<IJSRuntime>(new RecordingJsRuntime(module));

        var cut = RenderSurface(responsive: true);

        await cut.Instance.DisposeAsync();

        Assert.Contains(module.Invocations, invocation => invocation.Identifier == "observeElementSize");
        Assert.Contains(observer.Invocations, invocation => invocation.Identifier == "dispose");
    }

    private IRenderedComponent<ChartSurface> RenderSurface(
        double width = 600,
        double height = 400,
        bool responsive = false) =>
        RenderComponent<ChartSurface>(parameters => parameters
            .Add(component => component.Title, "Fire incidents by month")
            .Add(component => component.Description, "Comparing recent monthly totals")
            .Add(component => component.Width, width)
            .Add(component => component.Height, height)
            .Add(component => component.Responsive, responsive)
            .Add(component => component.HostCssClass, "test-host")
            .Add(component => component.SvgCssClass, "test-svg")
            .Add(component => component.ChildContent, (RenderFragment<ChartSurfaceContext>)(context => builder =>
            {
                builder.OpenElement(0, "text");
                builder.AddAttribute(1, "class", "child-metrics");
                builder.AddContent(2, $"child-{context.Width:F1}x{context.Height:F1}");
                builder.CloseElement();
            }))
            .Add(component => component.OverlayContent, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "overlay-details");
                builder.AddContent(2, "overlay-details");
                builder.CloseElement();
            })));

    private static RecordingJsRuntime CreateRuntime()
    {
        var observer = new RecordingJsObjectReference();
        observer.SetupHandler("dispose", _ => null!);

        var module = new RecordingJsObjectReference();
        module.SetupResult("observeElementSize", observer);

        return new RecordingJsRuntime(module);
    }
}
