using System.Globalization;
using System.Text;

namespace LabelVerification.Application.Verification.Normalization;

/// <summary>
/// Default deterministic text normalizer used by verification rules.
///
/// The implementation intentionally performs conservative normalization.
/// It canonicalizes representation differences but does not perform fuzzy
/// matching, spelling correction, semantic interpretation, or regulatory
/// decision-making.
/// </summary>
public sealed class TextNormalizer : ITextNormalizer
{
    /// <inheritdoc />
    public string NormalizeForComparison(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        // FormKC canonicalizes compatibility representations such as
        // full-width Latin characters without discarding meaningful accents.
        var canonical =
            value.Normalize(NormalizationForm.FormKC);

        var builder =
            new StringBuilder(canonical.Length);

        var previousWasWhitespace = false;

        foreach (var character in canonical)
        {
            // Ignore invisible formatting characters that can appear in
            // copied/OCR text but should not influence comparison.
            if (IsIgnorableFormattingCharacter(character))
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                // Collapse any sequence of spaces, tabs, newlines, or
                // non-breaking spaces into one ordinary space.
                if (builder.Length > 0 &&
                    !previousWasWhitespace)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }

                continue;
            }

            builder.Append(
                NormalizePunctuation(character));

            previousWasWhitespace = false;
        }

        // Remove a trailing collapsed space before applying invariant case.
        if (builder.Length > 0 &&
            builder[^1] == ' ')
        {
            builder.Length--;
        }

        return builder
            .ToString()
            .ToLowerInvariant();
    }

    private static char NormalizePunctuation(
        char character)
    {
        return character switch
        {
            // Canonicalize typographic apostrophes/prime-like characters.
            '\u2018' => '\'',
            '\u2019' => '\'',
            '\u201B' => '\'',
            '\u2032' => '\'',
            '\u02BC' => '\'',

            // Canonicalize common Unicode dash/minus variants.
            '\u2010' => '-',
            '\u2011' => '-',
            '\u2012' => '-',
            '\u2013' => '-',
            '\u2014' => '-',
            '\u2015' => '-',
            '\u2212' => '-',

            _ => character
        };
    }

    private static bool IsIgnorableFormattingCharacter(
        char character)
    {
        return character is
            '\u200B' or // zero-width space
            '\u200C' or // zero-width non-joiner
            '\u200D' or // zero-width joiner
            '\u2060' or // word joiner
            '\uFEFF';   // zero-width no-break space / BOM
    }
}
