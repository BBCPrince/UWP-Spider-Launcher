param([string]$VisualStudio = 'C:\Program Files\Microsoft Visual Studio\2022\Community', [string]$SteamKitAssembly, [string]$CombinedRuntimePayloadRoot, [string]$CombinedPreparationSupportRoot)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$msbuild = Join-Path $VisualStudio 'MSBuild\Current\Bin\MSBuild.exe'
if ([bool]$CombinedRuntimePayloadRoot -ne [bool]$CombinedPreparationSupportRoot) { throw 'Provide both combined-runtime input directories, or neither for the default launcher build.' }
& (Join-Path $repo 'asset-preparation\build.ps1') -VisualStudio $VisualStudio
& (Join-Path $PSScriptRoot 'make-icons.ps1')
$options = @('/restore','/m:2','/p:Configuration=Release','/p:Platform=x64','/p:PrivateValidation=true','/p:AppxPackageSigningEnabled=false','/p:UapAppxPackageBuildMode=SideloadOnly','/verbosity:minimal','/nologo')
if ($SteamKitAssembly) {
    $assembly = (Resolve-Path -LiteralPath $SteamKitAssembly).Path
    $options += "/p:SteamKitAssembly=$assembly"
}
if ($CombinedRuntimePayloadRoot) {
    $payloadInput = (Resolve-Path -LiteralPath $CombinedRuntimePayloadRoot).Path
    $supportInput = (Resolve-Path -LiteralPath $CombinedPreparationSupportRoot).Path
    $options += '/p:CombinedRuntimeBuild=true'
    $options += "/p:CombinedRuntimePayloadRoot=$payloadInput"
    $options += "/p:CombinedPreparationSupportRoot=$supportInput"
}
& $msbuild (Join-Path $repo 'source\SpiderManInstaller.csproj') @options
if ($LASTEXITCODE -ne 0) { throw 'Private validation build failed.' }
Write-Output 'Launcher build complete. This does not approve public binary redistribution. Read release/STATUS.md.'
