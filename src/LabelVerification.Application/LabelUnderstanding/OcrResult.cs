namespace LabelVerification.Application.LabelUnderstanding;

/// <summary>
/// Represents textual evidence extracted from a validated label image.
///
/// This object contains OCR/model evidence, confidence, and operational
/// metadata. It represents perception evidence only and does not make a
/// regulatory compliance determination.
/// </summary>
public sealed record OcrResult
{
    /// <summary>
    /// Combined text extracted from the image in provider reading order.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Text lines detected in the image.
    /// </summary>
    public required IReadOnlyList<OcrTextLine> Lines { get; init; }

    /// <summary>
    /// Individual words detected by the OCR provider together with their
    /// provider-reported confidence values.
    ///
    /// Preserving word-level evidence allows downstream processing to reason
    /// about uncertain portions of a label rather than relying only on a
    /// single aggregate confidence score.
    /// </summary>
    public required IReadOnlyList<OcrWord> Words { get; init; }

    /// <summary>
    /// Aggregate OCR confidence normalized to the range 0.0 through 1.0.
    ///
    /// The concrete provider determines how this aggregate is calculated.
    /// Individual word confidence remains available for more granular
    /// downstream decisions.
    /// </summary>
    public required double Confidence { get; init; }

    /// <summary>
    /// Total time spent performing OCR extraction.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Logical provider name used for diagnostics, observability, and
    /// future model evaluation.
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Provider model identifier or model version when available.
    /// </summary>
    public string? ModelVersion { get; init; }

    /// <summary>
    /// Optional typography evidence associated with spans of OCR text.
    /// Providers that cannot establish typography may return an empty collection.
    /// </summary>
    public IReadOnlyList<OcrTextStyle> Styles { get; init; } = [];
}