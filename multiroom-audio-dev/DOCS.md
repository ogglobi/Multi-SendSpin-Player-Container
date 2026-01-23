# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-c78b43c

**Current Dev Build Changes** (recent)

- maybe fixed stats
- Decouple stats UI from SDK access with active viewer tracking
- Fix stats caching: use on-demand TTL instead of background timer
- Add background stats caching to prevent audio dropouts
- Fix Kestrel address override warning at startup
- Fix stats for nerds overlapping requests causing audio issues
- Pause auto-refresh during modal editing, slow stats polling
- Remove buffer size UI and fix stats/format bugs
- Merge pull request #101 from scyto/dev
- Merge pull request #99 from scyto/feature/per-player-format-selection

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
