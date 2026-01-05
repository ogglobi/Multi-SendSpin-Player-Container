namespace MultiRoomAudio.Models;

/// <summary>
/// Information about an audio player provider.
/// </summary>
public class ProviderInfo
{
    /// <summary>
    /// The provider type identifier (e.g., "sendspin").
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// The human-readable display name for the provider.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Indicates whether the provider is currently available for use.
    /// </summary>
    public bool Available { get; set; }

    /// <summary>
    /// Optional description of the provider's capabilities.
    /// </summary>
    public string? Description { get; set; }
}
