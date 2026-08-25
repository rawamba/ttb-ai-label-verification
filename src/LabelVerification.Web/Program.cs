using LabelVerification.Application;
using LabelVerification.Infrastructure;
using LabelVerification.Web.Components;
using LabelVerification.Web.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Register application and infrastructure dependencies through layer-specific
// extension methods so the Web project remains the composition root.
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// Register Blazor components and enable interactive server rendering.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register a lightweight health check endpoint so deployment automation and
// operators can verify that the application host is running successfully.
builder.Services.AddHealthChecks();

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

// Expose a platform-neutral endpoint for deployment smoke tests and
// operational availability checks.
app.MapHealthChecks("/health");

app.Run();