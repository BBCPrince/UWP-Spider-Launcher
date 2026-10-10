# Release status — October 9, 2026

**Private test candidate; public binary release is not ready.** The repository remains private. A successful game launch is evidence of functionality, not permission to distribute every dependency.

## Completed

- The 0.4.0.1 launcher generated the modified assets from an owned import on Xbox, completed preparation, and the user confirmed the game launches.
- The 0.5.0.0 candidate moves compatibility binaries, game-module patch data, and runtime-specific seed files out of the launcher into the separately installed runtime's `PreparationSupport` directory. Both JSON control files are pinned by SHA-256. Existing prepared-game receipts remain recognized.
- The launcher MSIX no longer contains `PreparationPayload`, `ModulePatch.json`, `PreparationRecipe.json`, game archives, or game indexes. Package inspection uses an allowlist and checks required notices; the old 0.4.0.1 package fails that audit as intended.
- License notices are included. Native helper and asset conversion source, pinned dependencies, synthetic tests, and private-build instructions are available in this repository.
- Generated geometric app icons have a source script; no game artwork or font file is bundled by that script.
- The package audit has regression tests for private binaries, omitted notices, unsafe names, duplicates, and payload-like recipe data. Passing packaging checks cannot override pending release reviews.

## Still blocked

1. **DepotDownloader licensing compatibility.** The pinned source supplies GPL version 2 text. No verified later-version grant or linking exception has been found. The combined launcher also includes the GPL-3.0 converter and Apache-2.0 dependencies. Do not change upstream license labels to make this disappear. The options are an applicable permission/clarification from the rightsholders or a reviewed architectural/implementation change. A draft request is saved here; it has not been sent.
2. **Corresponding source distribution.** The launcher successfully rebuilt through .NET Native using a modified SteamKit 2.5.0 library built from the pinned upstream source. The compiler's input DLL hash matched the rebuilt DLL (`f61775d67d7ceb47abd8541ac199f8aa84c8a3a7666b13889795bdc8f4bb8717`). A complete, matching source/rebuild distribution is still needed; a README link alone is not the completed source-distribution plan.
3. **Separate runtime rights.** Moving private files out of the launcher does not make a runtime bundle redistributable. Its Windows system DLLs, Steam-related components, supplied compatibility code, and their source/license requirements need independent review. No runtime binary/source is uploaded to this repository.
4. **Fresh installation.** The owner volunteered this test. The new external-support candidate must be tested with the matching separately installed runtime/support files, including missing-support errors and an actual import/prepare/play cycle. Do not count a 0.4.0.1 test as a 0.5.0.0 test.
5. **Framework terms.** Microsoft UWP terms and third-party notices have been included; the exact distribution's conditions still apply. This is not an assertion that an ordinary Windows system DLL may be copied into a public release.

## Local validation

```powershell
.\tools\build-private.ps1
python .\tests\test_release_audit.py
python .\tools\audit_release.py <candidate.msix> --packaging-only
```

Without `--packaging-only`, the audit must fail while `release/policy.json` has outstanding reviews. The switch is for private packaging validation and never means public release approval. Do not upload the private runtime-support directory or a test signing private key.

## Primary references checked

- [Pinned DepotDownloader license](https://github.com/SteamRE/DepotDownloader/blob/ec692a8d9eaa9da4d966287e14fbc76d8ebe1081/LICENSE).
- [GNU license compatibility guidance](https://www.gnu.org/licenses/license-compatibility.en.html) and [Apache's GPL compatibility explanation](https://www.apache.org/licenses/GPL-compatibility.html).
- [SteamKit's pinned license](https://github.com/SteamRE/SteamKit/blob/0c20a7d2aff9842815bf6b5aec657553e4186dd5/LICENSE).
- [Microsoft UWP package terms](https://github.com/microsoft/dotnet/blob/master/releases/UWP/LICENSE.TXT) and [Visual Studio's redistribution list](https://learn.microsoft.com/en-us/visualstudio/releases/2022/redistribution).
