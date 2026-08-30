using LabelVerification.Application.LabelUnderstanding;

namespace LabelVerification.Application.Verification.Brand;

/// <summary>
/// Resolves the most appropriate observed brand-name candidate using the
/// authoritative expected application brand as a deterministic comparison
/// signal.
///
/// The resolver never invents label evidence and never substitutes the
/// expected application value for OCR evidence. It may select only a brand
/// candidate that was actually observed on the uploaded label.
/// </summary>
public interface IBrandNameCandidateResolver
{
    /// <summary>
    /// Selects the strongest supported observed brand candidate.
    /// </summary>
    ParsedLabelField<string>? Resolve(
        string? expectedBrandName,
        ParsedLabelField<string>? currentBrandName,
        IReadOnlyList<ParsedLabelField<string>> candidates);
}