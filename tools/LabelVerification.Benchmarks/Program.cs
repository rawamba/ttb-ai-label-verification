using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using LabelVerification.Application;
using LabelVerification.Application.LabelUnderstanding;
using LabelVerification.Application.Verification.Workflow;
using LabelVerification.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

const string ApplicationId =
    "COLA-84729";

const double FiveSecondTargetMilliseconds =
    5000.0;

const int WarmupPasses =
    1;

const int MeasuredIterations =
    10;

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

var enableFontStyling =
    GetBooleanEnvironmentVariable(
        "DocumentIntelligence__EnableFontStyling",
        true);

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

var benchmarkFiles =
    new[]
    {
        "compliant-label.png",
        "brand-variation-label.png",
        "rotated-label.png",
        "degraded-label.jpg",
        "compliant-with-glare.jpg"
    };

foreach (var fileName in benchmarkFiles)
{
    var path =
        Path.Combine(
            labelDirectory,
            fileName);

    if (!File.Exists(path))
    {
        throw new FileNotFoundException(
            $"Benchmark fixture was not found: {fileName}",
            path);
    }
}

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

configuration["DocumentIntelligence:EnableFontStyling"] =
    enableFontStyling.ToString(
        CultureInfo.InvariantCulture);

var services =
    new ServiceCollection();

services.AddApplication();

services.AddInfrastructure(
    configuration);

// The benchmark exercises the production workflow logging path without
// emitting routine verification logs to the console or benchmark files.
services.AddSingleton<ILogger<LabelVerificationService>>(
    NullLogger<LabelVerificationService>.Instance);

using var serviceProvider =
    services.BuildServiceProvider(
        new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

var verifier =
    serviceProvider.GetRequiredService<
        ILabelVerificationService>();

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
    "===== TTB LABEL VERIFICATION BENCHMARK =====");

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
    $"Warm-up attempts:    {benchmarkFiles.Length * WarmupPasses}");

Console.WriteLine(
    $"Measured attempts:   {benchmarkFiles.Length * MeasuredIterations}");

Console.WriteLine();

//
// Excluded warm-up phase.
//
// These calls deliberately exercise first-use behavior but are not included
// in the formal warm-state latency distribution.
//
Console.WriteLine(
    "===== EXCLUDED WARM-UP PHASE =====");

for (var warmupPass = 1;
     warmupPass <= WarmupPasses;
     warmupPass++)
{
    foreach (var fileName in benchmarkFiles)
    {
        var observation =
            await RunOnceAsync(
                verifier,
                labelDirectory,
                fileName,
                iteration: 0,
                sequence: 0,
                position: 0);

        Console.WriteLine(
            $"WARMUP {fileName,-30} " +
            DescribeObservation(
                observation));
    }
}

Console.WriteLine();
Console.WriteLine(
    "Warm-up observations are excluded from formal statistics.");
Console.WriteLine();

//
// Formal measured phase.
//
// The starting fixture rotates each iteration. With five fixtures and ten
// iterations, every image appears in every ordinal position twice. This
// reduces bias from residual ordering or service-state effects.
//
Console.WriteLine(
    "===== MEASURED WARM-STATE PHASE =====");

var observations =
    new List<BenchmarkObservation>();

var sequence = 0;

for (var iteration = 1;
     iteration <= MeasuredIterations;
     iteration++)
{
    var rotation =
        (iteration - 1) %
        benchmarkFiles.Length;

    var iterationFiles =
        benchmarkFiles
            .Skip(rotation)
            .Concat(
                benchmarkFiles.Take(rotation))
            .ToArray();

    for (var position = 0;
         position < iterationFiles.Length;
         position++)
    {
        sequence++;

        var fileName =
            iterationFiles[position];

        var observation =
            await RunOnceAsync(
                verifier,
                labelDirectory,
                fileName,
                iteration,
                sequence,
                position + 1);

        observations.Add(
            observation);

        Console.WriteLine(
            $"[{sequence,2}/{benchmarkFiles.Length * MeasuredIterations}] " +
            $"iter={iteration,2} " +
            $"pos={position + 1} " +
            $"{fileName,-30} " +
            DescribeObservation(
                observation));
    }
}

var environment =
    new BenchmarkEnvironment
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
            RuntimeInformation.ProcessArchitecture.ToString(),

        LogicalProcessorCount =
            Environment.ProcessorCount,

        BenchmarkLocation =
            benchmarkLocation,

        AzureRegion =
            azureRegion,

        DocumentIntelligenceSku =
            documentIntelligenceSku,

        EndpointHost =
            new Uri(endpoint).Host,

        ModelId =
            modelId,

        TimeoutSeconds =
            timeoutSeconds,

        EnableFontStyling =
            enableFontStyling,

        Authentication =
            "DefaultAzureCredential",

        WarmupObservationCount =
            benchmarkFiles.Length *
            WarmupPasses,

        MeasuredIterations =
            MeasuredIterations,

        MeasuredObservationCount =
            observations.Count
    };

var summary =
    new BenchmarkSummary
    {
        Environment =
            environment,

        Overall =
            BuildGroupSummary(
                "Overall",
                observations),

        ByImage =
            observations
                .GroupBy(
                    observation =>
                        observation.FileName,
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(
                    group =>
                        group.Key,
                    StringComparer.OrdinalIgnoreCase)
                .Select(
                    group =>
                        BuildGroupSummary(
                            group.Key,
                            group))
                .ToArray()
    };

Directory.CreateDirectory(
    outputDirectory);

var utf8NoBom =
    new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false);

var csvPath =
    Path.Combine(
        outputDirectory,
        "warm-results.csv");

var jsonPath =
    Path.Combine(
        outputDirectory,
        "warm-summary.json");

var markdownPath =
    Path.Combine(
        outputDirectory,
        "warm-summary.md");

File.WriteAllText(
    csvPath,
    BuildCsv(
        observations),
    utf8NoBom);

File.WriteAllText(
    jsonPath,
    JsonSerializer.Serialize(
        summary,
        new JsonSerializerOptions
        {
            WriteIndented = true
        }),
    utf8NoBom);

File.WriteAllText(
    markdownPath,
    BuildMarkdown(
        summary),
    utf8NoBom);

Console.WriteLine();
Console.WriteLine(
    "===== FORMAL BENCHMARK SUMMARY =====");

WriteGroupToConsole(
    summary.Overall);

Console.WriteLine();
Console.WriteLine(
    $"Raw observations: {csvPath}");

Console.WriteLine(
    $"JSON summary:     {jsonPath}");

Console.WriteLine(
    $"Markdown summary: {markdownPath}");

Console.WriteLine();

return observations.Count ==
       benchmarkFiles.Length *
       MeasuredIterations
    ? 0
    : 2;

async Task<BenchmarkObservation> RunOnceAsync(
    ILabelVerificationService verificationService,
    string labelsDirectory,
    string fileName,
    int iteration,
    int sequence,
    int position)
{
    var path =
        Path.Combine(
            labelsDirectory,
            fileName);

    var fileInfo =
        new FileInfo(
            path);

    var contentType =
        string.Equals(
            fileInfo.Extension,
            ".png",
            StringComparison.OrdinalIgnoreCase)
                ? "image/png"
                : "image/jpeg";

    var attemptStopwatch =
        Stopwatch.StartNew();

    try
    {
        await using var stream =
            File.OpenRead(
                path);

        var result =
            await verificationService.VerifyAsync(
                ApplicationId,
                stream,
                fileName,
                contentType,
                fileInfo.Length,
                CancellationToken.None);

        attemptStopwatch.Stop();

        var telemetry =
            result.Telemetry;

        var resultCategory =
            result.Verification is not null
                ? result.Verification.OverallStatus
                    .ToString()
                    .ToUpperInvariant()
                : "PROCESSING_FAILURE";

        var observedAttemptMilliseconds =
            telemetry?.TotalDuration.TotalMilliseconds
            ?? attemptStopwatch.Elapsed.TotalMilliseconds;

        return new BenchmarkObservation
        {
            Sequence =
                sequence,

            Iteration =
                iteration,

            Position =
                position,

            FileName =
                fileName,

            ProcessingSucceeded =
                result.ProcessingSucceeded,

            Outcome =
                result.ProcessingSucceeded
                    ? "COMPLETED"
                    : "PROCESSING_FAILURE",

            ResultCategory =
                resultCategory,

            ErrorCode =
                result.ErrorCode,

            ObservedAttemptMilliseconds =
                observedAttemptMilliseconds,

            TotalMilliseconds =
                telemetry?.TotalDuration.TotalMilliseconds,

            OcrMilliseconds =
                telemetry?.OcrDuration?.TotalMilliseconds,

            VerificationMilliseconds =
                telemetry?.VerificationDuration?.TotalMilliseconds,

            HarnessElapsedMilliseconds =
                attemptStopwatch.Elapsed.TotalMilliseconds,

            WithinFiveSecondTarget =
                result.ProcessingSucceeded &&
                observedAttemptMilliseconds <=
                    FiveSecondTargetMilliseconds,

            CorrelationId =
                telemetry?.CorrelationId
        };
    }
    catch (LabelTextExtractionException exception)
    {
        attemptStopwatch.Stop();

        var failureCategory =
            exception.InnerException switch
            {
                OperationCanceledException =>
                    "OCR_TIMEOUT",

                _ when string.Equals(
                    exception.InnerException?.GetType().Name,
                    "RequestFailedException",
                    StringComparison.Ordinal) =>
                    "AZURE_REQUEST_FAILED",

                _ =>
                    "OCR_FAILURE"
            };

        return new BenchmarkObservation
        {
            Sequence =
                sequence,

            Iteration =
                iteration,

            Position =
                position,

            FileName =
                fileName,

            ProcessingSucceeded =
                false,

            Outcome =
                "EXCEPTION",

            ResultCategory =
                failureCategory,

            ErrorCode =
                failureCategory,

            ObservedAttemptMilliseconds =
                attemptStopwatch.Elapsed.TotalMilliseconds,

            TotalMilliseconds =
                null,

            OcrMilliseconds =
                null,

            VerificationMilliseconds =
                null,

            HarnessElapsedMilliseconds =
                attemptStopwatch.Elapsed.TotalMilliseconds,

            WithinFiveSecondTarget =
                false,

            CorrelationId =
                null
        };
    }
    catch (Exception exception)
    {
        attemptStopwatch.Stop();

        return new BenchmarkObservation
        {
            Sequence =
                sequence,

            Iteration =
                iteration,

            Position =
                position,

            FileName =
                fileName,

            ProcessingSucceeded =
                false,

            Outcome =
                "UNEXPECTED_EXCEPTION",

            ResultCategory =
                "UNEXPECTED_EXCEPTION",

            ErrorCode =
                exception.GetType().Name,

            ObservedAttemptMilliseconds =
                attemptStopwatch.Elapsed.TotalMilliseconds,

            TotalMilliseconds =
                null,

            OcrMilliseconds =
                null,

            VerificationMilliseconds =
                null,

            HarnessElapsedMilliseconds =
                attemptStopwatch.Elapsed.TotalMilliseconds,

            WithinFiveSecondTarget =
                false,

            CorrelationId =
                null
        };
    }
}

string DescribeObservation(
    BenchmarkObservation observation)
{
    var target =
        observation.WithinFiveSecondTarget
            ? "TARGET=PASS"
            : "TARGET=MISS";

    return
        $"{observation.ResultCategory,-18} " +
        $"observed={observation.ObservedAttemptMilliseconds,7:N0} ms  " +
        $"{target}";
}

BenchmarkGroupSummary BuildGroupSummary(
    string name,
    IEnumerable<BenchmarkObservation> source)
{
    var group =
        source.ToArray();

    var successful =
        group
            .Where(
                observation =>
                    observation.ProcessingSucceeded)
            .ToArray();

    var targetMet =
        group.Count(
            observation =>
                observation.WithinFiveSecondTarget);

    var timeoutCount =
        group.Count(
            observation =>
                string.Equals(
                    observation.ResultCategory,
                    "OCR_TIMEOUT",
                    StringComparison.Ordinal));

    return new BenchmarkGroupSummary
    {
        Name =
            name,

        ObservationCount =
            group.Length,

        SuccessCount =
            successful.Length,

        FailureCount =
            group.Length -
            successful.Length,

        OcrTimeoutCount =
            timeoutCount,

        TargetMetCount =
            targetMet,

        TargetRatePercent =
            group.Length == 0
                ? 0.0
                : 100.0 *
                  targetMet /
                  group.Length,

        ObservedAttempt =
            BuildMetric(
                group.Select(
                    observation =>
                        (double?)observation
                            .ObservedAttemptMilliseconds)),

        Total =
            BuildMetric(
                successful.Select(
                    observation =>
                        observation.TotalMilliseconds)),

        Ocr =
            BuildMetric(
                successful.Select(
                    observation =>
                        observation.OcrMilliseconds)),

        Verification =
            BuildMetric(
                successful.Select(
                    observation =>
                        observation.VerificationMilliseconds)),

        HarnessElapsed =
            BuildMetric(
                group.Select(
                    observation =>
                        (double?)observation
                            .HarnessElapsedMilliseconds))
    };
}

MetricSummary BuildMetric(
    IEnumerable<double?> source)
{
    var values =
        source
            .Where(
                value =>
                    value.HasValue)
            .Select(
                value =>
                    value!.Value)
            .OrderBy(
                value =>
                    value)
            .ToArray();

    if (values.Length == 0)
    {
        return new MetricSummary();
    }

    var median =
        values.Length % 2 == 1
            ? values[values.Length / 2]
            : (
                values[(values.Length / 2) - 1] +
                values[values.Length / 2]
              ) / 2.0;

    // Nearest-rank p95.
    var p95Rank =
        (int)Math.Ceiling(
            0.95 *
            values.Length);

    var p95Index =
        Math.Clamp(
            p95Rank - 1,
            0,
            values.Length - 1);

    return new MetricSummary
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

string BuildCsv(
    IEnumerable<BenchmarkObservation> source)
{
    var builder =
        new StringBuilder();

    builder.AppendLine(
        "Sequence,Iteration,Position,FileName,ProcessingSucceeded," +
        "Outcome,ResultCategory,ErrorCode,ObservedAttemptMs,TotalMs," +
        "OcrMs,VerificationMs,HarnessElapsedMs,WithinFiveSecondTarget," +
        "CorrelationId");

    foreach (var observation in source)
    {
        builder.Append(
            observation.Sequence.ToString(
                CultureInfo.InvariantCulture));

        builder.Append(',');

        builder.Append(
            observation.Iteration.ToString(
                CultureInfo.InvariantCulture));

        builder.Append(',');

        builder.Append(
            observation.Position.ToString(
                CultureInfo.InvariantCulture));

        builder.Append(',');

        builder.Append(
            Csv(
                observation.FileName));

        builder.Append(',');

        builder.Append(
            observation.ProcessingSucceeded);

        builder.Append(',');

        builder.Append(
            Csv(
                observation.Outcome));

        builder.Append(',');

        builder.Append(
            Csv(
                observation.ResultCategory));

        builder.Append(',');

        builder.Append(
            Csv(
                observation.ErrorCode));

        builder.Append(',');

        builder.Append(
            Number(
                observation.ObservedAttemptMilliseconds));

        builder.Append(',');

        builder.Append(
            NullableNumber(
                observation.TotalMilliseconds));

        builder.Append(',');

        builder.Append(
            NullableNumber(
                observation.OcrMilliseconds));

        builder.Append(',');

        builder.Append(
            NullableNumber(
                observation.VerificationMilliseconds));

        builder.Append(',');

        builder.Append(
            Number(
                observation.HarnessElapsedMilliseconds));

        builder.Append(',');

        builder.Append(
            observation.WithinFiveSecondTarget);

        builder.Append(',');

        builder.Append(
            Csv(
                observation.CorrelationId));

        builder.AppendLine();
    }

    return builder.ToString();
}

string BuildMarkdown(
    BenchmarkSummary benchmark)
{
    var builder =
        new StringBuilder();

    builder.AppendLine(
        "# Prototype Performance Benchmark");

    builder.AppendLine();

    builder.AppendLine(
        $"Generated UTC: `{benchmark.Environment.GeneratedUtc:O}`");

    builder.AppendLine();

    builder.AppendLine(
        $"Git SHA: `{benchmark.Environment.GitSha}`");

    builder.AppendLine();

    builder.AppendLine(
        $"Measured warm-state observations: **{benchmark.Environment.MeasuredObservationCount}**");

    builder.AppendLine();

    builder.AppendLine(
        $"Excluded warm-up observations: **{benchmark.Environment.WarmupObservationCount}**");

    builder.AppendLine();

    builder.AppendLine(
        "The stakeholder's approximately five-second target is evaluated " +
        "against observed attempt latency. Completed workflows use the " +
        "Application-layer `TotalDuration`; attempts that terminate before " +
        "telemetry is returned use benchmark-harness elapsed time.");

    builder.AppendLine();

    builder.AppendLine(
        "Timeouts and processing failures are retained as target misses rather " +
        "than being removed from the performance distribution.");

    builder.AppendLine();

    builder.AppendLine(
        "| Dataset | N | Success | OCR Timeouts | <=5s | Median Observed | P95 Observed | Worst Observed | Median OCR | P95 OCR | Median Verification |");

    builder.AppendLine(
        "|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");

    AppendMarkdownRow(
        builder,
        benchmark.Overall);

    foreach (var group in benchmark.ByImage)
    {
        AppendMarkdownRow(
            builder,
            group);
    }

    builder.AppendLine();

    builder.AppendLine(
        "## Benchmark Environment");

    builder.AppendLine();

    builder.AppendLine(
        $"- Benchmark location: {benchmark.Environment.BenchmarkLocation}");

    builder.AppendLine(
        $"- Runtime: {benchmark.Environment.Runtime}");

    builder.AppendLine(
        $"- .NET SDK: {benchmark.Environment.DotnetSdk}");

    builder.AppendLine(
        $"- Operating system: {benchmark.Environment.OperatingSystem}");

    builder.AppendLine(
        $"- Process architecture: {benchmark.Environment.Architecture}");

    builder.AppendLine(
        $"- Logical processors visible to process: {benchmark.Environment.LogicalProcessorCount}");

    builder.AppendLine(
        $"- Azure region: {benchmark.Environment.AzureRegion}");

    builder.AppendLine(
        $"- Document Intelligence SKU: {benchmark.Environment.DocumentIntelligenceSku}");

    builder.AppendLine(
        $"- OCR endpoint host: {benchmark.Environment.EndpointHost}");

    builder.AppendLine(
        $"- OCR model: {benchmark.Environment.ModelId}");

    builder.AppendLine(
        $"- OCR timeout: {benchmark.Environment.TimeoutSeconds} seconds");

    builder.AppendLine(
        $"- Font styling enabled: {benchmark.Environment.EnableFontStyling}");

    builder.AppendLine(
        $"- Authentication: {benchmark.Environment.Authentication}");

    builder.AppendLine();

    builder.AppendLine(
        "## Methodology");

    builder.AppendLine();

    builder.AppendLine(
        "- Five representative synthetic label fixtures were benchmarked.");

    builder.AppendLine(
        "- One full five-image warm-up pass was executed and excluded from formal statistics.");

    builder.AppendLine(
        "- Ten measured iterations produced 50 formal observations.");

    builder.AppendLine(
        "- Fixture starting position rotated each iteration to reduce ordering bias.");

    builder.AppendLine(
        "- The OCR timeout remained fixed at five seconds.");

    builder.AppendLine(
        "- P95 uses the nearest-rank percentile method.");

    builder.AppendLine(
        "- OCR and verification stage metrics are calculated only for completed verification workflows.");

    builder.AppendLine(
        "- Observed-attempt metrics retain timeout and failure latency.");

    builder.AppendLine();

    builder.AppendLine(
        "## Timing Boundaries");

    builder.AppendLine();

    builder.AppendLine(
        "- `ObservedAttempt`: complete benchmark-observed attempt latency and the primary five-second-target metric.");

    builder.AppendLine(
        "- `TotalDuration`: Application-layer workflow duration for completed workflows.");

    builder.AppendLine(
        "- `OcrDuration`: OCR abstraction latency measured at the Application workflow boundary.");

    builder.AppendLine(
        "- `VerificationDuration`: parsing, deterministic comparison, and aggregation after OCR.");

    builder.AppendLine(
        "- Browser rendering and Internet transport to the deployed Blazor UI are not included in these measurements.");

    builder.AppendLine();

    builder.AppendLine(
        "No OCR text, image bytes, or extracted label field values are written to benchmark result files.");

    return builder.ToString();
}

void AppendMarkdownRow(
    StringBuilder builder,
    BenchmarkGroupSummary group)
{
    builder.Append("| ");
    builder.Append(group.Name);
    builder.Append(" | ");
    builder.Append(group.ObservationCount);
    builder.Append(" | ");
    builder.Append(group.SuccessCount);
    builder.Append(" | ");
    builder.Append(group.OcrTimeoutCount);
    builder.Append(" | ");
    builder.Append(
        $"{group.TargetMetCount}/{group.ObservationCount} " +
        $"({group.TargetRatePercent:0.#}%)");
    builder.Append(" | ");
    builder.Append(
        FormatMilliseconds(
            group.ObservedAttempt.MedianMilliseconds));
    builder.Append(" | ");
    builder.Append(
        FormatMilliseconds(
            group.ObservedAttempt.P95Milliseconds));
    builder.Append(" | ");
    builder.Append(
        FormatMilliseconds(
            group.ObservedAttempt.WorstMilliseconds));
    builder.Append(" | ");
    builder.Append(
        FormatMilliseconds(
            group.Ocr.MedianMilliseconds));
    builder.Append(" | ");
    builder.Append(
        FormatMilliseconds(
            group.Ocr.P95Milliseconds));
    builder.Append(" | ");
    builder.Append(
        FormatMilliseconds(
            group.Verification.MedianMilliseconds));
    builder.AppendLine(" |");
}

void WriteGroupToConsole(
    BenchmarkGroupSummary group)
{
    Console.WriteLine(
        $"Measured observations:  {group.ObservationCount}");

    Console.WriteLine(
        $"Successful workflows:   {group.SuccessCount}");

    Console.WriteLine(
        $"Failed attempts:        {group.FailureCount}");

    Console.WriteLine(
        $"OCR timeouts:           {group.OcrTimeoutCount}");

    Console.WriteLine(
        $"<= 5 second target:     {group.TargetMetCount}/{group.ObservationCount} " +
        $"({group.TargetRatePercent:0.#}%)");

    Console.WriteLine(
        $"Median observed:        {FormatMilliseconds(group.ObservedAttempt.MedianMilliseconds)}");

    Console.WriteLine(
        $"P95 observed:           {FormatMilliseconds(group.ObservedAttempt.P95Milliseconds)}");

    Console.WriteLine(
        $"Worst observed:         {FormatMilliseconds(group.ObservedAttempt.WorstMilliseconds)}");

    Console.WriteLine(
        $"Median OCR:             {FormatMilliseconds(group.Ocr.MedianMilliseconds)}");

    Console.WriteLine(
        $"P95 OCR:                {FormatMilliseconds(group.Ocr.P95Milliseconds)}");

    Console.WriteLine(
        $"Worst OCR:              {FormatMilliseconds(group.Ocr.WorstMilliseconds)}");

    Console.WriteLine(
        $"Median verification:    {FormatMilliseconds(group.Verification.MedianMilliseconds)}");

    Console.WriteLine(
        $"P95 verification:       {FormatMilliseconds(group.Verification.P95Milliseconds)}");
}

string FormatMilliseconds(
    double? value) =>
    value.HasValue
        ? $"{value.Value:N0} ms"
        : "n/a";

string Number(
    double value) =>
    value.ToString(
        "0.###",
        CultureInfo.InvariantCulture);

string NullableNumber(
    double? value) =>
    value.HasValue
        ? Number(
            value.Value)
        : string.Empty;

string Csv(
    string? value)
{
    if (string.IsNullOrEmpty(value))
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

string RequireEnvironmentVariable(
    string name)
{
    var value =
        Environment.GetEnvironmentVariable(
            name);

    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException(
            $"Required environment variable '{name}' is not configured.");
    }

    return value;
}

int GetPositiveIntegerEnvironmentVariable(
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

bool GetBooleanEnvironmentVariable(
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

string FindRepositoryRoot()
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
        "Unable to locate repository root containing LabelVerification.slnx.");
}

string GetCommandOutput(
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

internal sealed record BenchmarkObservation
{
    public required int Sequence { get; init; }
    public required int Iteration { get; init; }
    public required int Position { get; init; }
    public required string FileName { get; init; }
    public required bool ProcessingSucceeded { get; init; }
    public required string Outcome { get; init; }
    public required string ResultCategory { get; init; }
    public string? ErrorCode { get; init; }
    public required double ObservedAttemptMilliseconds { get; init; }
    public double? TotalMilliseconds { get; init; }
    public double? OcrMilliseconds { get; init; }
    public double? VerificationMilliseconds { get; init; }
    public required double HarnessElapsedMilliseconds { get; init; }
    public required bool WithinFiveSecondTarget { get; init; }
    public string? CorrelationId { get; init; }
}

internal sealed record MetricSummary
{
    public int Count { get; init; }
    public double? MedianMilliseconds { get; init; }
    public double? P95Milliseconds { get; init; }
    public double? WorstMilliseconds { get; init; }
}

internal sealed record BenchmarkGroupSummary
{
    public required string Name { get; init; }
    public required int ObservationCount { get; init; }
    public required int SuccessCount { get; init; }
    public required int FailureCount { get; init; }
    public required int OcrTimeoutCount { get; init; }
    public required int TargetMetCount { get; init; }
    public required double TargetRatePercent { get; init; }
    public required MetricSummary ObservedAttempt { get; init; }
    public required MetricSummary Total { get; init; }
    public required MetricSummary Ocr { get; init; }
    public required MetricSummary Verification { get; init; }
    public required MetricSummary HarnessElapsed { get; init; }
}

internal sealed record BenchmarkEnvironment
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
    public required bool EnableFontStyling { get; init; }
    public required string Authentication { get; init; }
    public required int WarmupObservationCount { get; init; }
    public required int MeasuredIterations { get; init; }
    public required int MeasuredObservationCount { get; init; }
}

internal sealed record BenchmarkSummary
{
    public required BenchmarkEnvironment Environment { get; init; }
    public required BenchmarkGroupSummary Overall { get; init; }
    public required IReadOnlyList<BenchmarkGroupSummary> ByImage { get; init; }
}