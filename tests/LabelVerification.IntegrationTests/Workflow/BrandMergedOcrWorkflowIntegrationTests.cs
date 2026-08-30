using LabelVerification.Application.LabelUnderstanding;
using LabelVerification.Application.Verification;
using LabelVerification.Application.Verification.Workflow;
using LabelVerification.IntegrationTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace LabelVerification.IntegrationTests.Workflow;

/// <summary>
/// Exercises the complete Application-layer verification workflow using the
/// OCR segmentation observed from the real nonstandard-layout label.
///
/// Azure itself remains outside this deterministic test. The controlled OCR
/// result reproduces evidence captured during the opt-in live Azure test,
/// while the actual JPEG regression fixture still passes through the real
/// image-validation boundary.
/// </summary>
public sealed class BrandMergedOcrWorkflowIntegrationTests
{
    /// <summary>
    /// Path to the real nonstandard-layout JPEG used to reproduce the original
    /// brand-extraction weakness.
    /// </summary>
    private static string RegressionLabelPath =>
        Path.Combine(
            IntegrationTestSupport.RepositoryRoot,
            "sample-data",
            "labels",
            "verification",
            "brand-nonstandard-layout-label.jpg");

    [Fact]
    public async Task VerifyAsync_WithObservedAzureMergedBrandLine_ResolvesBrandAndPasses()
    {
        // Arrange
        //
        // The OCR result reproduces the actual Azure Document Intelligence
        // segmentation captured from the JPEG fixture:
        //
        // KENTUCKY STRAIGHT BOURBON WHISKEY OLD TOM DISTILLERY
        //
        // The real JPEG is still passed through the production image validator.
        var extractor =
            new ControlledLabelTextExtractor(
                CreateObservedAzureOcrResult());

        using var services =
            IntegrationTestSupport.BuildServiceProvider(
                extractor);

        var sut =
            services.GetRequiredService<
                ILabelVerificationService>();

        var fileInfo =
            new FileInfo(
                RegressionLabelPath);

        Assert.True(
            fileInfo.Exists,
            $"Regression label fixture was not found: {RegressionLabelPath}");

        await using var image =
            File.OpenRead(
                RegressionLabelPath);

        // Act
        var result =
            await sut.VerifyAsync(
                IntegrationTestSupport.ApplicationId,
                image,
                fileInfo.Name,
                "image/jpeg",
                fileInfo.Length);

        // Assert
        Assert.True(
            result.ProcessingSucceeded,
            $"Processing failed with code '{result.ErrorCode}'.");

        Assert.Null(
            result.ErrorCode);

        Assert.NotNull(
            result.ParsedLabel);

        Assert.NotNull(
            result.Verification);

        var parsedLabel =
            result.ParsedLabel!;

        // Azure merged class/type and brand into one OCR line. The parser
        // cannot safely classify the entire line as a brand, so the
        // workflow-level resolver must recover the exact observed brand span.
        Assert.NotNull(
            parsedLabel.BrandName);

        Assert.Equal(
            "OLD TOM DISTILLERY",
            parsedLabel.BrandName!.Value);

        // Preserve the complete source OCR line as explainable evidence.
        Assert.Equal(
            "KENTUCKY STRAIGHT BOURBON WHISKEY OLD TOM DISTILLERY",
            parsedLabel.BrandName.Evidence);

        // All five implemented verification checks should pass. The patch
        // improves evidence selection without weakening deterministic
        // compliance rules.
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

    /// <summary>
    /// Reproduces the relevant Azure Document Intelligence output captured
    /// from brand-nonstandard-layout-label.jpg.
    /// </summary>
    private static OcrResult CreateObservedAzureOcrResult()
    {
        const string warningHeading =
            "GOVERNMENT WARNING:";

        const string warningBody =
            "(1) According to the Surgeon General, women should not drink " +
            "alcoholic beverages during pregnancy because of the risk of birth " +
            "defects. (2) Consumption of alcoholic beverages impairs your " +
            "ability to drive a car or operate machinery, and may cause health problems.";

        var sourceLines =
            new[]
            {
                "KENTUCKY STRAIGHT BOURBON WHISKEY OLD TOM DISTILLERY",
                "45% ALCOHOL BY VOLUME",
                "90 PROOF",
                "750 mL",
                "BOTTLED BY OLD TOM DISTILLERY FRANKFORT, KENTUCKY",
                warningHeading,
                warningBody
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

        var warningOffset =
            text.IndexOf(
                warningHeading,
                StringComparison.Ordinal);

        return new OcrResult
        {
            Text =
                text,

            Lines =
                lines,

            Words =
                words,

            Styles =
            [
                new OcrTextStyle
                {
                    Offset =
                        warningOffset,

                    Length =
                        warningHeading.Length,

                    Text =
                        warningHeading,

                    FontWeight =
                        OcrFontWeight.Bold,

                    Confidence =
                        0.99
                }
            ],

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