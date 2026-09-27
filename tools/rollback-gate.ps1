<#
.SYNOPSIS
    One rollback gate (faction-empowerment D29, event-library D29): the rollback checks a release must pass, run as one command.

.DESCRIPTION
    pwsh tools/rollback-gate.ps1 -From v0.4.0 -To v0.5.0 [-Plan event-library]
      Runs, in order, each in its own pwsh process:
        1. repo      tools/repo-rollback-drill.ps1 -From <From> -To <To>   → "rollback: clean" (D25)
                     (with -BeforePush while <To> is not on origin, faction-empowerment A9)
        2. drill     tools/rollback-drill.ps1 -From <To> -To <From>        → "rollback drill: pass" (D23)
        3. snapshot  tools/dev-snapshot.ps1 -SelfTest                      → "snapshot selftest: 6/6" (D28)
        4. routes    tools/preflight.ps1 -RollbackOf <Plan> -From <From> -To <To>
                                                                           → "rollback routes: <Plan> 5/5" (event-library D29;
                     only with -Plan: every route of the plan's Rollout › Rollback, and its range equal to -From..-To)
      A part passes only when it exits 0 and prints its success line. Every part runs even after one fails.
      → "rollback gate: <n>/<n>" (n = 3, or 4 with -Plan), else "rollback gate: <k>/<n>, failed: <names>" and exit 1.
      With fewer than two release tags (v<digits>…) nothing runs: "rollback gate: 0/<n>, failed: needs two releases".
      The drill needs the dev server stopped (it refuses otherwise, and that part fails).

    pwsh tools/rollback-gate.ps1 -SelfTest
      Six cases over four stub parts (scripts under %TEMP%\nyar-rollback-gatetest-<guid>, removed when done): all four
      pass → pass; each of repo, drill, snapshot and routes failing alone → fail naming it; a part exiting 0 without
      its success line → fail. → "rollback gate selftest: 6/6".
#>
[CmdletBinding()]
param(
    [string]$From,
    [string]$To,
    [string]$Plan,
    [switch]$SelfTest
)
$ErrorActionPreference = 'Stop'
$Repo = Split-Path $PSScriptRoot -Parent

# Runs each part (Name, Script, Args, Success) in its own pwsh process. Returns @{ Passed; Failed = names; Line }.
function Invoke-Gate([object[]]$Parts) {
    $failed = @()
    foreach ($p in $Parts) {
        Write-Host "rollback gate: running $($p.Name) — $([IO.Path]::GetFileName($p.Script)) $($p.Args -join ' ')"
        $out = @(& pwsh -NoProfile -File $p.Script @($p.Args) 2>&1 | ForEach-Object { "$_" })
        $code = $LASTEXITCODE
        $out | ForEach-Object { Write-Host "    $_" }
        $hasLine = [bool]($out | Where-Object { $_.Trim() -eq $p.Success })
        if ($code -ne 0 -or -not $hasLine) {
            $failed += $p.Name
            $why = if ($hasLine) { "exit $code" } else { "exit $code, no '$($p.Success)' line" }
            Write-Host "rollback gate: $($p.Name) failed ($why)"
        }
    }
    $passed = $Parts.Count - $failed.Count
    $line = if ($failed) { "rollback gate: $passed/$($Parts.Count), failed: $($failed -join ', ')" } else { "rollback gate: $passed/$($Parts.Count)" }
    return @{ Passed = $passed; Failed = $failed; Line = $line }
}

if ($SelfTest) {
    $scratch = Join-Path $env:TEMP "nyar-rollback-gatetest-$([guid]::NewGuid().ToString('N'))"
    $ok = 0
    try {
        New-Item -ItemType Directory -Path $scratch | Out-Null
        # A stub prints the given line and exits with the given code.
        $stub = Join-Path $scratch 'stub.ps1'
        Set-Content -LiteralPath $stub -Value 'param([string]$Line, [int]$Code) if ($Line) { Write-Host $Line }; exit $Code'
        function Stubs([hashtable]$Change) {
            foreach ($n in 'repo', 'drill', 'snapshot', 'routes') {
                $success = @{ repo = 'rollback: clean'; drill = 'rollback drill: pass'; snapshot = 'snapshot selftest: 6/6'; routes = 'rollback routes: stub-plan 5/5' }[$n]
                $line = $success; $code = 0
                if ($Change.ContainsKey($n)) { $line = $Change[$n].Line; $code = $Change[$n].Code }
                @{ Name = $n; Script = $stub; Args = @('-Line', "$line", '-Code', "$code"); Success = $success }
            }
        }
        $cases = @(
            @{ Name = 'all pass'; Change = @{}; Want = 'rollback gate: 4/4' },
            @{ Name = 'repo fails'; Change = @{ repo = @{ Line = 'rollback: fail — git revert: fatal'; Code = 1 } }; Want = 'rollback gate: 3/4, failed: repo' },
            @{ Name = 'drill fails'; Change = @{ drill = @{ Line = 'rollback drill: fail — boot'; Code = 1 } }; Want = 'rollback gate: 3/4, failed: drill' },
            @{ Name = 'snapshot fails'; Change = @{ snapshot = @{ Line = 'snapshot selftest: 5/6'; Code = 1 } }; Want = 'rollback gate: 3/4, failed: snapshot' },
            @{ Name = 'routes fail alone'; Change = @{ routes = @{ Line = 'rollback routes: stub-plan 4/5, failed: On a server missing'; Code = 1 } }; Want = 'rollback gate: 3/4, failed: routes' },
            @{ Name = 'exit 0 without its line'; Change = @{ drill = @{ Line = 'something else'; Code = 0 } }; Want = 'rollback gate: 3/4, failed: drill' })
        foreach ($c in $cases) {
            $r = Invoke-Gate @(Stubs $c.Change) 6>$null
            if ($r.Line -eq $c.Want) { $ok++ } else { Write-Host "  - $($c.Name): expected '$($c.Want)', got '$($r.Line)'" }
        }
    } finally { Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue }
    Write-Host "rollback gate selftest: $ok/6"
    exit ([int]($ok -ne 6))
}

if (-not $From -or -not $To) { Write-Host 'usage: rollback-gate.ps1 -From <older tag> -To <newer tag> [-Plan <slug>] | -SelfTest'; exit 2 }
$n = if ($Plan) { 4 } else { 3 }
$releases = @(git -C $Repo tag -l 'v[0-9]*')
if ($releases.Count -lt 2) { Write-Host "rollback gate: 0/$n, failed: needs two releases"; exit 1 }
# Before the push the newer tag is not on origin, and the older release's preflight fails on exactly that
# (faction-empowerment A9): the repository drill then runs with -BeforePush, which accepts that one failure only.
$repoArgs = @('-From', $From, '-To', $To)
if (-not (git -C $Repo ls-remote --tags origin "refs/tags/$To")) { $repoArgs += '-BeforePush'; Write-Host "rollback gate: $To is not on origin yet; the repository drill runs with -BeforePush" }
$parts = @(
    @{ Name = 'repo'; Script = (Join-Path $PSScriptRoot 'repo-rollback-drill.ps1'); Args = $repoArgs; Success = 'rollback: clean' },
    @{ Name = 'drill'; Script = (Join-Path $PSScriptRoot 'rollback-drill.ps1'); Args = @('-From', $To, '-To', $From); Success = 'rollback drill: pass' },
    @{ Name = 'snapshot'; Script = (Join-Path $PSScriptRoot 'dev-snapshot.ps1'); Args = @('-SelfTest'); Success = 'snapshot selftest: 6/6' })
if ($Plan) {
    $parts += @{ Name = 'routes'; Script = (Join-Path $PSScriptRoot 'preflight.ps1'); Args = @('-RollbackOf', $Plan, '-From', $From, '-To', $To); Success = "rollback routes: $Plan 5/5" }
}
$r = Invoke-Gate $parts
Write-Host $r.Line
exit ([int]($r.Failed.Count -gt 0))
