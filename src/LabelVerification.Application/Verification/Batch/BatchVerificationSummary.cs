namespace LabelVerification.Application.Verification.Batch;

/// <summary>
/// Final batch-level counts.
///
/// Regulatory results and technical errors remain distinct.
/// </summary>
public sealed record BatchVerificationSummary
{
    public required int TotalCount { get; init; }

    public required int CompletedCount { get; init; }

    public required int PassCount { get; init; }

    public required int ReviewCount { get; init; }

    public required int FailCount { get; init; }

    public required int ErrorCount { get; init; }

    public required TimeSpan Elapsed { get; init; }
}