using System.ComponentModel;

namespace FireCharts.Layout;

/// <summary>
/// Sizing inputs shared by the categorical bar layout strategies. Values are expected to be
/// pre-clamped by the caller (<c>CategoricalBarChartCore</c>) so the strategies stay pure math.
/// Public only because it appears on the public <see cref="IBarLayoutStrategy"/> method
/// signatures; hidden from IntelliSense and not a supported API.
/// </summary>
/// <param name="Horizontal">When <c>true</c>, bars grow left-to-right along the X axis.</param>
/// <param name="BarWidthRatio">Fraction of a category slot occupied by its bar/cluster (0.1..1).</param>
/// <param name="GroupSpacing">Fraction trimmed from a cluster width to gap adjacent groups (0..0.8). Ignored by the stacked strategy.</param>
/// <param name="SeriesSpacing">Fraction of a per-series slot used as the gap between clustered bars (0..0.8). Ignored by the stacked strategy.</param>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly record struct BarLayoutOptions(
    bool Horizontal,
    double BarWidthRatio,
    double GroupSpacing,
    double SeriesSpacing);
