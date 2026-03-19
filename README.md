# Fire Charts

A lightweight Blazor charting library with an example app and test project.

## Projects

- `src/FireCharts` — Reusable chart components library (`net10.0`)
- `src/FireCharts.Example` — Blazor WebAssembly demo app
- `src/FireCharts.Tests` — xUnit/bUnit test project

## Included Components

- `FireBarChart`
- `FireStackedBarChart`
- `FireLineChart`
- `FirePieChart`
- `FireScatterChart`
- `FireHeatmapChart`

## Quick Start

From the repository root:

```bash
# Restore dependencies
dotnet restore

# Build library
dotnet build src/FireCharts/FireCharts.csproj

# Run example app
dotnet run --project src/FireCharts.Example/FireCharts.Example.csproj

# Run tests
dotnet test src/FireCharts.Tests/FireCharts.Tests.csproj
```

## Notes

- Target framework: `net10.0`
- The example app references the library project directly for local development.
