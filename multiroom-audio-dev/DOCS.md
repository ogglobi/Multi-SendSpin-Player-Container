# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-8ab3627

**Current Dev Build Changes** (recent)

- Merge pull request #153 from scyto/dev
- Merge pull request #89 from scyto/bug/hid-signalr-broadcast
- Add SignalR broadcast for mute changes and log all HID events
- Merge pull request #88 from scyto/bug/hid-mute-state-sync
- Fix HID mute toggle using actual player state instead of cached state
- Merge pull request #87 from scyto/bug/hid-mute-race-condition
- Read HID events directly from /dev/input instead of PA events
- Merge pull request #86 from scyto/bug/hid-mute-race-condition
- Add grace period for mute changes to prevent race condition
- Merge pull request #85 from scyto/bug/hid-mute-uses-wrong-api

> WARNING: This is a development build. For stable releases, use the stable add-on.
<!-- VERSION_INFO_END -->

---

## Warning

Development builds:
- May contain bugs or incomplete features
- Could have breaking changes between builds
- Are not recommended for production use

## Installation

This add-on is automatically updated whenever code is pushed to the `dev` branch.
The version number (sha-XXXXXXX) indicates the commit it was built from.

## Reporting Issues

When reporting issues with dev builds, please include:
- The commit SHA (visible in the add-on info)
- Steps to reproduce the issue
- Expected vs actual behavior

## For Stable Release

Use the "Multi-Room Audio Controller" add-on (without "Dev") for stable releases.
