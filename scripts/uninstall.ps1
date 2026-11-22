#Requires -Version 5.1
<#
.SYNOPSIS
    Uninstall nwd-mcp: runs install.ps1 -Uninstall. -WhatIf and -Confirm are passed on.
#>
[CmdletBinding(SupportsShouldProcess)]
param()
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot\install.ps1" -Uninstall @PSBoundParameters
