namespace LabelVerification.Application.Verification;

/// <summary>
/// Calculates the overall verification status from individual field checks.
/// </summary>
public interface IVerificationResultAggregator
{
    VerificationResult Aggregate(
        IEnumerable<VerificationCheckResult> checks);
}