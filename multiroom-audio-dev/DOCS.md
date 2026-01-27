# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-0a9be78

**Current Dev Build Changes** (recent)

- Add robust reconnection with mDNS watch and WaitingForServer state
- Merge pull request #122 from scyto/fix/cleanup-stale-sdk-comments
- Clean up stale SDK version comments and add GroupId to diagnostics
- Merge pull request #121 from scyto/dev
- removed stale comment
- Fix volume delta bug during seek/skip for grouped players
- Update SendSpin.SDK from 5.4.1 to 6.0.0
- Update to 5.4.1 attempt to fix volume issues so @Scyto doesn't send me angry emojis
- Merge pull request #118 from scyto/dev
- Fix startup volume being overwritten by current volume in edit modal

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
