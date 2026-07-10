using FireCharts.Models;

namespace FireCharts.Layout;

/// <summary>
/// Stacks segments end-to-end within the full bar band. The axis maximum is the largest
/// stack total. Geometry ported verbatim from the former <c>FireStackedBarChart</c>
/// <c>GetBarBounds</c>/<c>GetSegmentRect</c>.
/// </summary>
internal sealed class StackedBarLayout : IBarLayoutStrategy
{
    public double ComputeRawMaxValue(IEnumerable<IReadOnlyList<double>> groupedSegmentValues)
    {
        ArgumentNullException.ThrowIfNull(groupedSegmentValues);

        var max = 0d;
        var hasGroup = false;
        foreach (var group in groupedSegmentValues)
        {
            hasGroup = true;
            var total = 0d;
            foreach (var value in group)
            {
                total += value;
            }

            if (total > max)
            {
                max = total;
            }
        }

        return hasGroup ? max : 0;
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
            var barHeight = Math.Max(step * options.BarWidthRatio, 1);
            var y = area.Top + groupIndex * step + (step - barHeight) / 2;
            return new SvgRect(area.Left, y, area.Width, barHeight);
        }

        var widthStep = area.Width / groupCount;
        var barWidth = Math.Max(widthStep * options.BarWidthRatio, 1);
        var x = area.Left + groupIndex * widthStep + (widthStep - barWidth) / 2;
        return new SvgRect(x, area.Top, barWidth, area.Height);
    }

    public IReadOnlyList<SvgRect> LayoutSegments(
        SvgRect groupBounds,
        IReadOnlyList<double> visibleValues,
        double maxValue,
        PlotArea area,
        BarLayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(visibleValues);

        var rects = new List<SvgRect>(visibleValues.Count);
        var runningOffset = 0d;

        foreach (var value in visibleValues)
        {
            var scale = maxValue > 0 ? Math.Clamp(value / maxValue, 0, 1) : 0;
            var offsetScale = maxValue > 0 ? Math.Clamp(runningOffset / maxValue, 0, 1) : 0;

            if (options.Horizontal)
            {
                var width = Math.Max(scale * area.Width, 0);
                var x = area.Left + offsetScale * area.Width;
                rects.Add(new SvgRect(x, groupBounds.Y, width, groupBounds.Height));
            }
            else
            {
                var height = Math.Max(scale * area.Height, 0);
                var topOffset = offsetScale * area.Height;
                var y = area.Bottom - topOffset - height;
                rects.Add(new SvgRect(groupBounds.X, y, groupBounds.Width, height));
            }

            runningOffset += value;
        }

        return rects;
    }
}
