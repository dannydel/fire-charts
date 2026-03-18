namespace FireCharts.Models;

public sealed record ChartPointInteraction<TItem>(
    TItem Item,
    int Index,
    string Label,
    double Value);
