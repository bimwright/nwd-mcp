#Requires -Version 5.1
# Installer regression tests. No Pester dependency: each case prints PASS/FAIL,
# -ResultPath writes a JSON report, and any failure exits non-zero.
# install.ps1 / uninstall.ps1 run as child processes of the same shell from a
# fake setup folder (the layout of the client setup ZIP). APPDATA and
# LOCALAPPDATA point into a disposable test root for the whole run, so the
# scripts never reach the real add-in, server or user data of this machine.
[CmdletBinding()]
param([string]$ResultPath, [string]$TestRootParent = [IO.Path]::GetTempPath())
$ErrorActionPreference = 'Stop'

$testRoot = Join-Path $TestRootParent ('nwd-installer-tests-' + [guid]::NewGuid().ToString('N'))
$sandboxAppData = Join-Path $testRoot 'profile\appdata'
$sandboxLocalAppData = Join-Path $testRoot 'profile\localappdata'
New-Item -ItemType Directory -Path $sandboxAppData, $sandboxLocalAppData -Force | Out-Null
$savedAppData = $env:APPDATA
$savedLocalAppData = $env:LOCALAPPDATA
$env:APPDATA = $sandboxAppData
$env:LOCALAPPDATA = $sandboxLocalAppData

$scriptsDir = Join-Path $PSScriptRoot '..\..\scripts'
$installer = (Resolve-Path (Join-Path $scriptsDir 'install.ps1')).Path
$uninstaller = (Resolve-Path (Join-Path $scriptsDir 'uninstall.ps1')).Path
$shell = (Get-Process -Id $PID).Path
$stubExe = Join-Path $env:WINDIR 'System32\PING.EXE'

$bundleDir = Join-Path $sandboxAppData 'Autodesk\ApplicationPlugins\Bimwright.Nwd.bundle'
$dataRoot = Join-Path $sandboxLocalAppData 'Bimwright\nwd-mcp'
$serverParent = Join-Path $dataRoot 'server'
$currentDir = Join-Path $serverParent 'current'
$backupPattern = '^current\.backup-[0-9a-f]{32}$'

$results = New-Object System.Collections.Generic.List[object]
function Assert($Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Test([string]$Name, [scriptblock]$Body) {
    Reset-Sandbox
    try { & $Body; $results.Add([pscustomobject]@{name=$Name;passed=$true}); Write-Host "PASS $Name" }
    catch { $results.Add([pscustomobject]@{name=$Name;passed=$false;error=$_.Exception.Message}); Write-Host "FAIL $Name : $_" }
}

function Reset-Sandbox {
    foreach ($dir in $sandboxAppData, $sandboxLocalAppData) {
        if (Test-Path -LiteralPath $dir) { Remove-Item -LiteralPath $dir -Recurse -Force }
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

# A setup folder shaped like the client setup ZIP. version.txt in server\ and
# bundle\ tells which release is installed.
function New-Setup([string]$Version) {
    $setup = Join-Path $testRoot ('setup-' + $Version + '-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path "$setup\bundle\Contents\2025", "$setup\server" -Force | Out-Null
    Copy-Item -LiteralPath $installer -Destination "$setup\install.ps1"
    Copy-Item -LiteralPath $uninstaller -Destination "$setup\uninstall.ps1"
    Set-Content "$setup\manifest.json" ('{"version":"' + $Version + '"}')
    Set-Content "$setup\bundle\PackageContents.xml" '<ApplicationPackage />'
    Set-Content "$setup\bundle\Contents\2025\version.txt" $Version
    Copy-Item -LiteralPath $stubExe -Destination "$setup\server\nwd-mcp.exe"
    Set-Content "$setup\server\e_sqlite3.dll" 'native'
    Set-Content "$setup\server\version.txt" $Version
    return $setup
}

# Runs a setup script in a child process of this shell. Refuses unless the
# profile folders it inherits are inside the test root.
function Invoke-Setup([string]$Script, [string[]]$Arguments = @()) {
    $root = [IO.Path]::GetFullPath($testRoot).TrimEnd('\') + '\'
    foreach ($p in $env:APPDATA, $env:LOCALAPPDATA) {
        if (-not [IO.Path]::GetFullPath($p).StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Profile env is not sandboxed ($p); refusing to run $Script"
        }
    }
    $ErrorActionPreference = 'Continue'
    $output = & $shell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $Script @Arguments 2>&1 | Out-String
    return [pscustomobject]@{ExitCode=$LASTEXITCODE; Output=$output}
}

function Install([string]$Setup, [string[]]$Arguments = @()) { Invoke-Setup (Join-Path $Setup 'install.ps1') $Arguments }

function Get-Backups {
    if (-not (Test-Path -LiteralPath $serverParent)) { return @() }
    return @(Get-ChildItem -LiteralPath $serverParent -Directory | Where-Object { $_.Name -match $backupPattern })
}

function Get-InstalledVersion { (Get-Content -LiteralPath "$currentDir\version.txt" -Raw).Trim() }

# Starts a test-owned copy of the stub exe, so the file is in use the way a
# running MCP server is. Its working folder is outside every server folder.
function Start-Stub([string]$Exe) {
    $proc = Start-Process -FilePath $Exe -ArgumentList '-n', '60', '127.0.0.1' -WorkingDirectory $testRoot -WindowStyle Hidden -PassThru
    Start-Sleep -Milliseconds 500
    Assert (-not $proc.HasExited) 'Stub process did not keep running'
    return $proc
}
function Stop-Stub($Proc) {
    if ($Proc) { Stop-Process -Id $Proc.Id -Force -ErrorAction SilentlyContinue; $null = $Proc.WaitForExit(10000) }
}

function Assert-Ok($Run, [string]$What) {
    Assert ($Run.ExitCode -eq 0) ("{0} exited {1}: {2}" -f $What, $Run.ExitCode, $Run.Output)
}

try {
    $v1 = New-Setup '1.0.0'
    $v2 = New-Setup '2.0.0'

    Test 'Fresh install places bundle and server and exits 0' {
        $run = Install $v1
        Assert-Ok $run 'install'
        Assert (Test-Path -LiteralPath "$bundleDir\PackageContents.xml") 'Bundle not installed'
        Assert (Test-Path -LiteralPath "$currentDir\nwd-mcp.exe") 'Server exe not installed'
        Assert ((Get-InstalledVersion) -eq '1.0.0') 'Wrong server version installed'
        Assert ((Get-Backups).Count -eq 0) 'Backup folder left after a fresh install'
        Assert ($run.Output -match 'MCP command:') 'MCP command line missing'
        Assert ($run.Output -notmatch 'In use\s*:') 'Fresh install reported an in-use copy'
    }

    Test 'install.ps1 -WhatIf changes nothing' {
        $run = Install $v1 @('-WhatIf')
        Assert-Ok $run 'install -WhatIf'
        Assert (-not (Test-Path -LiteralPath $bundleDir)) 'Bundle written under -WhatIf'
        Assert (-not (Test-Path -LiteralPath $serverParent)) 'Server written under -WhatIf'
    }

    Test 'Upgrade with no running server swaps current and removes the backup' {
        Assert-Ok (Install $v1) 'install v1'
        $run = Install $v2
        Assert-Ok $run 'install v2'
        Assert ((Get-InstalledVersion) -eq '2.0.0') 'Server not upgraded'
        Assert ((Get-Content -LiteralPath "$bundleDir\Contents\2025\version.txt" -Raw).Trim() -eq '2.0.0') 'Bundle not upgraded'
        Assert ((Get-Backups).Count -eq 0) 'Backup kept although the old server was not running'
        Assert ($run.Output -notmatch 'In use\s*:') 'In-use copy reported although none was running'
    }

    Test 'Upgrade while the old server runs exits 0, keeps the backup whole and reports it; next install sweeps it' {
        Assert-Ok (Install $v1) 'install v1'
        $proc = Start-Stub "$currentDir\nwd-mcp.exe"
        try {
            $run = Install $v2
            Assert-Ok $run 'install v2 while the old server runs'
            Assert ((Get-InstalledVersion) -eq '2.0.0') 'New server not in place'
            $backups = @(Get-Backups)
            Assert ($backups.Count -eq 1) ("Expected one kept backup, found {0}" -f $backups.Count)
            $backup = $backups[0].FullName
            foreach ($name in 'nwd-mcp.exe', 'e_sqlite3.dll', 'version.txt') {
                Assert (Test-Path -LiteralPath (Join-Path $backup $name)) "Kept backup lost $name"
            }
            Assert ((Get-Content -LiteralPath "$backup\version.txt" -Raw).Trim() -eq '1.0.0') 'Backup is not the old server'
            Assert ($run.Output -match ('In use\s*:\s*' + [regex]::Escape($backup) + ' - restart MCP clients; removed at next install')) "In-use line missing: $($run.Output)"
            Assert ($run.Output -notmatch 'Kept earlier server copies') 'Backup reported as an earlier server copy to repoint'

            # Still running: a second install keeps it and removes its own free backup.
            $run = Install $v2
            Assert-Ok $run 'second install while the old server runs'
            $backups = @(Get-Backups)
            Assert ($backups.Count -eq 1 -and $backups[0].FullName -eq $backup) 'Locked backup not kept, or the free backup not removed'
            Assert (Test-Path -LiteralPath "$backup\e_sqlite3.dll") 'Locked backup lost files on the second install'
            Assert ($run.Output -match 'In use\s*:') 'Locked backup not reported on the second install'
        } finally { Stop-Stub $proc }

        $run = Install $v2
        Assert-Ok $run 'install after the old server stopped'
        Assert ((Get-Backups).Count -eq 0) 'Released backup not swept'
        Assert ($run.Output -notmatch 'In use\s*:') 'In-use reported after the server stopped'
        Assert ((Get-InstalledVersion) -eq '2.0.0') 'Server wrong after the sweep'
    }

    Test 'Leftover backups from earlier runs are swept, locked ones skipped, other folders kept' {
        Assert-Ok (Install $v1) 'install v1'
        $free1 = Join-Path $serverParent ('current.backup-' + [guid]::NewGuid().ToString('N'))
        $free2 = Join-Path $serverParent ('current.backup-' + [guid]::NewGuid().ToString('N'))
        $locked = Join-Path $serverParent ('current.backup-' + [guid]::NewGuid().ToString('N'))
        $versioned = Join-Path $serverParent '0.9.0'
        $other = Join-Path $serverParent 'current.backup-mine'
        New-Item -ItemType Directory -Path $free1, $free2, $locked, $versioned, $other -Force | Out-Null
        Copy-Item -LiteralPath $stubExe -Destination "$free1\nwd-mcp.exe"
        Set-Content "$free1\e_sqlite3.dll" 'native'
        Set-Content "$free2\leftover.txt" 'no exe'
        Copy-Item -LiteralPath $stubExe -Destination "$locked\nwd-mcp.exe"
        Set-Content "$locked\e_sqlite3.dll" 'native'
        Set-Content "$versioned\nwd-mcp.exe" 'old'
        Set-Content "$other\keep.txt" 'keep'
        $proc = Start-Stub "$locked\nwd-mcp.exe"
        try {
            $run = Install $v2
            Assert-Ok $run 'install with leftovers'
            Assert (-not (Test-Path -LiteralPath $free1)) 'Free leftover with exe not swept'
            Assert (-not (Test-Path -LiteralPath $free2)) 'Free leftover without exe not swept'
            Assert (Test-Path -LiteralPath "$locked\e_sqlite3.dll") 'Locked leftover lost files'
            Assert ($run.Output -match ('In use\s*:\s*' + [regex]::Escape($locked))) 'Locked leftover not reported in use'
            Assert (Test-Path -LiteralPath "$versioned\nwd-mcp.exe") 'Versioned server copy removed'
            Assert (Test-Path -LiteralPath "$other\keep.txt") 'Folder outside the backup pattern removed'
            Assert ((Get-InstalledVersion) -eq '2.0.0') 'Server not upgraded'
        } finally { Stop-Stub $proc }
    }

    Test 'A failed swap restores the previous server and leaves no staging folder' {
        Assert-Ok (Install $v1) 'install v1'
        # An open handle without delete sharing blocks the rename of current itself.
        $handle = [IO.File]::Open("$currentDir\e_sqlite3.dll", 'Open', 'Read', 'None')
        try { $run = Install $v2 } finally { $handle.Dispose() }
        Assert ($run.ExitCode -ne 0) 'Failed swap reported success'
        Assert ((Get-InstalledVersion) -eq '1.0.0') 'Previous server not intact after the failed swap'
        Assert (Test-Path -LiteralPath "$currentDir\nwd-mcp.exe") 'Previous server exe missing after the failed swap'
        $staging = @(Get-ChildItem -LiteralPath $serverParent -Directory | Where-Object { $_.Name -like 'current.staging-*' })
        Assert ($staging.Count -eq 0) 'Staging folder left after the failed swap'
        Assert ((Get-Backups).Count -eq 0) 'Backup folder left after the failed swap'
    }

    $userData = @('nwd-mcp.config.json', 'reports\r1.json', 'baked\tool.cs', 'logs\a.log', 'nwd-mcp-calls.jsonl')
    function New-InstalledWithData {
        Assert-Ok (Install $v1) 'install v1'
        New-Item -ItemType Directory -Path "$dataRoot\spill", "$dataRoot\reports", "$dataRoot\baked", "$dataRoot\logs" -Force | Out-Null
        Set-Content "$dataRoot\spill\x.json" '{}'
        foreach ($f in $userData) { Set-Content (Join-Path $dataRoot $f) 'data' }
    }

    Test 'uninstall.ps1 removes bundle, server and spill and keeps user data' {
        New-InstalledWithData
        $run = Invoke-Setup (Join-Path $v1 'uninstall.ps1')
        Assert-Ok $run 'uninstall'
        Assert (-not (Test-Path -LiteralPath $bundleDir)) 'Bundle not removed'
        Assert (-not (Test-Path -LiteralPath $serverParent)) 'Server not removed'
        Assert (-not (Test-Path -LiteralPath "$dataRoot\spill")) 'Spill not removed'
        foreach ($f in $userData) { Assert (Test-Path -LiteralPath (Join-Path $dataRoot $f)) "User data removed: $f" }
    }
} finally {
    $env:APPDATA = $savedAppData
    $env:LOCALAPPDATA = $savedLocalAppData
    $report = [ordered]@{
        powershell = $PSVersionTable.PSVersion.ToString()
        installerSha256 = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash
        uninstallerSha256 = (Get-FileHash -LiteralPath $uninstaller -Algorithm SHA256).Hash
        results = @($results.ToArray())
    }
    if ($ResultPath) { $report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $ResultPath -Encoding UTF8 }
    # Only remove the uniquely named test folder under the temp root.
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $tempRoot = [IO.Path]::GetFullPath($TestRootParent).TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe test cleanup path' }
    Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction SilentlyContinue
}
$failed = @($results | Where-Object { -not $_.passed }).Count
Write-Host ("{0} passed, {1} failed" -f ($results.Count - $failed), $failed)
if ($failed) { exit 1 }
