using LabelVerification.Application.Verification.Normalization;

namespace LabelVerification.UnitTests.Verification.Normalization;

public sealed class TextNormalizerTests
{
    private readonly TextNormalizer _normalizer = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void NormalizeForComparison_WithEmptyInput_ReturnsEmpty(
        string? value)
    {
        var result =
            _normalizer.NormalizeForComparison(value);

        Assert.Equal(
            string.Empty,
            result);
    }

    [Fact]
    public void NormalizeForComparison_NormalizesCase()
    {
        var result =
            _normalizer.NormalizeForComparison(
                "GOLDEN ALE");

        Assert.Equal(
            "golden ale",
            result);
    }

    [Fact]
    public void NormalizeForComparison_CollapsesWhitespace()
    {
        var result =
            _normalizer.NormalizeForComparison(
                "  Kentucky   Straight\tBourbon\r\nWhiskey  ");

        Assert.Equal(
            "kentucky straight bourbon whiskey",
            result);
    }

    [Fact]
    public void NormalizeForComparison_NormalizesNonBreakingWhitespace()
    {
        var value =
            "STONE'S\u00A0THROW";

        var result =
            _normalizer.NormalizeForComparison(value);

        Assert.Equal(
            "stone's throw",
            result);
    }

    [Fact]
    public void NormalizeForComparison_NormalizesTypographicApostrophe()
    {
        var expected =
            _normalizer.NormalizeForComparison(
                "STONE'S THROW");

        var observed =
            _normalizer.NormalizeForComparison(
                "Stone\u2019s Throw");

        Assert.Equal(
            expected,
            observed);

        Assert.Equal(
            "stone's throw",
            observed);
    }

    [Fact]
    public void NormalizeForComparison_NormalizesUnicodeDashes()
    {
        var hyphen =
            _normalizer.NormalizeForComparison(
                "SMALL-BATCH");

        var enDash =
            _normalizer.NormalizeForComparison(
                "SMALL\u2013BATCH");

        var emDash =
            _normalizer.NormalizeForComparison(
                "SMALL\u2014BATCH");

        Assert.Equal(hyphen, enDash);
        Assert.Equal(hyphen, emDash);

        Assert.Equal(
            "small-batch",
            hyphen);
    }

    [Fact]
    public void NormalizeForComparison_RemovesZeroWidthFormatting()
    {
        var result =
            _normalizer.NormalizeForComparison(
                "Golden\u200BAle");

        Assert.Equal(
            "goldenale",
            result);
    }

    [Fact]
    public void NormalizeForComparison_NormalizesCompatibilityCharacters()
    {
        // Full-width Unicode Latin characters should normalize to their
        // ordinary ASCII equivalents through Unicode FormKC normalization.
        var result =
            _normalizer.NormalizeForComparison(
                "\uFF27\uFF2F\uFF2C\uFF24\uFF25\uFF2E " +
                "\uFF21\uFF2C\uFF25");

        Assert.Equal(
            "golden ale",
            result);
    }

    [Fact]
    public void NormalizeForComparison_PreservesMeaningfulPunctuation()
    {
        var result =
            _normalizer.NormalizeForComparison(
                "Smith & Sons' Reserve");

        Assert.Equal(
            "smith & sons' reserve",
            result);
    }

    [Fact]
    public void NormalizeForComparison_PreservesDiacritics()
    {
        var result =
            _normalizer.NormalizeForComparison(
                "Cuv\u00E9e R\u00E9serve");

        Assert.Equal(
            "cuv\u00E9e r\u00E9serve",
            result);
    }

    [Fact]
    public void NormalizeForComparison_IsIdempotent()
    {
        var first =
            _normalizer.NormalizeForComparison(
                "  Stone\u2019s   Throw  ");

        var second =
            _normalizer.NormalizeForComparison(
                first);

        Assert.Equal(
            first,
            second);

        Assert.Equal(
            "stone's throw",
            first);
    }
}
