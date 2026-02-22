PipeWire / Pulse startup for multiroom-audio

Summary
- Ensure a persistent Pulse socket is available at boot for the `multiroom-audio` service so `pactl`/Pulse clients work.

Minimal steps (one-time, performed on the host/VM as root):

1. Create dedicated `audio` user (optional) or run `pipewire` under the `multiroom-audio` user.

   - Create a dedicated user:

     sudo useradd -m -s /bin/bash audio
     sudo loginctl enable-linger audio

   - Or enable linger & user services for the existing runtime user (recommended):

     sudo loginctl enable-linger multiroom-audio

2. Install PipeWire + Pulse compatibility (Debian/Ubuntu):

   sudo apt-get update
   sudo DEBIAN_FRONTEND=noninteractive apt-get install -y pipewire pipewire-pulse

3. Start/enable the user services (run as the target user or via `runuser`):

   # as target user (example: multiroom-audio)
   XDG_RUNTIME_DIR=/run/user/$(id -u multiroom-audio) systemctl --user enable --now pipewire pipewire-pulse

4. Point the systemd unit for `multiroom-audio` at the user's Pulse socket using a drop-in.

   Create `/etc/systemd/system/multiroom-audio.service.d/pulse-audio-user.conf` with:

   [Service]
   Environment=XDG_RUNTIME_DIR=/run/user/<UID>
   Environment=PULSE_SERVER=unix:/run/user/<UID>/pulse/native

   Replace `<UID>` with the numeric UID of the user running PipeWire (e.g. `id -u multiroom-audio`).

5. (Optional) Add hardware-access groups / udev rules so `multiroom-audio` can access HID/tty devices.

   - Example udev rule snippet (put in `/etc/udev/rules.d/99-multiroom-audio.rules`):

     KERNEL=="hidraw*", SUBSYSTEM=="hidraw", MODE="0660", GROUP="multiroom-audio"
     KERNEL=="ttyUSB*", MODE="0660", GROUP="multiroom-audio"

   - Reload udev rules: `udevadm control --reload-rules && udevadm trigger`

6. Reload systemd and restart `multiroom-audio`:

   sudo systemctl daemon-reload
   sudo systemctl restart multiroom-audio

Verification
- On the target user (the one that owns the Pulse socket):

  sudo -u multiroom-audio env XDG_RUNTIME_DIR=/run/user/$(id -u multiroom-audio) pactl info
  sudo -u multiroom-audio env XDG_RUNTIME_DIR=/run/user/$(id -u multiroom-audio) pactl list short sinks

- From the server (app): `curl -sS http://localhost:8096/api/devices` should list the real audio sink (not Dummy Output).

Notes
- We archived any temporary drop-ins to `/root/multiroom-dropin-backups.tar.gz` during troubleshooting; keep that archive if you need to restore previous configs.
- Prefer enabling PipeWire for the same runtime user that runs the service (`multiroom-audio`) to avoid socket ownership mismatches.
- Keep `LC_ALL=C`/`LANG=C` in the service environment so `pactl` output is parsable by the app.
