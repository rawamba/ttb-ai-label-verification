using System.Diagnostics;
using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.Core;
using LabelVerification.Application.LabelUnderstanding;

namespace LabelVerification.Infrastructure.LabelUnderstanding;

/// <summary>
/// Extracts textual and optional typography evidence from alcohol-label
/// images using Azure Document Intelligence.
///
/// Azure-specific response types remain inside Infrastructure. The rest of
/// the application receives provider-neutral OCR evidence through
/// <see cref="OcrResult"/>.
///
/// Authentication readiness is intentionally established before the
/// latency-sensitive OCR timeout begins. Application-layer telemetry still
/// observes the complete extractor invocation, so startup cost remains visible
/// even though authentication no longer consumes the provider-operation
/// timeout.
/// </summary>
public sealed class DocumentIntelligenceLabelTextExtractor
    : ILabelTextExtractor
{
    private const string ProviderName =
        "AzureDocumentIntelligence";

    /// <summary>
    /// Azure Cognitive Services OAuth scope used by Document Intelligence.
    /// </summary>
    private static readonly string[] CognitiveServicesScopes =
    [
        "https://cognitiveservices.azure.com/.default"
    ];

    private readonly DocumentIntelligenceClient _client;

    private readonly DocumentIntelligenceOptions _options;

    private readonly TokenCredential _credential;

    /// <summary>
    /// Creates the production OCR provider using the same shared credential
    /// that was supplied to the Azure Document Intelligence client.
    /// </summary>
    public DocumentIntelligenceLabelTextExtractor(
        DocumentIntelligenceClient client,
        DocumentIntelligenceOptions options,
        TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(
            client);

        ArgumentNullException.ThrowIfNull(
            options);

        ArgumentNullException.ThrowIfNull(
            credential);

        _client =
            client;

        _options =
            options;

        _credential =
            credential;
    }

    /// <inheritdoc />
    public async Task<OcrResult> ExtractAsync(
        Stream image,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            image);

        //
        // Establish Azure authentication readiness first.
        //
        // This has a separate startup timeout because developer credential
        // discovery and Managed Identity initialization can be significantly
        // slower on the first request than on subsequent requests.
        //
        // The shared CachingTokenCredential makes the resulting token
        // immediately reusable by the Azure Document Intelligence client.
        //
        await EnsureAuthenticationReadyAsync(
            cancellationToken);

        //
        // Start the five-second latency-sensitive OCR operation only after
        // authentication is ready.
        //
        // Note that LabelVerificationService measures the entire call to this
        // extractor, so authentication startup time remains visible in
        // application-level OcrDuration and TotalDuration telemetry.
        //
        var stopwatch =
            Stopwatch.StartNew();

        using var timeoutCancellation =
            new CancellationTokenSource(
                _options.Timeout);

        using var linkedCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCancellation.Token);

        try
        {
            // Convert the already validated image stream into the binary
            // payload required by Azure Document Intelligence.
            BinaryData imageData =
                await BinaryData.FromStreamAsync(
                    image,
                    linkedCancellation.Token);

            // Use the strongly typed request object so optional provider
            // features can be configured without leaking Azure types into
            // Application-layer contracts.
            var analyzeOptions =
                new AnalyzeDocumentOptions(
                    _options.ModelId,
                    imageData);

            // Font styling is optional provider evidence. When enabled,
            // downstream deterministic rules can inspect evidence such as
            // whether the Government Warning heading appears bold.
            if (_options.EnableFontStyling)
            {
                analyzeOptions.Features.Add(
                    DocumentAnalysisFeature.FontStyling);
            }

            Operation<AnalyzeResult> operation =
                await _client.AnalyzeDocumentAsync(
                    WaitUntil.Completed,
                    analyzeOptions,
                    linkedCancellation.Token);

            AnalyzeResult result =
                operation.Value;

            stopwatch.Stop();

            var lines =
                result.Pages
                    .SelectMany(
                        page =>
                            page.Lines)
                    .Select(
                        line =>
                            new OcrTextLine
                            {
                                Text =
                                    line.Content
                            })
                    .ToArray();

            var words =
                result.Pages
                    .SelectMany(
                        page =>
                            page.Words)
                    .Select(
                        word =>
                            new OcrWord
                            {
                                Text =
                                    word.Content,

                                Confidence =
                                    word.Confidence
                            })
                    .ToArray();

            // Use a transparent arithmetic mean as the prototype-level
            // aggregate confidence. Individual word confidence remains
            // available for granular downstream review decisions.
            var aggregateConfidence =
                words.Length == 0
                    ? 0.0
                    : words.Average(
                        word =>
                            word.Confidence);

            return new OcrResult
            {
                Text =
                    result.Content
                    ?? string.Empty,

                Lines =
                    lines,

                Words =
                    words,

                Styles =
                    MapStyles(
                        result),

                Confidence =
                    aggregateConfidence,

                Duration =
                    stopwatch.Elapsed,

                Provider =
                    ProviderName,

                ModelVersion =
                    result.ModelId
            };
        }
        catch (OperationCanceledException exception)
            when (timeoutCancellation.IsCancellationRequested &&
                  !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            // Only the provider operation is governed by this five-second
            // timeout. Authentication readiness was handled separately.
            throw new LabelTextExtractionException(
                $"OCR operation exceeded the configured timeout of " +
                $"{_options.Timeout.TotalSeconds:0.#} seconds.",
                exception);
        }
        catch (RequestFailedException exception)
        {
            stopwatch.Stop();

            throw new LabelTextExtractionException(
                "Azure Document Intelligence failed to extract label evidence.",
                exception);
        }
    }

    /// <summary>
    /// Establishes access-token readiness before the latency-sensitive OCR
    /// operation begins.
    ///
    /// Concurrent first-use callers share the same caching credential, so only
    /// one underlying credential acquisition is required for the same Azure
    /// Cognitive Services scope.
    /// </summary>
    private async Task EnsureAuthenticationReadyAsync(
        CancellationToken cancellationToken)
    {
        using var authenticationTimeoutCancellation =
            new CancellationTokenSource(
                _options.AuthenticationTimeout);

        using var linkedCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                authenticationTimeoutCancellation.Token);

        try
        {
            var tokenRequestContext =
                new TokenRequestContext(
                    CognitiveServicesScopes);

            // The returned token does not need to be retained here.
            // CachingTokenCredential retains it for the subsequent Azure SDK
            // request made by DocumentIntelligenceClient.
            _ =
                await _credential.GetTokenAsync(
                    tokenRequestContext,
                    linkedCancellation.Token);
        }
        catch (OperationCanceledException exception)
            when (
                authenticationTimeoutCancellation
                    .IsCancellationRequested &&
                !cancellationToken.IsCancellationRequested)
        {
            throw new LabelTextExtractionException(
                "Azure authentication readiness exceeded the configured " +
                $"timeout of " +
                $"{_options.AuthenticationTimeout.TotalSeconds:0.#} seconds.",
                exception);
        }
        catch (OperationCanceledException)
        {
            // Preserve caller-requested cancellation so the batch coordinator
            // can stop promptly instead of converting cancellation into a
            // technical OCR failure.
            throw;
        }
        catch (Exception exception)
        {
            // Authentication is an external provider prerequisite. Normalize
            // credential failures into the existing OCR-boundary exception so
            // callers receive a predictable technical failure category.
            throw new LabelTextExtractionException(
                "Azure authentication could not be established for " +
                "Document Intelligence.",
                exception);
        }
    }

    /// <summary>
    /// Maps Azure-specific document style spans into provider-neutral OCR
    /// typography evidence.
    /// </summary>
    private static IReadOnlyList<OcrTextStyle> MapStyles(
        AnalyzeResult result)
    {
        var content =
            result.Content
            ?? string.Empty;

        return result.Styles
            .Where(
                style =>
                    style.FontWeight is not null)
            .SelectMany(
                style =>
                    style.Spans.Select(
                        span =>
                            new OcrTextStyle
                            {
                                Offset =
                                    span.Offset,

                                Length =
                                    span.Length,

                                // Preserve the exact OCR text represented by
                                // the provider style span for explainability.
                                Text =
                                    ExtractSpanText(
                                        content,
                                        span.Offset,
                                        span.Length),

                                FontWeight =
                                    MapFontWeight(
                                        style.FontWeight),

                                Confidence =
                                    style.Confidence
                            }))
            .ToArray();
    }

    /// <summary>
    /// Converts Azure font-weight values into the application's
    /// provider-neutral representation.
    /// </summary>
    private static OcrFontWeight MapFontWeight(
        DocumentFontWeight? fontWeight)
    {
        if (fontWeight is null)
        {
            return OcrFontWeight.Unknown;
        }

        if (fontWeight.Value ==
            DocumentFontWeight.Bold)
        {
            return OcrFontWeight.Bold;
        }

        if (fontWeight.Value ==
            DocumentFontWeight.Normal)
        {
            return OcrFontWeight.Normal;
        }

        return OcrFontWeight.Unknown;
    }

    /// <summary>
    /// Safely extracts the text represented by an Azure
    /// <c>DocumentSpan</c>.
    /// </summary>
    private static string ExtractSpanText(
        string content,
        int offset,
        int length)
    {
        if (offset < 0 ||
            length <= 0 ||
            offset >= content.Length)
        {
            return string.Empty;
        }

        var safeLength =
            Math.Min(
                length,
                content.Length - offset);

        return content.Substring(
            offset,
            safeLength);
    }
}