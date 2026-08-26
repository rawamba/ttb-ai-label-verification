namespace LabelVerification.Application.LabelUnderstanding;

/// <summary>
/// Represents a word detected in a label image together with the
/// model confidence associated with that individual observation.
/// </summary>
public sealed record OcrWord
{
    public required string Text { get; init; }

    /// <summary>
    /// Provider-reported confidence normalized to the range 0.0 - 1.0.
    /// </summary>
    public required double Confidence { get; init; }
}