namespace LabelVerification.Application.LabelIngestion;

/// <summary>
/// Represents the outcome of deterministic validation performed on an
/// uploaded label image before downstream OCR or AI processing.
/// </summary>
public sealed record LabelImageValidationResult
{
    private LabelImageValidationResult(
        bool isValid,
        string? errorCode,
        string? errorMessage)
    {
        IsValid = isValid;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Indicates whether the uploaded image passed all validation checks.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Stable machine-readable error code suitable for logging, testing,
    /// telemetry, and future UI mapping.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Human-readable explanation of the validation failure.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static LabelImageValidationResult Valid() =>
        new(
            isValid: true,
            errorCode: null,
            errorMessage: null);

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static LabelImageValidationResult Invalid(
        string errorCode,
        string errorMessage) =>
        new(
            isValid: false,
            errorCode,
            errorMessage);
}