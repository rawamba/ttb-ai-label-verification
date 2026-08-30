using LabelVerification.Application.LabelUnderstanding;
using LabelVerification.Application.Verification;
using LabelVerification.Application.Verification.Brand;
using LabelVerification.Application.Verification.Normalization;

namespace LabelVerification.UnitTests.Verification.Brand;

/// <summary>
/// Regression tests based on the actual Azure Document Intelligence
/// segmentation observed for the nonstandard-layout label fixture.
/// </summary>
public sealed class BrandMergedOcrResolutionTests
{
    [Fact]
    public void Resolve_WhenAzureMergesClassTypeAndBrand_RecoversExactObservedBrandSpan()
    {
        // Arrange
        //
        // Azure Document Intelligence returned this exact merged line for the
        // nonstandard-layout regression fixture:
        //
        // KENTUCKY STRAIGHT BOURBON WHISKEY OLD TOM DISTILLERY
        //
        // The parser correctly cannot treat that entire line as a brand because
        // it contains class/type terminology. The resolver must therefore
        // recover only an exact observed brand span.
        var verifier =
            new BrandNameVerifier(
                new TextNormalizer(),
                new BrandNameVerificationOptions
                {
                    PassThreshold = 0.95,
                    ReviewThreshold = 0.80
                });

        var resolver =
            new BrandNameCandidateResolver(
                verifier);

        var ocrResult =
            CreateObservedAzureOcrResult();

        // Act
        var resolved =
            resolver.Resolve(
                "Old Tom Distillery",
                currentBrandName: null,
                candidates: [],
                ocrResult,
                nameAndAddress: null);

        // Assert
        Assert.NotNull(
            resolved);

        Assert.Equal(
            "OLD TOM DISTILLERY",
            resolved.Value);

        // Evidence remains the complete OCR line from which the observed span
        // was recovered. The application value is never substituted as
        // observed evidence.
        Assert.Equal(
            "KENTUCKY STRAIGHT BOURBON WHISKEY OLD TOM DISTILLERY",
            resolved.Evidence);

        var verification =
            verifier.Verify(
                "Old Tom Distillery",
                resolved);

        Assert.Equal(
            VerificationStatus.Pass,
            verification.Status);

        Assert.Equal(
            1.0,
            verification.Similarity);
    }

    [Fact]
    public void Resolve_WhenBrandAppearsOnlyInProducerDeclaration_DoesNotPromoteProducerToBrand()
    {
        // Arrange
        //
        // A producer declaration may contain the same organization name as the
        // expected brand. That alone must not prove a separate brand declaration.
        var verifier =
            new BrandNameVerifier(
                new TextNormalizer(),
                new BrandNameVerificationOptions
                {
                    PassThreshold = 0.95,
                    ReviewThreshold = 0.80
                });

        var resolver =
            new BrandNameCandidateResolver(
                verifier);

        var producerLine =
            "BOTTLED BY OLD TOM DISTILLERY FRANKFORT, KENTUCKY";

        var ocrResult =
            new OcrResult
            {
                Text =
                    producerLine,

                Lines =
                [
                    new OcrTextLine
                    {
                        Text =
                            producerLine
                    }
                ],

                Words = [],

                Styles = [],

                Confidence =
                    0.99,

                Duration =
                    TimeSpan.FromMilliseconds(
                        25),

                Provider =
                    "AzureDocumentIntelligence",

                ModelVersion =
                    "prebuilt-read"
            };

        // Act
        var resolved =
            resolver.Resolve(
                "Old Tom Distillery",
                currentBrandName: null,
                candidates: [],
                ocrResult,
                nameAndAddress: null);

        // Assert
        //
        // Explicit "BOTTLED BY" evidence must not be converted into a brand
        // observation merely because it contains the expected organization name.
        Assert.Null(
            resolved);
    }

    /// <summary>
    /// Reproduces the relevant OCR lines observed during the live Azure
    /// Document Intelligence regression test.
    /// </summary>
    private static OcrResult CreateObservedAzureOcrResult()
    {
        var sourceLines =
            new[]
            {
                "KENTUCKY STRAIGHT BOURBON WHISKEY OLD TOM DISTILLERY",
                "45% ALCOHOL BY VOLUME",
                "90 PROOF",
                "750 mL",
                "BOTTLED BY OLD TOM DISTILLERY FRANKFORT, KENTUCKY"
            };

        var text =
            string.Join(
                Environment.NewLine,
                sourceLines);

        var lines =
            sourceLines
                .Select(
                    line =>
                        new OcrTextLine
                        {
                            Text =
                                line
                        })
                .ToArray();

        var words =
            sourceLines
                .SelectMany(
                    line =>
                        line.Split(
                            ' ',
                            StringSplitOptions.RemoveEmptyEntries))
                .Select(
                    word =>
                        new OcrWord
                        {
                            Text =
                                word,

                            Confidence =
                                0.995
                        })
                .ToArray();

        return new OcrResult
        {
            Text =
                text,

            Lines =
                lines,

            Words =
                words,

            Styles = [],

            Confidence =
                0.995,

            Duration =
                TimeSpan.FromMilliseconds(
                    2763),

            Provider =
                "AzureDocumentIntelligence",

            ModelVersion =
                "prebuilt-read"
        };
    }
}