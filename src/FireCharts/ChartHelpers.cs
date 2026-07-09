using System.Globalization;

namespace FireCharts;

/// <summary>
/// Invariant-culture number formatting for SVG coordinate/attribute output.
/// The single source of truth for the library's number-to-string policy.
/// </summary>
internal static class ChartFormat
{
    /// <summary>
    /// Formats <paramref name="value"/> with one decimal place using the invariant
    /// culture. Non-finite values (NaN, +/-Infinity) collapse to <c>"0.0"</c>.
    /// </summary>
    public static string Fmt(double value) =>
        double.IsFinite(value)
            ? value.ToString("F1", CultureInfo.InvariantCulture)
            : "0.0";
}

/// <summary>
/// Canonical data-value sanitizer: NaN/negative/non-finite values collapse to zero.
/// </summary>
internal static class ChartValues
{
    /// <summary>
    /// Returns <paramref name="value"/> clamped to a non-negative finite number.
    /// Non-finite values (NaN, +/-Infinity) and negatives collapse to <c>0</c>.
    /// </summary>
    public static double Sanitize(double value) =>
        double.IsFinite(value) ? Math.Max(value, 0) : 0;
}

/// <summary>
/// Hover/emphasis color derivation from <c>#rrggbb</c> hex strings.
/// </summary>
internal static class ChartColor
{
    /// <summary>
    /// Darkens a <c>#rrggbb</c> hex color by subtracting <paramref name="step"/> from
    /// each channel, clamped at 0. When the input is not a 7-character <c>#rrggbb</c>
    /// string it returns <paramref name="fallback"/> (the library default hover color).
    /// </summary>
    public static string Darken(string? hex, int step = 28, string fallback = "#8f2f1a")
    {
        if (hex is null || hex.Length != 7 || !hex.StartsWith('#'))
        {
            return fallback;
        }

        var r = Convert.ToInt32(hex[1..3], 16);
        var g = Convert.ToInt32(hex[3..5], 16);
        var b = Convert.ToInt32(hex[5..7], 16);

        return $"#{Math.Max(r - step, 0):X2}{Math.Max(g - step, 0):X2}{Math.Max(b - step, 0):X2}";
    }

    /// <summary>
    /// Darkens a <c>#rrggbb</c> hex color by scaling each channel by
    /// <paramref name="factor"/> (truncated to an integer), clamped at 0. When the input
    /// is not a parseable 7-character <c>#rrggbb</c> string it is returned unchanged.
    /// </summary>
    public static string DarkenByFactor(string hex, double factor = 0.78)
    {
        if (hex.Length != 7 || !hex.StartsWith('#'))
        {
            return hex;
        }

        if (!int.TryParse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) ||
            !int.TryParse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) ||
            !int.TryParse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return hex;
        }

        return $"#{Math.Max((int)(r * factor), 0):X2}{Math.Max((int)(g * factor), 0):X2}{Math.Max((int)(b * factor), 0):X2}";
    }
}
