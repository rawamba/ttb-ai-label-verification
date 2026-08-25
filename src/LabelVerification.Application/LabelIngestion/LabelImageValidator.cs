namespace LabelVerification.Application.LabelIngestion;

/// <summary>
/// Performs deterministic validation of uploaded label images before
/// OCR or AI-assisted processing begins.
///
/// Filename extensions and MIME types are treated only as claims. The
/// validator also inspects well-known file-signature bytes so obviously
/// mislabeled uploads are rejected before downstream processing.
/// </summary>
public sealed class LabelImageValidator : ILabelImageValidator
{
    private const int MaximumSignatureLength = 12;

    private readonly LabelImageValidationOptions _options;

    public LabelImageValidator(LabelImageValidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxFileSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Maximum file size must be greater than zero.");
        }

        _options = options;
    }

    /// <inheritdoc />
    public async Task<LabelImageValidationResult> ValidateAsync(
        Stream stream,
        string fileName,
        string? contentType,
        long length,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (length <= 0)
        {
            return LabelImageValidationResult.Invalid(
                "EMPTY_FILE",
                "The uploaded label image is empty.");
        }

        if (length > _options.MaxFileSizeBytes)
        {
            return LabelImageValidationResult.Invalid(
                "FILE_TOO_LARGE",
                $"The uploaded image exceeds the maximum allowed size of " +
                $"{_options.MaxFileSizeBytes / (1024 * 1024)} MiB.");
        }

        var extension = Path.GetExtension(fileName);

        if (string.IsNullOrWhiteSpace(extension) ||
            !_options.SupportedExtensions.Contains(extension))
        {
            return LabelImageValidationResult.Invalid(
                "UNSUPPORTED_IMAGE_TYPE",
                "The uploaded file does not use a supported image extension.");
        }

        if (string.IsNullOrWhiteSpace(contentType) ||
            !_options.SupportedContentTypes.Contains(contentType))
        {
            return LabelImageValidationResult.Invalid(
                "UNSUPPORTED_IMAGE_TYPE",
                "The uploaded file does not declare a supported image content type.");
        }

        var expectedFormat =
            DetermineDeclaredFormat(extension, contentType);

        if (expectedFormat is null)
        {
            return LabelImageValidationResult.Invalid(
                "IMAGE_TYPE_MISMATCH",
                "The file extension and declared image content type do not agree.");
        }

        var signature = new byte[MaximumSignatureLength];

        var originalPosition =
            stream.CanSeek
                ? stream.Position
                : (long?)null;

        try
        {
            var bytesRead = await stream.ReadAsync(
                signature.AsMemory(0, signature.Length),
                cancellationToken);

            if (!SignatureMatches(
                    expectedFormat.Value,
                    signature.AsSpan(0, bytesRead)))
            {
                return LabelImageValidationResult.Invalid(
                    "INVALID_FILE_SIGNATURE",
                    "The uploaded file contents do not match the declared image type.");
            }
        }
        finally
        {
            // Preserve stream position when possible so validation remains
            // non-destructive for downstream OCR or AI processing.
            if (originalPosition.HasValue)
            {
                stream.Position = originalPosition.Value;
            }
        }

        return LabelImageValidationResult.Valid();
    }

    /// <summary>
    /// Determines whether the filename extension and MIME type describe
    /// the same supported image format.
    /// </summary>
    private static SupportedImageFormat? DetermineDeclaredFormat(
        string extension,
        string contentType)
    {
        var normalizedExtension = extension.ToLowerInvariant();
        var normalizedContentType = contentType.ToLowerInvariant();

        return (normalizedExtension, normalizedContentType) switch
        {
            (".jpg" or ".jpeg", "image/jpeg") =>
                SupportedImageFormat.Jpeg,

            (".png", "image/png") =>
                SupportedImageFormat.Png,

            (".webp", "image/webp") =>
                SupportedImageFormat.WebP,

            _ => null
        };
    }

    /// <summary>
    /// Verifies well-known file magic bytes rather than trusting only
    /// filename or MIME metadata supplied by the client.
    /// </summary>
    private static bool SignatureMatches(
        SupportedImageFormat format,
        ReadOnlySpan<byte> bytes)
    {
        return format switch
        {
            SupportedImageFormat.Jpeg => IsJpeg(bytes),
            SupportedImageFormat.Png => IsPng(bytes),
            SupportedImageFormat.WebP => IsWebP(bytes),
            _ => false
        };
    }

    private static bool IsJpeg(ReadOnlySpan<byte> bytes)
    {
        return bytes.Length >= 3 &&
               bytes[0] == 0xFF &&
               bytes[1] == 0xD8 &&
               bytes[2] == 0xFF;
    }

    private static bool IsPng(ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> pngSignature =
        [
            0x89, 0x50, 0x4E, 0x47,
            0x0D, 0x0A, 0x1A, 0x0A
        ];

        return bytes.StartsWith(pngSignature);
    }

    private static bool IsWebP(ReadOnlySpan<byte> bytes)
    {
        // WebP uses a RIFF container:
        //
        // Bytes 0-3   = "RIFF"
        // Bytes 4-7   = file size
        // Bytes 8-11  = "WEBP"

        if (bytes.Length < 12)
        {
            return false;
        }

        return
            bytes[0] == (byte)'R' &&
            bytes[1] == (byte)'I' &&
            bytes[2] == (byte)'F' &&
            bytes[3] == (byte)'F' &&
            bytes[8] == (byte)'W' &&
            bytes[9] == (byte)'E' &&
            bytes[10] == (byte)'B' &&
            bytes[11] == (byte)'P';
    }

    private enum SupportedImageFormat
    {
        Jpeg,
        Png,
        WebP
    }
}