> Current source candidate: 0.6.0.1. See [project status](STATUS.md) before distributing a build.

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

## Scope and remaining review

`SOURCE-INPUTS.json` records exact upstream archives and SHA-256 values. Use `Get-FileHash -Algorithm SHA256` to verify downloaded archives against that metadata. Optional collection and package-audit scripts are kept privately; neither is needed to build the launcher.

This collection is a review artifact. Before public binary distribution, resolve the combined-license questions, decide the applicable framework/System Library treatment, and approve the complete corresponding-source arrangement for the exact binaries shipped. No indefinite written source offer is made here. The separate runtime is not licensed or rebuilt by this launcher bundle.
