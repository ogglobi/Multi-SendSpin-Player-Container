# Multi-Room Audio Controller

**One server. Multiple audio outputs. Whole-home audio with Music Assistant.**

---

## What Problem Does This Solve?

You want multi-room audio but:

- **Commercial solutions are expensive** - Sonos, HEOS cost $200-500 per room
- **You already have speakers, amps, or DACs** sitting unused
- **You use Music Assistant** and want additional audio endpoints
- **You need flexibility** - Different rooms, different requirements

## The Solution

Run a single Docker container on your NAS, Raspberry Pi, or Home Assistant server. Connect USB DACs or use built-in audio outputs. Each becomes an independent audio zone controllable from Music Assistant.

```
Your Server (NAS, Pi, HA, etc.)
         |
    [Container]
    /    |    \
 DAC1  DAC2  DAC3
   |     |     |
Kitchen Bedroom Patio
```

Players appear automatically in Music Assistant via the Sendspin protocol.

---

## Quick Links

| I want to... | Go here |
|--------------|---------|
| Get running in 5 minutes | [Getting Started](GETTING_STARTED) |
| Use with Home Assistant | [HAOS Add-on Guide](HAOS_ADDON_GUIDE) |
| Understand the code | [Code Structure](CODE_STRUCTURE) |
| Fix something broken | [Troubleshooting](#troubleshooting) |

---

## Quick Start

### Docker

```bash
docker run -d \
  --name multiroom-audio \
  -p 8096:8096 \
  --device /dev/snd:/dev/snd \
  ghcr.io/chrisuthe/multiroom-audio:latest
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
3. Verify Music Assistant is running

### Player not appearing in Music Assistant
1. Wait 30-60 seconds for discovery
2. Restart Music Assistant
3. Check both containers/add-ons are on the same network

---

## Links

- [GitHub Repository](https://github.com/chrisuthe/squeezelite-docker)
- [Report an Issue](https://github.com/chrisuthe/squeezelite-docker/issues)
