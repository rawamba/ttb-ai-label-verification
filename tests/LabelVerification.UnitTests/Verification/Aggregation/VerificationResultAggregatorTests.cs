using LabelVerification.Application.Verification;

namespace LabelVerification.UnitTests.Verification.Aggregation;

public sealed class VerificationResultAggregatorTests
{
    private readonly VerificationResultAggregator _aggregator = new();

    [Fact]
    public void Aggregate_WhenAllChecksPass_ReturnsPass()
    {
        var result =
            _aggregator.Aggregate(
            [
                CreateCheck("Brand Name", VerificationStatus.Pass),
                CreateCheck("Alcohol By Volume", VerificationStatus.Pass),
                CreateCheck("Net Contents", VerificationStatus.Pass),
                CreateCheck("Government Health Warning", VerificationStatus.Pass)
            ]);

        Assert.Equal(
            VerificationStatus.Pass,
            result.OverallStatus);

        Assert.Equal(4, result.PassCount);
        Assert.Equal(0, result.ReviewCount);
        Assert.Equal(0, result.FailCount);
    }

    [Fact]
    public void Aggregate_WhenOneCheckRequiresReview_ReturnsReview()
    {
        var result =
            _aggregator.Aggregate(
            [
                CreateCheck("Brand Name", VerificationStatus.Pass),
                CreateCheck("Alcohol By Volume", VerificationStatus.Pass),
                CreateCheck(
                    "Government Health Warning",
                    VerificationStatus.Review)
            ]);

        Assert.Equal(
            VerificationStatus.Review,
            result.OverallStatus);

        Assert.Equal(2, result.PassCount);
        Assert.Equal(1, result.ReviewCount);
        Assert.Equal(0, result.FailCount);
    }

    [Fact]
    public void Aggregate_WhenMultipleChecksRequireReview_ReturnsReview()
    {
        var result =
            _aggregator.Aggregate(
            [
                CreateCheck("Brand Name", VerificationStatus.Review),
                CreateCheck("Net Contents", VerificationStatus.Pass),
                CreateCheck(
                    "Government Health Warning",
                    VerificationStatus.Review)
            ]);

        Assert.Equal(
            VerificationStatus.Review,
            result.OverallStatus);

        Assert.Equal(2, result.ReviewCount);
    }

    [Fact]
    public void Aggregate_WhenOneCheckFails_ReturnsFail()
    {
        var result =
            _aggregator.Aggregate(
            [
                CreateCheck("Brand Name", VerificationStatus.Pass),
                CreateCheck("Alcohol By Volume", VerificationStatus.Fail),
                CreateCheck("Net Contents", VerificationStatus.Pass)
            ]);

        Assert.Equal(
            VerificationStatus.Fail,
            result.OverallStatus);

        Assert.Equal(1, result.FailCount);
    }

    [Fact]
    public void Aggregate_WhenFailAndReviewExist_ReturnsFail()
    {
        var result =
            _aggregator.Aggregate(
            [
                CreateCheck("Brand Name", VerificationStatus.Review),
                CreateCheck("Alcohol By Volume", VerificationStatus.Fail),
                CreateCheck(
                    "Government Health Warning",
                    VerificationStatus.Review)
            ]);

        // FAIL has higher aggregate precedence than REVIEW.
        Assert.Equal(
            VerificationStatus.Fail,
            result.OverallStatus);

        Assert.Equal(1, result.FailCount);
        Assert.Equal(2, result.ReviewCount);
    }

    [Fact]
    public void Aggregate_WithNoChecks_ReturnsReview()
    {
        var result =
            _aggregator.Aggregate([]);

        Assert.Equal(
            VerificationStatus.Review,
            result.OverallStatus);

        Assert.Empty(result.Checks);
    }

    [Fact]
    public void Aggregate_PreservesIndividualChecksAndOrder()
    {
        var brand =
            CreateCheck(
                "Brand Name",
                VerificationStatus.Pass);

        var warning =
            CreateCheck(
                "Government Health Warning",
                VerificationStatus.Review);

        var result =
            _aggregator.Aggregate(
            [
                brand,
                warning
            ]);

        Assert.Equal(2, result.Checks.Count);
        Assert.Same(brand, result.Checks[0]);
        Assert.Same(warning, result.Checks[1]);
    }

    [Fact]
    public void Aggregate_WithNullCollection_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => _aggregator.Aggregate(null!));
    }

    private static VerificationCheckResult CreateCheck(
        string field,
        VerificationStatus status)
    {
        return new VerificationCheckResult
        {
            Field = field,
            Status = status,
            Explanation = $"Test {status} result."
        };
    }
}