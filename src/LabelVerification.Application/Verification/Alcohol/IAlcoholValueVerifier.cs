using LabelVerification.Application.LabelUnderstanding;

namespace LabelVerification.Application.Verification.Alcohol;

/// <summary>
/// Performs deterministic comparison of alcohol-by-volume and proof values.
/// </summary>
public interface IAlcoholValueVerifier
{
    VerificationCheckResult VerifyAlcoholByVolume(
        decimal? expectedAlcoholByVolume,
        ParsedLabelField<decimal>? observedAlcoholByVolume);

    VerificationCheckResult VerifyProof(
        int? expectedProof,
        ParsedLabelField<int>? observedProof);
}