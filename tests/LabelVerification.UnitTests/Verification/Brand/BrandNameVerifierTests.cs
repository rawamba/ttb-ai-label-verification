using LabelVerification.Application.LabelUnderstanding;
using LabelVerification.Application.Verification;
using LabelVerification.Application.Verification.Brand;
using LabelVerification.Application.Verification.Normalization;

namespace LabelVerification.UnitTests.Verification.Brand;

public sealed class BrandNameVerifierTests
{
    private readonly BrandNameVerifier _verifier =
        new(
            new TextNormalizer(),
            new BrandNameVerificationOptions
            {
                PassThreshold = 0.95,
                ReviewThreshold = 0.80
            });

    [Fact]
    public void Verify_WithNormalizedExactMatch_ReturnsPass()
    {
        var observed =
            CreateObserved(
                "Stone\u2019s   Throw",
                0.98);

        var result =
            _verifier.Verify(
                "STONE'S THROW",
                observed);

        Assert.Equal(
            VerificationStatus.Pass,
            result.Status);

        Assert.Equal(
            1.0,
            result.Similarity);

        Assert.Equal(
            "stone's throw",
            result.NormalizedExpectedValue);

        Assert.Equal(
            "stone's throw",
            result.NormalizedObservedValue);
    }

    [Fact]
    public void Verify_WithVeryMinorDifference_ReturnsPass()
    {
        var observed =
            CreateObserved(
                "EXAMPL",
                0.98);

        var verifier =
            new BrandNameVerifier(
                new TextNormalizer(),
                new BrandNameVerificationOptions
                {
                    PassThreshold = 0.80,
                    ReviewThreshold = 0.60
                });

        var result =
            verifier.Verify(
                "EXAMPLE",
                observed);

        Assert.Equal(
            VerificationStatus.Pass,
            result.Status);

        Assert.NotNull(
            result.Similarity);
    }

    [Fact]
    public void Verify_WithPunctuationDifference_ReturnsReview()
    {
        var observed =
            CreateObserved(
                "STONES THROW",
                0.98);

        var result =
            _verifier.Verify(
                "STONE'S THROW",
                observed);

        Assert.Equal(
            VerificationStatus.Review,
            result.Status);

        Assert.NotNull(
            result.Similarity);

        Assert.InRange(
            result.Similarity.Value,
            0.80,
            0.949999);
    }

    [Fact]
    public void Verify_WithMateriallyDifferentBrand_ReturnsFail()
    {
        var observed =
            CreateObserved(
                "RIVER BEND",
                0.99);

        var result =
            _verifier.Verify(
                "STONE'S THROW",
                observed);

        Assert.Equal(
            VerificationStatus.Fail,
            result.Status);

        Assert.NotNull(
            result.Similarity);

        Assert.True(
            result.Similarity < 0.80);
    }

    [Fact]
    public void Verify_WithoutObservedBrand_ReturnsReview()
    {
        var result =
            _verifier.Verify(
                "STONE'S THROW",
                observedBrandName: null);

        Assert.Equal(
            VerificationStatus.Review,
            result.Status);

        Assert.Null(
            result.ObservedValue);

        Assert.Contains(
            "No brand-name evidence",
            result.Explanation);
    }

    [Fact]
    public void Verify_WithoutExpectedBrand_ReturnsReview()
    {
        var result =
            _verifier.Verify(
                expectedBrandName: null,
                CreateObserved(
                    "STONE'S THROW",
                    0.98));

        Assert.Equal(
            VerificationStatus.Review,
            result.Status);
    }

    [Fact]
    public void Verify_ResultPreservesEvidenceAndConfidence()
    {
        var observed =
            CreateObserved(
                "Stone\u2019s Throw",
                0.977,
                "Stone\u2019s Throw");

        var result =
            _verifier.Verify(
                "STONE'S THROW",
                observed);

        Assert.Equal(
            "STONE'S THROW",
            result.ExpectedValue);

        Assert.Equal(
            "Stone\u2019s Throw",
            result.ObservedValue);

        Assert.Equal(
            "Stone\u2019s Throw",
            result.Evidence);

        Assert.Equal(
            0.977,
            result.EvidenceConfidence);
    }

    [Fact]
    public void Constructor_WithInvalidThresholdOrder_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new BrandNameVerifier(
                new TextNormalizer(),
                new BrandNameVerificationOptions
                {
                    PassThreshold = 0.80,
                    ReviewThreshold = 0.90
                }));
    }

    private static ParsedLabelField<string> CreateObserved(
        string value,
        double confidence,
        string? evidence = null)
    {
        return new ParsedLabelField<string>
        {
            Value = value,
            Evidence = evidence ?? value,
            Confidence = confidence
        };
    }
}