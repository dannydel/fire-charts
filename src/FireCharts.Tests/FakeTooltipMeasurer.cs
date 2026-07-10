using FireCharts.Components;
using Microsoft.AspNetCore.Components;

namespace FireCharts.Tests;

/// <summary>
/// In-memory <see cref="ITooltipMeasurer"/> for bUnit tests. Returns scripted
/// measurements in order (falling back to <c>null</c> once exhausted) and records the
/// element references it was asked to measure.
/// </summary>
internal sealed class FakeTooltipMeasurer : ITooltipMeasurer
{
    private readonly Queue<TooltipMeasurement?> _measurements;
    private TooltipMeasurement? _last;

    public FakeTooltipMeasurer(params TooltipMeasurement?[] measurements)
    {
        _measurements = new Queue<TooltipMeasurement?>(measurements);
    }

    public List<(ElementReference Host, ElementReference Tooltip)> Calls { get; } = [];

    public int MeasureCount => Calls.Count;

    public ValueTask<TooltipMeasurement?> MeasureAsync(
        ElementReference host,
        ElementReference tooltip,
        CancellationToken ct = default)
    {
        Calls.Add((host, tooltip));

        // Dequeue the next scripted measurement, sticking on the last one once the
        // queue is exhausted so remeasure-triggering re-renders keep a valid rect.
        if (_measurements.Count > 0)
        {
            _last = _measurements.Dequeue();
        }

        return ValueTask.FromResult(_last);
    }
}
