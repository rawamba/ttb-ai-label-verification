using LabelVerification.Application.LabelIngestion;

namespace LabelVerification.UnitTests.LabelIngestion;

/// <summary>
/// Verifies deterministic validation of uploaded label images before
/// downstream OCR or AI-assisted processing.
///
/// These tests intentionally use in-memory streams so validation logic
/// remains isolated from filesystem, network, and presentation concerns.
/// </summary>
public sealed class LabelImageValidatorTests
{
    [Fact]
    public async Task ValidateAsync_WhenJpegIsValid_ReturnsValid()
    {
        // Arrange
        var validator = CreateValidator();

        byte[] imageBytes =
        [
            0xFF, 0xD8, 0xFF, 0xE0,
            0x00, 0x10, 0x4A, 0x46,
            0x49, 0x46, 0x00, 0x01
        ];

        await using var stream = new MemoryStream(imageBytes);

        // Act
        var result = await validator.ValidateAsync(
            stream,
            "label.jpg",
            "image/jpeg",
            imageBytes.Length);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_WhenPngIsValid_ReturnsValid()
    {
        // Arrange
        var validator = CreateValidator();

        byte[] imageBytes =
        [
            0x89, 0x50, 0x4E, 0x47,
            0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x00
        ];

        await using var stream = new MemoryStream(imageBytes);

        // Act
        var result = await validator.ValidateAsync(
            stream,
            "label.png",
            "image/png",
            imageBytes.Length);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenWebPIsValid_ReturnsValid()
    {
        // Arrange
        var validator = CreateValidator();

        byte[] imageBytes =
        [
            // RIFF
            0x52, 0x49, 0x46, 0x46,

            // File-size field
            0x24, 0x00, 0x00, 0x00,

            // WEBP
            0x57, 0x45, 0x42, 0x50
        ];

        await using var stream = new MemoryStream(imageBytes);

        // Act
        var result = await validator.ValidateAsync(
            stream,
            "label.webp",
            "image/webp",
            imageBytes.Length);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenFileIsEmpty_ReturnsEmptyFileError()
    {
        // Arrange
        var validator = CreateValidator();

        await using var stream = new MemoryStream();

        // Act
        var result = await validator.ValidateAsync(
            stream,
            "label.jpg",
            "image/jpeg",
            length: 0);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("EMPTY_FILE", result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenFileExceedsMaximumSize_ReturnsFileTooLargeError()
    {
        // Arrange
        const long maxFileSize = 1024;

        var validator = CreateValidator(
            maxFileSizeBytes: maxFileSize);

        byte[] imageBytes =
        [
            0xFF, 0xD8, 0xFF
        ];

        await using var stream = new MemoryStream(imageBytes);

        // Act
        var result = await validator.ValidateAsync(
            stream,
            "label.jpg",
            "image/jpeg",
            length: maxFileSize + 1);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("FILE_TOO_LARGE", result.ErrorCode);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    [InlineData("application/octet-stream")]
    [InlineData("")]
    [InlineData(null)]
    public async Task ValidateAsync_WhenContentTypeIsUnsupported_ReturnsUnsupportedImageType(
        string? contentType)
    {
        // Arrange
        var validator = CreateValidator();

        byte[] imageBytes =
        [
            0xFF, 0xD8, 0xFF
        ];

        await using var stream = new MemoryStream(imageBytes);

        // Act
        var result = await validator.ValidateAsync(
            stream,
            "label.jpg",
            contentType,
            imageBytes.Length);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("UNSUPPORTED_IMAGE_TYPE", result.ErrorCode);
    }

    [Theory]
    [InlineData("label.pdf")]
    [InlineData("label.txt")]
    [InlineData("label.exe")]
    [InlineData("label")]
    [InlineData("")]
    public async Task ValidateAsync_WhenExtensionIsUnsupported_ReturnsUnsupportedImageType(
        string fileName)
    {
        // Arrange
        var validator = CreateValidator();

        byte[] imageBytes =
        [
            0xFF, 0xD8, 0xFF
        ];

        await using var stream = new MemoryStream(imageBytes);

        // Act
        var result = await validator.ValidateAsync(
            stream,
            fileName,
            "image/jpeg",
            imageBytes.Length);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("UNSUPPORTED_IMAGE_TYPE", result.ErrorCode);
    }

    [Theory]
    [InlineData("label.png", "image/jpeg")]
    [InlineData("label.jpg", "image/png")]
    [InlineData("label.webp", "image/jpeg")]
    public async Task ValidateAsync_WhenExtensionAndContentTypeDisagree_ReturnsImageTypeMismatch(
        string fileName,
        string contentType)
    {
        // Arrange
        var validator = CreateValidator();

        byte[] bytes = new byte[12];

        await using var stream = new MemoryStream(bytes);

        // Act
        var result = await validator.ValidateAsync(
            stream,
            fileName,
            contentType,
            bytes.Length);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("IMAGE_TYPE_MISMATCH", result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenJpegSignatureIsInvalid_ReturnsInvalidFileSignature()
    {
        // Arrange
        var validator = CreateValidator();

        byte[] fakeJpeg =
        [
            0x4D, 0x5A, 0x90, 0x00,
            0x03, 0x00, 0x00, 0x00,
            0x04, 0x00, 0x00, 0x00
        ];

        await using var stream = new MemoryStream(fakeJpeg);

        // Act
        var result = await validator.ValidateAsync(
            stream,
            "label.jpg",
            "image/jpeg",
            fakeJpeg.Length);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("INVALID_FILE_SIGNATURE", result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenPngSignatureIsInvalid_ReturnsInvalidFileSignature()
    {
        // Arrange
        var validator = CreateValidator();

        byte[] fakePng =
        [
            0x89, 0x50, 0x4E, 0x00,
            0x0D, 0x0A, 0x1A, 0x0A
        ];

        await using var stream = new MemoryStream(fakePng);

        // Act
        var result = await validator.ValidateAsync(
            stream,
            "label.png",
            "image/png",
            fakePng.Length);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("INVALID_FILE_SIGNATURE", result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenWebPSignatureIsInvalid_ReturnsInvalidFileSignature()
    {
        // Arrange
        var validator = CreateValidator();

        byte[] fakeWebP =
        [
            0x52, 0x49, 0x46, 0x46,
            0x24, 0x00, 0x00, 0x00,
            0x4E, 0x4F, 0x50, 0x45
        ];

        await using var stream = new MemoryStream(fakeWebP);

        // Act
        var result = await validator.ValidateAsync(
            stream,
            "label.webp",
            "image/webp",
            fakeWebP.Length);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("INVALID_FILE_SIGNATURE", result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenStreamIsSeekable_RestoresOriginalPosition()
    {
        // Arrange
        var validator = CreateValidator();

        byte[] imageBytes =
        [
            0xFF, 0xD8, 0xFF, 0xE0,
            0x00, 0x10, 0x4A, 0x46,
            0x49, 0x46, 0x00, 0x01
        ];

        await using var stream = new MemoryStream(imageBytes);

        var originalPosition = stream.Position;

        // Act
        var result = await validator.ValidateAsync(
            stream,
            "label.jpg",
            "image/jpeg",
            imageBytes.Length);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(originalPosition, stream.Position);
    }

    [Fact]
    public void Constructor_WhenMaximumFileSizeIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var options = new LabelImageValidationOptions
        {
            MaxFileSizeBytes = 0
        };

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LabelImageValidator(options));
    }

    private static LabelImageValidator CreateValidator(
        long maxFileSizeBytes =
            LabelImageValidationOptions.DefaultMaxFileSizeBytes)
    {
        var options = new LabelImageValidationOptions
        {
            MaxFileSizeBytes = maxFileSizeBytes
        };

        return new LabelImageValidator(options);
    }
}