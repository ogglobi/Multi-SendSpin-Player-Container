namespace MultiRoomAudio.Models;

/// <summary>
/// Summary of multi-room sync status across all playing players.
/// Used by the UI to show inter-player drift at a glance.
/// </summary>
public record SyncSummaryResponse(
    /// <summary>Number of players currently in Playing state.</summary>
    int PlayingCount,
    /// <summary>Lowest sync error among playing players (ms). Null if no players playing.</summary>
    double? MinSyncErrorMs,
    /// <summary>Highest sync error among playing players (ms). Null if no players playing.</summary>
    double? MaxSyncErrorMs,
    /// <summary>Max - Min sync error (ms). Null if fewer than 2 players playing.</summary>
    double? InterPlayerDriftMs,
    /// <summary>True if all playing players are within sync tolerance.</summary>
    bool AllWithinTolerance,
    /// <summary>Correction mode: "Adaptive", "Standard", or "Mixed".</summary>
    string CorrectionMode
);
