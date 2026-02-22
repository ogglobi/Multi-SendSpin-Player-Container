#!/usr/bin/env bash
set -euo pipefail

echo "--- aplay -l ---"
aplay -l || true

echo "\n--- /proc/asound/cards ---"
cat /proc/asound/cards || true

echo "\n--- /proc/asound/pcm ---"
cat /proc/asound/pcm || true

echo "\n--- pactl list cards ---"
pactl list cards || true

echo "\n--- pactl list sinks ---"
pactl list sinks || true

echo "\n--- curl /api/cards ---"
curl -sS http://localhost:8096/api/cards || true

echo "\n--- curl /api/devices ---"
curl -sS http://localhost:8096/api/devices || true

echo "\n--- CardProfileService logs ---"
journalctl -u multiroom-audio -n 500 --no-pager | grep -E "CardProfileService|CardProfile" -n || true
