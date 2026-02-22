#!/usr/bin/env bash
set -euo pipefail

# Create audio user if missing; if 'audio' group exists, use it as primary group
if ! id -u audio >/dev/null 2>&1; then
  if getent group audio >/dev/null 2>&1; then
    useradd -m -s /bin/bash -g audio audio || useradd -m -s /bin/bash audio || true
  else
    useradd -m -s /bin/bash audio || true
  fi
fi
AUID=$(id -u audio)

# Ensure runtime dir exists and enable linger
mkdir -p /run/user/${AUID}
chown audio:audio /run/user/${AUID}
loginctl enable-linger audio || true

# Install pipewire packages if not present
if ! command -v pipewire >/dev/null 2>&1; then
  apt-get update -y
  DEBIAN_FRONTEND=noninteractive apt-get install -y pipewire pipewire-pulse || true
fi

export XDG_RUNTIME_DIR=/run/user/${AUID}

# Reload user units and start pipewire services under audio user
runuser -l audio -c "XDG_RUNTIME_DIR=${XDG_RUNTIME_DIR} systemctl --user daemon-reload || true"
runuser -l audio -c "XDG_RUNTIME_DIR=${XDG_RUNTIME_DIR} systemctl --user enable --now pipewire pipewire-pulse || true"

# Add multiroom-audio user to audio group (if exists)
if id -u multiroom-audio >/dev/null 2>&1; then
  usermod -a -G audio multiroom-audio || true
fi

# Update multiroom-audio drop-in to point at audio user's socket
mkdir -p /etc/systemd/system/multiroom-audio.service.d
cat > /etc/systemd/system/multiroom-audio.service.d/pulse-audio-user.conf <<EOF
[Service]
Environment=XDG_RUNTIME_DIR=/run/user/${AUID}
Environment=PULSE_SERVER=unix:/run/user/${AUID}/pulse/native
EOF

systemctl daemon-reload
systemctl restart multiroom-audio || true
sleep 3

# Report status
echo "--- audio user id ---"
id audio || true
echo "--- pactl info (as audio user) ---"
sudo -u audio env XDG_RUNTIME_DIR=/run/user/${AUID} pactl info || true
echo "--- pactl list short sinks (as audio user) ---"
sudo -u audio env XDG_RUNTIME_DIR=/run/user/${AUID} pactl list short sinks || true
echo "--- /api/devices ---"
curl -sS http://localhost:8096/api/devices || true

echo "--- recent multiroom-audio journal ---"
journalctl -u multiroom-audio -n 120 --no-pager | tail -n 80
