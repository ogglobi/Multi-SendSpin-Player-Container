#!/usr/bin/env bash
set -euo pipefail
# Backup and remove drop-in that points at the other user
mkdir -p /root/multiroom-dropin-backups || true
for f in /etc/systemd/system/multiroom-audio.service.d/pulse-audio-user.conf; do
  if [ -f "$f" ]; then mv "$f" /root/multiroom-dropin-backups/ || true; fi
done

# Ensure pulse-start-wait.conf uses multiroom-audio's runtime dir (1000)
sed -i.bak "s#unix:/run/user/1001/pulse/native#unix:/run/user/1000/pulse/native#g" /etc/systemd/system/multiroom-audio.service.d/pulse-start-wait.conf || true

# Enable linger for multiroom-audio and start user pipewire services under that user
loginctl enable-linger multiroom-audio || true
runuser -l multiroom-audio -c "XDG_RUNTIME_DIR=/run/user/1000 systemctl --user daemon-reload || true"
runuser -l multiroom-audio -c "XDG_RUNTIME_DIR=/run/user/1000 systemctl --user enable --now pipewire pipewire-pulse || true"

# Reload and restart service
systemctl daemon-reload || true
systemctl restart multiroom-audio || true
sleep 3

# Report status
systemctl show multiroom-audio --property=Environment || true
PID=$(pgrep -f MultiRoomAudio || true)
if [ -n "$PID" ]; then
  echo "PID=$PID"
  tr '\0' '\n' < /proc/$PID/environ | grep -E 'PULSE|XDG|DOTNET' || true
fi

echo "--- pactl as multiroom-audio ---"
sudo -u multiroom-audio env XDG_RUNTIME_DIR=/run/user/1000 pactl info || true
sudo -u multiroom-audio env XDG_RUNTIME_DIR=/run/user/1000 pactl list short sinks || true

echo "--- pactl as audio ---"
sudo -u audio env XDG_RUNTIME_DIR=/run/user/1001 pactl info || true
sudo -u audio env XDG_RUNTIME_DIR=/run/user/1001 pactl list short sinks || true

journalctl -u multiroom-audio -n 200 --no-pager | grep -E "PaSinkEventService|pactl subscribe|DevicesEndpoint|Audio device enumeration|pactl" -n || true
