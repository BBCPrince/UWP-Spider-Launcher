# Getting started

UWP Spider Launcher imports and prepares your own supported game files. It does not supply a game license or install the separate game runtime.

## Availability

The current launcher source is 0.6.0.52. This source update does not publish a new installer or runtime bundle. Existing release downloads may differ from the current source. The instructions below describe the workflow once matching launcher and runtime components are installed.

## Choose an import method

### USB

1. Copy your supported original game folder to a USB drive on your PC.
2. Connect the drive to Xbox.
3. Open **Install → USB drive → Find files**.
4. Select the game folder and choose **Import selected files**.

A ZIP is optional. Folder import avoids keeping an extra ZIP alongside the extracted files. The exact original 3.0618 and newer 4.0630 installations are supported. The newer one requires accepting an experimental-version warning; other versions or mismatched files stop validation.

### Wi-Fi

1. Put the PC and Xbox on the same trusted local network.
2. Open **Install → Wi-Fi → Start receiver**.
3. Scan the QR code or open the displayed address in a current desktop Edge or Chrome browser.
4. Enter the pairing code if it was not supplied by the QR link.
5. Select the game folder and start sending.

Keep the launcher open until the transfer finishes. The local receiver is not intended to be exposed to the internet.

### Steam

1. Open **Install → Steam** and choose a **Voice language**.
2. Choose **Sign in with QR code** or **Use account name and password**. Enter credentials only on your own Xbox and complete Steam Guard if prompted.
3. The launcher checks Steam access and downloads the supported core game files plus the chosen voice pack.
4. Choose **Prepare game** when verification finishes. The selected language is applied after preparation.
5. If interrupted, select the same language and sign in again to verify existing chunks and resume.

The downloader uses SteamKit directly and requires ownership and depot access. Passwords, Steam Guard codes, and login tokens are not saved by the downloader. This is not a claim of an independent security audit. Do not share credentials, codes or sign-in screenshots.

Core files and English audio use the pinned original-build manifests. Other voice-pack manifests are obtained from Steam; accepted archives must still match the packaged size and complete SHA-256. Incompatible or damaged audio is rejected, rather than replacing installed files.

### Change or add a language

Open **Settings → Game language**, choose an installed voice pack, and select **Apply language**. Restart the game afterward. **Default / restore previous language** restores the configuration that existed before the launcher changed it.

A missing voice pack is labeled **download from Steam**. Choose it and select **Download from Steam**, then sign in by QR or account/password. For a prepared game, only the selected voice pack is downloaded and verified; existing game files and saves are retained. It is selected when installation finishes. Restart the game to use it.

Supported audio: English, French, German, Italian, Japanese, Polish, Portuguese (Portugal), Portuguese (Brazil), Russian, Spanish (Spain), Spanish (Latin America), and Arabic. USB and Wi-Fi imports retain all supported voice packs already included in the source folder. In-game text-language settings can override the runtime default.

The new username/password and Steam language-download flows have passed local checks. Authenticated language downloads and Xbox UI/audio playback still need live testing.

## Prepare and play

Choose the imported copy, then **Prepare game**. The launcher generates asset variants and reconstructs 8,435 known shader pipeline recipes from your files. It enables **Play** only after preparation verifies successfully. The game runtime compiles these recipes on the device; generating them does not itself compile shaders or guarantee coverage of every scene.

For the default build, the compatible runtime and its preparation support must already be installed. A combined build carries its native host in the same package, with no second game-app registration, but requires externally supplied runtime inputs. Neither those inputs nor a complete playable installer are included in this source repository. If support is missing, do not substitute unrelated DLLs or files from another game version.

Do not run imports or preparation in two launcher installations at the same time: the Xbox test versions share game storage.

## Storage and troubleshooting

### Shader preparation checks

Before Play, the launcher checks the known preparation list. It can verify an existing matching list or generate a missing list from the original verified import. Keep that import until preparation completes. If the import is missing and the list cannot be verified, import the supported original files again. Cancellation or a validation failure does not mark incomplete generation as ready.

The reference map contains hashes, source ranges and graphics-state metadata, not shader bytecode. Generated `.recipe` files and compiled caches stay on your device.

### App registration and reinstalling

In a combined package, launcher and game entries are registered together when the package is installed. Reinstalling the package restores both entries; game-data preparation is separate.

For the default separate-runtime build, press **Play**. The launcher detects missing or incomplete game-app setup. For an already registered compatible runtime, it restores missing startup settings and launches after setup confirms success. **Settings / Install/repair game app** remains available for repair without launching.

**Separate-runtime full uninstall limitation:** deleting the game app also removes Xbox's Dev Home registration. The current launcher cannot recreate that registration on its own: ordinary UWP registration only restored the app-user entry, and the app could not connect to the console's own Device Portal. Register the separate runtime through Device Portal or the Windows Xbox deployment tools once, then press Play. Existing prepared game files do not need to be transferred again. This is not yet a fully automatic reinstall after deleting the runtime app.

This action keeps existing settings and saves. It does not restore deleted saves, learned pipeline history, or compiled pipeline caches. It cannot recover missing runtime files: those still require the compatible separate runtime setup, which is not distributed in this repository. Imported and prepared game files are separate from the app registration and do not need another transfer if they remain intact.

For a fresh pipeline-cache test, preserve the locally generated recipes, verify that the generated compiled cache is empty, and compare startup preparation with a second launch. A recipe being present does not prove its pipeline was prepared successfully, and a startup test does not establish coverage of every scene in the game.

Use **Storage → Remove game files** to review removal of the prepared game. This operation is designed to keep saves, the runtime app, and original import files. Always read the confirmation before proceeding.

For an issue report, include launcher version, import method, supported game build, and the exact error message. Do not upload game files, saves, account details, sign-in QR codes, tokens, or signing keys.

### Fresh preparation support-path errors

Version 0.6.0.48 corrects the newer-profile support-payload location. If 0.6.0.47 stops during Prepare with a missing installed-package path, update the launcher, select the already verified import, and choose Prepare again. Complete generated files are verified and reused; a verified transfer does not need to be repeated for this path fix.

### Xbox App / Game classification

The package declares `expandedResources` to request Game-mode resources for both entries. This capability is deprecated, and automatic allocation on a console configured to treat packages as App is not yet verified. If the game cannot launch with the App classification, set the package to **Game** in Dev Home. Device Portal also has **Settings → Preferences → Treat UWP apps as games by default**.
