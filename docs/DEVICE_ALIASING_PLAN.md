# Device Identification and Aliasing Plan

## Problem

Multiple identical USB audio devices ("USB Audio Device") can't be differentiated, and their IDs (`alsa_output_hw_X_0`) change on reboot based on USB enumeration order.

## Solution Overview

1. **Extract stable identifiers** from PulseAudio (serial, bus path, vendor/product ID)
2. **Allow user-defined aliases** (e.g., "Kitchen Speaker")
3. **Auto re-match devices** on startup using stable identifiers
4. **Persist device registry** in `devices.yaml`

---

## Implementation Steps

### Step 1: Extend AudioDevice Model

**File:** `src/MultiRoomAudio/Models/DeviceInfo.cs`

Add new record for stable identifiers:
```csharp
public record DeviceIdentifiers(
    string? Serial,           // device.serial
    string? BusPath,          // device.bus_path (stable per USB port)
    string? VendorId,         // device.vendor.id
    string? ProductId,        // device.product.id
    string? AlsaLongCardName  // alsa.long_card_name
);
```

Extend `AudioDevice` with:
- `DeviceIdentifiers? Identifiers`
- `string? Alias`

---

### Step 2: Parse Additional Properties from PulseAudio

**File:** `src/MultiRoomAudio/Audio/PulseAudio/PulseAudioDeviceEnumerator.cs`

Add regex patterns to extract from `pactl list sinks` Properties section:
- `device.serial`
- `device.bus_path`
- `device.vendor.id`
- `device.product.id`
- `alsa.long_card_name`

Modify `ParseSinkBlock` to populate `DeviceIdentifiers`.

---

### Step 3: Add Device Persistence

**File:** `src/MultiRoomAudio/Services/ConfigurationService.cs`

Add device alias storage:
- New file: `devices.yaml` in config directory
- Store: alias, last known sink name, stable identifiers, timestamps
- Methods: `LoadDevices()`, `SaveDevices()`, `SetDeviceAlias()`, `GetDeviceAlias()`

**YAML Format:**
```yaml
usb_08bb_2902_port3:
  alias: "Kitchen Speaker"
  last_known_sink_name: "alsa_output_hw_1_0"
  identifiers:
    bus_path: "pci-0000:00:14.0-usb-0:3:1.0"
    vendor_id: "08bb"
    product_id: "2902"
```

---

### Step 4: Create Device Matching Service

**New File:** `src/MultiRoomAudio/Services/DeviceMatchingService.cs`

Re-match persisted devices to current sinks using priority:
1. Serial number (exact match)
2. Bus path (same USB port)
3. Vendor+Product ID + name similarity

Methods:
- `FindCurrentSinkName(DeviceConfiguration)` - find current sink for a persisted device
- `MatchAllDevices()` - match all and return mapping
- `UpdatePlayerDevices()` - update player configs with new sink names

---

### Step 5: Add API Endpoints

**File:** `src/MultiRoomAudio/Controllers/DevicesEndpoint.cs`

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/devices/{id}/alias` | PUT | Set device alias |
| `/api/devices/aliases` | GET | Get all aliases (not used by UI - included in /api/devices) |
| `/api/devices/rematch` | POST | Force device re-matching (not used by UI) |

Extend existing `/api/devices` response to include `identifiers` and `alias`.

---

### Step 6: Integrate with Player Startup

**File:** `src/MultiRoomAudio/Services/PlayerManagerService.cs`

On `StartAsync`:
1. Load device registry
2. Run `DeviceMatchingService.MatchAllDevices()`
3. Update player configs if devices re-matched to new sink names
4. Log warnings for unmatched devices
5. Proceed with autostart

---

### Step 7: UI Updates

**Files:** `src/MultiRoomAudio/wwwroot/`

- Show alias in device dropdown (original name as tooltip/subtitle)
- Add "Edit Alias" button on device details or player creation form
- Show stable identifier info (bus path, serial) for debugging
- Visual indicator when device has been re-matched after reboot

---

## Files to Modify

| File | Changes |
|------|---------|
| `src/MultiRoomAudio/Models/DeviceInfo.cs` | Add `DeviceIdentifiers` record, extend `AudioDevice` |
| `src/MultiRoomAudio/Audio/PulseAudio/PulseAudioDeviceEnumerator.cs` | Parse additional pactl properties |
| `src/MultiRoomAudio/Services/ConfigurationService.cs` | Add device persistence methods |
| `src/MultiRoomAudio/Controllers/DevicesEndpoint.cs` | Add alias endpoints |
| `src/MultiRoomAudio/Services/PlayerManagerService.cs` | Integrate re-matching on startup |

## New Files

| File | Purpose |
|------|---------|
| `src/MultiRoomAudio/Services/DeviceMatchingService.cs` | Device re-matching logic |

---

## Key Considerations

1. **Not all USB DACs have serial numbers** - Bus path is primary fallback
2. **Bus path is stable per USB port** - Moving device to different port breaks matching
3. **Device key format:** `{vendor_id}_{product_id}_{bus_path_hash}` or serial if available
4. **Migration:** First run creates registry entries for existing player device references
