using LabelVerification.Application.LabelUnderstanding;
using LabelVerification.IntegrationTests.TestSupport;

namespace LabelVerification.IntegrationTests.LabelUnderstanding;

/// <summary>
/// Exercises the real structured parser against one cohesive OCR result rather
/// than testing individual regular expressions in isolation.
/// </summary>
public sealed class LabelFieldParserIntegrationTests
{
    [Fact]
    public void Parse_WithCompleteCompliantEvidence_ProducesExpectedStructuredFields()
    {
        // Arrange
        var parser =
            new LabelFieldParser();

        var ocrResult =
            IntegrationTestSupport.CreateCompliantOcrResult();

        // Act
        var result =
            parser.Parse(
                ocrResult);

        // Assert brand and class/type evidence.
        Assert.NotNull(result.BrandName);
        Assert.Equal(
            "OLD TOM DISTILLERY",
            result.BrandName.Value);

        Assert.NotNull(result.ClassType);
        Assert.Equal(
            "KENTUCKY STRAIGHT BOURBON WHISKEY",
            result.ClassType.Value);

        // Assert alcohol declarations.
        Assert.NotNull(result.AlcoholByVolume);
        Assert.Equal(
            45m,
            result.AlcoholByVolume.Value);

        Assert.NotNull(result.Proof);
        Assert.Equal(
            90,
            result.Proof.Value);

        // Assert declared contents.
        Assert.NotNull(result.NetContents);
        Assert.Equal(
            750m,
            result.NetContents.Value.Value);

        Assert.Equal(
            "mL",
            result.NetContents.Value.Unit);

        // Assert producer/location evidence is parsed separately from brand.
        Assert.NotNull(result.NameAndAddress);

        Assert.Equal(
            "OLD TOM DISTILLERY",
            result.NameAndAddress.Value.BusinessName);

        Assert.Equal(
            "FRANKFORT",
            result.NameAndAddress.Value.City);

        Assert.Equal(
            "KENTUCKY",
            result.NameAndAddress.Value.State);

        // Assert the complete Government Warning survives structured parsing.
        Assert.NotNull(result.GovernmentWarning);

        Assert.Contains(
            "GOVERNMENT WARNING:",
            result.GovernmentWarning.Value,
            StringComparison.Ordinal);

        Assert.Contains(
            "may cause health problems.",
            result.GovernmentWarning.Value,
            StringComparison.Ordinal);
    }
}