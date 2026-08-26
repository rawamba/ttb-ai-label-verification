using LabelVerification.Application;
using LabelVerification.Infrastructure;
using LabelVerification.Web.Components;
using LabelVerification.Web.Health;
using LabelVerification.Web.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Register application and infrastructure dependencies through layer-specific
// extension methods so the Web project remains the composition root.
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// Register the HTTP client used by the Document Intelligence readiness probe.
// The readiness check verifies dependency reachability without performing
// a full OCR inference or consuming model quota.
builder.Services.AddHttpClient("DocumentIntelligenceHealth");

// Register application liveness and dependency-readiness health checks.
builder.Services
    .AddHealthChecks()

    // Liveness answers only whether this application process is healthy.
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy(),
        tags: ["live"])

    // Readiness additionally verifies that the external OCR dependency
    // is configured and reachable.
    .AddCheck<DocumentIntelligenceReadinessHealthCheck>(
        "document-intelligence",
        tags: ["ready"]);

// Register Blazor components and enable interactive server rendering.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Establish request correlation before other middleware executes so logs,
// errors, OCR processing, and verification activity can share one identifier.
app.UseMiddleware<CorrelationIdMiddleware>();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);

    // Enforce HTTP Strict Transport Security in non-development environments.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Liveness probe.
//
// Used by deployment automation and monitoring to determine whether the
// application process itself is running. External AI dependencies are
// intentionally excluded from this probe.
app.MapHealthChecks(
    "/health",
    new HealthCheckOptions
    {
        Predicate = registration =>
            registration.Tags.Contains("live")
    });

// Readiness probe.
//
// Determines whether the application is ready to process label-verification
// requests, including availability of required external OCR dependencies.
app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = registration =>
            registration.Tags.Contains("ready")
    });

app.Run();