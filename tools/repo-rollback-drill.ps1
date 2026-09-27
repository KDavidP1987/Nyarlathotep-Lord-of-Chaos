<#
.SYNOPSIS
    Repository rollback drill (faction-empowerment D25): reverting release N back to N-1 restores N-1's tree, which
    builds and passes its tests and preflight.

.DESCRIPTION
    pwsh tools/repo-rollback-drill.ps1 -From v0.3.0 -To v0.4.0
      Runs D25's command with -From as the older tag and -To as the newer one: a disposable worktree of -To under
      %TEMP%\nyar-rollback-<guid>; `git revert --no-edit <From>..<To>`; `git diff --quiet <From>` (the reverted tree
      equals the older release); then, in that tree, the Release build (no deploy), `dotnet test` of
      Nyarlathotep/Nyarlathotep.Tests and the older release's own tools/preflight.ps1. Native commands stop the drill
      on a non-zero exit. The worktree is always removed. → "rollback: clean", else "rollback: fail — <message>", exit 1.

    pwsh tools/repo-rollback-drill.ps1 -SelfTest
      Three cases on a scratch git repository under %TEMP%\nyar-rollback-selftest-<guid> (removed when done), with the build,
      test and preflight stage replaced by a stub: a clean range passes; a range whose revert git cannot apply fails
      (a merge commit in the range; a linear range reverted from its tip never conflicts, so this is how a whole-range
      revert of this repository stops); a missing tag fails; and the -BeforePush rule over captured preflight output:
      the unpushed tag as the one failure passes, the same with a second failed check fails.
      → "repo rollback selftest: 5/5".

    -BeforePush (faction-empowerment A9): on the local tag before it is pushed, the older release's preflight fails
      its release-tags check ("release tags: <n>/<m> (<To> not pushed)"); that one failed check, and nothing else,
      is accepted. After the push, run without it: D25's command unchanged.
#>
[CmdletBinding()]
param(
    [string]$From,
    [string]$To,
    [switch]$BeforePush,
    [switch]$SelfTest
)
$ErrorActionPreference = 'Stop'
$Repo = Split-Path $PSScriptRoot -Parent

# D25's command, with the repository, the tags and the verification stage as parameters. Throws on any failure.
function Invoke-RepoRollback([string]$RepoDir, [string]$Old, [string]$New, [scriptblock]$Verify) {
    $ErrorActionPreference = 'Stop'
    # git with its last message in the error, so a failure names its cause (a missing tag, a refused revert).
    function Invoke-Git { $o = @(& git @args 2>&1); if ($LASTEXITCODE) { throw "git $($args[2]): $(($o | Select-Object -Last 1))" } }
    $wt = Join-Path $env:TEMP "nyar-rollback-$([guid]::NewGuid().ToString('N'))"
    Invoke-Git -C $RepoDir worktree add --detach $wt $New
    try {
        Invoke-Git -C $wt revert --no-edit "$Old..$New"
        git -C $wt diff --quiet $Old
        if ($LASTEXITCODE) { throw "the reverted tree differs from $Old" }
        & $Verify $wt
        'rollback: clean'
    } finally {
        git -C $RepoDir worktree remove --force $wt 2>&1 | Out-Null
        if (Test-Path -LiteralPath $wt) { Remove-Item -LiteralPath $wt -Recurse -Force -ErrorAction SilentlyContinue }
        git -C $RepoDir worktree prune 2>&1 | Out-Null
        # A locked file can keep the worktree folder; that is a failure now, not a later -Paths surprise (code review).
        if (Test-Path -LiteralPath $wt) { throw "leftover worktree folder $wt (a process still holds a file in it)" }
    }
}

# -BeforePush (faction-empowerment A9): the older release's preflight fails its release-tags check while the newer
# tag is unpushed. Accepted only when that is the one failed check and its only complaint is <Tag> not pushed.
# Returns $null when accepted, else why not.
function Test-PreflightBeforePush([string[]]$Out, [int]$Code, [string]$Tag) {
    if ($Code -eq 0) { return $null }
    $failed = $Out | Where-Object { $_ -match '^PREFLIGHT FAILED: \d+ check' } | Select-Object -Last 1
    if (-not $failed -or $failed -notmatch '^PREFLIGHT FAILED: 1 check') { return "preflight: $(if ($failed) { $failed } else { "exit $Code" })" }
    $want = "^release tags: \d+/\d+ \($([regex]::Escape($Tag)) not pushed\)$"
    if (-not ($Out | Where-Object { $_ -match $want })) { return "preflight: its one failed check is not 'release tags: <n>/<m> ($Tag not pushed)'" }
    return $null
}

$RealVerify = {
    param($wt)
    $PSNativeCommandUseErrorActionPreference = $true   # a non-zero build, test or preflight exit stops the drill (D25)
    dotnet build "$wt\Nyarlathotep\Nyarlathotep.sln" -c Release -p:VRisingServerPath=C:\__nodeploy__ | Out-Host
    dotnet test "$wt\Nyarlathotep\Nyarlathotep.Tests" | Out-Host
    if (-not $BeforePush) { pwsh -NoProfile -File "$wt\tools\preflight.ps1" | Out-Host; return }
    $PSNativeCommandUseErrorActionPreference = $false
    $out = @(pwsh -NoProfile -File "$wt\tools\preflight.ps1" 2>&1 | ForEach-Object { "$_" }); $code = $LASTEXITCODE
    $out | Out-Host
    $why = Test-PreflightBeforePush $out $code $To
    if ($why) { throw $why }
    if ($code) { Write-Host "preflight: the one failure is $To not pushed (-BeforePush, faction-empowerment A9)" }
}

if ($SelfTest) {
    $scratch = Join-Path $env:TEMP "nyar-rollback-selftest-$([guid]::NewGuid().ToString('N'))"
    $ok = 0
    $stub = { param($wt) if (-not (Test-Path -LiteralPath (Join-Path $wt 'a.txt'))) { throw 'stub: a.txt missing' } }
    try {
        New-Item -ItemType Directory -Path $scratch | Out-Null
        $g = { git -C $scratch -c user.name=drill -c user.email=drill@example.invalid @args 2>&1 | Out-Null; if ($LASTEXITCODE) { throw "git $args" } }
        & $g init -q -b main
        Set-Content -LiteralPath (Join-Path $scratch 'a.txt') -Value 'one'
        & $g add -A; & $g commit -q -m base; & $g tag v1
        Set-Content -LiteralPath (Join-Path $scratch 'a.txt') -Value 'two'
        Set-Content -LiteralPath (Join-Path $scratch 'b.txt') -Value 'new'
        & $g add -A; & $g commit -q -m change; & $g tag v2
        # A clean range: v2 reverted to v1.
        try { $r = Invoke-RepoRollback $scratch 'v1' 'v2' $stub; if ($r -eq 'rollback: clean') { $ok++ } else { Write-Host "  - clean range: got '$r'" } }
        catch { Write-Host "  - clean range: expected pass, got fail — $($_.Exception.Message)" }
        # A revert git cannot apply: a range holding a merge commit (git refuses it without -m; release history is
        # linear, so this is the one way a whole-range revert of it stops).
        & $g checkout -q v1
        Set-Content -LiteralPath (Join-Path $scratch 'c.txt') -Value 'side'
        & $g add -A; & $g commit -q -m side
        & $g checkout -q main
        & $g merge -q --no-ff -m merge '@{-1}'; & $g tag v3
        $refused = $false
        try { $null = Invoke-RepoRollback $scratch 'v2' 'v3' $stub } catch { $refused = $true; Write-Host "  - merge in range: fails — $($_.Exception.Message)" }
        if ($refused) { $ok++ } else { Write-Host '  - revert git cannot apply: expected fail, got pass' }
        # A missing tag.
        $missing = $false
        try { $null = Invoke-RepoRollback $scratch 'v1' 'v9' $stub } catch { $missing = $true; Write-Host "  - missing tag: fails — $($_.Exception.Message)" }
        if ($missing) { $ok++ } else { Write-Host '  - missing tag: expected fail, got pass' }
        # -BeforePush (A9) over captured preflight output: the unpushed tag alone is accepted, a second failure is not.
        $only = @('release tags: 3/4 (v2 not pushed)', '', 'PREFLIGHT FAILED: 1 check(s)')
        if ($null -eq (Test-PreflightBeforePush $only 1 'v2')) { $ok++ } else { Write-Host '  - before push, only v2 not pushed: expected pass' }
        $more = @('changelogs: 0.2.0 missing from CHANGELOG.md', 'release tags: 3/4 (v2 not pushed)', '', 'PREFLIGHT FAILED: 2 check(s)')
        $why = Test-PreflightBeforePush $more 1 'v2'
        if ($why) { $ok++; Write-Host "  - before push, another failure: fails - $why" } else { Write-Host '  - before push, another failure: expected fail, got pass' }
    } catch { Write-Host "  - scratch repository: $($_.Exception.Message)" }
    finally { Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue }
    Write-Host "repo rollback selftest: $ok/5"
    exit ([int]($ok -ne 5))
}

if (-not $From -or -not $To) { Write-Host 'usage: repo-rollback-drill.ps1 -From <older tag> -To <newer tag> | -SelfTest'; exit 2 }
try { $line = Invoke-RepoRollback $Repo $From $To $RealVerify; Write-Host $line; exit 0 }
catch { Write-Host "rollback: fail — $($_.Exception.Message)"; exit 1 }
