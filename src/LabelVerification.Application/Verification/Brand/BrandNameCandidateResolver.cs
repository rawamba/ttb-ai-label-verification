using LabelVerification.Application.LabelUnderstanding;

namespace LabelVerification.Application.Verification.Brand;

/// <summary>
/// Uses deterministic brand verification to improve selection among brand
/// observations that were actually detected by OCR.
///
/// Resolution is deliberately conservative:
///
/// 1. An exact normalized parser candidate always wins.
/// 2. If OCR merged the brand with unrelated text, an exact normalized token
///    span may be recovered directly from the OCR line.
/// 3. Producer/address declarations cannot independently prove a brand.
/// 4. An already-PASSING parser candidate remains selected.
/// 5. A single alternate PASS candidate may rescue a weaker first candidate.
/// 6. A single REVIEW candidate may replace a FAIL/missing first candidate,
///    preserving human review.
/// 7. Ambiguous fuzzy candidates leave the original selection unchanged.
///
/// Raw-OCR rescue requires exact normalized equality. Fuzzy matching against
/// raw OCR is intentionally prohibited to avoid confirmation bias from the
/// expected application value.
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

    /// <summary>
    /// Explicit producer declarations that may legitimately contain the same
    /// business name as the brand but must not independently establish that a
    /// separate brand declaration exists on the label.
    /// </summary>
    private static readonly string[] ProducerPrefixes =
    [
        "BREWED BY",
        "BOTTLED BY",
        "DISTILLED BY",
        "PRODUCED BY",
        "IMPORTED BY",
        "DISTRIBUTED BY"
    ];

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
        IReadOnlyList<ParsedLabelField<string>> candidates,
        OcrResult? ocrResult = null,
        ParsedLabelField<ParsedNameAndAddress>? nameAndAddress = null)
    {
        ArgumentNullException.ThrowIfNull(
            candidates);

        // Without an authoritative expected brand there is no safe
        // application-guided resolution signal. Preserve parser behavior.
        if (string.IsNullOrWhiteSpace(
                expectedBrandName))
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

        // Exact normalized equality among ordinary parser candidates is the
        // strongest possible parser-level evidence.
        //
        // Multiple OCR renderings may normalize to the same expected brand
        // because of casing or typographic punctuation. Prefer the observation
        // with the strongest OCR evidence confidence.
        var exactMatches =
            evaluations
                .Where(evaluation =>
                    IsExactMatch(
                        evaluation.Result.Similarity))
                .OrderByDescending(evaluation =>
                    evaluation.Candidate.Confidence)
                .ToArray();

        if (exactMatches.Length > 0)
        {
            return exactMatches[0].Candidate;
        }

        // Azure Document Intelligence may merge visually separate text regions
        // into a single OCR line. The real regression fixture produced:
        //
        // KENTUCKY STRAIGHT BOURBON WHISKEY OLD TOM DISTILLERY
        //
        // The parser correctly rejects that whole line as a brand candidate
        // because it contains class/type terminology. At this workflow boundary
        // we have the authoritative expected brand, so we may conservatively
        // search for an EXACT observed token span within raw OCR evidence.
        //
        // Fuzzy raw-OCR searching is deliberately not permitted.
        var rawOcrMatch =
            TryResolveExactObservedSpan(
                expectedBrandName,
                ocrResult,
                nameAndAddress);

        if (rawOcrMatch is not null)
        {
            return rawOcrMatch;
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

        // If exactly one alternate observed parser candidate satisfies the
        // existing PASS rule, it is a clear rescue of the first-candidate
        // heuristic.
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
    /// Searches raw OCR lines for an exact normalized token span matching the
    /// expected brand.
    ///
    /// This fallback exists specifically for OCR segmentation cases where the
    /// provider merges the brand with another visual label region.
    ///
    /// Only text actually present in OCR is returned.
    /// </summary>
    private ParsedLabelField<string>? TryResolveExactObservedSpan(
        string expectedBrandName,
        OcrResult? ocrResult,
        ParsedLabelField<ParsedNameAndAddress>? nameAndAddress)
    {
        if (ocrResult is null)
        {
            return null;
        }

        var expectedTokens =
            SplitTokens(
                expectedBrandName);

        if (expectedTokens.Length == 0)
        {
            return null;
        }

        foreach (var ocrLine in ocrResult.Lines)
        {
            var line =
                NormalizeWhitespace(
                    ocrLine.Text);

            if (string.IsNullOrWhiteSpace(
                    line))
            {
                continue;
            }

            // Producer/address evidence may legitimately contain the same
            // organization name as the brand. It must not independently prove
            // that a brand declaration exists elsewhere on the label.
            if (IsProducerOrAddressEvidence(
                    line,
                    nameAndAddress))
            {
                continue;
            }

            var observedTokens =
                SplitTokens(
                    line);

            if (observedTokens.Length <
                expectedTokens.Length)
            {
                continue;
            }

            // Slide a token window equal to the expected brand token count
            // across the actual OCR line.
            //
            // For:
            // KENTUCKY STRAIGHT BOURBON WHISKEY OLD TOM DISTILLERY
            //
            // and expected:
            // OLD TOM DISTILLERY
            //
            // one observed window will be:
            // OLD TOM DISTILLERY
            for (var start = 0;
                 start <=
                 observedTokens.Length -
                 expectedTokens.Length;
                 start++)
            {
                var observedSpan =
                    string.Join(
                        " ",
                        observedTokens
                            .Skip(start)
                            .Take(
                                expectedTokens.Length));

                var observedField =
                    new ParsedLabelField<string>
                    {
                        Value =
                            observedSpan,

                        // Preserve the entire OCR line so a reviewer can see
                        // exactly where the recovered span originated.
                        Evidence =
                            line,

                        Confidence =
                            CalculateObservedSpanConfidence(
                                observedSpan,
                                ocrResult)
                    };

                var comparison =
                    _brandNameVerifier.Verify(
                        expectedBrandName,
                        observedField);

                if (!IsExactMatch(
                        comparison.Similarity))
                {
                    continue;
                }

                return observedField;
            }
        }

        return null;
    }

    /// <summary>
    /// Prevents explicit producer/address evidence from being promoted into a
    /// brand declaration simply because the producer organization has the same
    /// name as the expected brand.
    ///
    /// A raw OCR line is excluded only when it is itself producer/address
    /// evidence. A legitimate standalone brand line must not be rejected merely
    /// because its text also appears inside a longer producer declaration.
    /// </summary>
    private static bool IsProducerOrAddressEvidence(
        string line,
        ParsedLabelField<ParsedNameAndAddress>? nameAndAddress)
    {
        // Explicit producer declarations are always producer evidence.
        if (ProducerPrefixes.Any(
                prefix =>
                    line.StartsWith(
                        prefix,
                        StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var producerEvidence =
            nameAndAddress?.Evidence;

        if (string.IsNullOrWhiteSpace(
                producerEvidence))
        {
            return false;
        }

        // Preserve evidence-line boundaries.
        //
        // Do NOT flatten producer evidence and use substring matching here.
        // For example:
        //
        // Producer evidence:
        //     BOTTLED BY OLD TOM DISTILLERY
        //
        // Legitimate standalone brand line:
        //     OLD TOM DISTILLERY
        //
        // The standalone brand is textually contained in the producer
        // declaration, but it is not itself the producer declaration.
        var producerEvidenceLines =
            producerEvidence
                .Split(
                    ['\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Select(
                    NormalizeWhitespace);

        return producerEvidenceLines.Any(
            evidenceLine =>
                string.Equals(
                    evidenceLine,
                    line,
                    StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Estimates confidence for the recovered span from OCR words that support
    /// the observed text. Provider-level confidence remains the fallback.
    /// </summary>
    private static double CalculateObservedSpanConfidence(
        string observedSpan,
        OcrResult ocrResult)
    {
        var tokens =
            SplitTokens(
                observedSpan)
                .Select(
                    NormalizeToken)
                .Where(token =>
                    token.Length > 0)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        if (tokens.Count == 0 ||
            ocrResult.Words.Count == 0)
        {
            return ocrResult.Confidence;
        }

        var supportingWords =
            ocrResult.Words
                .Where(word =>
                    tokens.Contains(
                        NormalizeToken(
                            word.Text)))
                .ToArray();

        return supportingWords.Length == 0
            ? ocrResult.Confidence
            : supportingWords.Average(
                word =>
                    word.Confidence);
    }

    /// <summary>
    /// Determines whether the existing deterministic verifier reported exact
    /// normalized equality.
    /// </summary>
    private static bool IsExactMatch(
        double? similarity)
    {
        return
            similarity.HasValue &&
            Math.Abs(
                similarity.Value -
                ExactSimilarity) <=
            SimilarityTolerance;
    }

    /// <summary>
    /// Splits text while retaining the original token contents. This ensures
    /// any selected candidate can be traced directly back to OCR evidence.
    /// </summary>
    private static string[] SplitTokens(
        string value)
    {
        return value
            .Split(
                [' ', '\t', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Performs only whitespace normalization on raw OCR evidence.
    /// </summary>
    private static string NormalizeWhitespace(
        string value)
    {
        return string.Join(
            " ",
            SplitTokens(
                value));
    }

    /// <summary>
    /// Normalizes punctuation only for identifying OCR words that support a
    /// recovered observed span. Brand equality remains the responsibility of
    /// the existing BrandNameVerifier.
    /// </summary>
    private static string NormalizeToken(
        string value)
    {
        return value
            .Trim()
            .Trim(',', '.', ':', ';', '(', ')')
            .ToUpperInvariant();
    }

    /// <summary>
    /// Couples one observed parser candidate with the deterministic comparison
    /// result produced by the existing brand verifier.
    /// </summary>
    private sealed record CandidateEvaluation(
        ParsedLabelField<string> Candidate,
        VerificationCheckResult Result);
}