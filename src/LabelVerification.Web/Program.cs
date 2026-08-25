using LabelVerification.Application;
using LabelVerification.Infrastructure;
using LabelVerification.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Register application and infrastructure dependencies through layer-specific
// extension methods so the Web project remains the composition root.
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// Register Blazor components and enable interactive server rendering.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

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

app.Run();