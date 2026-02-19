# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-f07c6b3

**Features in Development** (targeting 5.0 release)

- **12V Trigger Relay Control** - Automatic amplifier power management via USB HID, FTDI, and Modbus relay boards
- **Player Mute Button** - Bidirectional mute sync with Music Assistant
- **Now Playing Info** - Track title, artist, album in Player Details modal
- **Device Capabilities** - Shows supported sample rates, bit depths, channels
- **Volume Persistence** - Volume survives container restarts
- **Reconnection UX** - Startup progress overlay, WaitingForServer state, auto-reconnect
- **Sync Improvements** - Anti-oscillation debounce, latency lock-in
- **Mono Output** - Remap sinks support single-channel output
- **International Names** - Unicode player names (emojis, CJK, etc.)
- **SendSpin.SDK 6.1.1** - Major protocol improvements

> WARNING: This is a development build. For stable releases, use the stable add-on.
<!-- VERSION_INFO_END -->

---

## Warning

Development builds:
- May contain bugs or incomplete features
- Could have breaking changes between builds
- Are not recommended for production use

## Installation

This add-on is automatically updated whenever code is pushed to the `dev` branch.
The version number (sha-XXXXXXX) indicates the commit it was built from.

## Reporting Issues
