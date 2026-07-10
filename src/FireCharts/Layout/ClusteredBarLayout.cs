using FireCharts.Models;

namespace FireCharts.Layout;

/// <summary>
/// Places segments side by side within a space-trimmed cluster band. The axis maximum is the
/// largest single segment. Geometry ported verbatim from the former <c>FireClusteredBarChart</c>
/// <c>GetClusterBounds</c>/<c>GetSeriesSpacing</c>/<c>GetSegmentThickness</c>/<c>GetSegmentRect</c>.
/// </summary>
internal sealed class ClusteredBarLayout : IBarLayoutStrategy
{
    public double ComputeRawMaxValue(IEnumerable<IReadOnlyList<double>> groupedSegmentValues)
    {
        ArgumentNullException.ThrowIfNull(groupedSegmentValues);

        var max = 0d;
        var hasValue = false;
        foreach (var group in groupedSegmentValues)
        {
            foreach (var value in group)
            {
                hasValue = true;
                if (value > max)
                {
                    max = value;
                }
            }
        }

        return hasValue ? max : 0;
    }

    public SvgRect GetGroupBounds(int groupIndex, int groupCount, PlotArea area, BarLayoutOptions options)
    {
        if (groupCount <= 0)
        {
            return new SvgRect(0, 0, 0, 0);
        }

        if (options.Horizontal)
        {
            var step = area.Height / groupCount;
            var clusterHeight = Math.Max(step * options.BarWidthRatio * (1 - options.GroupSpacing), 1);
            var y = area.Top + groupIndex * step + (step - clusterHeight) / 2;
            return new SvgRect(area.Left, y, area.Width, clusterHeight);
        }

        var widthStep = area.Width / groupCount;
        var clusterWidth = Math.Max(widthStep * options.BarWidthRatio * (1 - options.GroupSpacing), 1);
        var x = area.Left + groupIndex * widthStep + (widthStep - clusterWidth) / 2;
        return new SvgRect(x, area.Top, clusterWidth, area.Height);
    }

    public IReadOnlyList<SvgRect> LayoutSegments(
        SvgRect groupBounds,
        IReadOnlyList<double> visibleValues,
        double maxValue,
        PlotArea area,
        BarLayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(visibleValues);

        var count = visibleValues.Count;
        var rects = new List<SvgRect>(count);
        if (count == 0)
        {
            return rects;
        }

        var seriesSpacingPixels = GetSeriesSpacing(groupBounds, count, options.SeriesSpacing, options.Horizontal);
        var segmentThickness = GetSegmentThickness(groupBounds, count, seriesSpacingPixels, options.Horizontal);

        for (var visibleIndex = 0; visibleIndex < count; visibleIndex++)
        {
            var value = visibleValues[visibleIndex];
            var scale = maxValue > 0 ? Math.Clamp(value / maxValue, 0, 1) : 0;
            var offset = visibleIndex * (segmentThickness + seriesSpacingPixels);

            if (options.Horizontal)
            {
                var width = Math.Max(scale * area.Width, 0);
                var segmentY = groupBounds.Y + offset;
                rects.Add(new SvgRect(area.Left, segmentY, width, segmentThickness));
            }
            else
            {
                var height = Math.Max(scale * area.Height, 0);
                var x = groupBounds.X + offset;
                var segmentTop = area.Bottom - height;
                rects.Add(new SvgRect(x, segmentTop, segmentThickness, height));
            }
        }

        return rects;
    }

    private static double GetSeriesSpacing(SvgRect clusterRect, int segmentCount, double safeSeriesSpacing, bool horizontal)
    {
        if (segmentCount <= 1)
        {
            return 0;
        }

        var dimension = horizontal ? clusterRect.Height : clusterRect.Width;
        var baseStep = dimension / segmentCount;
        return Math.Max(baseStep * safeSeriesSpacing, 0);
    }

    private static double GetSegmentThickness(SvgRect clusterRect, int segmentCount, double seriesSpacingPixels, bool horizontal)
    {
        var totalSpacing = seriesSpacingPixels * Math.Max(segmentCount - 1, 0);
        var dimension = horizontal ? clusterRect.Height : clusterRect.Width;
        var available = Math.Max(dimension - totalSpacing, 1);
        return Math.Max(available / segmentCount, 1);
    }
}
