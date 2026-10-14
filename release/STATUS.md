# Version notes

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

Steam downloads remain pinned to the original supported build and English audio. USB/Wi-Fi imports retain supported voice packs already present in the imported folder.

This is launcher/preparation source. Native runtime inputs and a complete playable combined installer are not provided by the repository. Older release downloads remain available separately.
