using Azure.AI.DocumentIntelligence;
using Azure.Core;
using Azure.Identity;
using LabelVerification.Application.Abstractions;
using LabelVerification.Application.LabelUnderstanding;
using LabelVerification.Infrastructure.ApplicationRecords;
using LabelVerification.Infrastructure.LabelUnderstanding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LabelVerification.Infrastructure;

/// <summary>
/// Registers Infrastructure-layer implementations for external concerns such
/// as Azure OCR and prototype application-record access.
///
/// Provider construction remains in Infrastructure so Application-layer
/// verification code does not depend on Azure SDK implementation details.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds Infrastructure-layer services to the built-in .NET dependency
    /// injection container.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(
            services);

        ArgumentNullException.ThrowIfNull(
            configuration);

        //
        // Prototype application-record adapter.
        //
        // Expected application data is intentionally supplied through an
        // abstraction so a future authorized COLA adapter can replace the
        // JSON fixture provider without changing verification rules.
        //
        var configuredDirectory =
            configuration["ApplicationData:Directory"];

        var applicationDataDirectory =
            string.IsNullOrWhiteSpace(
                configuredDirectory)
                ? Path.Combine(
                    AppContext.BaseDirectory,
                    "sample-data",
                    "applications")
                : Path.GetFullPath(
                    configuredDirectory,
                    AppContext.BaseDirectory);

        services.AddSingleton<
            IApplicationRecordProvider>(
            _ =>
                new JsonApplicationRecordProvider(
                    applicationDataDirectory));

        //
        // Azure Document Intelligence configuration.
        //
        var documentIntelligenceSection =
            configuration.GetSection(
                DocumentIntelligenceOptions.SectionName);

        var endpointValue =
            documentIntelligenceSection["Endpoint"];

        if (string.IsNullOrWhiteSpace(
                endpointValue))
        {
            throw new InvalidOperationException(
                "Document Intelligence endpoint configuration is missing.");
        }

        var timeoutSeconds =
            GetPositiveConfigurationValue(
                documentIntelligenceSection,
                "TimeoutSeconds",
                defaultValue: 5);

        var authenticationTimeoutSeconds =
            GetPositiveConfigurationValue(
                documentIntelligenceSection,
                "AuthenticationTimeoutSeconds",
                defaultValue: 15);

        var documentIntelligenceOptions =
            new DocumentIntelligenceOptions
            {
                Endpoint =
                    new Uri(
                        endpointValue),

                ModelId =
                    documentIntelligenceSection["ModelId"]
                    ?? "prebuilt-read",

                // This remains the latency-sensitive provider-operation
                // timeout used after authentication readiness is established.
                Timeout =
                    TimeSpan.FromSeconds(
                        timeoutSeconds),

                // Credential initialization has a separate budget because
                // developer credential discovery and managed-identity token
                // acquisition are startup concerns rather than OCR inference.
                AuthenticationTimeout =
                    TimeSpan.FromSeconds(
                        authenticationTimeoutSeconds),

                EnableFontStyling =
                    documentIntelligenceSection
                        .GetValue<bool?>(
                            "EnableFontStyling")
                    ?? true
            };

        services.AddSingleton(
            documentIntelligenceOptions);

        //
        // Shared Azure credential.
        //
        // One credential instance is shared by both:
        // 1. the explicit authentication-readiness step, and
        // 2. Azure Document Intelligence's HTTP authentication pipeline.
        //
        // The small caching wrapper guarantees that a token obtained during
        // readiness is visible when the Document Intelligence client
        // immediately requests the same Cognitive Services token.
        //
        services.AddSingleton<TokenCredential>(
            _ =>
            {
                var defaultCredential =
                    new DefaultAzureCredential();

                return new CachingTokenCredential(
                    defaultCredential);
            });

        //
        // Azure Document Intelligence client.
        //
        // No API key is stored in configuration. Local development uses
        // DefaultAzureCredential while Azure App Service can resolve its
        // system-assigned Managed Identity through the same abstraction.
        //
        services.AddSingleton(
            serviceProvider =>
            {
                var credential =
                    serviceProvider.GetRequiredService<
                        TokenCredential>();

                return new DocumentIntelligenceClient(
                    documentIntelligenceOptions.Endpoint,
                    credential);
            });

        // Register the provider-neutral OCR boundary consumed by the
        // Application-layer verification workflow.
        services.AddSingleton<
            ILabelTextExtractor,
            DocumentIntelligenceLabelTextExtractor>();

        return services;
    }

    /// <summary>
    /// Reads a positive integer configuration value while preserving an
    /// explicit safe default.
    ///
    /// Invalid zero or negative values fail fast instead of creating an
    /// immediately cancelled provider operation.
    /// </summary>
    private static int GetPositiveConfigurationValue(
        IConfiguration configuration,
        string key,
        int defaultValue)
    {
        var configuredValue =
            configuration.GetValue<int?>(
                key);

        if (!configuredValue.HasValue)
        {
            return defaultValue;
        }

        if (configuredValue.Value <= 0)
        {
            throw new InvalidOperationException(
                $"Document Intelligence configuration '{key}' " +
                "must be greater than zero.");
        }

        return configuredValue.Value;
    }
}