namespace LabelVerification.Application.Verification;

/// <summary>
/// Result of one deterministic verification check.
///
/// Original values and OCR evidence are retained for explainability,
/// auditability, and human review.
/// </summary>
public sealed record VerificationCheckResult
{
    public required string Field { get; init; }

    public required VerificationStatus Status { get; init; }

    public string? ExpectedValue { get; init; }

    public string? ObservedValue { get; init; }

    public string? NormalizedExpectedValue { get; init; }

    public string? NormalizedObservedValue { get; init; }

    /// <summary>
    /// Comparison similarity in the range 0.0 through 1.0 when applicable.
    /// </summary>
    public double? Similarity { get; init; }

    /// <summary>
    /// OCR confidence associated with the observed evidence when available.
    /// </summary>
    public double? EvidenceConfidence { get; init; }

    public string? Evidence { get; init; }

    public required string Explanation { get; init; }
}