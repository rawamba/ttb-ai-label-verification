namespace LabelVerification.Domain.Models;

/// <summary>
/// Represents the subset of an upstream COLA application required by
/// the label-verification prototype.
///
/// The prototype intentionally does not model the complete COLA record.
/// This narrow contract keeps the verification engine isolated from
/// upstream system implementation details.
/// </summary>
public sealed record ApplicationRecord
{
    /// <summary>
    /// Unique identifier of the upstream application record.
    /// </summary>
    public required string ApplicationId { get; init; }

    /// <summary>
    /// Beverage category associated with the application.
    ///
    /// Examples may include "distilled_spirits", "wine", or "beer".
    /// The value can later be used to select beverage-specific rules.
    /// </summary>
    public required string BeverageType { get; init; }

    /// <summary>
    /// Application-derived values expected to appear on the submitted label.
    /// </summary>
    public required ExpectedLabelData ExpectedData { get; init; }
}