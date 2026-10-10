# Distribution scope

The source tree contains launcher/converter code, recipes, notices, and build materials. The MSIX contains the compiled launcher/converter, recipes, artwork, framework-selected components, and notices.

Neither is a game or runtime bundle. A separately installed runtime supplies preparation support; that payload and any associated rights require their own review.

## Dependency evidence

- `SOURCE-INPUTS.json`: pinned upstream source archive URLs and hashes.
- `STEAMKIT-SOURCE.json`: SteamKit source identity and rebuild evidence.
- `../DEPENDENCIES.json`: resolved package metadata.
- `../THIRD-PARTY-NOTICES.txt` and `../licenses/`: attribution and license texts.

SteamKit's pinned source and package metadata use different license labels; both are retained in the notices. The .NET Native build can use a rebuilt SteamKit assembly through `-SteamKitAssembly`. That tested path is one part of the matching-source/rebuild arrangement, not the completed distribution review.

Framework terms, exact corresponding-source scope, and separate runtime provenance remain open. Internal review tools, checklists, and machine-readable approval records are maintained privately; current requirements are summarized in `STATUS.md`.
