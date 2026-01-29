# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-fad0505

**Current Dev Build Changes** (recent)

- Merge pull request #21 from scyto/feature/fix-haos-audio-crackling
- Add diagnostic logging for abnormal PA latency
- Align SyncToleranceMs with entry threshold (5→15ms)
- Fix sync correction overshoot by detecting sign flip
- Fix Stats for Nerds threshold display to show actual 15ms entry threshold
- Merge pull request #20 from scyto/feature/fix-haos-audio-crackling
- custom repository for haos
- Fix audio crackling on HAOS by reducing hot path overhead and adding correction hysteresis
- Merge pull request #125 from scyto/feature/handle-all-pipeline-states
- Merge pull request #124 from chrisuthe/feature/graceful-start-stop-and-disconnects

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
