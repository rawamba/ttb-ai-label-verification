namespace LabelVerification.Application.Verification;

/// <summary>
/// Represents the aggregate result of all verification checks performed
/// against one label/application pair.
/// </summary>
public sealed record VerificationResult
{
    /// <summary>
    /// Overall status derived deterministically from the individual checks.
    /// </summary>
    public required VerificationStatus OverallStatus { get; init; }

    /// <summary>
    /// Individual verification results preserved for explanation,
    /// auditability, and human review.
    /// </summary>
    public required IReadOnlyList<VerificationCheckResult> Checks { get; init; }

    public int PassCount =>
        Checks.Count(check =>
            check.Status == VerificationStatus.Pass);

    public int ReviewCount =>
        Checks.Count(check =>
            check.Status == VerificationStatus.Review);

    public int FailCount =>
        Checks.Count(check =>
            check.Status == VerificationStatus.Fail);
}