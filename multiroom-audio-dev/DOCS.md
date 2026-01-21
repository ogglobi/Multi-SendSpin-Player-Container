# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-d7deb7e

**Current Dev Build Changes** (recent)

- Update docs: clarify HID relay device mapping requirements
- Fix HID relay board hash stability across reboots
- Add helpful error messages for inaccessible HID relay boards
- Merge branch 'dev' into feature/hid-multi-board-support
- Merge pull request #87 from scyto/dev
- cool
- Fix URL-encoded boardId in trigger API endpoints
- Document /dev/serial/by-path for multiple Modbus boards
- Add USB port-based identification for Modbus/CH340 relay boards
- Add support for multiple identical HID relay boards

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
