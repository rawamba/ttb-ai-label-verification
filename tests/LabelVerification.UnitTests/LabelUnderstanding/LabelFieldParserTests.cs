using LabelVerification.Application.LabelUnderstanding;

namespace LabelVerification.UnitTests.LabelUnderstanding;

public sealed class LabelFieldParserTests
{
    private readonly LabelFieldParser _parser = new();

    [Fact]
    public void Parse_WithStandardAlcoholByVolume_ParsesValueAndEvidence()
    {
        var ocrResult = CreateOcrResult(
            "4% ALCOHOL BY VOLUME",
            [
                new OcrWord
                {
                    Text = "4%",
                    Confidence = 0.977
                },
                new OcrWord
                {
                    Text = "ALCOHOL",
                    Confidence = 0.995
                },
                new OcrWord
                {
                    Text = "BY",
                    Confidence = 0.996
                },
                new OcrWord
                {
                    Text = "VOLUME",
                    Confidence = 0.998
                }
            ]);

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.AlcoholByVolume);
        Assert.Equal(4.0m, result.AlcoholByVolume.Value);
        Assert.Equal(
            "4% ALCOHOL BY VOLUME",
            result.AlcoholByVolume.Evidence);

        Assert.InRange(
            result.AlcoholByVolume.Confidence,
            0.0,
            1.0);
    }

    [Fact]
    public void Parse_WithDecimalAlcoholByVolume_ParsesDecimalValue()
    {
        var ocrResult = CreateOcrResult(
            "12.5% ALCOHOL BY VOLUME");

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.AlcoholByVolume);
        Assert.Equal(
            12.5m,
            result.AlcoholByVolume.Value);
    }

    [Fact]
    public void Parse_WithAbbreviatedAlcoholDeclaration_ParsesValue()
    {
        var ocrResult = CreateOcrResult(
            "13.2% ALC. BY VOL.");

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.AlcoholByVolume);
        Assert.Equal(
            13.2m,
            result.AlcoholByVolume.Value);
    }

    [Fact]
    public void Parse_WithMixedCaseAlcoholDeclaration_ParsesValue()
    {
        var ocrResult = CreateOcrResult(
            "4% Alcohol by Volume");

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.AlcoholByVolume);
        Assert.Equal(
            4.0m,
            result.AlcoholByVolume.Value);
    }

    [Fact]
    public void Parse_WithoutAlcoholDeclaration_ReturnsNullAlcoholByVolume()
    {
        var ocrResult = CreateOcrResult(
            "Example Golden Ale 500 ML");

        var result = _parser.Parse(ocrResult);

        Assert.Null(result.AlcoholByVolume);
    }

    [Fact]
    public void Parse_WithStandardProofDeclaration_ParsesProof()
    {
        var ocrResult = CreateOcrResult(
            "90 PROOF",
            [
                new OcrWord
                {
                    Text = "90",
                    Confidence = 0.995
                },
                new OcrWord
                {
                    Text = "PROOF",
                    Confidence = 0.997
                }
            ]);

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.Proof);
        Assert.Equal(90, result.Proof.Value);
        Assert.Equal("90 PROOF", result.Proof.Evidence);

        Assert.InRange(
            result.Proof.Confidence,
            0.0,
            1.0);
    }

    [Fact]
    public void Parse_WithDegreeSymbolProofDeclaration_ParsesProof()
    {
        var ocrResult = CreateOcrResult(
            "100\u00B0 PROOF");

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.Proof);
        Assert.Equal(100, result.Proof.Value);
    }

    [Fact]
    public void Parse_WithMixedCaseProofDeclaration_ParsesProof()
    {
        var ocrResult = CreateOcrResult(
            "86 Proof");

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.Proof);
        Assert.Equal(86, result.Proof.Value);
    }

    [Fact]
    public void Parse_WithoutExplicitProofDeclaration_ReturnsNullProof()
    {
        var ocrResult = CreateOcrResult(
            "45% ALCOHOL BY VOLUME");

        var result = _parser.Parse(ocrResult);

        Assert.Null(result.Proof);
    }
    [Fact]
    public void Parse_WithMetricNetContents_ParsesMilliliters()
    {
        var ocrResult = CreateOcrResult(
            "750 ML");

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.NetContents);
        Assert.Equal(750m, result.NetContents.Value.Value);
        Assert.Equal("mL", result.NetContents.Value.Unit);
        Assert.Equal("750 ML", result.NetContents.Evidence);
    }

    [Fact]
    public void Parse_WithLiters_ParsesLiters()
    {
        var ocrResult = CreateOcrResult(
            "1 L");

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.NetContents);
        Assert.Equal(1m, result.NetContents.Value.Value);
        Assert.Equal("L", result.NetContents.Value.Unit);
    }

    [Fact]
    public void Parse_WithFluidOunces_ParsesFluidOunces()
    {
        var ocrResult = CreateOcrResult(
            "12 FL OZ");

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.NetContents);
        Assert.Equal(12m, result.NetContents.Value.Value);
        Assert.Equal("fl oz", result.NetContents.Value.Unit);
    }

    [Fact]
    public void Parse_WithPint_ParsesPint()
    {
        var ocrResult = CreateOcrResult(
            "1 PINT");

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.NetContents);
        Assert.Equal(1m, result.NetContents.Value.Value);
        Assert.Equal("pt", result.NetContents.Value.Unit);
    }

    [Fact]
    public void Parse_WithCustomaryAndMetricValues_PrefersMetricValue()
    {
        var ocrResult = CreateOcrResult(
            "1 PINT 0.9 FL OZ (500 ML)");

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.NetContents);
        Assert.Equal(500m, result.NetContents.Value.Value);
        Assert.Equal("mL", result.NetContents.Value.Unit);
        Assert.Equal("500 ML", result.NetContents.Evidence);
    }

    [Fact]
    public void Parse_WithoutNetContents_ReturnsNullNetContents()
    {
        var ocrResult = CreateOcrResult(
            "Example Golden Ale");

        var result = _parser.Parse(ocrResult);

        Assert.Null(result.NetContents);
    }
    [Fact]
    public void Parse_WithGovernmentWarning_ParsesCompleteWarning()
    {
        const string warning =
            "GOVERNMENT WARNING: (1) According to the Surgeon General, " +
            "women should not drink alcoholic beverages during pregnancy " +
            "because of the risk of birth defects. " +
            "(2) Consumption of alcoholic beverages impairs your ability " +
            "to drive a car or operate machinery, and may cause health problems.";

        var ocrResult = CreateOcrResult(warning);

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.GovernmentWarning);
        Assert.Equal(warning, result.GovernmentWarning.Value);
        Assert.Equal(warning, result.GovernmentWarning.Evidence);

        Assert.InRange(
            result.GovernmentWarning.Confidence,
            0.0,
            1.0);
    }

    [Fact]
    public void Parse_WithMultilineGovernmentWarning_NormalizesWhitespace()
    {
        var ocrResult = CreateOcrResult(
            """
            GOVERNMENT WARNING:
            (1) According to the Surgeon General,
            women should not drink alcoholic beverages during pregnancy
            because of the risk of birth defects.
            (2) Consumption of alcoholic beverages impairs your ability
            to drive a car or operate machinery, and may cause
            health problems.
            """);

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.GovernmentWarning);

        Assert.StartsWith(
            "GOVERNMENT WARNING:",
            result.GovernmentWarning.Value);

        Assert.Contains(
            "(1) According to the Surgeon General",
            result.GovernmentWarning.Value);

        Assert.Contains(
            "(2) Consumption of alcoholic beverages",
            result.GovernmentWarning.Value);

        Assert.EndsWith(
            "health problems.",
            result.GovernmentWarning.Value);
    }

    [Fact]
    public void Parse_WithCaseVariationGovernmentWarning_PreservesObservedText()
    {
        const string warning =
            "Government Warning: (1) According to the Surgeon General, " +
            "women should not drink alcoholic beverages during pregnancy " +
            "because of the risk of birth defects. " +
            "(2) Consumption of alcoholic beverages impairs your ability " +
            "to drive a car or operate machinery, and may cause health problems.";

        var ocrResult = CreateOcrResult(warning);

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.GovernmentWarning);

        // Parsing is case-insensitive for discovery, but the original OCR
        // evidence is preserved for later exact regulatory validation.
        Assert.StartsWith(
            "Government Warning:",
            result.GovernmentWarning.Value);
    }

    [Fact]
    public void Parse_WithIncompleteGovernmentWarning_PreservesObservedEvidence()
    {
        const string warning =
            "GOVERNMENT WARNING: (1) According to the Surgeon General, " +
            "women should not drink alcoholic beverages during pregnancy";

        var ocrResult = CreateOcrResult(warning);

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.GovernmentWarning);
        Assert.Equal(warning, result.GovernmentWarning.Value);
    }

    [Fact]
    public void Parse_WithoutGovernmentWarning_ReturnsNullGovernmentWarning()
    {
        var ocrResult = CreateOcrResult(
            "Example Golden Ale 500 ML 4% ALCOHOL BY VOLUME");

        var result = _parser.Parse(ocrResult);

        Assert.Null(result.GovernmentWarning);
    }
    [Fact]
    public void Parse_WithAspartameDeclaration_DetectsAspartame()
    {
        var result = _parser.Parse(
            CreateOcrResult("CONTAINS ASPARTAME"));

        var declaration = Assert.Single(
            result.IngredientDeclarations);

        Assert.Equal(
            IngredientDeclarationType.Aspartame,
            declaration.Type);

        Assert.Equal(
            "ASPARTAME",
            declaration.Evidence);
    }

    [Fact]
    public void Parse_WithSulfitesDeclaration_DetectsSulfites()
    {
        var result = _parser.Parse(
            CreateOcrResult("CONTAINS SULFITES"));

        var declaration = Assert.Single(
            result.IngredientDeclarations);

        Assert.Equal(
            IngredientDeclarationType.Sulfites,
            declaration.Type);

        Assert.Equal(
            "CONTAINS SULFITES",
            declaration.Evidence);
    }

    [Fact]
    public void Parse_WithFdAndCYellow5Declaration_DetectsYellow5()
    {
        var result = _parser.Parse(
            CreateOcrResult("CONTAINS FD&C YELLOW #5"));

        var declaration = Assert.Single(
            result.IngredientDeclarations);

        Assert.Equal(
            IngredientDeclarationType.FdAndCYellow5,
            declaration.Type);

        Assert.Equal(
            "FD&C YELLOW #5",
            declaration.Evidence);
    }

    [Fact]
    public void Parse_WithFdAndCYellowNo5Declaration_DetectsYellow5()
    {
        var result = _parser.Parse(
            CreateOcrResult("FD&C Yellow No. 5"));

        var declaration = Assert.Single(
            result.IngredientDeclarations);

        Assert.Equal(
            IngredientDeclarationType.FdAndCYellow5,
            declaration.Type);
    }

    [Fact]
    public void Parse_WithCochinealExtractDeclaration_DetectsCochineal()
    {
        var result = _parser.Parse(
            CreateOcrResult("CONTAINS COCHINEAL EXTRACT"));

        var declaration = Assert.Single(
            result.IngredientDeclarations);

        Assert.Equal(
            IngredientDeclarationType.CochinealOrCarmine,
            declaration.Type);

        Assert.Equal(
            "COCHINEAL EXTRACT",
            declaration.Evidence);
    }

    [Fact]
    public void Parse_WithCarmineDeclaration_DetectsCarmine()
    {
        var result = _parser.Parse(
            CreateOcrResult("CONTAINS CARMINE"));

        var declaration = Assert.Single(
            result.IngredientDeclarations);

        Assert.Equal(
            IngredientDeclarationType.CochinealOrCarmine,
            declaration.Type);

        Assert.Equal(
            "CARMINE",
            declaration.Evidence);
    }

    [Fact]
    public void Parse_WithMultipleIngredientDeclarations_ReturnsAllDetectedTypes()
    {
        var result = _parser.Parse(
            CreateOcrResult(
                "CONTAINS ASPARTAME, SULFITES, AND FD&C YELLOW #5"));

        Assert.Equal(
            3,
            result.IngredientDeclarations.Count);

        Assert.Contains(
            result.IngredientDeclarations,
            declaration =>
                declaration.Type ==
                IngredientDeclarationType.Aspartame);

        Assert.Contains(
            result.IngredientDeclarations,
            declaration =>
                declaration.Type ==
                IngredientDeclarationType.Sulfites);

        Assert.Contains(
            result.IngredientDeclarations,
            declaration =>
                declaration.Type ==
                IngredientDeclarationType.FdAndCYellow5);
    }

    [Fact]
    public void Parse_WithoutIngredientDeclarations_ReturnsEmptyCollection()
    {
        var result = _parser.Parse(
            CreateOcrResult(
                "Example Golden Ale 500 ML 4% ALCOHOL BY VOLUME"));

        Assert.Empty(
            result.IngredientDeclarations);
    }
    [Fact]
    public void Parse_WithGoldenAle_ParsesClassType()
    {
        var ocrResult = CreateOcrResult(
            "GOLDEN ALE");

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.ClassType);
        Assert.Equal(
            "GOLDEN ALE",
            result.ClassType.Value);

        Assert.Equal(
            "GOLDEN ALE",
            result.ClassType.Evidence);
    }

    [Fact]
    public void Parse_WithBourbonDeclaration_PreservesCompleteClassType()
    {
        var ocrResult = CreateOcrResult(
            "KENTUCKY STRAIGHT BOURBON WHISKEY");

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.ClassType);

        Assert.Equal(
            "KENTUCKY STRAIGHT BOURBON WHISKEY",
            result.ClassType.Value);
    }

    [Fact]
    public void Parse_WithMixedCaseClassType_PreservesObservedText()
    {
        var ocrResult = CreateOcrResult(
            "London Dry Gin");

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.ClassType);

        // Discovery is case-insensitive, but OCR evidence is preserved.
        Assert.Equal(
            "London Dry Gin",
            result.ClassType.Value);
    }

    [Fact]
    public void Parse_WithSimpleVodkaDeclaration_ParsesClassType()
    {
        var ocrResult = CreateOcrResult(
            "VODKA");

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.ClassType);
        Assert.Equal(
            "VODKA",
            result.ClassType.Value);
    }

    [Fact]
    public void Parse_WithoutRecognizedClassType_ReturnsNullClassType()
    {
        var ocrResult = CreateOcrResult(
            "Example Beverage Company");

        var result = _parser.Parse(ocrResult);

        Assert.Null(result.ClassType);
    }
    [Fact]
    public void Parse_WithRepresentativeLabel_ParsesExampleAsBrandName()
    {
        var ocrResult = CreateOcrResultWithLines(
            "ARLINGTON, VIRGINIA",
            "Fake Brewery Name",
            "Example",
            "GOLDEN ALE",
            "1 PINT 0.9 FL OZ (500 ML)",
            "4% ALCOHOL BY VOLUME");

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.BrandName);
        Assert.Equal(
            "Example",
            result.BrandName.Value);

        Assert.Equal(
            "Example",
            result.BrandName.Evidence);
    }

    [Fact]
    public void Parse_WithBrandAndClassType_PreservesBrandText()
    {
        var ocrResult = CreateOcrResultWithLines(
            "STONE'S THROW",
            "KENTUCKY STRAIGHT BOURBON WHISKEY",
            "45% ALCOHOL BY VOLUME",
            "90 PROOF");

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.BrandName);

        Assert.Equal(
            "STONE'S THROW",
            result.BrandName.Value);
    }

    [Fact]
    public void Parse_BrandDiscovery_IsNotConfusedByProducerInformation()
    {
        var ocrResult = CreateOcrResultWithLines(
            "BOTTLED BY EXAMPLE DISTILLERY",
            "1234 MAIN STREET",
            "LOUISVILLE, KENTUCKY",
            "OLD TOM",
            "BOURBON WHISKEY");

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.BrandName);

        Assert.Equal(
            "OLD TOM",
            result.BrandName.Value);
    }

    [Fact]
    public void Parse_BrandDiscovery_IgnoresMarketingInstructions()
    {
        var ocrResult = CreateOcrResultWithLines(
            "STORE COLD",
            "DRINK FRESH",
            "EXAMPLE",
            "GOLDEN ALE",
            "DRINK RESPONSIBLY.");

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.BrandName);

        Assert.Equal(
            "EXAMPLE",
            result.BrandName.Value);
    }

    [Fact]
    public void Parse_WithoutPlausibleBrandCandidate_ReturnsNullBrandName()
    {
        var ocrResult = CreateOcrResultWithLines(
            "GOLDEN ALE",
            "500 ML",
            "4% ALCOHOL BY VOLUME",
            "GOVERNMENT WARNING:");

        var result = _parser.Parse(ocrResult);

        Assert.Null(result.BrandName);
    }

    private static OcrResult CreateOcrResultWithLines(
        params string[] lines)
    {
        return new OcrResult
        {
            Text = string.Join(
                Environment.NewLine,
                lines),

            Lines = lines
                .Select(line => new OcrTextLine
                {
                    Text = line
                })
                .ToArray(),

            Words = [],

            Confidence = 0.95,

            Duration = TimeSpan.FromMilliseconds(100),

            Provider = "TestOCR",

            ModelVersion = "test"
        };
    }
    [Fact]
    public void Parse_WithProducerAndLocation_ParsesNameAndAddress()
    {
        var ocrResult = CreateOcrResultWithLines(
            "ARLINGTON, VIRGINIA",
            "Fake Brewery Name",
            "Example",
            "GOLDEN ALE");

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.NameAndAddress);

        Assert.Equal(
            "Fake Brewery Name",
            result.NameAndAddress.Value.BusinessName);

        Assert.Equal(
            "ARLINGTON",
            result.NameAndAddress.Value.City,
            ignoreCase: true);

        Assert.Equal(
            "VIRGINIA",
            result.NameAndAddress.Value.State,
            ignoreCase: true);
    }

    [Fact]
    public void Parse_WithFullProducerAddress_ParsesStructuredComponents()
    {
        var ocrResult = CreateOcrResultWithLines(
            "BOTTLED BY EXAMPLE DISTILLERY",
            "1234 MAIN STREET",
            "LOUISVILLE, KENTUCKY 40202",
            "OLD TOM",
            "BOURBON WHISKEY");

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.NameAndAddress);

        Assert.Equal(
            "EXAMPLE DISTILLERY",
            result.NameAndAddress.Value.BusinessName);

        Assert.Equal(
            "1234 MAIN STREET",
            result.NameAndAddress.Value.StreetAddress);

        Assert.Equal(
            "LOUISVILLE",
            result.NameAndAddress.Value.City);

        Assert.Equal(
            "KENTUCKY",
            result.NameAndAddress.Value.State);

        Assert.Equal(
            "40202",
            result.NameAndAddress.Value.PostalCode);
    }

    [Fact]
    public void Parse_WithCombinedLocationAndBusinessLine_ParsesBoth()
    {
        // Mirrors the segmentation observed in the representative Azure OCR
        // result where location and brewery name appeared on one OCR line.
        var ocrResult = CreateOcrResultWithLines(
            "ARLINGTON, VIRGINIA Fake Brewery Name",
            "Example",
            "GOLDEN ALE");

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.NameAndAddress);

        Assert.Equal(
            "Fake Brewery Name",
            result.NameAndAddress.Value.BusinessName);

        Assert.Equal(
            "ARLINGTON",
            result.NameAndAddress.Value.City,
            ignoreCase: true);

        Assert.Equal(
            "VIRGINIA",
            result.NameAndAddress.Value.State,
            ignoreCase: true);
    }

    [Fact]
    public void Parse_NameAndAddress_PreservesRawEvidence()
    {
        var ocrResult = CreateOcrResultWithLines(
            "BOTTLED BY EXAMPLE DISTILLERY",
            "1234 MAIN STREET",
            "LOUISVILLE, KY 40202");

        var result = _parser.Parse(ocrResult);

        Assert.NotNull(result.NameAndAddress);

        Assert.Contains(
            "BOTTLED BY EXAMPLE DISTILLERY",
            result.NameAndAddress.Value.RawText);

        Assert.Contains(
            "1234 MAIN STREET",
            result.NameAndAddress.Value.RawText);

        Assert.Contains(
            "LOUISVILLE, KY 40202",
            result.NameAndAddress.Value.RawText);
    }

    [Fact]
    public void Parse_WithoutProducerOrBusinessEvidence_ReturnsNullNameAndAddress()
    {
        var ocrResult = CreateOcrResultWithLines(
            "Example",
            "GOLDEN ALE",
            "500 ML",
            "4% ALCOHOL BY VOLUME");

        var result = _parser.Parse(ocrResult);

        Assert.Null(result.NameAndAddress);
    }
    [Fact]
    public void Parse_WithProductOfDeclaration_ParsesCountryOfOrigin()
    {
        var result = _parser.Parse(
            CreateOcrResultWithLines(
                "PRODUCT OF FRANCE"));

        Assert.NotNull(result.CountryOfOrigin);

        Assert.Equal(
            "FRANCE",
            result.CountryOfOrigin.Value);

        Assert.Equal(
            "PRODUCT OF FRANCE",
            result.CountryOfOrigin.Evidence);
    }

    [Fact]
    public void Parse_WithProducedInDeclaration_ParsesCountryOfOrigin()
    {
        var result = _parser.Parse(
            CreateOcrResultWithLines(
                "PRODUCED IN ITALY"));

        Assert.NotNull(result.CountryOfOrigin);

        Assert.Equal(
            "ITALY",
            result.CountryOfOrigin.Value);
    }

    [Fact]
    public void Parse_WithMadeInDeclaration_ParsesCountryOfOrigin()
    {
        var result = _parser.Parse(
            CreateOcrResultWithLines(
                "Made in Mexico"));

        Assert.NotNull(result.CountryOfOrigin);

        // Discovery is case-insensitive, but observed OCR casing is preserved.
        Assert.Equal(
            "Mexico",
            result.CountryOfOrigin.Value);
    }

    [Fact]
    public void Parse_WithCountryOfOriginPrefix_ParsesCountry()
    {
        var result = _parser.Parse(
            CreateOcrResultWithLines(
                "COUNTRY OF ORIGIN: SPAIN"));

        Assert.NotNull(result.CountryOfOrigin);

        Assert.Equal(
            "SPAIN",
            result.CountryOfOrigin.Value);
    }

    [Fact]
    public void Parse_WithMultiWordCountry_ParsesCompleteCountryName()
    {
        var result = _parser.Parse(
            CreateOcrResultWithLines(
                "PRODUCT OF SOUTH AFRICA"));

        Assert.NotNull(result.CountryOfOrigin);

        Assert.Equal(
            "SOUTH AFRICA",
            result.CountryOfOrigin.Value);
    }

    [Fact]
    public void Parse_WithOriginAndImporterOnSameLine_StopsBeforeImporter()
    {
        var result = _parser.Parse(
            CreateOcrResultWithLines(
                "PRODUCT OF FRANCE IMPORTED BY ABC IMPORTS"));

        Assert.NotNull(result.CountryOfOrigin);

        Assert.Equal(
            "FRANCE",
            result.CountryOfOrigin.Value);

        Assert.Equal(
            "PRODUCT OF FRANCE",
            result.CountryOfOrigin.Evidence);
    }

    [Fact]
    public void Parse_WithImporterAddressOnly_DoesNotInferCountryOfOrigin()
    {
        var result = _parser.Parse(
            CreateOcrResultWithLines(
                "IMPORTED BY ABC IMPORTS",
                "123 MAIN STREET",
                "NEW YORK, NY 10001"));

        Assert.Null(result.CountryOfOrigin);
    }

    [Fact]
    public void Parse_WithoutOriginDeclaration_ReturnsNullCountryOfOrigin()
    {
        var result = _parser.Parse(
            CreateOcrResultWithLines(
                "Example",
                "GOLDEN ALE",
                "500 ML"));

        Assert.Null(result.CountryOfOrigin);
    }
    private static OcrResult CreateOcrResult(
        string text,
        IReadOnlyList<OcrWord>? words = null)
    {
        return new OcrResult
        {
            Text = text,

            Lines =
            [
                new OcrTextLine
                {
                    Text = text
                }
            ],

            Words = words ?? [],

            Confidence = 0.95,

            Duration = TimeSpan.FromMilliseconds(100),

            Provider = "TestOCR",

            ModelVersion = "test"
        };
    }
}








