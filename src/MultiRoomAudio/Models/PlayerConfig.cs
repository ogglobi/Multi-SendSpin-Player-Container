namespace MultiRoomAudio.Models;

/// <summary>
/// Request to create a new player.
/// </summary>
public class PlayerCreateRequest
{
    public required string Name { get; set; }
    public string? Device { get; set; }
    public string? ClientId { get; set; }
    public string? ServerUrl { get; set; }
    public int Volume { get; set; } = 75;
    public int DelayMs { get; set; }
    public string LogLevel { get; set; } = "INFO";
    public string Codec { get; set; } = "opus";
    public int BufferSizeMs { get; set; } = 100;
}

/// <summary>
/// Request to switch audio device.
/// </summary>
public record DeviceSwitchRequest(string Device);

/// <summary>
/// Request to set volume.
/// </summary>
public record VolumeRequest(int Volume);

/// <summary>
/// Request to update offset.
/// </summary>
public record OffsetRequest(int DelayMs);

/// <summary>
/// Player configuration stored in memory.
/// </summary>
public class PlayerConfig
{
    public required string Name { get; set; }
    public required string ClientId { get; set; }
    public string? DeviceId { get; set; }
    public string? ServerUrl { get; set; }
    public int DelayMs { get; set; }
    public string LogLevel { get; set; } = "INFO";
    public string Codec { get; set; } = "opus";
    public int BufferSizeMs { get; set; } = 100;
    public int Volume { get; set; } = 75;
    public bool IsMuted { get; set; }
}
