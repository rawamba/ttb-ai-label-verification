using LabelVerification.Application.LabelUnderstanding;
using LabelVerification.Application.Verification;
using LabelVerification.Application.Verification.Brand;
using LabelVerification.Application.Verification.Normalization;

namespace LabelVerification.UnitTests.Verification.Brand;

/// <summary>
/// Regression coverage for the boundary between standalone brand evidence
/// and producer/address evidence containing the same organization name.
/// </summary>
public sealed class BrandProducerEvidenceBoundaryTests
{
    [Fact]
    public void Resolve_WhenStandaloneBrandAlsoAppearsInsideProducerEvidence_PreservesBrandEvidence()
    {
        // Arrange
        //
        // This reproduces the real failure discovered with the
        // nonstandard-layout regression image:
        //
        // Standalone brand:
        //   OLD TOM DISTILLERY
        //
        // Producer declaration:
        //   BOTTLED BY OLD TOM DISTILLERY
        //
        // The standalone brand must not be rejected merely because its text
        // is contained inside the longer producer declaration.
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

        const string brandLine =
            "OLD TOM DISTILLERY";

        const string producerLine =
            "BOTTLED BY OLD TOM DISTILLERY";

        const string locationLine =
            "FRANKFORT, KENTUCKY";

        var ocrResult =
            new OcrResult
            {
                Text =
                    string.Join(
                        Environment.NewLine,
                        brandLine,
                        producerLine,
                        locationLine),

                Lines =
                [
                    new OcrTextLine
                    {
                        Text =
                            brandLine
                    },

                    new OcrTextLine
                    {
                        Text =
                            producerLine
                    },

                    new OcrTextLine
                    {
                        Text =
                            locationLine
                    }
                ],

                Words = [],

                Styles = [],

                Confidence =
                    0.996,

                Duration =
                    TimeSpan.FromMilliseconds(
                        2500),

                Provider =
                    "AzureDocumentIntelligence",

                ModelVersion =
                    "prebuilt-read"
            };

        var producerEvidence =
            new ParsedLabelField<ParsedNameAndAddress>
            {
                Value =
                    new ParsedNameAndAddress
                    {
                        RawText =
                            string.Join(
                                Environment.NewLine,
                                producerLine,
                                locationLine),

                        BusinessName =
                            "OLD TOM DISTILLERY",

                        City =
                            "FRANKFORT",

                        State =
                            "KENTUCKY"
                    },

                Evidence =
                    string.Join(
                        Environment.NewLine,
                        producerLine,
                        locationLine),

                Confidence =
                    0.996
            };

        // Act
        var resolved =
            resolver.Resolve(
                "Old Tom Distillery",
                currentBrandName: null,
                candidates: [],
                ocrResult,
                producerEvidence);

        // Assert
        Assert.NotNull(
            resolved);

        Assert.Equal(
            brandLine,
            resolved.Value);

        Assert.Equal(
            brandLine,
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
}