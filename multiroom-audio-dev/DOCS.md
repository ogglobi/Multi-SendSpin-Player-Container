# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-c64270c

**Current Dev Build Changes** (recent)

- Merge pull request #22 from scyto/feature/fix-haos-audio-crackling
- Merge upstream/dev: SDK 6.1.1, latency lock-in, HAOS env vars
- Add latency lock-in to reduce sync corrections from PulseAudio jitter
- Expose HAOS add-on options as environment variables
- Update SDK to 6.1.1 and fix scheduled start timing issue
- Merge pull request #21 from scyto/feature/fix-haos-audio-crackling
- Add diagnostic logging for abnormal PA latency
- Align SyncToleranceMs with entry threshold (5→15ms)
- Fix sync correction overshoot by detecting sign flip
- Fix Stats for Nerds threshold display to show actual 15ms entry threshold

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
