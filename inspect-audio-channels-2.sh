#!/usr/bin/env bash
set -euo pipefail

echo "--- lsusb -v for device 0d8c:0102 ---"
lsusb -v -d 0d8c:0102 2>/dev/null || lsusb -v | sed -n '1,200p'

echo "\n--- aplay -L ---"
aplay -L || true

echo "\n--- amixer -c 0 contents ---"
amixer -c 0 contents || true

# Dump hw params by attempting a short 8-channel playback (timeout 2s)
echo "\n--- try playing 8-channel S16_LE to hw:0,0 (2s timeout) ---"
if command -v timeout >/dev/null 2>&1; then
  timeout 2s aplay -D hw:0,0 -f S16_LE -c 8 -r 48000 /dev/zero 2>&1 || true
else
  aplay -D hw:0,0 -f S16_LE -c 8 -r 48000 /dev/zero 2>&1 || true
fi

echo "\n--- cat possible hw info ---"
for f in /proc/asound/card0/*; do
  echo "=== $f ==="; sed -n '1,120p' "$f" || true; echo; done || true

echo "\n--- pipewire list nodes (pw-cli) ---"
if command -v pw-cli >/dev/null 2>&1; then
  pw-cli ls || true
else
  echo "pw-cli not installed"
fi
