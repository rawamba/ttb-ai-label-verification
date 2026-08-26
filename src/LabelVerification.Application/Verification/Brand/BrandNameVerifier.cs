using LabelVerification.Application.LabelUnderstanding;
using LabelVerification.Application.Verification.Normalization;

namespace LabelVerification.Application.Verification.Brand;

/// <summary>
/// Performs deterministic brand-name comparison.
///
/// Normalized exact comparison is attempted first. Fuzzy comparison is used
/// only when exact comparison fails. Ambiguous similarity is routed to REVIEW
/// rather than silently accepted or rejected.
/// </summary>
public sealed class BrandNameVerifier : IBrandNameVerifier
{
    private const string FieldName = "Brand Name";

    private readonly ITextNormalizer _textNormalizer;
    private readonly BrandNameVerificationOptions _options;

    public BrandNameVerifier(
        ITextNormalizer textNormalizer,
        BrandNameVerificationOptions options)
    {
        ArgumentNullException.ThrowIfNull(textNormalizer);
        ArgumentNullException.ThrowIfNull(options);

        ValidateOptions(options);

        _textNormalizer = textNormalizer;
        _options = options;
    }

    /// <inheritdoc />
    public VerificationCheckResult Verify(
        string? expectedBrandName,
        ParsedLabelField<string>? observedBrandName)
    {
        var normalizedExpected =
            _textNormalizer.NormalizeForComparison(
                expectedBrandName);

        if (string.IsNullOrWhiteSpace(normalizedExpected))
        {
            return CreateResult(
                VerificationStatus.Review,
                expectedBrandName,
                observedBrandName,
                normalizedExpected,
                normalizedObserved: null,
                similarity: null,
                "Expected brand name is missing; automated comparison " +
                "cannot determine compliance.");
        }

        if (observedBrandName is null ||
            string.IsNullOrWhiteSpace(observedBrandName.Value))
        {
            return CreateResult(
                VerificationStatus.Review,
                expectedBrandName,
                observedBrandName,
                normalizedExpected,
                normalizedObserved: null,
                similarity: null,
                "No brand-name evidence was extracted from the label.");
        }

        var normalizedObserved =
            _textNormalizer.NormalizeForComparison(
                observedBrandName.Value);

        if (string.IsNullOrWhiteSpace(normalizedObserved))
        {
            return CreateResult(
                VerificationStatus.Review,
                expectedBrandName,
                observedBrandName,
                normalizedExpected,
                normalizedObserved,
                similarity: null,
                "Observed brand-name evidence normalized to an empty value.");
        }

        // The strongest result is deterministic equality after normalization.
        if (string.Equals(
                normalizedExpected,
                normalizedObserved,
                StringComparison.Ordinal))
        {
            return CreateResult(
                VerificationStatus.Pass,
                expectedBrandName,
                observedBrandName,
                normalizedExpected,
                normalizedObserved,
                similarity: 1.0,
                "Brand name matches after deterministic text normalization.");
        }

        var similarity =
            CalculateNormalizedLevenshteinSimilarity(
                normalizedExpected,
                normalizedObserved);

        if (similarity >= _options.PassThreshold)
        {
            return CreateResult(
                VerificationStatus.Pass,
                expectedBrandName,
                observedBrandName,
                normalizedExpected,
                normalizedObserved,
                similarity,
                $"Brand names are a strong fuzzy match ({similarity:P1}), " +
                $"meeting the PASS threshold of {_options.PassThreshold:P0}.");
        }

        if (similarity >= _options.ReviewThreshold)
        {
            return CreateResult(
                VerificationStatus.Review,
                expectedBrandName,
                observedBrandName,
                normalizedExpected,
                normalizedObserved,
                similarity,
                $"Brand names are similar but not conclusive " +
                $"({similarity:P1}); human review is required.");
        }

        return CreateResult(
            VerificationStatus.Fail,
            expectedBrandName,
            observedBrandName,
            normalizedExpected,
            normalizedObserved,
            similarity,
            $"Brand names differ materially ({similarity:P1}), below the " +
            $"REVIEW threshold of {_options.ReviewThreshold:P0}.");
    }

    private static VerificationCheckResult CreateResult(
        VerificationStatus status,
        string? expectedBrandName,
        ParsedLabelField<string>? observedBrandName,
        string? normalizedExpected,
        string? normalizedObserved,
        double? similarity,
        string explanation)
    {
        return new VerificationCheckResult
        {
            Field = FieldName,
            Status = status,
            ExpectedValue = expectedBrandName,
            ObservedValue = observedBrandName?.Value,
            NormalizedExpectedValue = normalizedExpected,
            NormalizedObservedValue = normalizedObserved,
            Similarity = similarity,
            EvidenceConfidence = observedBrandName?.Confidence,
            Evidence = observedBrandName?.Evidence,
            Explanation = explanation
        };
    }

    /// <summary>
    /// Calculates normalized Levenshtein similarity from 0.0 through 1.0.
    ///
    /// Uses two rows rather than an entire distance matrix, reducing memory
    /// complexity to O(min(m, n)).
    /// </summary>
    private static double CalculateNormalizedLevenshteinSimilarity(
        string left,
        string right)
    {
        if (string.Equals(
                left,
                right,
                StringComparison.Ordinal))
        {
            return 1.0;
        }

        if (left.Length == 0 ||
            right.Length == 0)
        {
            return 0.0;
        }

        // Keep the second dimension shorter to minimize temporary memory.
        if (left.Length < right.Length)
        {
            (left, right) = (right, left);
        }

        var previous =
            new int[right.Length + 1];

        var current =
            new int[right.Length + 1];

        for (var column = 0;
             column <= right.Length;
             column++)
        {
            previous[column] = column;
        }

        for (var row = 1;
             row <= left.Length;
             row++)
        {
            current[0] = row;

            for (var column = 1;
                 column <= right.Length;
                 column++)
            {
                var substitutionCost =
                    left[row - 1] == right[column - 1]
                        ? 0
                        : 1;

                current[column] =
                    Math.Min(
                        Math.Min(
                            current[column - 1] + 1,
                            previous[column] + 1),
                        previous[column - 1] +
                        substitutionCost);
            }

            (previous, current) =
                (current, previous);
        }

        var distance =
            previous[right.Length];

        var maximumLength =
            Math.Max(
                left.Length,
                right.Length);

        return 1.0 -
            ((double)distance / maximumLength);
    }

    private static void ValidateOptions(
        BrandNameVerificationOptions options)
    {
        if (options.PassThreshold is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Brand PASS threshold must be between 0.0 and 1.0.");
        }

        if (options.ReviewThreshold is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Brand REVIEW threshold must be between 0.0 and 1.0.");
        }

        if (options.ReviewThreshold >=
            options.PassThreshold)
        {
            throw new ArgumentException(
                "Brand REVIEW threshold must be lower than the PASS threshold.",
                nameof(options));
        }
    }
}