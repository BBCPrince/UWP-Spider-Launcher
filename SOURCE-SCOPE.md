# Source scope

This tree contains the C#/XAML launcher, USB/ZIP/Wi-Fi import and validation, direct SteamKit download integration, preparation orchestration, storage cleanup, converter source, numeric recipes, dependency pins, notices, and build scripts.

It excludes game assets, generated archives and game indexes, executable patch payloads, game-engine or decompiled source, compatibility-runtime code/binaries, user saves, credentials, signing keys, recordings, and compiled outputs. The artwork in `docs/images/` is generated project artwork, not game artwork.

Preparation reads version-pinned support from the separately installed runtime. That support is excluded from this repository. Building this launcher alone does not produce a complete playable game installation.

## Build

On Windows with the documented build tools:

```powershell
.\build\build-launcher.ps1
```

Optional test suites, source-bundle collection scripts, package-audit tools, and internal release checklists are kept in a private maintenance archive. They are not required by this source build. Runtime input validation remains part of the launcher.

## Repository history

Historical test outputs, migration reports, release inventories, and optional development tools were backed up outside the source tree and removed from this revision. Older commits still contain them. Removing a file in a commit does not remove it from Git history.

A public repository would expose that history. History review or a separately prepared clean source snapshot is required before changing visibility. No history rewrite or public publication has been performed.
