# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-4c794c4

**Current Dev Build Changes** (recent)

- Merge pull request #159 from scyto/dev
- Remove 'v' prefix for dev builds in ProductName
- Truncate build SHA to short format in VersionService
- Merge remote-tracking branch 'origin/dev' into dev
- Merge pull request #105 from scyto/feature/custom-sink-migration
- Fix card profile and custom sink migration ordering bugs
- Merge pull request #104 from scyto/feature/custom-sink-migration
- Use devices.yaml historical sink names for migration fallback
- Merge pull request #103 from scyto/feature/custom-sink-migration
- Guard against null/empty profile in card matching

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
