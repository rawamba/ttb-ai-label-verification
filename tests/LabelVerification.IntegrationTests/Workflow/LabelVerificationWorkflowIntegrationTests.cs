using LabelVerification.Application.Verification;
using LabelVerification.Application.Verification.Workflow;
using LabelVerification.IntegrationTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace LabelVerification.IntegrationTests.Workflow;

/// <summary>
/// Verifies the complete deterministic application workflow using the real
/// application adapter, image validator, parser, field verifiers, and result
/// aggregator.
///
/// Only the OCR boundary is controlled so ordinary CI remains independent of
/// external Azure availability.
/// </summary>
public sealed class LabelVerificationWorkflowIntegrationTests
{
    [Fact]
    public async Task VerifyAsync_WithCompliantApplicationAndLabel_ReturnsPass()
    {
        // Arrange
        Assert.True(
            File.Exists(
                IntegrationTestSupport.CompliantLabelPath),
            $"Compliant fixture was not found: " +
            $"{IntegrationTestSupport.CompliantLabelPath}");

        var extractor =
            new ControlledLabelTextExtractor(
                IntegrationTestSupport.CreateCompliantOcrResult());

        using var services =
            IntegrationTestSupport.BuildServiceProvider(
                extractor);

        var sut =
            services.GetRequiredService<
                ILabelVerificationService>();

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
                image.Length);

        // Assert technical workflow completion separately from compliance
        // status.
        Assert.True(
            result.ProcessingSucceeded);

        Assert.Null(
            result.ErrorCode);

        Assert.NotNull(
            result.ApplicationRecord);

        Assert.NotNull(
            result.OcrResult);

        Assert.NotNull(
            result.ParsedLabel);

        Assert.NotNull(
            result.Verification);

        Assert.Equal(
            IntegrationTestSupport.ApplicationId,
            result.ApplicationRecord.ApplicationId);

        Assert.Equal(
            "OLD TOM DISTILLERY",
            result.ParsedLabel.BrandName?.Value);

        Assert.Equal(
            "KENTUCKY STRAIGHT BOURBON WHISKEY",
            result.ParsedLabel.ClassType?.Value);

        Assert.Equal(
            VerificationStatus.Pass,
            result.Verification.OverallStatus);

        // The current workflow verifies five fields:
        // brand, ABV, proof, net contents, and Government Warning.
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
            5,
            result.Verification.Checks.Count);

        Assert.Equal(
            1,
            extractor.CallCount);
    }
}