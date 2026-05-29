#Requires -Version 5.1
<#
.SYNOPSIS
    Deploy the Bimwright.Nwd plugin bundle to local Navisworks Manage plugins.
.PARAMETER Year
    Navisworks year to deploy.
.PARAMETER Configuration
    Build configuration (Debug or Release). Defaults to Debug.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet("2022", "2023", "2024", "2025", "2026", "2027")]
    [string]$Year = "2026",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$bundleName = "Bimwright.Nwd.bundle"
$targetRoot = "$env:APPDATA\Autodesk\ApplicationPlugins\$bundleName"
$projectSuffix = "navis" + $Year.Substring(2,2)

# All versions are net48
$targetFramework = "net48"
$sourceDir = "$PSScriptRoot\..\src\plugin-$projectSuffix\bin\$Configuration\$targetFramework"

Write-Warning "RuntimeRequirements SeriesMin/SeriesMax for Navisworks have not been verified against a real Navisworks SDK on this machine. Verify them before treating release packaging as final."

if (-not (Test-Path $sourceDir)) {
    Write-Error "Source not found: $sourceDir. Build the plugin first."
    return
}

$contentsDir = "$targetRoot\Contents\$Year"
if ($PSCmdlet.ShouldProcess($contentsDir, "Deploy Navisworks $Year plugin bundle")) {
    New-Item -ItemType Directory -Path $targetRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $contentsDir -Force | Out-Null

    $manifest = [xml](Get-Content "$PSScriptRoot\PackageContents.template.xml" -Raw)
    $appName = "Bimwright.Nwd.Plugin.Navis$($Year.Substring(2, 2))"
    
    # Strip other components to keep local dev load fast
    $nodes = @($manifest.ApplicationPackage.Components)
    foreach ($component in $nodes) {
        if ($component.ComponentEntry.AppName -ne $appName) {
            [void]$component.ParentNode.RemoveChild($component)
        }
    }
    $manifest.Save("$targetRoot\PackageContents.xml")

    Copy-Item "$sourceDir\*" $contentsDir -Recurse -Force
    Write-Host "Installed Navisworks $Year plugin to: $contentsDir"
    Write-Host "Restart Navisworks to load the plugin."
}
