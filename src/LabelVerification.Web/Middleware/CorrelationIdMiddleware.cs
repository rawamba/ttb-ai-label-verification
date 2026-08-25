namespace LabelVerification.Web.Middleware;

/// <summary>
/// Adds a consistent correlation identifier to each HTTP request so logs,
/// diagnostics, OCR processing, and verification activity can be traced
/// across the complete request lifecycle.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ContextItemName = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    /// <summary>
    /// Creates the middleware using the next component in the ASP.NET Core
    /// request pipeline.
    /// </summary>
    public CorrelationIdMiddleware(
        RequestDelegate next,
        ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Processes the current HTTP request and establishes its correlation ID.
    ///
    /// ASP.NET Core requires conventional middleware registered through
    /// UseMiddleware to expose a public Invoke or InvokeAsync method.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // ASP.NET Core generates a request-specific TraceIdentifier.
        // Reusing it avoids maintaining a separate identifier-generation
        // mechanism for the prototype.
        var correlationId = context.TraceIdentifier;

        // Make the correlation identifier available to downstream application
        // components that may need to associate processing with this request.
        context.Items[ContextItemName] = correlationId;

        // Return the ID to the caller so operational troubleshooting can
        // correlate a request with server-side logs.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;

            return Task.CompletedTask;
        });

        // Add the correlation identifier to structured logging for everything
        // executed downstream during this request.
        using (_logger.BeginScope(
                   new Dictionary<string, object>
                   {
                       ["CorrelationId"] = correlationId
                   }))
        {
            await _next(context);
        }
    }
}