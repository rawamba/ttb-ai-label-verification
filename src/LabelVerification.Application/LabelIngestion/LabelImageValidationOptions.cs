namespace LabelVerification.Application.LabelIngestion;

/// <summary>
/// Configures validation limits for uploaded label images.
///
/// These settings protect downstream OCR and AI processing from unsupported,
/// empty, or unexpectedly large inputs before expensive processing begins.
/// </summary>
public sealed class LabelImageValidationOptions
{
    /// <summary>
    /// Default maximum upload size: 10 MiB.
    /// </summary>
    public const long DefaultMaxFileSizeBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Maximum permitted size of a single uploaded label image.
    /// </summary>
    public long MaxFileSizeBytes { get; init; } = DefaultMaxFileSizeBytes;

    /// <summary>
    /// MIME types accepted by the prototype.
    /// </summary>
    public IReadOnlySet<string> SupportedContentTypes { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

    /// <summary>
    /// File extensions accepted by the prototype.
    /// </summary>
    public IReadOnlySet<string> SupportedExtensions { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };
}