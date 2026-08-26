using LabelVerification.Application.LabelUnderstanding;
using LabelVerification.Application.Verification;
using LabelVerification.Application.Verification.NetContents;

namespace LabelVerification.UnitTests.Verification.NetContents;

public sealed class NetContentsVerifierTests
{
    private readonly NetContentsVerifier _verifier =
        new(new NetContentsNormalizer());

    [Fact]
    public void Verify_WithSameMilliliterValue_ReturnsPass()
    {
        var result =
            _verifier.Verify(
                750m,
                "mL",
                CreateObserved(
                    750m,
                    "mL",
                    "750 ML"));

        Assert.Equal(
            VerificationStatus.Pass,
            result.Status);

        Assert.Equal(
            "750 mL",
            result.NormalizedExpectedValue);

        Assert.Equal(
            "750 mL",
            result.NormalizedObservedValue);
    }

    [Fact]
    public void Verify_WithEquivalentLitersAndMilliliters_ReturnsPass()
    {
        var result =
            _verifier.Verify(
                0.75m,
                "L",
                CreateObserved(
                    750m,
                    "mL",
                    "750 ML"));

        Assert.Equal(
            VerificationStatus.Pass,
            result.Status);

        Assert.Equal(
            "750 mL",
            result.NormalizedExpectedValue);

        Assert.Equal(
            "750 mL",
            result.NormalizedObservedValue);
    }

    [Fact]
    public void Verify_WithEquivalentPintAndFluidOunces_ReturnsPass()
    {
        var result =
            _verifier.Verify(
                1m,
                "pint",
                CreateObserved(
                    16m,
                    "fl oz",
                    "16 FL OZ"));

        Assert.Equal(
            VerificationStatus.Pass,
            result.Status);

        Assert.Equal(
            result.NormalizedExpectedValue,
            result.NormalizedObservedValue);
    }

    [Fact]
    public void Verify_WithDifferentQuantities_ReturnsFail()
    {
        var result =
            _verifier.Verify(
                750m,
                "mL",
                CreateObserved(
                    700m,
                    "mL",
                    "700 ML"));

        Assert.Equal(
            VerificationStatus.Fail,
            result.Status);

        Assert.Contains(
            "mismatch",
            result.Explanation,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verify_WithEquivalentUnitsButDifferentQuantities_ReturnsFail()
    {
        var result =
            _verifier.Verify(
                0.75m,
                "L",
                CreateObserved(
                    500m,
                    "mL",
                    "500 ML"));

        Assert.Equal(
            VerificationStatus.Fail,
            result.Status);

        Assert.Equal(
            "750 mL",
            result.NormalizedExpectedValue);

        Assert.Equal(
            "500 mL",
            result.NormalizedObservedValue);
    }

    [Fact]
    public void Verify_WithoutObservedNetContents_ReturnsReview()
    {
        var result =
            _verifier.Verify(
                750m,
                "mL",
                observedNetContents: null);

        Assert.Equal(
            VerificationStatus.Review,
            result.Status);

        Assert.Null(
            result.ObservedValue);
    }

    [Fact]
    public void Verify_WithoutExpectedNetContents_ReturnsReview()
    {
        var result =
            _verifier.Verify(
                expectedValue: null,
                expectedUnit: null,
                CreateObserved(
                    750m,
                    "mL",
                    "750 ML"));

        Assert.Equal(
            VerificationStatus.Review,
            result.Status);
    }

    [Fact]
    public void Verify_WithUnsupportedExpectedUnit_ReturnsReview()
    {
        var result =
            _verifier.Verify(
                1m,
                "gallon",
                CreateObserved(
                    750m,
                    "mL",
                    "750 ML"));

        Assert.Equal(
            VerificationStatus.Review,
            result.Status);

        Assert.Contains(
            "not supported",
            result.Explanation,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verify_WithUnsupportedObservedUnit_ReturnsReview()
    {
        var result =
            _verifier.Verify(
                750m,
                "mL",
                CreateObserved(
                    1m,
                    "gallon",
                    "1 GALLON"));

        Assert.Equal(
            VerificationStatus.Review,
            result.Status);

        Assert.Contains(
            "not supported",
            result.Explanation,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verify_PreservesObservedEvidenceAndConfidence()
    {
        var result =
            _verifier.Verify(
                500m,
                "mL",
                CreateObserved(
                    500m,
                    "mL",
                    "1 PINT 0.9 FL OZ (500 ML)",
                    confidence: 0.991));

        Assert.Equal(
            VerificationStatus.Pass,
            result.Status);

        Assert.Equal(
            "1 PINT 0.9 FL OZ (500 ML)",
            result.Evidence);

        Assert.Equal(
            0.991,
            result.EvidenceConfidence);
    }

    private static ParsedLabelField<ParsedNetContents> CreateObserved(
        decimal value,
        string unit,
        string evidence,
        double confidence = 0.99)
    {
        return new ParsedLabelField<ParsedNetContents>
        {
            Value = new ParsedNetContents
            {
                Value = value,
                Unit = unit
            },
            Evidence = evidence,
            Confidence = confidence
        };
    }
}
