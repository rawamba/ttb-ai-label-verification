namespace LabelVerification.Application.Verification.Workflow;

/// <summary>
/// Contains non-sensitive operational telemetry for one verification attempt.
///
/// Document content, OCR text, image bytes, filenames, and extracted field
/// values are intentionally excluded from this model.
/// </summary>
public sealed record VerificationTelemetry
{
    /// <summary>
    /// Correlates all telemetry emitted for one verification attempt.
    ///
    /// HTTP request correlation remains available independently through the
    /// Web correlation middleware.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Time spent invoking the configured OCR abstraction.
    /// Null when processing ended before OCR began.
    /// </summary>
    public TimeSpan? OcrDuration { get; init; }

    /// <summary>
    /// Time spent parsing OCR evidence, executing deterministic comparison
    /// rules, and aggregating the final verification result.
    /// Null when processing ended before verification began.
    /// </summary>
    public TimeSpan? VerificationDuration { get; init; }

    /// <summary>
    /// Complete Application-layer workflow duration, including application
    /// lookup, buffering, validation, OCR, and deterministic verification.
    /// </summary>
    public required TimeSpan TotalDuration { get; init; }
}