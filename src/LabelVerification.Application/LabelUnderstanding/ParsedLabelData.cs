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
    public ParsedLabelField<string>? BrandName { get; init; }

    public ParsedLabelField<ParsedNameAndAddress>? NameAndAddress { get; init; }

    public ParsedLabelField<string>? CountryOfOrigin { get; init; }

    public ParsedLabelField<string>? ClassType { get; init; }

    public ParsedLabelField<decimal>? AlcoholByVolume { get; init; }

    public ParsedLabelField<int>? Proof { get; init; }

    public ParsedLabelField<ParsedNetContents>? NetContents { get; init; }

    public ParsedLabelField<string>? GovernmentWarning { get; init; }

    public IReadOnlyList<ParsedIngredientDeclaration>
        IngredientDeclarations { get; init; } = [];
}
