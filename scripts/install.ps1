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
        # stale files from the previous release behind.
        $serverParent = Split-Path -Parent $serverInstallRoot
        New-Item -ItemType Directory -Path $serverParent -Force | Out-Null
        $stageDir = $serverInstallRoot + '.staging-' + [guid]::NewGuid().ToString('N')
        $backupDir = $serverInstallRoot + '.backup-' + [guid]::NewGuid().ToString('N')
        New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
        Copy-Item (Join-Path $PSScriptRoot 'server\*') $stageDir -Recurse -Force
        $backupMoved = $false
        try {
            if (Test-Path -LiteralPath $serverInstallRoot) {
                Move-Item -LiteralPath $serverInstallRoot -Destination $backupDir
                $backupMoved = $true
            }
            Move-Item -LiteralPath $stageDir -Destination $serverInstallRoot
        } catch {
            if ($backupMoved -and -not (Test-Path -LiteralPath $serverInstallRoot)) {
                Move-Item -LiteralPath $backupDir -Destination $serverInstallRoot
            }
            Remove-Item -LiteralPath $stageDir -Recurse -Force -ErrorAction SilentlyContinue
            throw
        }
        if ($backupMoved) { Remove-Item -LiteralPath $backupDir -Recurse -Force }
        $exe = Join-Path $serverInstallRoot 'nwd-mcp.exe'
        Write-Host "Installed server: $exe"
        Write-Host "MCP command: $exe"
        # Older installers wrote versioned copies (server\<version>\). Keep them,
        # but tell the user clients should point at the fixed current path.
        $versioned = @(Get-ChildItem -LiteralPath $serverParent -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -ne 'current' })
        if ($versioned.Count) {
            Write-Host ("Kept earlier server copies: {0}. Repoint MCP client entries to {1}" -f ($versioned.Name -join ', '), $exe)
        }
    }
}

Write-Host 'Restart Navisworks Manage to load the add-in. Packed years are listed in manifest.json.'
