using SendspinService.Audio;
using SendspinService.Models;

namespace SendspinService.Endpoints;

/// <summary>
/// REST API endpoints for audio device enumeration.
/// </summary>
public static class DevicesEndpoint
{
    public static void MapDevicesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/devices")
            .WithTags("Devices")
            .WithOpenApi();

        // GET /api/devices - List all output devices
        group.MapGet("/", () =>
        {
            try
            {
                var devices = PortAudioDeviceEnumerator.GetOutputDevices().ToList();
                return Results.Ok(new
                {
                    devices,
                    count = devices.Count,
                    defaultDevice = devices.FirstOrDefault(d => d.IsDefault)?.Id
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 500,
                    title: "Failed to enumerate devices");
            }
        })
        .WithName("ListDevices")
        .WithDescription("List all available audio output devices");

        // GET /api/devices/{id} - Get specific device
        group.MapGet("/{id}", (string id) =>
        {
            try
            {
                var device = PortAudioDeviceEnumerator.GetDevice(id);
                if (device == null)
                    return Results.NotFound(new ErrorResponse(false, $"Device '{id}' not found"));

                return Results.Ok(device);
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 500,
                    title: "Failed to get device info");
            }
        })
        .WithName("GetDevice")
        .WithDescription("Get details of a specific audio device");

        // GET /api/devices/default - Get default device
        group.MapGet("/default", () =>
        {
            try
            {
                var device = PortAudioDeviceEnumerator.GetDefaultDevice();
                if (device == null)
                    return Results.NotFound(new ErrorResponse(false, "No default output device found"));

                return Results.Ok(device);
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 500,
                    title: "Failed to get default device");
            }
        })
        .WithName("GetDefaultDevice")
        .WithDescription("Get the default audio output device");

        // POST /api/devices/refresh - Refresh device list
        group.MapPost("/refresh", () =>
        {
            try
            {
                PortAudioDeviceEnumerator.RefreshDevices();
                var devices = PortAudioDeviceEnumerator.GetOutputDevices().ToList();

                return Results.Ok(new
                {
                    message = "Device list refreshed",
                    devices,
                    count = devices.Count
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 500,
                    title: "Failed to refresh devices");
            }
        })
        .WithName("RefreshDevices")
        .WithDescription("Re-enumerate audio devices (detect newly connected USB devices)");
    }
}
