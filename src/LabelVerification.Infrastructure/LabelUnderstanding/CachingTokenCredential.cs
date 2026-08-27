using Azure.Core;

namespace LabelVerification.Infrastructure.LabelUnderstanding;

/// <summary>
/// Wraps an Azure <see cref="TokenCredential"/> and retains a valid access
/// token for reuse by both the explicit authentication-readiness step and
/// Azure SDK clients.
///
/// The purpose of this wrapper is narrow:
///
/// - establish Azure authentication before the five-second OCR operation
///   budget begins;
/// - ensure concurrent first-use requests share one token acquisition; and
/// - return the same valid token immediately when the Document Intelligence
///   client subsequently asks for it.
///
/// This is not a persistent credential store. Access tokens remain only in
/// process memory and are refreshed before expiration.
/// </summary>
public sealed class CachingTokenCredential
    : TokenCredential
{
    /// <summary>
    /// Avoid returning a token that is very close to expiration.
    /// </summary>
    private static readonly TimeSpan RefreshBuffer =
        TimeSpan.FromMinutes(
            5);

    private readonly TokenCredential _innerCredential;

    /// <summary>
    /// Serializes token refresh operations so three concurrent first-use
    /// batch workers do not independently initiate three credential flows.
    /// </summary>
    private readonly SemaphoreSlim _refreshGate =
        new(
            initialCount: 1,
            maxCount: 1);

    private AccessToken _cachedToken;

    private string? _cachedScopeKey;

    private bool _hasCachedToken;

    public CachingTokenCredential(
        TokenCredential innerCredential)
    {
        ArgumentNullException.ThrowIfNull(
            innerCredential);

        _innerCredential =
            innerCredential;
    }

    /// <summary>
    /// Returns a cached token synchronously when it remains valid; otherwise
    /// delegates token acquisition to the wrapped credential.
    /// </summary>
    public override AccessToken GetToken(
        TokenRequestContext requestContext,
        CancellationToken cancellationToken)
    {
        var scopeKey =
            BuildScopeKey(
                requestContext);

        _refreshGate.Wait(
            cancellationToken);

        try
        {
            if (CanUseCachedToken(
                    scopeKey))
            {
                return _cachedToken;
            }

            var token =
                _innerCredential.GetToken(
                    requestContext,
                    cancellationToken);

            CacheToken(
                scopeKey,
                token);

            return token;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    /// <summary>
    /// Returns a cached token asynchronously when it remains valid; otherwise
    /// delegates token acquisition to the wrapped credential.
    ///
    /// The semaphore is deliberately held only around credential access and
    /// cache mutation. It does not serialize OCR requests.
    /// </summary>
    public override async ValueTask<AccessToken> GetTokenAsync(
        TokenRequestContext requestContext,
        CancellationToken cancellationToken)
    {
        var scopeKey =
            BuildScopeKey(
                requestContext);

        await _refreshGate.WaitAsync(
            cancellationToken);

        try
        {
            if (CanUseCachedToken(
                    scopeKey))
            {
                return _cachedToken;
            }

            var token =
                await _innerCredential.GetTokenAsync(
                    requestContext,
                    cancellationToken);

            CacheToken(
                scopeKey,
                token);

            return token;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    /// <summary>
    /// Returns true only when the current cached token belongs to the same
    /// requested resource scopes and has sufficient remaining lifetime.
    /// </summary>
    private bool CanUseCachedToken(
        string scopeKey)
    {
        if (!_hasCachedToken)
        {
            return false;
        }

        if (!string.Equals(
                _cachedScopeKey,
                scopeKey,
                StringComparison.Ordinal))
        {
            return false;
        }

        return _cachedToken.ExpiresOn >
               DateTimeOffset.UtcNow.Add(
                   RefreshBuffer);
    }

    /// <summary>
    /// Updates the in-memory token cache while the refresh gate is held.
    /// </summary>
    private void CacheToken(
        string scopeKey,
        AccessToken token)
    {
        _cachedToken =
            token;

        _cachedScopeKey =
            scopeKey;

        _hasCachedToken =
            true;
    }

    /// <summary>
    /// Creates a stable cache key for the resource scopes requested by the
    /// Azure SDK.
    ///
    /// This prototype authenticates only to the Cognitive Services resource,
    /// so the scope collection is sufficient to distinguish its token cache.
    /// </summary>
    private static string BuildScopeKey(
        TokenRequestContext requestContext)
    {
        return string.Join(
            "\u001F",
            requestContext.Scopes);
    }
}