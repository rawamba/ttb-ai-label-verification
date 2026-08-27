using LabelVerification.Application.Verification.Workflow;
using LabelVerification.IntegrationTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace LabelVerification.IntegrationTests.Workflow;

/// <summary>
/// Verifies that technical failures are kept separate from regulatory
/// PASS / REVIEW / FAIL outcomes.
/// </summary>
public sealed class LabelVerificationWorkflowErrorTests
{
    [Fact]
    public async Task VerifyAsync_WhenApplicationDoesNotExist_ReturnsProcessingFailureWithoutCallingOcr()
    {
        // Arrange
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
                "COLA-DOES-NOT-EXIST",
                image,
                "compliant-label.png",
                "image/png",
                image.Length);

        // Assert
        Assert.False(
            result.ProcessingSucceeded);

        Assert.Equal(
            "application_not_found",
            result.ErrorCode);

        Assert.Null(
            result.Verification);

        Assert.Equal(
            0,
            extractor.CallCount);

        Assert.NotNull(
            result.Telemetry);

        Assert.False(
            string.IsNullOrWhiteSpace(
                result.Telemetry.CorrelationId));

        Assert.True(
            result.Telemetry.TotalDuration >= TimeSpan.Zero);

        // Processing ended before OCR and deterministic verification began.
        Assert.Null(
            result.Telemetry.OcrDuration);

        Assert.Null(
            result.Telemetry.VerificationDuration);

        Assert.NotNull(
            result.Telemetry);

        Assert.False(
            string.IsNullOrWhiteSpace(
                result.Telemetry.CorrelationId));

        Assert.True(
            result.Telemetry.TotalDuration >= TimeSpan.Zero);

        // Processing ended before OCR and deterministic verification began.
        Assert.Null(
            result.Telemetry.OcrDuration);

        Assert.Null(
            result.Telemetry.VerificationDuration);
    }

    [Fact]
    public async Task VerifyAsync_WhenImageContentIsInvalid_RejectsBeforeCallingOcr()
    {
        // Arrange
        var extractor =
            new ControlledLabelTextExtractor(
                IntegrationTestSupport.CreateCompliantOcrResult());

        using var services =
            IntegrationTestSupport.BuildServiceProvider(
                extractor);

        var sut =
            services.GetRequiredService<
                ILabelVerificationService>();

        var invalidBytes =
            new byte[]
            {
                0x54,
                0x48,
                0x49,
                0x53,
                0x20,
                0x49,
                0x53,
                0x20,
                0x4E,
                0x4F,
                0x54,
                0x20,
                0x41,
                0x4E,
                0x20,
                0x49,
                0x4D,
                0x41,
                0x47,
                0x45
            };

        await using var image =
            new MemoryStream(
                invalidBytes);

        // Act
        var result =
            await sut.VerifyAsync(
                IntegrationTestSupport.ApplicationId,
                image,
                "invalid.png",
                "image/png",
                image.Length);

        // Assert
        Assert.False(
            result.ProcessingSucceeded);

        Assert.False(
            string.IsNullOrWhiteSpace(
                result.ErrorCode));

        Assert.Null(
            result.Verification);

        Assert.Equal(
            0,
            extractor.CallCount);
    }

    [Fact]
    public async Task VerifyAsync_WhenOcrProviderThrows_DoesNotCreateRegulatoryResult()
    {
        // Arrange
        var extractor =
            new ControlledLabelTextExtractor(
                (_, _) =>
                    Task.FromException<
                        LabelVerification.Application.LabelUnderstanding.OcrResult>(
                            new InvalidOperationException(
                                "Simulated OCR provider failure.")));

        using var services =
            IntegrationTestSupport.BuildServiceProvider(
                extractor);

        var sut =
            services.GetRequiredService<
                ILabelVerificationService>();

        await using var image =
            File.OpenRead(
                IntegrationTestSupport.CompliantLabelPath);

        // Act / Assert
        var exception =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                    () =>
                        sut.VerifyAsync(
                            IntegrationTestSupport.ApplicationId,
                            image,
                            "compliant-label.png",
                            "image/png",
                            image.Length));

        Assert.Equal(
            "Simulated OCR provider failure.",
            exception.Message);

        Assert.Equal(
            1,
            extractor.CallCount);
    }
}