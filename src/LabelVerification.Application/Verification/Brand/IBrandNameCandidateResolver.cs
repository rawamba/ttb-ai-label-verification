using LabelVerification.Application.LabelUnderstanding;

namespace LabelVerification.Application.Verification.Brand;

/// <summary>
/// Resolves the most appropriate observed brand-name evidence using the
/// authoritative expected application brand as a deterministic comparison
/// signal.
///
/// Expected application data may help locate observed evidence, but it can
/// never become the evidence. Every returned value must originate from OCR.
/// </summary>
public interface IBrandNameCandidateResolver
{
    /// <summary>
    /// Selects the strongest supported observed brand candidate.
    ///
    /// Parser candidates remain the primary source. Raw OCR is inspected only
    /// as a conservative fallback when the OCR provider merged visually
    /// separate label regions into one line and the parser could not isolate
    /// the brand declaration.
    /// </summary>
    ParsedLabelField<string>? Resolve(
        string? expectedBrandName,
        ParsedLabelField<string>? currentBrandName,
        IReadOnlyList<ParsedLabelField<string>> candidates,
        OcrResult? ocrResult = null,
        ParsedLabelField<ParsedNameAndAddress>? nameAndAddress = null);
}