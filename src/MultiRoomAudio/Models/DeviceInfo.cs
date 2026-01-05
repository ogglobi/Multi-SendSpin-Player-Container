namespace MultiRoomAudio.Models;

// =============================================================================
// API Response Models
// =============================================================================
// This file contains lightweight API response types used across multiple endpoints.
// These types are grouped together because:
// 1. They are all simple record types with minimal logic
// 2. They serve a common purpose: API serialization
// 3. Splitting into separate files would create unnecessary file proliferation
//    for types that are each under 10 lines
//
// Device-specific types: AudioDevice, DevicesListResponse
// Generic response types: ErrorResponse, SuccessResponse, HealthResponse
// =============================================================================

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
