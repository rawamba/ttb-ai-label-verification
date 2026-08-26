using LabelVerification.Application.LabelUnderstanding;
using LabelVerification.Application.Verification;
using LabelVerification.Application.Verification.GovernmentWarning;

namespace LabelVerification.UnitTests.Verification.GovernmentWarning;

public sealed class GovernmentWarningVerifierTests
{
    private const string RequiredHeading =
        "GOVERNMENT WARNING:";

    private const string RequiredWarning =
        "GOVERNMENT WARNING: (1) According to the Surgeon General, " +
        "women should not drink alcoholic beverages during pregnancy " +
        "because of the risk of birth defects. " +
        "(2) Consumption of alcoholic beverages impairs your ability " +
        "to drive a car or operate machinery, and may cause health problems.";

    private readonly GovernmentWarningVerifier _verifier =
        new(
            new GovernmentWarningVerificationOptions
            {
                MinimumOcrConfidence = 0.90,
                MinimumStyleConfidence = 0.90,
                RequireBoldHeading = true
            });

    [Fact]
    public void Verify_WithExactWarningAndBoldHeading_ReturnsPass()
    {
        var observed =
            CreateObserved(
                RequiredWarning,
                confidence: 0.99);

        var ocrResult =
            CreateOcrResult(
                RequiredWarning,
                CreateHeadingStyle(
                    RequiredWarning,
                    OcrFontWeight.Bold,
                    confidence: 0.99));

        var result =
            _verifier.Verify(
                observed,
                ocrResult);

        Assert.Equal(
            VerificationStatus.Pass,
            result.Status);
    }

    [Fact]
    public void Verify_WithMultilineExactWarning_ReturnsPass()
    {
        var multiline =
            RequiredWarning
                .Replace(
                    "Surgeon General, ",
                    "Surgeon General,\r\n")
                .Replace(
                    "(2) Consumption",
                    "\r\n(2) Consumption");

        var observed =
            CreateObserved(
                multiline,
                confidence: 0.99);

        var ocrResult =
            CreateOcrResult(
                RequiredWarning,
                CreateHeadingStyle(
                    RequiredWarning,
                    OcrFontWeight.Bold,
                    confidence: 0.99));

        var result =
            _verifier.Verify(
                observed,
                ocrResult);

        Assert.Equal(
            VerificationStatus.Pass,
            result.Status);
    }

    [Fact]
    public void Verify_WithIncorrectHeadingCapitalization_ReturnsFail()
    {
        var warning =
            RequiredWarning.Replace(
                RequiredHeading,
                "Government Warning:");

        var result =
            _verifier.Verify(
                CreateObserved(
                    warning,
                    confidence: 0.99),
                CreateOcrResult(warning));

        Assert.Equal(
            VerificationStatus.Fail,
            result.Status);

        Assert.Contains(
            "capitalization",
            result.Explanation,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verify_WithChangedPunctuation_ReturnsFail()
    {
        var warning =
            RequiredWarning.Replace(
                "Surgeon General,",
                "Surgeon General");

        var result =
            _verifier.Verify(
                CreateObserved(
                    warning,
                    confidence: 0.99),
                CreateOcrResult(warning));

        Assert.Equal(
            VerificationStatus.Fail,
            result.Status);

        Assert.Contains(
            "punctuation",
            result.Explanation,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verify_WithChangedWording_ReturnsFail()
    {
        var warning =
            RequiredWarning.Replace(
                "health problems.",
                "serious health problems.");

        var result =
            _verifier.Verify(
                CreateObserved(
                    warning,
                    confidence: 0.99),
                CreateOcrResult(warning));

        Assert.Equal(
            VerificationStatus.Fail,
            result.Status);
    }

    [Fact]
    public void Verify_WithoutWarningEvidence_ReturnsReview()
    {
        var result =
            _verifier.Verify(
                observedWarning: null,
                CreateOcrResult(
                    "Example Golden Ale"));

        Assert.Equal(
            VerificationStatus.Review,
            result.Status);
    }

    [Fact]
    public void Verify_WithLowOcrConfidence_ReturnsReview()
    {
        var result =
            _verifier.Verify(
                CreateObserved(
                    RequiredWarning,
                    confidence: 0.70),
                CreateOcrResult(
                    RequiredWarning,
                    CreateHeadingStyle(
                        RequiredWarning,
                        OcrFontWeight.Bold,
                        confidence: 0.99)));

        Assert.Equal(
            VerificationStatus.Review,
            result.Status);

        Assert.Contains(
            "confidence",
            result.Explanation,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verify_WithCorrectTextButNoStyleEvidence_ReturnsReview()
    {
        var result =
            _verifier.Verify(
                CreateObserved(
                    RequiredWarning,
                    confidence: 0.99),
                CreateOcrResult(
                    RequiredWarning));

        Assert.Equal(
            VerificationStatus.Review,
            result.Status);

        Assert.Contains(
            "style",
            result.Explanation,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verify_WithHighConfidenceNormalHeading_ReturnsFail()
    {
        var result =
            _verifier.Verify(
                CreateObserved(
                    RequiredWarning,
                    confidence: 0.99),
                CreateOcrResult(
                    RequiredWarning,
                    CreateHeadingStyle(
                        RequiredWarning,
                        OcrFontWeight.Normal,
                        confidence: 0.99)));

        Assert.Equal(
            VerificationStatus.Fail,
            result.Status);

        Assert.Contains(
            "not bold",
            result.Explanation,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verify_PreservesObservedEvidence()
    {
        var observed =
            CreateObserved(
                RequiredWarning,
                confidence: 0.991);

        var result =
            _verifier.Verify(
                observed,
                CreateOcrResult(
                    RequiredWarning,
                    CreateHeadingStyle(
                        RequiredWarning,
                        OcrFontWeight.Bold,
                        confidence: 0.99)));

        Assert.Equal(
            RequiredWarning,
            result.Evidence);

        Assert.Equal(
            0.991,
            result.EvidenceConfidence);
    }

    private static ParsedLabelField<string> CreateObserved(
        string value,
        double confidence)
    {
        return new ParsedLabelField<string>
        {
            Value = value,
            Evidence = value,
            Confidence = confidence
        };
    }

    private static OcrResult CreateOcrResult(
        string text,
        params OcrTextStyle[] styles)
    {
        return new OcrResult
        {
            Text = text,
            Lines = [],
            Words = [],
            Styles = styles,
            Confidence = 0.99,
            Duration = TimeSpan.FromMilliseconds(100),
            Provider = "TestOCR",
            ModelVersion = "test"
        };
    }

    private static OcrTextStyle CreateHeadingStyle(
        string text,
        OcrFontWeight fontWeight,
        double confidence)
    {
        var offset =
            text.IndexOf(
                RequiredHeading,
                StringComparison.Ordinal);

        return new OcrTextStyle
        {
            Offset = offset,
            Length = RequiredHeading.Length,
            Text = RequiredHeading,
            FontWeight = fontWeight,
            Confidence = confidence
        };
    }
}