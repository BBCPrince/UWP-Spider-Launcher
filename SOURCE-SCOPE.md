> For the 0.6 direct-SteamKit candidate, see [migration status](release/STEAMKIT-MIGRATION.md). Details below describe the earlier 0.5 review.

# Launcher source scope

Included: the C#/XAML launcher, USB/ZIP/Wi-Fi import and validation logic, Steam QR download integration and its adapted DepotDownloader source, launcher preparation orchestration, storage cleanup logic, version fingerprints, license notices, and tests using synthetic files.

Excluded: game assets, modified archives and indexes, executable patch bytes, game-engine or decompiled source, compatibility DLL implementation/source, runtime app, user preferences/saves, deployment credentials, signing keys, recordings, compiled outputs, and the working game's build tree. No game artwork is included.

**This is a private validation source snapshot, not an approved public release.** The launcher can now be built using `tools/build-private.ps1`. Preparation reads pinned support files from the separately installed runtime; those files remain excluded. The default build guard requires an explicit private-validation property and does not claim licensing approval. The working Xbox build is not automatically replaced by a source build.

The owner chose preparation on the Xbox from each user's own files. The separate `asset-preparation` prototype contains conversion source, numeric recipes, hashes, and synthetic tests. It contains no converted textures, model payloads, or game indexes. Its GPL-3.0 license is separate from the launcher dependency review. The working build has not been replaced.

## Check the portable launcher components

On Windows with Visual Studio 2022 Community, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\run-tests.ps1
```

The script uses synthetic files and disposable test directories. It does not connect to Xbox, sign into Steam, or use game content. Test executables stay in ignored local output. Adjust the compiler path for other Visual Studio editions.

The package-content separation is implemented and audited. Before distributing a binary, finish the outstanding license/source-distribution reviews and fresh-install validation in `release/STATUS.md`. Passing the tests or packaging-only audit does not imply legal approval.
