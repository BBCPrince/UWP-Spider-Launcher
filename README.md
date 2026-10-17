![UWP Spider Launcher](docs/images/banner.svg)

# UWP Spider Launcher

**Import your own game. Prepare it on Xbox. Launch from one place.**

An unofficial, experimental UWP launcher for Xbox Developer Mode. Transfer a supported game folder from USB or over your local network, or sign in to Steam to download the supported version you own.

[Getting started](docs/GETTING-STARTED.md) · [Downloads](https://github.com/BBCPrince/UWP-Spider-Launcher/releases) · [Build from source](release/REBUILD-FROM-SOURCE.md) · [Third-party notices](THIRD-PARTY-NOTICES.txt)

> **Latest source: 0.6.0.52**
>
> This repository contains launcher and preparation source. Game files, native compatibility-runtime binaries/source, and complete playable installers are not included in the source tree. Existing release downloads are separate and may have a different version; this source update does not publish a new installer.

## Bring your own files

| Method | How it works |
| --- | --- |
| **USB** | Connect a drive containing your supported game folder. A ZIP is optional. |
| **Wi-Fi** | Open the Xbox receiver, pair from your PC browser, and send the folder. No ZIP required. |
| **Steam** | Choose a voice language, sign in by QR or account name/password, and download verified files for the supported build. |

**Import or download → Verify → Prepare game → Play**

## Features

- Exact game-version and file-hash verification.
- On-console generation of required asset variants from your own files.
- Local reconstruction of 8,435 known pipeline recipes; shader bytecode and generated recipes are not bundled.
- Resumable transfers and preparation with progress reporting.
- Supported voice packs retained from USB and Wi-Fi imports.
- Settings language selection for 12 supported voice languages, with Steam downloads for missing packs.
- Steam QR or account/password sign-in with Steam Guard; no saved passwords or login tokens.
- Voice-only downloads for a prepared game, without downloading the whole game again.
- Storage management and a confirmation screen before removing prepared game files.
- Compact controller-friendly layout with display-safe margins and scaled QR cards.
- Support for a separate runtime or an optional combined package built using matching external runtime inputs.

## Supported versions

| Game version | Steam build | Support |
| --- | --- | --- |
| **3.0618.0.0** | **14752622** | Original supported profile; pinned Steam download |
| **4.0630.0.0** | **23986256** | Experimental verified imports; Continue/Cancel warning |

Other versions remain unsupported. Owning a newer game does not make every executable compatible.

## Requirements

- Xbox in **Developer Mode**. Testing is on **Series X**; Series S support is unverified.
- Your own licensed copy of a supported game version.
- Matching native runtime and preparation support. These are external inputs, not supplied by this repository.
- Enough free space for your game and generated files.

The default source build opens a separately installed runtime. A combined build carries the native host in the same installed package and does not need a second game-app registration, but requires the matching external inputs. See the [build guide](release/REBUILD-FROM-SOURCE.md).

## Experimental status

Xbox UWP games have a **5 GiB app-memory limit**. The matching runtime settings use reduced texture resolution and asset detail to fit within that budget. Performance, visual quality, and stability can vary; a full play-through and sustained 60 FPS are not guaranteed.

The latest source adds Steam account/password sign-in, language selection and voice-pack downloads. It retains newer-version import support and preparation-path fixes. The new sign-in and language-download flows passed local checks; live Xbox testing is still pending. See [version notes](release/STATUS.md).

No game assets, executable, generated shader programs, compiled caches, saves, credentials, signing keys, or private runtime DLLs are included here.

## Build and contribute

The launcher uses **C#/XAML, UWP .NET Native, and native preparation helpers**. Build with Visual Studio 2022 and the documented Windows SDK:

```powershell
.\build\build-launcher.ps1
```

[Build instructions](release/REBUILD-FROM-SOURCE.md) · [Source scope](SOURCE-SCOPE.md)

When reporting a problem, include the launcher version, game version, import method, and exact error message. Do not upload game files, saves, Steam sign-in QR codes, tokens, or signing keys.

## Xbox memory-limit feedback

[Support the request for higher Xbox UWP memory limits](https://feedbackportal.microsoft.com/feedback/idea/ca05d091-c7a6-f111-85cd-6045bd7e876e).

## License and attribution

Component license texts are in [licenses/](licenses/), with attribution in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt). External runtime inputs have separate distribution terms and are not licensed by this source repository.

UWP Spider Launcher is independent and unofficial. It is not affiliated with or endorsed by Microsoft, Valve, Sony, Marvel, or the game's developers or publishers. Product names identify compatibility.
