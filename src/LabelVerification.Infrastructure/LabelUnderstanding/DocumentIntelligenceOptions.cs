namespace LabelVerification.Infrastructure.LabelUnderstanding;

/// <summary>
/// Configuration for the Azure Document Intelligence OCR provider.
///
/// Authentication readiness and OCR execution intentionally use separate
/// timeout budgets. This prevents first-use credential discovery from
/// consuming the complete latency-sensitive OCR operation budget.
/// </summary>
public sealed class DocumentIntelligenceOptions
{
    /// <summary>
    /// Configuration section used by the Web host and benchmark harness.
    /// </summary>
    public const string SectionName =
        "DocumentIntelligence";

    /// <summary>
    /// Azure Document Intelligence service endpoint.
    /// </summary>
    public required Uri Endpoint { get; init; }

    /// <summary>
    /// Prebuilt OCR model used for label text extraction.
    /// </summary>
    public string ModelId { get; init; } =
        "prebuilt-read";

    /// <summary>
    /// Maximum time allowed for the latency-sensitive OCR provider operation
    /// after Azure authentication readiness has been established.
    ///
    /// The prototype preserves the stakeholder-driven five-second default.
    /// </summary>
    public TimeSpan Timeout { get; init; } =
        TimeSpan.FromSeconds(
            5);

    /// <summary>
    /// Maximum time allowed to establish Azure credential readiness before
    /// starting the five-second OCR provider-operation timeout.
    ///
    /// This separate startup budget handles first-use credential discovery,
    /// token acquisition, and Managed Identity initialization without silently
    /// increasing the normal OCR timeout.
    /// </summary>
    public TimeSpan AuthenticationTimeout { get; init; } =
        TimeSpan.FromSeconds(
            15);

    /// <summary>
    /// Enables Azure Document Intelligence font-style extraction so supported
    /// regulatory rules can evaluate visual evidence such as bold Government
    /// Warning headings.
    /// </summary>
    public bool EnableFontStyling { get; init; } =
        true;
}