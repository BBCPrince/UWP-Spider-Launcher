# Outstanding license review

This repository is private and has no downloadable binary release. License texts and source are provided for review; this is not a legal clearance statement.

## Items that must be resolved before a binary release

- **DepotDownloader version compatibility.** The pinned upstream supplies a GNU GPL v2 license. The dependency graph includes Apache-2.0 protobuf-net. GPLv2-only and Apache-2.0 are not compatible according to the [GNU compatibility guidance](https://www.gnu.org/licenses/license-compatibility.en.html) and the [Apache FAQ](https://www.apache.org/foundation/license-faq). Confirm the exact upstream grant or a valid exception before deciding the combined distribution license; do not assume an “or later” grant or unilaterally relicense upstream code. This is a review finding, not a conclusion that the upstream project infringes.
- **Corresponding source.** Provide the applicable launcher/adaptation source and build materials with any GPL-covered binary distribution. This does not require publishing unrelated game-engine or compatibility-layer work merely because it shares the development folder.
- **LGPL native linking.** The working UWP build uses .NET Native. A future SteamKit-linked binary needs an appropriate source and rebuild/relink arrangement; a link to a NuGet package alone is not the completed plan.
- **Other dependencies.** Resolve applicable Microsoft/runtime redistributable terms and preserve MIT/Apache notices for the actual shipped components. DEPENDENCIES.json records the resolved versions, hashes, and available upstream license metadata.
- **Game/runtime content.** No game files or compatibility implementation are included here. Distribution of a future runtime, patches, transformed content, or branding requires its own review. Owning the game and keeping a repository private are not blanket permissions to distribute those materials.

Before a public release, have a qualified lawyer review the actual package, preparation method, and applicable game/platform terms. No technical audit can guarantee that a rights holder will not object.
