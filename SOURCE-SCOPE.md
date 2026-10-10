# Source scope

This tree contains the C#/XAML launcher, USB/ZIP/Wi-Fi import and validation, direct SteamKit download integration, preparation orchestration, storage cleanup, converter source, numeric recipes, dependency pins, notices, and synthetic tests.

It excludes game assets, generated archives and game indexes, executable patch payloads, game-engine or decompiled source, compatibility-runtime code/binaries, user saves, credentials, signing keys, recordings, and compiled outputs. The artwork in `docs/images/` is generated project artwork, not game artwork.

Preparation reads version-pinned support from the separately installed runtime. That support is excluded from this repository. Building this launcher alone does not produce a complete playable game installation.

## Validation

On Windows with the documented build tools:

```powershell
.\tests\run-tests.ps1
python .\tests\test_release_audit.py
```

The portable tests use synthetic files and disposable output directories. They do not require Steam sign-in or connect to Xbox. The optional Steam network probe is a separate command documented in the build guide.

## Repository history

Historical test outputs, migration reports, and release inventories were backed up outside the source tree and removed from this revision. Older commits still contain them. Removing a file in a commit does not remove it from Git history.

A public repository would expose that history. History review or a separately prepared clean source snapshot is required before changing visibility. No history rewrite or public publication has been performed.
