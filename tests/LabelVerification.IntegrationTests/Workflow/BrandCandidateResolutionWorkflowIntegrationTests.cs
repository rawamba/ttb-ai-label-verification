using LabelVerification.Application.LabelUnderstanding;
using LabelVerification.Application.Verification;
using LabelVerification.Application.Verification.Brand;
using LabelVerification.Application.Verification.Workflow;
using LabelVerification.IntegrationTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace LabelVerification.IntegrationTests.Workflow;

/// <summary>
/// Proves that expected-guided brand candidate resolution is integrated into
/// the real Application-layer verification workflow.
///
/// The tests intentionally replace only the external OCR boundary. Parsing,
/// candidate resolution, deterministic verification, application-record
/// loading, result aggregation, and dependency injection remain real.
/// </summary>
public sealed class BrandCandidateResolutionWorkflowIntegrationTests
{
    [Fact]
    public async Task VerifyAsync_WhenCorrectBrandAppearsAfterMisleadingCandidate_ResolvesObservedBrandAndPasses()
    {
        // Arrange
        //
        // Reproduce the exact weakness that motivated this hardening patch:
        //
        // OCR order:
        //   1. CRAFTED FOR ADVENTURE
        //   2. OLD TOM DISTILLERY
        //
        // The parser's backward-compatible first-candidate behavior initially
        // selects the marketing-style line. The new workflow resolver should
        // then use the authoritative application brand to select the stronger
        // candidate that was genuinely observed in OCR evidence.
        var ocrResult =
            CreateAdversarialBrandOrderOcrResult();

        var extractor =
            new ControlledLabelTextExtractor(
                ocrResult);

        using var services =
            IntegrationTestSupport.BuildServiceProvider(
                extractor);

        var sut =
            services.GetRequiredService<
                ILabelVerificationService>();

        var fileInfo =
            new FileInfo(
                IntegrationTestSupport.CompliantLabelPath);

        await using var image =
            File.OpenRead(
                IntegrationTestSupport.CompliantLabelPath);

        // Act
        var result =
            await sut.VerifyAsync(
                IntegrationTestSupport.ApplicationId,
                image,
                "compliant-label.png",
                "image/png",
                fileInfo.Length);

        // Assert
        Assert.True(
            result.ProcessingSucceeded);

        Assert.Null(
            result.ErrorCode);

        Assert.NotNull(
            result.ParsedLabel);

        Assert.NotNull(
            result.Verification);

        var parsedLabel =
            result.ParsedLabel!;

        // The parser must preserve both plausible OCR observations rather
        // than discarding all candidates after the first plausible line.
        Assert.Contains(
            parsedLabel.BrandNameCandidates,
            candidate =>
                candidate.Value ==
                "CRAFTED FOR ADVENTURE");

        Assert.Contains(
            parsedLabel.BrandNameCandidates,
            candidate =>
                candidate.Value ==
                "OLD TOM DISTILLERY");

        Assert.True(
            parsedLabel.BrandNameCandidates.Count >=
            2);

        Assert.NotNull(
            parsedLabel.BrandName);

        // The workflow must resolve to the correct candidate that actually
        // exists in OCR evidence.
        //
        // The expected application value is used only as a comparison signal;
        // it is never injected as fabricated observed evidence.
        Assert.Equal(
            "OLD TOM DISTILLERY",
            parsedLabel.BrandName!.Value);

        Assert.Equal(
            "OLD TOM DISTILLERY",
            parsedLabel.BrandName.Evidence);

        // Because all remaining controlled OCR evidence is compliant, fixing
        // the brand candidate selection should allow the complete workflow to
        // retain its expected PASS result.
        Assert.Equal(
            VerificationStatus.Pass,
            result.Verification!.OverallStatus);

        Assert.Equal(
            5,
            result.Verification.PassCount);

        Assert.Equal(
            0,
            result.Verification.ReviewCount);

        Assert.Equal(
            0,
            result.Verification.FailCount);

        Assert.Equal(
            1,
            extractor.CallCount);
    }

    [Fact]
    public void Composition_RegistersExpectedGuidedBrandCandidateResolver()
    {
        // Arrange
        //
        // Build the same Application-layer graph used by deterministic
        // integration tests. This proves AddApplication() exposes the resolver
        // required by the production verification workflow.
        var extractor =
            new ControlledLabelTextExtractor(
                IntegrationTestSupport.CreateCompliantOcrResult());

        using var services =
            IntegrationTestSupport.BuildServiceProvider(
                extractor);

        // Act
        var resolver =
            services.GetService<
                IBrandNameCandidateResolver>();

        // Assert
        Assert.NotNull(
            resolver);

        Assert.IsType<
            BrandNameCandidateResolver>(
            resolver);
    }

    /// <summary>
    /// Creates otherwise compliant deterministic OCR evidence but inserts a
    /// misleading plausible line before the true brand.
    ///
    /// The Government Warning style offset is shifted so the real warning
    /// verifier continues to receive valid typography evidence.
    /// </summary>
    private static OcrResult CreateAdversarialBrandOrderOcrResult()
    {
        const string misleadingCandidate =
            "CRAFTED FOR ADVENTURE";

        var source =
            IntegrationTestSupport
                .CreateCompliantOcrResult();

        var prefix =
            misleadingCandidate +
            Environment.NewLine;

        var text =
            prefix +
            source.Text;

        var lines =
            new[]
            {
                new OcrTextLine
                {
                    Text =
                        misleadingCandidate
                }
            }
            .Concat(
                source.Lines.Select(
                    line =>
                        new OcrTextLine
                        {
                            Text =
                                line.Text
                        }))
            .ToArray();

        var misleadingWords =
            misleadingCandidate
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(
                    word =>
                        new OcrWord
                        {
                            Text =
                                word,

                            Confidence =
                                0.99
                        });

        var sourceWords =
            source.Words.Select(
                word =>
                    new OcrWord
                    {
                        Text =
                            word.Text,

                        Confidence =
                            word.Confidence
                    });

        var words =
            misleadingWords
                .Concat(
                    sourceWords)
                .ToArray();

        // Prepending one text line moves every existing style span forward by
        // exactly the prefix length. Preserving those offsets ensures the real
        // Government Warning verifier continues to validate bold-heading
        // evidence correctly.
        var styles =
            source.Styles
                .Select(
                    style =>
                        new OcrTextStyle
                        {
                            Offset =
                                style.Offset +
                                prefix.Length,

                            Length =
                                style.Length,

                            Text =
                                style.Text,

                            FontWeight =
                                style.FontWeight,

                            Confidence =
                                style.Confidence
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

            Styles =
                styles,

            Confidence =
                source.Confidence,

            Duration =
                source.Duration,

            Provider =
                source.Provider,

            ModelVersion =
                "integration-brand-candidate-resolution-v1"
        };
    }
}