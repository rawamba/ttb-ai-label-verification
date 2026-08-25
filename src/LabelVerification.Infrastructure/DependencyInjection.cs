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
    /// Adds infrastructure services to the built-in .NET dependency injection container.
    /// Configuration is accepted here because infrastructure implementations may require
    /// provider-specific settings such as OCR paths, timeouts, or external endpoints.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Concrete implementations will be registered here behind interfaces
        // defined by the Application layer. This allows providers to be replaced
        // without changing verification workflow code.
        return services;
    }
}