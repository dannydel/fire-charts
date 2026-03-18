using Microsoft.JSInterop;

namespace FireCharts.Tests;

internal sealed class RecordingJsRuntime : IJSRuntime
{
    public RecordingJsRuntime(RecordingJsObjectReference module)
    {
        Module = module;
    }

    public RecordingJsObjectReference Module { get; }

    public List<JsInvocation> Invocations { get; } = [];

    public int ImportCount => Invocations.Count(invocation => invocation.Identifier == "import");

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        var safeArgs = args ?? [];
        Invocations.Add(new JsInvocation(identifier, safeArgs));

        if (identifier == "import")
        {
            return ValueTask.FromResult((TValue)(object)Module);
        }

        throw new NotSupportedException($"Unexpected JS runtime invocation '{identifier}'.");
    }
}

internal sealed class RecordingJsObjectReference : IJSObjectReference
{
    private readonly Dictionary<string, Func<object?[], object?>> _handlers = new(StringComparer.Ordinal);

    public List<JsInvocation> Invocations { get; } = [];

    public bool DisposeCalled { get; private set; }

    public Exception? DisposeException { get; set; }

    public void SetupResult(string identifier, object? result) =>
        _handlers[identifier] = _ => result!;

    public void SetupHandler(string identifier, Func<object?[], object?> handler) =>
        _handlers[identifier] = handler;

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        var safeArgs = args ?? [];
        Invocations.Add(new JsInvocation(identifier, safeArgs));

        if (_handlers.TryGetValue(identifier, out var handler))
        {
            var result = handler(safeArgs);
            return ValueTask.FromResult(result is null ? default! : (TValue)result);
        }

        throw new NotSupportedException($"Unexpected JS module invocation '{identifier}'.");
    }

    public ValueTask DisposeAsync()
    {
        DisposeCalled = true;

        if (DisposeException is not null)
        {
            throw DisposeException;
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed record JsInvocation(string Identifier, IReadOnlyList<object?> Arguments);
