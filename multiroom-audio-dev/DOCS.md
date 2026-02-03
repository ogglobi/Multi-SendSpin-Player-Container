# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-82556b4

**Current Dev Build Changes** (recent)

- Fix audio clock baseline capture timing
- Fix audio clock offset causing players to be ahead of other players
- Bump SendSpin.SDK to 6.3.5 to fix timer jump warnings
- Add SDK version and server time to Stats for Nerds
- Woops, SDK Fix.
- Use smoothed sync error in Stats for Nerds display
- Reduce UI polling frequency to minimize VM scheduling impact
- Add hero section and timing source to Stats for Nerds
- Add sync architecture documentation
- Bump Sendspin.SDK to 6.3.2 for timing source visibility

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
