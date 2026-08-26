using System.Globalization;
using LabelVerification.Application.LabelUnderstanding;

namespace LabelVerification.Application.Verification.Alcohol;

/// <summary>
/// Performs deterministic numeric verification of alcohol declarations.
///
/// Parsing is intentionally outside this class. The verifier receives already
/// structured numeric values and answers whether expected and observed values
/// agree.
///
/// Missing evidence is routed to REVIEW rather than FAIL because absence of
/// usable evidence is not the same as a demonstrated mismatch.
/// </summary>
public sealed class AlcoholValueVerifier : IAlcoholValueVerifier
{
    private const string AlcoholByVolumeFieldName =
        "Alcohol By Volume";

    private const string ProofFieldName =
        "Proof";

    /// <inheritdoc />
    public VerificationCheckResult VerifyAlcoholByVolume(
        decimal? expectedAlcoholByVolume,
        ParsedLabelField<decimal>? observedAlcoholByVolume)
    {
        if (expectedAlcoholByVolume is null)
        {
            return CreateAbvResult(
                VerificationStatus.Review,
                expectedAlcoholByVolume,
                observedAlcoholByVolume,
                "Expected alcohol-by-volume value is missing; " +
                "automated comparison cannot determine compliance.");
        }

        if (observedAlcoholByVolume is null)
        {
            return CreateAbvResult(
                VerificationStatus.Review,
                expectedAlcoholByVolume,
                observedAlcoholByVolume,
                "No alcohol-by-volume evidence was extracted from the label.");
        }

        if (expectedAlcoholByVolume.Value ==
            observedAlcoholByVolume.Value)
        {
            return CreateAbvResult(
                VerificationStatus.Pass,
                expectedAlcoholByVolume,
                observedAlcoholByVolume,
                "Alcohol-by-volume value matches the expected " +
                "application value.");
        }

        return CreateAbvResult(
            VerificationStatus.Fail,
            expectedAlcoholByVolume,
            observedAlcoholByVolume,
            $"Alcohol-by-volume mismatch: expected " +
            $"{FormatAbv(expectedAlcoholByVolume.Value)} but observed " +
            $"{FormatAbv(observedAlcoholByVolume.Value)}.");
    }

    /// <inheritdoc />
    public VerificationCheckResult VerifyProof(
        decimal? expectedProof,
        ParsedLabelField<int>? observedProof)
    {
        if (expectedProof is null)
        {
            return CreateProofResult(
                VerificationStatus.Review,
                expectedProof,
                observedProof,
                "Expected proof value is missing; automated comparison " +
                "cannot determine compliance.");
        }

        if (observedProof is null)
        {
            return CreateProofResult(
                VerificationStatus.Review,
                expectedProof,
                observedProof,
                "No proof evidence was extracted from the label.");
        }

        if (expectedProof.Value ==
            observedProof.Value)
        {
            return CreateProofResult(
                VerificationStatus.Pass,
                expectedProof,
                observedProof,
                "Proof value matches the expected application value.");
        }

        return CreateProofResult(
            VerificationStatus.Fail,
            expectedProof,
            observedProof,
            $"Proof mismatch: expected {expectedProof.Value} PROOF " +
            $"but observed {observedProof.Value} PROOF.");
    }

    private static VerificationCheckResult CreateAbvResult(
        VerificationStatus status,
        decimal? expected,
        ParsedLabelField<decimal>? observed,
        string explanation)
    {
        return new VerificationCheckResult
        {
            Field = AlcoholByVolumeFieldName,
            Status = status,

            ExpectedValue =
                expected is null
                    ? null
                    : FormatAbv(expected.Value),

            ObservedValue =
                observed is null
                    ? null
                    : FormatAbv(observed.Value),

            NormalizedExpectedValue =
                expected?.ToString(
                    CultureInfo.InvariantCulture),

            NormalizedObservedValue =
                observed?.Value.ToString(
                    CultureInfo.InvariantCulture),

            Similarity = null,
            EvidenceConfidence = observed?.Confidence,
            Evidence = observed?.Evidence,
            Explanation = explanation
        };
    }

    private static VerificationCheckResult CreateProofResult(
        VerificationStatus status,
        decimal? expected,
        ParsedLabelField<int>? observed,
        string explanation)
    {
        return new VerificationCheckResult
        {
            Field = ProofFieldName,
            Status = status,

            ExpectedValue =
                expected is null
                    ? null
                    : $"{expected.Value} PROOF",

            ObservedValue =
                observed is null
                    ? null
                    : $"{observed.Value} PROOF",

            NormalizedExpectedValue =
                expected?.ToString(
                    CultureInfo.InvariantCulture),

            NormalizedObservedValue =
                observed?.Value.ToString(
                    CultureInfo.InvariantCulture),

            Similarity = null,
            EvidenceConfidence = observed?.Confidence,
            Evidence = observed?.Evidence,
            Explanation = explanation
        };
    }

    private static string FormatAbv(
        decimal value)
    {
        return $"{value.ToString(
            "0.###",
            CultureInfo.InvariantCulture)}%";
    }
}
