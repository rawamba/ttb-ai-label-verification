namespace LabelVerification.Application.LabelUnderstanding;

/// <summary>
/// Represents visual style evidence associated with a span of OCR text.
///
/// Offset and Length refer to the provider's concatenated OCR content.
/// Text is retained to make downstream verification and diagnostics easier.
/// </summary>
public sealed record OcrTextStyle
{
    public required int Offset { get; init; }

    public required int Length { get; init; }

    public required string Text { get; init; }

    public required OcrFontWeight FontWeight { get; init; }

    public required double Confidence { get; init; }
}
