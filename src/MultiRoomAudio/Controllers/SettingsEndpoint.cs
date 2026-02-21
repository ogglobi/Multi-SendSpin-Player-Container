using Microsoft.AspNetCore.Mvc;
using MultiRoomAudio.Services;
using MultiRoomAudio.Utilities;
using System.Text.Json.Serialization;

namespace MultiRoomAudio.Controllers;

/// <summary>
/// REST API endpoints for global settings.
/// </summary>
public static class SettingsEndpoint
{
    /// <summary>
    /// Registers all settings API endpoints with the application.
    /// </summary>
    public static void MapSettingsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/settings")
            .WithTags("Settings")
            .WithOpenApi();

        // GET /api/settings - Get all global settings
        group.MapGet("/", (ConfigurationService config, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("SettingsEndpoint");
            logger.LogDebug("API: GET /api/settings");
            
            return Results.Ok(new
            {
                globalVolumeScale = config.GlobalVolumeScale
            });
        })
        .WithName("GetSettings")
        .WithDescription("Get all global settings");

        // GET /api/settings/volume-scale - Get global volume scale
        group.MapGet("/volume-scale", (ConfigurationService config, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("SettingsEndpoint");
            logger.LogDebug("API: GET /api/settings/volume-scale");
            
            return Results.Ok(new VolumeScaleResponse(config.GlobalVolumeScale));
        })
        .WithName("GetGlobalVolumeScale")
        .WithDescription("Get the global volume scale factor (0.01-1.0)");

        // PUT /api/settings/volume-scale - Set global volume scale
        group.MapPut("/volume-scale", (
            double volumeScale,
            ConfigurationService config,
            PlayerManagerService? playerManager,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("SettingsEndpoint");
            logger.LogInformation("API: PUT /api/settings/volume-scale: {VolumeScale}", volumeScale);

            return ApiExceptionHandler.Execute(() =>
            {
                var scale = Math.Clamp((decimal)volumeScale, 0.01m, 1.0m);
                var success = config.SetGlobalVolumeScale(scale);
                
                if (!success)
                {
                    return Results.Problem(
                        statusCode: 500,
                        title: "Failed to save global volume scale");
                }

                // Apply the new scale to all active players
                if (playerManager != null)
                {
                    ApplyVolumeScaleToAllPlayers(playerManager, config, logger);
                }

                logger.LogInformation("Global volume scale set to {VolumeScale}", scale);
                return Results.Ok(new VolumeScaleResponse(scale));
            }, logger, "set global volume scale");
        })
        .WithName("SetGlobalVolumeScale")
        .WithDescription("Set the global volume scale factor (0.01-1.0). Applied to all players.");

        // PUT /api/players/{name}/volume-scale - Set per-player volume scale
        app.MapPut("/api/players/{name}/volume-scale", (
            string name,
            double volumeScale,
            ConfigurationService config,
            PlayerManagerService playerManager,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("SettingsEndpoint");
            logger.LogInformation("API: PUT /api/players/{Name}/volume-scale: {VolumeScale}", name, volumeScale);

            return ApiExceptionHandler.Execute(() =>
            {
                // Check if player exists in config
                if (!config.PlayerExists(name))
                {
                    return Results.NotFound(new { error = $"Player '{name}' not found" });
                }

                var scale = Math.Clamp((decimal)volumeScale, 0.01m, 1.0m);
                
                // Update the player's volume scale in config
                config.UpdatePlayerField(name, cfg => cfg.VolumeScale = scale, save: true);
                
                // Apply the new scale to the player's current volume (if player is active)
                playerManager.RefreshPlayerVolume(name);

                logger.LogInformation("Player '{Name}' volume scale set to {VolumeScale}", name, scale);
                return Results.Ok(new VolumeScaleResponse(scale));
            }, logger, "set player volume scale");
        })
        .WithName("SetPlayerVolumeScale")
        .WithDescription("Set per-player volume scale factor (0.01-1.0). Overrides global scale for this player.");
    }

    /// <summary>
    /// Apply the current volume scale to all active players.
    /// </summary>
    private static void ApplyVolumeScaleToAllPlayers(
        PlayerManagerService playerManager,
        ConfigurationService config,
        ILogger logger)
    {
        try
        {
            var playersResponse = playerManager.GetAllPlayers();
            foreach (var player in playersResponse.Players)
            {
                playerManager.RefreshPlayerVolume(player.Name);
            }
            logger.LogInformation("Applied new volume scale to {Count} players", playersResponse.Players.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to apply volume scale to all players");
        }
    }
}

/// <summary>
/// Request to set volume scale.
/// </summary>
public class VolumeScaleRequest
{
    /// <summary>
    /// Volume scale factor (0.01-1.0).
    /// </summary>
    public double VolumeScale { get; set; }
}

/// <summary>
/// Response for volume scale operations.
/// </summary>
/// <param name="VolumeScale">Current volume scale factor.</param>
public record VolumeScaleResponse(decimal VolumeScale);
