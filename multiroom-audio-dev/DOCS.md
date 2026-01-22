# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-aa1f412

**Current Dev Build Changes** (recent)

- Update documentation for ENABLE_ADVANCED_FORMATS behavior
- Merge pull request #15 from scyto/feature/per-player-format-selection
- Apply flac-48000 default regardless of ENABLE_ADVANCED_FORMATS
- Default to flac-48000 format for maximum MA compatibility
- Merge pull request #14 from scyto/feature/per-player-format-selection
- Fix format dropdown not updating and change detection issues
- Merge pull request #13 from scyto/feature/per-player-format-selection
- Fix: Include advertisedFormat in player creation payload
- Add per-player audio format selection feature (dev-only)
- Document PA_SAMPLE_RATE and PA_SAMPLE_FORMAT environment variables

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
