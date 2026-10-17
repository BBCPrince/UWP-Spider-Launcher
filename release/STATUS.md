# Version notes

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
