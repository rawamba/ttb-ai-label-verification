using LabelVerification.Application.LabelUnderstanding;

namespace LabelVerification.Application.Verification.GovernmentWarning;

/// <summary>
/// Verifies the observed Government Health Warning against required wording
/// and available OCR typography evidence.
/// </summary>
public interface IGovernmentWarningVerifier
{
    VerificationCheckResult Verify(
        ParsedLabelField<string>? observedWarning,
        OcrResult ocrResult);
}