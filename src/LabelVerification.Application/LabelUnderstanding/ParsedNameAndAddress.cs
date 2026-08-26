namespace LabelVerification.Application.LabelUnderstanding;

/// <summary>
/// Represents producer, bottler, importer, or other name-and-address
/// information observed on the label.
///
/// RawText is retained because address interpretation can be ambiguous,
/// and the original evidence must remain available for human review.
/// </summary>
public sealed record ParsedNameAndAddress
{
    public required string RawText { get; init; }

    public string? BusinessName { get; init; }

    public string? City { get; init; }

    public string? State { get; init; }

    public string? Country { get; init; }
}
