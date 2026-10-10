param([string]$VisualStudio = 'C:\Program Files\Microsoft Visual Studio\2022\Community')
$ErrorActionPreference = 'Stop'
$componentRoot = $PSScriptRoot
$compiler = Join-Path $VisualStudio 'MSBuild\Current\Bin\Roslyn\csc.exe'
$msbuild = Join-Path $VisualStudio 'MSBuild\Current\Bin\MSBuild.exe'
if (!(Test-Path -LiteralPath $compiler) -or !(Test-Path -LiteralPath $msbuild)) { throw 'Visual Studio build tools not found.' }
Push-Location $componentRoot
try {
    New-Item -ItemType Directory -Path 'bin','packages' -Force | Out-Null
    $dependencies = @(
        @('k4os.compression.lz4','1.3.8','3a64f72703b8aa99190cbed7aa216a642cb1c928fae2cf6dfa65dc3754fe2320','lib\net462\K4os.Compression.LZ4.dll'),
        @('system.memory','4.5.5','10f43da352a29fb2b3188e4edd4dcf5100194c8b526e4f61fe2e2b5623775a22','lib\net461\System.Memory.dll'),
        @('system.runtime.compilerservices.unsafe','6.0.0','6c41b53e70e9eee298cff3a02ce5acdd15b04125589be0273f0566026720a762','lib\net461\System.Runtime.CompilerServices.Unsafe.dll')
    )
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    foreach ($dependency in $dependencies) {
        $id,$version,$expected,$dll = $dependency
        $packagePath = Join-Path $componentRoot "packages\$id.$version.nupkg"
        if (!(Test-Path -LiteralPath $packagePath)) { Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/$id/$version/$id.$version.nupkg" -OutFile $packagePath }
        if ((Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw "Package hash mismatch: $id" }
        $unpacked = Join-Path $componentRoot "packages\$id.$version"
        if (!(Test-Path -LiteralPath $unpacked)) { [IO.Compression.ZipFile]::ExtractToDirectory($packagePath,$unpacked) }
        Copy-Item -LiteralPath (Join-Path $unpacked $dll) -Destination 'bin'
    }
    if (!(Test-Path -LiteralPath 'DirectXTex')) {
        & git clone --depth 1 --branch may2026 https://github.com/microsoft/DirectXTex.git DirectXTex
        if ($LASTEXITCODE -ne 0) { throw 'DirectXTex checkout failed.' }
    }
    $revision = & git -C DirectXTex rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or $revision -ne '4feb3e11a020f35b796fc769a74216a555d4f5ef') { throw 'Unexpected DirectXTex revision.' }
    & $msbuild TextureConvert.vcxproj /m:2 /p:Configuration=Release /p:Platform=x64 /verbosity:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw 'Native texture adapter build failed.' }
    & $msbuild TextureConvert.vcxproj /m:2 /p:Configuration=Release /p:Platform=x64 /p:AssetDesktop=true /verbosity:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw 'Desktop texture adapter build failed.' }
    $source = @('AssetGenerator.cs','AssetIndex.cs','ModelLod.cs','ResampledTexture.cs','AssetBatch.cs')
    $references = @('/r:bin\K4os.Compression.LZ4.dll','/r:bin\System.Memory.dll','/r:System.IO.Compression.dll')
    & $compiler /nologo /target:exe /optimize+ /platform:x64 /out:bin\AssetGenerator.exe @references @source
    if ($LASTEXITCODE -ne 0) { throw 'Converter build failed.' }
} finally { Pop-Location }
