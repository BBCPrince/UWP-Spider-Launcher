# Spider-Man Launcher

Private release preparation for an Xbox Developer Mode launcher that imports a user's own supported Steam game files through USB, local Wi-Fi, or authenticated Steam download.

**No installable release is uploaded yet.** This repository contains launcher source for review, synthetic tests, dependency/license information, and release preparation notes. It excludes game assets, game-engine code, compatibility runtime code/binaries, and game binary-patch data. The working local package is not included.

The intended distribution includes the launcher, accurate dependency notices, and any required corresponding-source materials. Each user supplies their own legitimately owned game content. The game runtime is a separate UWP application.

## Current status

- Repository is private.
- Initial package audit covered 0.3.5.1; the current private candidate is 0.5.0.0.
- USB folder import and local Wi-Fi folder transfer are implemented in the local build.
- Steam download uses Steam mobile QR approval.
- Release packaging and dependency compliance work remain unfinished.
- Private candidate 0.5.0.0 removes runtime support and patch data from the launcher package. It reads those files from the separately installed runtime. Build with `tools/build-private.ps1`; review [current release status](release/STATUS.md) before using or distributing it.
- Asset preparation now runs on Xbox. The [converter](asset-preparation/README.md) recreated all modified assets on PC, passed a 107-asset Xbox sample test, and completed full on-console preparation with local launcher 0.4.0.1. The owner confirmed that installation launches the game. A fresh import/prepare/play test of 0.5.0.0 remains pending. It is not an approved public release.

See [source scope and tests](SOURCE-SCOPE.md), [license review](LICENSE-REVIEW.md), and [the original package audit](RELEASE-AUDIT.md). The default build guard prevents treating a private validation build as an approved public release. Keep the repository private until the owner explicitly requests otherwise.
