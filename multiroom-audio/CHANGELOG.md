# Changelog

## [1.2.12] - Persistent Crash Logging

### Changed
- **Crash traces now written to file**: Faulthandler output is saved to `/data/logs/crash.log` (in addition to stderr)
- **Previous crash detection**: On startup, if a crash.log exists from a previous run, its contents are printed to the supervisor log
- This ensures crash diagnostics are captured even if stderr buffer doesn't flush before the segfault

### Notes
- After a crash, restart the add-on and check the supervisor logs for "PREVIOUS CRASH DETECTED" message
- The crash.log is cleared after being displayed to avoid repeated warnings

---

## [1.2.11] - Crash Diagnostics

### Added
- **Faulthandler enabled**: Python stack traces will now be printed on segfaults to help diagnose crashes
- This will show exactly which Python code was running when a native library (PortAudio, etc.) crashes

---

## [1.2.10] - Stability Improvements

### Added
- Signal handlers (SIGTERM, SIGINT) for graceful shutdown on HAOS
- Enhanced startup logging with environment details (log path, config path)

### Changed
- Process manager now uses safer subprocess handling that works across all container environments
- Improved error handling around process group creation (os.setsid)

---

## [1.2.9] - Fix Ingress & Sendspin Device Detection

### Fixed
- **404 on /api/providers through Ingress**: Moved Swagger UI from `/api/docs` to `/docs` to avoid route conflicts with HA Ingress proxy
- **Empty Sendspin device list on HAOS**: Added PulseAudio sink fallback when PortAudio doesn't detect any devices
- **Test tone fails on PulseAudio sinks**: Added `paplay` support for testing Bluetooth and PulseAudio devices (e.g., `bluez_sink.XXX.a2dp_sink`)
- The player type dropdown was empty when accessed through HA sidebar (now fixed)

---

## [1.2.8] - Fix Provider Detection & API Error Handling

### Fixed
- **Empty player type dropdown**: Providers now fall back to showing all registered providers (with availability status) when no binaries are found in PATH
- **JSON parsing errors on device test**: Added global error handlers to return JSON instead of HTML for all 500 errors
- **Binary detection on HAOS**: Moved squeezelite to `/usr/bin/` and added explicit PATH configuration in startup script
- **Cross-origin access issues**: Added CORS headers to support both Ingress and direct access

### Added
- Detailed logging for provider availability checks (shows PATH when binary not found)
- Console logging in JavaScript for API debugging
- Binary verification step during Docker build
- Fallback PATH discovery for binaries in non-standard locations

### Changed
- Startup script now logs full PATH and binary locations for easier debugging
- Provider dropdown shows "(not installed)" for unavailable providers

---

## [1.2.7] - Fix Audio Device Detection on HAOS

### Fixed
- **No audio devices on HAOS**: Now uses PulseAudio (`pactl list sinks`) for device detection on HAOS instead of ALSA (`aplay -l`)
- **Sendspin shows no devices**: Added stderr capture and logging for `sendspin --list-audio-devices` to diagnose PortAudio issues
- **Devices missing after reboot**: PulseAudio detection is more reliable than ALSA on HAOS

### Changed
- AudioManager now auto-detects environment and uses appropriate backend (PulseAudio on HAOS, ALSA on standalone Docker)

---

## [1.2.6] - Fix PortAudio Segfault on HAOS

### Fixed
- **Segmentation fault on HAOS startup**: Made `sounddevice`/`numpy` imports lazy to avoid PortAudio C extension crash during module load
- PortAudio initialization on Alpine Linux (HAOS) can segfault before Python exception handling catches it
- Libraries are now only imported when `play_test_tone()` is actually called, not at app startup

---

## [1.2.5] - Fix HAOS Startup Crash

### Fixed
- **FileNotFoundError on HAOS startup**: App now correctly uses `LOG_PATH` and `CONFIG_PATH` environment variables instead of hardcoded `/app/logs` and `/app/config` paths
- Creates log directory before initializing file logging handler
- HAOS add-on sets these paths to `/data/logs` and `/data` which weren't being respected

---

## [1.2.4] - Simplify Add-on Config

### Changed
- Removed direct port exposure; now uses ingress only (sidebar access)
- Removed `default_server_ip` option; set server IP per-player in web UI instead
- Add-on configuration now only has `log_level` setting

---

## [1.2.2] - Startup Fix

### Fixed
- **Segmentation fault on startup**: Replaced gunicorn with Flask's built-in server to avoid fork-related crashes with SocketIO and audio libraries (sounddevice/PortAudio)
- The gunicorn pre-fork model caused segfaults when native audio libraries were initialized before the fork

### Technical Details
- Flask's development server is sufficient for home automation with limited concurrent users
- Eliminates race conditions between gunicorn worker forks and SocketIO/audio initialization

---

## [1.1.0] - Snapcast & HAOS Support

### Added
- Snapcast player provider for synchronized multiroom audio
- Home Assistant OS add-on support with PulseAudio integration
- Environment detection module for automatic Docker/HAOS adaptation
- Host ID auto-generation for Snapcast players
- Latency compensation configuration for Snapcast

### Changed
- Audio backend automatically switches between ALSA (Docker) and PulseAudio (HAOS)
- Updated documentation for all three player backends
- Improved provider architecture with consistent interfaces

### Notes
- Squeezelite and Snapcast use ALSA devices (`hw:X,Y`) in standalone Docker
- All providers use PulseAudio when running as HAOS add-on
- Sendspin continues to use PortAudio device indices

---

## [1.0.0] - Initial Release

### Added
- Multi-room audio player management via web interface
- Squeezelite player support (LMS/SlimProto protocol)
- Sendspin player support (Music Assistant native)
- Snapcast player support (synchronized multiroom)
- PulseAudio integration for HAOS audio system
- Ingress support for seamless HA sidebar integration
- Real-time player status via WebSocket
- Individual volume control per player
- Audio device auto-detection
- Persistent configuration across restarts

### Notes
- This is an experimental add-on
- Requires `full_access` permission for audio device access
- Uses Home Assistant's PulseAudio system (hassio_audio)
