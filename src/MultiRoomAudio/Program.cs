using MultiRoomAudio.Controllers;
using MultiRoomAudio.Services;
using MultiRoomAudio.Utilities;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var logLevel = Environment.GetEnvironmentVariable("LOG_LEVEL")?.ToLower() switch
{
    "debug" => LogLevel.Debug,
    "trace" => LogLevel.Trace,
    "warning" => LogLevel.Warning,
    "error" => LogLevel.Error,
    _ => LogLevel.Information
};
builder.Logging.SetMinimumLevel(logLevel);

// Configure services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Multi-Room Audio Controller API",
        Version = "v2",
        Description = "REST API for managing Sendspin audio players. Provides device enumeration, player lifecycle management, and real-time control."
    });
});

// Add SignalR for real-time status updates
builder.Services.AddSignalR();

// Add CORS for web UI and external access
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add health checks
builder.Services.AddHealthChecks();

// Core services (singletons for shared state)
builder.Services.AddSingleton<EnvironmentService>();
builder.Services.AddSingleton<ConfigurationService>();
builder.Services.AddSingleton(sp =>
{
    var env = sp.GetRequiredService<EnvironmentService>();
    var logger = sp.GetRequiredService<ILogger<AlsaCommandRunner>>();
    return new AlsaCommandRunner(logger, env.UsePulseAudio);
});

// Add PlayerManagerService as singleton and hosted service
builder.Services.AddSingleton<PlayerManagerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PlayerManagerService>());

// Serve static files from wwwroot
builder.Services.AddDirectoryBrowser();

// Configure Kestrel to listen on port 8096 (or PORT env var)
var port = int.Parse(Environment.GetEnvironmentVariable("WEB_PORT")
    ?? Environment.GetEnvironmentVariable("PORT")
    ?? "8096");

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(port);
});

var app = builder.Build();

// Configure middleware pipeline
app.UseCors("AllowAll");

// Serve static files (wwwroot)
app.UseDefaultFiles();
app.UseStaticFiles();

// Enable Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Multi-Room Audio API v2");
    c.RoutePrefix = "docs";
});

// Map health check endpoints
app.MapHealthChecks("/healthz");

// Map SignalR hub
// TODO: app.MapHub<PlayerStatusHub>("/hubs/status");

// Map API endpoints
app.MapHealthEndpoints();
app.MapPlayersEndpoints();
app.MapDevicesEndpoints();
app.MapProvidersEndpoints();

// Root endpoint redirects to index.html or shows API info
app.MapGet("/api", () => Results.Ok(new
{
    service = "multi-room-audio",
    description = "Sendspin-only Multi-Room Audio Controller",
    version = "2.0.0",
    endpoints = new
    {
        health = "/api/health",
        players = "/api/players",
        devices = "/api/devices",
        providers = "/api/providers",
        swagger = "/docs"
    }
}))
.WithTags("Info")
.WithName("ApiInfo");

var logger = app.Services.GetRequiredService<ILogger<Program>>();
var environmentService = app.Services.GetRequiredService<EnvironmentService>();

logger.LogInformation("Multi-Room Audio Controller starting on port {Port}", port);
logger.LogInformation("Environment: {Env}", environmentService.EnvironmentName);
logger.LogInformation("Config path: {Path}", environmentService.ConfigPath);
logger.LogInformation("Audio backend: {Backend}", environmentService.AudioBackend);

app.Run();
