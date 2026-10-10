# Project status

**0.6.0.1 — experimental source preview. No public installer.**

## Implemented and checked

- USB folder/ZIP import and local Wi-Fi folder transfer.
- Independent SteamKit downloader with QR approval, pinned manifests, bounded download workers, chunk verification/resume, and final SHA-256 verification.
- On-console asset preparation from an owned import, exercised in earlier launcher builds.
- Separate runtime/preparation support; no game or compatibility-runtime payload in the launcher package.
- Visual Studio Release x64 / .NET Native build and package-content checks for 0.6.0.1.
- Local transfer, cancellation, corruption, storage, and path-validation tests.
- The owner reports that Steam works on the Xbox with 0.6.0.0. This is not a documented full interruption/resume and prepare/play cycle.
- 0.6.0.1 updates the visible name to UWP Spider Launcher. It has been built, but not deployed as part of the branding/documentation update.

## Before a public binary release

1. Complete the matching corresponding-source and rebuild distribution for the shipped binaries.
2. Review framework redistribution and any separately distributed runtime.
3. Complete the [fresh-install test](FRESH-INSTALL-TEST.md), including download interruption/resume.
4. Review repository history before changing visibility.
5. Obtain an explicit publication decision.

The machine-readable requirements remain in `policy.json`. None have been waived by the documentation cleanup. The repository remains private.

## Package check

```powershell
python .\tools\audit_release.py <candidate.msix> --packaging-only
```

Packaging-only validation checks file contents and required notices. It does not approve public distribution. Without this flag the tool also enforces the outstanding release requirements.
