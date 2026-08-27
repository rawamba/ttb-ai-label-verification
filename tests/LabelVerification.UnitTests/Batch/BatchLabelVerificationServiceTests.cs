using System.Collections.Concurrent;
using LabelVerification.Application.LabelUnderstanding;
using LabelVerification.Application.Verification;
using LabelVerification.Application.Verification.Batch;
using LabelVerification.Application.Verification.Workflow;
using Microsoft.Extensions.Logging.Abstractions;

namespace LabelVerification.UnitTests.Verification.Batch;

public sealed class BatchLabelVerificationServiceTests
{
    [Fact]
    public async Task VerifyAsync_WithMixedRegulatoryResults_CreatesExpectedSummary()
    {
        var singleLabelService =
            new FakeLabelVerificationService(
                new Dictionary<string, VerificationStatus>
                {
                    ["pass.png"] =
                        VerificationStatus.Pass,

                    ["review.png"] =
                        VerificationStatus.Review,

                    ["fail.png"] =
                        VerificationStatus.Fail
                });

        var service =
            CreateService(
                singleLabelService);

        var result =
            await service.VerifyAsync(
                "COLA-84729",
                [
                    CreateRequest(
                        "item-1",
                        "pass.png"),

                    CreateRequest(
                        "item-2",
                        "review.png"),

                    CreateRequest(
                        "item-3",
                        "fail.png")
                ]);

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
    }

    [Fact]
    public async Task VerifyAsync_WhenItemsFinishOutOfOrder_PreservesInputOrder()
    {
        var singleLabelService =
            new FakeLabelVerificationService(
                statuses:
                    new Dictionary<string, VerificationStatus>
                    {
                        ["first.png"] =
                            VerificationStatus.Pass,

                        ["second.png"] =
                            VerificationStatus.Pass,

                        ["third.png"] =
                            VerificationStatus.Pass
                    },
                delays:
                    new Dictionary<string, TimeSpan>
                    {
                        ["first.png"] =
                            TimeSpan.FromMilliseconds(80),

                        ["second.png"] =
                            TimeSpan.FromMilliseconds(40),

                        ["third.png"] =
                            TimeSpan.FromMilliseconds(5)
                    });

        var service =
            CreateService(
                singleLabelService,
                maxConcurrency: 3);

        var result =
            await service.VerifyAsync(
                "COLA-84729",
                [
                    CreateRequest(
                        "item-1",
                        "first.png"),

                    CreateRequest(
                        "item-2",
                        "second.png"),

                    CreateRequest(
                        "item-3",
                        "third.png")
                ]);

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
    }

    [Fact]
    public async Task VerifyAsync_WhenOneItemThrows_IsolatesFailureAndContinuesBatch()
    {
        var singleLabelService =
            new FakeLabelVerificationService(
                statuses:
                    new Dictionary<string, VerificationStatus>
                    {
                        ["good-1.png"] =
                            VerificationStatus.Pass,

                        ["good-2.png"] =
                            VerificationStatus.Pass
                    },
                failures:
                    new Dictionary<string, Exception>
                    {
                        ["broken.png"] =
                            new IOException(
                                "Simulated image read failure.")
                    });

        var service =
            CreateService(
                singleLabelService);

        var result =
            await service.VerifyAsync(
                "COLA-84729",
                [
                    CreateRequest(
                        "item-1",
                        "good-1.png"),

                    CreateRequest(
                        "item-2",
                        "broken.png"),

                    CreateRequest(
                        "item-3",
                        "good-2.png")
                ]);

        Assert.Equal(
            3,
            result.Items.Count);

        Assert.Equal(
            2,
            result.Summary.PassCount);

        Assert.Equal(
            1,
            result.Summary.ErrorCount);

        var failedItem =
            Assert.Single(
                result.Items.Where(
                    item =>
                        item.ItemId ==
                        "item-2"));

        Assert.Equal(
            BatchItemProcessingStatus.Error,
            failedItem.ProcessingStatus);

        Assert.Equal(
            "IMAGE_READ_FAILURE",
            failedItem.ErrorCode);

        Assert.Equal(
            BatchItemProcessingStatus.Completed,
            result.Items[0].ProcessingStatus);

        Assert.Equal(
            BatchItemProcessingStatus.Completed,
            result.Items[2].ProcessingStatus);
    }

    [Fact]
    public async Task VerifyAsync_WhenSingleLabelWorkflowReturnsTechnicalFailure_ReportsError()
    {
        var singleLabelService =
            new FakeLabelVerificationService(
                statuses:
                    new Dictionary<string, VerificationStatus>(),
                technicalFailures:
                    new Dictionary<
                        string,
                        LabelVerificationSubmissionResult>
                    {
                        ["technical-error.png"] =
                            new LabelVerificationSubmissionResult
                            {
                                ProcessingSucceeded =
                                    false,

                                ErrorCode =
                                    "TEST_TECHNICAL_FAILURE",

                                ErrorMessage =
                                    "Simulated technical failure."
                            }
                    });

        var service =
            CreateService(
                singleLabelService);

        var result =
            await service.VerifyAsync(
                "COLA-84729",
                [
                    CreateRequest(
                        "item-1",
                        "technical-error.png")
                ]);

        var item =
            Assert.Single(
                result.Items);

        Assert.Equal(
            BatchItemProcessingStatus.Error,
            item.ProcessingStatus);

        Assert.Equal(
            "TEST_TECHNICAL_FAILURE",
            item.ErrorCode);

        Assert.Equal(
            1,
            result.Summary.ErrorCount);

        Assert.Equal(
            0,
            result.Summary.FailCount);
    }

    [Fact]
    public async Task VerifyAsync_UsesConfiguredBoundedConcurrency()
    {
        const int maxConcurrency =
            3;

        var statuses =
            Enumerable.Range(
                    1,
                    12)
                .ToDictionary(
                    number =>
                        $"label-{number}.png",
                    _ =>
                        VerificationStatus.Pass);

        var delays =
            statuses.Keys
                .ToDictionary(
                    name =>
                        name,
                    _ =>
                        TimeSpan.FromMilliseconds(30));

        var singleLabelService =
            new FakeLabelVerificationService(
                statuses,
                delays);

        var service =
            CreateService(
                singleLabelService,
                maxConcurrency);

        var requests =
            statuses.Keys
                .Select(
                    (name, index) =>
                        CreateRequest(
                            $"item-{index + 1}",
                            name))
                .ToArray();

        var result =
            await service.VerifyAsync(
                "COLA-84729",
                requests);

        Assert.Equal(
            12,
            result.Summary.PassCount);

        Assert.True(
            singleLabelService.MaximumObservedConcurrency <=
            maxConcurrency);

        Assert.True(
            singleLabelService.MaximumObservedConcurrency > 1);
    }

    [Fact]
    public async Task VerifyAsync_ReportsBatchAndPerItemProgress()
    {
        var singleLabelService =
            new FakeLabelVerificationService(
                new Dictionary<string, VerificationStatus>
                {
                    ["first.png"] =
                        VerificationStatus.Pass,

                    ["second.png"] =
                        VerificationStatus.Fail
                });

        var service =
            CreateService(
                singleLabelService);

        var progress =
            new RecordingProgress<
                BatchVerificationProgress>();

        var result =
            await service.VerifyAsync(
                "COLA-84729",
                [
                    CreateRequest(
                        "item-1",
                        "first.png"),

                    CreateRequest(
                        "item-2",
                        "second.png")
                ],
                progress);

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
                snapshot.CompletedItemResult is not null);

        Assert.Contains(
            snapshots,
            snapshot =>
                snapshot.ItemId ==
                "item-2" &&
                snapshot.CompletedItemResult is not null);

        var finalSnapshot =
            snapshots
                .Where(
                    snapshot =>
                        snapshot.CompletedItems == 2)
                .Last();

        Assert.Equal(
            2,
            finalSnapshot.TotalItems);

        Assert.Equal(
            2,
            finalSnapshot.CompletedItems);

        Assert.Equal(
            1,
            finalSnapshot.PassCount);

        Assert.Equal(
            1,
            finalSnapshot.FailCount);

        Assert.Equal(
            0,
            finalSnapshot.ErrorCount);
    }

    [Fact]
    public async Task VerifyAsync_WhenBatchExceedsConfiguredMaximum_Throws()
    {
        var singleLabelService =
            new FakeLabelVerificationService(
                new Dictionary<string, VerificationStatus>());

        var service =
            CreateService(
                singleLabelService,
                maxBatchSize: 2);

        var exception =
            await Assert.ThrowsAsync<ArgumentException>(
                () =>
                    service.VerifyAsync(
                        "COLA-84729",
                        [
                            CreateRequest(
                                "item-1",
                                "one.png"),

                            CreateRequest(
                                "item-2",
                                "two.png"),

                            CreateRequest(
                                "item-3",
                                "three.png")
                        ]));

        Assert.Contains(
            "configured maximum of 2",
            exception.Message);
    }

    [Fact]
    public async Task VerifyAsync_WhenBatchIsEmpty_Throws()
    {
        var singleLabelService =
            new FakeLabelVerificationService(
                new Dictionary<string, VerificationStatus>());

        var service =
            CreateService(
                singleLabelService);

        await Assert.ThrowsAsync<ArgumentException>(
            () =>
                service.VerifyAsync(
                    "COLA-84729",
                    []));
    }

    [Fact]
    public async Task VerifyAsync_PassesCancellationTokenToSingleLabelWorkflow()
    {
        var singleLabelService =
            new CancellationObservingLabelVerificationService();

        var service =
            CreateService(
                singleLabelService);

        using var cancellation =
            new CancellationTokenSource();

        var verificationTask =
            service.VerifyAsync(
                "COLA-84729",
                [
                    CreateRequest(
                        "item-1",
                        "cancel.png")
                ],
                cancellationToken:
                    cancellation.Token);

        await singleLabelService
            .Started
            .Task
            .WaitAsync(
                TimeSpan.FromSeconds(2));

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await verificationTask);

        Assert.True(
            singleLabelService
                .ObservedCancellation);
    }

    private static BatchLabelVerificationService CreateService(
        ILabelVerificationService singleLabelService,
        int maxConcurrency = 3,
        int maxBatchSize = 300)
    {
        var options =
            new BatchVerificationOptions
            {
                MaxConcurrency =
                    maxConcurrency,

                MaxBatchSize =
                    maxBatchSize
            };

        return new BatchLabelVerificationService(
            singleLabelService,
            options,
            NullLogger<BatchLabelVerificationService>
                .Instance);
    }

    private static BatchVerificationItemRequest CreateRequest(
        string itemId,
        string displayName)
    {
        return new BatchVerificationItemRequest(
            itemId,
            displayName,
            "image/png",
            3,
            () =>
                new MemoryStream(
                    new byte[]
                    {
                        1,
                        2,
                        3
                    },
                    writable: false));
    }

    private static LabelVerificationSubmissionResult CreateSuccess(
        VerificationStatus status)
    {
        var aggregator =
            new VerificationResultAggregator();

        var verification =
            aggregator.Aggregate(
            [
                new VerificationCheckResult
                {
                    Field =
                        "Test field",

                    Status =
                        status,

                    Explanation =
                        $"Simulated {status} verification result."
                }
            ]);

        return new LabelVerificationSubmissionResult
        {
            ProcessingSucceeded =
                true,

            Verification =
                verification
        };
    }

    private sealed class FakeLabelVerificationService
        : ILabelVerificationService
    {
        private readonly IReadOnlyDictionary<
            string,
            VerificationStatus> _statuses;

        private readonly IReadOnlyDictionary<
            string,
            TimeSpan> _delays;

        private readonly IReadOnlyDictionary<
            string,
            Exception> _failures;

        private readonly IReadOnlyDictionary<
            string,
            LabelVerificationSubmissionResult> _technicalFailures;

        private int _currentConcurrency;
        private int _maximumObservedConcurrency;

        public FakeLabelVerificationService(
            IReadOnlyDictionary<
                string,
                VerificationStatus> statuses,
            IReadOnlyDictionary<
                string,
                TimeSpan>? delays = null,
            IReadOnlyDictionary<
                string,
                Exception>? failures = null,
            IReadOnlyDictionary<
                string,
                LabelVerificationSubmissionResult>? technicalFailures = null)
        {
            _statuses =
                statuses;

            _delays =
                delays ??
                new Dictionary<string, TimeSpan>();

            _failures =
                failures ??
                new Dictionary<string, Exception>();

            _technicalFailures =
                technicalFailures ??
                new Dictionary<
                    string,
                    LabelVerificationSubmissionResult>();
        }

        public int MaximumObservedConcurrency =>
            Volatile.Read(
                ref _maximumObservedConcurrency);

        public async Task<LabelVerificationSubmissionResult> VerifyAsync(
            string applicationId,
            Stream image,
            string fileName,
            string? contentType,
            long length,
            CancellationToken cancellationToken = default)
        {
            var currentConcurrency =
                Interlocked.Increment(
                    ref _currentConcurrency);

            UpdateMaximumConcurrency(
                currentConcurrency);

            try
            {
                if (_delays.TryGetValue(
                        fileName,
                        out var delay))
                {
                    await Task.Delay(
                        delay,
                        cancellationToken);
                }

                if (_failures.TryGetValue(
                        fileName,
                        out var failure))
                {
                    throw failure;
                }

                if (_technicalFailures.TryGetValue(
                        fileName,
                        out var technicalFailure))
                {
                    return technicalFailure;
                }

                if (!_statuses.TryGetValue(
                        fileName,
                        out var status))
                {
                    throw new InvalidOperationException(
                        $"No test result configured for {fileName}.");
                }

                return CreateSuccess(
                    status);
            }
            finally
            {
                Interlocked.Decrement(
                    ref _currentConcurrency);
            }
        }

        private void UpdateMaximumConcurrency(
            int currentConcurrency)
        {
            while (true)
            {
                var observedMaximum =
                    Volatile.Read(
                        ref _maximumObservedConcurrency);

                if (currentConcurrency <=
                    observedMaximum)
                {
                    return;
                }

                if (Interlocked.CompareExchange(
                        ref _maximumObservedConcurrency,
                        currentConcurrency,
                        observedMaximum) ==
                    observedMaximum)
                {
                    return;
                }
            }
        }
    }

    private sealed class CancellationObservingLabelVerificationService
        : ILabelVerificationService
    {
        public TaskCompletionSource<bool> Started { get; } =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ObservedCancellation { get; private set; }

        public async Task<LabelVerificationSubmissionResult> VerifyAsync(
            string applicationId,
            Stream image,
            string fileName,
            string? contentType,
            long length,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult(
                true);

            try
            {
                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                ObservedCancellation =
                    true;

                throw;
            }

            throw new InvalidOperationException(
                "The cancellation test should never reach this point.");
        }
    }

    private sealed class RecordingProgress<T>
        : IProgress<T>
    {
        private readonly ConcurrentQueue<T> _values =
            new();

        public IReadOnlyList<T> Values =>
            _values.ToArray();

        public void Report(
            T value)
        {
            _values.Enqueue(
                value);
        }
    }
}