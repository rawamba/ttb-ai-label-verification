using System.Collections.Concurrent;
using LabelVerification.Application.LabelUnderstanding;
using LabelVerification.Application.Verification;
using LabelVerification.Application.Verification.Batch;
using LabelVerification.Application.Verification.Workflow;
using LabelVerification.IntegrationTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace LabelVerification.IntegrationTests.Workflow;

/// <summary>
/// Exercises the batch coordinator together with the real Application-layer
/// single-label verification workflow.
///
/// Azure OCR remains replaced by the deterministic controlled extractor.
/// These tests therefore validate composition, regulatory-result handling,
/// technical fault isolation, ordering, telemetry, and progress without
/// depending on an external AI service.
/// </summary>
public sealed class BatchVerificationWorkflowIntegrationTests
{
    [Fact]
    public async Task VerifyAsync_WithMixedRegulatoryOutcomes_ReturnsPassReviewAndFailSeparately()
    {
        // Arrange
        var extractor =
            CreateSequenceExtractor(
                IntegrationTestSupport.CreateCompliantOcrResult(),
                CreateMissingWarningOcrResult(),
                CreateNetContentsMismatchOcrResult());

        using var services =
            IntegrationTestSupport.BuildServiceProvider(
                extractor);

        ConfigureBatchConcurrency(
            services,
            1);

        var sut =
            services.GetRequiredService<
                IBatchLabelVerificationService>();

        var requests =
            new[]
            {
                CreateCompliantFileRequest(
                    "item-pass",
                    "pass-label.png"),

                CreateCompliantFileRequest(
                    "item-review",
                    "review-label.png"),

                CreateCompliantFileRequest(
                    "item-fail",
                    "fail-label.png")
            };

        // Act
        var result =
            await sut.VerifyAsync(
                IntegrationTestSupport.ApplicationId,
                requests);

        // Assert
        Assert.Equal(
            3,
            result.Summary.TotalCount);

        Assert.Equal(
            3,
            result.Summary.CompletedCount);

        Assert.Equal(
            1,
            result.Summary.PassCount);

        Assert.Equal(
            1,
            result.Summary.ReviewCount);

        Assert.Equal(
            1,
            result.Summary.FailCount);

        Assert.Equal(
            0,
            result.Summary.ErrorCount);

        Assert.False(
            string.IsNullOrWhiteSpace(
                result.BatchCorrelationId));

        Assert.Equal(
            [
                "item-pass",
                "item-review",
                "item-fail"
            ],
            result.Items
                .Select(
                    item =>
                        item.ItemId)
                .ToArray());

        Assert.Equal(
            VerificationStatus.Pass,
            result.Items[0]
                .VerificationResult!
                .Verification!
                .OverallStatus);

        Assert.Equal(
            VerificationStatus.Review,
            result.Items[1]
                .VerificationResult!
                .Verification!
                .OverallStatus);

        Assert.Equal(
            VerificationStatus.Fail,
            result.Items[2]
                .VerificationResult!
                .Verification!
                .OverallStatus);

        Assert.All(
            result.Items,
            item =>
                Assert.Equal(
                    BatchItemProcessingStatus.Completed,
                    item.ProcessingStatus));

        Assert.Equal(
            3,
            extractor.CallCount);

        // Each existing single-label workflow retains its own correlation
        // identifier while the batch has a separate correlation identifier.
        var itemCorrelationIds =
            result.Items
                .Select(
                    item =>
                        item.VerificationResult?
                            .Telemetry?
                            .CorrelationId)
                .ToArray();

        Assert.All(
            itemCorrelationIds,
            correlationId =>
                Assert.False(
                    string.IsNullOrWhiteSpace(
                        correlationId)));

        Assert.Equal(
            3,
            itemCorrelationIds
                .Distinct(
                    StringComparer.Ordinal)
                .Count());

        Assert.DoesNotContain(
            result.BatchCorrelationId,
            itemCorrelationIds);
    }

    [Fact]
    public async Task VerifyAsync_WhenOneImageCannotBeOpened_IsolatesItemAndContinuesRemainingLabels()
    {
        // Arrange
        var extractor =
            new ControlledLabelTextExtractor(
                IntegrationTestSupport.CreateCompliantOcrResult());

        using var services =
            IntegrationTestSupport.BuildServiceProvider(
                extractor);

        ConfigureBatchConcurrency(
            services,
            1);

        var sut =
            services.GetRequiredService<
                IBatchLabelVerificationService>();

        var requests =
            new[]
            {
                CreateCompliantFileRequest(
                    "item-1",
                    "first-good-label.png"),

                new BatchVerificationItemRequest(
                    "item-2",
                    "unreadable-label.png",
                    "image/png",
                    100,
                    () =>
                        throw new IOException(
                            "Simulated batch image read failure.")),

                CreateCompliantFileRequest(
                    "item-3",
                    "second-good-label.png")
            };

        // Act
        var result =
            await sut.VerifyAsync(
                IntegrationTestSupport.ApplicationId,
                requests);

        // Assert
        Assert.Equal(
            3,
            result.Items.Count);

        Assert.Equal(
            [
                "item-1",
                "item-2",
                "item-3"
            ],
            result.Items
                .Select(
                    item =>
                        item.ItemId)
                .ToArray());

        Assert.Equal(
            BatchItemProcessingStatus.Completed,
            result.Items[0].ProcessingStatus);

        Assert.Equal(
            BatchItemProcessingStatus.Error,
            result.Items[1].ProcessingStatus);

        Assert.Equal(
            BatchItemProcessingStatus.Completed,
            result.Items[2].ProcessingStatus);

        Assert.Equal(
            "IMAGE_READ_FAILURE",
            result.Items[1].ErrorCode);

        Assert.Null(
            result.Items[1].VerificationResult);

        Assert.Equal(
            2,
            result.Summary.PassCount);

        Assert.Equal(
            0,
            result.Summary.ReviewCount);

        Assert.Equal(
            0,
            result.Summary.FailCount);

        Assert.Equal(
            1,
            result.Summary.ErrorCount);

        // OCR is invoked only for the two valid items.
        Assert.Equal(
            2,
            extractor.CallCount);
    }

    [Fact]
    public async Task VerifyAsync_WhenApplicationDoesNotExist_ReportsTechnicalErrorsNotRegulatoryFailures()
    {
        // Arrange
        var extractor =
            new ControlledLabelTextExtractor(
                IntegrationTestSupport.CreateCompliantOcrResult());

        using var services =
            IntegrationTestSupport.BuildServiceProvider(
                extractor);

        ConfigureBatchConcurrency(
            services,
            1);

        var sut =
            services.GetRequiredService<
                IBatchLabelVerificationService>();

        var requests =
            new[]
            {
                CreateCompliantFileRequest(
                    "item-1",
                    "first-label.png"),

                CreateCompliantFileRequest(
                    "item-2",
                    "second-label.png")
            };

        // Act
        var result =
            await sut.VerifyAsync(
                "COLA-DOES-NOT-EXIST",
                requests);

        // Assert
        Assert.Equal(
            2,
            result.Summary.TotalCount);

        Assert.Equal(
            2,
            result.Summary.ErrorCount);

        Assert.Equal(
            0,
            result.Summary.PassCount);

        Assert.Equal(
            0,
            result.Summary.ReviewCount);

        Assert.Equal(
            0,
            result.Summary.FailCount);

        Assert.All(
            result.Items,
            item =>
            {
                Assert.Equal(
                    BatchItemProcessingStatus.Error,
                    item.ProcessingStatus);

                Assert.Equal(
                    "application_not_found",
                    item.ErrorCode);

                Assert.NotNull(
                    item.VerificationResult);

                Assert.False(
                    item.VerificationResult!
                        .ProcessingSucceeded);

                Assert.Null(
                    item.VerificationResult
                        .Verification);
            });

        // Application validation fails before OCR.
        Assert.Equal(
            0,
            extractor.CallCount);
    }

    [Fact]
    public async Task VerifyAsync_ReportsBatchCorrelationAndPerItemProgress()
    {
        // Arrange
        var extractor =
            new ControlledLabelTextExtractor(
                IntegrationTestSupport.CreateCompliantOcrResult());

        using var services =
            IntegrationTestSupport.BuildServiceProvider(
                extractor);

        ConfigureBatchConcurrency(
            services,
            1);

        var sut =
            services.GetRequiredService<
                IBatchLabelVerificationService>();

        var progress =
            new RecordingProgress<
                BatchVerificationProgress>();

        var requests =
            new[]
            {
                CreateCompliantFileRequest(
                    "item-1",
                    "first-label.png"),

                CreateCompliantFileRequest(
                    "item-2",
                    "second-label.png")
            };

        // Act
        var result =
            await sut.VerifyAsync(
                IntegrationTestSupport.ApplicationId,
                requests,
                progress);

        // Assert
        var snapshots =
            progress.Values;

        Assert.NotEmpty(
            snapshots);

        Assert.All(
            snapshots,
            snapshot =>
                Assert.Equal(
                    result.BatchCorrelationId,
                    snapshot.BatchCorrelationId));

        var initial =
            snapshots.First();

        Assert.Equal(
            2,
            initial.TotalItems);

        Assert.Equal(
            0,
            initial.CompletedItems);

        Assert.Contains(
            snapshots,
            snapshot =>
                snapshot.ItemId ==
                "item-1" &&
                snapshot.ItemProcessingStatus ==
                BatchItemProcessingStatus.Processing);

        Assert.Contains(
            snapshots,
            snapshot =>
                snapshot.ItemId ==
                "item-2" &&
                snapshot.ItemProcessingStatus ==
                BatchItemProcessingStatus.Processing);

        Assert.Contains(
            snapshots,
            snapshot =>
                snapshot.ItemId ==
                "item-1" &&
                snapshot.CompletedItemResult is not null &&
                snapshot.ItemProcessingStatus ==
                BatchItemProcessingStatus.Completed);

        Assert.Contains(
            snapshots,
            snapshot =>
                snapshot.ItemId ==
                "item-2" &&
                snapshot.CompletedItemResult is not null &&
                snapshot.ItemProcessingStatus ==
                BatchItemProcessingStatus.Completed);

        var final =
            snapshots
                .Last(
                    snapshot =>
                        snapshot.CompletedItems ==
                        2);

        Assert.Equal(
            2,
            final.TotalItems);

        Assert.Equal(
            2,
            final.CompletedItems);

        Assert.Equal(
            2,
            final.PassCount);

        Assert.Equal(
            0,
            final.ReviewCount);

        Assert.Equal(
            0,
            final.FailCount);

        Assert.Equal(
            0,
            final.ErrorCount);

        Assert.Equal(
            2,
            result.Summary.PassCount);
    }

    private static void ConfigureBatchConcurrency(
        ServiceProvider services,
        int maxConcurrency)
    {
        var options =
            services.GetRequiredService<
                BatchVerificationOptions>();

        options.MaxConcurrency =
            maxConcurrency;
    }

    private static BatchVerificationItemRequest CreateCompliantFileRequest(
        string itemId,
        string displayName)
    {
        var path =
            IntegrationTestSupport.CompliantLabelPath;

        var fileInfo =
            new FileInfo(
                path);

        return new BatchVerificationItemRequest(
            itemId,
            displayName,
            "image/png",
            fileInfo.Length,
            () =>
                File.OpenRead(
                    path));
    }

    /// <summary>
    /// Creates a deterministic extractor that returns the supplied OCR
    /// results in order.
    ///
    /// Tests using this helper set batch concurrency to one so the sequence
    /// represents the corresponding request order deterministically.
    /// </summary>
    private static ControlledLabelTextExtractor CreateSequenceExtractor(
        params OcrResult[] results)
    {
        var queue =
            new ConcurrentQueue<OcrResult>(
                results);

        return new ControlledLabelTextExtractor(
            (_, _) =>
            {
                if (!queue.TryDequeue(
                        out var result))
                {
                    throw new InvalidOperationException(
                        "No deterministic OCR result remains for this test.");
                }

                return Task.FromResult(
                    result);
            });
    }

    /// <summary>
    /// Represents a label where the required Government Warning evidence is
    /// absent. The production warning verifier should route this ambiguity
    /// to human REVIEW rather than manufacturing a compliant result.
    /// </summary>
    private static OcrResult CreateMissingWarningOcrResult()
    {
        var sourceLines =
            new[]
            {
                "OLD TOM DISTILLERY",
                "KENTUCKY STRAIGHT BOURBON WHISKEY",
                "45% ALCOHOL BY VOLUME",
                "90 PROOF",
                "750 mL",
                "BOTTLED BY OLD TOM DISTILLERY",
                "FRANKFORT, KENTUCKY"
            };

        var text =
            string.Join(
                Environment.NewLine,
                sourceLines);

        var lines =
            sourceLines
                .Select(
                    value =>
                        new OcrTextLine
                        {
                            Text =
                                value
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
                                0.99
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
                [],

            Confidence =
                0.99,

            Duration =
                TimeSpan.FromMilliseconds(
                    25),

            Provider =
                "DeterministicIntegrationOcr",

            ModelVersion =
                "integration-missing-warning-v1"
        };
    }

    /// <summary>
    /// Creates otherwise compliant OCR evidence with a deterministic net
    /// contents mismatch.
    ///
    /// 750 mL is replaced by 700 mL using the same text length so the
    /// Government Warning style-span offsets remain valid.
    /// </summary>
    private static OcrResult CreateNetContentsMismatchOcrResult()
    {
        var source =
            IntegrationTestSupport
                .CreateCompliantOcrResult();

        var text =
            source.Text.Replace(
                "750 mL",
                "700 mL",
                StringComparison.Ordinal);

        var lines =
            source.Lines
                .Select(
                    line =>
                        new OcrTextLine
                        {
                            Text =
                                line.Text.Replace(
                                    "750 mL",
                                    "700 mL",
                                    StringComparison.Ordinal)
                        })
                .ToArray();

        var words =
            source.Words
                .Select(
                    word =>
                        new OcrWord
                        {
                            Text =
                                string.Equals(
                                    word.Text,
                                    "750",
                                    StringComparison.Ordinal)
                                        ? "700"
                                        : word.Text,

                            Confidence =
                                word.Confidence
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
                source.Styles,

            Confidence =
                source.Confidence,

            Duration =
                source.Duration,

            Provider =
                source.Provider,

            ModelVersion =
                "integration-net-contents-mismatch-v1"
        };
    }

    private sealed class RecordingProgress<T>
        : IProgress<T>
    {
        private readonly ConcurrentQueue<T> _values =
            new();

        internal IReadOnlyList<T> Values =>
            _values.ToArray();

        public void Report(
            T value)
        {
            _values.Enqueue(
                value);
        }
    }
}