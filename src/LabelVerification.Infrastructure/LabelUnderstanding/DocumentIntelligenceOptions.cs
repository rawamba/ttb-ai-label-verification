namespace LabelVerification.Infrastructure.LabelUnderstanding;

/// <summary>
/// Configuration for the Azure Document Intelligence OCR provider.
/// </summary>
public sealed class DocumentIntelligenceOptions
{
    public const string SectionName = "DocumentIntelligence";

    /// <summary>
    /// Azure Document Intelligence service endpoint.
    /// </summary>
    public required Uri Endpoint { get; init; }

    /// <summary>
    /// Prebuilt OCR model used for label text extraction.
    /// </summary>
    public string ModelId { get; init; } = "prebuilt-read";

    /// <summary>
    /// Maximum time allowed for a single OCR request.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
}