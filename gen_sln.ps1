$ErrorActionPreference = "Stop"
$root = "H:\Audio forge with helix sound engine using winforms"
$slnPath = Join-Path $root "AudioForge.sln"
$engineDir = Join-Path $root "src\Sound Engine"

$csProjType = "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"
$folderType = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}"
$folderGuid = [guid]::NewGuid().ToString().ToUpper()

$projects = @()
$projects += [pscustomobject]@{
    Name = "AudioForge.UI"
    Path = "src\UI\AudioForge.UI.csproj"
    Guid = "{A8FDB46A-1234-4ABC-9DEF-112233445566}"
    Folder = $null
}

Get-ChildItem $engineDir -Recurse -Filter *.csproj | ForEach-Object {
    $rel = "src\Sound Engine\" + $_.FullName.Substring($engineDir.Length + 1)
    $bytes = [System.Security.Cryptography.MD5]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($rel))
    $g = New-Object byte[] 16
    [Array]::Copy($bytes, 0, $g, 0, 16)
    $g[7] = ($g[7] -band 0x0F) -bor 0x30
    $g[8] = ($g[8] -band 0x3F) -bor 0x80
    $guid = ([guid]::new($g).ToString().ToUpper())
    $projects += [pscustomobject]@{ Name = $_.BaseName; Path = $rel; Guid = $guid; Folder = $folderGuid }
}

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("")
[void]$sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00")
[void]$sb.AppendLine("# Visual Studio Version 18")
[void]$sb.AppendLine("VisualStudioVersion = 18.11.12328.437 stable")
[void]$sb.AppendLine("MinimumVisualStudioVersion = 10.0.40219.1")

foreach ($p in $projects) {
    [void]$sb.AppendLine("Project(`"$csProjType`") = `"$($p.Name)`", `"$($p.Path)`", `"$($p.Guid)`"")
    [void]$sb.AppendLine("EndProject")
}
[void]$sb.AppendLine("Project(`"$folderType`") = `"Sound Engine`", `"Sound Engine`", `"$folderGuid`"")
[void]$sb.AppendLine("EndProject")

[void]$sb.AppendLine("Global")
[void]$sb.AppendLine("`tGlobalSection(SolutionConfigurationPlatforms) = preSolution")
[void]$sb.AppendLine("`t`tDebug|Any CPU = Debug|Any CPU")
[void]$sb.AppendLine("`t`tRelease|Any CPU = Release|Any CPU")
[void]$sb.AppendLine("`tEndGlobalSection")
[void]$sb.AppendLine("`tGlobalSection(ProjectConfigurationPlatforms) = postSolution")
foreach ($p in $projects) {
    foreach ($cfg in @("Debug|Any CPU", "Release|Any CPU")) {
        [void]$sb.AppendLine("`t`t$($p.Guid).$cfg.ActiveCfg = $cfg")
        [void]$sb.AppendLine("`t`t$($p.Guid).$cfg.Build.0 = $cfg")
    }
}
[void]$sb.AppendLine("`tEndGlobalSection")
[void]$sb.AppendLine("`tGlobalSection(NestedProjects) = preSolution")
foreach ($p in $projects) {
    if ($p.Folder) { [void]$sb.AppendLine("`t`t$($p.Guid) = $($p.Folder)") }
}
[void]$sb.AppendLine("`tEndGlobalSection")
[void]$sb.AppendLine("`tGlobalSection(SolutionProperties) = preSolution")
[void]$sb.AppendLine("`t`tHideSolutionNode = FALSE")
[void]$sb.AppendLine("`tEndGlobalSection")
[void]$sb.AppendLine("EndGlobal")

Set-Content $slnPath $sb.ToString()
Write-Output "Wrote $($projects.Count) projects + Sound Engine folder"
$projects | ForEach-Object { Write-Output "  $($_.Name) -> $($_.Path)" }
