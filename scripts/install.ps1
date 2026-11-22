#Requires -Version 5.1
<#
.SYNOPSIS
    Install or uninstall nwd-mcp from a client setup ZIP (or -Uninstall).
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'
$bundleName = 'Bimwright.Nwd.bundle'
$targetRoot = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\$bundleName"

$setupVersion = 'dev'
if (Test-Path (Join-Path $PSScriptRoot 'manifest.json')) {
    $setupVersion = ((Get-Content -Raw (Join-Path $PSScriptRoot 'manifest.json')) | ConvertFrom-Json).version
}
$serverInstallRoot = Join-Path $env:LOCALAPPDATA 'Bimwright\nwd-mcp\server\current'

# A running server's exe cannot be deleted. Probe it first so a copy that an
# MCP client still runs is kept whole instead of half-deleted.
function Remove-ServerCopy([string]$Dir) {
    $exe = Join-Path $Dir 'nwd-mcp.exe'
    if (Test-Path -LiteralPath $exe) {
        try { Remove-Item -LiteralPath $exe -Force -ErrorAction Stop } catch { return $false }
    }
    try { Remove-Item -LiteralPath $Dir -Recurse -Force -ErrorAction Stop; return $true }
    catch { Write-Warning ("Could not fully remove {0}: {1}" -f $Dir, $_.Exception.Message); return $false }
}

if ($Uninstall) {
    if ($PSCmdlet.ShouldProcess($targetRoot, 'Remove Navisworks bundle')) {
        if (Test-Path $targetRoot) { Remove-Item $targetRoot -Recurse -Force }
        Write-Host "Removed plugin bundle (if present): $targetRoot"
    }
    $serverParent = Join-Path $env:LOCALAPPDATA 'Bimwright\nwd-mcp\server'
    if ($PSCmdlet.ShouldProcess($serverParent, 'Remove installed servers')) {
        if (Test-Path $serverParent) { Remove-Item $serverParent -Recurse -Force }
        Write-Host "Removed server installs (if present): $serverParent"
    }
    $spillDir = Join-Path $env:LOCALAPPDATA 'Bimwright\nwd-mcp\spill'
    if ($PSCmdlet.ShouldProcess($spillDir, 'Remove spill cache')) {
        if (Test-Path $spillDir) { Remove-Item $spillDir -Recurse -Force }
        Write-Host "Removed spill cache (if present): $spillDir"
    }
    return
}

if (-not (Test-Path (Join-Path $PSScriptRoot 'bundle'))) {
    Write-Error 'This install.ps1 expects a client setup ZIP (bundle/ + server/). For a local build use scripts/install-bundle.ps1.'
    return
}

if ($PSCmdlet.ShouldProcess($targetRoot, 'Install nwd-mcp plugin bundle')) {
    if (Test-Path $targetRoot) { Remove-Item $targetRoot -Recurse -Force }
    Copy-Item (Join-Path $PSScriptRoot 'bundle') $targetRoot -Recurse -Force
    Write-Host "Installed plugin bundle: $targetRoot"
}

$exeSrc = Join-Path $PSScriptRoot 'server\nwd-mcp.exe'
if (Test-Path $exeSrc) {
    if ($PSCmdlet.ShouldProcess($serverInstallRoot, 'Install nwd-mcp.exe')) {
        # Replace the whole current\ folder: stage beside it, swap with two
        # renames, restore the backup on failure. A plain copy would leave
        # stale files from the previous release behind. Directory.Move is one
        # rename: a running server exe does not block it, and a locked file
        # fails it whole (Move-Item in PowerShell 7 can stop halfway).
        $serverParent = Split-Path -Parent $serverInstallRoot
        New-Item -ItemType Directory -Path $serverParent -Force | Out-Null
        $stageDir = $serverInstallRoot + '.staging-' + [guid]::NewGuid().ToString('N')
        $backupDir = $serverInstallRoot + '.backup-' + [guid]::NewGuid().ToString('N')
        New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
        Copy-Item (Join-Path $PSScriptRoot 'server\*') $stageDir -Recurse -Force
        $backupMoved = $false
        try {
            if (Test-Path -LiteralPath $serverInstallRoot) {
                [IO.Directory]::Move($serverInstallRoot, $backupDir)
                $backupMoved = $true
            }
            [IO.Directory]::Move($stageDir, $serverInstallRoot)
        } catch {
            if ($backupMoved -and -not (Test-Path -LiteralPath $serverInstallRoot)) {
                [IO.Directory]::Move($backupDir, $serverInstallRoot)
            }
            Remove-Item -LiteralPath $stageDir -Recurse -Force -ErrorAction SilentlyContinue
            throw
        }
        $exe = Join-Path $serverInstallRoot 'nwd-mcp.exe'
        Write-Host "Installed server: $exe"
        Write-Host "MCP command: $exe"
        # The new server is in place. Remove this run's backup and the ones
        # earlier runs kept; a copy an MCP client still runs stays whole until
        # a later install finds it free.
        $backupPattern = '^current\.backup-[0-9a-f]{32}$'
        $inUse = @()
        foreach ($dir in @(Get-ChildItem -LiteralPath $serverParent -Directory | Where-Object { $_.Name -match $backupPattern })) {
            if (-not (Remove-ServerCopy $dir.FullName)) { $inUse += $dir.FullName }
        }
        if ($inUse.Count) {
            Write-Host ("In use  : {0} - restart MCP clients; removed at next install" -f ($inUse -join ', '))
        }
        # Older installers wrote versioned copies (server\<version>\). Keep them,
        # but tell the user clients should point at the fixed current path.
        $versioned = @(Get-ChildItem -LiteralPath $serverParent -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -ne 'current' -and $_.Name -notmatch $backupPattern })
        if ($versioned.Count) {
            Write-Host ("Kept earlier server copies: {0}. Repoint MCP client entries to {1}" -f ($versioned.Name -join ', '), $exe)
        }
    }
}

Write-Host 'Restart Navisworks Manage to load the add-in. Packed years are listed in manifest.json.'
