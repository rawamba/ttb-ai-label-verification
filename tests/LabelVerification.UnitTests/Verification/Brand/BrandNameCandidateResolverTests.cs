using LabelVerification.Application.LabelUnderstanding;
using LabelVerification.Application.Verification;
using LabelVerification.Application.Verification.Brand;
using LabelVerification.Application.Verification.Normalization;

namespace LabelVerification.UnitTests.Verification.Brand;

/// <summary>
/// Regression tests for expected-guided brand candidate resolution.
///
/// These tests prove that authoritative application data may help select
/// among brand candidates actually observed by OCR, while never inventing
/// label evidence or forcing ambiguous evidence into an automatic match.
/// </summary>
public sealed class BrandNameCandidateResolverTests
{
    private readonly BrandNameVerifier _verifier;

    private readonly BrandNameCandidateResolver _resolver;

    public BrandNameCandidateResolverTests()
    {
        _verifier =
            new BrandNameVerifier(
                new TextNormalizer(),
                new BrandNameVerificationOptions
                {
                    PassThreshold = 0.95,
                    ReviewThreshold = 0.80
                });

        _resolver =
            new BrandNameCandidateResolver(
                _verifier);
    }

    [Fact]
    public void Resolve_WithAdversarialOcrOrder_SelectsObservedExpectedBrand()
    {
        // This reproduces the weakness that motivated the patch.
        //
        // The old parser selected the first surviving line:
        // "CRAFTED FOR ADVENTURE".
        //
        // The actual brand is also present later in OCR evidence, but the
        // previous implementation discarded that alternate candidate.
        var parser =
            new LabelFieldParser();

        var ocrResult =
            CreateOcrResultWithLines(
                "CRAFTED FOR ADVENTURE",
                "OLD TOM DISTILLERY",
                "KENTUCKY STRAIGHT BOURBON WHISKEY",
                "45% ALCOHOL BY VOLUME",
                "90 PROOF",
                "750 ML");

        var parsed =
            parser.Parse(
                ocrResult);

        // Preserve backward-compatible parser behavior: the first plausible
        // OCR line remains the initial brand selection.
        Assert.NotNull(
            parsed.BrandName);

        Assert.Equal(
            "CRAFTED FOR ADVENTURE",
            parsed.BrandName.Value);

        // The important improvement is that the parser no longer discards
        // the other plausible observed brand candidate.
        Assert.Equal(
            2,
            parsed.BrandNameCandidates.Count);

        Assert.Contains(
            parsed.BrandNameCandidates,
            candidate =>
                candidate.Value ==
                "CRAFTED FOR ADVENTURE");

        Assert.Contains(
            parsed.BrandNameCandidates,
            candidate =>
                candidate.Value ==
                "OLD TOM DISTILLERY");

        var resolved =
            _resolver.Resolve(
                "OLD TOM DISTILLERY",
                parsed.BrandName,
                parsed.BrandNameCandidates);

        Assert.NotNull(
            resolved);

        // The resolver selects the matching value that was genuinely present
        // in OCR evidence. It does not copy the expected application value
        // into the observed evidence model.
        Assert.Equal(
            "OLD TOM DISTILLERY",
            resolved.Value);

        Assert.Equal(
            "OLD TOM DISTILLERY",
            resolved.Evidence);

        var verification =
            _verifier.Verify(
                "OLD TOM DISTILLERY",
                resolved);

        Assert.Equal(
            VerificationStatus.Pass,
            verification.Status);

        Assert.Equal(
            1.0,
            verification.Similarity);
    }

    [Fact]
    public void Resolve_WithUniqueReviewCandidate_PreservesHumanReview()
    {
        var current =
            CreateCandidate(
                "RIVER BEND");

        var reviewCandidate =
            CreateCandidate(
                "STONES THROW");

        var candidates =
            new[]
            {
                current,
                reviewCandidate
            };

        var resolved =
            _resolver.Resolve(
                "STONE'S THROW",
                current,
                candidates);

        Assert.NotNull(
            resolved);

        Assert.Equal(
            "STONES THROW",
            resolved.Value);

        // Candidate resolution improves evidence selection but does not
        // promote an ambiguous fuzzy match to PASS.
        var verification =
            _verifier.Verify(
                "STONE'S THROW",
                resolved);

        Assert.Equal(
            VerificationStatus.Review,
            verification.Status);

        Assert.NotNull(
            verification.Similarity);

        Assert.InRange(
            verification.Similarity.Value,
            0.80,
            0.949999);
    }

    [Fact]
    public void Resolve_WithMultipleAmbiguousReviewCandidates_PreservesOriginalSelection()
    {
        var current =
            CreateCandidate(
                "RIVER BEND");

        var candidates =
            new[]
            {
                current,

                CreateCandidate(
                    "STONES THROW"),

                CreateCandidate(
                    "STONE THROW")
            };

        var resolved =
            _resolver.Resolve(
                "STONE'S THROW",
                current,
                candidates);

        Assert.NotNull(
            resolved);

        // Two alternate candidates fall into the REVIEW range. The resolver
        // intentionally refuses to manufacture certainty between them.
        Assert.Same(
            current,
            resolved);

        Assert.Equal(
            "RIVER BEND",
            resolved.Value);
    }

    [Fact]
    public void Resolve_WithoutExpectedApplicationBrand_PreservesParserSelection()
    {
        var current =
            CreateCandidate(
                "OBSERVED BRAND");

        var candidates =
            new[]
            {
                current,

                CreateCandidate(
                    "ANOTHER BRAND")
            };

        var resolved =
            _resolver.Resolve(
                expectedBrandName: null,
                current,
                candidates);

        // Without authoritative application data there is no safe
        // expected-guided resolution signal.
        Assert.Same(
            current,
            resolved);
    }

    [Fact]
    public void Resolve_WhenExpectedBrandWasNotObserved_DoesNotInventEvidence()
    {
        var current =
            CreateCandidate(
                "CRAFTED FOR ADVENTURE");

        var candidates =
            new[]
            {
                current
            };

        var resolved =
            _resolver.Resolve(
                "OLD TOM DISTILLERY",
                current,
                candidates);

        Assert.NotNull(
            resolved);

        // The expected value is absent from OCR evidence. The resolver must
        // therefore preserve the observed candidate rather than creating a
        // false match from application data.
        Assert.Equal(
            "CRAFTED FOR ADVENTURE",
            resolved.Value);

        Assert.NotEqual(
            "OLD TOM DISTILLERY",
            resolved.Value);

        var verification =
            _verifier.Verify(
                "OLD TOM DISTILLERY",
                resolved);

        Assert.Equal(
            VerificationStatus.Fail,
            verification.Status);
    }

    /// <summary>
    /// Creates one synthetic OCR-derived brand observation.
    /// </summary>
    private static ParsedLabelField<string> CreateCandidate(
        string value,
        double confidence = 0.95)
    {
        return new ParsedLabelField<string>
        {
            Value = value,
            Evidence = value,
            Confidence = confidence
        };
    }

    /// <summary>
    /// Creates OCR evidence with a deterministic line order so tests can
    /// reproduce arbitrary label layouts without calling an external OCR
    /// provider.
    /// </summary>
    private static OcrResult CreateOcrResultWithLines(
        params string[] lines)
    {
        return new OcrResult
        {
            Text =
                string.Join(
                    Environment.NewLine,
                    lines),

            Lines =
                lines
                    .Select(
                        line =>
                            new OcrTextLine
                            {
                                Text = line
                            })
                    .ToArray(),

            Words = [],

            Confidence = 0.95,

            Duration =
                TimeSpan.FromMilliseconds(
                    100),

            Provider =
                "TestOCR",

            ModelVersion =
                "test"
        };
    }
}