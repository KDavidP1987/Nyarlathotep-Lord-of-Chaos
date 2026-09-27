<#
.SYNOPSIS
    The soak report (event-library D24): reads a soak's server logs in order and says whether the soak passed.

.DESCRIPTION
    pwsh tools/soak-report.ps1 -Log <LogOutput.log paths, oldest first> -Templates <ids> -MinMinutes 240
      -Log and -Templates take comma-separated lists (pwsh -File passes a list as one argument).
      Reads each BepInEx log in the order given (one log per boot: the archived copies, then the last one) and prints
        soak: <m> timing minutes, <s> starts, <e> ends, <c> cancelled by restart, <u> unpaired, <x> unhandled, tick avg max <a> ms, templates <k>/<n>
      then "soak: pass" and exit 0 only when
        - m >= MinMinutes: m counts the "tick timing" lines, one per minute with Debug.TimingLog on;
        - u = 0: every "event <id> started" has a later end of that id ("event <id> ended (", the stop path's
          "event <id> <why>: <n> units queued" or "empower <id>: <n> carriers queued for removal"), a "purge: <n> events
          ended" line (it ends every running event) or a "event <id> cancelled by restart" in the next boot's log (not the same boot, not a later one: an event still
          open two boots on is unpaired); an end or cancel with no open start is unpaired too, and the one second end line
          allowed is "event <id> ended (pillar off)" inside the unbroken run a pillar-off writes (all its stop lines,
          then one echo per event in the same order); an echo out of that order, after any other line, or in another
          boot, is unpaired;
        - x = 0: no stack frame of our assembly ("at Nyarlathotep.<type>.<method>(", as -LogCheck counts them);
        - every timing line's average is under 5 ms (a = the highest average);
        - k = n: each id of -Templates started at least once.
      Otherwise "soak: fail — <reasons>" and exit 1. No log given prints "soak: fail — no log" alone; a given log that
      is missing or empty prints "soak: fail — no log <path>" alone, so a mistyped archive path never drops a boot.
      The logs are read with shared access, so a running server's log can be read.

    pwsh tools/soak-report.ps1 -SelfTest
      Runs the fixtures under tools/soak-report-fixtures/ with -Templates legion-weekend-surge,bandit-ambush and
      -MinMinutes 5, each fixture's logs being its 1.txt, 2.txt, … in order (tools/ holds no .log files): good must
      pass; bad-unpaired, bad-tick, bad-unhandled, bad-short, bad-missing-template, bad-echo and bad-echo-order must
      fail for their planted reason alone; empty (no log) must print exactly "soak: fail — no log". → "soak selftest: 9/9".
#>
[CmdletBinding()]
param(
    [string[]]$Log,
    [string[]]$Templates,
    [int]$MinMinutes = 240,
    [switch]$SelfTest
)
$ErrorActionPreference = 'Stop'

$script:TickLimitMs = 5.0

function Read-SharedText([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    $fs = [IO.File]::Open($Path, 'Open', 'Read', 'ReadWrite')
    try { return [IO.StreamReader]::new($fs).ReadToEnd() } finally { $fs.Dispose() }
}

# The report's lines for the given logs (oldest first), each @{ Name; Text } with Text $null for a missing file.
# Returns @{ Lines; Pass }.
function Get-SoakReport([object[]]$Logs, [string[]]$Ids, [int]$Min) {
    $logs = @($Logs | Where-Object { $_ })
    if ($logs.Count -eq 0) { return @{ Lines = @('soak: fail — no log'); Pass = $false } }
    $gone = @($logs | Where-Object { $null -eq $_.Text -or $_.Text -notmatch '\S' } | ForEach-Object { $_.Name })
    if ($gone) { return @{ Lines = @("soak: fail — no log $($gone -join ', ')"); Pass = $false } }
    $texts = @($logs | ForEach-Object { $_.Text })
    $inv = [Globalization.CultureInfo]::InvariantCulture
    $m = 0; $s = 0; $e = 0; $c = 0; $u = 0; $x = 0; $avgMax = 0.0; $slow = 0
    # $open[id]: the boot (log index) of the start still open; $echo: the ids of pillar-off stop lines, in order, whose own
    # "event <id> ended (pillar off)" line may follow once, as the same end, only as the head of that queue. `.nyar pillar <name> off` writes one
    # unbroken run: every stop line first (EventRuntime.EndPillar), then every echo in the same order (Logic/Pillars.cs);
    # any other line, or a new boot, ends the run and drops the echoes still pending.
    $open = @{}; $started = @{}; $echo = [Collections.Generic.List[string]]::new()
    for ($bi = 0; $bi -lt $texts.Count; $bi++) {
        $text = $texts[$bi]
        $echo.Clear()
        if ($bi -gt 0) {
            # an event still open from two boots back was never cancelled by the next boot
            foreach ($id in @($open.Keys | Where-Object { $null -ne $open[$_] -and $open[$_] -lt $bi - 1 })) { $u++; $open[$id] = $null }
        }
        foreach ($line in ($text -split '\r?\n')) {
            $inRun = $line -match '\[nyar\] (event \S+ ended \(pillar off\): \d+ units queued, \d+ spawns cancelled|empower \S+: \d+ carriers queued for removal \(ended \(pillar off\)\))' -or
                ($line -match '\[nyar\] event (\S+) ended \(pillar off\)$' -and $echo.Count -gt 0 -and $echo[0] -eq $Matches[1])
            if (-not $inRun) { $echo.Clear() }
            if ($line -match '(?<![\w.])at\s+Nyarlathotep\.[\w.`+<>\[\],]*[ \t]*\(') { $x++; continue }
            if ($line -notmatch '\[nyar\] (.*)$') { continue }
            $msg = $Matches[1]
            if ($msg -match '^tick timing: avg ([\d.]+) ms, max [\d.]+ ms over \d+ ticks') {
                $m++
                $a = [double]::Parse($Matches[1], $inv)
                if ($a -gt $avgMax) { $avgMax = $a }
                if ($a -ge $script:TickLimitMs) { $slow++ }
                continue
            }
            if ($msg -match '^event (\S+) started by ') {
                $id = $Matches[1]; $s++; $started[$id] = $true
                if ($null -ne $open[$id]) { $u++ }   # a second start before the first ended
                $open[$id] = $bi; [void]$echo.Remove($id)
                continue
            }
            if ($msg -match '^purge: \d+ events ended') {
                foreach ($id in @($open.Keys | Where-Object { $null -ne $open[$_] })) { $open[$id] = $null; $e++ }
                continue
            }
            if ($msg -match '^event (\S+) ended \(pillar off\)$' -and $echo.Count -gt 0 -and $echo[0] -eq $Matches[1]) { $echo.RemoveAt(0); continue }   # the same end's second line, in stop order
            $endId = $null; $cancel = $false; $pillar = $false
            if ($msg -match '^event (\S+) cancelled by restart') { $endId = $Matches[1]; $cancel = $true }
            elseif ($msg -match '^event (\S+) ([^:]+): \d+ units queued, \d+ spawns cancelled') { $endId = $Matches[1]; $pillar = $Matches[2] -eq 'ended (pillar off)' }
            elseif ($msg -match '^empower (\S+): \d+ carriers queued for removal \((.*)\)$') { $endId = $Matches[1]; $pillar = $Matches[2] -eq 'ended (pillar off)' }
            elseif ($msg -match '^event (\S+) ended \(') { $endId = $Matches[1] }
            if ($null -eq $endId) { continue }
            if ($cancel) { $c++ } else { $e++ }
            # a cancel pairs only with a start of the boot before; an end only with a start still open
            $pairs = if ($cancel) { $null -ne $open[$endId] -and $open[$endId] -eq $bi - 1 } else { $null -ne $open[$endId] }
            if ($pairs) { $open[$endId] = $null; if ($pillar) { $echo.Add($endId) } } else { $u++ }
        }
    }
    $u += @($open.Keys | Where-Object { $null -ne $open[$_] }).Count
    $ids = @($Ids | Where-Object { $_ })
    $k = @($ids | Where-Object { $started[$_] }).Count
    $summary = 'soak: {0} timing minutes, {1} starts, {2} ends, {3} cancelled by restart, {4} unpaired, {5} unhandled, tick avg max {6} ms, templates {7}/{8}' -f `
        $m, $s, $e, $c, $u, $x, $avgMax.ToString('0.000', $inv), $k, $ids.Count
    $why = @()
    if ($m -lt $Min) { $why += "$m of $Min timing minutes" }
    if ($u -gt 0) { $why += "$u unpaired" }
    if ($x -gt 0) { $why += "$x unhandled" }
    if ($slow -gt 0) { $why += "$slow timing lines at or over $($script:TickLimitMs) ms" }
    if ($ids.Count -eq 0) { $why += 'no templates given' }
    elseif ($k -lt $ids.Count) { $why += "never started: $(@($ids | Where-Object { -not $started[$_] }) -join ', ')" }
    $verdict = if ($why) { "soak: fail — $($why -join '; ')" } else { 'soak: pass' }
    return @{ Lines = @($summary, $verdict); Pass = -not $why }
}

if ($SelfTest) {
    $root = Join-Path $PSScriptRoot 'soak-report-fixtures'
    $ids = @('legion-weekend-surge', 'bandit-ambush')
    $ok = 0
    foreach ($name in 'good', 'bad-unpaired', 'bad-tick', 'bad-unhandled', 'bad-short', 'bad-missing-template', 'bad-echo', 'bad-echo-order', 'empty') {
        $dir = Join-Path $root $name
        $files = @(Get-ChildItem -LiteralPath $dir -Filter '*.txt' -File -ErrorAction SilentlyContinue | Where-Object Name -Match '^\d+\.txt$' | Sort-Object Name)
        $r = Get-SoakReport @($files | ForEach-Object { @{ Name = $_.Name; Text = (Read-SharedText $_.FullName) } }) $ids 5
        $printedPass = $r.Lines -contains 'soak: pass'
        # each bad fixture fails for its planted reason alone (the verdict names only that reason)
        $reason = @{ 'bad-unpaired' = '^soak: fail — \d+ unpaired$'; 'bad-tick' = '^soak: fail — \d+ timing lines at or over 5 ms$'
            'bad-unhandled' = '^soak: fail — \d+ unhandled$'; 'bad-short' = '^soak: fail — \d+ of \d+ timing minutes$'
            'bad-missing-template' = '^soak: fail — never started: bandit-ambush$'; 'bad-echo' = '^soak: fail — 2 unpaired$'; 'bad-echo-order' = '^soak: fail — 2 unpaired$' }[$name]
        $good = switch ($name) {
            'good' { $printedPass -and $r.Pass }
            'empty' { $r.Lines.Count -eq 1 -and $r.Lines[0] -eq 'soak: fail — no log' -and $files.Count -eq 0 -and (Test-Path -LiteralPath $dir) }
            default { $files.Count -gt 0 -and -not $printedPass -and -not $r.Pass -and $r.Lines[-1] -match $reason }
        }
        if ($good) { $ok++ } else { Write-Host "  - $name`: $($r.Lines -join ' / ')" }
    }
    Write-Host "soak selftest: $ok/9"
    exit ([int]($ok -ne 9))
}

if (-not $Log) { Write-Host 'soak: fail — no log'; exit 1 }
$r = Get-SoakReport @($Log | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ } | ForEach-Object { @{ Name = $_; Text = (Read-SharedText $_) } }) @($Templates | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() }) $MinMinutes
$r.Lines | ForEach-Object { Write-Host $_ }
exit ([int](-not $r.Pass))
