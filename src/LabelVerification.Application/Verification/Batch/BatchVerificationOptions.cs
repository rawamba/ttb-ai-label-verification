namespace LabelVerification.Application.Verification.Batch;

/// <summary>
/// Operational limits for batch label verification.
/// </summary>
public sealed class BatchVerificationOptions
{
    public const int DefaultMaxBatchSize =
        300;

    public const int DefaultMaxConcurrency =
        3;

    public int MaxBatchSize { get; set; } =
        DefaultMaxBatchSize;

    public int MaxConcurrency { get; set; } =
        DefaultMaxConcurrency;
}