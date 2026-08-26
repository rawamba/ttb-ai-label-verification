using LabelVerification.Application.LabelUnderstanding;
using LabelVerification.Application.Verification;
using LabelVerification.Application.Verification.Alcohol;

namespace LabelVerification.UnitTests.Verification.Alcohol;

public sealed class AlcoholValueVerifierTests
{
    private readonly AlcoholValueVerifier _verifier = new();

    [Fact]
    public void VerifyAlcoholByVolume_WithEqualValues_ReturnsPass()
    {
        var result =
            _verifier.VerifyAlcoholByVolume(
                45.0m,
                CreateObservedAbv(
                    45.0m,
                    "45% ALCOHOL BY VOLUME"));

        Assert.Equal(
            VerificationStatus.Pass,
            result.Status);

        Assert.Equal(
            "45%",
            result.ExpectedValue);

        Assert.Equal(
            "45%",
            result.ObservedValue);
    }

    [Fact]
    public void VerifyAlcoholByVolume_WithEquivalentDecimalScale_ReturnsPass()
    {
        var result =
            _verifier.VerifyAlcoholByVolume(
                45.00m,
                CreateObservedAbv(
                    45.0m,
                    "45.0% ALCOHOL BY VOLUME"));

        Assert.Equal(
            VerificationStatus.Pass,
            result.Status);
    }

    [Fact]
    public void VerifyAlcoholByVolume_WithDifferentValue_ReturnsFail()
    {
        var result =
            _verifier.VerifyAlcoholByVolume(
                45.0m,
                CreateObservedAbv(
                    40.0m,
                    "40% ALCOHOL BY VOLUME"));

        Assert.Equal(
            VerificationStatus.Fail,
            result.Status);

        Assert.Contains(
            "mismatch",
            result.Explanation,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerifyAlcoholByVolume_WithoutObservedValue_ReturnsReview()
    {
        var result =
            _verifier.VerifyAlcoholByVolume(
                45.0m,
                observedAlcoholByVolume: null);

        Assert.Equal(
            VerificationStatus.Review,
            result.Status);

        Assert.Null(
            result.ObservedValue);
    }

    [Fact]
    public void VerifyAlcoholByVolume_WithoutExpectedValue_ReturnsReview()
    {
        var result =
            _verifier.VerifyAlcoholByVolume(
                expectedAlcoholByVolume: null,
                CreateObservedAbv(
                    45.0m,
                    "45% ALCOHOL BY VOLUME"));

        Assert.Equal(
            VerificationStatus.Review,
            result.Status);
    }

    [Fact]
    public void VerifyAlcoholByVolume_PreservesEvidenceAndConfidence()
    {
        var result =
            _verifier.VerifyAlcoholByVolume(
                45.0m,
                CreateObservedAbv(
                    45.0m,
                    "45% ALCOHOL BY VOLUME",
                    confidence: 0.977));

        Assert.Equal(
            "45% ALCOHOL BY VOLUME",
            result.Evidence);

        Assert.Equal(
            0.977,
            result.EvidenceConfidence);
    }

    [Fact]
    public void VerifyProof_WithEqualValues_ReturnsPass()
    {
        var result =
            _verifier.VerifyProof(
                90,
                CreateObservedProof(
                    90,
                    "90 PROOF"));

        Assert.Equal(
            VerificationStatus.Pass,
            result.Status);

        Assert.Equal(
            "90 PROOF",
            result.ExpectedValue);

        Assert.Equal(
            "90 PROOF",
            result.ObservedValue);
    }

    [Fact]
    public void VerifyProof_WithDifferentValue_ReturnsFail()
    {
        var result =
            _verifier.VerifyProof(
                90,
                CreateObservedProof(
                    80,
                    "80 PROOF"));

        Assert.Equal(
            VerificationStatus.Fail,
            result.Status);

        Assert.Contains(
            "mismatch",
            result.Explanation,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerifyProof_WithoutObservedValue_ReturnsReview()
    {
        var result =
            _verifier.VerifyProof(
                90,
                observedProof: null);

        Assert.Equal(
            VerificationStatus.Review,
            result.Status);
    }

    [Fact]
    public void VerifyProof_WithoutExpectedValue_ReturnsReview()
    {
        var result =
            _verifier.VerifyProof(
                expectedProof: null,
                CreateObservedProof(
                    90,
                    "90 PROOF"));

        Assert.Equal(
            VerificationStatus.Review,
            result.Status);
    }

    private static ParsedLabelField<decimal> CreateObservedAbv(
        decimal value,
        string evidence,
        double confidence = 0.99)
    {
        return new ParsedLabelField<decimal>
        {
            Value = value,
            Evidence = evidence,
            Confidence = confidence
        };
    }

    private static ParsedLabelField<int> CreateObservedProof(
        int value,
        string evidence,
        double confidence = 0.99)
    {
        return new ParsedLabelField<int>
        {
            Value = value,
            Evidence = evidence,
            Confidence = confidence
        };
    }
}