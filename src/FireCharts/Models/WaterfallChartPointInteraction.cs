namespace FireCharts.Models;

public sealed record WaterfallChartPointInteraction<TItem>(
    TItem Item,
    int Index,
    string Label,
    double Value,
    double StartValue,
    double EndValue,
    double DisplayValue,
    WaterfallStepType StepType);
