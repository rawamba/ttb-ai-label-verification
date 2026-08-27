using LabelVerification.Application.Verification.Workflow;
using LabelVerification.IntegrationTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace LabelVerification.IntegrationTests.Workflow;

/// <summary>
/// Verifies operational telemetry without exposing document content or
/// extracted OCR evidence.
/// </summary>
public sealed class VerificationTelemetryIntegrationTests
{
    [Fact]
    public async Task VerifyAsync_WithCompliantLabel_LogsMetadataWithoutSensitiveDocumentContent()
    {
        // Arrange
        var ocr =
            IntegrationTestSupport.CreateCompliantOcrResult();

        var extractor =
            new ControlledLabelTextExtractor(
                ocr);

        var logger =
            new CapturingLogger<
                LabelVerificationService>();

        using var services =
            IntegrationTestSupport.BuildServiceProvider(
                extractor,
                logger);

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
                "sensitive-upload-name.png",
                "image/png",
                image.Length);

        // Assert
        Assert.True(
            result.ProcessingSucceeded);

        Assert.NotNull(
            result.Telemetry);

        var combinedLog =
            string.Join(
                Environment.NewLine,
                logger.Messages);

        Assert.Contains(
            "ResultCategory PASS",
            combinedLog,
            StringComparison.Ordinal);

        Assert.Contains(
            "OcrDurationMs",
            combinedLog,
            StringComparison.Ordinal);

        Assert.Contains(
            "VerificationDurationMs",
            combinedLog,
            StringComparison.Ordinal);

        Assert.Contains(
            "TotalDurationMs",
            combinedLog,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "sensitive-upload-name.png",
            combinedLog,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "OLD TOM DISTILLERY",
            combinedLog,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "GOVERNMENT WARNING",
            combinedLog,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "FRANKFORT",
            combinedLog,
            StringComparison.OrdinalIgnoreCase);
    }
}