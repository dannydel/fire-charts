using System.Globalization;

namespace FireCharts.Tests;

/// <summary>
/// Boundary coverage for the shared <see cref="ChartFormat"/>, <see cref="ChartValues"/>
/// and <see cref="ChartColor"/> primitives that the chart components delegate to.
/// </summary>
public sealed class ChartHelpersTests
{
    [Fact]
    public void FmtFormatsFiniteValueWithOneDecimalPlace()
    {
        Assert.Equal("1234.5", ChartFormat.Fmt(1234.5));
        Assert.Equal("0.0", ChartFormat.Fmt(0));
        Assert.Equal("-3.0", ChartFormat.Fmt(-3));
    }

    [Fact]
    public void FmtCollapsesNonFiniteValuesToZero()
    {
        Assert.Equal("0.0", ChartFormat.Fmt(double.NaN));
        Assert.Equal("0.0", ChartFormat.Fmt(double.PositiveInfinity));
        Assert.Equal("0.0", ChartFormat.Fmt(double.NegativeInfinity));
    }

    [Fact]
    public void FmtPreservesIeeeNegativeZero()
    {
        // Negative zero is finite, so it flows through F1 formatting unchanged.
        Assert.Equal("-0.0", ChartFormat.Fmt(-0.0));
    }

    [Fact]
    public void FmtUsesInvariantCultureRegardlessOfCurrentCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            // de-DE uses ',' as the decimal separator; invariant formatting must ignore it.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            Assert.Equal("1234.5", ChartFormat.Fmt(1234.5));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void SanitizeClampsNegativeAndNonFiniteToZero()
    {
        Assert.Equal(0, ChartValues.Sanitize(double.NaN));
        Assert.Equal(0, ChartValues.Sanitize(double.PositiveInfinity));
        Assert.Equal(0, ChartValues.Sanitize(double.NegativeInfinity));
        Assert.Equal(0, ChartValues.Sanitize(-42.5));
        Assert.Equal(0, ChartValues.Sanitize(-0.0));
    }

    [Fact]
    public void SanitizePassesThroughFiniteNonNegativeValues()
    {
        Assert.Equal(0, ChartValues.Sanitize(0));
        Assert.Equal(17.25, ChartValues.Sanitize(17.25));
        Assert.Equal(double.MaxValue, ChartValues.Sanitize(double.MaxValue));
    }

    [Fact]
    public void DarkenSubtractsStepFromEachChannel()
    {
        // 0xFF - 28 = 0xE3 on each channel.
        Assert.Equal("#E3E3E3", ChartColor.Darken("#ffffff"));
    }

    [Fact]
    public void DarkenClampsEachChannelAtZero()
    {
        // Every channel underflows below zero and clamps to 0x00.
        Assert.Equal("#000000", ChartColor.Darken("#101010"));
    }

    [Fact]
    public void DarkenHonoursCustomStep()
    {
        Assert.Equal("#F1F1F1", ChartColor.Darken("#ffffff", step: 14));
    }

    [Fact]
    public void DarkenFallsBackForNullShortOrMalformedInput()
    {
        Assert.Equal("#8f2f1a", ChartColor.Darken(null));
        Assert.Equal("#8f2f1a", ChartColor.Darken("#abc"));
        Assert.Equal("#8f2f1a", ChartColor.Darken("abcdef"));
        Assert.Equal("#8f2f1a", ChartColor.Darken(string.Empty));
    }

    [Fact]
    public void DarkenHonoursCustomFallback()
    {
        Assert.Equal("#000000", ChartColor.Darken(null, fallback: "#000000"));
    }

    [Fact]
    public void DarkenByFactorScalesEachChannel()
    {
        // 0xFF * 0.78 = 198.9 -> truncated to 198 = 0xC6.
        Assert.Equal("#C6C6C6", ChartColor.DarkenByFactor("#ffffff"));
    }

    [Fact]
    public void DarkenByFactorClampsEachChannelAtZero()
    {
        Assert.Equal("#000000", ChartColor.DarkenByFactor("#000000"));
    }

    [Fact]
    public void DarkenByFactorHonoursCustomFactor()
    {
        // 0xFF * 0.5 = 127.5 -> 127 = 0x7F.
        Assert.Equal("#7F7F7F", ChartColor.DarkenByFactor("#ffffff", factor: 0.5));
    }

    [Fact]
    public void DarkenByFactorReturnsInputUnchangedForMalformedHex()
    {
        // Distinct from Darken: the multiplicative variant echoes the input back.
        Assert.Equal("#abc", ChartColor.DarkenByFactor("#abc"));
        Assert.Equal("abcdef", ChartColor.DarkenByFactor("abcdef"));
        Assert.Equal("#gggggg", ChartColor.DarkenByFactor("#gggggg"));
    }
}
