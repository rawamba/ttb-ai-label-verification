namespace LabelVerification.Application.LabelUnderstanding;

/// <summary>
/// Structured representation of regulatory label information derived
/// from OCR evidence.
///
/// This model describes what was observed on the label. Regulatory
/// applicability and compliance decisions belong to the verification layer.
/// </summary>
public sealed record ParsedLabelData
{
    /// <summary>
    /// The currently selected brand-name observation.
    ///
    /// The parser initially selects the first plausible candidate to preserve
    /// the prototype's existing behavior. The verification workflow may later
    /// replace this selection with another observed candidate when the
    /// authoritative application brand provides a clear deterministic signal.
    /// </summary>
    public ParsedLabelField<string>? BrandName { get; init; }

    /// <summary>
    /// All plausible brand-name observations found in OCR evidence.
    ///
    /// These are observations from the label only. Expected application data
    /// is deliberately not used by the parsing layer to create candidates.
    /// </summary>
    public IReadOnlyList<ParsedLabelField<string>>
        BrandNameCandidates
    { get; init; } = [];

    public ParsedLabelField<ParsedNameAndAddress>? NameAndAddress { get; init; }

    public ParsedLabelField<string>? CountryOfOrigin { get; init; }

    public ParsedLabelField<string>? ClassType { get; init; }

    public ParsedLabelField<decimal>? AlcoholByVolume { get; init; }

    public ParsedLabelField<int>? Proof { get; init; }

    public ParsedLabelField<ParsedNetContents>? NetContents { get; init; }

    public ParsedLabelField<string>? GovernmentWarning { get; init; }

    public IReadOnlyList<ParsedIngredientDeclaration>
        IngredientDeclarations
    { get; init; } = [];
}