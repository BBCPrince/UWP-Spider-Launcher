$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$compiler = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Install the Visual Studio C# compiler or adjust its path in this script.' }
$output = Join-Path $repo 'test-output'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$common = @('ImportEngine.cs', 'LanguageImports.cs', 'FolderImportEngine.cs', 'PreparationEngine.cs') | ForEach-Object { Join-Path $repo "source\$_" }
$references = @('/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll','/r:System.Runtime.Serialization.dll','/r:System.Web.Extensions.dll')
foreach ($name in @('ImportTests','LanguageTests','WifiTests','PreparationTests','CleanupTests','DownloadProgressTests','TransferDiagnosticsTests')) {
    $extra = switch ($name) {
        'WifiTests' { @('WifiFolderTransfer.cs','WifiProtocol.cs') }
        'CleanupTests' { @('GameContentCleanup.cs') }
        'DownloadProgressTests' { @('DepotDownloader\DownloadProgress.cs') }
        'TransferDiagnosticsTests' { @('DepotDownloader\TransferDiagnostics.cs') }
        default { @() }
    }
    $files = @($common) + @($extra | ForEach-Object { Join-Path $repo "source\$_" }) + @(Join-Path $PSScriptRoot "$name.cs")
    $exe = Join-Path $output "$name.exe"
    & $compiler /nologo /target:exe /optimize+ "/out:$exe" @references @files
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $name" }
    switch ($name) {
        'LanguageTests' { & $exe (Join-Path $repo 'source\LanguagePacks.json') }
        'WifiTests' { & $exe (Join-Path $repo 'source\WifiTransfer.html') }
        default { & $exe }
    }
    if ($LASTEXITCODE -ne 0) { throw "Tests failed: $name" }
}
