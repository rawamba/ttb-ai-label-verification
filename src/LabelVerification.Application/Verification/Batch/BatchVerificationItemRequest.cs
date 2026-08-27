namespace LabelVerification.Application.Verification.Batch;

/// <summary>
/// Describes one label to be processed within a batch.
///
/// The stream factory allows each worker to open the image only when it is
/// ready to process the item. This avoids opening hundreds of streams up front.
/// </summary>
public sealed record BatchVerificationItemRequest(
    string ItemId,
    string DisplayName,
    string ContentType,
    long Size,
    Func<Stream> OpenReadStream);