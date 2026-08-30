using LabelVerification.Application.LabelIngestion;
using LabelVerification.Application.LabelUnderstanding;
using LabelVerification.Application.Verification;
using LabelVerification.Application.Verification.Alcohol;
using LabelVerification.Application.Verification.Batch;
using LabelVerification.Application.Verification.Brand;
using LabelVerification.Application.Verification.GovernmentWarning;
using LabelVerification.Application.Verification.NetContents;
using LabelVerification.Application.Verification.Normalization;
using LabelVerification.Application.Verification.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        // Convert OCR evidence into structured label observations.
        services.AddSingleton<
            ILabelFieldParser,
            LabelFieldParser>();

        // Text normalization is stateless and safe to share for the lifetime
        // of the application.
        services.AddSingleton<
            ITextNormalizer,
            TextNormalizer>();

        // Brand verification uses conservative deterministic normalization
        // and fuzzy comparison thresholds.
        services.AddSingleton(
            new BrandNameVerificationOptions
            {
                PassThreshold = 0.95,
                ReviewThreshold = 0.80
            });

        services.AddSingleton<
            IBrandNameVerifier,
            BrandNameVerifier>();

        // Resolve among multiple observed brand candidates using the expected
        // application brand as a deterministic signal. The resolver never
        // invents evidence or replaces OCR evidence with application data.
        services.AddSingleton<
            IBrandNameCandidateResolver,
            BrandNameCandidateResolver>();

        // Alcohol value verification is stateless and deterministic.
        services.AddSingleton<
            IAlcoholValueVerifier,
            AlcoholValueVerifier>();

        services.AddSingleton<
            INetContentsNormalizer,
            NetContentsNormalizer>();

        services.AddSingleton<
            INetContentsVerifier,
            NetContentsVerifier>();

        // Government Warning verification evaluates exact regulatory wording
        // and supported typography evidence.
        services.AddSingleton(
            new GovernmentWarningVerificationOptions
            {
                MinimumOcrConfidence = 0.90,
                MinimumStyleConfidence = 0.90,
                RequireBoldHeading = true
            });

        services.AddSingleton<
            IGovernmentWarningVerifier,
            GovernmentWarningVerifier>();

        // Coordinate the end-to-end application verification use case while
        // keeping Web components independent of individual OCR and rule services.
        services.AddTransient<
            ILabelVerificationService,
            LabelVerificationService>();

        // Provide conservative defaults for hosts that do not explicitly
        // configure batch processing, including tests and benchmarks.
        //
        // A composition root may register BatchVerificationOptions before
        // calling AddApplication() to override these defaults.
        services.TryAddSingleton(
            new BatchVerificationOptions());

        // Coordinate multiple independent label-verification operations using
        // bounded concurrency while reusing the existing single-label workflow.
        services.AddTransient<
            IBatchLabelVerificationService,
            BatchLabelVerificationService>();

        // Aggregate field-level PASS / REVIEW / FAIL checks into the overall
        // deterministic verification result.
        services.AddSingleton<
            IVerificationResultAggregator,
            VerificationResultAggregator>();

        return services;
    }
}