using AutomationEngine.Application.Extensions;
using AutomationEngine.Infrastructure.Extensions;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// ── Cloud Run: listen on PORT env var (default 8080) ─────────────────────────
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// ── Structured JSON logging for Google Cloud Logging ─────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.JsonWriterOptions = new JsonWriterOptions { Indented = false };
});

// ── Service registration ──────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Onion layers: Application (use cases) + Infrastructure (Google Cloud adapters)
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddGoogleCloudInfrastructure(builder.Configuration);

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// Serve wwwroot/ static files (frontend UI)
app.UseDefaultFiles();   // index.html as default document
app.UseStaticFiles();

app.MapControllers();
app.Run();

