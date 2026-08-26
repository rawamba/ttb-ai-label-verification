namespace LabelVerification.Application.LabelUnderstanding;

/// <summary>
/// Conditional ingredient declarations that may appear on an alcohol label.
/// Applicability is determined later by regulatory verification rules.
/// </summary>
public enum IngredientDeclarationType
{
    Aspartame,
    Sulfites,
    FdAndCYellow5,
    CochinealOrCarmine
}
