namespace MultiRoomAudio.Models;

/// <summary>
/// Audio device information.
/// </summary>
public record AudioDevice(
    int Index,
    string Id,
    string Name,
    int MaxChannels,
    int DefaultSampleRate,
    int DefaultLowLatencyMs,
    int DefaultHighLatencyMs,
    bool IsDefault
);

/// <summary>
/// Response containing device list.
/// </summary>
public record DevicesListResponse(
    List<AudioDevice> Devices,
    int Count
);

/// <summary>
/// Error response format.
/// </summary>
public record ErrorResponse(
    bool Success,
    string Message
);

/// <summary>
/// Success response format.
/// </summary>
public record SuccessResponse(
    bool Success,
    string Message
);

/// <summary>
/// Health check response.
/// </summary>
public record HealthResponse(
    string Status,
    DateTime Timestamp,
    string Version
);

/// <summary>
/// Player statistics for health check.
/// </summary>
public record PlayerStats(
    int Total,
    int Running,
    int Connected,
    int Failed
);
