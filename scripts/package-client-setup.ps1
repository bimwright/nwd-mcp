#Requires -Version 5.1
<#
.SYNOPSIS
  Build the end-user nwd-mcp setup ZIP (self-contained server + Navisworks plugins that compile here).
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Config = 'Release',
    [string]$RepoRoot,
    [string]$Version,
    [string]$OutputDir
)

$ErrorActionPreference = 'Stop'

if (-not $RepoRoot) { $RepoRoot = Split-Path -Parent $PSScriptRoot }
$RepoRoot = (Resolve-Path $RepoRoot).Path
if (-not $OutputDir) { $OutputDir = Join-Path $RepoRoot 'build\client-setup' }

if (-not $Version) {
    $csproj = Join-Path $RepoRoot 'src\server\Bimwright.Nwd.Server.csproj'
    $Version = ([regex]::Match((Get-Content -Raw $csproj), '<Version>([^<]+)</Version>')).Groups[1].Value
}
if (-not $Version) { throw 'Pass -Version.' }

$displayVersion = if ($Version.StartsWith('v')) { $Version } else { "v$Version" }
$stageRoot = Join-Path $OutputDir 'stage'
$serverStage = Join-Path $stageRoot 'server'
$bundleStage = Join-Path $stageRoot 'bundle'
$contentsStage = Join-Path $bundleStage 'Contents'

if (Test-Path $stageRoot) { Remove-Item $stageRoot -Recurse -Force }
New-Item -ItemType Directory -Path $serverStage, $contentsStage -Force | Out-Null

Write-Host "=== nwd-mcp package-client-setup ($displayVersion) ==="

$serverProject = Join-Path $RepoRoot 'src\server\Bimwright.Nwd.Server.csproj'
& dotnet publish $serverProject -c $Config -r win-x64 --self-contained true /p:PublishSingleFile=true -o $serverStage
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $LASTEXITCODE" }

$published = Join-Path $serverStage 'Bimwright.Nwd.Server.exe'
$friendly = Join-Path $serverStage 'nwd-mcp.exe'
if (-not (Test-Path $published)) { throw "Missing $published" }
Move-Item $published $friendly -Force

$packedYears = @()
foreach ($year in 2022..2027) {
    $nn = '{0:00}' -f ($year - 2000)
    $csproj = Join-Path $RepoRoot "src\plugin-navis$nn\Bimwright.Nwd.Plugin.Navis$nn.csproj"
    Write-Host "[plugin] Navisworks $year"
    & dotnet build $csproj -c $Config --nologo -v q
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Skipping Navisworks $year (Manage SDK missing or build failed)."
        continue
    }
    $outDir = Join-Path $RepoRoot "src\plugin-navis$nn\bin\$Config\net48"
    $dll = Join-Path $outDir "Bimwright.Nwd.Plugin.Navis$nn.dll"
    if (-not (Test-Path $dll)) { throw "Built but missing $dll" }
    $dest = Join-Path $contentsStage "$year"
    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    # Autodesk binaries are never redistributed: the API reference package is compile-time only, and this
    # filter keeps any Navisworks/AdWindows DLL out of the ZIP even if one reaches the output folder.
    Get-ChildItem $outDir -File | Where-Object {
        $_.Name -notmatch '^(Autodesk\.Navisworks\.|AdWindows\.|navisworks\.)'
    } | Copy-Item -Destination $dest -Force
    $packedYears += $year
}

if ($packedYears.Count -eq 0) { throw 'No Navisworks plugin years compiled. Cannot ship an empty ZIP.' }

$templatePath = Join-Path $RepoRoot 'scripts\PackageContents.template.xml'
[xml]$manifestXml = Get-Content -Raw $templatePath
$manifestXml.ApplicationPackage.AppVersion = $Version
$toRemove = @()
foreach ($comp in @($manifestXml.ApplicationPackage.Components)) {
    $mod = [string]$comp.ComponentEntry.ModuleName
    $keep = $false
    foreach ($yr in $packedYears) {
        if ($mod -match "/$yr/") { $keep = $true; break }
    }
    if (-not $keep) { $toRemove += $comp }
}
foreach ($comp in $toRemove) { [void]$manifestXml.ApplicationPackage.RemoveChild($comp) }
$manifestXml.Save((Join-Path $bundleStage 'PackageContents.xml'))

Copy-Item (Join-Path $RepoRoot 'scripts\install.ps1') (Join-Path $stageRoot 'install.ps1') -Force
Copy-Item (Join-Path $RepoRoot 'scripts\uninstall.ps1') (Join-Path $stageRoot 'uninstall.ps1') -Force

function Get-Rel([string]$Root, [string]$Path) {
    return $Path.Substring($Root.Length).TrimStart('\', '/') -replace '\\', '/'
}
function Get-Sha256Lower([string]$Path) {
    return ((Get-FileHash -Algorithm SHA256 -Path $Path).Hash).ToLowerInvariant()
}

$commit = ''
try { $commit = (& git -C $RepoRoot rev-parse HEAD).Trim() } catch { }

$files = @()
foreach ($f in @(Get-ChildItem $stageRoot -File -Recurse | Sort-Object FullName)) {
    $files += [ordered]@{ path = Get-Rel $stageRoot $f.FullName; sha256 = Get-Sha256Lower $f.FullName; bytes = $f.Length }
}

$manifest = [ordered]@{
    name = 'NwdMcp.Setup'
    version = $Version
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    commit = $commit
    platform = 'win-x64'
    packedNavisworksYears = @($packedYears)
    supportedNavisworksYears = @(2022, 2023, 2024, 2025, 2026, 2027)
    server = [ordered]@{ command = 'server/nwd-mcp.exe'; selfContained = $true; requiresDotnet = $false }
    files = $files
}
$manifest | ConvertTo-Json -Depth 20 | Set-Content (Join-Path $stageRoot 'manifest.json') -Encoding UTF8

$setupZip = Join-Path $OutputDir ("NwdMcp.Setup-{0}-win-x64.zip" -f $displayVersion)
if (Test-Path $setupZip) { Remove-Item $setupZip -Force }
Compress-Archive -Path (Join-Path $stageRoot '*') -DestinationPath $setupZip -Force
# Release checksum sidecar, same "<hash>  <file name>" format as rvt-mcp (attach both to the GitHub Release).
$setupHash = Get-Sha256Lower $setupZip
Set-Content -LiteralPath "$setupZip.sha256" -Value ("{0}  {1}" -f $setupHash, (Split-Path -Leaf $setupZip)) -Encoding ascii -NoNewline
Write-Host "Output : $setupZip"
Write-Host ("SHA-256: {0}  ({1})" -f $setupHash, (Split-Path -Leaf "$setupZip.sha256"))
Write-Host "Years  : $($packedYears -join ', ')"
