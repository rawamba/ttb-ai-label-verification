namespace LabelVerification.Application.LabelUnderstanding;

/// <summary>
/// Represents a conditional ingredient declaration detected on the label.
/// </summary>
public sealed record ParsedIngredientDeclaration
{
    public required IngredientDeclarationType Type { get; init; }

    public required string Evidence { get; init; }

    public required double Confidence { get; init; }
}
