namespace LabelVerification.Application.LabelUnderstanding;

/// <summary>
/// Provider-neutral font-weight classification derived from OCR/style evidence.
/// Unknown is intentionally supported because not every OCR provider can
/// establish typography with sufficient confidence.
/// </summary>
public enum OcrFontWeight
{
    Unknown,
    Normal,
    Bold
}
