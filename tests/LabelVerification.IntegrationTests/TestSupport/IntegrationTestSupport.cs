using LabelVerification.Application;
using LabelVerification.Application.Abstractions;
using LabelVerification.Application.LabelUnderstanding;
using LabelVerification.Infrastructure.ApplicationRecords;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using LabelVerification.Application.Verification.Workflow;

namespace LabelVerification.IntegrationTests.TestSupport;

/// <summary>
/// Provides deterministic integration-test data and composition helpers.
///
/// External OCR is deliberately replaced with a controlled provider for the
/// normal integration suite. The existing LiveOcr test remains responsible
/// for exercising Azure Document Intelligence.
/// </summary>
internal static class IntegrationTestSupport
{
    internal const string ApplicationId =
        "COLA-84729";

    internal static string RepositoryRoot =>
        FindRepositoryRoot();

    internal static string ApplicationDataDirectory =>
        Path.Combine(
            RepositoryRoot,
            "sample-data",
            "applications");

    internal static string CompliantLabelPath =>
        Path.Combine(
            RepositoryRoot,
            "sample-data",
            "labels",
            "verification",
            "compliant-label.png");

    /// <summary>
    /// Creates OCR evidence representing the compliant synthetic fixture.
    ///
    /// The text intentionally mirrors the expected application record while
    /// preserving a bold Government Warning heading so the real warning
    /// verifier can execute without weakening production rules.
    /// </summary>
    internal static OcrResult CreateCompliantOcrResult()
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
                "OLD TOM DISTILLERY",
                "KENTUCKY STRAIGHT BOURBON WHISKEY",
                "45% ALCOHOL BY VOLUME",
                "90 PROOF",
                "750 mL",
                "BOTTLED BY OLD TOM DISTILLERY",
                "FRANKFORT, KENTUCKY",
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
                    value =>
                        new OcrTextLine
                        {
                            Text = value
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
                            Text = word,
                            Confidence = 0.99
                        })
                .ToArray();

        var headingOffset =
            text.IndexOf(
                warningHeading,
                StringComparison.Ordinal);

        return new OcrResult
        {
            Text = text,
            Lines = lines,
            Words = words,
            Confidence = 0.99,
            Duration = TimeSpan.FromMilliseconds(25),
            Provider = "DeterministicIntegrationOcr",
            ModelVersion = "integration-fixture-v1",

            Styles =
            [
                new OcrTextStyle
                {
                    Offset = headingOffset,
                    Length = warningHeading.Length,
                    Text = warningHeading,
                    FontWeight = OcrFontWeight.Bold,
                    Confidence = 0.99
                }
            ]
        };
    }

    /// <summary>
    /// Builds the real Application-layer verification graph while substituting
    /// only the upstream application adapter and OCR boundary with controlled
    /// integration implementations.
    /// </summary>
    internal static ServiceProvider BuildServiceProvider(
        ILabelTextExtractor textExtractor,
        ILogger<LabelVerificationService>? logger = null)
    {
        var services =
            new ServiceCollection();

        // Register the standard ILogger<T> infrastructure so all application
        // services can participate in structured logging during composition tests.
        services.AddLogging();

        services.AddApplication();

        // The production Web host supplies the real logging infrastructure.
        // Integration tests use a null logger unless a test explicitly needs
        // to capture structured telemetry.
        services.AddSingleton<ILogger<LabelVerificationService>>(
            logger ??
            NullLogger<LabelVerificationService>.Instance);

        services.AddSingleton<IApplicationRecordProvider>(
            _ =>
                new JsonApplicationRecordProvider(
                    ApplicationDataDirectory));

        services.AddSingleton<ILabelTextExtractor>(
            textExtractor);

        return services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
    }

    private static string FindRepositoryRoot()
    {
        var directory =
            new DirectoryInfo(
                AppContext.BaseDirectory);

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
}

/// <summary>
/// Controlled OCR boundary used by deterministic integration tests.
/// </summary>
internal sealed class ControlledLabelTextExtractor
    : ILabelTextExtractor
{
    private readonly Func<
        Stream,
        CancellationToken,
        Task<OcrResult>> _handler;

    internal ControlledLabelTextExtractor(
        OcrResult result)
        : this(
            (_, _) =>
                Task.FromResult(result))
    {
    }

    internal ControlledLabelTextExtractor(
        Func<
            Stream,
            CancellationToken,
            Task<OcrResult>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        _handler =
            handler;
    }

    internal int CallCount { get; private set; }

    public Task<OcrResult> ExtractAsync(
        Stream image,
        CancellationToken cancellationToken = default)
    {
        CallCount++;

        return _handler(
            image,
            cancellationToken);
    }
}