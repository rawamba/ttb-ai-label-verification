using System.Diagnostics;
using Azure;
using Azure.AI.DocumentIntelligence;
using LabelVerification.Application.LabelUnderstanding;

namespace LabelVerification.Infrastructure.LabelUnderstanding;

/// <summary>
/// Extracts textual evidence from alcohol label images using Azure
/// Document Intelligence's prebuilt Read model.
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
            // required by the Azure Document Intelligence SDK.
            BinaryData imageData =
                await BinaryData.FromStreamAsync(
                    image,
                    linkedCancellation.Token);

            Operation<AnalyzeResult> operation =
                await _client.AnalyzeDocumentAsync(
                    WaitUntil.Completed,
                    _options.ModelId,
                    imageData,
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
            // aggregate. Individual word confidence remains available for
            // more granular downstream decisions.
            var aggregateConfidence =
                words.Length == 0
                    ? 0.0
                    : words.Average(word => word.Confidence);

            return new OcrResult
            {
                Text = result.Content ?? string.Empty,
                Lines = lines,
                Words = words,
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

            throw new LabelTextExtractionException(
                $"OCR processing exceeded the configured timeout of " +
                $"{_options.Timeout.TotalSeconds:0.#} seconds.",
                exception);
        }
        catch (RequestFailedException exception)
        {
            stopwatch.Stop();

            throw new LabelTextExtractionException(
                "Azure Document Intelligence failed to extract label text.",
                exception);
        }
    }
}