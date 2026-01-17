using System.ComponentModel.DataAnnotations;

namespace MultiRoomAudio.Models;

/// <summary>
/// State of the relay trigger feature.
/// </summary>
public enum TriggerFeatureState
{
    /// <summary>Feature is disabled.</summary>
    Disabled,
    /// <summary>Feature is enabled but relay board not connected.</summary>
    Disconnected,
    /// <summary>Feature is enabled and relay board is connected.</summary>
    Connected,
    /// <summary>Feature is enabled but encountered an error.</summary>
    Error
}

/// <summary>
/// State of an individual relay channel.
/// </summary>
public enum RelayState
{
    /// <summary>Relay is off (NO contacts open).</summary>
    Off,
    /// <summary>Relay is on (NO contacts closed).</summary>
    On,
    /// <summary>Relay state is unknown.</summary>
    Unknown
}

/// <summary>
/// Configuration for a single trigger channel (1-8).
/// Maps a relay to a custom sink with configurable off-delay.
/// </summary>
public class TriggerConfiguration
{
    /// <summary>
    /// Channel number (1-8).
    /// </summary>
    [Range(1, 8)]
    public int Channel { get; set; }

    /// <summary>
    /// Name of the custom sink that triggers this relay.
    /// Null or empty means this trigger is not assigned.
    /// </summary>
    public string? CustomSinkName { get; set; }

    /// <summary>
    /// Delay in seconds before turning off the relay after playback stops.
    /// Default is 60 seconds (1 minute).
    /// </summary>
    [Range(0, 3600)]
    public int OffDelaySeconds { get; set; } = 60;

    /// <summary>
    /// Optional friendly name for this trigger/zone.
    /// </summary>
    [StringLength(100)]
    public string? ZoneName { get; set; }
}

/// <summary>
/// Root configuration for the trigger feature.
/// </summary>
public class TriggerFeatureConfiguration
{
    /// <summary>
    /// Whether the trigger feature is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Serial port device path for the relay board.
    /// On Linux, typically /dev/ttyUSB0 or similar for FTDI devices.
    /// </summary>
    public string? DevicePath { get; set; }

    /// <summary>
    /// FTDI device serial number for identification.
    /// Used to find the correct device when multiple FTDI devices are present.
    /// </summary>
    public string? FtdiSerialNumber { get; set; }

    /// <summary>
    /// Configuration for each trigger channel (1-8).
    /// </summary>
    public List<TriggerConfiguration> Triggers { get; set; } = new();
}

/// <summary>
/// Response for a single trigger channel status.
/// </summary>
public record TriggerResponse(
    int Channel,
    string? CustomSinkName,
    string? CustomSinkDisplayName,
    int OffDelaySeconds,
    string? ZoneName,
    RelayState RelayState,
    bool IsActive,
    DateTime? LastActivated,
    DateTime? ScheduledOffTime
);

/// <summary>
/// Response for the overall trigger feature status.
/// </summary>
public record TriggerFeatureResponse(
    bool Enabled,
    TriggerFeatureState State,
    string? DevicePath,
    string? FtdiSerialNumber,
    string? ErrorMessage,
    List<TriggerResponse> Triggers,
    byte CurrentRelayStates
);

/// <summary>
/// Request to enable/disable the trigger feature.
/// </summary>
public class TriggerFeatureEnableRequest
{
    /// <summary>
    /// Whether to enable the trigger feature.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Optional FTDI device serial number.
    /// If not specified, uses the first available FTDI device.
    /// </summary>
    public string? FtdiSerialNumber { get; set; }
}

/// <summary>
/// Request to configure a single trigger channel.
/// </summary>
public class TriggerConfigureRequest
{
    /// <summary>
    /// Channel number (1-8).
    /// </summary>
    [Required]
    [Range(1, 8, ErrorMessage = "Channel must be between 1 and 8.")]
    public int Channel { get; set; }

    /// <summary>
    /// Custom sink name to assign to this trigger.
    /// Set to null or empty to unassign.
    /// </summary>
    public string? CustomSinkName { get; set; }

    /// <summary>
    /// Off delay in seconds (0-3600).
    /// </summary>
    [Range(0, 3600, ErrorMessage = "Off delay must be between 0 and 3600 seconds.")]
    public int OffDelaySeconds { get; set; } = 60;

    /// <summary>
    /// Optional friendly name for this zone.
    /// </summary>
    [StringLength(100, ErrorMessage = "Zone name must be 100 characters or less.")]
    public string? ZoneName { get; set; }
}

/// <summary>
/// Request to manually control a relay (for testing).
/// </summary>
public class RelayManualControlRequest
{
    /// <summary>
    /// Channel number (1-8).
    /// </summary>
    [Required]
    [Range(1, 8, ErrorMessage = "Channel must be between 1 and 8.")]
    public int Channel { get; set; }

    /// <summary>
    /// Desired relay state.
    /// </summary>
    [Required]
    public bool On { get; set; }
}

/// <summary>
/// Information about a detected FTDI device.
/// </summary>
public record FtdiDeviceInfo(
    int Index,
    string? SerialNumber,
    string? Description,
    bool IsOpen
);
