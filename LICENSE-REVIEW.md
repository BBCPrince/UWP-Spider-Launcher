> For the 0.6 direct-SteamKit candidate, see [migration status](release/STEAMKIT-MIGRATION.md). Details below describe the earlier 0.5 review.

# Outstanding license review

This repository is private and has no downloadable binary release. License texts and source are provided for review; this is not a legal clearance statement.

The October 10 [detailed evidence review](release/REDISTRIBUTION-REVIEW.md) records the DepotDownloader maintainer discussion, the differing SteamKit source/package notices, the exact runtime categories still unverified, and the new source/rebuild materials.

## Items that must be resolved before a binary release

- **Asset converter integration.** The separate asset-preparation prototype adapts GPL-3.0 format tools. Its source is provided separately for review. Resolve compatibility with the launcher's DepotDownloader grant before distributing a combined binary; the local integration test does not settle that question. Microsoft DirectXTex and K4os license notices are included with the component.
- **DepotDownloader version compatibility.** The pinned upstream supplies a GNU GPL v2 license. The dependency graph includes Apache-2.0 protobuf-net. GPLv2-only and Apache-2.0 are not compatible according to the [GNU compatibility guidance](https://www.gnu.org/licenses/license-compatibility.en.html) and the [Apache FAQ](https://www.apache.org/foundation/license-faq). Confirm the exact upstream grant or a valid exception before deciding the combined distribution license; do not assume an “or later” grant or unilaterally relicense upstream code. This is a review finding, not a conclusion that the upstream project infringes.
- **Corresponding source.** Provide the applicable launcher/adaptation source and build materials with any GPL-covered binary distribution. This does not require publishing unrelated game-engine or compatibility-layer work merely because it shares the development folder.
- **LGPL native linking.** The working UWP build uses .NET Native. The private build script successfully rebuilt the launcher with a modified SteamKit library from pinned upstream source, and the native compiler input was verified by hash. A complete matching source and rebuild/relink distribution is still required; see release/STEAMKIT-SOURCE.json. A NuGet link alone is not the completed plan.
- **Other dependencies.** Resolve applicable Microsoft/runtime redistributable terms and preserve MIT/Apache notices for the actual shipped components. DEPENDENCIES.json records the resolved versions, hashes, and available upstream license metadata.
- **Game/runtime content.** No game files or compatibility implementation are included here. Distribution of a future runtime, patches, transformed content, or branding requires its own review. Owning the game and keeping a repository private are not blanket permissions to distribute those materials.

Before a public release, have a qualified lawyer review the actual package, preparation method, and applicable game/platform terms. No technical audit can guarantee that a rights holder will not object.
