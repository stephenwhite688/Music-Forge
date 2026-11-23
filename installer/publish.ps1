# MusicForge release build: self-contained win-x64 single-file publish.
# Output: ..\publish\win-x64\MusicForge.exe (+ Assets)
# Then compile the installer: ISCC installer\MusicForge.iss

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

try {
    dotnet publish "src\UI\MusicForge.UI.csproj" `
        -c Release -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o "publish\win-x64"

    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

    $exe = "publish\win-x64\MusicForge.exe"
    $size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Host "Published $exe ($size MB)"
    Write-Host "Next: ISCC installer\MusicForge.iss"
}
finally {
    Pop-Location
}
