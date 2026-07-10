using FireCharts.Scales;

namespace FireCharts.Tests;

/// <summary>
/// Boundary tests for the unified <see cref="AxisScale"/> module. Golden values are derived
/// by executing the line-chart nice-number algorithm (breakpoints 1.5/3/7 round, 1/2/5/10
/// floor) by hand; these are the canonical reference for the whole chart family.
/// </summary>
public sealed class AxisScaleTests
{
    private const int Precision = 9;

    [Fact]
    public void PositiveData_ZeroBaseline_ProducesNiceScaleAndTicks()
    {
        // [10..50], include-zero clamps min to 0. range=NiceNumber(50)=50,
        // step=NiceNumber(12.5,round)=10, niceMax=ceil(50/10)*10=50.
        var scale = AxisScale.FromValues(new double[] { 10, 20, 30, 40, 50 }, 5, 400, 0);

        Assert.Equal(0, scale.Min, Precision);
        Assert.Equal(50, scale.Max, Precision);
        Assert.Equal(10, scale.Step, Precision);
        AssertTickValues(scale, 0, 10, 20, 30, 40, 50);

        // Y inversion: min -> pixelStart (bottom), max -> pixelEnd (top).
        Assert.Equal(400, scale.Ticks[0].Pixel, Precision);
        Assert.Equal(0, scale.Ticks[^1].Pixel, Precision);
    }

    [Fact]
    public void NegativeOnlyData_ZeroBaseline_ClampsMaxToZero()
    {
        // [-9,-3,-6], include-zero clamps max to 0. range=NiceNumber(9)=10,
        // step=NiceNumber(2.5,round)=2, niceMin=floor(-9/2)*2=-10.
        var scale = AxisScale.FromValues(new double[] { -9, -3, -6 }, 5, 400, 0);

        Assert.Equal(-10, scale.Min, Precision);
        Assert.Equal(0, scale.Max, Precision);
        Assert.Equal(2, scale.Step, Precision);
        AssertTickValues(scale, -10, -8, -6, -4, -2, 0);
    }

    [Fact]
    public void MixedSignData_SpansBothSidesOfZero()
    {
        // [-5,10]. range=NiceNumber(15)=20, step=NiceNumber(5,round)=5,
        // niceMin=floor(-5/5)*5=-5, niceMax=ceil(10/5)*5=10.
        var scale = AxisScale.FromValues(new double[] { -5, 10 }, 5, 400, 0);

        Assert.Equal(-5, scale.Min, Precision);
        Assert.Equal(10, scale.Max, Precision);
        Assert.Equal(5, scale.Step, Precision);
        AssertTickValues(scale, -5, 0, 5, 10);
    }

    [Fact]
    public void ZeroCrossingTick_IsPositiveZeroNotNegativeZero()
    {
        var scale = AxisScale.FromValues(new double[] { -5, 10 }, 5, 400, 0);

        var zeroTick = scale.Ticks.Single(tick => tick.Value == 0);
        Assert.False(double.IsNegative(zeroTick.Value));
    }

    [Fact]
    public void NonFiniteValues_AreFilteredOut()
    {
        var scale = AxisScale.FromValues(
            new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, 10, 20, 30 },
            5,
            400,
            0);

        Assert.Equal(0, scale.Min, Precision);
        Assert.Equal(30, scale.Max, Precision);
        Assert.Equal(10, scale.Step, Precision);
        AssertTickValues(scale, 0, 10, 20, 30);
    }

    [Fact]
    public void EmptyValues_FallBackToConfiguredMax()
    {
        var scale = AxisScale.FromValues(
            Array.Empty<double>(),
            400,
            0,
            new AxisScaleOptions
            {
                TickCount = 5,
                Baseline = AxisBaseline.IncludeZero,
                EmptyFallbackMax = 100
            });

        Assert.Equal(0, scale.Min, Precision);
        Assert.Equal(100, scale.Max, Precision);
        Assert.Equal(20, scale.Step, Precision);
        AssertTickValues(scale, 0, 20, 40, 60, 80, 100);
    }

    [Fact]
    public void DegenerateRange_IsPaddedAwayFromASinglePoint()
    {
        // [5,5] with DataExtent: |max-min| < 1e-6 -> pad by max(|5|*0.2,1)=1 -> [4,6].
        // range=NiceNumber(2)=2, step=NiceNumber(0.5,round)=0.5.
        var scale = AxisScale.FromValues(
            new double[] { 5, 5 },
            400,
            0,
            new AxisScaleOptions { TickCount = 5, Baseline = AxisBaseline.DataExtent });

        Assert.Equal(4, scale.Min, Precision);
        Assert.Equal(6, scale.Max, Precision);
        Assert.Equal(0.5, scale.Step, Precision);
        AssertTickValues(scale, 4, 4.5, 5, 5.5, 6);
    }

    [Fact]
    public void ForcedMax_OverridesDataExtent()
    {
        // Largest segment is 24, but MaxValue=60 forces the top. range=NiceNumber(60)=100,
        // step=NiceNumber(25,round)=20, niceMax=ceil(60/20)*20=60.
        var scale = AxisScale.FromValues(
            new double[] { 24, 12, 8, 18, 15, 6 },
            400,
            0,
            new AxisScaleOptions
            {
                TickCount = 5,
                Baseline = AxisBaseline.IncludeZero,
                ForcedMax = 60
            });

        Assert.Equal(0, scale.Min, Precision);
        Assert.Equal(60, scale.Max, Precision);
        Assert.Equal(20, scale.Step, Precision);
        AssertTickValues(scale, 0, 20, 40, 60);
    }

    [Fact]
    public void BarStyleSanitizedData_ProducesUnifiedNiceTop()
    {
        // The bar-family re-baseline: [67, 0, 0] (negatives/NaN sanitized to 0) now yields a
        // top of 80 via the unified algorithm (Algorithm 2 previously produced 75).
        var scale = AxisScale.FromValues(
            new double[] { 67, 0, 0 },
            400,
            0,
            new AxisScaleOptions
            {
                TickCount = 5,
                Baseline = AxisBaseline.IncludeZero,
                EmptyFallbackMax = 100
            });

        Assert.Equal(0, scale.Min, Precision);
        Assert.Equal(80, scale.Max, Precision);
        Assert.Equal(20, scale.Step, Precision);
        AssertTickValues(scale, 0, 20, 40, 60, 80);
    }

    [Fact]
    public void ToPixel_ClampsOutOfRangeValues()
    {
        var scale = AxisScale.FromValues(new double[] { 10, 20, 30, 40, 50 }, 5, 400, 0);

        Assert.Equal(400, scale.ToPixel(0), Precision);
        Assert.Equal(0, scale.ToPixel(50), Precision);
        Assert.Equal(200, scale.ToPixel(25), Precision);
        Assert.Equal(400, scale.ToPixel(-100), Precision); // clamped to min
        Assert.Equal(0, scale.ToPixel(1000), Precision);    // clamped to max
    }

    [Fact]
    public void ToPixel_HonorsPixelOrderingForHorizontalAxes()
    {
        // Passing left/right (ascending) yields a non-inverted axis for horizontal bars.
        var scale = AxisScale.FromValues(
            new double[] { 0, 100 },
            64,
            624,
            new AxisScaleOptions { TickCount = 5, Baseline = AxisBaseline.IncludeZero });

        Assert.Equal(64, scale.ToPixel(scale.Min), Precision);
        Assert.Equal(624, scale.ToPixel(scale.Max), Precision);
    }

    [Fact]
    public void Fraction_ReturnsClampedZeroToOne()
    {
        var scale = AxisScale.FromValues(new double[] { 10, 20, 30, 40, 50 }, 5, 400, 0);

        Assert.Equal(0.5, scale.Fraction(25), Precision);
        Assert.Equal(0, scale.Fraction(-100), Precision);
        Assert.Equal(1, scale.Fraction(1000), Precision);
    }

    [Fact]
    public void CrossFamilyRegression_IncludeZeroAndDataExtentAgreeWhenDataStraddlesZero()
    {
        // The original bug: bar (IncludeZero) and line (default/zero-baseline) disagreed on the
        // Y-axis top for identical data. They must now agree.
        var values = new double[] { 10, 20, 30, 40, 50 };

        var lineStyle = AxisScale.FromValues(values, 5, 400, 0); // short factory (zero baseline)
        var barStyle = AxisScale.FromValues(
            values,
            400,
            0,
            new AxisScaleOptions { TickCount = 5, Baseline = AxisBaseline.IncludeZero });

        Assert.Equal(lineStyle.Max, barStyle.Max, Precision);

        // For zero-straddling data, DataExtent and IncludeZero converge on the same Max.
        var straddling = new double[] { -5, 10 };
        var includeZero = AxisScale.FromValues(
            straddling,
            400,
            0,
            new AxisScaleOptions { TickCount = 5, Baseline = AxisBaseline.IncludeZero });
        var dataExtent = AxisScale.FromValues(
            straddling,
            400,
            0,
            new AxisScaleOptions { TickCount = 5, Baseline = AxisBaseline.DataExtent });

        Assert.Equal(includeZero.Max, dataExtent.Max, Precision);
        Assert.Equal(includeZero.Min, dataExtent.Min, Precision);
    }

    private static void AssertTickValues(AxisScale scale, params double[] expected)
    {
        Assert.Equal(expected.Length, scale.Ticks.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], scale.Ticks[i].Value, Precision);
        }
    }
}
