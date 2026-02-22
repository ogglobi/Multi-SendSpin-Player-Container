#!/usr/bin/env bash
set -euo pipefail
AUID=$(id -u audio 2>/dev/null || echo 1001)
echo "--- drop-ins ---"
ls -la /etc/systemd/system/multiroom-audio.service.d || true
for f in /etc/systemd/system/multiroom-audio.service.d/*; do
  echo "=== $f ==="
  sed -n '1,200p' "$f" || true
done

echo "--- systemctl cat ---"
systemctl cat multiroom-audio || true

echo "--- systemctl show Environment ---"
systemctl show multiroom-audio --property=Environment || true

echo "--- process env ---"
PID=$(pgrep -f MultiRoomAudio || true)
if [ -n "$PID" ]; then
  echo "PID=$PID"
  tr '\0' '\n' < /proc/$PID/environ | grep -E 'PULSE|XDG|DOTNET' || true
else
  echo "no process"
fi
