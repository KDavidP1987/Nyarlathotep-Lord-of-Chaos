<#
.SYNOPSIS
    Release-surface sync check for Nyarlathotep, Lord of Chaos.

.DESCRIPTION
    Run from the repo root before any chore(release) commit. Verifies:
      1. Nyarlathotep.csproj <Version> == thunderstore.toml versionNumber
      2. Root CHANGELOG.md (full/GitHub) has an entry for that version
      3. Package CHANGELOG.md (concise/Thunderstore) has an entry for that version
      4. thunderstore.toml description is within Thunderstore's 250-char limit
      5. Shipped event templates (Resources/*.json) parse as JSON
      6. Working tree status (informational)

    Exits 1 if any hard check fails, 0 otherwise.

.EXAMPLE
    pwsh tools/preflight.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$failures = @()
$warnings = @()

$pkgDir            = Join-Path $repoRoot 'Nyarlathotep\Nyarlathotep'
$csprojPath        = Join-Path $pkgDir 'Nyarlathotep.csproj'
$tomlPath          = Join-Path $pkgDir 'thunderstore.toml'
$rootChangelogPath = Join-Path $repoRoot 'CHANGELOG.md'
$pkgChangelogPath  = Join-Path $pkgDir 'CHANGELOG.md'
$pkgReadmePath     = Join-Path $pkgDir 'README.md'

# --- 1. Version parity ---
$csprojVersion = ([xml](Get-Content $csprojPath -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$tomlRaw = Get-Content $tomlPath -Raw
$tomlVersion = if ($tomlRaw -match 'versionNumber\s*=\s*"([^"]+)"') { $Matches[1] } else { $null }

if (-not $csprojVersion) { $failures += "Could not read <Version> from Nyarlathotep.csproj" }
if (-not $tomlVersion)   { $failures += "Could not read versionNumber from thunderstore.toml" }
if ($csprojVersion -and $tomlVersion -and $csprojVersion -ne $tomlVersion) {
    $failures += "VERSION MISMATCH: csproj=$csprojVersion vs thunderstore.toml=$tomlVersion"
}
Write-Host "Version: csproj=$csprojVersion  toml=$tomlVersion"

# --- 2+3. Changelog entries for the current version ---
$version = $csprojVersion
if ($version) {
    $escaped = [regex]::Escape($version)
    if ((Get-Content $rootChangelogPath -Raw) -notmatch "##\s*\[?$escaped\]?") {
        $failures += "Root CHANGELOG.md (full/GitHub) has NO entry for $version"
    } else { Write-Host "Root CHANGELOG.md: entry for $version found" }

    if ((Get-Content $pkgChangelogPath -Raw) -notmatch "##\s*\[?$escaped\]?") {
        $failures += "Package CHANGELOG.md (Thunderstore) has NO entry for $version"
    } else { Write-Host "Package CHANGELOG.md: entry for $version found" }
}

# --- 4. Thunderstore description length (hard limit 250) ---
if ($tomlRaw -match 'description\s*=\s*"([^"]*)"') {
    $len = $Matches[1].Length
    if ($len -gt 250) { $failures += "thunderstore.toml description is $len chars (limit 250)" }
    else { Write-Host "Thunderstore description: $len/250 chars" }
}

# --- 5. Shipped event templates must be valid JSON ---
$resDir = Join-Path $pkgDir 'Resources'
if (Test-Path $resDir) {
    Get-ChildItem $resDir -Filter *.json -File | ForEach-Object {
        try { Get-Content $_.FullName -Raw | ConvertFrom-Json | Out-Null; Write-Host "Resources/$($_.Name): valid JSON" }
        catch { $failures += "Resources/$($_.Name) is not valid JSON: $($_.Exception.Message)" }
    }
}

# --- 5b. Icon present (Thunderstore requires a 256x256 icon.png) ---
if (-not (Test-Path (Join-Path $pkgDir 'icon.png'))) {
    $warnings += "icon.png is missing from Nyarlathotep/Nyarlathotep/ (required before a Thunderstore upload)"
}

# --- 5c. Doc hygiene (WARNINGS only — see docs/DOC_STYLE.md) ---
if (Test-Path $pkgReadmePath) {
    $proseLines = (Get-Content $pkgReadmePath | Where-Object {
        $t = $_.Trim()
        $t -ne '' -and $t -notmatch '^\|' -and $t -notmatch '^!\[' -and $t -notmatch '^#' -and $t -notmatch '^---' -and $t -notmatch '^>'
    }).Count
    if ($proseLines -gt 120) {
        $warnings += "Thunderstore README has $proseLines prose lines (soft budget ~120 — trim the explaining)"
    }
}

# --- 6. Working tree (informational) ---
try {
    $dirty = git -C $repoRoot status --porcelain
    if ($dirty) { Write-Host "NOTE: working tree has uncommitted changes:`n$dirty" }
    else { Write-Host "Working tree clean." }
} catch { Write-Host "NOTE: git status unavailable." }

if ($warnings.Count -gt 0) {
    Write-Host ""
    Write-Host "WARNINGS (non-blocking):" -ForegroundColor Yellow
    $warnings | ForEach-Object { Write-Host "  ! $_" -ForegroundColor Yellow }
}

if ($failures.Count -gt 0) {
    Write-Host ""
    Write-Host "PREFLIGHT FAILED:" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}

Write-Host ""
Write-Host "PREFLIGHT OK. Manual reminder: scan both READMEs (root=GitHub, package=Thunderstore) for staleness and keep docs to docs/DOC_STYLE.md." -ForegroundColor Green
exit 0
