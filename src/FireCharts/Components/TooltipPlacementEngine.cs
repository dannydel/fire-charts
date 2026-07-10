namespace FireCharts.Components;

internal readonly record struct TooltipLayoutRequest(
    double AnchorX,
    double AnchorY,
    ChartTooltipPlacement PreferredPlacement,
    double Offset = 8,
    double Gutter = 8);

internal readonly record struct TooltipLayout(
    double Left,
    double Top,
    ChartTooltipPlacement Placement);

/// <summary>
/// Pure, in-process placement math: flip -> overflow-min -> clamp. Ported line-for-line
/// from the former <c>resolveTooltipPosition</c> in <c>chartTooltip.js</c>; the
/// overflow-min loop is order-sensitive and is intentionally not "cleaned up".
/// </summary>
internal static class TooltipPlacementEngine
{
    public static TooltipLayout Resolve(in TooltipLayoutRequest request, in TooltipMeasurement measurement)
    {
        var anchorX = request.AnchorX;
        var anchorY = request.AnchorY;
        var offset = request.Offset;
        var gutter = request.Gutter;
        var preferredPlacement = request.PreferredPlacement;

        var hostWidth = measurement.HostWidth;
        var hostHeight = measurement.HostHeight;
        var width = measurement.TooltipWidth;
        var height = measurement.TooltipHeight;

        var placements = preferredPlacement is ChartTooltipPlacement.Right or ChartTooltipPlacement.Left
            ? new[]
            {
                preferredPlacement,
                preferredPlacement == ChartTooltipPlacement.Right ? ChartTooltipPlacement.Left : ChartTooltipPlacement.Right
            }
            : new[]
            {
                preferredPlacement,
                preferredPlacement == ChartTooltipPlacement.Above ? ChartTooltipPlacement.Below : ChartTooltipPlacement.Above
            };

        var bestPlacement = placements[0];
        var bestPosition = GetCandidatePosition(anchorX, anchorY, width, height, bestPlacement, offset);
        var bestOverflow = GetOverflow(bestPosition, width, height, hostWidth, hostHeight, gutter);

        foreach (var placement in placements)
        {
            var position = GetCandidatePosition(anchorX, anchorY, width, height, placement, offset);
            var overflow = GetOverflow(position, width, height, hostWidth, hostHeight, gutter);

            if (overflow == 0)
            {
                bestPlacement = placement;
                bestPosition = position;
                bestOverflow = 0;
                break;
            }

            if (overflow < bestOverflow)
            {
                bestPlacement = placement;
                bestPosition = position;
                bestOverflow = overflow;
            }
        }

        var maxLeft = Math.Max(hostWidth - width - gutter, gutter);
        var maxTop = Math.Max(hostHeight - height - gutter, gutter);
        var left = Clamp(bestPosition.Left, gutter, maxLeft);
        var top = Clamp(bestPosition.Top, gutter, maxTop);

        return new TooltipLayout(left, top, bestPlacement);
    }

    private static (double Left, double Top) GetCandidatePosition(
        double anchorX,
        double anchorY,
        double width,
        double height,
        ChartTooltipPlacement placement,
        double offset) =>
        placement switch
        {
            ChartTooltipPlacement.Below => (anchorX - (width / 2), anchorY + offset),
            ChartTooltipPlacement.Left => (anchorX - width - offset, anchorY - (height / 2)),
            ChartTooltipPlacement.Right => (anchorX + offset, anchorY - (height / 2)),
            _ => (anchorX - (width / 2), anchorY - height - offset)
        };

    private static double GetOverflow(
        (double Left, double Top) position,
        double width,
        double height,
        double hostWidth,
        double hostHeight,
        double gutter)
    {
        var leftOverflow = Math.Max(gutter - position.Left, 0);
        var topOverflow = Math.Max(gutter - position.Top, 0);
        var rightOverflow = Math.Max((position.Left + width + gutter) - hostWidth, 0);
        var bottomOverflow = Math.Max((position.Top + height + gutter) - hostHeight, 0);

        return leftOverflow + topOverflow + rightOverflow + bottomOverflow;
    }

    private static double Clamp(double value, double min, double max) =>
        Math.Min(Math.Max(value, min), max);
}
