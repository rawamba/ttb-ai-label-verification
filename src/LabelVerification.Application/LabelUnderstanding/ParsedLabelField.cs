namespace LabelVerification.Application.LabelUnderstanding;

/// <summary>
/// Represents a structured value parsed from OCR evidence while preserving
/// the evidence and confidence used to derive that value.
/// </summary>
/// <typeparam name="T">The normalized parsed value type.</typeparam>
public sealed record ParsedLabelField<T>
{
    /// <summary>
    /// Normalized value derived from the OCR evidence.
    /// </summary>
    public required T Value { get; init; }

    /// <summary>
    /// Confidence associated with the supporting OCR evidence.
    /// Expected range is 0.0 through 1.0.
    /// </summary>
    public required double Confidence { get; init; }

    /// <summary>
    /// Original OCR text supporting the parsed value.
    /// Preserved for explainability and human review.
    /// </summary>
    public required string Evidence { get; init; }
}
