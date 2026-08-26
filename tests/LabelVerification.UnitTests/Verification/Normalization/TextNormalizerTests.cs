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
                "Stone’s Throw");

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
                "SMALL–BATCH");

        var emDash =
            _normalizer.NormalizeForComparison(
                "SMALL—BATCH");

        Assert.Equal(hyphen, enDash);
        Assert.Equal(hyphen, emDash);
        Assert.Equal("small-batch", hyphen);
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
        // Unicode full-width Latin characters should compare with their
        // ordinary ASCII representation after FormKC normalization.
        var result =
            _normalizer.NormalizeForComparison(
                "GOLDEN ALE");

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
                "Cuvée Réserve");

        Assert.Equal(
            "cuvée réserve",
            result);
    }

    [Fact]
    public void NormalizeForComparison_IsIdempotent()
    {
        var first =
            _normalizer.NormalizeForComparison(
                "  Stone’s   Throw  ");

        var second =
            _normalizer.NormalizeForComparison(
                first);

        Assert.Equal(
            first,
            second);
    }
}
