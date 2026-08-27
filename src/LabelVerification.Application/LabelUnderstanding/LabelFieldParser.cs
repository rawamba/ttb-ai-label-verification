using System.Globalization;
using System.Text.RegularExpressions;

namespace LabelVerification.Application.LabelUnderstanding;

/// <summary>
/// Deterministically converts OCR evidence into structured alcohol-label
/// fields.
///
/// This parser answers "What does the label appear to say?" It does not
/// determine regulatory compliance and never returns PASS, REVIEW, or FAIL.
/// </summary>
public sealed partial class LabelFieldParser : ILabelFieldParser
{
    private static readonly string[] UsStateTokens =
    [
        "DISTRICT OF COLUMBIA",
        "NORTH CAROLINA",
        "SOUTH CAROLINA",
        "WEST VIRGINIA",
        "NEW HAMPSHIRE",
        "NEW JERSEY",
        "NEW MEXICO",
        "NEW YORK",
        "NORTH DAKOTA",
        "SOUTH DAKOTA",
        "RHODE ISLAND",
        "MASSACHUSETTS",
        "PENNSYLVANIA",
        "CONNECTICUT",
        "MISSISSIPPI",
        "TENNESSEE",
        "CALIFORNIA",
        "WASHINGTON",
        "WISCONSIN",
        "MINNESOTA",
        "LOUISIANA",
        "KENTUCKY",
        "VIRGINIA",
        "MARYLAND",
        "MICHIGAN",
        "MISSOURI",
        "MONTANA",
        "NEBRASKA",
        "OKLAHOMA",
        "ARKANSAS",
        "COLORADO",
        "DELAWARE",
        "FLORIDA",
        "GEORGIA",
        "ILLINOIS",
        "INDIANA",
        "OREGON",
        "VERMONT",
        "ALABAMA",
        "ALASKA",
        "ARIZONA",
        "HAWAII",
        "IDAHO",
        "IOWA",
        "KANSAS",
        "MAINE",
        "NEVADA",
        "OHIO",
        "TEXAS",
        "UTAH",
        "WYOMING",

        "AL", "AK", "AZ", "AR", "CA", "CO", "CT", "DE",
        "FL", "GA", "HI", "ID", "IL", "IN", "IA", "KS",
        "KY", "LA", "ME", "MD", "MA", "MI", "MN", "MS",
        "MO", "MT", "NE", "NV", "NH", "NJ", "NM", "NY",
        "NC", "ND", "OH", "OK", "OR", "PA", "RI", "SC",
        "SD", "TN", "TX", "UT", "VT", "VA", "WA", "WV",
        "WI", "WY", "DC"
    ];

    /// <inheritdoc />
    public ParsedLabelData Parse(OcrResult ocrResult)
    {
        ArgumentNullException.ThrowIfNull(ocrResult);

        return new ParsedLabelData
        {
            BrandName = ParseBrandName(ocrResult),
            NameAndAddress = ParseNameAndAddress(ocrResult),
            CountryOfOrigin = ParseCountryOfOrigin(ocrResult),
            ClassType = ParseClassType(ocrResult),
            AlcoholByVolume = ParseAlcoholByVolume(ocrResult),
            Proof = ParseProof(ocrResult),
            NetContents = ParseNetContents(ocrResult),
            GovernmentWarning = ParseGovernmentWarning(ocrResult),
            IngredientDeclarations = ParseIngredientDeclarations(ocrResult)
        };
    }

    /// <summary>
    /// Identifies a likely brand-name declaration from OCR lines.
    ///
    /// Brand names generally do not have a standard textual prefix, so this
    /// method uses conservative exclusion rules. Regulatory ambiguity is
    /// resolved later by verification and human review.
    /// </summary>
    private static ParsedLabelField<string>? ParseBrandName(
        OcrResult ocrResult)
    {
        var governmentWarning =
            ParseGovernmentWarning(ocrResult);

        var governmentWarningEvidence =
            governmentWarning?.Evidence;

        var lines =
            ocrResult.Lines
                .Select(line =>
                    NormalizeWhitespace(line.Text))
                .Where(line =>
                    !string.IsNullOrWhiteSpace(line))
                .ToArray();

        for (var index = 0;
             index < lines.Length;
             index++)
        {
            var text =
                lines[index];

            // Government Warning body text must never become a brand
            // candidate. This specifically prevents trailing warning text
            // such as "health problems." from being selected.
            if (!string.IsNullOrWhiteSpace(
                    governmentWarningEvidence) &&
                governmentWarningEvidence.Contains(
                    text,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!IsPotentialBrandName(text))
            {
                continue;
            }

            // Business terminology such as Brewery, Distillery, or Company
            // can legitimately occur in a brand name. Only treat it as
            // producer/business evidence when nearby OCR lines provide
            // supporting address, location, or producer context.
            if (ProducerOrBusinessRegex().IsMatch(text) &&
                HasNearbyProducerContext(
                    lines,
                    index))
            {
                continue;
            }

            return new ParsedLabelField<string>
            {
                Value = text,
                Evidence = text,
                Confidence = CalculateEvidenceConfidence(
                    text,
                    ocrResult)
            };
        }

        return null;
    }

    private static bool HasNearbyProducerContext(
        IReadOnlyList<string> lines,
        int candidateIndex)
    {
        const int neighborhood =
            2;

        var start =
            Math.Max(
                0,
                candidateIndex - neighborhood);

        var end =
            Math.Min(
                lines.Count - 1,
                candidateIndex + neighborhood);

        for (var index = start;
             index <= end;
             index++)
        {
            if (index == candidateIndex)
            {
                continue;
            }

            var nearby =
                lines[index];

            if (ProducerPrefixRegex().IsMatch(
                    nearby) ||
                AddressLikeRegex().IsMatch(
                    nearby))
            {
                return true;
            }

            if (TryParseLocationLine(
                    nearby,
                    out _,
                    out _,
                    out _,
                    out _))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPotentialBrandName(string text)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            text.Length < 2 ||
            text.Length > 80)
        {
            return false;
        }

        if (AlcoholByVolumeRegex().IsMatch(text) ||
            ProofRegex().IsMatch(text) ||
            MetricNetContentsRegex().IsMatch(text) ||
            FluidOunceNetContentsRegex().IsMatch(text) ||
            PintNetContentsRegex().IsMatch(text) ||
            ClassTypeKeywordRegex().IsMatch(text))
        {
            return false;
        }

        if (text.Contains(
                "GOVERNMENT WARNING",
                StringComparison.OrdinalIgnoreCase) ||
            text.Contains(
                "SURGEON GENERAL",
                StringComparison.OrdinalIgnoreCase) ||
            text.Contains(
                "ALCOHOLIC BEVERAGES",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (AspartameRegex().IsMatch(text) ||
            SulfitesRegex().IsMatch(text) ||
            FdAndCYellow5Regex().IsMatch(text) ||
            CochinealOrCarmineRegex().IsMatch(text))
        {
            return false;
        }

        if (ProducerPrefixRegex().IsMatch(text) ||
            AddressLikeRegex().IsMatch(text) ||
            LooksLikeCityStateLine(text))
        {
            return false;
        }

        if (MarketingInstructionRegex().IsMatch(text))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Extracts producer, bottler, importer, or similar name-and-address
    /// evidence from nearby OCR lines.
    ///
    /// Raw evidence is always retained. Structured components are populated
    /// only when they can be identified conservatively.
    /// </summary>
    private static ParsedLabelField<ParsedNameAndAddress>?
        ParseNameAndAddress(
            OcrResult ocrResult)
    {
        var lines = ocrResult.Lines
            .Select(line => NormalizeWhitespace(line.Text))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (lines.Length == 0)
        {
            return null;
        }

        // Prefer explicit producer declarations such as "BOTTLED BY",
        // "DISTILLED BY", or "IMPORTED BY". Generic business terminology
        // such as "DISTILLERY" may legitimately be part of a brand name,
        // so use it only as a fallback when no explicit declaration exists.
        var businessIndex =
            Array.FindIndex(
                lines,
                line =>
                    ProducerPrefixRegex().IsMatch(line));

        if (businessIndex < 0)
        {
            businessIndex =
                Array.FindIndex(
                    lines,
                    line =>
                        ProducerOrBusinessRegex().IsMatch(line));
        }

        if (businessIndex < 0)
        {
            return null;
        }

        var businessLine = lines[businessIndex];

        string? businessName = null;
        string? streetAddress = null;
        string? city = null;
        string? state = null;
        string? postalCode = null;

        // Azure may combine location and business information onto a single
        // OCR line, for example:
        //
        // ARLINGTON, VIRGINIA Fake Brewery Name
        if (TryParseLocationLine(
                businessLine,
                out var combinedCity,
                out var combinedState,
                out var combinedPostalCode,
                out var trailingBusiness))
        {
            city = combinedCity;
            state = combinedState;
            postalCode = combinedPostalCode;

            if (!string.IsNullOrWhiteSpace(trailingBusiness))
            {
                businessName = trailingBusiness;
            }
        }

        businessName ??=
            NormalizeBusinessName(businessLine);

        // Restrict address discovery to a small neighborhood around the
        // business line so unrelated label text is not absorbed.
        var start = Math.Max(0, businessIndex - 2);
        var end = Math.Min(lines.Length - 1, businessIndex + 2);

        var evidenceLines = new List<string>();

        for (var index = start; index <= end; index++)
        {
            var line = lines[index];

            if (index == businessIndex)
            {
                evidenceLines.Add(line);
                continue;
            }

            if (TryParseLocationLine(
                    line,
                    out var parsedCity,
                    out var parsedState,
                    out var parsedPostal,
                    out _))
            {
                city ??= parsedCity;
                state ??= parsedState;
                postalCode ??= parsedPostal;

                evidenceLines.Add(line);
                continue;
            }

            if (streetAddress is null &&
                AddressLikeRegex().IsMatch(line))
            {
                streetAddress = line;
                evidenceLines.Add(line);
            }
        }

        if (!evidenceLines.Contains(
                businessLine,
                StringComparer.OrdinalIgnoreCase))
        {
            evidenceLines.Insert(0, businessLine);
        }

        var evidence = string.Join(
            Environment.NewLine,
            evidenceLines.Distinct(
                StringComparer.OrdinalIgnoreCase));

        return new ParsedLabelField<ParsedNameAndAddress>
        {
            Value = new ParsedNameAndAddress
            {
                RawText = evidence,
                BusinessName = businessName,
                StreetAddress = streetAddress,
                City = city,
                State = state,
                PostalCode = postalCode,
                Country = null
            },

            Evidence = evidence,

            Confidence = CalculateEvidenceConfidence(
                evidence,
                ocrResult)
        };
    }

    private static string NormalizeBusinessName(
        string value)
    {
        return ProducerPrefixRegex()
            .Replace(value, string.Empty)
            .Trim();
    }

    private static bool LooksLikeCityStateLine(
        string value)
    {
        return TryParseLocationLine(
            value,
            out _,
            out _,
            out _,
            out var trailingText) &&
            string.IsNullOrWhiteSpace(trailingText);
    }

    /// <summary>
    /// Parses common U.S. city/state/postal patterns and optionally returns
    /// text following the location. The trailing text is useful when OCR
    /// combines location and business name onto one line.
    /// </summary>
    private static bool TryParseLocationLine(
        string value,
        out string? city,
        out string? state,
        out string? postalCode,
        out string? trailingText)
    {
        city = null;
        state = null;
        postalCode = null;
        trailingText = null;

        var commaIndex = value.IndexOf(',');

        if (commaIndex <= 0 ||
            commaIndex >= value.Length - 1)
        {
            return false;
        }

        var cityCandidate =
            value[..commaIndex].Trim();

        var remainder =
            value[(commaIndex + 1)..].Trim();

        if (string.IsNullOrWhiteSpace(cityCandidate) ||
            string.IsNullOrWhiteSpace(remainder))
        {
            return false;
        }

        foreach (var stateToken in UsStateTokens)
        {
            var exactState =
                remainder.Equals(
                    stateToken,
                    StringComparison.OrdinalIgnoreCase);

            var stateWithRemainder =
                remainder.StartsWith(
                    stateToken + " ",
                    StringComparison.OrdinalIgnoreCase);

            if (!exactState && !stateWithRemainder)
            {
                continue;
            }

            city = cityCandidate;

            // Preserve the casing observed by OCR.
            state = remainder[..stateToken.Length];

            var afterState =
                remainder[stateToken.Length..].Trim();

            if (string.IsNullOrWhiteSpace(afterState))
            {
                return true;
            }

            var postalMatch =
                PostalAndTrailingRegex().Match(afterState);

            if (postalMatch.Success)
            {
                postalCode =
                    postalMatch.Groups["postal"].Value;

                trailingText =
                    postalMatch.Groups["trailing"].Success
                        ? postalMatch.Groups["trailing"].Value.Trim()
                        : null;

                return true;
            }

            trailingText = afterState;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Extracts an explicitly stated country-of-origin declaration.
    ///
    /// Origin is intentionally not inferred from importer, producer, or
    /// distributor addresses. Only explicit statements such as
    /// "PRODUCT OF FRANCE" or "MADE IN MEXICO" are treated as origin evidence.
    /// Regulatory applicability is evaluated later by the verification layer.
    /// </summary>
    private static ParsedLabelField<string>? ParseCountryOfOrigin(
        OcrResult ocrResult)
    {
        foreach (var line in ocrResult.Lines)
        {
            var text = NormalizeWhitespace(line.Text);

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var match = CountryOfOriginRegex().Match(text);

            if (!match.Success)
            {
                continue;
            }

            var country =
                NormalizeCountryName(
                    match.Groups["country"].Value);

            if (string.IsNullOrWhiteSpace(country))
            {
                continue;
            }

            return new ParsedLabelField<string>
            {
                Value = country,
                Evidence = match.Value.Trim(),

                Confidence = CalculateEvidenceConfidence(
                    match.Value,
                    ocrResult)
            };
        }

        return null;
    }

    /// <summary>
    /// Performs only structural cleanup of a detected country name.
    /// It does not translate aliases or infer canonical geopolitical names.
    /// </summary>
    private static string NormalizeCountryName(
        string value)
    {
        return value
            .Trim()
            .TrimEnd('.', ',', ';', ':');
    }
    /// <summary>
    /// Identifies a likely beverage class/type declaration from OCR lines.
    /// The full observed line is preserved rather than reducing a phrase such
    /// as "KENTUCKY STRAIGHT BOURBON WHISKEY" to only "BOURBON."
    /// </summary>
    private static ParsedLabelField<string>? ParseClassType(
        OcrResult ocrResult)
    {
        foreach (var line in ocrResult.Lines)
        {
            var text = NormalizeWhitespace(line.Text);

            if (string.IsNullOrWhiteSpace(text) ||
                !ClassTypeKeywordRegex().IsMatch(text))
            {
                continue;
            }

            return new ParsedLabelField<string>
            {
                Value = text,
                Evidence = text,
                Confidence = CalculateEvidenceConfidence(
                    text,
                    ocrResult)
            };
        }

        return null;
    }

    /// <summary>
    /// Extracts explicitly printed alcohol-by-volume declarations.
    /// </summary>
    private static ParsedLabelField<decimal>? ParseAlcoholByVolume(
        OcrResult ocrResult)
    {
        foreach (var line in ocrResult.Lines)
        {
            var match =
                AlcoholByVolumeRegex().Match(line.Text);

            if (!match.Success)
            {
                continue;
            }

            if (!decimal.TryParse(
                    match.Groups["value"].Value,
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var value))
            {
                continue;
            }

            return new ParsedLabelField<decimal>
            {
                Value = value,
                Evidence = match.Value.Trim(),
                Confidence = CalculateEvidenceConfidence(
                    match.Value,
                    ocrResult)
            };
        }

        var textMatch =
            AlcoholByVolumeRegex().Match(ocrResult.Text);

        if (!textMatch.Success ||
            !decimal.TryParse(
                textMatch.Groups["value"].Value,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var textValue))
        {
            return null;
        }

        return new ParsedLabelField<decimal>
        {
            Value = textValue,
            Evidence = textMatch.Value.Trim(),
            Confidence = CalculateEvidenceConfidence(
                textMatch.Value,
                ocrResult)
        };
    }

    /// <summary>
    /// Extracts only explicitly printed proof declarations.
    /// Proof is not derived from ABV in the parsing layer.
    /// </summary>
    private static ParsedLabelField<int>? ParseProof(
        OcrResult ocrResult)
    {
        foreach (var line in ocrResult.Lines)
        {
            var match =
                ProofRegex().Match(line.Text);

            if (!match.Success)
            {
                continue;
            }

            if (!int.TryParse(
                    match.Groups["value"].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var value))
            {
                continue;
            }

            return new ParsedLabelField<int>
            {
                Value = value,
                Evidence = match.Value.Trim(),
                Confidence = CalculateEvidenceConfidence(
                    match.Value,
                    ocrResult)
            };
        }

        var textMatch =
            ProofRegex().Match(ocrResult.Text);

        if (!textMatch.Success ||
            !int.TryParse(
                textMatch.Groups["value"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var textValue))
        {
            return null;
        }

        return new ParsedLabelField<int>
        {
            Value = textValue,
            Evidence = textMatch.Value.Trim(),
            Confidence = CalculateEvidenceConfidence(
                textMatch.Value,
                ocrResult)
        };
    }

    /// <summary>
    /// Extracts net contents. Metric declarations are preferred when both
    /// customary and metric declarations appear.
    /// </summary>
    private static ParsedLabelField<ParsedNetContents>?
        ParseNetContents(
            OcrResult ocrResult)
    {
        foreach (var line in ocrResult.Lines)
        {
            var parsed =
                TryParseNetContents(
                    line.Text,
                    ocrResult);

            if (parsed is not null)
            {
                return parsed;
            }
        }

        return TryParseNetContents(
            ocrResult.Text,
            ocrResult);
    }

    private static ParsedLabelField<ParsedNetContents>?
        TryParseNetContents(
            string text,
            OcrResult ocrResult)
    {
        var metricMatch =
            MetricNetContentsRegex().Match(text);

        if (metricMatch.Success &&
            decimal.TryParse(
                metricMatch.Groups["value"].Value,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var metricValue))
        {
            var unit =
                NormalizeNetContentsUnit(
                    metricMatch.Groups["unit"].Value);

            return new ParsedLabelField<ParsedNetContents>
            {
                Value = new ParsedNetContents
                {
                    Value = metricValue,
                    Unit = unit
                },

                Evidence = metricMatch.Value.Trim(),

                Confidence = CalculateEvidenceConfidence(
                    metricMatch.Value,
                    ocrResult)
            };
        }

        var fluidOunceMatch =
            FluidOunceNetContentsRegex().Match(text);

        if (fluidOunceMatch.Success &&
            decimal.TryParse(
                fluidOunceMatch.Groups["value"].Value,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var fluidOunces))
        {
            return new ParsedLabelField<ParsedNetContents>
            {
                Value = new ParsedNetContents
                {
                    Value = fluidOunces,
                    Unit = "fl oz"
                },

                Evidence = fluidOunceMatch.Value.Trim(),

                Confidence = CalculateEvidenceConfidence(
                    fluidOunceMatch.Value,
                    ocrResult)
            };
        }

        var pintMatch =
            PintNetContentsRegex().Match(text);

        if (pintMatch.Success &&
            decimal.TryParse(
                pintMatch.Groups["value"].Value,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var pints))
        {
            return new ParsedLabelField<ParsedNetContents>
            {
                Value = new ParsedNetContents
                {
                    Value = pints,
                    Unit = "pt"
                },

                Evidence = pintMatch.Value.Trim(),

                Confidence = CalculateEvidenceConfidence(
                    pintMatch.Value,
                    ocrResult)
            };
        }

        return null;
    }

    private static string NormalizeNetContentsUnit(
        string unit)
    {
        return unit.Trim().ToUpperInvariant() switch
        {
            "ML" => "mL",
            "L" => "L",
            _ => unit.Trim()
        };
    }

    /// <summary>
    /// Extracts the Government Health Warning while preserving the wording
    /// observed by OCR. Compliance with exact wording and typography is
    /// evaluated later by regulatory verification rules.
    /// </summary>
    private static ParsedLabelField<string>?
        ParseGovernmentWarning(
            OcrResult ocrResult)
    {
        if (string.IsNullOrWhiteSpace(ocrResult.Text))
        {
            return null;
        }

        var match =
            GovernmentWarningRegex().Match(
                ocrResult.Text);

        if (!match.Success)
        {
            return null;
        }

        var evidence =
            NormalizeWhitespace(
                match.Value.Trim());

        if (string.IsNullOrWhiteSpace(evidence))
        {
            return null;
        }

        return new ParsedLabelField<string>
        {
            Value = evidence,
            Evidence = evidence,
            Confidence = CalculateEvidenceConfidence(
                evidence,
                ocrResult)
        };
    }

    /// <summary>
    /// Detects conditional ingredient declarations visible in OCR evidence.
    /// Whether a declaration was legally required is a verification concern.
    /// </summary>
    private static IReadOnlyList<ParsedIngredientDeclaration>
        ParseIngredientDeclarations(
            OcrResult ocrResult)
    {
        var declarations =
            new List<ParsedIngredientDeclaration>();

        AddIngredientDeclaration(
            declarations,
            ocrResult,
            IngredientDeclarationType.Aspartame,
            AspartameRegex());

        AddIngredientDeclaration(
            declarations,
            ocrResult,
            IngredientDeclarationType.Sulfites,
            SulfitesRegex());

        AddIngredientDeclaration(
            declarations,
            ocrResult,
            IngredientDeclarationType.FdAndCYellow5,
            FdAndCYellow5Regex());

        AddIngredientDeclaration(
            declarations,
            ocrResult,
            IngredientDeclarationType.CochinealOrCarmine,
            CochinealOrCarmineRegex());

        return declarations;
    }

    private static void AddIngredientDeclaration(
        ICollection<ParsedIngredientDeclaration> declarations,
        OcrResult ocrResult,
        IngredientDeclarationType type,
        Regex regex)
    {
        foreach (var line in ocrResult.Lines)
        {
            var match =
                regex.Match(line.Text);

            if (!match.Success)
            {
                continue;
            }

            AddDeclaration(
                declarations,
                ocrResult,
                type,
                match.Value);

            return;
        }

        var textMatch =
            regex.Match(ocrResult.Text);

        if (textMatch.Success)
        {
            AddDeclaration(
                declarations,
                ocrResult,
                type,
                textMatch.Value);
        }
    }

    private static void AddDeclaration(
        ICollection<ParsedIngredientDeclaration> declarations,
        OcrResult ocrResult,
        IngredientDeclarationType type,
        string evidence)
    {
        if (declarations.Any(
                declaration =>
                    declaration.Type == type))
        {
            return;
        }

        var normalizedEvidence =
            NormalizeWhitespace(
                evidence.Trim());

        declarations.Add(
            new ParsedIngredientDeclaration
            {
                Type = type,
                Evidence = normalizedEvidence,
                Confidence = CalculateEvidenceConfidence(
                    normalizedEvidence,
                    ocrResult)
            });
    }

    /// <summary>
    /// Estimates confidence using the OCR words supporting the extracted
    /// evidence. Provider-level confidence is retained as a fallback.
    /// </summary>
    private static double CalculateEvidenceConfidence(
        string evidence,
        OcrResult ocrResult)
    {
        var evidenceTokens =
            Tokenize(evidence);

        if (evidenceTokens.Count == 0 ||
            ocrResult.Words.Count == 0)
        {
            return ocrResult.Confidence;
        }

        var supportingWords =
            ocrResult.Words
                .Where(word =>
                    evidenceTokens.Contains(
                        NormalizeToken(word.Text),
                        StringComparer.OrdinalIgnoreCase))
                .ToArray();

        return supportingWords.Length == 0
            ? ocrResult.Confidence
            : supportingWords.Average(
                word => word.Confidence);
    }

    private static IReadOnlyList<string> Tokenize(
        string value)
    {
        return value
            .Split(
                [' ', '\t', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeToken)
            .Where(token => token.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeToken(
        string value)
    {
        return value
            .Trim()
            .Trim(',', '.', ':', ';', '(', ')')
            .ToUpperInvariant();
    }

    private static string NormalizeWhitespace(
        string value)
    {
        return WhitespaceRegex()
            .Replace(value, " ")
            .Trim();
    }

    [GeneratedRegex(
        @"(?<value>\d{1,3}(?:\.\d+)?)\s*%\s*" +
        @"(?:(?:ALCOHOL)|(?:ALC\.?))\s*" +
        @"(?:BY\s*)?(?:(?:VOLUME)|(?:VOL\.?))",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex AlcoholByVolumeRegex();

    [GeneratedRegex(
        @"(?<value>\d{1,3})\s*(?:\u00B0\s*)?PROOF\b",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex ProofRegex();

    [GeneratedRegex(
        @"(?<value>\d+(?:\.\d+)?)\s*(?<unit>ML|L)\b",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex MetricNetContentsRegex();

    [GeneratedRegex(
        @"(?<value>\d+(?:\.\d+)?)\s*FL\.?\s*OZ\.?\b",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex FluidOunceNetContentsRegex();

    [GeneratedRegex(
        @"(?<value>\d+(?:\.\d+)?)\s*(?:PINT|PT)\b",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex PintNetContentsRegex();

    [GeneratedRegex(
        @"GOVERNMENT\s+WARNING\s*:\s*.*?" +
        @"(?:health\s+problems\.?|$)",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant |
        RegexOptions.Singleline)]
    private static partial Regex GovernmentWarningRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(
        @"\bASPARTAME\b",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex AspartameRegex();

    [GeneratedRegex(
        @"\b(?:CONTAINS?\s+)?SULFITES?\b",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex SulfitesRegex();

    [GeneratedRegex(
        @"\bFD\s*&\s*C\s+YELLOW\s*" +
        @"(?:#\s*5|NO\.?\s*5)\b",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex FdAndCYellow5Regex();

    [GeneratedRegex(
        @"\b(?:COCHINEAL\s+EXTRACT|CARMINE)\b",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex CochinealOrCarmineRegex();

    [GeneratedRegex(
        @"\b(?:" +
        @"ALE|" +
        @"BEER|" +
        @"LAGER|" +
        @"STOUT|" +
        @"PORTER|" +
        @"MALT\s+BEVERAGE|" +
        @"HARD\s+SELTZER|" +
        @"CIDER|" +
        @"WINE|" +
        @"CHAMPAGNE|" +
        @"SPARKLING\s+WINE|" +
        @"WHISKEY|" +
        @"WHISKY|" +
        @"BOURBON|" +
        @"SCOTCH|" +
        @"VODKA|" +
        @"GIN|" +
        @"RUM|" +
        @"TEQUILA|" +
        @"MEZCAL|" +
        @"BRANDY|" +
        @"COGNAC|" +
        @"LIQUEUR|" +
        @"CORDIAL" +
        @")\b",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex ClassTypeKeywordRegex();

    [GeneratedRegex(
        @"\b(?:" +
        @"BREWED\s+BY|" +
        @"BREWER(?:Y)?|" +
        @"BREWING\s+(?:COMPANY|CO\.?)|" +
        @"BOTTLED\s+BY|" +
        @"DISTILLED\s+BY|" +
        @"DISTILLER(?:Y)?|" +
        @"PRODUCED\s+BY|" +
        @"IMPORTED\s+BY|" +
        @"IMPORTER|" +
        @"DISTRIBUTED\s+BY|" +
        @"WINER(?:Y|IES)|" +
        @"COMPANY|" +
        @"CORPORATION|" +
        @"CORP\.?" +
        @")\b",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex ProducerOrBusinessRegex();

    [GeneratedRegex(
        @"^(?:" +
        @"BREWED\s+BY|" +
        @"BOTTLED\s+BY|" +
        @"DISTILLED\s+BY|" +
        @"PRODUCED\s+BY|" +
        @"IMPORTED\s+BY|" +
        @"DISTRIBUTED\s+BY" +
        @")\s+",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex ProducerPrefixRegex();

    [GeneratedRegex(
        @"(?:\b\d{1,6}\s+\w+|" +
        @"\b(?:ROAD|RD\.?|STREET|ST\.?|AVENUE|AVE\.?|" +
        @"BOULEVARD|BLVD\.?|HIGHWAY|HWY\.?|DRIVE|DR\.?|" +
        @"LANE|LN\.?|SUITE|P\.?\s*O\.?\s+BOX)\b)",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex AddressLikeRegex();

    [GeneratedRegex(
        @"^(?<postal>\d{5}(?:-\d{4})?)" +
        @"(?:\s+(?<trailing>.+))?$",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex PostalAndTrailingRegex();

    [GeneratedRegex(
        @"\b(?:" +
        @"PRODUCT\s+OF|" +
        @"PRODUCED\s+IN|" +
        @"MADE\s+IN|" +
        @"COUNTRY\s+OF\s+ORIGIN\s*:?" +
        @")\s+" +
        @"(?<country>[A-Z][A-Z .'-]{1,60}?)" +
        @"(?=\s+(?:" +
        @"IMPORTED\s+BY|" +
        @"DISTRIBUTED\s+BY|" +
        @"BOTTLED\s+BY|" +
        @"PRODUCED\s+BY" +
        @")\b|$)",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex CountryOfOriginRegex();
    [GeneratedRegex(
        @"^(?:" +
        @"STORE\s+COLD|" +
        @"SERVE\s+COLD|" +
        @"DRINK\s+FRESH|" +
        @"DRINK\s+RESPONSIBLY\.?|" +
        @"PLEASE\s+DRINK\s+RESPONSIBLY\.?|" +
        @"ALWAYS\s+RECYCLE\.?" +
        @")$",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex MarketingInstructionRegex();
}



