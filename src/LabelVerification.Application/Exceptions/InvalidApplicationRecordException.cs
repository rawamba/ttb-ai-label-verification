namespace LabelVerification.Application.Exceptions;

/// <summary>
/// Represents a failure to load a usable application record from an
/// application-data provider.
///
/// This exception is reserved for records that exist but cannot be safely
/// interpreted as valid application data. A record that simply does not
/// exist is represented by a null provider result instead.
/// </summary>
public sealed class InvalidApplicationRecordException : Exception
{
    /// <summary>
    /// Gets the application identifier associated with the invalid record.
    /// </summary>
    public string ApplicationId { get; }

    /// <summary>
    /// Creates an exception for an invalid application record.
    /// </summary>
    public InvalidApplicationRecordException(
        string applicationId,
        string message)
        : base(message)
    {
        ApplicationId = applicationId;
    }

    /// <summary>
    /// Creates an exception for an invalid application record while
    /// preserving the underlying provider or serialization error.
    /// </summary>
    public InvalidApplicationRecordException(
        string applicationId,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        ApplicationId = applicationId;
    }
}