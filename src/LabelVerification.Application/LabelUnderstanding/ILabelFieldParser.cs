namespace LabelVerification.Application.LabelUnderstanding;

/// <summary>
/// Converts raw OCR evidence into structured label fields.
///
/// This component answers "What does the label appear to say?"
/// It does not make regulatory PASS, REVIEW, or FAIL decisions.
/// </summary>
public interface ILabelFieldParser
{
    ParsedLabelData Parse(OcrResult ocrResult);
}
