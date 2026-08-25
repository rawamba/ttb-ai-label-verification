namespace LabelVerification.Domain.Models;

/// <summary>
/// Represents the label values expected from the approved application record.
///
/// These values form the authoritative application-data side of the
/// verification process. They are compared with values later extracted
/// from the submitted label image.
/// </summary>
public sealed record ExpectedLabelData
{
    /// <summary>
    /// Brand name expected to appear on the submitted label.
    /// </summary>
    public required string BrandName { get; init; }

    /// <summary>
    /// Class or type designation expected to appear on the label.
    /// </summary>
    public required string ClassType { get; init; }

    /// <summary>
    /// Expected alcohol by volume percentage.
    ///
    /// Decimal is used instead of floating-point types because this value
    /// participates in deterministic compliance comparisons.
    /// </summary>
    public required decimal AlcoholByVolume { get; init; }

    /// <summary>
    /// Expected proof value when applicable to the beverage type.
    /// </summary>
    public required decimal Proof { get; init; }

    /// <summary>
    /// Expected declared net contents.
    /// </summary>
    public required NetContents NetContents { get; init; }
}

/// <summary>
/// Represents a quantity and unit used for declared net contents.
///
/// Keeping the numeric value separate from the unit allows verification
/// logic to normalize equivalent units without relying on formatted strings.
/// </summary>
public sealed record NetContents
{
    /// <summary>
    /// Numeric quantity of the declared contents.
    /// </summary>
    public required decimal Value { get; init; }

    /// <summary>
    /// Unit associated with the declared quantity, for example "mL".
    /// </summary>
    public required string Unit { get; init; }
}