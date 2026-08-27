using LabelVerification.Application.Verification.Workflow;

namespace LabelVerification.Application.Verification.Batch;

/// <summary>
/// Result for one label in a batch.
///
/// ProcessingStatus represents technical execution.
/// VerificationResult contains PASS / REVIEW / FAIL when processing completes.
/// </summary>
public sealed record BatchVerificationItemResult
{
    public required string ItemId { get; init; }

    public required string DisplayName { get; init; }

    public required BatchItemProcessingStatus ProcessingStatus { get; init; }

    public LabelVerificationSubmissionResult? VerificationResult { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public TimeSpan Duration { get; init; }
}