using Azure.AI.DocumentIntelligence;
using Azure.Identity;
using LabelVerification.Application.Abstractions;
using LabelVerification.Application.LabelUnderstanding;
using LabelVerification.Infrastructure.ApplicationRecords;
using LabelVerification.Infrastructure.LabelUnderstanding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static System.Collections.Specialized.BitVector32;

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

        // Configure Azure Document Intelligence as the primary OCR provider.
        //
        // DefaultAzureCredential allows local development to use the developer's
        // Azure CLI/IDE identity while Azure App Service can use its managed identity.
        // No Cognitive Services access key is stored in application configuration.
        var documentIntelligenceSection =
            configuration.GetSection(DocumentIntelligenceOptions.SectionName);

        var endpointValue =
            documentIntelligenceSection["Endpoint"];

        if (string.IsNullOrWhiteSpace(endpointValue))
        {
            throw new InvalidOperationException(
                "Document Intelligence endpoint configuration is missing.");
        }

        var documentIntelligenceOptions =
            new DocumentIntelligenceOptions
            {
                Endpoint = new Uri(endpointValue),
                ModelId =
                    documentIntelligenceSection["ModelId"]
                    ?? "prebuilt-read",
                Timeout =
                    TimeSpan.FromSeconds(
                        documentIntelligenceSection.GetValue<int?>("TimeoutSeconds")
                        ?? 5),
                EnableFontStyling =
        documentIntelligenceSection.GetValue<bool?>("EnableFontStyling") ?? true
            };

        services.AddSingleton(documentIntelligenceOptions);

        services.AddSingleton(
            new DocumentIntelligenceClient(
                documentIntelligenceOptions.Endpoint,
                new DefaultAzureCredential()));

        services.AddSingleton<
            ILabelTextExtractor,
            DocumentIntelligenceLabelTextExtractor>();

        return services;
    }
}