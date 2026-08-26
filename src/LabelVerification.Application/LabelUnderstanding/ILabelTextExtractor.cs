namespace LabelVerification.Application.LabelUnderstanding;

/// <summary>
/// Extracts textual evidence from a validated alcohol label image.
///
/// Implementations may use managed OCR, local OCR, or multimodal vision
/// models. The Application layer depends only on this abstraction and
/// therefore remains independent of the selected AI provider.
/// </summary>
public interface ILabelTextExtractor
{
    /// <summary>
    /// Extracts textual evidence from a validated label image.
    /// </summary>
    /// <param name="image">
    /// Stream containing the validated label image.
    /// </param>
    /// <param name="cancellationToken">
    /// Allows the caller to cancel extraction or enforce a processing timeout.
    /// </param>
    Task<OcrResult> ExtractAsync(
        Stream image,
        CancellationToken cancellationToken = default);
}