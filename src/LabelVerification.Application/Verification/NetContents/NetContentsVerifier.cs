using System.Globalization;
using LabelVerification.Application.LabelUnderstanding;

namespace LabelVerification.Application.Verification.NetContents;

/// <summary>
/// Performs deterministic net-content comparison after normalizing supported
/// liquid-volume units to milliliters.
///
/// Missing or unsupported evidence is routed to REVIEW. A demonstrated
/// numeric mismatch returns FAIL.
/// </summary>
public sealed class NetContentsVerifier : INetContentsVerifier
{
    private const string FieldName = "Net Contents";

    private readonly INetContentsNormalizer _normalizer;

    public NetContentsVerifier(
        INetContentsNormalizer normalizer)
    {
        ArgumentNullException.ThrowIfNull(normalizer);

        _normalizer = normalizer;
    }

    /// <inheritdoc />
    public VerificationCheckResult Verify(
        decimal? expectedValue,
        string? expectedUnit,
        ParsedLabelField<ParsedNetContents>? observedNetContents)
    {
        if (expectedValue is null ||
            string.IsNullOrWhiteSpace(expectedUnit))
        {
            return CreateResult(
                VerificationStatus.Review,
                expectedValue,
                expectedUnit,
                observedNetContents,
                normalizedExpected: null,
                normalizedObserved: null,
                "Expected net contents are missing or incomplete; " +
                "automated comparison cannot determine compliance.");
        }

        if (observedNetContents is null)
        {
            return CreateResult(
                VerificationStatus.Review,
                expectedValue,
                expectedUnit,
                observedNetContents,
                normalizedExpected: null,
                normalizedObserved: null,
                "No net-content evidence was extracted from the label.");
        }

        // Normalize the expected application quantity to the canonical
        // milliliter representation.
        if (!_normalizer.TryNormalizeToMilliliters(
                expectedValue.Value,
                expectedUnit,
                out var normalizedExpected))
        {
            return CreateResult(
                VerificationStatus.Review,
                expectedValue,
                expectedUnit,
                observedNetContents,
                normalizedExpected: null,
                normalizedObserved: null,
                $"Expected net-content unit '{expectedUnit}' is not " +
                "supported for automated comparison.");
        }

        // Normalize the OCR-derived label quantity through the same path so
        // equivalent representations can be compared deterministically.
        if (!_normalizer.TryNormalizeToMilliliters(
                observedNetContents.Value.Value,
                observedNetContents.Value.Unit,
                out var normalizedObserved))
        {
            return CreateResult(
                VerificationStatus.Review,
                expectedValue,
                expectedUnit,
                observedNetContents,
                normalizedExpected,
                normalizedObserved: null,
                $"Observed net-content unit " +
                $"'{observedNetContents.Value.Unit}' is not supported " +
                "for automated comparison.");
        }

        if (normalizedExpected == normalizedObserved)
        {
            return CreateResult(
                VerificationStatus.Pass,
                expectedValue,
                expectedUnit,
                observedNetContents,
                normalizedExpected,
                normalizedObserved,
                $"Net contents are equivalent after unit normalization " +
                $"({FormatMilliliters(normalizedObserved)}).");
        }

        return CreateResult(
            VerificationStatus.Fail,
            expectedValue,
            expectedUnit,
            observedNetContents,
            normalizedExpected,
            normalizedObserved,
            $"Net-content mismatch: expected " +
            $"{FormatMilliliters(normalizedExpected)} after normalization " +
            $"but observed {FormatMilliliters(normalizedObserved)}.");
    }

    private static VerificationCheckResult CreateResult(
        VerificationStatus status,
        decimal? expectedValue,
        string? expectedUnit,
        ParsedLabelField<ParsedNetContents>? observed,
        decimal? normalizedExpected,
        decimal? normalizedObserved,
        string explanation)
    {
        return new VerificationCheckResult
        {
            Field = FieldName,
            Status = status,

            ExpectedValue =
                FormatOriginal(
                    expectedValue,
                    expectedUnit),

            ObservedValue =
                observed is null
                    ? null
                    : FormatOriginal(
                        observed.Value.Value,
                        observed.Value.Unit),

            NormalizedExpectedValue =
                normalizedExpected is null
                    ? null
                    : FormatMilliliters(
                        normalizedExpected.Value),

            NormalizedObservedValue =
                normalizedObserved is null
                    ? null
                    : FormatMilliliters(
                        normalizedObserved.Value),

            Similarity = null,
            EvidenceConfidence = observed?.Confidence,
            Evidence = observed?.Evidence,
            Explanation = explanation
        };
    }

    private static string? FormatOriginal(
        decimal? value,
        string? unit)
    {
        if (value is null ||
            string.IsNullOrWhiteSpace(unit))
        {
            return null;
        }

        return
            $"{value.Value.ToString(
                "0.###",
                CultureInfo.InvariantCulture)} {unit.Trim()}";
    }

    private static string FormatMilliliters(
        decimal value)
    {
        return
            $"{value.ToString(
                "0.############",
                CultureInfo.InvariantCulture)} mL";
    }
}
