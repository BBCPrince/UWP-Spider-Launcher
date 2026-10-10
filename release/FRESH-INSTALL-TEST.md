# Fresh-install test for the separated launcher

Candidate: 0.5.0.0, private testing only. Do not begin by deleting saves or resetting Developer Mode. Keep a verified save backup and the working 0.4.0.1 installer.

## Required setup

The launcher does not install the native game runtime. The matching runtime must already be installed, with its private `PreparationSupport` folder beside `runtime-bootstrap.json`. A launcher-only ZIP is not a complete from-zero runtime installation. The public repository intentionally excludes that support payload and its patch data.

On October 10, 2026, launcher 0.5.0.0 was installed on the owner's Xbox, with all 59 separate preparation-support files verified by SHA-256. The existing prepared-game receipt was unchanged. A fresh import/prepare/play cycle with this version has not yet been confirmed.

## Record the result of each step

1. Open the launcher with no prepared game. Confirm it offers import and does not say Play is ready.
2. Import the supported owned game folder through USB. Confirm the language pack is detected and the full copy verifies. Wi-Fi folder import and Steam download require their own follow-up tests; one USB pass does not validate all three routes.
3. Choose Prepare. Record the final message. Preparation must finish without another game download and enable Play only after verification.
4. Launch the game, reach its menu, and load gameplay. Check characters, roads, controller input, and an ordinary cutscene.
5. Close and reopen the launcher. Confirm it still recognizes the prepared game and does not regenerate assets unnecessarily.
6. Only with a disposable import, test cancellation during preparation and resume. Do not force-delete files or interrupt the brief final commit.

If preparation fails, preserve the import and report the complete message. Do not recopy or reset the console until the failure is inspected. A test pass changes the functional-validation status only; it does not settle the recorded license or runtime-distribution questions.
