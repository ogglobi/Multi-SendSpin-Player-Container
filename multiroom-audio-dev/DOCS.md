# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-10c89f0

**Current Dev Build Changes** (recent)

- Merge pull request #134 from scyto/fix-header-button-white
- Fix header dropdown buttons turning white when active
- Merge pull request #133 from scyto/fix-card-index-mismatch-clean
- Fix card index mismatch causing wrong device names in UI
- Merge pull request #130 from scyto/ui-cleanup
- Add Sink:/Device: prefix to device dropdown
- Fix Codex review issues for device dropdown handling
- Unify device hiding and rename Sound Card to Audio Device
- Persist volume changes to survive container restarts
- Add anti-oscillation debounce to sync correction

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
