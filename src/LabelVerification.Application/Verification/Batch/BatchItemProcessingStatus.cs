namespace LabelVerification.Application.Verification.Batch;

/// <summary>
/// Describes the technical processing state of a label within a batch.
///
/// This is deliberately separate from the regulatory verification result.
/// A technically completed item may still result in PASS, REVIEW, or FAIL.
/// </summary>
public enum BatchItemProcessingStatus
{
    Pending,
    Processing,
    Completed,
    Error
}