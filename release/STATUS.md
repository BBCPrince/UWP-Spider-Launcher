# Project status

**0.6.0.12 — experimental launcher package in the private repository.**

This update includes Play-triggered runtime setup repair, matching-receipt validation, a manual repair button, and startup-setting restoration for the separately installed runtime. A full runtime uninstall still requires external registration through Device Portal or Xbox deployment tools. Private runtime binaries, pipeline recipes and compiled game caches are excluded.

The 0.6.0.12 Release x64 build is a packaging update of the console-tested 0.6.0.11 repair changes, with corrected status-text encoding. It is not a new end-to-end console validation. The repository visibility is unchanged.

## Implemented and checked

- USB folder/ZIP import and local Wi-Fi folder transfer.
- Independent SteamKit downloader with QR approval, pinned manifests, bounded download workers, chunk verification/resume, and final SHA-256 verification.
- On-console asset preparation from an owned import, exercised in earlier launcher builds.
- Separate runtime/preparation support; no game or compatibility-runtime payload in the launcher package.
- Visual Studio Release x64 / .NET Native build and package-content checks for 0.6.0.1.
- Local transfer, cancellation, corruption, storage, and path-validation tests.
- The owner reports that Steam works on the Xbox with 0.6.0.0. This is not a documented full interruption/resume and prepare/play cycle.
- 0.6.0.1 updates the visible name to UWP Spider Launcher. It was installed and opened on Xbox; the app header and package display name were verified, and the prepared-game receipt was unchanged.

## Before a public binary release

1. Complete the matching corresponding-source and rebuild distribution for the shipped binaries.
2. Review framework redistribution and any separately distributed runtime.
3. Complete a fresh download/import, interruption/resume, preparation, and play test.
4. Review repository history before changing visibility.
5. Obtain an explicit publication decision.

The machine-readable approval records, detailed test checklists, and release-audit scripts are maintained privately. None of the requirements above have been waived by the source cleanup. The repository remains private.

## Source distribution

The public-facing source tree contains application/converter code, recipes, build scripts, dependency metadata, and documentation. Optional test suites and internal release tools are not needed to build the launcher and are maintained separately. The project still requires the private-validation build property; the supplied build script sets it without claiming distribution approval.
