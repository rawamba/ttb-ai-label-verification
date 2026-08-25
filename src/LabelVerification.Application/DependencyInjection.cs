using Microsoft.Extensions.DependencyInjection;

namespace LabelVerification.Application;

/// <summary>
/// Registers application-layer services that coordinate label verification use cases.
/// This layer contains orchestration and business-facing services, while avoiding
/// dependencies on UI, OCR vendors, storage providers, or other infrastructure concerns.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds application-layer services to the built-in .NET dependency injection container.
    /// </summary>
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        // Application services will be registered here as the verification
        // workflow is implemented. Keeping registration in this layer preserves
        // a clean composition boundary and keeps Program.cs focused on startup.
        return services;
    }
}