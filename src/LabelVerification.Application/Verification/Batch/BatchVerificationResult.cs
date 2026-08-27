namespace LabelVerification.Application.Verification.Batch;

/// <summary>
/// Final result of one batch-verification execution.
/// </summary>
public sealed record BatchVerificationResult
{
    public required string BatchCorrelationId { get; init; }

    public required IReadOnlyList<BatchVerificationItemResult> Items { get; init; }

    public required BatchVerificationSummary Summary { get; init; }
}