#!/usr/bin/env bash
set -euo pipefail

echo "--- pactl list cards (multiroom-audio) ---"
sudo -u multiroom-audio env XDG_RUNTIME_DIR=/run/user/1000 pactl list cards || true

echo "\n--- pactl list sinks (multiroom-audio) ---"
sudo -u multiroom-audio env XDG_RUNTIME_DIR=/run/user/1000 pactl list sinks || true

echo "\n--- CardProfileService recent logs (full) ---"
journalctl -u multiroom-audio -n 400 --no-pager | sed -n '/CardProfileService/,/CardProfileService/p' || true
