using System.Collections.Concurrent;
using System.Diagnostics;
using LabelVerification.Application.Exceptions;
using LabelVerification.Application.LabelUnderstanding;
using LabelVerification.Application.Verification.Workflow;
using Microsoft.Extensions.Logging;

namespace LabelVerification.Application.Verification.Batch;

/// <summary>
/// Coordinates bounded-concurrency execution of the existing single-label
/// verification workflow.
///
/// This service contains no regulatory verification rules.
/// Each item delegates to ILabelVerificationService.
/// </summary>
public sealed class BatchLabelVerificationService
    : IBatchLabelVerificationService
{
    private readonly ILabelVerificationService _labelVerificationService;
    private readonly ILogger<BatchLabelVerificationService> _logger;
    private readonly BatchVerificationOptions _options;

    public BatchLabelVerificationService(
        ILabelVerificationService labelVerificationService,
        BatchVerificationOptions options,
        ILogger<BatchLabelVerificationService> logger)
    {
        ArgumentNullException.ThrowIfNull(
            labelVerificationService);

        ArgumentNullException.ThrowIfNull(
            options);

        ArgumentNullException.ThrowIfNull(
            logger);

        _labelVerificationService =
            labelVerificationService;

        _options =
            options;

        _logger =
            logger;
    }

    public async Task<BatchVerificationResult> VerifyAsync(
        string applicationId,
        IReadOnlyList<BatchVerificationItemRequest> items,
        IProgress<BatchVerificationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            applicationId);

        ArgumentNullException.ThrowIfNull(
            items);

        if (items.Count == 0)
        {
            throw new ArgumentException(
                "At least one batch item is required.",
                nameof(items));
        }

        if (items.Count >
            _options.MaxBatchSize)
        {
            throw new ArgumentException(
                $"Batch contains {items.Count} items, which exceeds " +
                $"the configured maximum of {_options.MaxBatchSize}.",
                nameof(items));
        }

        var batchCorrelationId =
            Guid.NewGuid()
                .ToString("N");

        using var batchScope =
            _logger.BeginScope(
                new Dictionary<string, object>
                {
                    ["BatchCorrelationId"] =
                        batchCorrelationId
                });

        _logger.LogInformation(
            "Batch verification started for application {ApplicationId}. " +
            "ItemCount={ItemCount}, MaxConcurrency={MaxConcurrency}.",
            applicationId,
            items.Count,
            _options.MaxConcurrency);

        var batchStopwatch =
            Stopwatch.StartNew();

        var indexedItems =
            items
                .Select(
                    (item, index) =>
                        new IndexedBatchItem(
                            index,
                            item))
                .ToArray();

        var results =
            new ConcurrentDictionary<
                int,
                BatchVerificationItemResult>();

        var progressState =
            new BatchProgressState(
                batchCorrelationId,
                items.Count,
                progress);

        progressState.ReportInitial();

        try
        {
            await Parallel.ForEachAsync(
                indexedItems,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism =
                        _options.MaxConcurrency,

                    CancellationToken =
                        cancellationToken
                },
                async (indexedItem, token) =>
                {
                    progressState.ReportStarted(
                        indexedItem.Item.ItemId);

                    var result =
                        await ProcessItemAsync(
                            applicationId,
                            indexedItem.Item,
                            token);

                    results[indexedItem.Index] =
                        result;

                    progressState.ReportCompleted(
                        result);
                });
        }
        finally
        {
            batchStopwatch.Stop();
        }

        var orderedResults =
            results
                .OrderBy(
                    pair =>
                        pair.Key)
                .Select(
                    pair =>
                        pair.Value)
                .ToArray();

        var summary =
            CreateSummary(
                orderedResults,
                batchStopwatch.Elapsed);

        _logger.LogInformation(
            "Batch verification completed for application {ApplicationId}. " +
            "Total={TotalCount}, Pass={PassCount}, Review={ReviewCount}, " +
            "Fail={FailCount}, Error={ErrorCount}, ElapsedMs={ElapsedMs}.",
            applicationId,
            summary.TotalCount,
            summary.PassCount,
            summary.ReviewCount,
            summary.FailCount,
            summary.ErrorCount,
            summary.Elapsed.TotalMilliseconds);

        return new BatchVerificationResult
        {
            BatchCorrelationId =
                batchCorrelationId,

            Items =
                orderedResults,

            Summary =
                summary
        };
    }

    private async Task<BatchVerificationItemResult> ProcessItemAsync(
        string applicationId,
        BatchVerificationItemRequest item,
        CancellationToken cancellationToken)
    {
        var stopwatch =
            Stopwatch.StartNew();

        try
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            await using var image =
                item.OpenReadStream();

            var result =
                await _labelVerificationService.VerifyAsync(
                    applicationId,
                    image,
                    item.DisplayName,
                    item.ContentType,
                    item.Size,
                    cancellationToken);

            stopwatch.Stop();

            if (!result.ProcessingSucceeded)
            {
                return new BatchVerificationItemResult
                {
                    ItemId =
                        item.ItemId,

                    DisplayName =
                        item.DisplayName,

                    ProcessingStatus =
                        BatchItemProcessingStatus.Error,

                    VerificationResult =
                        result,

                    ErrorCode =
                        result.ErrorCode,

                    ErrorMessage =
                        result.ErrorMessage,

                    Duration =
                        stopwatch.Elapsed
                };
            }

            return new BatchVerificationItemResult
            {
                ItemId =
                    item.ItemId,

                DisplayName =
                    item.DisplayName,

                ProcessingStatus =
                    BatchItemProcessingStatus.Completed,

                VerificationResult =
                    result,

                Duration =
                    stopwatch.Elapsed
            };
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (LabelTextExtractionException exception)
        {
            stopwatch.Stop();

            _logger.LogWarning(
                exception,
                "OCR failed for an item in batch verification.");

            return CreateErrorResult(
                item,
                "OCR_FAILURE",
                "The label could not be read by the OCR service.",
                stopwatch.Elapsed);
        }
        catch (InvalidApplicationRecordException exception)
        {
            stopwatch.Stop();

            _logger.LogWarning(
                exception,
                "Application data was invalid during batch verification.");

            return CreateErrorResult(
                item,
                "APPLICATION_DATA_INVALID",
                "The application data could not be loaded safely.",
                stopwatch.Elapsed);
        }
        catch (IOException exception)
        {
            stopwatch.Stop();

            _logger.LogWarning(
                exception,
                "Unable to read a label image during batch verification.");

            return CreateErrorResult(
                item,
                "IMAGE_READ_FAILURE",
                "The label image could not be read.",
                stopwatch.Elapsed);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();

            _logger.LogError(
                exception,
                "Unexpected batch item verification failure.");

            return CreateErrorResult(
                item,
                "UNEXPECTED_ERROR",
                "The label could not be processed.",
                stopwatch.Elapsed);
        }
    }

    private static BatchVerificationItemResult CreateErrorResult(
        BatchVerificationItemRequest item,
        string errorCode,
        string errorMessage,
        TimeSpan duration)
    {
        return new BatchVerificationItemResult
        {
            ItemId =
                item.ItemId,

            DisplayName =
                item.DisplayName,

            ProcessingStatus =
                BatchItemProcessingStatus.Error,

            ErrorCode =
                errorCode,

            ErrorMessage =
                errorMessage,

            Duration =
                duration
        };
    }

    private static BatchVerificationSummary CreateSummary(
        IReadOnlyList<BatchVerificationItemResult> results,
        TimeSpan elapsed)
    {
        var passCount = 0;
        var reviewCount = 0;
        var failCount = 0;
        var errorCount = 0;

        foreach (var item in results)
        {
            if (item.ProcessingStatus ==
                BatchItemProcessingStatus.Error)
            {
                errorCount++;
                continue;
            }

            var verification =
                item.VerificationResult?
                    .Verification;

            if (verification is null)
            {
                errorCount++;
                continue;
            }

            switch (verification.OverallStatus)
            {
                case VerificationStatus.Pass:
                    passCount++;
                    break;

                case VerificationStatus.Review:
                    reviewCount++;
                    break;

                case VerificationStatus.Fail:
                    failCount++;
                    break;

                default:
                    errorCount++;
                    break;
            }
        }

        return new BatchVerificationSummary
        {
            TotalCount =
                results.Count,

            CompletedCount =
                results.Count,

            PassCount =
                passCount,

            ReviewCount =
                reviewCount,

            FailCount =
                failCount,

            ErrorCount =
                errorCount,

            Elapsed =
                elapsed
        };
    }

    private sealed record IndexedBatchItem(
        int Index,
        BatchVerificationItemRequest Item);

    /// <summary>
    /// Thread-safe progress accumulator used by parallel workers.
    /// </summary>
    private sealed class BatchProgressState
    {
        private readonly string _batchCorrelationId;
        private readonly int _totalItems;
        private readonly IProgress<BatchVerificationProgress>? _progress;

        private readonly object _sync =
            new();

        private int _completedItems;
        private int _passCount;
        private int _reviewCount;
        private int _failCount;
        private int _errorCount;

        public BatchProgressState(
            string batchCorrelationId,
            int totalItems,
            IProgress<BatchVerificationProgress>? progress)
        {
            _batchCorrelationId =
                batchCorrelationId;

            _totalItems =
                totalItems;

            _progress =
                progress;
        }

        public void ReportInitial()
        {
            _progress?
                .Report(
                    new BatchVerificationProgress
                    {
                        BatchCorrelationId =
                            _batchCorrelationId,

                        TotalItems =
                            _totalItems,

                        CompletedItems =
                            0,

                        PassCount =
                            0,

                        ReviewCount =
                            0,

                        FailCount =
                            0,

                        ErrorCount =
                            0,

                        LastCompletedItemId =
                            null
                    });
        }

        public void ReportStarted(
            string itemId)
        {
            BatchVerificationProgress snapshot;

            lock (_sync)
            {
                snapshot =
                    new BatchVerificationProgress
                    {
                        BatchCorrelationId =
                            _batchCorrelationId,

                        TotalItems =
                            _totalItems,

                        CompletedItems =
                            _completedItems,

                        PassCount =
                            _passCount,

                        ReviewCount =
                            _reviewCount,

                        FailCount =
                            _failCount,

                        ErrorCount =
                            _errorCount,

                        LastCompletedItemId =
                            null,

                        ItemId =
                            itemId,

                        ItemProcessingStatus =
                            BatchItemProcessingStatus.Processing,

                        CompletedItemResult =
                            null
                    };
            }

            _progress?
                .Report(
                    snapshot);
        }

        public void ReportCompleted(
            BatchVerificationItemResult result)
        {
            BatchVerificationProgress snapshot;

            lock (_sync)
            {
                _completedItems++;

                if (result.ProcessingStatus ==
                    BatchItemProcessingStatus.Error)
                {
                    _errorCount++;
                }
                else
                {
                    var status =
                        result.VerificationResult?
                            .Verification?
                            .OverallStatus;

                    switch (status)
                    {
                        case VerificationStatus.Pass:
                            _passCount++;
                            break;

                        case VerificationStatus.Review:
                            _reviewCount++;
                            break;

                        case VerificationStatus.Fail:
                            _failCount++;
                            break;

                        default:
                            _errorCount++;
                            break;
                    }
                }

                snapshot =
                    new BatchVerificationProgress
                    {
                        BatchCorrelationId =
                            _batchCorrelationId,

                        TotalItems =
                            _totalItems,

                        CompletedItems =
                            _completedItems,

                        PassCount =
                            _passCount,

                        ReviewCount =
                            _reviewCount,

                        FailCount =
                            _failCount,

                        ErrorCount =
                            _errorCount,

                        LastCompletedItemId =
                            result.ItemId,

                        ItemId =
                            result.ItemId,

                        ItemProcessingStatus =
                            result.ProcessingStatus,

                        CompletedItemResult =
                            result
                    };
            }

            _progress?
                .Report(
                    snapshot);
        }
    }
}