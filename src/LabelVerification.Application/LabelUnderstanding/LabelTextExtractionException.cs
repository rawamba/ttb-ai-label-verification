namespace LabelVerification.Application.LabelUnderstanding;

/// <summary>
/// Represents a failure by an OCR or vision provider to extract usable
/// textual evidence from a validated label image.
/// </summary>
public class LabelTextExtractionException : Exception
{
    public LabelTextExtractionException(string message)
        : base(message)
    {
    }

    public LabelTextExtractionException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}