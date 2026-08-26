namespace LabelVerification.Application.Verification.NetContents;

/// <summary>
/// Deterministically converts supported liquid-volume units to milliliters.
///
/// This component performs unit conversion only. It does not apply regulatory
/// tolerances, determine compliance, or decide PASS / REVIEW / FAIL.
/// </summary>
public sealed class NetContentsNormalizer : INetContentsNormalizer
{
    private const decimal MillilitersPerLiter = 1000m;

    // One U.S. fluid ounce equals exactly 29.5735295625 milliliters
    // based on the defined U.S. liquid gallon.
    private const decimal MillilitersPerFluidOunce =
        29.5735295625m;

    // One U.S. liquid pint equals 16 U.S. fluid ounces.
    private const decimal MillilitersPerPint =
        473.176473m;

    /// <inheritdoc />
    public bool TryNormalizeToMilliliters(
        decimal value,
        string? unit,
        out decimal milliliters)
    {
        milliliters = 0m;

        if (value <= 0m ||
            string.IsNullOrWhiteSpace(unit))
        {
            return false;
        }

        var normalizedUnit =
            NormalizeUnitToken(unit);

        switch (normalizedUnit)
        {
            case "ML":
            case "MILLILITER":
            case "MILLILITERS":
            case "MILLILITRE":
            case "MILLILITRES":
                milliliters = value;
                return true;

            case "L":
            case "LITER":
            case "LITERS":
            case "LITRE":
            case "LITRES":
                milliliters =
                    value * MillilitersPerLiter;

                return true;

            case "FLOZ":
            case "FLUIDOUNCE":
            case "FLUIDOUNCES":
                milliliters =
                    value * MillilitersPerFluidOunce;

                return true;

            case "PT":
            case "PINT":
            case "PINTS":
                milliliters =
                    value * MillilitersPerPint;

                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Canonicalizes only the unit token.
    ///
    /// Examples:
    /// "mL"       -> "ML"
    /// "FL OZ"    -> "FLOZ"
    /// "FL. OZ."  -> "FLOZ"
    /// "fluid oz" -> "FLUIDOZ"
    ///
    /// No numeric interpretation occurs here.
    /// </summary>
    private static string NormalizeUnitToken(
        string unit)
    {
        return new string(
            unit
                .Where(char.IsLetter)
                .Select(char.ToUpperInvariant)
                .ToArray());
    }
}
