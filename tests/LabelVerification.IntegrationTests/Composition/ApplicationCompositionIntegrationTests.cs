using LabelVerification.Application.LabelUnderstanding;
using LabelVerification.Application.Verification;
using LabelVerification.Application.Verification.Alcohol;
using LabelVerification.Application.Verification.Brand;
using LabelVerification.Application.Verification.GovernmentWarning;
using LabelVerification.Application.Verification.NetContents;
using LabelVerification.Application.Verification.Workflow;
using LabelVerification.IntegrationTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace LabelVerification.IntegrationTests.Composition;

/// <summary>
/// Guards the production Application-layer dependency graph against missing
/// service registrations.
/// </summary>
public sealed class ApplicationCompositionIntegrationTests
{
    [Fact]
    public void BuildServiceProvider_WithVerificationDependencies_ResolvesCompleteWorkflow()
    {
        // Arrange
        var extractor =
            new ControlledLabelTextExtractor(
                IntegrationTestSupport.CreateCompliantOcrResult());

        // BuildServiceProvider uses ValidateOnBuild, which fails immediately
        // if a constructor dependency is missing.
        using var services =
            IntegrationTestSupport.BuildServiceProvider(
                extractor);

        // Act / Assert
        Assert.NotNull(
            services.GetRequiredService<
                ILabelVerificationService>());

        Assert.NotNull(
            services.GetRequiredService<
                ILabelFieldParser>());

        Assert.NotNull(
            services.GetRequiredService<
                IBrandNameVerifier>());

        Assert.NotNull(
            services.GetRequiredService<
                IAlcoholValueVerifier>());

        Assert.NotNull(
            services.GetRequiredService<
                INetContentsVerifier>());

        Assert.NotNull(
            services.GetRequiredService<
                IGovernmentWarningVerifier>());

        Assert.NotNull(
            services.GetRequiredService<
                IVerificationResultAggregator>());
    }
}