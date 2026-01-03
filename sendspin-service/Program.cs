using SendspinService.Endpoints;
using SendspinService.Services;

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
        Title = "Sendspin Service API",
        Version = "v1",
        Description = "REST API for managing Sendspin audio players. Provides device enumeration, player lifecycle management, and real-time control."
    });
});

// Add CORS for Python Flask app
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
    {
        policy.WithOrigins(
            "http://localhost:8080",
            "http://127.0.0.1:8080",
            "http://localhost:5000",
            "http://127.0.0.1:5000"
        )
        .AllowAnyMethod()
        .AllowAnyHeader();
    });
});

// Add health checks
builder.Services.AddHealthChecks();

// Add PlayerManagerService as singleton and hosted service
builder.Services.AddSingleton<PlayerManagerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PlayerManagerService>());

// Configure Kestrel to listen on port 5100
builder.WebHost.ConfigureKestrel(options =>
{
    var port = int.Parse(Environment.GetEnvironmentVariable("PORT") ?? "5100");
    options.ListenLocalhost(port);
});

var app = builder.Build();

// Configure middleware pipeline
app.UseCors("AllowLocalhost");

// Enable Swagger in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Sendspin Service API v1");
        c.RoutePrefix = "swagger";
    });
}

// Map health check endpoints
app.MapHealthChecks("/healthz");

// Map custom endpoints
app.MapHealthEndpoints();
app.MapPlayersEndpoints();
app.MapDevicesEndpoints();

// Root endpoint with service info
app.MapGet("/", () => Results.Ok(new
{
    service = "sendspin-service",
    description = "REST API for Sendspin audio players",
    version = "1.0.0",
    endpoints = new
    {
        health = "/health",
        ready = "/health/ready",
        live = "/health/live",
        status = "/api/status",
        players = "/api/players",
        devices = "/api/devices",
        swagger = "/swagger"
    }
}))
.WithTags("Info")
.WithName("ServiceInfo")
.ExcludeFromDescription();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Sendspin Service starting on port {Port}",
    Environment.GetEnvironmentVariable("PORT") ?? "5100");

app.Run();
