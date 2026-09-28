<#
.SYNOPSIS
    Release verify (faction-empowerment D24): the zip on the GitHub pre-release is the zip the release audit recorded.

.DESCRIPTION
    pwsh tools/release-verify.ps1 -Tag v0.4.0 -Asset kdpen-Nyarlathotep-0.4.0.zip [-Audit docs/audits/faction-empowerment.md]
      Without -Audit it reads every docs/audits/*.md and uses the one that records the asset's line (event-library A31):
      none, or two or more, fail. Downloads the asset with `gh release download <Tag> -p <Asset>` into %TEMP%\nyar-rel-<guid> (always removed),
      hashes it (SHA-256) and compares the hash with the audit's line "zip sha256: <Asset> <hash>", written when the
      zip was built. → "release verify: hashes equal", else "release verify: fail — <why>" and exit 1.

    pwsh tools/release-verify.ps1 -SelfTest
      Eight cases with a local folder as the asset source instead of gh (scratch under %TEMP%\nyar-rel-<guid>, removed
      when done): a matching hash passes; a differing hash fails; a missing asset fails; an audit without the line
      fails, including one that quotes the line inside another bullet (the line must stand on its own); an audit with two
      lines for the asset fails; and the audit
      search: one audit recording the asset is found, no audit and two audits fail. → "release verify selftest: 8/8".
#>
[CmdletBinding()]
param(
    [string]$Tag,
    [string]$Asset,
    [string]$Audit,
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
    $all = [regex]::Matches("$AuditText", "(?m)^\s*(?:-\s+)?zip sha256: $([regex]::Escape($Name)) ([0-9A-Fa-f]{64})\s*$")   # its own line (Codex F1)
    if ($all.Count -eq 0) { return "the audit has no ""zip sha256: $Name <hash>"" line" }
    if ($all.Count -gt 1) { return "the audit has $($all.Count) ""zip sha256: $Name"" lines; keep only the one for the published zip" }
    $m = $all[0]
    try { $hash = Get-AssetHash $Name $Fetch } catch { return "no asset $Name ($($_.Exception.Message))" }
    if ($hash -ne $m.Groups[1].Value.ToUpperInvariant()) { return "hashes differ (release $hash, audit $($m.Groups[1].Value))" }
    return $null
}

# The audit that records the asset (A31): $Texts maps an audit's path to its text. Returns @(path, $null) or
# @($null, reason). The same own-line match as Test-ReleaseAsset, so a line quoted inside another bullet does not count.
function Find-ReleaseAudit([string]$Name, [hashtable]$Texts) {
    $rx = "(?m)^\s*(?:-\s+)?zip sha256: $([regex]::Escape($Name)) [0-9A-Fa-f]{64}\s*$"
    $hits = @($Texts.Keys | Where-Object { [regex]::IsMatch("$($Texts[$_])", $rx) } | Sort-Object)
    if ($hits.Count -eq 0) { return @($null, "no audit under docs/audits records ""zip sha256: $Name <hash>""") }
    if ($hits.Count -gt 1) { return @($null, "$($hits.Count) audits record ""zip sha256: $Name"" ($($hits -join ', ')); pass -Audit") }
    return @($hits[0], $null)
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
            @{ Name = 'audit without the line'; Asset = $name; Audit = "- tcli build: done`n- an old note: zip sha256: $name $hash"; Pass = $false },
            @{ Name = 'two lines in one audit (a rebuilt zip)'; Asset = $name; Audit = "- zip sha256: $name $('0' * 64)`n- zip sha256: $name $hash"; Pass = $false })
        foreach ($c in $cases) {
            $why = Test-ReleaseAsset $c.Asset $c.Audit $local
            if (($null -eq $why) -eq $c.Pass) { $ok++; if ($why) { Write-Host "  - $($c.Name): fails — $why" } }
            else { Write-Host "  - $($c.Name): expected $(if ($c.Pass) { 'pass' } else { 'fail' }), got $(if ($why) { "fail — $why" } else { 'pass' })" }
        }
        $line = "- zip sha256: $name $hash"
        $finds = @(
            @{ Name = 'one audit records the asset'; Texts = @{ 'a.md' = $line; 'b.md' = "- an old note: zip sha256: $name $hash" }; Pass = $true },
            @{ Name = 'no audit records the asset'; Texts = @{ 'a.md' = '- tcli build: done'; 'b.md' = '' }; Pass = $false },
            @{ Name = 'two audits record the asset'; Texts = @{ 'a.md' = $line; 'b.md' = $line }; Pass = $false })
        foreach ($c in $finds) {
            $r = Find-ReleaseAudit $name $c.Texts
            $good = if ($c.Pass) { $r[0] -eq 'a.md' -and -not $r[1] } else { -not $r[0] -and $r[1] }
            if ($good) { $ok++; if ($r[1]) { Write-Host "  - $($c.Name): fails — $($r[1])" } }
            else { Write-Host "  - $($c.Name): expected $(if ($c.Pass) { 'a.md' } else { 'fail' }), got $(if ($r[1]) { "fail — $($r[1])" } else { $r[0] })" }
        }
    } finally { Remove-Item -LiteralPath $src -Recurse -Force -ErrorAction SilentlyContinue }
    Write-Host "release verify selftest: $ok/8"
    exit ([int]($ok -ne 8))
}

if (-not $Tag -or -not $Asset) { Write-Host 'usage: release-verify.ps1 -Tag <tag> -Asset <zip name> [-Audit <audit.md>] | -SelfTest'; exit 2 }
if ($Audit) { $auditPath = if ([IO.Path]::IsPathRooted($Audit)) { $Audit } else { Join-Path $Repo $Audit } }
else {
    $texts = @{}
    Get-ChildItem -LiteralPath (Join-Path $Repo 'docs/audits') -Filter *.md -File | ForEach-Object { $texts[$_.FullName] = Get-Content -LiteralPath $_.FullName -Raw }
    $found = Find-ReleaseAudit $Asset $texts
    if ($found[1]) { Write-Host "release verify: fail — $($found[1])"; exit 1 }
    $auditPath = $found[0]
}
# The closure has its own scope, so gh's exit code is checked here (step 7 code review).
$gh = { param($d, $n) gh release download $Tag -R KDavidP1987/Nyarlathotep-Lord-of-Chaos -p $n -D $d; if ($LASTEXITCODE) { throw "gh release download exit $LASTEXITCODE" } }.GetNewClosure()
$why = Test-ReleaseAsset $Asset (Get-Content -LiteralPath $auditPath -Raw) $gh
if ($why) { Write-Host "release verify: fail — $why"; exit 1 }
Write-Host 'release verify: hashes equal'
