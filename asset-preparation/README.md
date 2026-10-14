# Owned-file asset preparation prototype

This component generates the launcher's modified textures, selected authored model LODs, and indexes from an owned, supported original installation. It contains code, sizes, offsets, dimensions, and hashes. It does not contain the game assets or their replacement payload bytes. It is separate from the game runtime and does not patch or launch an executable.

The public-facing source excludes optional test suites and historical test reports. These are preserved in the private maintenance archive. The current recipes retain the known-working police-model restorations and validate their required input hashes.

## Build on Windows

Requires Visual Studio 2022 Community C++/UWP tools and Windows SDK 10.0.26100.0. The build script downloads pinned open-source dependencies, verifies package hashes, compiles the UWP-compatible native texture helper, and builds the desktop converter. It does not access a console or game files.

```powershell
.\build.ps1
```

With owned files, run the optional full preparation outside the original installation:

```powershell
.\bin\AssetGenerator.exe --batch "D:\OwnedGame\asset_archive" .\recipes "D:\GeneratedAssets" .\generated-files.tsv
```

The output is approximately 1.2 GB of transformed game content. Keep it private; it must not be committed or included in this source package. The original files are read only. A partially generated file can be retried; completed outputs are decoded and verified before reuse. Cancellation is checked between assets and verification blocks, so a single conversion is not instantly interruptible.

For UWP, compile the five shared C# files with `WINDOWS_UWP` defined, reference K4os.Compression.LZ4 1.3.8, include `bin/uwp/TextureConvert.dll` and `recipes`, and call `AssetBatch.Run` on a worker thread. The `bin/TextureConvert.dll` is the separate desktop helper for CLI testing. `Program.Main` is excluded by that symbol. Pass the preserved original index separately if imports have already moved into a runtime directory. Show progress and only commit readiness after all outputs and the rest of preparation are verified. The native project compiles only CPU conversion code with OpenMP disabled, avoiding a desktop OpenMP dependency in the UWP package.

## License and provenance

Shared C# conversion code: GPL-3.0-only, adapted from the project's GPL-3.0 asset-format research and Tkachov/ALERT format tools at commit `bc90ed9b53fbcaa3b7acf94b36f00e77eab0dacb`. See LICENSE.txt. Original model metadata, materials, skin data and retained vertex attributes are copied from the user's input; they are not embedded here.

Native texture adapter: MIT. DirectXTex: Microsoft MIT, pinned to `4feb3e11a020f35b796fc769a74216a555d4f5ef` (may2026). K4os.Compression.LZ4 1.3.8: MIT, with underlying LZ4 BSD notice retained. See licenses. Required .NET/Visual C++ runtime terms remain applicable.

The current launcher uses an independent SteamKit downloader rather than DepotDownloader. Matching-source, framework, and separate-runtime distribution reviews remain open; see [project status](../release/STATUS.md). This component's source and local test build do not establish permission to distribute a combined runtime binary or game-derived output.
