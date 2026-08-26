using LabelVerification.Infrastructure.LabelUnderstanding;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LabelVerification.Web.Health;

/// <summary>
/// Verifies that the Azure Document Intelligence dependency is configured
/// and network-reachable without performing an OCR inference.
///
/// This is a readiness check rather than a model-quality check. Actual OCR
/// behavior is validated separately through model-evaluation tests.
/// </summary>
public sealed class DocumentIntelligenceReadinessHealthCheck : IHealthCheck
{
    private const string HttpClientName = "DocumentIntelligenceHealth";

    private readonly DocumentIntelligenceOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;

    public DocumentIntelligenceReadinessHealthCheck(
        DocumentIntelligenceOptions options,
        IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        _options = options;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_options.Endpoint is null)
        {
            return HealthCheckResult.Unhealthy(
                "Document Intelligence endpoint is not configured.");
        }

        if (!string.Equals(
                _options.Endpoint.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
        {
            return HealthCheckResult.Unhealthy(
                "Document Intelligence endpoint must use HTTPS.");
        }

        using var timeoutCancellation =
            new CancellationTokenSource(TimeSpan.FromSeconds(2));

        using var linkedCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCancellation.Token);

        try
        {
            var client =
                _httpClientFactory.CreateClient(HttpClientName);

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Head,
                    _options.Endpoint);

            using var response =
                await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    linkedCancellation.Token);

            // The service root may legitimately return 401, 404, or 405
            // because this probe is intentionally not an authenticated OCR
            // operation. Receiving any HTTP response proves that the Azure
            // endpoint is reachable from this application environment.
            return HealthCheckResult.Healthy(
                $"Document Intelligence endpoint is reachable " +
                $"(HTTP {(int)response.StatusCode}).");
        }
        catch (OperationCanceledException)
            when (timeoutCancellation.IsCancellationRequested &&
                  !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy(
                "Document Intelligence readiness probe timed out.");
        }
        catch (HttpRequestException exception)
        {
            return HealthCheckResult.Unhealthy(
                "Document Intelligence endpoint is not reachable.",
                exception);
        }
    }
}