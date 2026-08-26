using LabelVerification.Application.LabelUnderstanding;

namespace LabelVerification.Application.Verification.Brand;

/// <summary>
/// Verifies an observed label brand against the expected application brand.
/// </summary>
public interface IBrandNameVerifier
{
    VerificationCheckResult Verify(
        string? expectedBrandName,
        ParsedLabelField<string>? observedBrandName);
}