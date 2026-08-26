namespace LabelVerification.Application.LabelUnderstanding;

/// <summary>
/// Represents normalized net-content information detected on a label.
/// </summary>
public sealed record ParsedNetContents
{
    public required decimal Value { get; init; }

    public required string Unit { get; init; }
}
