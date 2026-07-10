namespace FireCharts.Models;

public readonly record struct ChartPadding(
    double Top,
    double Right,
    double Bottom,
    double Left)
{
    public static readonly ChartPadding Zero = default;

    public static ChartPadding All(double value) =>
        new(value, value, value, value);

    public static ChartPadding Symmetric(double vertical, double horizontal) =>
        new(vertical, horizontal, vertical, horizontal);
}
