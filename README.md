# Spider-Man Launcher

Private release preparation for an Xbox Developer Mode launcher that imports a user's own supported Steam game files through USB, local Wi-Fi, or authenticated Steam download.

**No installable release is uploaded yet.** This repository contains launcher source for review, synthetic tests, dependency/license information, and release preparation notes. It excludes game assets, game-engine code, compatibility runtime code/binaries, and game binary-patch data. The working local package is not included.

The intended distribution includes the launcher, accurate dependency notices, and any required corresponding-source materials. Each user supplies their own legitimately owned game content. The game runtime is a separate UWP application.

## Current status

- Repository is private.
- Local package reviewed: `SpiderManInstaller_0.3.5.1_x64.msix`.
- USB folder import and local Wi-Fi folder transfer are implemented in the local build.
- Steam download uses Steam mobile QR approval.
- Release packaging and dependency compliance work remain unfinished.
- Asset preparation is being moved onto the Xbox. The separate [converter prototype](asset-preparation/README.md) recreated all modified assets on PC and passed a 107-asset sample test on Xbox. Full preparation and gameplay validation on Xbox remain pending. It is not part of a distributable release.

See [source scope and tests](SOURCE-SCOPE.md), [license review](LICENSE-REVIEW.md), and [the original package audit](RELEASE-AUDIT.md). The source snapshot is not a complete release build; a build guard prevents accidentally packaging its unfinished preparation path. Keep the repository private until the owner explicitly requests otherwise.
