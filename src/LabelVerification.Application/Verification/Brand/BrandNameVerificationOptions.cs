namespace LabelVerification.Application.Verification.Brand;

/// <summary>
/// Thresholds controlling deterministic brand-name similarity decisions.
/// </summary>
public sealed record BrandNameVerificationOptions
{
    /// <summary>
    /// Similarity at or above this threshold is considered a strong match.
    /// </summary>
    public double PassThreshold { get; init; } = 0.95;

    /// <summary>
    /// Similarity at or above this value but below PassThreshold requires
    /// human review. Values below this threshold are clear mismatches.
    /// </summary>
    public double ReviewThreshold { get; init; } = 0.80;
}