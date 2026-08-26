namespace LabelVerification.Application.Verification.GovernmentWarning;

/// <summary>
/// Thresholds controlling Government Warning evidence evaluation.
/// </summary>
public sealed record GovernmentWarningVerificationOptions
{
    /// <summary>
    /// OCR confidence below this threshold is treated as insufficient
    /// evidence and routed to human review.
    /// </summary>
    public double MinimumOcrConfidence { get; init; } = 0.90;

    /// <summary>
    /// Minimum confidence required before font-weight evidence can support
    /// an automated typography decision.
    /// </summary>
    public double MinimumStyleConfidence { get; init; } = 0.90;

    /// <summary>
    /// Controls whether bold-heading evidence is required for an automated
    /// PASS result.
    /// </summary>
    public bool RequireBoldHeading { get; init; } = true;
}