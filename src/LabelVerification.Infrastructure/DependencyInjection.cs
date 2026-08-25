using LabelVerification.Application.Abstractions;
using LabelVerification.Infrastructure.ApplicationRecords;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LabelVerification.Infrastructure;

/// <summary>
/// Registers infrastructure implementations for external concerns such as OCR,
/// application-record providers, file access, and future Azure integrations.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds infrastructure services to the built-in .NET dependency injection
    /// container.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // The prototype reads expected application data from local JSON
        // fixtures. The Application layer depends only on the provider
        // abstraction, allowing this implementation to be replaced by a
        // future COLA adapter without changing verification workflow code.
        var configuredDirectory = configuration["ApplicationData:Directory"];

        var applicationDataDirectory = string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "sample-data", "applications")
            : Path.GetFullPath(configuredDirectory, AppContext.BaseDirectory);

        services.AddSingleton<IApplicationRecordProvider>(
            _ => new JsonApplicationRecordProvider(applicationDataDirectory));

        return services;
    }
}