# Multi-Room Audio Docker Controller

**One server. Multiple audio outputs. Whole-home audio.**

---

## What Problem Does This Solve?

You want multi-room audio but:
- **Commercial solutions are expensive** - Sonos, HEOS cost $200-500 per room
- **You already have speakers, amps, or DACs** sitting unused
- **You want software integration** - Music Assistant, LMS, or Home Assistant
- **You need flexibility** - Different rooms, different requirements

## The Solution

Run a single Docker container on your NAS, Raspberry Pi, or any server. Connect USB DACs or use built-in audio outputs. Each becomes an independent audio zone controllable from Music Assistant, Logitech Media Server, or Snapcast.

```
Your Server (NAS, Pi, etc.)
         |
    [Container]
    /    |    \
 DAC1  DAC2  DAC3
   |     |     |
Kitchen Bedroom Patio
```

---

## Quick Links

| I want to... | Go here |
|--------------|---------|
| Get running in 5 minutes | [Getting Started](GETTING_STARTED) |
| Use with Home Assistant | [HAOS Add-on Guide](HAOS_ADDON_GUIDE) |
| Understand the code | [Code Structure](CODE_STRUCTURE) |
| Fix something broken | [Troubleshooting](#troubleshooting) |

---

## Supported Backends

| Backend | Best For | Server Required |
|---------|----------|-----------------|
| **Sendspin** | Music Assistant users | Music Assistant |
| **Squeezelite** | LMS users, mixed environments | LMS or Music Assistant |
| **Snapcast** | Bit-perfect synchronized audio | Snapcast Server |

---

## Quick Start

### Docker

```bash
docker run -d \
  --name multiroom-audio \
  -p 8096:8096 \
  --device /dev/snd:/dev/snd \
  chrisuthe/squeezelitemultiroom:latest
```

Then open `http://YOUR-SERVER-IP:8096`

### Home Assistant OS

1. Add repository: `https://github.com/chrisuthe/squeezelite-docker`
2. Install "Multi-Room Audio Controller" add-on
3. Start and open from sidebar

---

## Troubleshooting

### No audio devices found
- **Docker**: Add `--device /dev/snd:/dev/snd`
- **HAOS**: Restart add-on after connecting USB devices

### Player won't start
1. Try `null` device first (tests without audio hardware)
2. Check player logs in web interface
3. Verify audio server is running

### Player not appearing in Music Assistant
1. Wait 30-60 seconds for discovery
2. Restart Music Assistant
3. For Squeezelite: set server IP explicitly

---

## Links

- [GitHub Repository](https://github.com/chrisuthe/squeezelite-docker)
- [Report an Issue](https://github.com/chrisuthe/squeezelite-docker/issues)
- [Docker Hub](https://hub.docker.com/r/chrisuthe/squeezelitemultiroom)
