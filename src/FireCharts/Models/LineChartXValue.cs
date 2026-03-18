using System.Globalization;

namespace FireCharts.Models;

public readonly record struct LineChartXValue(LineChartXValueKind Kind, double NumericValue)
{
    public bool IsDateTime => Kind == LineChartXValueKind.DateTime;

    public static LineChartXValue FromNumber(double value) =>
        new(LineChartXValueKind.Number, value);

    public static LineChartXValue FromDateTime(DateTime value) =>
        new(LineChartXValueKind.DateTime, value.ToOADate());

    public DateTime ToDateTime() => DateTime.FromOADate(NumericValue);

    public override string ToString() =>
        Kind == LineChartXValueKind.DateTime
            ? ToDateTime().ToString("u", CultureInfo.InvariantCulture)
            : NumericValue.ToString("0.##", CultureInfo.InvariantCulture);
}
