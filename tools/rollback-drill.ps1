<#
.SYNOPSIS
    Rollback drill (Epic D32, raphael-api-core D16): release N-1 must start on the files release N wrote.

.DESCRIPTION
    pwsh tools/rollback-drill.ps1 -From v0.3.0 -To v0.2.1
      1. Refuses while any VRisingServer process runs. Refuses while a leftover %TEMP%\nyar-drill-* folder holds a
         saved\ copy (a crashed run, or a restore that did not match): that copy is the only one of the dev server's
         DLL and config, so the admin restores it by hand first (raphael-api-core A11). Other leftovers are removed.
      2. Checks that -To is an ancestor of -From and that `git diff --name-only <To>..<From>` lists only paths a
         tracked glob of tools/paths-manifest.txt covers.
      3. Builds each tag's DLL in a disposable worktree under %TEMP%\nyar-drill-<guid>.
      4. Saves the dev server's plugin DLL and BepInEx/config/Nyarlathotep/, then empties that folder so N writes
         its own files (raphael-api-core A9): installs N and boots the dev world (save-data-nyardev) until "Nyarlathotep
         initialized" (N seeds events.json); stops; renames one event and adds drill-mark (1 unit at a Point, Schedule
         a few minutes out), because the mod writes state.json only on a change; boots N until drill-mark fires and
         state.json lists its unit; stops.
      5. Installs N-1, boots again and checks its log: "Nyarlathotep initialized"; for each file N wrote its load
         line with no "SchemaVersion … is newer … read-only" warning (events.json: "events: reloaded: <v> valid, <x>
         disabled", the same counts N logged; state.json: "(<n> listed in state.json)" with n ≥ 1; stats.json:
         reported absent while no release writes it); and "marker sweep".
      After each boot it runs `preflight.ps1 -LogCheck` on that boot's logs and prints the "log check:" line.
      6. Always (finally): stops the server if the drill started one (a refusal never stops a running server), puts
         back exactly what was there (the DLL or its absence, the config folder or its absence, hidden files
         included), checks the DLL's and the config's hashes against the saved ones, and removes the worktrees and the
         temp folder; on a mismatch the temp folder and its saved copy are kept (A13, A14).
    Prints "rollback drill: pass", or "rollback drill: fail — <stage>" and exits 1. It never prints pass on a
    missing log line. Dev world only: the owner's world is never touched.

    pwsh tools/rollback-drill.ps1 -SelfTest
    Runs the N-1 log check over tools/rollback-drill-fixtures/{good,bad,empty}/LogOutput.txt (a captured BepInEx LogOutput.log): good (a real boot log)
    must pass, bad (the same log without "marker sweep") and empty ("no log") must fail; then the leftover check over
    two scratch folders: one holding saved\ must be refused, one without it must not → "drill selftest: 5/5".
#>
[CmdletBinding()]
param(
    [string]$From,
    [string]$To,
    [switch]$SelfTest,
    [int]$BootTimeoutSeconds = 300
)
$ErrorActionPreference = 'Stop'
$Repo = Split-Path $PSScriptRoot -Parent
$ServerDir = 'C:\Program Files (x86)\Steam\steamapps\common\VRisingDedicatedServer'
$PluginDll = Join-Path $ServerDir 'BepInEx\plugins\Nyarlathotep.dll'
$ConfigDir = Join-Path $ServerDir 'BepInEx\config\Nyarlathotep'
$BepLog = Join-Path $ServerDir 'BepInEx\LogOutput.log'

class DrillFailure : System.Exception { DrillFailure([string]$m) : base($m) {} }
function Fail([string]$stage) { throw [DrillFailure]::new($stage) }

# The N-1 boot checks. Returns $null when the log passes, else the failing stage.
function Test-RollbackLog([string]$Text, [int]$MinListed = 0) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return 'no log' }
    if ($Text -notmatch 'Nyarlathotep initialized') { return 'N-1 did not log "Nyarlathotep initialized"' }
    if ($Text -notmatch '\[nyar\] events: reloaded: \d+ valid, \d+ disabled') { return 'no events.json load line' }
    if ($Text -match 'events\.json SchemaVersion \d+ is newer') { return 'events.json loaded read-only (newer schema)' }
    $listed = [regex]::Match($Text, '\((\d+) listed in state\.json\)')
    if (-not $listed.Success) { return 'no state.json load line' }
    if ([int]$listed.Groups[1].Value -lt $MinListed) { return "state.json listed $($listed.Groups[1].Value) units, expected at least $MinListed" }
    if ($Text -match 'state\.json SchemaVersion \d+ is newer') { return 'state.json loaded read-only (newer schema)' }
    if ($Text -notmatch 'marker sweep') { return 'no "marker sweep" line' }
    if ($Text -match '(?m)^\[(Error|Fatal)\s*:\s*Nyarlathotep\]') { return 'N-1 logged an error' }
    return $null
}

# The leftover check. Returns $null when no nyar-drill-* folder under $Root holds a saved\ copy, else the refusal.
function Get-LeftoverRefusal([string]$Root) {
    $held = @(Get-ChildItem -LiteralPath $Root -Directory -Filter 'nyar-drill-*' -ErrorAction SilentlyContinue |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'saved') })
    if (-not $held) { return $null }
    "an earlier drill left the saved plugin DLL and config in $(($held.FullName) -join ', '); copy saved\Nyarlathotep.dll to BepInEx\plugins and saved\config\* to BepInEx\config\Nyarlathotep, then delete the folder"
}

function Read-Shared([string]$Path) {
    try {
        $fs = [IO.File]::Open($Path, 'Open', 'Read', 'ReadWrite,Delete')
        try { return [IO.StreamReader]::new($fs).ReadToEnd() } finally { $fs.Dispose() }
    } catch { return '' }
}

if ($SelfTest) {
    $fx = Join-Path $PSScriptRoot 'rollback-drill-fixtures'
    # Expected result per fixture: $null = pass, otherwise the failing stage.
    $want = [ordered]@{ good = $null; bad = 'no "marker sweep" line'; empty = 'no log' }
    $ok = 0
    foreach ($k in $want.Keys) {
        $p = Join-Path $fx "$k\LogOutput.txt"
        $text = if (Test-Path -LiteralPath $p) { Get-Content -LiteralPath $p -Raw } else { $null }
        $why = Test-RollbackLog $text
        if ($why -eq $want[$k]) { $ok++ }
        else { Write-Host "  - $k fixture: expected $(if ($want[$k]) { "fail — $($want[$k])" } else { 'pass' }), got $(if ($why) { "fail — $why" } else { 'pass' })" }
    }
    # The leftover check over scratch folders: a leftover holding saved\ is refused, one without it is not.
    $scratch = Join-Path $env:TEMP "nyar-drilltest-$([guid]::NewGuid().ToString('N'))"
    try {
        New-Item -ItemType Directory -Path (Join-Path $scratch 'nyar-drill-plain') | Out-Null
        if (-not (Get-LeftoverRefusal $scratch)) { $ok++ } else { Write-Host '  - leftover without saved\: expected no refusal' }
        New-Item -ItemType Directory -Path (Join-Path $scratch 'nyar-drill-crashed\saved\config') | Out-Null
        if (Get-LeftoverRefusal $scratch) { $ok++ } else { Write-Host '  - leftover holding saved\: expected a refusal' }
    } finally { Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue }
    $total = $want.Count + 2
    Write-Host "drill selftest: $ok/$total"
    exit ([int]($ok -ne $total))
}

if (-not $From -or -not $To) { Write-Host 'usage: rollback-drill.ps1 -From <tag N> -To <tag N-1> | -SelfTest'; exit 2 }

# Manifest tracked globs as regexes (** any depth, * one segment, ? one character).
function Get-TrackedPatterns {
    Get-Content -LiteralPath (Join-Path $PSScriptRoot 'paths-manifest.txt') | Where-Object { $_ -match '^tracked:\s*(.+)$' } | ForEach-Object {
        $g = $Matches[1].Trim()
        $rx = [regex]::Escape($g) -replace '\\\*\\\*/', '(?:.*/)?' -replace '\\\*\\\*', '.*' -replace '\\\*', '[^/]*' -replace '\\\?', '[^/]'
        "^$rx$"
    }
}

function Stop-Server {
    Get-Process VRisingServer -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    $deadline = (Get-Date).AddSeconds(60)
    while ((Get-Process VRisingServer -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 1 }
}

# Boots the dev world and returns its BepInEx log once "Nyarlathotep initialized" appears (after $ExtraSeconds more).
function Invoke-Boot([string]$Stage, [int]$ExtraSeconds) {
    $started = Get-Date
    $script:booted = $true   # from here on the finally stops the server (A14: a refusal never stops one it did not start)
    $env:SteamAppId = '1604030'
    Start-Process -FilePath (Join-Path $ServerDir 'VRisingServer.exe') -WorkingDirectory $ServerDir -ArgumentList '-persistentDataPath .\save-data-nyardev -serverName "Nyar Dev" -saveName nyardev -logFile .\logs\NyarDev.log' | Out-Null
    $deadline = $started.AddSeconds($BootTimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 2
        if (-not (Get-Process VRisingServer -ErrorAction SilentlyContinue)) { Fail "$Stage — the server exited during boot" }
        if ((Test-Path -LiteralPath $BepLog) -and (Get-Item -LiteralPath $BepLog).LastWriteTime -gt $started -and (Read-Shared $BepLog) -match 'Nyarlathotep initialized') {
            Start-Sleep -Seconds $ExtraSeconds
            return Read-Shared $BepLog
        }
    }
    Fail "$Stage — no ""Nyarlathotep initialized"" within $BootTimeoutSeconds s"
}

# preflight -LogCheck on the logs of the boot that just stopped (every boot is a server session, raphael-api-core D20).
function Invoke-LogCheck([string]$Stage) {
    $out = @(pwsh -NoProfile -File (Join-Path $PSScriptRoot 'preflight.ps1') -LogCheck 2>&1 | ForEach-Object { "$_" })
    $line = $out | Where-Object { $_ -match '^log check:' } | Select-Object -Last 1
    Write-Host "$Stage $line"
    if ($LASTEXITCODE -or -not $line) { Fail "$Stage — preflight -LogCheck failed: $(($out | Where-Object { $_ -match 'FAIL|log check' }) -join ' ')" }
}

function Get-FolderHashes([string]$Dir) {
    if (-not (Test-Path -LiteralPath $Dir)) { return @() }
    @(Get-ChildItem -LiteralPath $Dir -File -Recurse -Force | Sort-Object FullName | ForEach-Object {
        "$($_.FullName.Substring($Dir.Length)) $((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)"
    })
}

$tmp = $null; $saved = $false; $booted = $false; $hadDll = $true; $hadConfig = $true; $dllHash = $null; $result = $null
try {
    if (Get-Process VRisingServer -ErrorAction SilentlyContinue) { Fail 'a VRisingServer process is running; stop it first' }
    $refusal = Get-LeftoverRefusal $env:TEMP
    if ($refusal) { Fail $refusal }
    foreach ($old in @(Get-ChildItem -LiteralPath $env:TEMP -Directory -Filter 'nyar-drill-*' -ErrorAction SilentlyContinue)) {
        Write-Host "leftover from an earlier run removed: $($old.FullName)"
        Remove-Item -LiteralPath $old.FullName -Recurse -Force
    }
    git -C $Repo worktree prune

    # Tags: ancestor order and the paths of the range.
    foreach ($t in @($From, $To)) { git -C $Repo rev-parse --verify --quiet "$t^{commit}" | Out-Null; if ($LASTEXITCODE) { Fail "tag $t not found" } }
    git -C $Repo merge-base --is-ancestor $To $From
    if ($LASTEXITCODE) { Fail "$To is not an ancestor of $From" }
    $patterns = @(Get-TrackedPatterns)
    $changed = @(git -C $Repo diff --name-only "$To..$From")
    $unlisted = @($changed | Where-Object { $p = $_; -not ($patterns | Where-Object { $p -match $_ }) })
    if ($unlisted) { Fail "the range $To..$From touches paths outside tools/paths-manifest.txt: $($unlisted -join ', ')" }
    Write-Host "range $To..${From}: $($changed.Count) paths, all in the manifest; $To is an ancestor of $From"

    # Build both DLLs in disposable worktrees.
    $tmp = Join-Path $env:TEMP "nyar-drill-$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $tmp | Out-Null
    $dll = @{}
    foreach ($t in @($From, $To)) {
        $wt = Join-Path $tmp "wt-$($t -replace '[^\w.-]', '_')"
        git -C $Repo worktree add --detach $wt $t 2>&1 | Out-Null
        if ($LASTEXITCODE) { Fail "worktree for $t" }
        $out = dotnet build (Join-Path $wt 'Nyarlathotep\Nyarlathotep.sln') -c Release '-p:VRisingServerPath=C:\__nodeploy__' 2>&1
        if ($LASTEXITCODE) { Fail "build of $t — $(($out | Select-Object -Last 3) -join ' ')" }
        $dll[$t] = Join-Path $wt 'Nyarlathotep\Nyarlathotep\bin\Release\net6.0\Nyarlathotep.dll'
        if (-not (Test-Path -LiteralPath $dll[$t])) { Fail "build of $t produced no DLL" }
        Write-Host "built $t"
    }

    # Save the dev server's DLL and config; N starts from an empty config folder so the files are N's own.
    $save = Join-Path $tmp 'saved'
    New-Item -ItemType Directory -Path (Join-Path $save 'config') -Force | Out-Null
    # Hidden files included (-Force); a server without the plugin gets none back (raphael-api-core A13).
    $hadDll = Test-Path -LiteralPath $PluginDll
    if ($hadDll) {
        Copy-Item -LiteralPath $PluginDll -Destination (Join-Path $save 'Nyarlathotep.dll')
        $dllHash = (Get-FileHash -LiteralPath $PluginDll -Algorithm SHA256).Hash
    }
    $hadConfig = Test-Path -LiteralPath $ConfigDir
    if ($hadConfig) { Get-ChildItem -LiteralPath $ConfigDir -Force | Copy-Item -Destination (Join-Path $save 'config') -Recurse -Force }
    $savedHashes = Get-FolderHashes (Join-Path $save 'config')
    $saved = $true
    if (Test-Path -LiteralPath $ConfigDir) { Get-ChildItem -LiteralPath $ConfigDir -Force | Remove-Item -Recurse -Force }

    # Release N, first boot: it seeds events.json.
    Copy-Item -LiteralPath $dll[$From] -Destination $PluginDll -Force
    $logN = Invoke-Boot "boot $From (seed)" 5
    Stop-Server
    Invoke-LogCheck "boot $From (seed):"
    if ($logN -match '(?m)^\[(Error|Fatal)\s*:\s*Nyarlathotep\]') { Fail "boot $From (seed) logged an error" }
    $evPath = Join-Path $ConfigDir 'events.json'
    if (-not (Test-Path -LiteralPath $evPath)) { Fail "boot $From did not seed events.json" }

    # Edit one event, as an admin would, and add drill-mark so N has a change to write to state.json (A9).
    $doc = Get-Content -LiteralPath $evPath -Raw | ConvertFrom-Json
    $doc.events[0].name = 'Renamed by the drill'   # a valid name (1-40 characters): N-1 must accept the edit, not disable it
    # The schedule fires only in its own minute and never replays a missed one, so the minute is set past the longest
    # boot the drill waits for (A13), and a boot that ends after it fails by name.
    $at = (Get-Date).AddMinutes([math]::Ceiling($BootTimeoutSeconds / 60) + 2)
    $mark = [ordered]@{ id = 'drill-mark'; name = 'Rollback drill mark'; enabled = $true; pillar = 'spawns'
        trigger = [ordered]@{ type = 'Schedule'; days = @($at.ToString('ddd', [Globalization.CultureInfo]::InvariantCulture)); times = @($at.ToString('HH:mm')) }
        durationSeconds = 600
        action = [ordered]@{ type = 'SpawnWaves'; units = @([ordered]@{ prefab = 'CHAR_Bandit_Deadeye'; count = 1 }); waves = 1; intervalSeconds = 30; radius = 6
            location = [ordered]@{ type = 'Point'; x = -1200; z = -800 } } }
    $doc.events = @($doc.events) + [pscustomobject]$mark
    [IO.File]::WriteAllText($evPath, ($doc | ConvertTo-Json -Depth 32), [Text.UTF8Encoding]::new($false))
    Write-Host "edited event $($doc.events[0].id): name ""$($doc.events[0].name)""; drill-mark scheduled at $($at.ToString('HH:mm'))"

    # Release N, second boot: drill-mark fires and N writes state.json.
    $null = Invoke-Boot "boot $From (drill-mark)" 0
    if ((Get-Date).ToString('yyyyMMddHHmm') -ge $at.ToString('yyyyMMddHHmm')) { Fail "boot $From (drill-mark) — the boot ended after drill-mark's minute $($at.ToString('HH:mm'))" }
    $statePath = Join-Path $ConfigDir 'state.json'
    $deadline = $at.AddMinutes(3)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 2
        if ((Test-Path -LiteralPath $statePath) -and (Read-Shared $statePath) -match 'drill-mark' -and (Read-Shared $BepLog) -match 'spawn batch: 1 of 1 spawned') { Start-Sleep -Seconds 3; break }
    }
    $logN = Read-Shared $BepLog
    Stop-Server
    Invoke-LogCheck "boot $From (drill-mark):"
    if ($logN -match '(?m)^\[(Error|Fatal)\s*:\s*Nyarlathotep\]') { Fail "boot $From (drill-mark) logged an error" }
    if ($logN -notmatch 'event drill-mark started') { Fail "boot $From — drill-mark did not fire" }
    $written = @('events.json', 'state.json', 'stats.json' | Where-Object { Test-Path -LiteralPath (Join-Path $ConfigDir $_) })
    Write-Host "boot ${From}: drill-mark fired; files: $($written -join ', ')"
    if ($written -notcontains 'state.json') { Fail "boot $From did not write state.json" }
    if ($written -contains 'stats.json') { Fail 'stats.json was written, and the drill has no load line for it yet' }
    Write-Host 'stats.json: absent (no release writes it)'

    # Release N-1 on N's files.
    Copy-Item -LiteralPath $dll[$To] -Destination $PluginDll -Force
    $logN1 = Invoke-Boot "boot $To" 20
    Stop-Server
    Invoke-LogCheck "boot ${To}:"
    $why = Test-RollbackLog $logN1 -MinListed 1
    if ($why) { Fail "boot $To — $why" }
    # N-1 must read the same definitions N read: an event it disables is a rollback that loses content.
    $readN = [regex]::Matches($logN, 'events: reloaded: (\d+ valid, \d+ disabled)') | Select-Object -Last 1
    $readN1 = [regex]::Matches($logN1, 'events: reloaded: (\d+ valid, \d+ disabled)') | Select-Object -Last 1
    if (-not $readN -or $readN.Groups[1].Value -ne $readN1.Groups[1].Value) { Fail "boot $To read events.json as '$($readN1.Groups[1].Value)', $From as '$($readN.Groups[1].Value)'" }
    Write-Host "events.json: $From and $To both read '$($readN1.Groups[1].Value)'"
    Write-Host "boot ${To}: initialized on $From's files; events.json and state.json loaded without a read-only warning; marker sweep logged"
    $result = 'pass'
}
catch [DrillFailure] { $result = "fail — $($_.Exception.Message)" }
catch { $result = "fail — $($_.Exception.Message)" }
finally {
    if ($booted) { Stop-Server }
    if ($saved) {
        # Put back exactly what was there: the DLL or its absence, the config folder or its absence (A13, A14).
        $savedDll = Join-Path $tmp 'saved\Nyarlathotep.dll'
        if ($hadDll) { Copy-Item -LiteralPath $savedDll -Destination $PluginDll -Force -ErrorAction SilentlyContinue }
        else { Remove-Item -LiteralPath $PluginDll -Force -ErrorAction SilentlyContinue }
        if (Test-Path -LiteralPath $ConfigDir) { Get-ChildItem -LiteralPath $ConfigDir -Force | Remove-Item -Recurse -Force }
        if ($hadConfig) {
            if (-not (Test-Path -LiteralPath $ConfigDir)) { New-Item -ItemType Directory -Path $ConfigDir | Out-Null }
            Get-ChildItem -LiteralPath (Join-Path $tmp 'saved\config') -Force -ErrorAction SilentlyContinue | Copy-Item -Destination $ConfigDir -Recurse -Force
        }
        elseif (Test-Path -LiteralPath $ConfigDir) { Remove-Item -LiteralPath $ConfigDir -Recurse -Force }
        $wrong = @()
        if ($hadDll) { if (-not (Test-Path -LiteralPath $PluginDll) -or (Get-FileHash -LiteralPath $PluginDll -Algorithm SHA256).Hash -ne $dllHash) { $wrong += 'plugin DLL' } }
        elseif (Test-Path -LiteralPath $PluginDll) { $wrong += 'plugin DLL (none before the drill)' }
        if ($hadConfig) { if (Compare-Object @($savedHashes) @(Get-FolderHashes $ConfigDir)) { $wrong += 'config' } }
        elseif (Test-Path -LiteralPath $ConfigDir) { $wrong += 'config folder (none before the drill)' }
        if ($wrong) { $result = "fail — the restored $($wrong -join ' and ') differs from the saved state (the copy is kept in the temp folder)"; $tmp = $null }
        else { Write-Host "restored the saved $(if ($hadDll) { 'plugin DLL' } else { 'state (no plugin DLL)' }) and $(if ($hadConfig) { 'config' } else { 'state (no config folder)' }); hashes equal" }
    }
    if ($tmp -and (Test-Path -LiteralPath $tmp)) {
        foreach ($wt in @(Get-ChildItem -LiteralPath $tmp -Directory -Filter 'wt-*' -ErrorAction SilentlyContinue)) { git -C $Repo worktree remove --force $wt.FullName 2>&1 | Out-Null }
        Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
        git -C $Repo worktree prune
    }
}
Write-Host "rollback drill: $result"
exit ([int]($result -ne 'pass'))
