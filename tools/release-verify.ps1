<#
.SYNOPSIS
    Release verify (faction-empowerment D24): the zip on the GitHub pre-release is the zip the release audit recorded.

.DESCRIPTION
    pwsh tools/release-verify.ps1 -Tag v0.4.0 -Asset kdpen-Nyarlathotep-0.4.0.zip [-Audit docs/audits/faction-empowerment.md]
      Downloads the asset with `gh release download <Tag> -p <Asset>` into %TEMP%\nyar-rel-<guid> (always removed),
      hashes it (SHA-256) and compares the hash with the audit's line "zip sha256: <Asset> <hash>", written when the
      zip was built. → "release verify: hashes equal", else "release verify: fail — <why>" and exit 1.

    pwsh tools/release-verify.ps1 -SelfTest
      Four cases with a local folder as the asset source instead of gh (scratch under %TEMP%\nyar-rel-<guid>, removed
      when done): a matching hash passes; a differing hash fails; a missing asset fails; an audit without the line
      fails, including one that quotes the line inside another bullet (the line must stand on its own). → "release verify selftest: 4/4".
#>
[CmdletBinding()]
param(
    [string]$Tag,
    [string]$Asset,
    [string]$Audit = 'docs/audits/faction-empowerment.md',
    [switch]$SelfTest
)
$ErrorActionPreference = 'Stop'
$Repo = Split-Path $PSScriptRoot -Parent

# D24's command with the download as a parameter: $Fetch receives the folder and the asset name and puts the asset
# there. Returns the asset's SHA-256; throws when it cannot.
function Get-AssetHash([string]$Name, [scriptblock]$Fetch) {
    $ErrorActionPreference = 'Stop'; $PSNativeCommandUseErrorActionPreference = $true
    $d = Join-Path $env:TEMP "nyar-rel-$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory $d | Out-Null
    try {
        & $Fetch $d $Name
        (Get-FileHash (Join-Path $d $Name) -Algorithm SHA256).Hash
    } finally { Remove-Item -Recurse -Force $d }
}

# Compares the asset's hash with the audit's "zip sha256: <asset> <hash>" line. Returns $null or the reason it fails.
function Test-ReleaseAsset([string]$Name, [string]$AuditText, [scriptblock]$Fetch) {
    $m = [regex]::Match("$AuditText", "(?m)^\s*(?:-\s+)?zip sha256: $([regex]::Escape($Name)) ([0-9A-Fa-f]{64})\s*$")   # its own line (Codex F1)
    if (-not $m.Success) { return "the audit has no ""zip sha256: $Name <hash>"" line" }
    try { $hash = Get-AssetHash $Name $Fetch } catch { return "no asset $Name ($($_.Exception.Message))" }
    if ($hash -ne $m.Groups[1].Value.ToUpperInvariant()) { return "hashes differ (release $hash, audit $($m.Groups[1].Value))" }
    return $null
}

if ($SelfTest) {
    $src = Join-Path $env:TEMP "nyar-rel-$([guid]::NewGuid().ToString('N'))"
    $ok = 0
    try {
        New-Item -ItemType Directory -Path $src | Out-Null
        $name = 'kdpen-Nyarlathotep-9.9.9.zip'
        [IO.File]::WriteAllBytes((Join-Path $src $name), [byte[]](1..64))
        $hash = (Get-FileHash (Join-Path $src $name) -Algorithm SHA256).Hash
        $local = { param($d, $n) Copy-Item -LiteralPath (Join-Path $src $n) -Destination $d }.GetNewClosure()
        $cases = @(
            @{ Name = 'matching hash'; Asset = $name; Audit = "- zip sha256: $name $hash"; Pass = $true },
            @{ Name = 'differing hash'; Asset = $name; Audit = "- zip sha256: $name $('0' * 64)"; Pass = $false },
            @{ Name = 'missing asset'; Asset = 'kdpen-Nyarlathotep-9.9.8.zip'; Audit = "- zip sha256: kdpen-Nyarlathotep-9.9.8.zip $hash"; Pass = $false },
            @{ Name = 'audit without the line'; Asset = $name; Audit = "- tcli build: done`n- an old note: zip sha256: $name $hash"; Pass = $false })
        foreach ($c in $cases) {
            $why = Test-ReleaseAsset $c.Asset $c.Audit $local
            if (($null -eq $why) -eq $c.Pass) { $ok++; if ($why) { Write-Host "  - $($c.Name): fails — $why" } }
            else { Write-Host "  - $($c.Name): expected $(if ($c.Pass) { 'pass' } else { 'fail' }), got $(if ($why) { "fail — $why" } else { 'pass' })" }
        }
    } finally { Remove-Item -LiteralPath $src -Recurse -Force -ErrorAction SilentlyContinue }
    Write-Host "release verify selftest: $ok/4"
    exit ([int]($ok -ne 4))
}

if (-not $Tag -or -not $Asset) { Write-Host 'usage: release-verify.ps1 -Tag <tag> -Asset <zip name> [-Audit <audit.md>] | -SelfTest'; exit 2 }
$auditPath = if ([IO.Path]::IsPathRooted($Audit)) { $Audit } else { Join-Path $Repo $Audit }
$gh = { param($d, $n) gh release download $Tag -R KDavidP1987/Nyarlathotep-Lord-of-Chaos -p $n -D $d }.GetNewClosure()
$why = Test-ReleaseAsset $Asset (Get-Content -LiteralPath $auditPath -Raw) $gh
if ($why) { Write-Host "release verify: fail — $why"; exit 1 }
Write-Host 'release verify: hashes equal'
