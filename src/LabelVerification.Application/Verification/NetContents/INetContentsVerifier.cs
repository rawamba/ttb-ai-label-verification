using LabelVerification.Application.LabelUnderstanding;

namespace LabelVerification.Application.Verification.NetContents;

/// <summary>
/// Verifies observed label net contents against the expected application
/// quantity.
/// </summary>
public interface INetContentsVerifier
{
    VerificationCheckResult Verify(
        decimal? expectedValue,
        string? expectedUnit,
        ParsedLabelField<ParsedNetContents>? observedNetContents);
}