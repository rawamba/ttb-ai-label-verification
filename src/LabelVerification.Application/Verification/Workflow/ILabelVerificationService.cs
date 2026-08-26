namespace LabelVerification.Application.Verification.Workflow;

/// <summary>
/// Executes the complete label-verification application use case.
///
/// Presentation layers submit application identity and image metadata through
/// this abstraction without coordinating individual OCR or verification
/// services themselves.
/// </summary>
public interface ILabelVerificationService
{
    Task<LabelVerificationSubmissionResult> VerifyAsync(
        string applicationId,
        Stream image,
        string fileName,
        string? contentType,
        long length,
        CancellationToken cancellationToken = default);
}