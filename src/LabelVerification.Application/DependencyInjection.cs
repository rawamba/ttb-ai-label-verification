using LabelVerification.Application.LabelIngestion;
using Microsoft.Extensions.DependencyInjection;

namespace LabelVerification.Application;

/// <summary>
/// Registers application-layer services that coordinate label-verification
/// use cases and deterministic input validation.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds application-layer services to the built-in .NET dependency
    /// injection container.
    /// </summary>
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        // Validate uploaded label inputs before they are submitted to OCR
        // or other probabilistic AI processing.
        services.AddSingleton(
            new LabelImageValidationOptions());

        services.AddSingleton<
            ILabelImageValidator,
            LabelImageValidator>();

        return services;
    }
}