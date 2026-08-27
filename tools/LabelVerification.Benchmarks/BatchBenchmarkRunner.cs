using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using LabelVerification.Application;
using LabelVerification.Application.Verification;
using LabelVerification.Application.Verification.Batch;
using LabelVerification.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
// Required for the concrete single-label workflow type used when registering
// its silent benchmark logger.
using LabelVerification.Application.Verification.Workflow;

internal static class BatchBenchmarkRunner
{
    private const string ApplicationId =
        "COLA-84729";

    private const double FiveSecondTargetMilliseconds =
        5000.0;

    private const int DefaultBatchSize =
        30;

    private const int DefaultMeasuredIterations =
        3;

    private const int DefaultMaxConcurrency =
        3;

    private const int MaximumSupportedBatchSize =
        300;

    public static async Task<int> RunAsync()
    {
        var repositoryRoot =
            FindRepositoryRoot();

        var endpoint =
            RequireEnvironmentVariable(
                "DocumentIntelligence__Endpoint");

        var modelId =
            Environment.GetEnvironmentVariable(
                "DocumentIntelligence__ModelId")
            ?? "prebuilt-read";

        var timeoutSeconds =
            GetPositiveIntegerEnvironmentVariable(
                "DocumentIntelligence__TimeoutSeconds",
                5);

        var authenticationTimeoutSeconds =
            GetPositiveIntegerEnvironmentVariable(
                "DocumentIntelligence__AuthenticationTimeoutSeconds",
                15);

        var enableFontStyling =
            GetBooleanEnvironmentVariable(
                "DocumentIntelligence__EnableFontStyling",
                true);

        var batchSize =
            GetPositiveIntegerEnvironmentVariable(
                "BATCH_BENCHMARK_SIZE",
                DefaultBatchSize);

        var measuredIterations =
            GetPositiveIntegerEnvironmentVariable(
                "BATCH_BENCHMARK_ITERATIONS",
                DefaultMeasuredIterations);

        var maxConcurrency =
            GetPositiveIntegerEnvironmentVariable(
                "BATCH_BENCHMARK_CONCURRENCY",
                DefaultMaxConcurrency);

        if (batchSize >
            MaximumSupportedBatchSize)
        {
            throw new InvalidOperationException(
                $"BATCH_BENCHMARK_SIZE cannot exceed " +
                $"{MaximumSupportedBatchSize}.");
        }

        if (maxConcurrency >
            batchSize)
        {
            throw new InvalidOperationException(
                "BATCH_BENCHMARK_CONCURRENCY cannot exceed " +
                "BATCH_BENCHMARK_SIZE.");
        }

        var azureRegion =
            Environment.GetEnvironmentVariable(
                "BENCHMARK_AZURE_REGION")
            ?? "(not recorded)";

        var documentIntelligenceSku =
            Environment.GetEnvironmentVariable(
                "BENCHMARK_DOCINTEL_SKU")
            ?? "(not recorded)";

        var benchmarkLocation =
            Environment.GetEnvironmentVariable(
                "BENCHMARK_LOCATION")
            ?? "Developer workstation to Azure Document Intelligence";

        var applicationDataDirectory =
            Path.Combine(
                repositoryRoot,
                "sample-data",
                "applications");

        var labelDirectory =
            Path.Combine(
                repositoryRoot,
                "sample-data",
                "labels",
                "verification");

        var outputDirectory =
            Path.Combine(
                repositoryRoot,
                "benchmark-results");

        var fixturePool =
            new[]
            {
                "compliant-label.png",
                "brand-variation-label.png",
                "incorrect-abv-label.png",
                "incorrect-net-contents-label.png",
                "missing-warning-label.png",
                "modified-warning-label.png",
                "rotated-label.png",
                "degraded-label.jpg",
                "compliant-with-glare.jpg",
                "compliant-with-poor-light.jpg"
            };

        ValidateFixtures(
            labelDirectory,
            fixturePool);

        var configuration =
            new ConfigurationManager();

        configuration["ApplicationData:Directory"] =
            applicationDataDirectory;

        configuration["DocumentIntelligence:Endpoint"] =
            endpoint;

        configuration["DocumentIntelligence:ModelId"] =
            modelId;

        configuration["DocumentIntelligence:TimeoutSeconds"] =
            timeoutSeconds.ToString(
                CultureInfo.InvariantCulture);

        configuration["DocumentIntelligence:AuthenticationTimeoutSeconds"] =
            authenticationTimeoutSeconds.ToString(
                CultureInfo.InvariantCulture);

        configuration["DocumentIntelligence:EnableFontStyling"] =
            enableFontStyling.ToString(
                CultureInfo.InvariantCulture);

        var services =
            new ServiceCollection();

        // Register the production Application-layer workflow graph. The batch
        // coordinator deliberately delegates every item to the existing
        // ILabelVerificationService implementation rather than duplicating
        // verification rules inside the benchmark.
        services.AddApplication();

        // Register the production Infrastructure layer so this benchmark exercises
        // the real Azure Document Intelligence provider and application-data adapter.
        services.AddInfrastructure(
            configuration);

        // ValidateOnBuild validates the complete workflow dependency graph. The
        // single-label workflow requires ILogger<LabelVerificationService>, so supply
        // a NullLogger to keep benchmark output free from routine application logs
        // while still satisfying the production constructor contract.
        services.AddSingleton<
            ILogger<LabelVerificationService>>(
                NullLogger<LabelVerificationService>.Instance);

        // The batch coordinator also uses structured logging in production. A
        // NullLogger preserves that constructor contract without adding console log
        // noise that would make benchmark output harder to read.
        services.AddSingleton<
            ILogger<BatchLabelVerificationService>>(
                NullLogger<BatchLabelVerificationService>.Instance);

        using var serviceProvider =
            services.BuildServiceProvider(
                new ServiceProviderOptions
                {
                    ValidateOnBuild =
                        true,

                    ValidateScopes =
                        true
                });

        var batchOptions =
            serviceProvider.GetRequiredService<
                BatchVerificationOptions>();

        batchOptions.MaxBatchSize =
            MaximumSupportedBatchSize;

        batchOptions.MaxConcurrency =
            maxConcurrency;

        var batchVerifier =
            serviceProvider.GetRequiredService<
                IBatchLabelVerificationService>();

        var gitSha =
            GetCommandOutput(
                "git",
                "rev-parse HEAD");

        var dotnetSdk =
            GetCommandOutput(
                "dotnet",
                "--version");

        Console.WriteLine();
        Console.WriteLine(
            "===== TTB BATCH LABEL VERIFICATION BENCHMARK =====");

        Console.WriteLine(
            $"Git SHA:             {gitSha}");

        Console.WriteLine(
            $"OCR model:           {modelId}");

        Console.WriteLine(
            $"OCR timeout:         {timeoutSeconds} seconds");

        Console.WriteLine(
            $"Font styling:        {enableFontStyling}");

        Console.WriteLine(
            $"Azure region:        {azureRegion}");

        Console.WriteLine(
            $"Document Intel SKU:  {documentIntelligenceSku}");

        Console.WriteLine(
            $"Measured batch size: {batchSize} labels");

        Console.WriteLine(
            $"Measured batches:    {measuredIterations}");

        Console.WriteLine(
            $"Max concurrency:     {maxConcurrency}");

        Console.WriteLine(
            $"Formal label attempts: " +
            $"{batchSize * measuredIterations}");

        Console.WriteLine();

        //
        // Warm-up.
        //
        // A small multi-label batch exercises credential, SDK, network,
        // Document Intelligence, and concurrent workflow initialization.
        // This batch is excluded from formal statistics.
        //
        var warmupSize =
            Math.Min(
                fixturePool.Length,
                Math.Max(
                    maxConcurrency * 2,
                    maxConcurrency));

        Console.WriteLine(
            "===== EXCLUDED BATCH WARM-UP =====");

        var warmupRequests =
            CreateRequests(
                labelDirectory,
                fixturePool,
                warmupSize,
                iteration: 0,
                rotation: 0);

        var warmup =
            await RunBatchAsync(
                batchVerifier,
                warmupRequests,
                iteration: 0);

        Console.WriteLine(
            $"Warm-up labels:      {warmup.RequestedCount}");

        Console.WriteLine(
            $"Warm-up returned:    {warmup.ReturnedCount}");

        Console.WriteLine(
            $"Warm-up errors:      {warmup.ErrorCount}");

        Console.WriteLine(
            $"Warm-up wall time:   " +
            $"{FormatMilliseconds(warmup.BatchElapsedMilliseconds)}");

        Console.WriteLine(
            $"Warm-up throughput:  " +
            $"{warmup.LabelsPerMinute:N1} labels/min");

        Console.WriteLine();

        Console.WriteLine(
            "Warm-up batch is excluded from formal statistics.");

        Console.WriteLine();

        //
        // Formal measurement.
        //
        Console.WriteLine(
            "===== MEASURED BATCH PHASE =====");

        var runs =
            new List<BatchBenchmarkRunObservation>();

        var itemObservations =
            new List<BatchBenchmarkItemObservation>();

        for (var iteration = 1;
             iteration <= measuredIterations;
             iteration++)
        {
            var rotation =
                (iteration - 1) %
                fixturePool.Length;

            var requests =
                CreateRequests(
                    labelDirectory,
                    fixturePool,
                    batchSize,
                    iteration,
                    rotation);

            var run =
                await RunBatchAsync(
                    batchVerifier,
                    requests,
                    iteration);

            runs.Add(
                run);

            itemObservations.AddRange(
                run.Items);

            Console.WriteLine();
            Console.WriteLine(
                $"Batch {iteration}/{measuredIterations}");

            Console.WriteLine(
                $"  Wall time:       " +
                $"{FormatMilliseconds(run.BatchElapsedMilliseconds)}");

            Console.WriteLine(
                $"  Throughput:      " +
                $"{run.LabelsPerMinute:N1} labels/min");

            Console.WriteLine(
                $"  Returned:        " +
                $"{run.ReturnedCount}/{run.RequestedCount}");

            Console.WriteLine(
                $"  PASS:            {run.PassCount}");

            Console.WriteLine(
                $"  REVIEW:          {run.ReviewCount}");

            Console.WriteLine(
                $"  FAIL:            {run.FailCount}");

            Console.WriteLine(
                $"  ERROR:           {run.ErrorCount}");

            Console.WriteLine(
                $"  <=5s per item:   " +
                $"{run.ItemsWithinFiveSecondTarget}/" +
                $"{run.RequestedCount}");

            Console.WriteLine(
                $"  Median item:     " +
                $"{FormatMilliseconds(run.ItemDuration.MedianMilliseconds)}");

            Console.WriteLine(
                $"  P95 item:        " +
                $"{FormatMilliseconds(run.ItemDuration.P95Milliseconds)}");

            Console.WriteLine(
                $"  Worst item:      " +
                $"{FormatMilliseconds(run.ItemDuration.WorstMilliseconds)}");

            if (!string.IsNullOrWhiteSpace(
                    run.BatchFailure))
            {
                Console.WriteLine(
                    $"  Batch failure:   {run.BatchFailure}");
            }
        }

        var environment =
            new BatchBenchmarkEnvironment
            {
                GeneratedUtc =
                    DateTimeOffset.UtcNow,

                GitSha =
                    gitSha,

                DotnetSdk =
                    dotnetSdk,

                Runtime =
                    RuntimeInformation.FrameworkDescription,

                OperatingSystem =
                    RuntimeInformation.OSDescription,

                Architecture =
                    RuntimeInformation.ProcessArchitecture
                        .ToString(),

                LogicalProcessorCount =
                    Environment.ProcessorCount,

                BenchmarkLocation =
                    benchmarkLocation,

                AzureRegion =
                    azureRegion,

                DocumentIntelligenceSku =
                    documentIntelligenceSku,

                EndpointHost =
                    new Uri(
                        endpoint)
                        .Host,

                ModelId =
                    modelId,

                TimeoutSeconds =
                    timeoutSeconds,

                AuthenticationTimeoutSeconds =
                    authenticationTimeoutSeconds,

                EnableFontStyling =
                    enableFontStyling,

                Authentication =
                    "DefaultAzureCredential via shared caching credential",

                BatchSize =
                    batchSize,

                MeasuredIterations =
                    measuredIterations,

                MaxConcurrency =
                    maxConcurrency,

                WarmupBatchSize =
                    warmupSize,

                FixtureCount =
                    fixturePool.Length
            };

        var summary =
            BuildSummary(
                environment,
                runs,
                itemObservations);

        Directory.CreateDirectory(
            outputDirectory);

        var utf8NoBom =
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier:
                    false);

        var runCsvPath =
            Path.Combine(
                outputDirectory,
                "batch-results.csv");

        var itemCsvPath =
            Path.Combine(
                outputDirectory,
                "batch-item-results.csv");

        var jsonPath =
            Path.Combine(
                outputDirectory,
                "batch-summary.json");

        var markdownPath =
            Path.Combine(
                outputDirectory,
                "batch-summary.md");

        File.WriteAllText(
            runCsvPath,
            BuildRunCsv(
                runs),
            utf8NoBom);

        File.WriteAllText(
            itemCsvPath,
            BuildItemCsv(
                itemObservations),
            utf8NoBom);

        File.WriteAllText(
            jsonPath,
            JsonSerializer.Serialize(
                summary,
                new JsonSerializerOptions
                {
                    WriteIndented =
                        true
                }),
            utf8NoBom);

        File.WriteAllText(
            markdownPath,
            BuildMarkdown(
                summary),
            utf8NoBom);

        Console.WriteLine();
        Console.WriteLine(
            "===== FORMAL BATCH BENCHMARK SUMMARY =====");

        Console.WriteLine(
            $"Measured batches:       {summary.MeasuredBatchCount}");

        Console.WriteLine(
            $"Requested labels:       {summary.RequestedLabelCount}");

        Console.WriteLine(
            $"Returned item results:  {summary.ReturnedLabelCount}");

        Console.WriteLine(
            $"Regulatory completions: {summary.RegulatoryCompletionCount}");

        Console.WriteLine(
            $"Technical errors:       {summary.ErrorCount}");

        Console.WriteLine(
            $"PASS:                   {summary.PassCount}");

        Console.WriteLine(
            $"REVIEW:                 {summary.ReviewCount}");

        Console.WriteLine(
            $"FAIL:                   {summary.FailCount}");

        Console.WriteLine(
            $"<=5s per-item target:   " +
            $"{summary.ItemsWithinFiveSecondTarget}/" +
            $"{summary.RequestedLabelCount} " +
            $"({summary.ItemTargetRatePercent:N1}%)");

        Console.WriteLine(
            $"Median batch wall:      " +
            $"{FormatMilliseconds(summary.BatchElapsed.MedianMilliseconds)}");

        Console.WriteLine(
            $"P95 batch wall:         " +
            $"{FormatMilliseconds(summary.BatchElapsed.P95Milliseconds)}");

        Console.WriteLine(
            $"Median throughput:      " +
            $"{summary.LabelsPerMinute.Median:N1} labels/min");

        Console.WriteLine(
            $"P95 throughput:         " +
            $"{summary.LabelsPerMinute.P95:N1} labels/min");

        Console.WriteLine(
            $"Median item duration:   " +
            $"{FormatMilliseconds(summary.ItemDuration.MedianMilliseconds)}");

        Console.WriteLine(
            $"P95 item duration:      " +
            $"{FormatMilliseconds(summary.ItemDuration.P95Milliseconds)}");

        Console.WriteLine(
            $"Worst item duration:    " +
            $"{FormatMilliseconds(summary.ItemDuration.WorstMilliseconds)}");

        Console.WriteLine();
        Console.WriteLine(
            "IMPORTANT: the approximately five-second stakeholder target is " +
            "evaluated per label, not against total batch wall-clock time.");

        Console.WriteLine();

        Console.WriteLine(
            $"Batch runs:       {runCsvPath}");

        Console.WriteLine(
            $"Batch items:      {itemCsvPath}");

        Console.WriteLine(
            $"JSON summary:     {jsonPath}");

        Console.WriteLine(
            $"Markdown summary: {markdownPath}");

        Console.WriteLine();

        var allRunsCompleted =
            runs.All(
                run =>
                    string.IsNullOrWhiteSpace(
                        run.BatchFailure) &&
                    run.ReturnedCount ==
                        run.RequestedCount);

        var noTechnicalErrors =
            runs.All(
                run =>
                    run.ErrorCount == 0);

        return allRunsCompleted &&
               noTechnicalErrors
            ? 0
            : 2;
    }

    private static async Task<BatchBenchmarkRunObservation>
        RunBatchAsync(
            IBatchLabelVerificationService batchVerifier,
            IReadOnlyList<BatchVerificationItemRequest> requests,
            int iteration)
    {
        var stopwatch =
            Stopwatch.StartNew();

        try
        {
            var result =
                await batchVerifier.VerifyAsync(
                    ApplicationId,
                    requests,
                    progress:
                        null,
                    cancellationToken:
                        CancellationToken.None);

            stopwatch.Stop();

            var items =
                result.Items
                    .Select(
                        item =>
                            CreateItemObservation(
                                iteration,
                                item))
                    .ToArray();

            var itemDurations =
                BuildMetric(
                    items.Select(
                        item =>
                            item.DurationMilliseconds));

            var elapsedMinutes =
                stopwatch.Elapsed.TotalMinutes;

            var labelsPerMinute =
                elapsedMinutes <= 0
                    ? 0
                    : result.Items.Count /
                      elapsedMinutes;

            return new BatchBenchmarkRunObservation
            {
                Iteration =
                    iteration,

                RequestedCount =
                    requests.Count,

                ReturnedCount =
                    result.Items.Count,

                PassCount =
                    result.Summary.PassCount,

                ReviewCount =
                    result.Summary.ReviewCount,

                FailCount =
                    result.Summary.FailCount,

                ErrorCount =
                    result.Summary.ErrorCount,

                RegulatoryCompletionCount =
                    result.Summary.PassCount +
                    result.Summary.ReviewCount +
                    result.Summary.FailCount,

                BatchElapsedMilliseconds =
                    stopwatch.Elapsed.TotalMilliseconds,

                LabelsPerMinute =
                    labelsPerMinute,

                ItemsWithinFiveSecondTarget =
                    items.Count(
                        item =>
                            item.WithinFiveSecondTarget),

                BatchCorrelationId =
                    result.BatchCorrelationId,

                BatchFailure =
                    null,

                ItemDuration =
                    itemDurations,

                Items =
                    items
            };
        }
        catch (Exception exception)
        {
            stopwatch.Stop();

            return new BatchBenchmarkRunObservation
            {
                Iteration =
                    iteration,

                RequestedCount =
                    requests.Count,

                ReturnedCount =
                    0,

                PassCount =
                    0,

                ReviewCount =
                    0,

                FailCount =
                    0,

                ErrorCount =
                    requests.Count,

                RegulatoryCompletionCount =
                    0,

                BatchElapsedMilliseconds =
                    stopwatch.Elapsed.TotalMilliseconds,

                LabelsPerMinute =
                    0,

                ItemsWithinFiveSecondTarget =
                    0,

                BatchCorrelationId =
                    null,

                BatchFailure =
                    exception.GetType().Name,

                ItemDuration =
                    new BatchMetricSummary(),

                Items =
                    []
            };
        }
    }

    private static BatchBenchmarkItemObservation
        CreateItemObservation(
            int iteration,
            BatchVerificationItemResult item)
    {
        var regulatoryStatus =
            item.VerificationResult?
                .Verification?
                .OverallStatus
                .ToString()
                .ToUpperInvariant();

        var verificationCorrelationId =
            item.VerificationResult?
                .Telemetry?
                .CorrelationId;

        var processingCompleted =
            item.ProcessingStatus ==
            BatchItemProcessingStatus.Completed;

        return new BatchBenchmarkItemObservation
        {
            Iteration =
                iteration,

            ItemId =
                item.ItemId,

            FileName =
                item.DisplayName,

            ProcessingStatus =
                item.ProcessingStatus
                    .ToString()
                    .ToUpperInvariant(),

            RegulatoryStatus =
                regulatoryStatus,

            ErrorCode =
                item.ErrorCode ??
                item.VerificationResult?
                    .ErrorCode,

            DurationMilliseconds =
                item.Duration.TotalMilliseconds,

            WithinFiveSecondTarget =
                processingCompleted &&
                item.Duration.TotalMilliseconds <=
                    FiveSecondTargetMilliseconds,

            VerificationCorrelationId =
                verificationCorrelationId
        };
    }

    private static IReadOnlyList<BatchVerificationItemRequest>
        CreateRequests(
            string labelDirectory,
            IReadOnlyList<string> fixturePool,
            int batchSize,
            int iteration,
            int rotation)
    {
        var requests =
            new List<BatchVerificationItemRequest>(
                batchSize);

        for (var index = 0;
             index < batchSize;
             index++)
        {
            var fixtureIndex =
                (index + rotation) %
                fixturePool.Count;

            var fileName =
                fixturePool[fixtureIndex];

            var path =
                Path.Combine(
                    labelDirectory,
                    fileName);

            var fileInfo =
                new FileInfo(
                    path);

            var contentType =
                GetContentType(
                    fileInfo.Extension);

            var itemId =
                iteration == 0
                    ? $"warmup-{index + 1:D3}"
                    : $"batch-{iteration:D2}-item-{index + 1:D3}";

            var streamPath =
                path;

            requests.Add(
                new BatchVerificationItemRequest(
                    itemId,
                    fileName,
                    contentType,
                    fileInfo.Length,
                    () =>
                        File.OpenRead(
                            streamPath)));
        }

        return requests;
    }

    private static BatchBenchmarkSummary BuildSummary(
        BatchBenchmarkEnvironment environment,
        IReadOnlyList<BatchBenchmarkRunObservation> runs,
        IReadOnlyList<BatchBenchmarkItemObservation> items)
    {
        var requestedCount =
            runs.Sum(
                run =>
                    run.RequestedCount);

        var returnedCount =
            runs.Sum(
                run =>
                    run.ReturnedCount);

        var targetCount =
            items.Count(
                item =>
                    item.WithinFiveSecondTarget);

        return new BatchBenchmarkSummary
        {
            Environment =
                environment,

            MeasuredBatchCount =
                runs.Count,

            RequestedLabelCount =
                requestedCount,

            ReturnedLabelCount =
                returnedCount,

            RegulatoryCompletionCount =
                runs.Sum(
                    run =>
                        run.RegulatoryCompletionCount),

            PassCount =
                runs.Sum(
                    run =>
                        run.PassCount),

            ReviewCount =
                runs.Sum(
                    run =>
                        run.ReviewCount),

            FailCount =
                runs.Sum(
                    run =>
                        run.FailCount),

            ErrorCount =
                runs.Sum(
                    run =>
                        run.ErrorCount),

            BatchFailureCount =
                runs.Count(
                    run =>
                        !string.IsNullOrWhiteSpace(
                            run.BatchFailure)),

            ItemsWithinFiveSecondTarget =
                targetCount,

            ItemTargetRatePercent =
                requestedCount == 0
                    ? 0
                    : 100.0 *
                      targetCount /
                      requestedCount,

            BatchElapsed =
                BuildMetric(
                    runs.Select(
                        run =>
                            run.BatchElapsedMilliseconds)),

            ItemDuration =
                BuildMetric(
                    items.Select(
                        item =>
                            item.DurationMilliseconds)),

            LabelsPerMinute =
                BuildRateMetric(
                    runs.Select(
                        run =>
                            run.LabelsPerMinute)),

            Runs =
                runs
        };
    }

    private static BatchMetricSummary BuildMetric(
        IEnumerable<double> source)
    {
        var values =
            source
                .OrderBy(
                    value =>
                        value)
                .ToArray();

        if (values.Length == 0)
        {
            return new BatchMetricSummary();
        }

        var median =
            values.Length % 2 == 1
                ? values[
                    values.Length / 2]
                : (
                    values[
                        (values.Length / 2) - 1] +
                    values[
                        values.Length / 2]
                  ) / 2.0;

        var p95Rank =
            (int)Math.Ceiling(
                0.95 *
                values.Length);

        var p95Index =
            Math.Clamp(
                p95Rank - 1,
                0,
                values.Length - 1);

        return new BatchMetricSummary
        {
            Count =
                values.Length,

            MedianMilliseconds =
                median,

            P95Milliseconds =
                values[p95Index],

            WorstMilliseconds =
                values[^1]
        };
    }

    private static BatchRateSummary BuildRateMetric(
        IEnumerable<double> source)
    {
        var values =
            source
                .OrderBy(
                    value =>
                        value)
                .ToArray();

        if (values.Length == 0)
        {
            return new BatchRateSummary();
        }

        var median =
            values.Length % 2 == 1
                ? values[
                    values.Length / 2]
                : (
                    values[
                        (values.Length / 2) - 1] +
                    values[
                        values.Length / 2]
                  ) / 2.0;

        var p95Rank =
            (int)Math.Ceiling(
                0.95 *
                values.Length);

        var p95Index =
            Math.Clamp(
                p95Rank - 1,
                0,
                values.Length - 1);

        return new BatchRateSummary
        {
            Count =
                values.Length,

            Median =
                median,

            P95 =
                values[p95Index],

            Best =
                values[^1]
        };
    }

    private static string BuildRunCsv(
        IEnumerable<BatchBenchmarkRunObservation> runs)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "Iteration,RequestedCount,ReturnedCount,PassCount," +
            "ReviewCount,FailCount,ErrorCount,RegulatoryCompletionCount," +
            "BatchElapsedMs,LabelsPerMinute,ItemsWithinFiveSecondTarget," +
            "BatchCorrelationId,BatchFailure");

        foreach (var run in runs)
        {
            builder.Append(
                run.Iteration);

            builder.Append(',');

            builder.Append(
                run.RequestedCount);

            builder.Append(',');

            builder.Append(
                run.ReturnedCount);

            builder.Append(',');

            builder.Append(
                run.PassCount);

            builder.Append(',');

            builder.Append(
                run.ReviewCount);

            builder.Append(',');

            builder.Append(
                run.FailCount);

            builder.Append(',');

            builder.Append(
                run.ErrorCount);

            builder.Append(',');

            builder.Append(
                run.RegulatoryCompletionCount);

            builder.Append(',');

            builder.Append(
                Number(
                    run.BatchElapsedMilliseconds));

            builder.Append(',');

            builder.Append(
                Number(
                    run.LabelsPerMinute));

            builder.Append(',');

            builder.Append(
                run.ItemsWithinFiveSecondTarget);

            builder.Append(',');

            builder.Append(
                Csv(
                    run.BatchCorrelationId));

            builder.Append(',');

            builder.Append(
                Csv(
                    run.BatchFailure));

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildItemCsv(
        IEnumerable<BatchBenchmarkItemObservation> items)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "Iteration,ItemId,FileName,ProcessingStatus,RegulatoryStatus," +
            "ErrorCode,DurationMs,WithinFiveSecondTarget," +
            "VerificationCorrelationId");

        foreach (var item in items)
        {
            builder.Append(
                item.Iteration);

            builder.Append(',');

            builder.Append(
                Csv(
                    item.ItemId));

            builder.Append(',');

            builder.Append(
                Csv(
                    item.FileName));

            builder.Append(',');

            builder.Append(
                Csv(
                    item.ProcessingStatus));

            builder.Append(',');

            builder.Append(
                Csv(
                    item.RegulatoryStatus));

            builder.Append(',');

            builder.Append(
                Csv(
                    item.ErrorCode));

            builder.Append(',');

            builder.Append(
                Number(
                    item.DurationMilliseconds));

            builder.Append(',');

            builder.Append(
                item.WithinFiveSecondTarget);

            builder.Append(',');

            builder.Append(
                Csv(
                    item.VerificationCorrelationId));

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildMarkdown(
        BatchBenchmarkSummary summary)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "# Batch Verification Performance Benchmark");

        builder.AppendLine();

        builder.AppendLine(
            $"Generated UTC: `{summary.Environment.GeneratedUtc:O}`");

        builder.AppendLine();

        builder.AppendLine(
            $"Git SHA: `{summary.Environment.GitSha}`");

        builder.AppendLine();

        builder.AppendLine(
            "## Results");

        builder.AppendLine();

        builder.AppendLine(
            "| Metric | Result |");

        builder.AppendLine(
            "|---|---:|");

        builder.AppendLine(
            $"| Measured batches | {summary.MeasuredBatchCount} |");

        builder.AppendLine(
            $"| Labels per batch | {summary.Environment.BatchSize} |");

        builder.AppendLine(
            $"| Configured max concurrency | {summary.Environment.MaxConcurrency} |");

        builder.AppendLine(
            $"| Requested labels | {summary.RequestedLabelCount} |");

        builder.AppendLine(
            $"| Returned item results | {summary.ReturnedLabelCount} |");

        builder.AppendLine(
            $"| Regulatory completions | {summary.RegulatoryCompletionCount} |");

        builder.AppendLine(
            $"| PASS | {summary.PassCount} |");

        builder.AppendLine(
            $"| REVIEW | {summary.ReviewCount} |");

        builder.AppendLine(
            $"| FAIL | {summary.FailCount} |");

        builder.AppendLine(
            $"| Technical ERROR | {summary.ErrorCount} |");

        builder.AppendLine(
            $"| Batch-level failures | {summary.BatchFailureCount} |");

        builder.AppendLine(
            $"| Per-item <=5s | " +
            $"{summary.ItemsWithinFiveSecondTarget}/" +
            $"{summary.RequestedLabelCount} " +
            $"({summary.ItemTargetRatePercent:0.#}%) |");

        builder.AppendLine(
            $"| Median batch wall time | " +
            $"{FormatMilliseconds(summary.BatchElapsed.MedianMilliseconds)} |");

        builder.AppendLine(
            $"| P95 batch wall time | " +
            $"{FormatMilliseconds(summary.BatchElapsed.P95Milliseconds)} |");

        builder.AppendLine(
            $"| Median throughput | " +
            $"{summary.LabelsPerMinute.Median:0.0} labels/min |");

        builder.AppendLine(
            $"| P95 throughput | " +
            $"{summary.LabelsPerMinute.P95:0.0} labels/min |");

        builder.AppendLine(
            $"| Best measured throughput | " +
            $"{summary.LabelsPerMinute.Best:0.0} labels/min |");

        builder.AppendLine(
            $"| Median item duration | " +
            $"{FormatMilliseconds(summary.ItemDuration.MedianMilliseconds)} |");

        builder.AppendLine(
            $"| P95 item duration | " +
            $"{FormatMilliseconds(summary.ItemDuration.P95Milliseconds)} |");

        builder.AppendLine(
            $"| Worst item duration | " +
            $"{FormatMilliseconds(summary.ItemDuration.WorstMilliseconds)} |");

        builder.AppendLine();

        builder.AppendLine(
            "## Interpretation");

        builder.AppendLine();

        builder.AppendLine(
            "The approximately five-second stakeholder latency target is " +
            "evaluated **per label item**. It is not applied to the total " +
            "wall-clock duration of a multi-label batch.");

        builder.AppendLine();

        builder.AppendLine(
            "Batch throughput is reported separately in labels per minute. " +
            "The benchmark does not claim that an entire high-volume batch " +
            "completes within five seconds.");

        builder.AppendLine();

        builder.AppendLine(
            "Technical `ERROR` outcomes remain separate from regulatory " +
            "`PASS`, `REVIEW`, and `FAIL` outcomes.");

        builder.AppendLine();

        builder.AppendLine(
            "## Benchmark Environment");

        builder.AppendLine();

        builder.AppendLine(
            $"- Benchmark location: {summary.Environment.BenchmarkLocation}");

        builder.AppendLine(
            $"- Runtime: {summary.Environment.Runtime}");

        builder.AppendLine(
            $"- .NET SDK: {summary.Environment.DotnetSdk}");

        builder.AppendLine(
            $"- Operating system: {summary.Environment.OperatingSystem}");

        builder.AppendLine(
            $"- Process architecture: {summary.Environment.Architecture}");

        builder.AppendLine(
            $"- Logical processors: {summary.Environment.LogicalProcessorCount}");

        builder.AppendLine(
            $"- Azure region: {summary.Environment.AzureRegion}");

        builder.AppendLine(
            $"- Document Intelligence SKU: {summary.Environment.DocumentIntelligenceSku}");

        builder.AppendLine(
            $"- OCR endpoint host: {summary.Environment.EndpointHost}");

        builder.AppendLine(
            $"- OCR model: {summary.Environment.ModelId}");

        builder.AppendLine(
            $"- OCR timeout: {summary.Environment.TimeoutSeconds} seconds");

        builder.AppendLine(
            $"- Authentication readiness timeout: " +
            $"{summary.Environment.AuthenticationTimeoutSeconds} seconds");

        builder.AppendLine(
            $"- Font styling enabled: {summary.Environment.EnableFontStyling}");

        builder.AppendLine(
            $"- Authentication: {summary.Environment.Authentication}");

        builder.AppendLine(
            $"- Batch size: {summary.Environment.BatchSize}");

        builder.AppendLine(
            $"- Measured batches: {summary.Environment.MeasuredIterations}");

        builder.AppendLine(
            $"- Max concurrency: {summary.Environment.MaxConcurrency}");

        builder.AppendLine(
            $"- Excluded warm-up batch size: {summary.Environment.WarmupBatchSize}");

        builder.AppendLine(
            $"- Representative fixture pool: {summary.Environment.FixtureCount} images");

        builder.AppendLine();

        builder.AppendLine(
            "## Methodology");

        builder.AppendLine();

        builder.AppendLine(
            "- The benchmark executes the real batch coordinator and the " +
            "existing single-label verification workflow.");

        builder.AppendLine(
            "- Azure Document Intelligence is exercised live through the " +
            "same Infrastructure implementation used by the prototype.");

        builder.AppendLine(
            "- Batch concurrency is bounded by the production " +
            "`BatchVerificationOptions.MaxConcurrency` setting.");

        builder.AppendLine(
            "- One smaller concurrent warm-up batch is executed and excluded " +
            "from formal measurements.");

        builder.AppendLine(
            "- Representative synthetic fixtures are repeated to construct " +
            "the configured batch size.");

        builder.AppendLine(
            "- Fixture starting position rotates between measured batches " +
            "to reduce ordering bias.");

        builder.AppendLine(
            "- Per-item durations come from the batch item workflow timing.");

        builder.AppendLine(
            "- Batch wall-clock timing is measured by the benchmark harness.");

        builder.AppendLine(
            "- P95 uses the nearest-rank percentile method.");

        builder.AppendLine(
            "- Browser upload/staging time, Blazor rendering, and user review " +
            "time are intentionally excluded from this service-throughput benchmark.");

        builder.AppendLine();

        builder.AppendLine(
            "No OCR text or image bytes are written to benchmark result files. " +
            "The recorded filenames refer only to synthetic repository fixtures.");

        return builder.ToString();
    }

    private static void ValidateFixtures(
        string labelDirectory,
        IEnumerable<string> fixturePool)
    {
        foreach (var fileName in fixturePool)
        {
            var path =
                Path.Combine(
                    labelDirectory,
                    fileName);

            if (!File.Exists(
                    path))
            {
                throw new FileNotFoundException(
                    $"Batch benchmark fixture was not found: {fileName}",
                    path);
            }
        }
    }

    private static string GetContentType(
        string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".png" =>
                "image/png",

            ".jpg" or ".jpeg" =>
                "image/jpeg",

            ".webp" =>
                "image/webp",

            _ =>
                "application/octet-stream"
        };
    }

    private static string FormatMilliseconds(
        double? value)
    {
        return value.HasValue
            ? $"{value.Value:N0} ms"
            : "n/a";
    }

    private static string Number(
        double value)
    {
        return value.ToString(
            "0.###",
            CultureInfo.InvariantCulture);
    }

    private static string Csv(
        string? value)
    {
        if (string.IsNullOrEmpty(
                value))
        {
            return string.Empty;
        }

        return
            "\"" +
            value.Replace(
                "\"",
                "\"\"",
                StringComparison.Ordinal) +
            "\"";
    }

    private static string RequireEnvironmentVariable(
        string name)
    {
        var value =
            Environment.GetEnvironmentVariable(
                name);

        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new InvalidOperationException(
                $"Required environment variable '{name}' is not configured.");
        }

        return value;
    }

    private static int GetPositiveIntegerEnvironmentVariable(
        string name,
        int defaultValue)
    {
        var value =
            Environment.GetEnvironmentVariable(
                name);

        return int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed) &&
            parsed > 0
                ? parsed
                : defaultValue;
    }

    private static bool GetBooleanEnvironmentVariable(
        string name,
        bool defaultValue)
    {
        var value =
            Environment.GetEnvironmentVariable(
                name);

        return bool.TryParse(
            value,
            out var parsed)
                ? parsed
                : defaultValue;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory =
            new(
                Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            if (File.Exists(
                    Path.Combine(
                        directory.FullName,
                        "LabelVerification.slnx")))
            {
                return directory.FullName;
            }

            directory =
                directory.Parent;
        }

        throw new InvalidOperationException(
            "Unable to locate repository root containing " +
            "LabelVerification.slnx.");
    }

    private static string GetCommandOutput(
        string fileName,
        string arguments)
    {
        try
        {
            using var process =
                new Process
                {
                    StartInfo =
                        new ProcessStartInfo
                        {
                            FileName =
                                fileName,

                            Arguments =
                                arguments,

                            RedirectStandardOutput =
                                true,

                            RedirectStandardError =
                                true,

                            UseShellExecute =
                                false,

                            CreateNoWindow =
                                true
                        }
                };

            process.Start();

            var output =
                process.StandardOutput
                    .ReadToEnd();

            process.WaitForExit();

            return process.ExitCode == 0
                ? output.Trim()
                : "(unavailable)";
        }
        catch
        {
            return "(unavailable)";
        }
    }
}

internal sealed record BatchBenchmarkItemObservation
{
    public required int Iteration { get; init; }

    public required string ItemId { get; init; }

    public required string FileName { get; init; }

    public required string ProcessingStatus { get; init; }

    public string? RegulatoryStatus { get; init; }

    public string? ErrorCode { get; init; }

    public required double DurationMilliseconds { get; init; }

    public required bool WithinFiveSecondTarget { get; init; }

    public string? VerificationCorrelationId { get; init; }
}

internal sealed record BatchBenchmarkRunObservation
{
    public required int Iteration { get; init; }

    public required int RequestedCount { get; init; }

    public required int ReturnedCount { get; init; }

    public required int RegulatoryCompletionCount { get; init; }

    public required int PassCount { get; init; }

    public required int ReviewCount { get; init; }

    public required int FailCount { get; init; }

    public required int ErrorCount { get; init; }

    public required double BatchElapsedMilliseconds { get; init; }

    public required double LabelsPerMinute { get; init; }

    public required int ItemsWithinFiveSecondTarget { get; init; }

    public string? BatchCorrelationId { get; init; }

    public string? BatchFailure { get; init; }

    public required BatchMetricSummary ItemDuration { get; init; }

    public required IReadOnlyList<BatchBenchmarkItemObservation>
        Items
    { get; init; }
}

internal sealed record BatchMetricSummary
{
    public int Count { get; init; }

    public double? MedianMilliseconds { get; init; }

    public double? P95Milliseconds { get; init; }

    public double? WorstMilliseconds { get; init; }
}

internal sealed record BatchRateSummary
{
    public int Count { get; init; }

    public double Median { get; init; }

    public double P95 { get; init; }

    public double Best { get; init; }
}

internal sealed record BatchBenchmarkEnvironment
{
    public required DateTimeOffset GeneratedUtc { get; init; }

    public required string GitSha { get; init; }

    public required string DotnetSdk { get; init; }

    public required string Runtime { get; init; }

    public required string OperatingSystem { get; init; }

    public required string Architecture { get; init; }

    public required int LogicalProcessorCount { get; init; }

    public required string BenchmarkLocation { get; init; }

    public required string AzureRegion { get; init; }

    public required string DocumentIntelligenceSku { get; init; }

    public required string EndpointHost { get; init; }

    public required string ModelId { get; init; }

    public required int TimeoutSeconds { get; init; }

    public required int AuthenticationTimeoutSeconds { get; init; }

    public required bool EnableFontStyling { get; init; }

    public required string Authentication { get; init; }

    public required int BatchSize { get; init; }

    public required int MeasuredIterations { get; init; }

    public required int MaxConcurrency { get; init; }

    public required int WarmupBatchSize { get; init; }

    public required int FixtureCount { get; init; }
}

internal sealed record BatchBenchmarkSummary
{
    public required BatchBenchmarkEnvironment Environment { get; init; }

    public required int MeasuredBatchCount { get; init; }

    public required int RequestedLabelCount { get; init; }

    public required int ReturnedLabelCount { get; init; }

    public required int RegulatoryCompletionCount { get; init; }

    public required int PassCount { get; init; }

    public required int ReviewCount { get; init; }

    public required int FailCount { get; init; }

    public required int ErrorCount { get; init; }

    public required int BatchFailureCount { get; init; }

    public required int ItemsWithinFiveSecondTarget { get; init; }

    public required double ItemTargetRatePercent { get; init; }

    public required BatchMetricSummary BatchElapsed { get; init; }

    public required BatchMetricSummary ItemDuration { get; init; }

    public required BatchRateSummary LabelsPerMinute { get; init; }

    public required IReadOnlyList<BatchBenchmarkRunObservation>
        Runs
    { get; init; }
}