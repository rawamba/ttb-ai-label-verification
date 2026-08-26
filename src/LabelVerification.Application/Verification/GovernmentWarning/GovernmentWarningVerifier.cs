using LabelVerification.Application.LabelUnderstanding;

namespace LabelVerification.Application.Verification.GovernmentWarning;

/// <summary>
/// Deterministically verifies Government Health Warning text and available
/// typography evidence.
///
/// The verifier distinguishes demonstrated noncompliance from insufficient
/// OCR evidence. Uncertainty is routed to REVIEW rather than being converted
/// into an automatic FAIL.
/// </summary>
public sealed class GovernmentWarningVerifier
    : IGovernmentWarningVerifier
{
    private const string FieldName =
        "Government Health Warning";

    private const string RequiredHeading =
        "GOVERNMENT WARNING:";

    // Required statutory warning text.
    //
    // Whitespace layout may vary on the physical label, but wording,
    // capitalization, and punctuation are compared exactly after whitespace
    // is collapsed.
    private const string RequiredWarning =
        "GOVERNMENT WARNING: (1) According to the Surgeon General, " +
        "women should not drink alcoholic beverages during pregnancy " +
        "because of the risk of birth defects. " +
        "(2) Consumption of alcoholic beverages impairs your ability " +
        "to drive a car or operate machinery, and may cause health problems.";

    private readonly GovernmentWarningVerificationOptions _options;

    public GovernmentWarningVerifier(
        GovernmentWarningVerificationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        ValidateOptions(options);

        _options = options;
    }

    /// <inheritdoc />
    public VerificationCheckResult Verify(
        ParsedLabelField<string>? observedWarning,
        OcrResult ocrResult)
    {
        ArgumentNullException.ThrowIfNull(ocrResult);

        if (observedWarning is null ||
            string.IsNullOrWhiteSpace(observedWarning.Value))
        {
            return CreateResult(
                VerificationStatus.Review,
                observedWarning,
                normalizedObserved: null,
                "No Government Warning evidence was extracted from the " +
                "label; human review is required.");
        }

        var normalizedObserved =
            NormalizeWhitespaceOnly(
                observedWarning.Value);

        // Confidence gates all deterministic mismatch decisions. If the OCR
        // evidence itself is weak, a textual discrepancy might be perception
        // error rather than demonstrated label noncompliance.
        if (observedWarning.Confidence <
            _options.MinimumOcrConfidence)
        {
            return CreateResult(
                VerificationStatus.Review,
                observedWarning,
                normalizedObserved,
                $"Government Warning OCR confidence " +
                $"({observedWarning.Confidence:P1}) is below the configured " +
                $"review threshold of {_options.MinimumOcrConfidence:P0}.");
        }

        // Explicitly validate the required prefix before comparing the whole
        // warning so the resulting explanation is useful to an agent.
        if (!normalizedObserved.StartsWith(
                RequiredHeading,
                StringComparison.Ordinal))
        {
            if (normalizedObserved.StartsWith(
                    RequiredHeading,
                    StringComparison.OrdinalIgnoreCase))
            {
                return CreateResult(
                    VerificationStatus.Fail,
                    observedWarning,
                    normalizedObserved,
                    "The Government Warning heading was detected, but " +
                    "'GOVERNMENT WARNING:' is not presented with the " +
                    "required capitalization.");
            }

            return CreateResult(
                VerificationStatus.Fail,
                observedWarning,
                normalizedObserved,
                "The required 'GOVERNMENT WARNING:' prefix is missing " +
                "or altered.");
        }

        // Regulatory warning comparison is intentionally case-sensitive and
        // punctuation-sensitive. Only whitespace layout is normalized.
        if (!string.Equals(
                RequiredWarning,
                normalizedObserved,
                StringComparison.Ordinal))
        {
            return CreateResult(
                VerificationStatus.Fail,
                observedWarning,
                normalizedObserved,
                "Government Warning wording or punctuation differs from " +
                "the required statement.");
        }

        if (!_options.RequireBoldHeading)
        {
            return CreateResult(
                VerificationStatus.Pass,
                observedWarning,
                normalizedObserved,
                "Government Warning wording, punctuation, and capitalization " +
                "match the required statement.");
        }

        var headingStart =
            ocrResult.Text.IndexOf(
                RequiredHeading,
                StringComparison.Ordinal);

        if (headingStart < 0)
        {
            return CreateResult(
                VerificationStatus.Review,
                observedWarning,
                normalizedObserved,
                "Warning text matches, but the OCR result does not contain " +
                "enough positional evidence to verify bold heading style.");
        }

        var headingLength =
            RequiredHeading.Length;

        if (IsRangeFullyCoveredByStyle(
                ocrResult.Styles,
                headingStart,
                headingLength,
                OcrFontWeight.Bold,
                _options.MinimumStyleConfidence))
        {
            return CreateResult(
                VerificationStatus.Pass,
                observedWarning,
                normalizedObserved,
                "Government Warning wording, punctuation, capitalization, " +
                "and bold-heading evidence satisfy the automated checks.");
        }

        // A high-confidence Normal style covering the entire heading is
        // affirmative evidence that the bold requirement was not met.
        if (IsRangeFullyCoveredByStyle(
                ocrResult.Styles,
                headingStart,
                headingLength,
                OcrFontWeight.Normal,
                _options.MinimumStyleConfidence))
        {
            return CreateResult(
                VerificationStatus.Fail,
                observedWarning,
                normalizedObserved,
                "Government Warning wording is correct, but high-confidence " +
                "style evidence indicates the required heading is not bold.");
        }

        // Absence of trustworthy style evidence is not proof of a violation.
        return CreateResult(
            VerificationStatus.Review,
            observedWarning,
            normalizedObserved,
            "Government Warning text is correct, but available OCR style " +
            "evidence is insufficient to verify that the heading is bold.");
    }

    /// <summary>
    /// Determines whether every character in the requested OCR text range is
    /// covered by sufficiently confident style evidence of the requested
    /// font weight.
    ///
    /// Multiple adjacent style spans may collectively satisfy the range.
    /// </summary>
    private static bool IsRangeFullyCoveredByStyle(
        IReadOnlyList<OcrTextStyle> styles,
        int rangeStart,
        int rangeLength,
        OcrFontWeight requiredWeight,
        double minimumConfidence)
    {
        if (rangeLength <= 0 ||
            styles.Count == 0)
        {
            return false;
        }

        var rangeEnd =
            rangeStart + rangeLength;

        var covered =
            new bool[rangeLength];

        foreach (var style in styles)
        {
            if (style.FontWeight != requiredWeight ||
                style.Confidence < minimumConfidence)
            {
                continue;
            }

            var styleStart =
                style.Offset;

            var styleEnd =
                style.Offset + style.Length;

            var overlapStart =
                Math.Max(
                    rangeStart,
                    styleStart);

            var overlapEnd =
                Math.Min(
                    rangeEnd,
                    styleEnd);

            if (overlapStart >= overlapEnd)
            {
                continue;
            }

            for (var position = overlapStart;
                 position < overlapEnd;
                 position++)
            {
                covered[position - rangeStart] = true;
            }
        }

        return covered.All(value => value);
    }

    /// <summary>
    /// Normalizes only physical whitespace layout.
    ///
    /// Case, punctuation, apostrophes, and wording are intentionally left
    /// untouched because they are regulatory evidence.
    /// </summary>
    private static string NormalizeWhitespaceOnly(
        string value)
    {
        return string.Join(
            " ",
            value.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));
    }

    private static VerificationCheckResult CreateResult(
        VerificationStatus status,
        ParsedLabelField<string>? observedWarning,
        string? normalizedObserved,
        string explanation)
    {
        return new VerificationCheckResult
        {
            Field = FieldName,
            Status = status,
            ExpectedValue = RequiredWarning,
            ObservedValue = observedWarning?.Value,
            NormalizedExpectedValue = RequiredWarning,
            NormalizedObservedValue = normalizedObserved,
            Similarity = null,
            EvidenceConfidence = observedWarning?.Confidence,
            Evidence = observedWarning?.Evidence,
            Explanation = explanation
        };
    }

    private static void ValidateOptions(
        GovernmentWarningVerificationOptions options)
    {
        if (options.MinimumOcrConfidence is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Minimum OCR confidence must be between 0.0 and 1.0.");
        }

        if (options.MinimumStyleConfidence is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Minimum style confidence must be between 0.0 and 1.0.");
        }
    }
}