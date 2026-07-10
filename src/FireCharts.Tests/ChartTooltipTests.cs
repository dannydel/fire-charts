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
    public void ContainedModeRequestsMeasuredPlacementAndAppliesResolvedCoordinates()
    {
        var runtime = CreateTooltipRuntime("""{"left":24,"top":32,"placement":"below"}""");
        Services.AddSingleton<IJSRuntime>(runtime);

        var cut = RenderComponent<TooltipHarness>(parameters => parameters
            .Add(component => component.ConstrainToBounds, true)
            .Add(component => component.MeasurementKey, "initial")
            .Add(component => component.ContentText, "measured-tooltip"));

        cut.WaitForAssertion(() =>
        {
            var tooltip = cut.Find(".chart-tooltip");
            Assert.Contains("chart-tooltip--contained", tooltip.GetAttribute("class"));
            Assert.Contains("chart-tooltip--placement-below", tooltip.GetAttribute("class"));
            Assert.Equal("left: 24.0px; top: 32.0px;", tooltip.GetAttribute("style"));
            Assert.Equal("measured-tooltip", tooltip.TextContent.Trim());
        });

        Assert.Equal(1, runtime.ImportCount);
        Assert.Equal("./_content/FireCharts/chartTooltip.js", runtime.Invocations.Single().Arguments[0]);
        Assert.Single(runtime.Module.Invocations, invocation => invocation.Identifier == "resolveTooltipPosition");
    }

    [Fact]
    public void ContainedModeRemeasuresWhenAnchorOrMeasurementKeyChanges()
    {
        var runtime = CreateTooltipRuntime(
            """{"left":24,"top":32,"placement":"above"}""",
            """{"left":48,"top":60,"placement":"right"}""");
        Services.AddSingleton<IJSRuntime>(runtime);

        var cut = RenderComponent<TooltipHarness>(parameters => parameters
            .Add(component => component.ConstrainToBounds, true)
            .Add(component => component.AnchorX, 120d)
            .Add(component => component.AnchorY, 90d)
            .Add(component => component.MeasurementKey, "initial"));

        cut.WaitForAssertion(() =>
            Assert.Equal("left: 24.0px; top: 32.0px;", cut.Find(".chart-tooltip").GetAttribute("style")));

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.AnchorX, 180d)
            .Add(component => component.AnchorY, 110d)
            .Add(component => component.MeasurementKey, "updated")
            .Add(component => component.ContentText, "updated-tooltip"));

        cut.WaitForAssertion(() =>
        {
            var tooltip = cut.Find(".chart-tooltip");
            Assert.Contains("chart-tooltip--placement-right", tooltip.GetAttribute("class"));
            Assert.Equal("left: 48.0px; top: 60.0px;", tooltip.GetAttribute("style"));
            Assert.Equal("updated-tooltip", tooltip.TextContent.Trim());
        });

        Assert.Equal(2, runtime.Module.Invocations.Count(invocation => invocation.Identifier == "resolveTooltipPosition"));
    }

    private static RecordingJsRuntime CreateTooltipRuntime(params string[] responses)
    {
        var queue = new Queue<string>(responses);
        var module = new RecordingJsObjectReference();
        module.SetupHandler("resolveTooltipPosition", _ => queue.Dequeue());
        return new RecordingJsRuntime(module);
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
