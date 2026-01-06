# Getting Started with Multi-Room Audio

**Time to first audio**: 5 minutes | **Difficulty**: Beginner

---

## The Problem

You want whole-home audio but:

- **Commercial systems are expensive** - Sonos, HEOS, and similar cost $200-500 per room
- **You have unused audio gear** - DACs, amplifiers, and speakers sitting idle
- **You want software control** - Integration with Music Assistant
- **You need flexibility** - Different rooms, different requirements

## The Solution

Multi-Room Audio Controller turns any Docker host into a multi-zone audio server:

1. Connect USB DACs or use built-in audio outputs
2. Run one container that manages all players
3. Each player appears as a controllable zone in Music Assistant
4. Control everything from a web interface

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

## Choose Your Path

| Your Situation | Start Here |
|----------------|------------|
| I run Docker on a NAS/server/Pi | [Quick Start (Docker)](#quick-start-docker) |
| I run Home Assistant OS | [Quick Start (HAOS)](#quick-start-haos) |

---

## Quick Start (Docker)

### Prerequisites

- Docker installed and running
- Music Assistant running (for audio control)
- (Optional) USB DAC connected

### Step 1: Deploy the Container

```bash
docker run -d \
  --name multiroom-audio \
  -p 8096:8096 \
  --device /dev/snd:/dev/snd \
  ghcr.io/chrisuthe/multiroom-audio:latest
```

### Step 2: Open the Web Interface

Navigate to: `http://YOUR-SERVER-IP:8096`

You should see the Multi-Room Audio Controller dashboard.

### Step 3: Create Your First Player

1. Click **"Add Player"**
2. Fill in the form:
   - **Name**: `Test Player` (or your room name)
   - **Audio Device**: Select your device, or `null` for testing
3. Click **"Create Player"**
4. Click **"Start"** on your new player

### Step 4: Verify It Works

1. Open Music Assistant
2. Go to Settings > Players
3. Your player should appear within 30-60 seconds

### Success! What's Next?

- Add persistent storage so config survives restarts
- Create more players for each room
- Check Troubleshooting if something is not working

### Advanced: Custom ALSA Configuration

If you have a custom `/etc/asound.conf` on your host (for virtual devices, dmix, multi-channel routing, etc.), mount it into the container:

```bash
docker run -d \
  --name multiroom-audio \
  -p 8096:8096 \
  --device /dev/snd:/dev/snd \
  -v /etc/asound.conf:/etc/asound.conf:ro \
  ghcr.io/chrisuthe/multiroom-audio:latest
```

Or in Docker Compose:

```yaml
volumes:
  - ./config:/app/config
  - /etc/asound.conf:/etc/asound.conf:ro
```

After restarting the container, your custom ALSA devices will appear in the device selection dropdown.

---

## Quick Start (HAOS)

### Prerequisites

- Home Assistant OS or Supervised installation
- Music Assistant add-on (recommended)

### Step 1: Add the Repository

1. Go to **Settings** > **Add-ons** > **Add-on Store**
2. Click the three-dot menu (top right)
3. Select **Repositories**
4. Add: `https://github.com/chrisuthe/squeezelite-docker`
5. Click **Add**, then **Close**

### Step 2: Install the Add-on

1. Refresh the add-on store page
2. Find **"Multi-Room Audio Controller"**
3. Click **Install**
4. Wait for installation to complete

### Step 3: Start and Access

1. Go to the add-on's **Info** tab
2. Click **Start**
3. Enable **Show in sidebar** (optional but recommended)
4. Click **Open Web UI**

### Step 4: Create Your First Player

1. Click **"Add Player"**
2. Fill in the form:
   - **Name**: Your room name (e.g., "Kitchen")
   - **Audio Device**: Select from available devices
3. Click **"Create Player"**
4. Click **"Start"**

### Step 5: Verify in Music Assistant

1. Open Music Assistant
2. Go to Settings > Players
3. Your new player should appear

### Success! What's Next?

- Connect USB DACs for more zones
- Check Troubleshooting if needed

---

## Common First-Time Issues

### "No audio devices found"

**Docker**: Ensure you passed `--device /dev/snd:/dev/snd`

**HAOS**: USB devices appear after add-on restart. Try:
1. Restart the add-on
2. Check Settings > System > Hardware for your device

### "Player won't start"

1. Try creating a player with `null` device first (tests without audio)
2. Check the player logs in the web interface
3. Verify Music Assistant is running and reachable

### "Player not appearing in Music Assistant"

1. Wait 30-60 seconds for discovery
2. Restart Music Assistant
3. Check both are on the same network

---

## Next Steps

| Goal | Documentation |
|------|---------------|
| Full HAOS setup | [HAOS Add-on Guide](HAOS_ADDON_GUIDE.md) |
| Understand the architecture | [Architecture](ARCHITECTURE.md) |
| Something not working | Troubleshooting |
