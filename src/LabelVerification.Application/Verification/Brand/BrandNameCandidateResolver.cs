using LabelVerification.Application.LabelUnderstanding;

namespace LabelVerification.Application.Verification.Brand;

/// <summary>
/// Uses the existing deterministic brand verifier to improve selection among
/// brand-name candidates that were actually observed in OCR evidence.
///
/// Resolution is deliberately conservative:
///
/// 1. An exact normalized candidate always wins.
/// 2. An already-PASSING current candidate remains selected.
/// 3. A single alternate PASS candidate may rescue a weaker first candidate.
/// 4. A single REVIEW candidate may replace a FAIL/missing first candidate,
///    preserving human review rather than producing an unsupported FAIL.
/// 5. Ambiguous groups of fuzzy candidates leave the original selection
///    unchanged.
///
/// This component performs evidence selection only. The normal brand verifier
/// still performs the final regulatory PASS / REVIEW / FAIL comparison.
/// </summary>
public sealed class BrandNameCandidateResolver
    : IBrandNameCandidateResolver
{
    private const double ExactSimilarity =
        1.0;

    private const double SimilarityTolerance =
        0.0000001;

    private readonly IBrandNameVerifier _brandNameVerifier;

    public BrandNameCandidateResolver(
        IBrandNameVerifier brandNameVerifier)
    {
        ArgumentNullException.ThrowIfNull(
            brandNameVerifier);

        _brandNameVerifier =
            brandNameVerifier;
    }

    /// <inheritdoc />
    public ParsedLabelField<string>? Resolve(
        string? expectedBrandName,
        ParsedLabelField<string>? currentBrandName,
        IReadOnlyList<ParsedLabelField<string>> candidates)
    {
        ArgumentNullException.ThrowIfNull(
            candidates);

        // Without an authoritative expected brand there is no safe
        // application-guided resolution signal. Preserve parser behavior.
        if (string.IsNullOrWhiteSpace(
                expectedBrandName) ||
            candidates.Count == 0)
        {
            return currentBrandName;
        }

        var evaluations =
            candidates
                .Where(candidate =>
                    candidate is not null &&
                    !string.IsNullOrWhiteSpace(
                        candidate.Value))
                .Select(candidate =>
                    new CandidateEvaluation(
                        candidate,
                        _brandNameVerifier.Verify(
                            expectedBrandName,
                            candidate)))
                .ToArray();

        if (evaluations.Length == 0)
        {
            return currentBrandName;
        }

        // Exact normalized equality is the strongest possible evidence.
        //
        // Multiple OCR renderings may normalize to the same expected brand
        // because of casing or typographic punctuation. In that case select
        // the observation with the highest OCR evidence confidence.
        var exactMatches =
            evaluations
                .Where(evaluation =>
                    evaluation.Result.Similarity.HasValue &&
                    Math.Abs(
                        evaluation.Result.Similarity.Value -
                        ExactSimilarity) <=
                    SimilarityTolerance)
                .OrderByDescending(evaluation =>
                    evaluation.Candidate.Confidence)
                .ToArray();

        if (exactMatches.Length > 0)
        {
            return exactMatches[0].Candidate;
        }

        // Preserve the current parser selection when it already satisfies
        // the existing deterministic PASS rule. We do not replace a valid
        // fuzzy match merely because another fuzzy candidate also exists.
        var currentResult =
            _brandNameVerifier.Verify(
                expectedBrandName,
                currentBrandName);

        if (currentResult.Status ==
            VerificationStatus.Pass)
        {
            return currentBrandName;
        }

        // If exactly one alternate observed candidate satisfies the existing
        // PASS rule, it is a clear rescue of the first-candidate heuristic.
        var passingCandidates =
            evaluations
                .Where(evaluation =>
                    evaluation.Result.Status ==
                    VerificationStatus.Pass)
                .ToArray();

        if (passingCandidates.Length == 1)
        {
            return passingCandidates[0].Candidate;
        }

        // A single REVIEW candidate is safer than retaining a clearly failed
        // or missing first candidate. The downstream verifier will still
        // return REVIEW, so human judgment remains required.
        if (currentBrandName is null ||
            currentResult.Status ==
            VerificationStatus.Fail)
        {
            var reviewCandidates =
                evaluations
                    .Where(evaluation =>
                        evaluation.Result.Status ==
                        VerificationStatus.Review)
                    .ToArray();

            if (reviewCandidates.Length == 1)
            {
                return reviewCandidates[0].Candidate;
            }
        }

        // Multiple plausible fuzzy candidates are intentionally not forced
        // into a choice. Preserve existing behavior rather than manufacture
        // certainty from ambiguous evidence.
        return currentBrandName;
    }

    /// <summary>
    /// Couples one observed OCR candidate with the deterministic comparison
    /// result produced by the existing brand verifier.
    /// </summary>
    private sealed record CandidateEvaluation(
        ParsedLabelField<string> Candidate,
        VerificationCheckResult Result);
}