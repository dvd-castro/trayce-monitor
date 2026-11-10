<#
.SYNOPSIS
  Builds the Trayce Monitor release files locally, the same way the release workflow does.

.DESCRIPTION
  Produces, in out/release (override with -OutDir):
    TrayceMonitor-<version>-win-x64-setup.exe      per-user installer (needs Inno Setup 6; skipped with a warning if missing)
    TrayceMonitor-<version>-win-x64-portable.zip   portable build
    SHA256SUMS.txt                                 SHA-256 of the files above (sha256sum format)

  Publish mode (-Aot):
    Auto (default)  NativeAOT if the MSVC linker is installed (Visual Studio / Build Tools with the C++ workload),
                    otherwise self-contained ReadyToRun + trimmed.
    On              NativeAOT; fails if it cannot build.
    Off             ReadyToRun + trimmed.

.PARAMETER Version
  Version without the leading "v", e.g. 0.1.0 or 0.1.0-rc.1. It is passed to MSBuild as -p:Version and shows in "Sobre".

.EXAMPLE
  ./tools/build-release.ps1 -Version 0.1.0
#>
[CmdletBinding()]
param(
    [string] $Version = '0.0.0-local',
    [ValidateSet('Auto', 'On', 'Off')] [string] $Aot = 'Auto',
    [string] $OutDir = '',
    [switch] $RequireInstaller   # fail instead of skipping when Inno Setup is not found (used by CI)
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
if (-not $OutDir) { $OutDir = Join-Path $root 'out/release' }
$OutDir = [System.IO.Path]::GetFullPath($OutDir)
$publishDir = Join-Path $OutDir 'publish'

if ($Version -notmatch '^(?<core>\d+\.\d+\.\d+)(-[0-9A-Za-z]+(\.[0-9A-Za-z]+)*)?$') {
    throw "Invalid version '$Version'. Use MAJOR.MINOR.PATCH or MAJOR.MINOR.PATCH-suffix (no leading 'v')."
}
$numericVersion = "$($Matches['core']).0"

function Find-Dotnet {
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $local = Join-Path $env:USERPROFILE '.dotnet/dotnet.exe'
    if (Test-Path $local) { return $local }
    throw 'dotnet SDK not found. Install the .NET 10 SDK.'
}

function Test-Msvc {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    if (-not (Test-Path $vswhere)) { return $false }
    $found = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    return [bool]$found
}

function Find-Iscc {
    if ($env:ISCC -and (Test-Path $env:ISCC)) { return $env:ISCC }
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $dirs = @(${env:ProgramFiles(x86)}, $env:ProgramFiles, (Join-Path $env:LOCALAPPDATA 'Programs')) | Where-Object { $_ }
    foreach ($d in $dirs) {
        $p = Join-Path $d 'Inno Setup 6/ISCC.exe'
        if (Test-Path $p) { return $p }
    }
    return $null
}

$dotnet = Find-Dotnet
$useAot = switch ($Aot) { 'On' { $true } 'Off' { $false } default { Test-Msvc } }
if ($Aot -eq 'Auto' -and -not $useAot) {
    Write-Warning 'MSVC linker not found: building self-contained ReadyToRun + trimmed instead of NativeAOT.'
}
$mode = if ($useAot) { 'NativeAOT' } else { 'ReadyToRun + trimmed' }
Write-Host "Trayce Monitor $Version ($mode)"

# --- clean output ---
if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
New-Item -ItemType Directory -Path $publishDir | Out-Null

# --- publish ---
$publishArgs = @('publish', (Join-Path $root 'src/TrayceMonitor/TrayceMonitor.csproj'), '-c', 'Release', '-o', $publishDir, "-p:Version=$Version")
if ($useAot) { $publishArgs += '-p:TrayceAot=true' }
& $dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }
Get-ChildItem $publishDir -Recurse -Filter *.pdb | Remove-Item -Force
$exe = Join-Path $publishDir 'TrayceMonitor.exe'
if (-not (Test-Path $exe)) { throw "TrayceMonitor.exe was not produced in $publishDir" }

# --- portable zip: one top-level folder; entry names use forward slashes ---
$base = "TrayceMonitor-$Version-win-x64"
$zipPath = Join-Path $OutDir "$base-portable.zip"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$topFolder = "$base-portable"
$zip = [System.IO.Compression.ZipFile]::Open($zipPath, 'Create')
try {
    $items = @()
    foreach ($f in Get-ChildItem $publishDir -Recurse -File) {
        $items += [pscustomobject]@{ Path = $f.FullName; Rel = $f.FullName.Substring($publishDir.Length).TrimStart([char[]]'\/') }
    }
    $items += [pscustomobject]@{ Path = (Join-Path $root 'LICENSE'); Rel = 'LICENSE' }
    foreach ($i in $items) {
        $entry = "$topFolder/" + $i.Rel.Replace('\', '/')
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $i.Path, $entry, [System.IO.Compression.CompressionLevel]::Optimal)
    }
}
finally { $zip.Dispose() }
Write-Host "Created $zipPath"

# --- installer ---
$setupPath = Join-Path $OutDir "$base-setup.exe"
$iscc = Find-Iscc
if (-not $iscc) {
    $msg = 'Inno Setup 6 (ISCC.exe) not found: skipping the installer. Install it from https://jrsoftware.org/isdl.php (the "install for me only" option needs no admin) or set the ISCC environment variable.'
    if ($RequireInstaller) { throw $msg }
    Write-Warning $msg
}
else {
    $iss = Join-Path $root 'installer/TrayceMonitor.iss'
    & $iscc '/Qp' "/DAppVersion=$Version" "/DNumericVersion=$numericVersion" "/DSourceDir=$publishDir" "/DOutputDir=$OutDir" $iss
    if ($LASTEXITCODE -ne 0) { throw "ISCC failed (exit $LASTEXITCODE)" }
    if (-not (Test-Path $setupPath)) { throw "Installer not found at $setupPath" }
    Write-Host "Created $setupPath"
}

# --- SHA256SUMS.txt (sha256sum format: "<hash> *<file>", LF line endings, no BOM) ---
$lines = foreach ($p in @($setupPath, $zipPath)) {
    if (Test-Path $p) { '{0} *{1}' -f (Get-FileHash $p -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path -Leaf $p) }
}
$sumsPath = Join-Path $OutDir 'SHA256SUMS.txt'
[System.IO.File]::WriteAllText($sumsPath, (($lines -join "`n") + "`n"), (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Created $sumsPath"
Get-Content $sumsPath
