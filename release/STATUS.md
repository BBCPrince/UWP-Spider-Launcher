# Version notes

## 0.6.0.56

- Combined-package Play generates a fast-travel tutorial correction from the user’s verified map UI.
- Fixes the UI rejection when the active reveal tutorial receives a travelable, unlocked destination while its map reports offline. The game-specific map flag is not an internet-connectivity diagnosis.
- Caches native destination eligibility when it arrives before the tutorial opens, then rechecks it when the hold step registers its completion callback.
- Keeps the original one-second hold, tutorial completion and native fast-travel callback; locked and nontravelable destinations remain blocked.
- Supports both retained imported archives and archives moved into the prepared runtime, using the preserved original index. The source archive is unchanged, the active TOC is backed up, and other asset routes are preserved.
- No original map UI, generated archive, shader program, save, runtime DLL or private installer is added to this source release.

Retained updates: both supported game profiles, USB and folder-based Wi-Fi imports, Steam QR/account sign-in, Steam Guard, voice-language selection and downloads, preparation/recovery, shader-recipe reconstruction, and compact display-safe UI. The source was compared with the current launcher/preparation candidate; required source inputs are included. Native performance, memory and rendering fixes belong to the separately supplied runtime, not this repository.

Validation: Release x64 .NET Native builds passed for the published launcher source and the private combined build, along with 27 preparation checks and 1,536 bytecode state cases. The test package was installed on Xbox with saves preserved. On October 19, 2026, the user confirmed that the fast-travel fix worked on Xbox Series X. This confirms the tested tutorial case, not full-game stability or every fast-travel destination. Native runtime and diagnostic changes remain external inputs.

## 0.6.0.52

- Steam username/password sign-in with Steam Guard, alongside QR sign-in.
- Settings language selection and reversible runtime configuration for 12 supported voice languages.
- Voice-language choice during an initial Steam download.
- Authenticated downloads of a missing voice pack without downloading the full game again.
- Steam language metadata lookup with pinned archive sizes and full SHA-256 verification.
- Package-wide `expandedResources` request for Game-mode resources; automatic allocation remains unverified.

Validation: Release x64 .NET Native builds passed. Local checks covered sign-in cancellation and log redaction, language configuration/readback, Steam depot selection and verified voice-pack installation. No live username/password login, authenticated language download, or Xbox language playback is claimed as tested.

## 0.6.0.48

- Exact import support for original 3.0618 and experimental 4.0630.
- Newer-version warning with Continue/Cancel; unknown executables remain rejected.
- Version-specific shader-location maps reconstructed from owned files.
- Xbox package/app-data mount handling for shader preparation.
- Shared support-payload location for fresh preparation.
- Support-file verification before copying and resumable preparation.
- Compact layout, display-safe margins, and scaled QR cards.

## Current limits

Testing is on Xbox Series X. The newer-version test reached gameplay with working controls; this does not establish full-game stability or sustained 60 FPS. The fresh-preparation correction passed local checks; completion of the resumed console preparation has not yet been confirmed.

Steam core downloads remain pinned to the original supported build. English audio uses its pinned manifest; other audio manifests come from Steam, with accepted archive sizes and SHA-256 hashes pinned locally. USB/Wi-Fi imports retain supported voice packs already present in the imported folder.

This is launcher/preparation source. Native runtime inputs and a complete playable combined installer are not provided by the repository. Existing release downloads remain available separately and are unchanged by this source update.
