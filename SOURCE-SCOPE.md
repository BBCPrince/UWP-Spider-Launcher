# Launcher source scope

Included: the C#/XAML launcher, USB/ZIP/Wi-Fi import and validation logic, Steam QR download integration and its adapted DepotDownloader source, launcher preparation orchestration, storage cleanup logic, version fingerprints, license notices, and tests using synthetic files.

Excluded: game assets, modified archives and indexes, executable patch bytes, game-engine or decompiled source, compatibility DLL implementation/source, runtime app, user preferences/saves, deployment credentials, signing keys, recordings, compiled outputs, and the working game's build tree. No game artwork is included.

**This is a source review snapshot, not a complete installable release.** Preparation still refers to files deliberately excluded from redistribution. The project has an explicit build guard so it cannot accidentally produce an incomplete or misleading release package. The existing local and Xbox builds remain unchanged.

The owner chose preparation on the Xbox from each user's own files. The separate `asset-preparation` prototype contains conversion source, numeric recipes, hashes, and synthetic tests. It contains no converted textures, model payloads, or game indexes. Its GPL-3.0 license is separate from the launcher dependency review. The working build has not been replaced.

## Check the portable launcher components

On Windows with Visual Studio 2022 Community, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\run-tests.ps1
```

The script uses synthetic files and disposable test directories. It does not connect to Xbox, sign into Steam, or use game content. Test executables stay in ignored local output. Adjust the compiler path for other Visual Studio editions.

Before distributing a binary, finish preparation separation, license review, dependency source/relink provisions, notices, signing, and end-to-end validation. The test suite passing does not imply legal approval.
