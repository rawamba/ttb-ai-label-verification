namespace LabelVerification.Application.Verification.NetContents;

/// <summary>
/// Converts supported liquid net-content quantities into a canonical
/// milliliter representation for deterministic comparison.
/// </summary>
public interface INetContentsNormalizer
{
    /// <summary>
    /// Attempts to normalize a liquid-volume quantity to milliliters.
    /// </summary>
    /// <param name="value">The numeric quantity to normalize.</param>
    /// <param name="unit">The observed or expected unit.</param>
    /// <param name="milliliters">
    /// The normalized milliliter quantity when normalization succeeds.
    /// </param>
    /// <returns>
    /// True when the quantity and unit are supported; otherwise false.
    /// </returns>
    bool TryNormalizeToMilliliters(
        decimal value,
        string? unit,
        out decimal milliliters);
}
