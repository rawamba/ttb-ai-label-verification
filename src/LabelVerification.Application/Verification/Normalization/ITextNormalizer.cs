namespace LabelVerification.Application.Verification.Normalization;

/// <summary>
/// Produces a deterministic comparison representation of textual values.
///
/// Normalization is used only for comparison. Original application values
/// and OCR evidence must remain unchanged for explainability and auditability.
/// </summary>
public interface ITextNormalizer
{
    /// <summary>
    /// Normalizes text for deterministic comparison.
    /// </summary>
    /// <param name="value">
    /// The original value. Null and whitespace-only values normalize to
    /// an empty string.
    /// </param>
    string NormalizeForComparison(string? value);
}
