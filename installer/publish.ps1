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

    # Visualiser plugins must exist as real files next to the exe - the
    # single-file bundle can't host plugin assemblies loaded by path.
    $pluginDll = Get-ChildItem "src\AudioForge.VisualPlugins.Samples\bin\Release" `
        -Recurse -Filter "AudioForge.VisualPlugins.Samples.dll" |
        Select-Object -First 1
    if ($pluginDll) {
        $dest = "publish\win-x64\visualisers"
        New-Item -ItemType Directory -Force $dest | Out-Null
        Copy-Item $pluginDll.FullName $dest
        Write-Host "Copied visualiser plugin -> $dest"
    } else {
        Write-Warning "VisualPlugins.Samples.dll not found - visualiser plugins will be missing"
    }

    $exe = "publish\win-x64\MusicForge.exe"
    $size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Host "Published $exe ($size MB)"
    Write-Host "Next: ISCC installer\MusicForge.iss"
}
finally {
    Pop-Location
}
