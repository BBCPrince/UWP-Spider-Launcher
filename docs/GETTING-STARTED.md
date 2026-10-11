# Getting started

UWP Spider Launcher imports and prepares your own supported game files. It does not supply a game license or install the separate game runtime.

## Availability

The current source preview is 0.6.0.1. No public installer or runtime bundle is provided. The instructions below describe the current private test workflow once both applications and matching runtime support are installed.

## Choose an import method

### USB

1. Copy your supported original game folder to a USB drive on your PC.
2. Connect the drive to Xbox.
3. Open **Install → USB drive → Find files**.
4. Select the game folder and choose **Import selected files**.

A ZIP is optional. Folder import avoids keeping an extra ZIP alongside the extracted files. Unsupported versions or mismatched files stop validation.

### Wi-Fi

1. Put the PC and Xbox on the same trusted local network.
2. Open **Install → Wi-Fi → Start receiver**.
3. Scan the QR code or open the displayed address in a current desktop Edge or Chrome browser.
4. Enter the pairing code if it was not supplied by the QR link.
5. Select the game folder and start sending.

Keep the launcher open until the transfer finishes. The local receiver is not intended to be exposed to the internet.

### Steam

1. Open **Install → Steam → Sign in and download**.
2. Scan the QR code with the Steam mobile app and approve sign-in.
3. The launcher checks access to the pinned supported depots and downloads verified files.
4. If interrupted, sign in again to verify existing chunks and resume.

The downloader uses SteamKit directly. It requires account access to the game and supported depots. No Steam password is entered in the launcher; session credentials are not saved by the downloader. This is not a claim of an independent security audit.

The Steam route currently selects English audio. USB and Wi-Fi imports copy all included supported voice packs.

## Prepare and play

Choose the imported copy, then **Prepare game**. The launcher generates asset variants from your files and enables **Play** only after preparation verifies successfully.

The compatible runtime and its preparation support must already be installed. They are not included in this source repository. If support is missing, do not substitute unrelated DLLs or files from another game version.

Do not run imports or preparation in two launcher installations at the same time: the Xbox test versions share game storage.

## Storage and troubleshooting

### Reinstalling a deleted game app

Press **Play**. The launcher detects missing or incomplete game-app setup. For an already registered compatible runtime, it restores missing startup settings and launches after setup confirms success. **Settings / Install/repair game app** remains available for repair without launching.

**Full uninstall limitation:** deleting the game app also removes Xbox's Dev Home registration. The current launcher cannot recreate that registration on its own: ordinary UWP registration only restored the app-user entry, and the app could not connect to the console's own Device Portal. Register the separate runtime through Device Portal or the Windows Xbox deployment tools once, then press Play. Existing prepared game files do not need to be transferred again. This is not yet a fully automatic reinstall after deleting the runtime app.

This action keeps existing settings and saves. It does not restore deleted saves, learned pipeline history, or compiled pipeline caches. It cannot recover missing runtime files: those still require the compatible separate runtime setup, which is not distributed in this repository. Imported and prepared game files are separate from the app registration and do not need another transfer if they remain intact.

For a fresh pipeline-cache test, preserve the supplied recipes, verify that the generated compiled cache is empty, and compare startup preparation with a second launch. A recipe being present does not prove its pipeline was prepared successfully, and a startup test does not establish coverage of every scene in the game.

Use **Storage → Remove game files** to review removal of the prepared game. This operation is designed to keep saves, the runtime app, and original import files. Always read the confirmation before proceeding.

For an issue report, include launcher version, import method, supported game build, and the exact error message. Do not upload game files, saves, account details, sign-in QR codes, tokens, or signing keys.
