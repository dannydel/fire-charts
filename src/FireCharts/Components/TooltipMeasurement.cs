using Microsoft.AspNetCore.Components;

namespace FireCharts.Components;

/// <summary>
/// The four scalars the tooltip placement algorithm consumes from the DOM: the host
/// and tooltip dimensions. This is the entire DOM dependency of the placement math.
/// </summary>
internal readonly record struct TooltipMeasurement(
    double HostWidth,
    double HostHeight,
    double TooltipWidth,
    double TooltipHeight);

/// <summary>
/// Port for the only operation that must cross into the DOM: measuring the host and
/// tooltip elements.
/// </summary>
internal interface ITooltipMeasurer
{
    /// <summary>
    /// Measures the host and tooltip elements. Returns <c>null</c> when the elements
    /// are not laid out yet.
    /// </summary>
    ValueTask<TooltipMeasurement?> MeasureAsync(
        ElementReference host,
        ElementReference tooltip,
        CancellationToken ct = default);
}
