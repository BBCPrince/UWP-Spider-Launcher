# Source and rebuild materials

The private review bundle contains the reviewed launcher/converter tree, upstream source archives, hashes, dependency metadata and these build instructions. It excludes the game, saves, credentials, private package-signing keys and compatibility runtime source/binaries. Upstream source archives preserve their own test fixtures and public build files; they are not files taken from a game installation.

## Build prerequisites

- Windows with Visual Studio 2022 C++ and UWP build tools, v143, Windows SDK 10.0.26100.0.
- .NET SDK able to build SteamKit's netstandard2.0 target (the local test used an installed newer SDK).
- Python 3.12 or later, Git, PowerShell, and NuGet/network access for framework/build dependencies.
- Your own test signing certificate if installing a rebuilt MSIX. The owner's private signing key is never provided.

## Normal launcher build

From the launcher source root:

```powershell
.\tools\build-private.ps1
python .\tests\test_release_audit.py
.\tests\run-tests.ps1
python .\tools\audit_release.py .\packages\SpiderManInstaller_0.5.0.0_x64_Test\SpiderManInstaller_0.5.0.0_x64.msix --packaging-only
```

The native helper build downloads/checks the pinned DirectXTex revision and the pinned converter NuGet inputs. The launcher restores the framework and managed packages recorded in `DEPENDENCIES.json`. PrivateValidation is not a public-distribution approval.

## Rebuild with a changed SteamKit library

Unpack the verified `SteamKit-source.zip` to a separate folder. Do not use a game DLL as a replacement. In the SteamKit source root:

```powershell
dotnet build SteamKit2/SteamKit2/SteamKit2.csproj -c Release -f netstandard2.0 -p:TargetFrameworks=netstandard2.0
```

Then from the launcher root:

```powershell
.\tools\build-private.ps1 -SteamKitAssembly 'C:\YourSource\SteamKit\SteamKit2\SteamKit2\bin\Release\netstandard2.0\SteamKit2.dll'
```

This path was tested by adding an assembly-metadata attribute to SteamKit, rebuilding it, and successfully compiling the launcher through .NET Native. The native compiler input hash matched the changed DLL. A rebuilt library need not produce byte-identical compiler outputs; package signing and timestamps also affect MSIX hashes.

## Scope and remaining review

`SOURCE-INPUTS.json` records exact upstream archives and SHA-256 values. `collect-release-sources.py` verifies them. The matching launcher snapshot is identified by its source-file hash inventory and Git revision in the bundle, rather than merely linking to a moving branch.

This collection is a review artifact. Before public binary distribution, resolve the combined-license questions, decide the applicable framework/System Library treatment, and approve the complete corresponding-source arrangement for the exact binaries shipped. No indefinite written source offer is made here. The separate runtime is not licensed or rebuilt by this launcher bundle.

Validation on October 10: the launcher snapshot at commit 7a686b0 was extracted into a new directory and rebuilt successfully through .NET Native using the documented normal build command. All 33 converter/batch checks passed. The pinned upstream source archives were checked for SHA-256 and ZIP integrity. The separate modified-SteamKit rebuild test is described above.
