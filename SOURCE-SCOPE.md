# Source scope

Included:

- C#/XAML launcher, controller UI, import validation, USB/ZIP/Wi-Fi transfer, and direct SteamKit download integration.
- Preparation orchestration, storage management, converter source, and numeric reconstruction metadata.
- Build scripts, dependency pins, upstream license texts, attribution, and user documentation.
- Exact supported-version identities and shader-location maps. These contain hashes, ranges, and graphics-state metadata, not shader programs.

Excluded:

- Game assets, game executable, executable-derivation payloads, and game-engine/decompiled source.
- Native compatibility-runtime source/binaries and private Steam companion DLLs.
- Generated archives, indexes, shader recipes/programs, and compiled caches.
- Saves, credentials, signing keys, recordings, optional tests, and internal audit tools.

The default build needs a separately installed native runtime. A combined build requires matching external runtime/preparation inputs. Building this repository alone does not produce the private playable installer. Project banner and icons are original project artwork.

Component terms and attribution are retained in `licenses/` and `THIRD-PARTY-NOTICES.txt`. The main branch is a clean source snapshot; the retained older release has its own historical tag.
