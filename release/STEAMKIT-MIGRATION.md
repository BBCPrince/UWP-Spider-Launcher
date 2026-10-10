# Direct SteamKit downloader candidate — October 10, 2026

The 0.6.0.0 candidate replaces the adapted DepotDownloader implementation with
new launcher code that calls SteamKit2 directly. The installed Xbox launcher
and game were not changed during this work. The test identity is
`Local.SpiderManLauncherSteamKitTest`; the working launcher remains
`Local.SpiderManLauncher`.

## Implemented

- Steam WebSocket connection, mobile QR approval, session-only login tokens and
  normal account-license/depot-access checks. No password collection or stored
  refresh token. Steam authentication URLs and service exception messages are
  excluded from diagnostics.
- Supported build 14752622, with the same three pinned depot manifests as the
  working importer. The executable and asset index are downloaded and verified
  before the bulk assets. No automatic fallback to a different game build.
- Two, four or eight bounded workers, secure content servers, four attempts
  with server rotation/backoff, progress and recent download speed.
- Partial-file resume based on checking each chunk's bytes. Existing complete
  files must pass SHA-256 verification to be reused. Checked partials count as
  occupied storage for the free-space calculation, without being trusted as
  valid content. A final SHA-256 mismatch clears the bad partial for repair.
- Staged replacement preserves the existing file until its replacement is
  verified. Failure/cancellation waits for active workers before closing their
  file. Only a fully verified file set receives a preparation receipt.
- USB/Wi-Fi transfer, language handling and the existing preparation system
  remain in the launcher. Steam download remains the existing English depot
  selection; this change does not add Steam language selection.

New implementation: `source/SteamTransfer`, `source/SteamDownload.cs`,
`source/SteamChecks.cs`, and `source/SteamProbe.cs`. DepotDownloader source files
were removed from this candidate, its compile inputs, package notices and
current upstream source-input list. Earlier Git revisions retain their own
sources/notices; they were not relicensed. New transfer files have GPL-3.0-or-later
headers; existing converter and third-party licenses remain applicable.

## Validation actually completed

- Visual Studio 2022 Release x64 build through the UWP .NET Native compiler.
- Existing launcher/import tests, progress/diagnostic tests, plus 23 transfer
  checks: corrupt chunks, partial resume, wrong final hash, preservation of an
  existing file, bounded concurrency, failure/cancellation draining workers,
  unsafe paths, incomplete/overlapping manifests and partial storage accounting.
- Five package-audit tests. Candidate packaging audit finds no game/runtime
  payload, private signing key, unexpected files or missing required notices.
- Live PC harness: Steam WebSocket connection, an HTTPS QR challenge, cancellation
  while awaiting mobile approval. No account was signed in during this probe.
- Compiled native UWP installed under its separate test identity on this PC:
  UI startup, local verified transfer self-test, Steam connection and local QR
  encoding completed. Probe log: Started → Connected → QrReady → Completed.

The existing compiler warnings about SteamKit's unused macOS native imports
and UWP reference-version conflicts remain. The Windows probe completed despite
them; it does not establish Xbox compatibility for authenticated downloads.

## Still required before replacing the working launcher

The owner is unavailable for Xbox testing. No account-approved game download,
large-file interruption/resume on Xbox, or import/prepare/play cycle has been
tested with this new downloader. The test app shares the Xbox game-import root
with the working launcher; do not run two imports/preparations simultaneously.

Next live check: install the separate candidate, approve its Steam QR, verify
the pinned manifests and version files, interrupt/resume a partial download,
then finish verification and prepare/play. Retain the 0.5 package for rollback.
Do not delete an existing prepared game merely to run this test.

Removing DepotDownloader removes that implementation from the new binary;
it does not settle separate runtime redistribution permissions or all public
release requirements. `release/policy.json` continues to block public release.
The GitHub repository must stay private. No maintainers were contacted.

## Reproduce the checks

```powershell
.\tests\run-tests.ps1
python .\tests\test_release_audit.py
dotnet run --project .\tests\steam-probe\SteamProbe.csproj -c Release
.\tools\build-private.ps1
python .\tools\audit_release.py .\packages\SpiderManInstaller_0.6.0.0_x64_Test\SpiderManInstaller_0.6.0.0_x64.msix --packaging-only
```

The network harness requires .NET 9 and network access. It does not print or
persist the QR challenge. The native UWP supports `--steam-probe`; results go
to its own LocalState `steam-probe.txt`, with event names and numeric metadata
only. A test package is not a public-distribution approval.
