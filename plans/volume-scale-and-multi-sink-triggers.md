# Feature Plan: Volume Scale & Multi-Sink Triggers

## Overview

Two feature requests from user:
1. **Volume Scale** - Global volume attenuation factor to reduce overall loudness
2. **Multi-Sink Triggers** - Allow relays to be triggered by multiple sinks

---

## Feature 1: Volume Scale (Lautstärke-Dämpfer)

### Problem
- User reports that even at 1% volume, the audio is too loud
- Need a global attenuation factor that can be configured per player

### Current Architecture
- Volume is stored as 0-100 in `PlayerConfig.Volume`
- Applied to player via `context.Player.Volume = volume / 100.0f`
- Hardware volume is set to 80% by default (`HardwareVolumePercent`)

### Proposed Solution

Add **BOTH** global and per-player volume scale:

```
EffectiveVolume = Volume * GlobalVolumeScale * PlayerVolumeScale
```

Example:
- Volume: 50%
- GlobalVolumeScale: 0.5 (50% global limit)
- PlayerVolumeScale: 0.6 (60% player limit)
- EffectiveVolume: 50% * 0.5 * 0.6 = 15%

### Files to Modify

#### 1. Services/ConfigurationService.cs - Add Global Setting
```csharp
// Add to DeviceConfiguration or new GlobalConfiguration
/// <summary>
/// Global volume scale factor (0.01-1.0). Applied to all players.
/// Use this to reduce maximum loudness system-wide. Default is 1.0 (no reduction).
/// </summary>
public decimal GlobalVolumeScale { get; set; } = 1.0m;
```

#### 2. Models/PlayerConfig.cs - Add Per-Player Setting
```csharp
// Add to PlayerCreateRequest
/// <summary>
/// Per-player volume scale factor (0.01-1.0). Multiplied with global scale.
/// Use this to reduce maximum loudness for this player. Default is 1.0 (no reduction).
/// </summary>
[Range(0.01, 1.0, ErrorMessage = "VolumeScale must be between 0.01 and 1.0.")]
public decimal? VolumeScale { get; set; }

// Add to PlayerConfig
public decimal? VolumeScale { get; set; }  // null = use global

// Add to PlayerUpdateRequest
[Range(0.01, 1.0, ErrorMessage = "VolumeScale must be between 0.01 and 1.0.")]
public decimal? VolumeScale { get; set; }
```

#### 3. Services/PlayerManagerService.cs
```csharp
// Modify volume application
var globalScale = _config.GlobalVolumeScale;  // Get from config
var playerScale = context.Config.VolumeScale ?? 1.0m;  // Player override or default
var effectiveVolume = (volume / 100.0f) * (float)globalScale * (float)playerScale;
context.Player.Volume = effectiveVolume;
```

#### 4. Controllers/PlayersEndpoint.cs
- Add PUT /api/players/{name}/volume-scale endpoint

#### 5. Controllers/SettingsEndpoint.cs (new or existing)
- Add GET/PUT /api/settings/volume-scale for global setting

#### 6. wwwroot/js/app.js
- Add global volume scale slider in settings
- Add per-player volume scale slider in player settings

### UI Design
- **Global**: Settings page, Volume Scale slider (1% - 100%, default 100%)
- **Per-Player**: Player settings panel, Volume Scale slider (1% - 100%, default = use global)
- Label: Max Volume Limit or Volume Attenuation

---

## Feature 2: Multi-Sink Triggers

### Problem
- Currently each relay channel can only be assigned to ONE custom sink
- User wants to assign multiple sinks to a single relay
- Example: One relay controls an amplifier that receives audio from multiple zones

### Current Architecture
```csharp
// TriggerConfiguration.cs
public class TriggerConfiguration
{
    public int Channel { get; set; }
    public string? CustomSinkName { get; set; }  // Single sink
    public int OffDelaySeconds { get; set; } = 60;
    public string? ZoneName { get; set; }
}
```

### Proposed Solution

Change to support multiple sinks:

```csharp
public class TriggerConfiguration
{
    public int Channel { get; set; }
    
    // New: Support multiple sinks
    public List<string> CustomSinkNames { get; set; } = new();
    
    // Legacy: Keep for backward compatibility during migration
    [Obsolete("Use CustomSinkNames instead")]
    public string? CustomSinkName { get; set; }
    
    public int OffDelaySeconds { get; set; } = 60;
    public string? ZoneName { get; set; }
}
```

### Behavior
- Relay turns ON when ANY of the assigned sinks becomes active
- Relay turns OFF after delay when ALL assigned sinks are inactive

### Files to Modify

#### 1. Models/TriggerModels.cs
- Add `CustomSinkNames` property
- Add migration logic for legacy `CustomSinkName`

#### 2. Services/TriggerService.cs
- Modify `CheckTriggersAsync` to check all sinks in list
- Change logic from single sink check to any/all logic

#### 3. Controllers/TriggersEndpoint.cs
- Update API to accept list of sink names
- Add PUT endpoint for updating trigger sink assignments

#### 4. wwwroot/js/app.js
- Change single-select to multi-select dropdown
- Update trigger configuration UI

### Migration Strategy
```csharp
// On config load, migrate legacy format
if (!string.IsNullOrEmpty(CustomSinkName) && CustomSinkNames.Count == 0)
{
    CustomSinkNames = new List<string> { CustomSinkName };
    CustomSinkName = null; // Clear legacy
}
```

---

## Implementation Order

### Phase 1: Volume Scale (Global + Per-Player)
1. [ ] Add GlobalVolumeScale to ConfigurationService
2. [ ] Add VolumeScale to PlayerConfig models
3. [ ] Update PlayerManagerService to apply both scales
4. [ ] Add API endpoints for global and per-player volume scale
5. [ ] Add UI controls (global settings + player settings)

### Phase 2: Multi-Sink Triggers
1. [ ] Update TriggerModels with CustomSinkNames list
2. [ ] Add migration logic for legacy CustomSinkName
3. [ ] Update TriggerService with OR logic for multiple sinks
4. [ ] Update API endpoints
5. [ ] Update UI to multi-select dropdown

---

## User Decisions

1. **Volume Scale**: **BOTH** global AND per-player
   - Global default value that applies to all players
   - Per-player override option
   - Formula: `EffectiveVolume = Volume * GlobalScale * PlayerScale`

2. **Multi-Sink Triggers**: **OR logic**
   - Relay ON when ANY of the assigned sinks is active
   - Relay OFF after delay when ALL assigned sinks are inactive

3. **Implementation**: Both features together
