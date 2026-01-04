# NextGen: Pure C# Sendspin-Only Multi-Room Audio Controller

> **Status:** Implementation Complete - Testing Phase
> **Branch:** `nextgen`
> **Target:** v2.0.0
> **Last Updated:** 2026-01-03

---

## Current State

### Completed
- **Phases 1-7**: All implementation phases complete
- **Docker Image**: Builds successfully (294MB uncompressed, ~120MB compressed)
- **API**: All endpoints functional (`/api/players`, `/api/providers`, `/api/devices`)
- **Web UI**: Static HTML with SignalR at `http://localhost:8096`
- **GitHub Actions**: Updated for .NET build/lint

### Verified Working
```bash
# Build
docker build -f docker/Dockerfile -t multiroom-audio:test .

# Run
docker run -d --name multiroom-test -p 8096:8096 multiroom-audio:test

# Test
curl http://localhost:8096/api/players    # {"players":[],"count":0}
curl http://localhost:8096/api/providers  # Sendspin available
```

### Commits on `nextgen` Branch
```
c6f4ff7 refactor: complete Phase 7 cleanup - remove Python, update for C#
ebedec5 feat: Phase 5 - Web UI implementation
6e64339 feat: Phase 4 - Complete API endpoints
df15f99 feat: Phase 3 - PlayerManagerService integration with core services
581ca6b feat: Phase 2 - Core Services implementation
```

### Next Steps
1. Test on HAOS with real audio devices
2. Verify SignalR works through HAOS ingress
3. Test player creation/start/stop with Music Assistant
4. Update CHANGELOG.md and DOCS.md
5. Merge to main and tag v2.0.0

## Summary

Rewrite the multi-room audio controller from Python/Flask to **pure C# ASP.NET Core 8.0**, focusing exclusively on **Sendspin** support with **Alpine Linux** deployment for both HAOS Add-on and standalone Docker.

---

## Reference Documentation

- **Home Assistant Add-on Development:**
  - [Add-on Configuration](https://developers.home-assistant.io/docs/add-ons/configuration/) - config.yaml schema, image field
  - [Add-on Communication](https://developers.home-assistant.io/docs/add-ons/communication/) - Ingress, supervisor API
  - [Add-on Publishing](https://developers.home-assistant.io/docs/add-ons/publishing/) - Repository setup
  - [Add-on Presentation](https://developers.home-assistant.io/docs/add-ons/presentation/) - Icons, docs, changelog
- **Base Images:**
  - [home-assistant/docker-base](https://github.com/home-assistant/docker-base) - Official HA base images
  - [hassio-addons/addon-base](https://github.com/hassio-addons/addon-base) - Community add-on base
- **Reference Add-ons:**
  - [home-assistant/addons/vlc](https://github.com/home-assistant/addons/tree/master/vlc) - Official VLC add-on (audio player pattern)

---

## Goals

1. **Pure C# implementation** - Remove all Python code
2. **Sendspin-only** - Drop Squeezelite and Snapcast support
3. **Alpine Linux base** - Same image for HAOS Add-on and vanilla Docker
4. **Full web UI** - Port existing Flask/Jinja2 templates to ASP.NET Core with SignalR

---

## Architecture

```
┌─────────────────────────────────────────────────────┐
│             ASP.NET Core 8.0 Application            │
├─────────────────────────────────────────────────────┤
│  Controllers          │  Hubs                       │
│  - PlayersController  │  - PlayerStatusHub (SignalR)│
│  - DevicesController  │                             │
├─────────────────────────────────────────────────────┤
│  Services                                           │
│  - PlayerManagerService (SDK player lifecycle)      │
│  - ConfigurationService (YAML with YamlDotNet)      │
│  - EnvironmentService (Docker vs HAOS detection)    │
│  - PlayerStatusBackgroundService (2s status push)   │
├─────────────────────────────────────────────────────┤
│  Audio (from C#-SDK-Based branch)                   │
│  - PortAudioPlayer.cs (IAudioPlayer for Linux)      │
│  - PortAudioDeviceEnumerator.cs                     │
│  - BufferedAudioSampleSource.cs                     │
├─────────────────────────────────────────────────────┤
│  Static Web UI (wwwroot/)                           │
│  - index.html (ported from Jinja2)                  │
│  - app.js (SignalR client)                          │
│  - style.css                                        │
└─────────────────────────────────────────────────────┘
           │
           ▼
    ┌───────────────────────────────┐
    │  SendSpin.SDK v2.0.0          │  (NuGet package)
    │  - Protocol handling          │
    │  - Audio decoding/buffering   │
    │  - mDNS discovery             │
    └───────────────────────────────┘
           │
           ▼
    ┌───────────────────────────────┐
    │  PortAudioSharp2              │  (NuGet package)
    │  - Native audio output        │
    │  - Device enumeration         │
    └───────────────────────────────┘
           │
           ▼
    ┌──────────────┐
    │ Audio Device │  (ALSA/PulseAudio)
    └──────────────┘
```

---

## Key Decisions

### Sendspin Integration: Native SDK

**Using: SendSpin.SDK v2.0.0 + PortAudioSharp2**
- Full control over audio pipeline
- No external process spawning
- Native C# audio playback
- Pull implementation from existing `C#-SDK-Based` branch:
  - `sendspin-service/Audio/PortAudioPlayer.cs` - IAudioPlayer implementation
  - `sendspin-service/Audio/PortAudioDeviceEnumerator.cs` - Device detection
  - `sendspin-service/Services/PlayerManagerService.cs` - SDK lifecycle

### Web UI Technology: Static HTML with SignalR

- Port `index.html` to static file in `wwwroot/`
- Use SignalR JavaScript client for WebSocket updates
- Keep existing Bootstrap/FontAwesome styling
- No Razor/Blazor complexity needed

### Configuration: YamlDotNet

- Maintain `players.yaml` format for backward compatibility
- Use `YamlDotNet.Serialization` for read/write

### Base Image Strategy: Unified Alpine .NET Image

**Base image:** `mcr.microsoft.com/dotnet/aspnet:8.0-alpine`

**Why this works for both HAOS and Docker:**
1. HAOS uses `image:` field in config.yaml (pre-built pull, not local build)
2. We control the image content completely
3. No dependency on Home Assistant base images or bashio

**HAOS-specific handling (in C#, not bash):**
- Detect HAOS: Check for `/data/options.json` or `SUPERVISOR_TOKEN` env var
- Read config: Parse `/data/options.json` directly with `System.Text.Json`
- Paths: Use `/data` for config, `/share/multiroom-audio/logs` for logs
- Audio: Default to PulseAudio backend, use `PULSE_SERVER` from supervisor

**Docker-specific handling:**
- Config path: `/app/config` (or `CONFIG_PATH` env var)
- Log path: `/app/logs` (or `LOG_PATH` env var)
- Audio: ALSA by default, PulseAudio if `AUDIO_BACKEND=pulse`

---

## Project Structure

```
src/
  MultiRoomAudio/
    MultiRoomAudio.csproj
    Program.cs
    appsettings.json

    Audio/                          # Ported from C#-SDK-Based branch
      PortAudioPlayer.cs            # IAudioPlayer implementation
      PortAudioDeviceEnumerator.cs  # Device listing via PortAudio
      BufferedAudioSampleSource.cs  # Audio buffer adapter

    Services/
      PlayerManagerService.cs       # SDK player lifecycle (start/stop)
      ConfigurationService.cs       # YAML config persistence
      EnvironmentService.cs         # Docker vs HAOS detection
      PlayerStatusBackgroundService.cs

    Controllers/
      PlayersController.cs          # /api/players CRUD
      DevicesController.cs          # /api/devices
      ProvidersController.cs        # /api/providers
      DebugController.cs            # /api/debug/audio

    Hubs/
      PlayerStatusHub.cs            # SignalR for real-time status

    Models/
      PlayerConfiguration.cs
      AudioDevice.cs
      ApiResponse.cs
      PlayerStatus.cs

    Utilities/
      ClientIdGenerator.cs          # MD5-based ID generation
      AlsaCommandRunner.cs          # Volume control via amixer

    wwwroot/
      index.html
      css/style.css
      js/app.js

docker/
  Dockerfile                        # Alpine-based, unified for HAOS + Docker

multiroom-audio/                    # HAOS add-on metadata (no Dockerfile!)
  config.yaml                       # Points to ghcr.io image
  CHANGELOG.md
  DOCS.md
```

---

## NuGet Dependencies

```xml
<!-- Core SDK for Sendspin protocol -->
<PackageReference Include="SendSpin.SDK" Version="2.0.0" />
<!-- PortAudio for Linux audio output -->
<PackageReference Include="PortAudioSharp2" Version="1.0.2" />
<!-- Configuration & API -->
<PackageReference Include="YamlDotNet" Version="16.*" />
<PackageReference Include="Microsoft.AspNetCore.SignalR" Version="8.*" />
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="8.*" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.*" />
```

---

## API Endpoints (Sendspin-only)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/players` | List all players with statuses |
| POST | `/api/players` | Create new player |
| GET | `/api/players/{name}` | Get player config |
| PUT | `/api/players/{name}` | Update player |
| DELETE | `/api/players/{name}` | Delete player |
| POST | `/api/players/{name}/start` | Start player |
| POST | `/api/players/{name}/stop` | Stop player |
| GET | `/api/players/{name}/volume` | Get volume |
| POST | `/api/players/{name}/volume` | Set volume |
| PUT | `/api/players/{name}/offset` | Update delay_ms |
| GET | `/api/providers` | Returns `[{type: "sendspin", available: true}]` |
| GET | `/api/devices` | List ALSA devices |
| GET | `/api/devices/portaudio` | List PortAudio devices |
| POST | `/api/devices/test` | Play test tone |

---

## Unified Alpine Dockerfile

**One image for both HAOS and Docker:**

```dockerfile
# Base: Official Microsoft .NET 8 Alpine image
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 8096

# Audio dependencies for PortAudioSharp2 native bindings
RUN apk add --no-cache \
    alsa-utils alsa-lib alsa-plugins-pulse \
    pulseaudio-utils portaudio curl

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY src/ .
# Note: Trimming disabled because SendSpin.SDK uses reflection
RUN dotnet publish "MultiRoomAudio/MultiRoomAudio.csproj" \
    -c Release -o /app/publish \
    -p:PublishSingleFile=true \
    --self-contained true \
    -r linux-musl-x64

# Final image
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Create directories for both deployment modes
RUN mkdir -p /app/config /app/logs /data /share

# ALSA→PulseAudio bridge (auto-routes ALSA to PulseAudio when available)
RUN echo 'pcm.!default { type pulse fallback "sysdefault" }' > /etc/asound.conf && \
    echo 'ctl.!default { type pulse fallback "sysdefault" }' >> /etc/asound.conf

HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8096/api/players || exit 1

# Single binary handles both HAOS and Docker modes
ENTRYPOINT ["./MultiRoomAudio"]
```

**Key points:**
- No bashio, no Python, no pip
- C# binary auto-detects HAOS vs Docker
- Same image pushed to `ghcr.io/chrisuthe/multiroom-audio`
- HAOS config.yaml references same image via `image:` field
- ALSA config has fallback for both PulseAudio (HAOS) and raw ALSA (Docker)

---

## Implementation Phases

### Phase 1: Project Setup & SDK Integration ✅
- [x] Create .NET solution structure
- [x] Merge C#-SDK-Based branch to get existing SDK code
- [x] Port `Audio/` directory from `sendspin-service/`
- [x] Verify SendSpin.SDK and PortAudioSharp2 NuGet packages work

### Phase 2: Core Services ✅
- [x] Implement `EnvironmentService` (Docker vs HAOS detection)
- [x] Implement `ConfigurationService` (YamlDotNet, maintain `players.yaml` format)
- [x] Implement `AlsaCommandRunner` for volume control via amixer
- [x] Port client ID generation (MD5-based) to `ClientIdGenerator`

### Phase 3: Player Orchestration ✅
- [x] Implement `PlayerManagerService` using SendSpin.SDK
- [x] Manage SDK player instances (ConcurrentDictionary)
- [x] Implement start/stop/status for SDK-based players
- [x] Add autostart functionality on service startup

### Phase 4: API Layer ✅
- [x] Create ASP.NET Core Minimal API endpoints
- [x] Implement SignalR hub for status updates
- [x] Add `PlayerStatusBackgroundService` (2-second status polling)
- [x] Port Swagger/OpenAPI documentation

### Phase 5: Web UI ✅
- [x] Port `index.html` from Jinja2 to static HTML
- [x] Update JavaScript to use SignalR client instead of Socket.IO
- [x] Keep Bootstrap 5 / FontAwesome styling

### Phase 6: Docker & HAOS ✅
- [x] Create unified Alpine Dockerfile (`docker/Dockerfile`)
- [x] Update HAOS add-on metadata (`multiroom-audio/config.yaml`)
- [ ] Test with actual HAOS installation (pending)
- [ ] Validate ingress functionality (pending)

### Phase 7: Cleanup ✅
- [x] Remove all Python files (`app/`, `tests/`, `requirements.txt`)
- [x] Remove old Dockerfiles
- [x] Update `CLAUDE.md` for C# conventions
- [x] Update GitHub Actions for .NET build
- [x] Update docker-compose files

---

## Files to Create

```
src/MultiRoomAudio/MultiRoomAudio.csproj
src/MultiRoomAudio/Program.cs
src/MultiRoomAudio/appsettings.json
src/MultiRoomAudio/Audio/PortAudioPlayer.cs
src/MultiRoomAudio/Audio/PortAudioDeviceEnumerator.cs
src/MultiRoomAudio/Audio/BufferedAudioSampleSource.cs
src/MultiRoomAudio/Services/PlayerManagerService.cs
src/MultiRoomAudio/Services/ConfigurationService.cs
src/MultiRoomAudio/Services/EnvironmentService.cs
src/MultiRoomAudio/Services/PlayerStatusBackgroundService.cs
src/MultiRoomAudio/Controllers/PlayersController.cs
src/MultiRoomAudio/Controllers/DevicesController.cs
src/MultiRoomAudio/Controllers/ProvidersController.cs
src/MultiRoomAudio/Controllers/DebugController.cs
src/MultiRoomAudio/Hubs/PlayerStatusHub.cs
src/MultiRoomAudio/Models/PlayerConfiguration.cs
src/MultiRoomAudio/Models/AudioDevice.cs
src/MultiRoomAudio/Models/ApiResponse.cs
src/MultiRoomAudio/Models/PlayerStatus.cs
src/MultiRoomAudio/Utilities/ClientIdGenerator.cs
src/MultiRoomAudio/Utilities/AlsaCommandRunner.cs
src/MultiRoomAudio/wwwroot/index.html
src/MultiRoomAudio/wwwroot/css/style.css
src/MultiRoomAudio/wwwroot/js/app.js
docker/Dockerfile
tests/MultiRoomAudio.Tests/*.cs
```

## Files to Remove

```
app/ (entire Python directory)
tests/ (Python tests)
requirements.txt
pyproject.toml
supervisord.conf
supervisord-sdk.conf
entrypoint.sh
entrypoint-sdk.sh
Dockerfile (root - replace with docker/Dockerfile)
Dockerfile.slim
Dockerfile.sdk
multiroom-audio/Dockerfile (no longer needed - use image: field)
multiroom-audio/run.sh (no longer needed - C# handles everything)
sendspin-service/ (merge into src/MultiRoomAudio)
```

## Files to Update

```
CLAUDE.md → C# conventions (remove Python references)
README.md → Update for C# project
ENVIRONMENT_VARIABLES.md → Update for C# app
docs/ARCHITECTURE.md → New architecture diagram
multiroom-audio/config.yaml → Update description, remove Squeezelite/Snapcast
multiroom-audio/CHANGELOG.md → Add v2.0.0 entry
multiroom-audio/DOCS.md → Update for Sendspin-only
.github/workflows/*.yml → .NET build instead of Python
docker-compose.yml → Update for new image
```

---

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| .NET on Alpine (musl libc) | Use official Microsoft Alpine images; test ARM64 early |
| PortAudioSharp2 native libs on Alpine | Verify portaudio package compatibility; test audio output early |
| SendSpin.SDK API compatibility | SDK uses reflection, disable trimming; test with Music Assistant server |
| SignalR through HAOS ingress | Test early; configure CORS properly |
| YAML format differences | Write migration tests against existing configs |

---

## Success Criteria

1. All existing functionality works with C# implementation
2. Same `players.yaml` config format (backward compatible)
3. Web UI looks and behaves identically
4. Single Docker image works for both HAOS and standalone
5. Image size < 200MB (compressed)
6. Pure C# - no Python runtime required
7. Native SendSpin.SDK audio playback works on both ALSA and PulseAudio
