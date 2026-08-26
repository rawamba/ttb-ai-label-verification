using System.Diagnostics;
using Azure;
using Azure.AI.DocumentIntelligence;
using LabelVerification.Application.LabelUnderstanding;

namespace LabelVerification.Infrastructure.LabelUnderstanding;

/// <summary>
/// Extracts textual and optional typography evidence from alcohol label
/// images using Azure Document Intelligence.
///
/// Azure-specific response types remain inside Infrastructure. The rest of
/// the application receives provider-neutral OCR evidence through OcrResult.
/// </summary>
public sealed class DocumentIntelligenceLabelTextExtractor
    : ILabelTextExtractor
{
    private const string ProviderName = "AzureDocumentIntelligence";

    private readonly DocumentIntelligenceClient _client;
    private readonly DocumentIntelligenceOptions _options;

    public DocumentIntelligenceLabelTextExtractor(
        DocumentIntelligenceClient client,
        DocumentIntelligenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        _client = client;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<OcrResult> ExtractAsync(
        Stream image,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        var stopwatch = Stopwatch.StartNew();

        using var timeoutCancellation =
            new CancellationTokenSource(_options.Timeout);

        using var linkedCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCancellation.Token);

        try
        {
            // Convert the already validated image into the binary payload
            // required by Azure Document Intelligence.
            BinaryData imageData =
                await BinaryData.FromStreamAsync(
                    image,
                    linkedCancellation.Token);

            // Use the strongly typed options overload so optional model
            // capabilities can be enabled without changing the application
            // contract.
            var analyzeOptions =
                new AnalyzeDocumentOptions(
                    _options.ModelId,
                    imageData);

            // Font styling is optional because it can affect latency and cost.
            // When enabled, downstream regulatory rules can inspect visual
            // evidence such as whether "GOVERNMENT WARNING" is bold.
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

            AnalyzeResult result = operation.Value;

            stopwatch.Stop();

            var lines = result.Pages
                .SelectMany(page => page.Lines)
                .Select(line => new OcrTextLine
                {
                    Text = line.Content
                })
                .ToArray();

            var words = result.Pages
                .SelectMany(page => page.Words)
                .Select(word => new OcrWord
                {
                    Text = word.Content,
                    Confidence = word.Confidence
                })
                .ToArray();

            // Use the arithmetic mean as a transparent prototype-level
            // aggregate confidence. Individual word confidence remains
            // available for granular downstream review decisions.
            var aggregateConfidence =
                words.Length == 0
                    ? 0.0
                    : words.Average(word => word.Confidence);

            return new OcrResult
            {
                Text = result.Content ?? string.Empty,
                Lines = lines,
                Words = words,
                Styles = MapStyles(result),
                Confidence = aggregateConfidence,
                Duration = stopwatch.Elapsed,
                Provider = ProviderName,
                ModelVersion = result.ModelId
            };
        }
        catch (OperationCanceledException exception)
            when (timeoutCancellation.IsCancellationRequested &&
                  !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            // The timeout covers the complete provider operation:
            // authentication, network transport, Azure processing, polling,
            // and result retrieval.
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
    /// Maps Azure-specific document style spans into provider-neutral OCR
    /// typography evidence.
    /// </summary>
    private static IReadOnlyList<OcrTextStyle> MapStyles(
        AnalyzeResult result)
    {
        var content = result.Content ?? string.Empty;

        return result.Styles
            .Where(style => style.FontWeight is not null)
            .SelectMany(
                style => style.Spans.Select(
                    span => new OcrTextStyle
                    {
                        Offset = span.Offset,
                        Length = span.Length,

                        // Preserve the exact OCR content associated with the
                        // style span for explainability and auditability.
                        Text = ExtractSpanText(
                            content,
                            span.Offset,
                            span.Length),

                        FontWeight = MapFontWeight(
                            style.FontWeight),

                        Confidence = style.Confidence
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

        if (fontWeight.Value == DocumentFontWeight.Bold)
        {
            return OcrFontWeight.Bold;
        }

        if (fontWeight.Value == DocumentFontWeight.Normal)
        {
            return OcrFontWeight.Normal;
        }

        return OcrFontWeight.Unknown;
    }

    /// <summary>
    /// Safely extracts the text represented by an Azure DocumentSpan.
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