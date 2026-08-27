namespace LabelVerification.Application.Verification.Batch;

/// <summary>
/// Coordinates multiple independent label-verification operations.
///
/// The existing single-label verification service remains authoritative for
/// validation, OCR, parsing, deterministic verification, and aggregation.
/// </summary>
public interface IBatchLabelVerificationService
{
    Task<BatchVerificationResult> VerifyAsync(
        string applicationId,
        IReadOnlyList<BatchVerificationItemRequest> items,
        IProgress<BatchVerificationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}