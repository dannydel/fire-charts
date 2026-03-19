using FireCharts.Components;
using FireCharts.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FireCharts.Tests;

public sealed class FirePieChartTests : TestContext
{
    [Fact]
    public void RendersCustomEmptyStateWhenNoPositiveValuesRemain()
    {
        var data = new[]
        {
            new PieDatum("Zero", 0),
            new PieDatum("Negative", -4),
            new PieDatum("Invalid", double.NaN)
        };

        var cut = RenderComponent<FirePieChart<PieDatum>>(parameters => parameters
            .Add(component => component.Items, data)
            .Add(component => component.ValueSelector, item => item.Value)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.EmptyStateTemplate, (RenderFragment)(builder => builder.AddContent(0, "No slices"))));

        Assert.Contains("No slices", cut.Markup);
        Assert.Empty(cut.FindAll("path.pie-slice"));
    }

    [Fact]
    public void FiltersInvalidSlicesAndUsesCenterLabelForTotal()
    {
        var data = new[]
        {
            new PieDatum("Suppression", 10),
            new PieDatum("Medical", 0),
            new PieDatum("Rescue", -2),
            new PieDatum("Other", double.NaN)
        };

        var cut = RenderPieChart(data);

        Assert.Single(cut.FindAll("path.pie-slice"));
        Assert.Contains("10", cut.Find(".pie-center-label__value").TextContent);
    }

    [Fact]
    public void SupportsDonutModeOverlayThresholdsAndCustomLabelTemplates()
    {
        var data = new[]
        {
            new PieDatum("Large", 80),
            new PieDatum("Medium", 15),
            new PieDatum("Small", 5)
        };

        var cut = RenderComponent<FirePieChart<PieDatum>>(parameters => parameters
            .Add(component => component.Items, data)
            .Add(component => component.ValueSelector, item => item.Value)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.InnerRadiusRatio, 0.45)
            .Add(component => component.MinimumLabelPercentage, 0.20)
            .Add(component => component.PointLabelTemplate, (RenderFragment<PieChartPoint<PieDatum>>)(point => builder =>
            {
                builder.OpenElement(0, "text");
                builder.AddAttribute(1, "class", "custom-slice-label");
                builder.AddContent(2, point.Label);
                builder.CloseElement();
            })));

        Assert.Contains("fire-pie-chart-shell--donut", cut.Find(".fire-pie-chart-shell").GetAttribute("class"));
        Assert.Single(cut.FindAll("text.custom-slice-label"));
        Assert.Contains("Large", cut.Markup);
        Assert.Empty(cut.FindAll(".pie-legend"));
    }

    [Fact]
    public void LegendModeRendersLegendItemsAndUsesCustomColors()
    {
        var data = new[]
        {
            new PieDatum("Suppression", 12, "#111111", "#212121"),
            new PieDatum("Medical", 7, "#222222", "#323232"),
            new PieDatum("Rescue", 5, "#333333", "#434343")
        };

        var cut = RenderComponent<FirePieChart<PieDatum>>(parameters => parameters
            .Add(component => component.Items, data)
            .Add(component => component.ValueSelector, item => item.Value)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.LabelMode, PieChartLabelMode.Legend)
            .Add(component => component.ColorSelector, item => item.Fill)
            .Add(component => component.HoverColorSelector, item => item.HoverFill));

        var firstSlice = cut.Find("path.pie-slice");

        Assert.Equal(3, cut.FindAll(".pie-legend__item").Count);
        Assert.Equal(3, cut.FindAll("button.pie-legend__item").Count);
        Assert.Empty(cut.FindAll(".pie-slice-label-box"));
        Assert.Contains("--slice-color: #111111; --slice-hover-color: #212121", firstSlice.GetAttribute("style"));
    }

    [Fact]
    public void HoverFocusKeyboardAndTooltipTemplateUpdateInteractionState()
    {
        var data = new[]
        {
            new PieDatum("Suppression", 12),
            new PieDatum("Medical", 7)
        };

        ChartPointInteraction<PieDatum>? hovered = null;
        ChartPointInteraction<PieDatum>? clicked = null;
        PieDatum? selected = null;

        var cut = RenderComponent<FirePieChart<PieDatum>>(parameters => parameters
            .Add(component => component.Items, data)
            .Add(component => component.ValueSelector, item => item.Value)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.LabelMode, PieChartLabelMode.Legend)
            .Add(component => component.SelectedItem, data[1])
            .Add(component => component.OnPointHoverChanged, (Action<ChartPointInteraction<PieDatum>>)(interaction => hovered = interaction))
            .Add(component => component.OnPointClick, (Action<ChartPointInteraction<PieDatum>>)(interaction => clicked = interaction))
            .Add(component => component.SelectedItemChanged, (Action<PieDatum?>)(item => selected = item))
            .Add(component => component.TooltipTemplate, (RenderFragment<PieChartPoint<PieDatum>>)(point => builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "custom-pie-tooltip");
                builder.AddContent(2, $"tooltip-{point.Label}");
                builder.CloseElement();
            })));

        cut.FindAll("g.pie-slice-group")[0].MouseOver();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(hovered);
            Assert.Equal("Suppression", hovered!.Label);
            Assert.Contains("tooltip-Suppression", cut.Markup);
            Assert.Contains("is-active", cut.FindAll(".pie-legend__item")[0].GetAttribute("class"));
            Assert.NotEqual("translate(0.0 0.0)", cut.FindAll("g.pie-slice-group")[0].GetAttribute("transform"));
        });

        cut.FindAll("g.pie-slice-group")[0].Focus();
        cut.FindAll("g.pie-slice-group")[0].MouseOut();
        cut.WaitForAssertion(() =>
        {
            var firstGroup = cut.FindAll("g.pie-slice-group")[0];
            Assert.Contains("is-focused", firstGroup.GetAttribute("class"));
            Assert.Contains("is-active", cut.FindAll(".pie-legend__item")[0].GetAttribute("class"));
        });

        cut.FindAll("g.pie-slice-group")[0].Blur();
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".chart-tooltip"));
            Assert.DoesNotContain("is-active", cut.FindAll(".pie-legend__item")[0].GetAttribute("class"));
        });

        cut.FindAll("g.pie-slice-group")[1].KeyDown(new KeyboardEventArgs { Key = " " });
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(selected);
            Assert.Equal("Medical", selected!.Label);
            Assert.NotNull(clicked);
            Assert.Equal(7, clicked!.Value);
            Assert.Contains("is-selected", cut.FindAll("g.pie-slice-group")[1].GetAttribute("class"));
        });
    }

    [Fact]
    public void LegendItemsSupportHoverFocusAndClickSelection()
    {
        var data = new[]
        {
            new PieDatum("Suppression", 12),
            new PieDatum("Medical", 7),
            new PieDatum("Rescue", 5)
        };

        ChartPointInteraction<PieDatum>? hovered = null;
        ChartPointInteraction<PieDatum>? clicked = null;
        PieDatum? selected = null;

        var cut = RenderComponent<FirePieChart<PieDatum>>(parameters => parameters
            .Add(component => component.Items, data)
            .Add(component => component.ValueSelector, item => item.Value)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.LabelMode, PieChartLabelMode.Legend)
            .Add(component => component.OnPointHoverChanged, (Action<ChartPointInteraction<PieDatum>>)(interaction => hovered = interaction))
            .Add(component => component.OnPointClick, (Action<ChartPointInteraction<PieDatum>>)(interaction => clicked = interaction))
            .Add(component => component.SelectedItemChanged, (Action<PieDatum?>)(item => selected = item))
            .Add(component => component.TooltipTemplate, (RenderFragment<PieChartPoint<PieDatum>>)(point => builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "custom-pie-tooltip");
                builder.AddContent(2, $"legend-tooltip-{point.Label}");
                builder.CloseElement();
            })));

        cut.FindAll("button.pie-legend__item")[2].MouseOver();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(hovered);
            Assert.Equal("Rescue", hovered!.Label);
            Assert.Contains("legend-tooltip-Rescue", cut.Markup);
            Assert.Contains("is-hovered", cut.FindAll("g.pie-slice-group")[2].GetAttribute("class"));
        });

        cut.FindAll("button.pie-legend__item")[2].Focus();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("is-focused", cut.FindAll("g.pie-slice-group")[2].GetAttribute("class"));
        });

        cut.FindAll("button.pie-legend__item")[1].Click();
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(selected);
            Assert.Equal("Medical", selected!.Label);
            Assert.NotNull(clicked);
            Assert.Equal("Medical", clicked!.Label);
            Assert.Contains("is-selected", cut.FindAll("g.pie-slice-group")[1].GetAttribute("class"));
            Assert.Contains("is-selected", cut.FindAll("button.pie-legend__item")[1].GetAttribute("class"));
        });
    }

    [Fact]
    public void CanHideGuideRingAndCenterLabel()
    {
        var cut = RenderComponent<FirePieChart<PieDatum>>(parameters => parameters
            .Add(component => component.Items, SampleData)
            .Add(component => component.ValueSelector, item => item.Value)
            .Add(component => component.LabelSelector, item => item.Label)
            .Add(component => component.ShowGuideRing, false)
            .Add(component => component.ShowCenterLabel, false));

        Assert.Empty(cut.FindAll("circle.pie-guide-ring"));
        Assert.Empty(cut.FindAll(".pie-center-label"));
    }

    private IRenderedComponent<FirePieChart<PieDatum>> RenderPieChart(IReadOnlyList<PieDatum> data) =>
        RenderComponent<FirePieChart<PieDatum>>(parameters => parameters
            .Add(component => component.Items, data)
            .Add(component => component.ValueSelector, item => item.Value)
            .Add(component => component.LabelSelector, item => item.Label));

    private static readonly PieDatum[] SampleData =
    [
        new("Suppression", 12),
        new("Medical", 7),
        new("Rescue", 5)
    ];

    private sealed record PieDatum(string Label, double Value, string Fill = "#d94f3d", string HoverFill = "#a73728");
}
