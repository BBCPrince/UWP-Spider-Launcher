> Current source: 0.6.0.52. See [version notes](STATUS.md) for compatibility and limitations.

# Source and rebuild materials

The source snapshot contains the launcher/converter code, recipes, dependency metadata, build scripts, and notices. It excludes optional developer tests and release helpers as well as game content, saves, credentials, signing keys, and compatibility-runtime source/binaries.

## Build prerequisites

- Windows with Visual Studio 2022 C++ and UWP build tools, v143, Windows SDK 10.0.26100.0.
- Git, Windows PowerShell, and NuGet/network access for framework/build dependencies.
- A .NET SDK able to build SteamKit's netstandard2.0 target only if rebuilding that dependency from source.
- Your own test signing certificate if installing a rebuilt MSIX. The owner's private signing key is never provided.

## Normal launcher build

From the launcher source root:

```powershell
.\build\build-launcher.ps1
```

The native helper build downloads/checks the pinned DirectXTex revision and the pinned converter NuGet inputs. The launcher restores the framework and managed packages recorded in `DEPENDENCIES.json`. PrivateValidation is not a public-distribution approval.

The script generates original app icons and produces an unsigned MSIX under `packages/`. Sign it with your own matching test certificate before installation. No tests folder, Python release tool, game files, Xbox connection, or Steam sign-in is required to compile it. For a different Visual Studio installation, pass `-VisualStudio` with its root directory.

## Rebuild with a changed SteamKit library

Download the SteamKit source archive specified in `STEAMKIT-SOURCE.json`, verify its SHA-256 value using `Get-FileHash`, and unpack it to a separate folder. Do not use a game DLL as a replacement. In the SteamKit source root:

```powershell
dotnet build SteamKit2/SteamKit2/SteamKit2.csproj -c Release -f netstandard2.0 -p:TargetFrameworks=netstandard2.0
```

Then from the launcher root:

```powershell
.\build\build-launcher.ps1 -SteamKitAssembly 'C:\YourSource\SteamKit\SteamKit2\SteamKit2\bin\Release\netstandard2.0\SteamKit2.dll'
```

This path was tested by adding an assembly-metadata attribute to SteamKit, rebuilding it, and successfully compiling the launcher through .NET Native. The native compiler input hash matched the changed DLL. A rebuilt library need not produce byte-identical compiler outputs; package signing and timestamps also affect MSIX hashes.

## Optional combined package

The source includes combined-package launch and preparation orchestration. It does not include the native host, adapters, runtime defaults, or executable-derivation support. A combined build requires the matching runtime inputs, supplied separately and reviewed for their own redistribution terms:

```powershell
.\build\build-launcher.ps1 -CombinedRuntimePayloadRoot 'C:\YourInputs\RuntimePayload' -CombinedPreparationSupportRoot 'C:\YourInputs\PreparationSupport'
```

The payload must contain the pinned `RuntimeInventory.json`, `StartupContent/` runtime components, `Defaults/`, and `InitialSettings/`. Preparation support must contain the matching `PreparationRecipe.json`, `ModulePatch.json`, and a shared `PreparationPayload/` directory. For the newer profile, `Version40630/` contains its metadata; shared support files stay at the base payload root, not a duplicate version-local payload directory. Do not place these directories in the source repository. The normal build excludes them, and a combined build with missing inputs fails explicitly. Game executables, derived game modules, generated shader recipes, and compiled caches are not valid bundled inputs.

The matching native host must select the correct per-version adapters. The newer 4.0630 profile also requires an explicit load of its verified owned crash-report client before loading the game module. The launcher reconstructs the supported crash-reporting import from the owned SDK and verifies its result; the SDK itself is not distributed here. This source does not reproduce or license the private native host. The public binary-release guard also applies to combined builds.

## Dependency inputs

`SOURCE-INPUTS.json` records pinned upstream inputs and SHA-256 values. Verify downloaded inputs with `Get-FileHash -Algorithm SHA256`. License texts and attribution are retained in `licenses/` and `THIRD-PARTY-NOTICES.txt`.

Optional tests and internal package-audit tools are not required by the source build and are not included. External native runtime inputs, credentials, signing keys, and private installers remain outside the repository.
