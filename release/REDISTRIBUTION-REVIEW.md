# Redistribution review — October 10, 2026

This is a technical provenance and license-evidence review, not a legal clearance. The installed private test remains unchanged. The launcher and separate runtime have different release boundaries.

## Findings that are now concrete

| Component | Evidence | Action before public distribution |
| --- | --- | --- |
| DepotDownloader 2.5.0 | Pinned repository has GPLv2 text. In upstream issue 42, maintainer azuisleet says the maintainers chose GPLv2; the comment does not specify later versions or a linking exception. | Obtain clarification covering the GPLv3 converter and Apache-2.0 protobuf dependency, or replace/restructure the affected implementation with a reviewed design. Do not simply relabel it GPLv3. |
| SteamKit 2.5.0 | NuGet metadata says LGPL-2.1-only, but `SteamKit2/SteamKit2/license.txt` at the same pinned commit expressly permits 2.1 or later. The source files refer to that license notice. | Preserve both pieces of evidence and the full notice. The build/relink procedure is verified. Supply the matching source and build materials; later-version wording does not remove those obligations. |
| Converter | Adapted GPL-3.0 source is in `asset-preparation`; upstream ALERT revision is recorded. | Keep the converter source and notices available. Resolve the combined-work license issue above. |
| QRCoder, K4os/LZ4, DirectXTex, protobuf-net | Exact source revisions/archives are pinned in `SOURCE-INPUTS.json`. | Preserve their notices and review the exact binary/source distribution. |
| Microsoft UWP/.NET Native/VCLibs | The package uses SDK/NuGet framework components, not arbitrary Windows DLLs. Microsoft terms permit specified object-code distribution subject to conditions. | Keep Microsoft notices, use the official unmodified framework packages, and apply the distribution conditions. This finding does not approve the separate runtime's WMI DLLs. |

Earlier documentation described SteamKit as only LGPL-2.1-only without mentioning the conflicting source notice. This review corrects that omission. It does not change upstream notices or assert that DepotDownloader's separate uncertainty is resolved.

## Separate runtime: exact categories to resolve

The private support payload was inventoried by file size, SHA-256, version metadata, and Authenticode signer. Those hashes and binaries stay local. A valid signature identifies a signer; it is not a redistribution grant.

- `SpArgs32`, `SpCrs`, `SpD12`, `SpDx`, `SpOle`, `SpRegCfg`, `SpShell`, `SpUser`, `SpiderManNativeStartup`, `UxTheme`, and `xinput9_1_0`: supplied/generated compatibility components. Trace their source and upstream license or obtain the supplier's grant. Availability of source alone is insufficient.
- `steam_api64`, `steam_api64_real`, and `crs-client`: establish exact origin and applicable distribution terms. These particular support copies do not have verified Authenticode signatures. Do not describe them as verified original signed DLLs.
- `wbemcomn` and `wbemprox`: signed Microsoft Windows components. No applicable redistribution grant was established. The Visual C++ redistributable list is not a grant for arbitrary Windows WMI components.
- Visual C++ runtime files: distinguish approved unmodified redist inputs from copies taken out of Windows or a game folder; record the exact redist version and terms before bundling.
- Saved preferences, runtime defaults and module-patch data: keep out of the public launcher. Review source, privacy, and whether they contain game-derived content before any separate runtime release.

The owner does not know who supplied the runtime code. A search of the saved export and supplied source folders found licenses for third-party tools and headers, but no established grant covering the whole supplied compatibility implementation. No message has been sent to external maintainers. There is no technical edit that can manufacture a missing rightsholder grant.

## What is safe to conclude

The new launcher package excludes game assets, game-engine source, private compatibility binaries and binary-patch payloads. That package-content check passed. Source collection and successful builds address reproducibility; they do not by themselves resolve all license compatibility or game/platform terms.

Do not remove the Steam downloader, converter, or game preparation silently merely to produce a package called "ready." If permission is unavailable, prepare a functional replacement or clearly agree a smaller release scope with the owner.

## References

- [DepotDownloader licensing discussion](https://github.com/SteamRE/DepotDownloader/issues/42), including comments obtained through the [GitHub API](https://api.github.com/repos/SteamRE/DepotDownloader/issues/42/comments).
- [Pinned SteamKit source notice](https://github.com/SteamRE/SteamKit/blob/0c20a7d2aff9842815bf6b5aec657553e4186dd5/SteamKit2/SteamKit2/license.txt) and its project package metadata.
- [Apache GPL compatibility explanation](https://www.apache.org/licenses/GPL-compatibility.html).
- [Microsoft VS 2022 redist list](https://learn.microsoft.com/en-us/visualstudio/releases/2022/redistribution) and the bundled Microsoft UWP terms.
