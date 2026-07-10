namespace FireCharts.Models;

public readonly record struct PlotArea(
    double SurfaceWidth,
    double SurfaceHeight,
    double Left,
    double Top,
    double Right,
    double Bottom)
{
    public double Width => Math.Max(Right - Left, 1);

    public double Height => Math.Max(Bottom - Top, 1);

    public SvgPoint Center => new((Left + Right) / 2, (Top + Bottom) / 2);

    public double Radius(double margin = 0, double minimum = 1) =>
        Math.Max((Math.Min(Width, Height) / 2) - margin, minimum);

    public static PlotArea FromInset(double width, double height, ChartPadding pad)
    {
        var safeWidth = Math.Max(width, 1);
        var safeHeight = Math.Max(height, 1);
        return new PlotArea(
            safeWidth,
            safeHeight,
            pad.Left,
            pad.Top,
            safeWidth - pad.Right,
            safeHeight - pad.Bottom);
    }
}
