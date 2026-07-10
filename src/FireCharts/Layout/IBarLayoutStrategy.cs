using System.ComponentModel;
using FireCharts.Models;

namespace FireCharts.Layout;

/// <summary>
/// The geometry seam that distinguishes a stacked bar chart from a clustered one. Pure,
/// stateless math over a <see cref="PlotArea"/> so it is unit-testable without rendering.
/// Consumed by <c>CategoricalBarChartCore</c>; implementations are held as static singletons
/// on the public wrapper components. Public only because it appears on the (SDK-forced) public
/// core component's parameter surface; hidden from IntelliSense and not a supported API.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IBarLayoutStrategy
{
    /// <summary>
    /// The un-rounded axis maximum implied by the data: the largest stack total (stacked)
    /// versus the largest single segment (clustered). The core feeds this to
    /// <see cref="Scales.AxisScale"/> for nice rounding.
    /// </summary>
    double ComputeRawMaxValue(IEnumerable<IReadOnlyList<double>> groupedSegmentValues);

    /// <summary>
    /// The rectangle a category occupies: the full bar band (stacked) or the space-trimmed
    /// cluster band (clustered).
    /// </summary>
    SvgRect GetGroupBounds(int groupIndex, int groupCount, PlotArea area, BarLayoutOptions options);

    /// <summary>
    /// Lays out one rect per visible (positive) segment within a group: a running stack
    /// (stacked) or side-by-side slots (clustered). The result is index-aligned with
    /// <paramref name="visibleValues"/>.
    /// </summary>
    IReadOnlyList<SvgRect> LayoutSegments(
        SvgRect groupBounds,
        IReadOnlyList<double> visibleValues,
        double maxValue,
        PlotArea area,
        BarLayoutOptions options);
}
