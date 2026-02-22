#!/usr/bin/env bash
set -euo pipefail

mkdir -p /root/multiroom-dropin-backups || true
for f in /etc/systemd/system/multiroom-audio.service.d/pulse-socket.conf /etc/systemd/system/multiroom-audio.service.d/run-as-audio-user.conf; do
  if [ -f "$f" ]; then mv "$f" /root/multiroom-dropin-backups/ || true; fi
done

if [ -f /etc/systemd/system/multiroom-audio.service.d/pulse-start-wait.conf ]; then
  sed -i.bak "s#unix:/run/user/110/pulse/native#unix:/run/user/1001/pulse/native#g" /etc/systemd/system/multiroom-audio.service.d/pulse-start-wait.conf || true
fi

systemctl daemon-reload || true
systemctl restart multiroom-audio || true
sleep 3

systemctl show multiroom-audio --property=Environment || true
PID=$(pgrep -f MultiRoomAudio || true)
if [ -n "$PID" ]; then
  echo "PID=$PID"
  tr '\0' '\n' < /proc/$PID/environ | grep -E 'PULSE|XDG|DOTNET' || true
else
  echo no-process
fi

journalctl -u multiroom-audio -n 200 --no-pager | grep -E "PaSinkEventService|pactl subscribe|DevicesEndpoint|Dummy Output|auto_null|Audio device enumeration" -n || true
