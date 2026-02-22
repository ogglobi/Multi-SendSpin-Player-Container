#!/usr/bin/env bash
set -euo pipefail
AUID=$(id -u audio 2>/dev/null || echo 1001)
echo "Run pactl subscribe as multiroom-audio (timeout 3s)"
sudo -u multiroom-audio env XDG_RUNTIME_DIR=/run/user/${AUID} timeout 3s pactl subscribe 2>&1 || true
echo "Exit code: $?"

echo "Run pactl info as multiroom-audio"
sudo -u multiroom-audio env XDG_RUNTIME_DIR=/run/user/${AUID} pactl info 2>&1 || true
