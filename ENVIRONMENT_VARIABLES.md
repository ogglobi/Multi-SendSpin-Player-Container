# Environment Variables

This document describes all environment variables supported by the Multi-Room Audio Controller application.

## Overview

The application validates environment variables on startup. Invalid values trigger warnings but do not crash the application - sensible defaults are used instead.

## Application Configuration

### WEB_PORT

Web server port number.

- **Type:** Integer
- **Default:** `8096`
- **Valid Range:** 1-65535

**Behavior differs between Docker and HAOS:**

| Environment | Notes |
|-------------|-------|
| **Standalone Docker** | Can be changed; update port mapping to match |
| **Home Assistant OS** | Must use port 8096 - ingress requires fixed port |

**Examples:**
```bash
# Default port
WEB_PORT=8096

# Custom port (Docker only - update port mapping)
WEB_PORT=8080
```

### LOG_LEVEL

Application logging verbosity.

- **Type:** String (enum)
- **Default:** `info`
- **Valid Values:** `debug`, `info`, `warning`, `error`
- **Description:** Controls how much detail is written to logs.

| Level | Use Case |
|-------|----------|
| `debug` | Troubleshooting issues, development |
| `info` | Normal operation |
| `warning` | Only show potential problems |
| `error` | Only show failures |

**Examples:**
```bash
# Verbose logging for troubleshooting
LOG_LEVEL=debug

# Normal operation (default)
LOG_LEVEL=info

# Quiet mode
LOG_LEVEL=error
```

### AUDIO_BACKEND

Audio backend selection.

- **Type:** String (enum)
- **Default:** Auto-detected
- **Valid Values:** `alsa`, `pulse`
- **Description:** Override the auto-detected audio backend. Normally detected based on environment (ALSA for standalone Docker, PulseAudio for Home Assistant OS).

**Examples:**
```bash
# ALSA (default for standalone Docker)
AUDIO_BACKEND=alsa

# PulseAudio (default for Home Assistant OS)
AUDIO_BACKEND=pulse
```

### CONFIG_PATH

Configuration directory path.

- **Type:** String (directory path)
- **Default:** `/app/config` (Docker) or `/data` (HAOS)
- **Description:** Directory where player configurations are stored.

**Examples:**
```bash
# Custom config directory
CONFIG_PATH=/custom/config
```

### LOG_PATH

Log directory path.

- **Type:** String (directory path)
- **Default:** `/app/logs` (Docker) or `/share/multiroom-audio/logs` (HAOS)
- **Description:** Directory where application logs are written.

**Examples:**
```bash
# Custom log directory
LOG_PATH=/custom/logs
```

## HAOS-Specific Variables

### SUPERVISOR_TOKEN

Home Assistant supervisor authentication token.

- **Type:** String
- **Default:** Not set
- **Description:** Automatically set by Home Assistant OS when running as an add-on. Presence of this variable indicates HAOS mode.

**Note:** Do not set this manually. It is injected by the Home Assistant supervisor.

## Docker Compose Example

Here is a complete example showing all environment variables:

```yaml
version: '3.8'

services:
  multiroom-audio:
    image: ghcr.io/chrisuthe/multiroom-audio:latest
    environment:
      # Application Configuration
      WEB_PORT: "8096"
      LOG_LEVEL: "info"
      AUDIO_BACKEND: "alsa"
      CONFIG_PATH: "/app/config"
      LOG_PATH: "/app/logs"
    volumes:
      - ./config:/app/config
      - ./logs:/app/logs
    devices:
      - /dev/snd:/dev/snd
    ports:
      - "8096:8096"
```

## Environment Detection

The application automatically detects its runtime environment:

| Check | Result |
|-------|--------|
| `/data/options.json` exists | HAOS mode |
| `SUPERVISOR_TOKEN` set | HAOS mode |
| Neither | Docker mode |

### HAOS Mode Defaults

When running as a Home Assistant add-on:

| Variable | HAOS Default |
|----------|--------------|
| `CONFIG_PATH` | `/data` |
| `LOG_PATH` | `/share/multiroom-audio/logs` |
| `AUDIO_BACKEND` | `pulse` |

### Docker Mode Defaults

When running as standalone Docker:

| Variable | Docker Default |
|----------|----------------|
| `CONFIG_PATH` | `/app/config` |
| `LOG_PATH` | `/app/logs` |
| `AUDIO_BACKEND` | `alsa` |

## Common Issues

### Issue: "No audio devices found"

**Possible causes:**
- AUDIO_BACKEND set incorrectly
- Audio devices not passed to container

**Solution:**
- Let the application auto-detect the audio backend
- Ensure `--device /dev/snd:/dev/snd` is passed to Docker

### Issue: "Port already in use"

**Possible causes:**
- Another service using port 8096
- Previous container not stopped

**Solution:**
- Change WEB_PORT (Docker only)
- Stop conflicting service
- For HAOS: Port 8096 is required and cannot be changed
