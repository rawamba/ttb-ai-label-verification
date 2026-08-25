using System.Text.Json;
using LabelVerification.Application.Abstractions;
using LabelVerification.Application.Exceptions;
using LabelVerification.Domain.Models;

namespace LabelVerification.Infrastructure.ApplicationRecords;

/// <summary>
/// Loads prototype application records from JSON fixtures.
///
/// This adapter represents the prototype implementation of the application-data
/// boundary. The verification workflow depends only on
/// <see cref="IApplicationRecordProvider"/> and therefore remains unaware of
/// whether data originates from JSON, COLA, or another upstream source.
/// </summary>
public sealed class JsonApplicationRecordProvider : IApplicationRecordProvider
{
    private readonly string _applicationDataDirectory;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Creates a JSON-backed application-record provider.
    /// </summary>
    /// <param name="applicationDataDirectory">
    /// Directory containing prototype application JSON fixtures.
    /// </param>
    public JsonApplicationRecordProvider(string applicationDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDataDirectory);

        _applicationDataDirectory = applicationDataDirectory;
    }

    /// <inheritdoc />
    public async Task<ApplicationRecord?> GetByIdAsync(
        string applicationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);

        var normalizedApplicationId = applicationId.Trim();

        // Fixture filenames are normalized so lookup behavior remains
        // consistent across case-sensitive and case-insensitive filesystems.
        var fileName =
            $"{normalizedApplicationId.ToLowerInvariant()}.json";

        var filePath =
            Path.Combine(_applicationDataDirectory, fileName);

        // Missing application data is an expected lookup outcome rather than
        // an exceptional provider failure.
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(filePath);

            var record =
                await JsonSerializer.DeserializeAsync<ApplicationRecord>(
                    stream,
                    SerializerOptions,
                    cancellationToken);

            // Empty files or JSON literal "null" technically deserialize
            // successfully but do not represent usable application records.
            if (record is null)
            {
                throw CreateInvalidRecordException(
                    normalizedApplicationId,
                    "The application record did not contain application data.");
            }

            ValidateRecord(record, normalizedApplicationId);

            return record;
        }
        catch (JsonException exception)
        {
            // Do not leak serialization-specific exceptions across the
            // Application boundary. Translate them into the provider contract.
            throw new InvalidApplicationRecordException(
                normalizedApplicationId,
                $"Application record '{normalizedApplicationId}' contains invalid JSON.",
                exception);
        }
        catch (NotSupportedException exception)
        {
            // A serialization shape that System.Text.Json cannot interpret is
            // also treated as an invalid upstream record.
            throw new InvalidApplicationRecordException(
                normalizedApplicationId,
                $"Application record '{normalizedApplicationId}' contains unsupported data.",
                exception);
        }
    }

    /// <summary>
    /// Validates the minimum structural integrity required before an
    /// application record is returned to the verification workflow.
    ///
    /// This validation intentionally checks data integrity rather than
    /// regulatory compliance. Compliance rules belong elsewhere.
    /// </summary>
    private static void ValidateRecord(
        ApplicationRecord record,
        string requestedApplicationId)
    {
        if (string.IsNullOrWhiteSpace(record.ApplicationId))
        {
            throw CreateInvalidRecordException(
                requestedApplicationId,
                "The application record is missing its application identifier.");
        }

        // A fixture must not silently return data for a different application.
        if (!string.Equals(
                record.ApplicationId,
                requestedApplicationId,
                StringComparison.OrdinalIgnoreCase))
        {
            throw CreateInvalidRecordException(
                requestedApplicationId,
                $"The application record identifier '{record.ApplicationId}' " +
                $"does not match the requested identifier '{requestedApplicationId}'.");
        }

        if (string.IsNullOrWhiteSpace(record.BeverageType))
        {
            throw CreateInvalidRecordException(
                requestedApplicationId,
                "The application record is missing its beverage type.");
        }

        if (record.ExpectedData is null)
        {
            throw CreateInvalidRecordException(
                requestedApplicationId,
                "The application record is missing expected label data.");
        }

        if (string.IsNullOrWhiteSpace(record.ExpectedData.BrandName))
        {
            throw CreateInvalidRecordException(
                requestedApplicationId,
                "Expected label data is missing the brand name.");
        }

        if (string.IsNullOrWhiteSpace(record.ExpectedData.ClassType))
        {
            throw CreateInvalidRecordException(
                requestedApplicationId,
                "Expected label data is missing the class or type.");
        }

        if (record.ExpectedData.NetContents is null)
        {
            throw CreateInvalidRecordException(
                requestedApplicationId,
                "Expected label data is missing net contents.");
        }

        if (record.ExpectedData.NetContents.Value <= 0)
        {
            throw CreateInvalidRecordException(
                requestedApplicationId,
                "Expected net contents must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(record.ExpectedData.NetContents.Unit))
        {
            throw CreateInvalidRecordException(
                requestedApplicationId,
                "Expected net contents is missing its unit.");
        }
    }

    private static InvalidApplicationRecordException
        CreateInvalidRecordException(
            string applicationId,
            string message)
    {
        return new InvalidApplicationRecordException(
            applicationId,
            message);
    }
}