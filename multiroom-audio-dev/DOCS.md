# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-a49d96b

**Current Dev Build Changes** (recent)

- Merge branch 'dev' into feature/hid-multi-board-support
- Merge pull request #87 from scyto/dev
- cool
- Fix URL-encoded boardId in trigger API endpoints
- Document /dev/serial/by-path for multiple Modbus boards
- Add USB port-based identification for Modbus/CH340 relay boards
- Add support for multiple identical HID relay boards
- Fix relay_serial_port optional schema in dev config
- Fix code formatting (dotnet format)
- Fix: Read mock_hardware from HAOS options.json for DI registration

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
