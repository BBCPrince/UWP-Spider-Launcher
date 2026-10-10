# Spider-Man Launcher

Private release preparation for an Xbox Developer Mode launcher that imports a user's own supported Steam game files through USB, local Wi-Fi, or authenticated Steam download.

**No installable release is uploaded yet.** This repository currently contains release preparation notes only. The working local package includes modified game assets and compatibility binaries that must be separated or reviewed before a launcher-only distribution is ready.

The intended distribution includes the launcher, accurate dependency notices, and any required corresponding-source materials. Each user supplies their own legitimately owned game content. The game runtime is a separate UWP application.

## Current status

- Repository is private.
- Local package reviewed: `SpiderManInstaller_0.3.5.1_x64.msix`.
- USB folder import and local Wi-Fi folder transfer are implemented in the local build.
- Steam download uses Steam mobile QR approval.
- Release packaging and dependency compliance work remain unfinished.

See [the release audit](RELEASE-AUDIT.md) for the remaining work. Keep the repository private until the owner explicitly requests otherwise.
