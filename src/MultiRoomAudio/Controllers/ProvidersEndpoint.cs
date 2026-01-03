using MultiRoomAudio.Models;

namespace MultiRoomAudio.Controllers;

/// <summary>
/// REST API endpoints for provider information.
/// </summary>
public static class ProvidersEndpoint
{
    public static void MapProvidersEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/providers")
            .WithTags("Providers")
            .WithOpenApi();

        // GET /api/providers - List available providers
        group.MapGet("/", () =>
        {
            // Sendspin-only implementation
            var providers = new[]
            {
                new ProviderInfo
                {
                    Type = "sendspin",
                    DisplayName = "Sendspin",
                    Available = true,
                    Description = "Native SendSpin.SDK audio streaming"
                }
            };

            return Results.Ok(providers);
        })
        .WithName("ListProviders")
        .WithDescription("Get available audio player providers");
    }
}

/// <summary>
/// Information about an audio player provider.
/// </summary>
public class ProviderInfo
{
    public required string Type { get; set; }
    public required string DisplayName { get; set; }
    public bool Available { get; set; }
    public string? Description { get; set; }
}
