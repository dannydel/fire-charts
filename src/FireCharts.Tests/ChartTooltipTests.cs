using FireCharts.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace FireCharts.Tests;

public sealed class ChartTooltipTests : TestContext
{
    [Fact]
    public void LegacyModePreservesLegacyClassAndStyle()
    {
        var cut = RenderComponent<TooltipHarness>(parameters => parameters
            .Add(component => component.ConstrainToBounds, false)
            .Add(component => component.LegacyStyle, "left: 140.0px; top: 120.0px;")
            .Add(component => component.LegacyPlacementClass, "chart-tooltip--top")
            .Add(component => component.ContentText, "legacy-tooltip"));

        var tooltip = cut.Find(".chart-tooltip");

        Assert.Contains("chart-tooltip--top", tooltip.GetAttribute("class"));
        Assert.Equal("left: 140.0px; top: 120.0px;", tooltip.GetAttribute("style"));
        Assert.Equal("legacy-tooltip", tooltip.TextContent.Trim());
    }

    [Fact]
    public void ContainedModeAppliesResolvedGeometryFromMeasurer()
    {
        // above at (140,120) inside a 420x240 host, 100x40 tooltip -> fits: (90,72,above).
        var measurer = new FakeTooltipMeasurer(new TooltipMeasurement(420, 240, 100, 40));
        Services.AddSingleton<ITooltipMeasurer>(measurer);

        var cut = RenderComponent<TooltipHarness>(parameters => parameters
            .Add(component => component.ConstrainToBounds, true)
            .Add(component => component.MeasurementKey, "initial")
            .Add(component => component.ContentText, "measured-tooltip"));

        cut.WaitForAssertion(() =>
        {
            var tooltip = cut.Find(".chart-tooltip");
            Assert.Contains("chart-tooltip--contained", tooltip.GetAttribute("class"));
            Assert.Contains("chart-tooltip--placement-above", tooltip.GetAttribute("class"));
            Assert.Equal("left: 90.0px; top: 72.0px;", tooltip.GetAttribute("style"));
            Assert.Equal("measured-tooltip", tooltip.TextContent.Trim());
        });

        Assert.Equal(1, measurer.MeasureCount);
    }

    [Fact]
    public void ContainedModeFlipsPlacementReportedByEngine()
    {
        // above at (140,20) would overflow the top edge; the engine flips to below: (90,28,below).
        var measurer = new FakeTooltipMeasurer(new TooltipMeasurement(420, 240, 100, 40));
        Services.AddSingleton<ITooltipMeasurer>(measurer);

        var cut = RenderComponent<TooltipHarness>(parameters => parameters
            .Add(component => component.ConstrainToBounds, true)
            .Add(component => component.AnchorX, 140d)
            .Add(component => component.AnchorY, 20d)
            .Add(component => component.MeasurementKey, "initial"));

        cut.WaitForAssertion(() =>
        {
            var tooltip = cut.Find(".chart-tooltip");
            Assert.Contains("chart-tooltip--placement-below", tooltip.GetAttribute("class"));
            Assert.Equal("left: 90.0px; top: 28.0px;", tooltip.GetAttribute("style"));
        });
    }

    [Fact]
    public void ContainedModeRemeasuresWhenAnchorOrMeasurementKeyChanges()
    {
        var measurer = new FakeTooltipMeasurer(
            new TooltipMeasurement(420, 240, 100, 40),
            new TooltipMeasurement(420, 240, 100, 40));
        Services.AddSingleton<ITooltipMeasurer>(measurer);

        var cut = RenderComponent<TooltipHarness>(parameters => parameters
            .Add(component => component.ConstrainToBounds, true)
            .Add(component => component.AnchorX, 120d)
            .Add(component => component.AnchorY, 90d)
            .Add(component => component.MeasurementKey, "initial"));

        // above at (120,90): left = 70, top = 42.
        cut.WaitForAssertion(() =>
            Assert.Equal("left: 70.0px; top: 42.0px;", cut.Find(".chart-tooltip").GetAttribute("style")));

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.AnchorX, 180d)
            .Add(component => component.AnchorY, 110d)
            .Add(component => component.MeasurementKey, "updated")
            .Add(component => component.ContentText, "updated-tooltip"));

        // above at (180,110): left = 130, top = 62.
        cut.WaitForAssertion(() =>
        {
            var tooltip = cut.Find(".chart-tooltip");
            Assert.Contains("chart-tooltip--placement-above", tooltip.GetAttribute("class"));
            Assert.Equal("left: 130.0px; top: 62.0px;", tooltip.GetAttribute("style"));
            Assert.Equal("updated-tooltip", tooltip.TextContent.Trim());
        });

        Assert.Equal(2, measurer.MeasureCount);
    }

    [Fact]
    public void ContainedModeStaysHiddenWhenMeasurementIsNull()
    {
        // No scripted measurements -> the measurer reports "not laid out yet" (null).
        var measurer = new FakeTooltipMeasurer();
        Services.AddSingleton<ITooltipMeasurer>(measurer);

        var cut = RenderComponent<TooltipHarness>(parameters => parameters
            .Add(component => component.ConstrainToBounds, true)
            .Add(component => component.MeasurementKey, "initial")
            .Add(component => component.ContentText, "hidden-tooltip"));

        cut.WaitForAssertion(() => Assert.Equal(1, measurer.MeasureCount));

        var tooltip = cut.Find(".chart-tooltip");
        Assert.Contains("visibility: hidden;", tooltip.GetAttribute("style"));
        Assert.Contains("chart-tooltip--placement-above", tooltip.GetAttribute("class"));
    }

    [Fact]
    public async Task JsTooltipMeasurerImportsModuleAndReturnsMeasurement()
    {
        var module = new RecordingJsObjectReference();
        module.SetupHandler("measure", _ => new TooltipMeasurement(420, 240, 100, 40));
        var runtime = new RecordingJsRuntime(module);

        var measurer = new JsTooltipMeasurer(runtime);
        var measurement = await measurer.MeasureAsync(default, default);

        Assert.Equal(new TooltipMeasurement(420, 240, 100, 40), measurement);
        Assert.Equal(1, runtime.ImportCount);
        Assert.Equal("./_content/FireCharts/chartTooltip.js", runtime.Invocations.Single().Arguments[0]);
        Assert.Single(module.Invocations, invocation => invocation.Identifier == "measure");

        await measurer.DisposeAsync();
        Assert.True(module.DisposeCalled);
    }

    public sealed class TooltipHarness : ComponentBase
    {
        [Parameter] public double AnchorX { get; set; } = 140;
        [Parameter] public double AnchorY { get; set; } = 120;
        [Parameter] public bool ConstrainToBounds { get; set; }
        [Parameter] public object? MeasurementKey { get; set; } = "tooltip";
        [Parameter] public string LegacyStyle { get; set; } = "left: 140.0px; top: 120.0px;";
        [Parameter] public string LegacyPlacementClass { get; set; } = "chart-tooltip--top";
        [Parameter] public string ContentText { get; set; } = "tooltip";
        [Parameter] public ChartTooltipPlacement PreferredPlacement { get; set; } = ChartTooltipPlacement.Above;
        [Parameter] public double Offset { get; set; } = 8;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ChartSurface>(0);
            builder.AddAttribute(1, nameof(ChartSurface.Title), "Tooltip harness");
            builder.AddAttribute(2, nameof(ChartSurface.Description), "Tooltip harness description");
            builder.AddAttribute(3, nameof(ChartSurface.Width), 420d);
            builder.AddAttribute(4, nameof(ChartSurface.Height), 240d);
            builder.AddAttribute(5, nameof(ChartSurface.OverlayContent), (RenderFragment)(overlayBuilder =>
            {
                overlayBuilder.OpenComponent<ChartTooltip>(0);
                overlayBuilder.AddAttribute(1, nameof(ChartTooltip.AnchorX), AnchorX);
                overlayBuilder.AddAttribute(2, nameof(ChartTooltip.AnchorY), AnchorY);
                overlayBuilder.AddAttribute(3, nameof(ChartTooltip.ConstrainToBounds), ConstrainToBounds);
                overlayBuilder.AddAttribute(4, nameof(ChartTooltip.MeasurementKey), MeasurementKey);
                overlayBuilder.AddAttribute(5, nameof(ChartTooltip.PreferredPlacement), PreferredPlacement);
                overlayBuilder.AddAttribute(6, nameof(ChartTooltip.LegacyStyle), LegacyStyle);
                overlayBuilder.AddAttribute(7, nameof(ChartTooltip.LegacyPlacementClass), LegacyPlacementClass);
                overlayBuilder.AddAttribute(8, nameof(ChartTooltip.Offset), Offset);
                overlayBuilder.AddAttribute(9, nameof(ChartTooltip.ChildContent), (RenderFragment)(contentBuilder =>
                {
                    contentBuilder.OpenElement(0, "div");
                    contentBuilder.AddContent(1, ContentText);
                    contentBuilder.CloseElement();
                }));
                overlayBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }
}
