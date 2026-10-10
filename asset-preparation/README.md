# Owned-file asset preparation prototype

This component generates the launcher's modified textures, selected authored model LODs, and indexes from an owned, supported original installation. It contains code, sizes, offsets, dimensions, and hashes. It does not contain the game assets or their replacement payload bytes. It is separate from the game runtime and does not patch or launch an executable.

## Verified on PC, 9 October 2026

- All 12 archive layers regenerated; every decoded byte matched the preserved working reference using an independent Python archive reader.
- Both indexes regenerated and matched their respective decoded references. They are intentionally separate: the runtime-root index and asset-directory index are not identical.
- Three archives have different LZ4 compressed bytes, and the indexes have different compressed bytes. Their decoded data is identical. Integration must use the generated file's actual size/hash after decoded verification.
- Complete fresh batch: 75.60 seconds, 290,684,928 bytes peak working set (277.2 MiB), 320,606,208 bytes peak paged memory (305.8 MiB). These are PC measurements, not Xbox estimates.
- Resume with complete outputs: 2.80 seconds, all outputs verified without regeneration.
- 29 synthetic archive/batch checks passed, including malformed data, output protection, cancellation/resume, original preservation, and corrupt-output rejection.
- A separate local UWP/.NET Native integration candidate compiled. Its package contained no `.dsar` archives or game indexes. It has not been installed or tested on Xbox. It also retains private runtime files excluded from this repository, so it is not a release artifact.

The current recipes require original index SHA-256 `58e571cf1e54e08b18db4e8effb3f964eded1ddaa3e4feebd72f88fbbc066da1`. Unsupported versions and mismatched input assets stop preparation. The generated asset index preserves the known-working police-model restorations.

## Xbox sample validation

A separate UWP/.NET Native test app converted 107 private owned-file samples into five archives on Xbox Series X: 100 mip-reduced textures, the largest selected model, two zone textures (including an array), three distant textures (including volume slices), and one restored texture. Every output passed decoded hash verification on Xbox and a second verification after download using the independent Python archive reader. The sample generation and verification took 0.98 seconds; this is not a full-preparation time estimate. The one-second memory sampler did not capture that short run, so no Xbox peak-memory claim is made.

The console currently has no complete original game archives, so the full preparation, D: interruption/resume test, and resulting gameplay launch remain untested. The existing game and launcher packages were not replaced. Probe data and generated samples stay private and are excluded from this repository.

The UWP native helper uses AppContainer CRT dependencies and no OpenMP. Free-space checks query the destination directory with `GetDiskFreeSpaceExW`; probing the root of the UWP package volume with `DriveInfo` was denied on Xbox.

## Build and test on Windows

Requires Visual Studio 2022 Community C++/UWP tools and Windows SDK 10.0.26100.0. The build script downloads pinned open-source dependencies, verifies package hashes, compiles the UWP-compatible native texture helper, and runs synthetic tests. It does not access a console or game files.

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

Shared C# conversion code and synthetic tests: GPL-3.0-only, adapted from the project's GPL-3.0 asset-format research and Tkachov/ALERT format tools at commit `bc90ed9b53fbcaa3b7acf94b36f00e77eab0dacb`. See LICENSE.txt. Original model metadata, materials, skin data and retained vertex attributes are copied from the user's input; they are not embedded here.

Native texture adapter: MIT. DirectXTex: Microsoft MIT, pinned to `4feb3e11a020f35b796fc769a74216a555d4f5ef` (may2026). K4os.Compression.LZ4 1.3.8: MIT, with underlying LZ4 BSD notice retained. See licenses. Required .NET/Visual C++ runtime terms remain applicable.

The launcher's GPLv2/other-dependency compatibility and runtime redistribution reviews remain open. This standalone source prototype and local test build do not establish permission to distribute a combined launcher/runtime binary or game-derived output.

## Remaining validation

Run full packaged preparation on Xbox against a verified import; measure elapsed time and peak memory; test interruption/resume on its D: storage; then launch the prepared runtime and compare the known-working scene. The sample test confirms conversion compatibility, not completion of that end-to-end validation.
