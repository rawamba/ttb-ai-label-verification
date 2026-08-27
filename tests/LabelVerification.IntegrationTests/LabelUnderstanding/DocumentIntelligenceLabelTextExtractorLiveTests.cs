using Azure.AI.DocumentIntelligence;
using Azure.Identity;
using LabelVerification.Application.LabelUnderstanding;
using LabelVerification.Infrastructure.LabelUnderstanding;
using Xunit.Abstractions;

namespace LabelVerification.IntegrationTests.LabelUnderstanding;

/// <summary>
/// Exercises the real Azure Document Intelligence OCR provider against a
/// representative alcohol-label image.
///
/// This test is intentionally opt-in because it depends on:
///
/// - a live Azure service;
/// - developer authentication;
/// - network access;
/// - RBAC configuration; and
/// - a representative image fixture.
///
/// Normal CI therefore remains deterministic and independent of external
/// AI-service availability.
/// </summary>
public sealed class DocumentIntelligenceLabelTextExtractorLiveTests
{
    private const string RunLiveTestsVariable =
        "RUN_LIVE_OCR_TESTS";

    private const string EndpointVariable =
        "DocumentIntelligence__Endpoint";

    private const string ModelVariable =
        "DocumentIntelligence__ModelId";

    private const string TimeoutVariable =
        "DocumentIntelligence__TimeoutSeconds";

    private const string AuthenticationTimeoutVariable =
        "DocumentIntelligence__AuthenticationTimeoutSeconds";

    private const string ImageVariable =
        "OCR_TEST_IMAGE";

    private const string ExpectedTermsVariable =
        "OCR_TEST_EXPECTED_TERMS";

    private readonly ITestOutputHelper _output;

    public DocumentIntelligenceLabelTextExtractorLiveTests(
        ITestOutputHelper output)
    {
        _output =
            output;
    }

    [Fact]
    [Trait(
        "Category",
        "LiveOcr")]
    public async Task ExtractAsync_WithRepresentativeLabel_ReturnsOcrEvidence()
    {
        // Live Azure evaluation is explicitly opt-in. Normal solution tests
        // return before making any external service call.
        if (!IsLiveTestEnabled())
        {
            _output.WriteLine(
                "Live OCR test not executed. " +
                $"Set {RunLiveTestsVariable}=true to enable it.");

            return;
        }

        //
        // Arrange.
        //
        var endpointValue =
            RequireEnvironmentVariable(
                EndpointVariable);

        var modelId =
            Environment.GetEnvironmentVariable(
                ModelVariable)
            ?? "prebuilt-read";

        var timeoutSeconds =
            GetPositiveEnvironmentValue(
                TimeoutVariable,
                defaultValue: 5);

        var authenticationTimeoutSeconds =
            GetPositiveEnvironmentValue(
                AuthenticationTimeoutVariable,
                defaultValue: 15);

        var imagePath =
            GetRepresentativeImagePath();

        Assert.True(
            File.Exists(
                imagePath),
            $"Representative OCR image was not found: {imagePath}");

        var options =
            new DocumentIntelligenceOptions
            {
                Endpoint =
                    new Uri(
                        endpointValue),

                ModelId =
                    modelId,

                Timeout =
                    TimeSpan.FromSeconds(
                        timeoutSeconds),

                AuthenticationTimeout =
                    TimeSpan.FromSeconds(
                        authenticationTimeoutSeconds),

                EnableFontStyling =
                    true
            };

        //
        // The live test uses AzureCliCredential as the local developer
        // credential because the test is intentionally workstation-oriented.
        //
        // CachingTokenCredential mirrors the production readiness pattern:
        // the extractor explicitly acquires a token before the five-second
        // OCR operation begins, and the Document Intelligence client then
        // reuses that token.
        //
        var credential =
            new CachingTokenCredential(
                new AzureCliCredential());

        var client =
            new DocumentIntelligenceClient(
                options.Endpoint,
                credential);

        var extractor =
            new DocumentIntelligenceLabelTextExtractor(
                client,
                options,
                credential);

        await using var imageStream =
            File.OpenRead(
                imagePath);

        //
        // Act.
        //
        var result =
            await extractor.ExtractAsync(
                imageStream,
                CancellationToken.None);

        //
        // Assert the provider-neutral OCR contract.
        //
        Assert.False(
            string.IsNullOrWhiteSpace(
                result.Text));

        Assert.NotEmpty(
            result.Lines);

        Assert.NotEmpty(
            result.Words);

        Assert.InRange(
            result.Confidence,
            0.0,
            1.0);

        Assert.True(
            result.Duration >
            TimeSpan.Zero);

        Assert.Equal(
            "AzureDocumentIntelligence",
            result.Provider);

        Assert.Equal(
            modelId,
            result.ModelVersion);

        ValidateExpectedTerms(
            result.Text);

        WriteResultSummary(
            imagePath,
            result);
    }

    /// <summary>
    /// Returns true only when the developer deliberately enables the live
    /// external Azure OCR test.
    /// </summary>
    private static bool IsLiveTestEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(
                RunLiveTestsVariable),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads a positive integer environment variable while retaining the
    /// documented fallback value when the variable is absent or invalid.
    /// </summary>
    private static int GetPositiveEnvironmentValue(
        string variableName,
        int defaultValue)
    {
        var configuredValue =
            Environment.GetEnvironmentVariable(
                variableName);

        return int.TryParse(
                   configuredValue,
                   out var value) &&
               value > 0
            ? value
            : defaultValue;
    }

    /// <summary>
    /// Uses OCR_TEST_IMAGE when supplied; otherwise resolves the repository's
    /// representative label fixture.
    /// </summary>
    private static string GetRepresentativeImagePath()
    {
        var configuredPath =
            Environment.GetEnvironmentVariable(
                ImageVariable);

        if (!string.IsNullOrWhiteSpace(
                configuredPath))
        {
            return Path.GetFullPath(
                configuredPath);
        }

        var repositoryRoot =
            FindRepositoryRoot();

        return Path.Combine(
            repositoryRoot,
            "sample-data",
            "labels",
            "representative-label.jpg");
    }

    /// <summary>
    /// Finds the repository root without depending on the working directory
    /// selected by dotnet test or Visual Studio Test Explorer.
    /// </summary>
    private static string FindRepositoryRoot()
    {
        var directory =
            new DirectoryInfo(
                AppContext.BaseDirectory);

        while (directory is not null)
        {
            var solutionPath =
                Path.Combine(
                    directory.FullName,
                    "LabelVerification.slnx");

            if (File.Exists(
                    solutionPath))
            {
                return directory.FullName;
            }

            directory =
                directory.Parent;
        }

        throw new InvalidOperationException(
            "Unable to locate the repository root containing " +
            "LabelVerification.slnx.");
    }

    /// <summary>
    /// Optionally validates known terms in the representative image.
    ///
    /// Terms are separated by semicolons so the live test can be reused with
    /// different representative fixtures.
    /// </summary>
    private static void ValidateExpectedTerms(
        string detectedText)
    {
        var configuredTerms =
            Environment.GetEnvironmentVariable(
                ExpectedTermsVariable);

        if (string.IsNullOrWhiteSpace(
                configuredTerms))
        {
            return;
        }

        var expectedTerms =
            configuredTerms.Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        foreach (var expectedTerm in expectedTerms)
        {
            Assert.True(
                detectedText.Contains(
                    expectedTerm,
                    StringComparison.OrdinalIgnoreCase),
                $"OCR output did not contain expected term " +
                $"'{expectedTerm}'.");
        }
    }

    /// <summary>
    /// Writes diagnostic evidence to xUnit output for deliberately executed
    /// live OCR runs.
    ///
    /// This diagnostic output is limited to the opt-in developer test and is
    /// not part of routine production telemetry.
    /// </summary>
    private void WriteResultSummary(
        string imagePath,
        OcrResult result)
    {
        _output.WriteLine(
            string.Empty);

        _output.WriteLine(
            "===== LIVE OCR RESULT =====");

        _output.WriteLine(
            $"Image:      {imagePath}");

        _output.WriteLine(
            $"Provider:   {result.Provider}");

        _output.WriteLine(
            $"Model:      {result.ModelVersion}");

        _output.WriteLine(
            $"Confidence: {result.Confidence:P2}");

        _output.WriteLine(
            $"Duration:   {result.Duration.TotalMilliseconds:N0} ms");

        _output.WriteLine(
            $"Lines:      {result.Lines.Count}");

        _output.WriteLine(
            $"Words:      {result.Words.Count}");

        _output.WriteLine(
            string.Empty);

        _output.WriteLine(
            "Detected text:");

        _output.WriteLine(
            "-------------------------");

        _output.WriteLine(
            result.Text);

        _output.WriteLine(
            "-------------------------");

        _output.WriteLine(
            string.Empty);

        _output.WriteLine(
            "Lowest-confidence detected words:");

        foreach (var word in
                 result.Words
                     .OrderBy(
                         word =>
                             word.Confidence)
                     .Take(
                         10))
        {
            _output.WriteLine(
                $"{word.Confidence:P2}  {word.Text}");
        }

        // Font-style evidence is not guaranteed for every provider response,
        // so an empty collection is a valid diagnostic result.
        _output.WriteLine(
            string.Empty);

        _output.WriteLine(
            "Detected font-weight evidence:");

        _output.WriteLine(
            "------------------------------");

        if (result.Styles.Count == 0)
        {
            _output.WriteLine(
                "No font-style evidence returned.");
        }
        else
        {
            foreach (var style in
                     result.Styles
                         .OrderBy(
                             style =>
                                 style.Offset))
            {
                _output.WriteLine(
                    $"{style.FontWeight,-7} " +
                    $"{style.Confidence,7:P2}  " +
                    $"{style.Text}");
            }
        }

        _output.WriteLine(
            "==========================");
    }

    /// <summary>
    /// Returns a required environment variable or fails with a clear setup
    /// message for the developer running the live external-service test.
    /// </summary>
    private static string RequireEnvironmentVariable(
        string variableName)
    {
        var value =
            Environment.GetEnvironmentVariable(
                variableName);

        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new InvalidOperationException(
                $"Required environment variable " +
                $"'{variableName}' is not configured.");
        }

        return value;
    }
}