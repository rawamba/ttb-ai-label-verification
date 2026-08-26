namespace LabelVerification.Application.LabelUnderstanding;

/// <summary>
/// Represents a line of text detected in a label image.
///
/// Line-level text preserves provider reading structure while individual
/// word evidence and confidence values are retained separately in
/// <see cref="OcrResult.Words"/>.
/// </summary>
public sealed record OcrTextLine
{
    /// <summary>
    /// Text detected for this line in provider reading order.
    /// </summary>
    public required string Text { get; init; }
}