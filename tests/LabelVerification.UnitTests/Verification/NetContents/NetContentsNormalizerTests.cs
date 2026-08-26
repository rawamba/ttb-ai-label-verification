using LabelVerification.Application.Verification.NetContents;

namespace LabelVerification.UnitTests.Verification.NetContents;

public sealed class NetContentsNormalizerTests
{
    private readonly NetContentsNormalizer _normalizer = new();

    [Theory]
    [InlineData("mL")]
    [InlineData("ML")]
    [InlineData("ml")]
    [InlineData("milliliter")]
    [InlineData("milliliters")]
    [InlineData("millilitre")]
    [InlineData("millilitres")]
    public void TryNormalize_WithMilliliterAliases_ReturnsSameValue(
        string unit)
    {
        var succeeded =
            _normalizer.TryNormalizeToMilliliters(
                750m,
                unit,
                out var result);

        Assert.True(succeeded);
        Assert.Equal(750m, result);
    }

    [Theory]
    [InlineData("L")]
    [InlineData("liter")]
    [InlineData("liters")]
    [InlineData("litre")]
    [InlineData("litres")]
    public void TryNormalize_WithLiters_ConvertsToMilliliters(
        string unit)
    {
        var succeeded =
            _normalizer.TryNormalizeToMilliliters(
                0.75m,
                unit,
                out var result);

        Assert.True(succeeded);
        Assert.Equal(750m, result);
    }

    [Theory]
    [InlineData("FL OZ")]
    [InlineData("FL. OZ.")]
    [InlineData("fl oz")]
    [InlineData("fluid ounce")]
    [InlineData("fluid ounces")]
    public void TryNormalize_WithFluidOunces_ConvertsToMilliliters(
        string unit)
    {
        var succeeded =
            _normalizer.TryNormalizeToMilliliters(
                12m,
                unit,
                out var result);

        Assert.True(succeeded);

        Assert.Equal(
            354.8823547500m,
            result);
    }

    [Theory]
    [InlineData("pt")]
    [InlineData("PT")]
    [InlineData("pint")]
    [InlineData("pints")]
    public void TryNormalize_WithPints_ConvertsToMilliliters(
        string unit)
    {
        var succeeded =
            _normalizer.TryNormalizeToMilliliters(
                1m,
                unit,
                out var result);

        Assert.True(succeeded);

        Assert.Equal(
            473.176473m,
            result);
    }

    [Fact]
    public void TryNormalize_WithEquivalentPintAndFluidOunces_ReturnsSameValue()
    {
        var pintSucceeded =
            _normalizer.TryNormalizeToMilliliters(
                1m,
                "pint",
                out var pintResult);

        var ounceSucceeded =
            _normalizer.TryNormalizeToMilliliters(
                16m,
                "fl oz",
                out var ounceResult);

        Assert.True(pintSucceeded);
        Assert.True(ounceSucceeded);

        Assert.Equal(
            pintResult,
            ounceResult);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("gallons")]
    [InlineData("ounces")]
    public void TryNormalize_WithUnsupportedUnit_ReturnsFalse(
        string? unit)
    {
        var succeeded =
            _normalizer.TryNormalizeToMilliliters(
                750m,
                unit,
                out var result);

        Assert.False(succeeded);
        Assert.Equal(0m, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TryNormalize_WithNonPositiveValue_ReturnsFalse(
        int value)
    {
        var succeeded =
            _normalizer.TryNormalizeToMilliliters(
                value,
                "mL",
                out var result);

        Assert.False(succeeded);
        Assert.Equal(0m, result);
    }
}
