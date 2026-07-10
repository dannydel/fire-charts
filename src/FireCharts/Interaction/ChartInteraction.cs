using Microsoft.AspNetCore.Components.Web;

namespace FireCharts.Interaction;

/// <summary>
/// Direction of an arrow-key navigation request routed through a
/// <see cref="ChartInteractionOptions{TElement, TKey}.Navigator"/>.
/// </summary>
public enum ChartArrowDirection
{
    Left,
    Right,
    Up,
    Down
}

/// <summary>
/// Configuration for a <see cref="ChartInteraction{TElement, TKey}"/> instance.
/// One options object is created per chart component in <c>OnInitialized</c>.
/// </summary>
/// <remarks>
/// <typeparamref name="TKey"/> is constrained to a value type so <c>TKey?</c>
/// gives allocation-free nullable state that matches every current chart key
/// (<c>int</c> index or a <c>readonly record struct</c>). A future string-keyed
/// chart needs a wrapper struct.
/// </remarks>
public sealed class ChartInteractionOptions<TElement, TKey>
    where TElement : class
    where TKey : struct, IEquatable<TKey>
{
    /// <summary>Projects the stable interaction key from an element.</summary>
    public required Func<TElement, TKey> KeySelector { get; init; }

    /// <summary>
    /// Marshals a render (typically <c>() =&gt; InvokeAsync(StateHasChanged)</c>).
    /// Charts whose geometry depends on interaction state rebuild inside this callback.
    /// </summary>
    public required Func<Task> RequestRender { get; init; }

    /// <summary>Invoked after hover OR focus becomes an element (the tooltip target changed).</summary>
    public Func<TElement, Task>? OnActiveChanged { get; init; }

    /// <summary>Invoked on click / Enter / Space. Selection stays chart-owned behind this callback.</summary>
    public Func<TElement, Task>? OnActivate { get; init; }

    /// <summary>
    /// Opt-in roving focus for single-tabindex surfaces (canvas). Given the current key and a
    /// direction, returns the neighbouring key (or <c>null</c> / the same key to stay put).
    /// </summary>
    public Func<TKey, ChartArrowDirection, TKey?>? Navigator { get; init; }
}

/// <summary>
/// Shared per-component hover/focus/select/keyboard state machine for a chart.
/// Owns the hovered/focused keys, the key→element registry, the transition rules
/// (hover dedupe, focus-implies-hover mirroring, don't-clear-hover-when-focused,
/// blur asymmetry), keyboard activation, and roving focus. Selection, aria markup,
/// and group/legend hover stay chart-owned.
/// </summary>
public sealed class ChartInteraction<TElement, TKey>
    where TElement : class
    where TKey : struct, IEquatable<TKey>
{
    private readonly ChartInteractionOptions<TElement, TKey> _options;
    private readonly Dictionary<TKey, TElement> _byKey = [];
    private IReadOnlyList<TElement> _elements = Array.Empty<TElement>();
    private TKey? _hoveredKey;
    private TKey? _focusedKey;

    public ChartInteraction(ChartInteractionOptions<TElement, TKey> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.KeySelector);
        ArgumentNullException.ThrowIfNull(options.RequestRender);
        _options = options;
    }

    /// <summary>The currently hovered key, if any.</summary>
    public TKey? HoveredKey => _hoveredKey;

    /// <summary>The currently focused key, if any.</summary>
    public TKey? FocusedKey => _focusedKey;

    /// <summary>The currently hovered element, resolved through the registry.</summary>
    public TElement? Hovered => Resolve(_hoveredKey);

    /// <summary>The currently focused element, resolved through the registry.</summary>
    public TElement? Focused => Resolve(_focusedKey);

    /// <summary>The active element (hovered wins over focused) — the tooltip target.</summary>
    public TElement? Active => Hovered ?? Focused;

    public bool IsHovered(TElement element) =>
        _hoveredKey is { } key && key.Equals(_options.KeySelector(element));

    public bool IsFocused(TElement element) =>
        _focusedKey is { } key && key.Equals(_options.KeySelector(element));

    /// <summary>Resolves a key to its element, or <c>null</c> when unknown/stale.</summary>
    public TElement? Resolve(TKey? key) =>
        key is { } value && _byKey.TryGetValue(value, out var element) ? element : null;

    /// <summary>
    /// Rebuilds the key→element registry and drops hovered/focused keys that no longer
    /// resolve. Call at the end of the chart's rebuild pass. Never fires callbacks or renders.
    /// </summary>
    public void SetElements(IReadOnlyList<TElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);

        _elements = elements;
        _byKey.Clear();
        foreach (var element in elements)
        {
            _byKey[_options.KeySelector(element)] = element;
        }

        if (_hoveredKey is { } hovered && !_byKey.ContainsKey(hovered))
        {
            _hoveredKey = null;
        }

        if (_focusedKey is { } focused && !_byKey.ContainsKey(focused))
        {
            _focusedKey = null;
        }
    }

    // ----- element-based transitions (SVG per-element handlers — the common case) -----

    public Task HoverAsync(TElement element) => HoverKeyAsync(_options.KeySelector(element));

    public Task HoverLeaveAsync(TElement element) => HoverLeaveKeyAsync(_options.KeySelector(element));

    public Task FocusAsync(TElement element) => FocusKeyAsync(_options.KeySelector(element));

    public Task BlurAsync(TElement element) => BlurKeyAsync(_options.KeySelector(element));

    public Task ActivateAsync(TElement element) => InvokeActivateAsync(element);

    public Task KeyDownAsync(KeyboardEventArgs args, TElement element) =>
        IsActivationKey(args) ? ActivateAsync(element) : Task.CompletedTask;

    // ----- key-based transitions (opt-in: canvas / hit-test surfaces) -----

    /// <summary>Hover the given key. No-op (no render) when it is already hovered.</summary>
    public async Task HoverKeyAsync(TKey key)
    {
        if (_hoveredKey is { } current && current.Equals(key))
        {
            return;
        }

        _hoveredKey = key;
        await _options.RequestRender();
        await NotifyActiveChangedAsync(key);
    }

    /// <summary>Clear the hover for a specific key (per-element mouse-out). Focus pins the hover.</summary>
    public async Task HoverLeaveKeyAsync(TKey key)
    {
        if (!(_hoveredKey is { } hovered && hovered.Equals(key)))
        {
            return;
        }

        if (_focusedKey is { } focused && focused.Equals(key))
        {
            return;
        }

        _hoveredKey = null;
        await _options.RequestRender();
    }

    /// <summary>Clear the hover when the pointer leaves the whole surface, unless focus pins it.</summary>
    public async Task HoverLeaveSurfaceAsync()
    {
        if (_hoveredKey is null)
        {
            return;
        }

        if (_focusedKey is { } focused && _hoveredKey is { } hovered && focused.Equals(hovered))
        {
            return;
        }

        _hoveredKey = null;
        await _options.RequestRender();
    }

    /// <summary>Focus the given key. Focus implies hover (contractual: tooltip on keyboard focus).</summary>
    public async Task FocusKeyAsync(TKey key)
    {
        _focusedKey = key;
        _hoveredKey = key;
        await _options.RequestRender();
        await NotifyActiveChangedAsync(key);
    }

    /// <summary>Clear focus/hover for a specific key (per-element blur). Clears only matching keys.</summary>
    public async Task BlurKeyAsync(TKey key)
    {
        if (_focusedKey is { } focused && focused.Equals(key))
        {
            _focusedKey = null;
        }

        if (_hoveredKey is { } hovered && hovered.Equals(key))
        {
            _hoveredKey = null;
        }

        await _options.RequestRender();
    }

    /// <summary>Clear focus and hover when the whole surface loses focus.</summary>
    public async Task BlurSurfaceAsync()
    {
        if (_focusedKey is null && _hoveredKey is null)
        {
            return;
        }

        _focusedKey = null;
        _hoveredKey = null;
        await _options.RequestRender();
    }

    public Task ActivateAsync(TKey key) => ActivateKeyAsync(key);

    public async Task ActivateKeyAsync(TKey key)
    {
        var element = Resolve(key);
        if (element is not null)
        {
            await InvokeActivateAsync(element);
        }
    }

    /// <summary>
    /// Entry-focus policy for a surface: focus the already-focused key, else the
    /// <paramref name="fallback"/> (e.g. the selected key), else the first element.
    /// </summary>
    public async Task FocusEntryAsync(TKey? fallback = null)
    {
        var entry = _focusedKey ?? fallback ?? FirstKey();
        if (entry is { } key)
        {
            await FocusKeyAsync(key);
        }
    }

    /// <summary>
    /// Surface keydown: Enter/Space commits the current key (focus + activate, mirroring a
    /// surface click); arrow keys rove focus through the <see cref="ChartInteractionOptions{TElement, TKey}.Navigator"/>.
    /// </summary>
    public async Task KeyDownSurfaceAsync(KeyboardEventArgs args, TKey? fallback = null)
    {
        var current = _focusedKey ?? fallback ?? FirstKey();
        if (current is not { } key)
        {
            return;
        }

        if (IsActivationKey(args))
        {
            await FocusKeyAsync(key);
            await ActivateKeyAsync(key);
            return;
        }

        if (_options.Navigator is null || ToDirection(args.Key) is not { } direction)
        {
            return;
        }

        var next = _options.Navigator(key, direction);
        if (next is not { } nextKey || nextKey.Equals(key))
        {
            return;
        }

        await FocusKeyAsync(nextKey);
    }

    private async Task NotifyActiveChangedAsync(TKey key)
    {
        if (_options.OnActiveChanged is null)
        {
            return;
        }

        var element = Resolve(key);
        if (element is not null)
        {
            await _options.OnActiveChanged(element);
        }
    }

    private async Task InvokeActivateAsync(TElement element)
    {
        if (_options.OnActivate is not null)
        {
            await _options.OnActivate(element);
        }
    }

    private TKey? FirstKey() =>
        _elements.Count > 0 ? _options.KeySelector(_elements[0]) : null;

    private static bool IsActivationKey(KeyboardEventArgs args) =>
        args.Key is "Enter" or " ";

    private static ChartArrowDirection? ToDirection(string? key) =>
        key switch
        {
            "ArrowLeft" => ChartArrowDirection.Left,
            "ArrowRight" => ChartArrowDirection.Right,
            "ArrowUp" => ChartArrowDirection.Up,
            "ArrowDown" => ChartArrowDirection.Down,
            _ => null
        };
}
