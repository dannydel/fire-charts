using FireCharts.Interaction;
using Microsoft.AspNetCore.Components.Web;

namespace FireCharts.Tests;

public sealed class ChartInteractionTests
{
    private sealed record Node(int Id, string Name);

    private sealed class Harness
    {
        public List<string> Events { get; } = [];
        public List<Node> ActiveChanged { get; } = [];
        public List<Node> Activated { get; } = [];
        public int RenderCount { get; private set; }
        public Func<int, ChartArrowDirection, int?>? Navigator { get; set; }

        public ChartInteraction<Node, int> Interaction { get; }

        public Harness(IReadOnlyList<Node> initial)
        {
            Interaction = new ChartInteraction<Node, int>(new ChartInteractionOptions<Node, int>
            {
                KeySelector = node => node.Id,
                RequestRender = () =>
                {
                    RenderCount++;
                    Events.Add("render");
                    return Task.CompletedTask;
                },
                OnActiveChanged = node =>
                {
                    ActiveChanged.Add(node);
                    Events.Add($"active:{node.Id}");
                    return Task.CompletedTask;
                },
                OnActivate = node =>
                {
                    Activated.Add(node);
                    Events.Add($"activate:{node.Id}");
                    return Task.CompletedTask;
                },
                Navigator = (key, direction) => Navigator?.Invoke(key, direction)
            });

            Interaction.SetElements(initial);
        }
    }

    private static readonly Node A = new(1, "A");
    private static readonly Node B = new(2, "B");
    private static readonly Node C = new(3, "C");

    private static Harness NewHarness() => new([A, B, C]);

    [Fact]
    public async Task HoverIsDedupedAndDoesNotRenderTwice()
    {
        var harness = NewHarness();

        await harness.Interaction.HoverAsync(A);
        await harness.Interaction.HoverAsync(A);

        Assert.Equal(1, harness.RenderCount);
        Assert.Equal(A.Id, harness.Interaction.HoveredKey);
        Assert.Same(A, harness.Interaction.Hovered);
        Assert.Same(A, harness.Interaction.Active);
        Assert.Single(harness.ActiveChanged);
    }

    [Fact]
    public async Task FocusImpliesHover()
    {
        var harness = NewHarness();

        await harness.Interaction.FocusAsync(B);

        Assert.Equal(B.Id, harness.Interaction.FocusedKey);
        Assert.Equal(B.Id, harness.Interaction.HoveredKey);
        Assert.True(harness.Interaction.IsHovered(B));
        Assert.True(harness.Interaction.IsFocused(B));
        Assert.Same(B, harness.Interaction.Active);
        Assert.Equal([B], harness.ActiveChanged);
    }

    [Fact]
    public async Task HoverLeaveWhileFocusedKeepsHover()
    {
        var harness = NewHarness();

        await harness.Interaction.FocusAsync(A);
        harness.Events.Clear();

        await harness.Interaction.HoverLeaveAsync(A);

        Assert.Equal(A.Id, harness.Interaction.HoveredKey);
        Assert.Empty(harness.Events);
    }

    [Fact]
    public async Task HoverLeaveWhenNotFocusedClearsHover()
    {
        var harness = NewHarness();

        await harness.Interaction.HoverAsync(A);
        await harness.Interaction.HoverLeaveAsync(A);

        Assert.Null(harness.Interaction.HoveredKey);
        Assert.Null(harness.Interaction.Hovered);
    }

    [Fact]
    public async Task HoverLeaveForDifferentKeyIsNoOp()
    {
        var harness = NewHarness();

        await harness.Interaction.HoverAsync(A);
        harness.Events.Clear();

        await harness.Interaction.HoverLeaveAsync(B);

        Assert.Equal(A.Id, harness.Interaction.HoveredKey);
        Assert.Empty(harness.Events);
    }

    [Fact]
    public async Task BlurClearsOnlyMatchingKeys()
    {
        var harness = NewHarness();

        await harness.Interaction.FocusAsync(A);
        await harness.Interaction.BlurAsync(B);

        Assert.Equal(A.Id, harness.Interaction.FocusedKey);
        Assert.Equal(A.Id, harness.Interaction.HoveredKey);

        await harness.Interaction.BlurAsync(A);

        Assert.Null(harness.Interaction.FocusedKey);
        Assert.Null(harness.Interaction.HoveredKey);
    }

    [Fact]
    public async Task SetElementsPrunesStaleHoverAndFocusKeys()
    {
        var harness = NewHarness();

        await harness.Interaction.FocusAsync(C);
        Assert.Equal(C.Id, harness.Interaction.HoveredKey);

        harness.Interaction.SetElements([A, B]);

        Assert.Null(harness.Interaction.HoveredKey);
        Assert.Null(harness.Interaction.FocusedKey);
    }

    [Fact]
    public async Task SetElementsKeepsLiveKeysAndRefreshesRegistry()
    {
        var harness = NewHarness();

        await harness.Interaction.HoverAsync(B);
        var refreshed = new Node(B.Id, "B-refreshed");
        harness.Interaction.SetElements([A, refreshed, C]);

        Assert.Equal(B.Id, harness.Interaction.HoveredKey);
        Assert.Same(refreshed, harness.Interaction.Hovered);
    }

    [Fact]
    public async Task EnterAndSpaceActivateButOtherKeysDoNot()
    {
        var harness = NewHarness();

        await harness.Interaction.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }, A);
        await harness.Interaction.KeyDownAsync(new KeyboardEventArgs { Key = " " }, B);
        await harness.Interaction.KeyDownAsync(new KeyboardEventArgs { Key = "a" }, C);

        Assert.Equal([A, B], harness.Activated);
    }

    [Fact]
    public async Task ActivateInvokesOnActivateWithoutMutatingHoverOrFocus()
    {
        var harness = NewHarness();

        await harness.Interaction.ActivateAsync(A);

        Assert.Equal([A], harness.Activated);
        Assert.Null(harness.Interaction.HoveredKey);
        Assert.Null(harness.Interaction.FocusedKey);
    }

    [Fact]
    public async Task ArrowKeyRovesFocusThroughNavigator()
    {
        var harness = NewHarness();
        harness.Navigator = (key, direction) =>
            direction == ChartArrowDirection.Right && key < C.Id ? key + 1 : (int?)null;

        await harness.Interaction.FocusEntryAsync();
        Assert.Equal(A.Id, harness.Interaction.FocusedKey);

        await harness.Interaction.KeyDownSurfaceAsync(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal(B.Id, harness.Interaction.FocusedKey);
        Assert.Equal(B.Id, harness.Interaction.HoveredKey);

        await harness.Interaction.KeyDownSurfaceAsync(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal(C.Id, harness.Interaction.FocusedKey);
    }

    [Fact]
    public async Task ArrowNavigationStaysPutWhenNavigatorReturnsSameOrNull()
    {
        var harness = NewHarness();
        harness.Navigator = (key, _) => key; // never moves

        await harness.Interaction.FocusEntryAsync();
        harness.Events.Clear();

        await harness.Interaction.KeyDownSurfaceAsync(new KeyboardEventArgs { Key = "ArrowUp" });

        Assert.Equal(A.Id, harness.Interaction.FocusedKey);
        Assert.Empty(harness.Events);
    }

    [Fact]
    public async Task SurfaceEnterCommitsCurrentKeyWithFocusThenActivate()
    {
        var harness = NewHarness();

        await harness.Interaction.KeyDownSurfaceAsync(new KeyboardEventArgs { Key = "Enter" }, fallback: B.Id);

        Assert.Equal(B.Id, harness.Interaction.FocusedKey);
        Assert.Equal([B], harness.ActiveChanged);
        Assert.Equal([B], harness.Activated);
        Assert.True(
            harness.Events.IndexOf("active:2") < harness.Events.IndexOf("activate:2"),
            "hover-changed should fire before activate");
    }

    [Fact]
    public async Task FocusEntryUsesFocusedThenFallbackThenFirst()
    {
        var focusedHarness = NewHarness();
        await focusedHarness.Interaction.FocusAsync(C);
        await focusedHarness.Interaction.FocusEntryAsync(fallback: B.Id);
        Assert.Equal(C.Id, focusedHarness.Interaction.FocusedKey);

        var fallbackHarness = NewHarness();
        await fallbackHarness.Interaction.FocusEntryAsync(fallback: B.Id);
        Assert.Equal(B.Id, fallbackHarness.Interaction.FocusedKey);

        var firstHarness = NewHarness();
        await firstHarness.Interaction.FocusEntryAsync();
        Assert.Equal(A.Id, firstHarness.Interaction.FocusedKey);
    }

    [Fact]
    public async Task HoverSurfaceLeaveKeepsHoverWhenPinnedByFocus()
    {
        var harness = NewHarness();

        await harness.Interaction.FocusKeyAsync(A.Id);
        harness.Events.Clear();

        await harness.Interaction.HoverLeaveSurfaceAsync();
        Assert.Equal(A.Id, harness.Interaction.HoveredKey);
        Assert.Empty(harness.Events);

        await harness.Interaction.HoverKeyAsync(B.Id);
        await harness.Interaction.HoverLeaveSurfaceAsync();
        Assert.Null(harness.Interaction.HoveredKey);
        Assert.Equal(A.Id, harness.Interaction.FocusedKey);
    }

    [Fact]
    public async Task BlurSurfaceClearsFocusAndHover()
    {
        var harness = NewHarness();

        await harness.Interaction.FocusKeyAsync(A.Id);
        await harness.Interaction.BlurSurfaceAsync();

        Assert.Null(harness.Interaction.FocusedKey);
        Assert.Null(harness.Interaction.HoveredKey);
    }

    [Fact]
    public async Task CallbackOrderIsMutateThenRenderThenActiveChanged()
    {
        var harness = NewHarness();

        await harness.Interaction.HoverAsync(A);

        Assert.Equal(["render", "active:1"], harness.Events);
    }

    [Fact]
    public async Task MutationIsVisibleBeforeRenderRuns()
    {
        var observedDuringRender = new List<int?>();
        ChartInteraction<Node, int>? interaction = null;
        interaction = new ChartInteraction<Node, int>(new ChartInteractionOptions<Node, int>
        {
            KeySelector = node => node.Id,
            RequestRender = () =>
            {
                observedDuringRender.Add(interaction!.HoveredKey);
                return Task.CompletedTask;
            }
        });
        interaction.SetElements([A, B, C]);

        await interaction.HoverAsync(A);

        Assert.Equal([A.Id], observedDuringRender);
    }
}
