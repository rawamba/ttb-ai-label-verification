namespace LabelVerification.Application.Verification;

/// <summary>
/// Deterministically calculates the overall verification status.
///
/// Precedence:
///
/// FAIL   - at least one check demonstrates a clear mismatch.
/// REVIEW - no failures exist, but at least one check is uncertain.
/// PASS   - every performed check passed.
///
/// An empty check collection returns REVIEW because absence of verification
/// evidence must never be interpreted as successful compliance.
/// </summary>
public sealed class VerificationResultAggregator
    : IVerificationResultAggregator
{
    /// <inheritdoc />
    public VerificationResult Aggregate(
        IEnumerable<VerificationCheckResult> checks)
    {
        ArgumentNullException.ThrowIfNull(checks);

        // Materialize once so callers may safely provide lazy enumerables
        // without causing repeated evaluation.
        var results =
            checks.ToArray();

        var overallStatus =
            CalculateOverallStatus(results);

        return new VerificationResult
        {
            OverallStatus = overallStatus,
            Checks = results
        };
    }

    private static VerificationStatus CalculateOverallStatus(
        IReadOnlyList<VerificationCheckResult> checks)
    {
        // No evidence is an uncertainty condition, never an automatic PASS.
        if (checks.Count == 0)
        {
            return VerificationStatus.Review;
        }

        // FAIL has the highest precedence because a demonstrated mismatch
        // remains a failure even when other fields require review.
        if (checks.Any(
                check =>
                    check.Status == VerificationStatus.Fail))
        {
            return VerificationStatus.Fail;
        }

        // REVIEW takes precedence over PASS when there is no demonstrated
        // failure but at least one check lacks sufficient certainty.
        if (checks.Any(
                check =>
                    check.Status == VerificationStatus.Review))
        {
            return VerificationStatus.Review;
        }

        return VerificationStatus.Pass;
    }
}