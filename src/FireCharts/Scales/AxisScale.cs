using System.Collections.ObjectModel;

namespace FireCharts.Scales;

/// <summary>
/// Controls whether a value axis is anchored to zero or fitted to the data extent.
/// </summary>
public enum AxisBaseline
{
    /// <summary>The axis spans only the range of the supplied data.</summary>
    DataExtent,

    /// <summary>The axis is clamped so that zero is always included in the range.</summary>
    IncludeZero
}

/// <summary>
/// One gridline/label position: a nice, zero-normalized value and its projected pixel.
/// </summary>
public readonly record struct ScaleTick(double Value, double Pixel);

/// <summary>
/// Options controlling how <see cref="AxisScale"/> resolves a value axis.
/// </summary>
public readonly record struct AxisScaleOptions
{
    /// <summary>Target number of ticks. Coerced to a minimum of 2.</summary>
    public int TickCount { get; init; }

    /// <summary>Baseline behavior. Defaults to <see cref="AxisBaseline.DataExtent"/>.</summary>
    public AxisBaseline Baseline { get; init; }

    /// <summary>Optional forced maximum (e.g. a bar chart's <c>MaxValue</c> parameter).</summary>
    public double? ForcedMax { get; init; }

    /// <summary>Optional forced minimum.</summary>
    public double? ForcedMin { get; init; }

    /// <summary>Maximum used when no finite values are supplied. Defaults to 1.</summary>
    public double EmptyFallbackMax { get; init; }
}

/// <summary>
/// A resolved, immutable linear value axis: nice min/max/step, materialized ticks, and a
/// clamped value-to-pixel projection. Pure math with no dependency-injection surface.
/// </summary>
public sealed class AxisScale
{
    private const double Epsilon = 0.000001;
    private const int MaxTicks = 100;

    private AxisScale(
        double min,
        double max,
        double step,
        double pixelStart,
        double pixelEnd,
        IReadOnlyList<ScaleTick> ticks)
    {
        Min = min;
        Max = max;
        Step = step;
        PixelStart = pixelStart;
        PixelEnd = pixelEnd;
        Ticks = ticks;
    }

    /// <summary>Nice minimum of the axis.</summary>
    public double Min { get; }

    /// <summary>Nice maximum of the axis.</summary>
    public double Max { get; }

    /// <summary>Nice step between ticks.</summary>
    public double Step { get; }

    /// <summary>Pixel coordinate that <see cref="Min"/> maps to.</summary>
    public double PixelStart { get; }

    /// <summary>Pixel coordinate that <see cref="Max"/> maps to.</summary>
    public double PixelEnd { get; }

    /// <summary>The materialized tick values and their projected pixels.</summary>
    public IReadOnlyList<ScaleTick> Ticks { get; }

    /// <summary>
    /// Builds a zero-baseline value axis. The one-liner for the dominant case
    /// (line/scatter Y axes, which always include zero).
    /// </summary>
    public static AxisScale FromValues(
        IEnumerable<double> values,
        int tickCount,
        double pixelStart,
        double pixelEnd) =>
        FromValues(values, pixelStart, pixelEnd, new AxisScaleOptions
        {
            TickCount = tickCount,
            Baseline = AxisBaseline.IncludeZero
        });

    /// <summary>
    /// Builds a value axis with full control over baseline, forced bounds, and empty fallback.
    /// </summary>
    public static AxisScale FromValues(
        IEnumerable<double> values,
        double pixelStart,
        double pixelEnd,
        in AxisScaleOptions options)
    {
        ArgumentNullException.ThrowIfNull(values);

        var tickCount = Math.Max(options.TickCount, 2);
        var emptyFallbackMax = options.EmptyFallbackMax <= 0 ? 1 : options.EmptyFallbackMax;

        var (min, max, hasValue) = Extent(values);

        if (!hasValue)
        {
            min = 0;
            max = emptyFallbackMax;
        }

        if (options.ForcedMin is { } forcedMin && double.IsFinite(forcedMin))
        {
            min = forcedMin;
        }

        if (options.ForcedMax is { } forcedMax && double.IsFinite(forcedMax))
        {
            max = forcedMax;
        }

        if (min > max)
        {
            (min, max) = (max, min);
        }

        if (options.Baseline == AxisBaseline.IncludeZero)
        {
            if (min >= 0)
            {
                min = 0;
            }

            if (max <= 0)
            {
                max = 0;
            }
        }

        if (Math.Abs(max - min) < Epsilon)
        {
            var padding = Math.Max(Math.Abs(max) * 0.2, 1d);
            min -= padding;
            max += padding;

            if (options.Baseline == AxisBaseline.IncludeZero)
            {
                if (max <= 0)
                {
                    max = 0;
                }

                if (min >= 0)
                {
                    min = 0;
                }
            }
        }

        var (niceMin, niceMax, step) = GetNiceScale(min, max, tickCount);

        var ticks = BuildTicks(niceMin, niceMax, step, pixelStart, pixelEnd);
        return new AxisScale(niceMin, niceMax, step, pixelStart, pixelEnd, ticks);
    }

    /// <summary>
    /// Projects a value to a pixel on the axis, clamped to the axis range. Y-inversion is
    /// achieved simply by passing the bottom pixel as <c>pixelStart</c> and the top as
    /// <c>pixelEnd</c>.
    /// </summary>
    public double ToPixel(double value)
    {
        var ratio = (value - Min) / Math.Max(Max - Min, Epsilon);
        return PixelStart + (Math.Clamp(ratio, 0, 1) * (PixelEnd - PixelStart));
    }

    /// <summary>Returns the clamped 0..1 fraction of a value within the axis range.</summary>
    public double Fraction(double value) =>
        Math.Clamp((value - Min) / Math.Max(Max - Min, Epsilon), 0, 1);

    private static (double Min, double Max, bool HasValue) Extent(IEnumerable<double> values)
    {
        var min = 0d;
        var max = 0d;
        var hasValue = false;

        foreach (var value in values)
        {
            if (!double.IsFinite(value))
            {
                continue;
            }

            if (!hasValue)
            {
                min = max = value;
                hasValue = true;
                continue;
            }

            if (value < min)
            {
                min = value;
            }

            if (value > max)
            {
                max = value;
            }
        }

        return (min, max, hasValue);
    }

    private static (double Min, double Max, double Step) GetNiceScale(double min, double max, int tickCount)
    {
        var safeTickCount = Math.Max(tickCount, 2);
        var range = NiceNumber(max - min, false);
        var step = NiceNumber(range / (safeTickCount - 1), true);
        var niceMin = Math.Floor(min / step) * step;
        var niceMax = Math.Ceiling(max / step) * step;
        return (niceMin, niceMax, step);
    }

    private static double NiceNumber(double range, bool round)
    {
        if (range <= 0 || !double.IsFinite(range))
        {
            return 1;
        }

        var exponent = Math.Floor(Math.Log10(range));
        var fraction = range / Math.Pow(10, exponent);
        double niceFraction;

        if (round)
        {
            if (fraction < 1.5) niceFraction = 1;
            else if (fraction < 3) niceFraction = 2;
            else if (fraction < 7) niceFraction = 5;
            else niceFraction = 10;
        }
        else
        {
            if (fraction <= 1) niceFraction = 1;
            else if (fraction <= 2) niceFraction = 2;
            else if (fraction <= 5) niceFraction = 5;
            else niceFraction = 10;
        }

        return niceFraction * Math.Pow(10, exponent);
    }

    private static IReadOnlyList<ScaleTick> BuildTicks(
        double min,
        double max,
        double step,
        double pixelStart,
        double pixelEnd)
    {
        var ticks = new List<ScaleTick>();
        var value = min;
        var guard = 0;
        var span = Math.Max(max - min, Epsilon);

        while (value <= max + (step * 0.5) && guard < MaxTicks)
        {
            var normalized = NormalizeZero(value);
            var ratio = (normalized - min) / span;
            var pixel = pixelStart + (Math.Clamp(ratio, 0, 1) * (pixelEnd - pixelStart));
            ticks.Add(new ScaleTick(normalized, pixel));
            value += step;
            guard++;
        }

        return new ReadOnlyCollection<ScaleTick>(ticks);
    }

    private static double NormalizeZero(double value) =>
        Math.Abs(value) < Epsilon ? 0 : value;
}
