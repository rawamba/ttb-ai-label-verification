using LabelVerification.Application.LabelIngestion;
using LabelVerification.Application.LabelUnderstanding;
using Microsoft.Extensions.DependencyInjection;
using LabelVerification.Application.Verification.Normalization;
using LabelVerification.Application.Verification.Brand;

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

        services.AddSingleton<ILabelFieldParser, LabelFieldParser>();
        // Text normalization is stateless and safe to share for the lifetime
        // of the application.
        services.AddSingleton<ITextNormalizer, TextNormalizer>();

        // Brand name verification is stateless and safe to share for the lifetime
        services.AddSingleton(
    new BrandNameVerificationOptions
    {
        PassThreshold = 0.95,
        ReviewThreshold = 0.80
    });

        services.AddSingleton<IBrandNameVerifier, BrandNameVerifier>();

        return services;
    }
}