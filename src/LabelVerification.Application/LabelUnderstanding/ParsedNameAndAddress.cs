namespace LabelVerification.Application.LabelUnderstanding;

/// <summary>
/// Represents producer, bottler, importer, or other name-and-address
/// information observed on the label.
///
/// RawText is always retained because OCR layout and address interpretation
/// can be ambiguous. Structured components are populated only when they can
/// be identified conservatively from the observed evidence.
/// </summary>
public sealed record ParsedNameAndAddress
{
    public required string RawText { get; init; }

    public string? BusinessName { get; init; }

    public string? StreetAddress { get; init; }

    public string? City { get; init; }

    public string? State { get; init; }

    public string? PostalCode { get; init; }

    public string? Country { get; init; }
}
