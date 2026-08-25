using LabelVerification.Application.Exceptions;
using LabelVerification.Infrastructure.ApplicationRecords;

namespace LabelVerification.IntegrationTests.ApplicationRecords;

/// <summary>
/// Verifies the JSON-backed application-record adapter against representative
/// valid, missing, and invalid upstream-data scenarios.
///
/// These are integration tests rather than pure unit tests because they exercise
/// the provider's real filesystem and JSON-deserialization behavior.
/// </summary>
public sealed class JsonApplicationRecordProviderTests : IDisposable
{
    private readonly string _fixtureDirectory;
    private readonly JsonApplicationRecordProvider _provider;

    public JsonApplicationRecordProviderTests()
    {
        // Each test instance receives an isolated temporary directory so tests
        // do not depend on repository fixtures or interfere with one another.
        _fixtureDirectory = Path.Combine(
            Path.GetTempPath(),
            "LabelVerificationTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_fixtureDirectory);

        _provider = new JsonApplicationRecordProvider(_fixtureDirectory);
    }

    [Fact]
    public async Task GetByIdAsync_WhenRecordIsValid_ReturnsApplicationRecord()
    {
        // Arrange
        const string applicationId = "COLA-84729";

        await WriteFixtureAsync(
            applicationId,
            """
            {
              "applicationId": "COLA-84729",
              "beverageType": "distilled_spirits",
              "expectedData": {
                "brandName": "Old Tom Distillery",
                "classType": "Kentucky Straight Bourbon Whiskey",
                "alcoholByVolume": 45.0,
                "proof": 90,
                "netContents": {
                  "value": 750,
                  "unit": "mL"
                }
              }
            }
            """);

        // Act
        var result = await _provider.GetByIdAsync(applicationId);

        // Assert
        Assert.NotNull(result);

        Assert.Equal("COLA-84729", result.ApplicationId);
        Assert.Equal("distilled_spirits", result.BeverageType);

        Assert.Equal(
            "Old Tom Distillery",
            result.ExpectedData.BrandName);

        Assert.Equal(
            "Kentucky Straight Bourbon Whiskey",
            result.ExpectedData.ClassType);

        Assert.Equal(
            45.0m,
            result.ExpectedData.AlcoholByVolume);

        Assert.Equal(
            90m,
            result.ExpectedData.Proof);

        Assert.Equal(
            750m,
            result.ExpectedData.NetContents.Value);

        Assert.Equal(
            "mL",
            result.ExpectedData.NetContents.Unit);
    }

    [Fact]
    public async Task GetByIdAsync_WhenRecordDoesNotExist_ReturnsNull()
    {
        // Act
        var result = await _provider.GetByIdAsync("COLA-DOES-NOT-EXIST");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_WhenJsonIsMalformed_ThrowsInvalidApplicationRecordException()
    {
        // Arrange
        const string applicationId = "COLA-MALFORMED";

        await WriteFixtureAsync(
            applicationId,
            """
            {
              "applicationId": "COLA-MALFORMED",
              "beverageType": "distilled_spirits",
              "expectedData":
            """);

        // Act
        var exception =
            await Assert.ThrowsAsync<InvalidApplicationRecordException>(
                () => _provider.GetByIdAsync(applicationId));

        // Assert
        Assert.Equal(applicationId, exception.ApplicationId);

        Assert.Contains(
            "invalid JSON",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    public async Task GetByIdAsync_WhenRecordContainsNoUsableData_ThrowsInvalidApplicationRecordException(
        string json)
    {
        // Arrange
        const string applicationId = "COLA-EMPTY";

        await WriteFixtureAsync(applicationId, json);

        // Act
        var exception =
            await Assert.ThrowsAsync<InvalidApplicationRecordException>(
                () => _provider.GetByIdAsync(applicationId));

        // Assert
        Assert.Equal(applicationId, exception.ApplicationId);
    }

    [Fact]
    public async Task GetByIdAsync_WhenRecordIdDoesNotMatchRequest_ThrowsInvalidApplicationRecordException()
    {
        // Arrange
        const string requestedApplicationId = "COLA-84729";

        await WriteFixtureAsync(
            requestedApplicationId,
            """
            {
              "applicationId": "COLA-99999",
              "beverageType": "distilled_spirits",
              "expectedData": {
                "brandName": "Old Tom Distillery",
                "classType": "Kentucky Straight Bourbon Whiskey",
                "alcoholByVolume": 45.0,
                "proof": 90,
                "netContents": {
                  "value": 750,
                  "unit": "mL"
                }
              }
            }
            """);

        // Act
        var exception =
            await Assert.ThrowsAsync<InvalidApplicationRecordException>(
                () => _provider.GetByIdAsync(requestedApplicationId));

        // Assert
        Assert.Equal(
            requestedApplicationId,
            exception.ApplicationId);

        Assert.Contains(
            "does not match",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetByIdAsync_WhenRequiredFieldIsMissing_ThrowsInvalidApplicationRecordException()
    {
        // Arrange
        const string applicationId = "COLA-MISSING-BRAND";

        await WriteFixtureAsync(
            applicationId,
            """
            {
              "applicationId": "COLA-MISSING-BRAND",
              "beverageType": "distilled_spirits",
              "expectedData": {
                "classType": "Kentucky Straight Bourbon Whiskey",
                "alcoholByVolume": 45.0,
                "proof": 90,
                "netContents": {
                  "value": 750,
                  "unit": "mL"
                }
              }
            }
            """);

        // Act
        var exception =
            await Assert.ThrowsAsync<InvalidApplicationRecordException>(
                () => _provider.GetByIdAsync(applicationId));

        // Assert
        Assert.Equal(applicationId, exception.ApplicationId);
    }

    /// <summary>
    /// Writes a fixture using the same filename-normalization convention as
    /// the production JSON provider.
    /// </summary>
    private async Task WriteFixtureAsync(
        string applicationId,
        string json)
    {
        var fileName =
            $"{applicationId.Trim().ToLowerInvariant()}.json";

        var filePath =
            Path.Combine(_fixtureDirectory, fileName);

        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Removes temporary test data after each test instance.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_fixtureDirectory))
        {
            Directory.Delete(
                _fixtureDirectory,
                recursive: true);
        }
    }
}