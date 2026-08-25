namespace LabelVerification.Application.LabelIngestion;

/// <summary>
/// Validates uploaded label images before they are submitted to OCR
/// or other AI-assisted processing.
///
/// The abstraction intentionally uses general .NET types rather than
/// ASP.NET Core or Blazor-specific upload types so ingestion validation
/// remains independent of the presentation layer.
/// </summary>
public interface ILabelImageValidator
{
    /// <summary>
    /// Validates image metadata and, where practical, verifies that the
    /// actual file signature agrees with the declared image type.
    /// </summary>
    /// <param name="stream">
    /// Stream containing the uploaded label image.
    /// </param>
    /// <param name="fileName">
    /// Original filename supplied with the upload.
    /// </param>
    /// <param name="contentType">
    /// Declared MIME type supplied with the upload.
    /// </param>
    /// <param name="length">
    /// Declared file length in bytes.
    /// </param>
    /// <param name="cancellationToken">
    /// Allows validation to be cancelled by the calling workflow.
    /// </param>
    Task<LabelImageValidationResult> ValidateAsync(
        Stream stream,
        string fileName,
        string? contentType,
        long length,
        CancellationToken cancellationToken = default);
}