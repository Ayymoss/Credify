#requires -Version 7
<#
.SYNOPSIS
    Packs the local (WIP) SharedLibraryCore NuGet — which now bundles WebCommon.dll —
    into Credify's local feed, and points Credify.csproj at the new version.

.DESCRIPTION
    WebCommon is no longer a standalone package; its assembly ships INSIDE the SLC nupkg
    (see SharedLibraryCore.csproj). Until that SLC version is published to nuget.org we pack
    it locally from the IW4MAdmin source clone.

    THE ONE RULE THIS SCRIPT EXISTS TO ENFORCE:
      Pack with -p:PackageVersion ONLY. NEVER pass -p:Version.

    -p:Version stamps the *assembly* version (AssemblyVersion/FileVersion). The host binds
    plugins by assembly identity, so if SLC/Data/WebCommon assemblies are stamped to some
    feed-only version (e.g. 2026.6.10.x) the plugin ends up referencing assemblies no host
    ships — IW4MAdmin then fails with "Could not load file or assembly 'Data, Version=...'"
    and refuses to start. PackageVersion moves the *NuGet* identity for feed resolution only;
    the assemblies keep their real csproj versions (SLC/WebCommon 2025.12.24.1, Data 1.0.0.0)
    so they bind against a normally-built host.

.PARAMETER CloneRoot
    Path to the IW4MAdmin source clone. Defaults to ..\..\..\_Cloned\IW4MAdmin relative to repo.

.PARAMETER Configuration
    Build configuration to pack. Default: Release.

.PARAMETER Version
    Explicit package version (e.g. 2026.7.1.1-preview). If omitted, the current version in
    Credify.csproj has its final numeric segment bumped by one (a fresh version each run,
    which also sidesteps the NuGet global-cache reuse that would otherwise serve a stale copy).

.EXAMPLE
    pwsh scripts/pack-slc.ps1
    # bumps version, builds WebCommon, packs SLC, updates Credify.csproj, restores.
#>
[CmdletBinding()]
param(
    [string]$CloneRoot,
    [string]$Configuration = 'Release',
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$csproj   = Join-Path $repoRoot 'Credify\Credify.csproj'
$feed     = Join-Path $repoRoot 'localpackages'

if (-not $CloneRoot) {
    $CloneRoot = Resolve-Path (Join-Path $repoRoot '..\..\_Cloned\IW4MAdmin') -ErrorAction SilentlyContinue
}
if (-not $CloneRoot -or -not (Test-Path $CloneRoot)) {
    throw "IW4MAdmin clone not found. Pass -CloneRoot <path>."
}

$webCommonProj = Join-Path $CloneRoot 'WebCommon\WebCommon.csproj'
$slcProj       = Join-Path $CloneRoot 'SharedLibraryCore\SharedLibraryCore.csproj'
foreach ($p in @($webCommonProj, $slcProj, $csproj)) {
    if (-not (Test-Path $p)) { throw "Required project not found: $p" }
}

$pkgId = 'RaidMax.IW4MAdmin.SharedLibraryCore'

# --- resolve the new package version ------------------------------------------------------
$csprojText = Get-Content $csproj -Raw
$refPattern = "(?<pre><PackageReference\s+Include=`"$([regex]::Escape($pkgId))`"\s+Version=`")(?<ver>[^`"]+)(?<post>`")"
$m = [regex]::Match($csprojText, $refPattern)
if (-not $m.Success) { throw "Could not find the $pkgId PackageReference in $csproj." }
$currentVersion = $m.Groups['ver'].Value

if (-not $Version) {
    # bump the final numeric segment of the base (pre-release tag preserved)
    $parts = $currentVersion -split '-', 2
    $base  = $parts[0]
    $tag   = if ($parts.Count -gt 1) { '-' + $parts[1] } else { '-preview' }
    $nums  = $base -split '\.'
    $nums[-1] = ([int]$nums[-1] + 1).ToString()
    $Version = ($nums -join '.') + $tag
}

Write-Host "Current : $currentVersion"  -ForegroundColor DarkGray
Write-Host "Packing : $Version"         -ForegroundColor Cyan
Write-Host "Clone   : $CloneRoot"       -ForegroundColor DarkGray
Write-Host ""

# --- build WebCommon first (its prebuilt dll is bundled into the SLC nupkg) ----------------
# NOTE: no -p:Version. WebCommon.dll must keep its csproj assembly version.
Write-Host "==> Building WebCommon ($Configuration)..." -ForegroundColor Yellow
dotnet build $webCommonProj -c $Configuration | Out-Host
if ($LASTEXITCODE -ne 0) { throw "WebCommon build failed." }

# --- pack SLC: PackageVersion ONLY ---------------------------------------------------------
New-Item -ItemType Directory -Force $feed | Out-Null
Get-ChildItem "$feed\*.nupkg" -ErrorAction SilentlyContinue | Remove-Item -Force

Write-Host "==> Packing SharedLibraryCore ($Configuration)..." -ForegroundColor Yellow
dotnet pack $slcProj -c $Configuration -p:PackageVersion=$Version -o $feed | Out-Host
if ($LASTEXITCODE -ne 0) { throw "SLC pack failed." }

$nupkg = Get-ChildItem "$feed\$pkgId.$Version.nupkg" -ErrorAction SilentlyContinue
if (-not $nupkg) { throw "Expected nupkg not produced: $pkgId.$Version.nupkg" }

# --- verify WebCommon.dll landed inside the package (mirrors the CI assertion) --------------
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($nupkg.FullName)
try {
    if (-not ($zip.Entries.FullName -contains 'lib/net10.0/WebCommon.dll')) {
        throw "WebCommon.dll missing from $($nupkg.Name) — build ordering regression."
    }
} finally { $zip.Dispose() }
Write-Host "    WebCommon.dll present in package." -ForegroundColor DarkGray

# --- evict any stale extraction of this exact version from the global cache ----------------
$globalCache = Join-Path $env:USERPROFILE ".nuget\packages\$($pkgId.ToLower())\$Version"
if (Test-Path $globalCache) { Remove-Item $globalCache -Recurse -Force }

# --- point Credify.csproj at the new version, then force-restore ---------------------------
$updated = [regex]::Replace($csprojText, $refPattern, "`${pre}$Version`${post}")
if ($updated -ne $csprojText) {
    Set-Content -Path $csproj -Value $updated -NoNewline
    Write-Host "==> Credify.csproj -> $Version" -ForegroundColor Yellow
}

Write-Host "==> Restoring Credify..." -ForegroundColor Yellow
dotnet restore (Join-Path $repoRoot 'Credify.slnx') --force --no-cache | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Restore failed." }

Write-Host ""
Write-Host "Done. SLC $Version packed (assemblies unstamped) and wired into Credify." -ForegroundColor Green
Write-Host "Reminder: rebuild + redeploy the IW4MAdmin host from the same clone so its" -ForegroundColor Green
Write-Host "runtime WebCommon.dll / SharedLibraryCore.dll match what the plugin references." -ForegroundColor Green
