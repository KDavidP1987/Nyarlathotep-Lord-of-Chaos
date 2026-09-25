# In-game session helper (foundation step 5, sessions 12-13). Run in the background after the server's boot line:
#   pwsh -NoProfile -File tools/ingame/session-watch.ps1
# It watches BepInEx/LogOutput.log for admin marker commands (`.nyar event stop go|d23a|d23b|cool|d21`; mode d22 is run by hand before a boot) and rewrites the
# dev server's events.json through session-events.py; after the d21 marker followed by "event t-manual wave 2/2" it stops
# the server right after the next "Finished Saving" (D21 setup). Notes go to
# %TEMP%/nyar-session/watcher.log.
# Dev server only: never point it at the owner's world.
$ErrorActionPreference = 'Continue'
$d = 'C:\Program Files (x86)\Steam\steamapps\common\VRisingDedicatedServer'
$bep = Join-Path $d 'BepInEx\LogOutput.log'; $srv = Join-Path $d 'logs\NyarDev.log'
$here = $PSScriptRoot; $note = Join-Path $env:TEMP 'nyar-session\watcher.log'; New-Item -ItemType Directory -Force (Split-Path $note) | Out-Null
function Note($m) { Add-Content -LiteralPath $note -Value "$(Get-Date -Format HH:mm:ss) $m" }
function ReadShared($p) {
    try { $fs = [IO.File]::Open($p, 'Open', 'Read', 'ReadWrite,Delete'); $sr = [IO.StreamReader]::new($fs); $t = $sr.ReadToEnd(); $sr.Close(); return $t } catch { return '' }
}
function Gen($mode) { $o = & python (Join-Path $here 'session-events.py') $mode 2>&1; Note "gen $mode -> $o" }
$go = $a = $b = $restored = $cool = $armed = $false; $saves = 0
Note 'watcher started'
while ($true) {
    Start-Sleep -Seconds 2
    if (-not (Get-Process VRisingServer -ErrorAction SilentlyContinue)) { Note 'server gone; watcher exits'; exit 0 }
    $t = ReadShared $bep
    if (-not $go -and $t -match 'ran event stop go\b') { Gen 'go'; $go = $true }
    if (-not $a -and $t -match 'ran event stop d23a\b') { Gen 'd23a'; $a = $true }
    if (-not $b -and $t -match 'ran event stop d23b\b') { Gen 'd23b'; $b = $true }
    if ($b -and -not $restored -and $t -match 'ran event stop d23b[\s\S]*events\.json rejected') { Gen 'restore-valid'; $restored = $true }
    if (-not $cool -and $t -match 'ran event stop cool\b') { Gen 'cool'; $cool = $true }
    if (-not $armed) {
        $i = $t.LastIndexOf('ran event stop d21')
        if ($i -ge 0 -and $t.Substring($i) -match 'event t-manual wave 2/2') {
            $armed = $true; $saves = ([regex]::Matches((ReadShared $srv), 'Finished Saving')).Count
            Note "armed for D21: t-manual wave 2/2 after the d21 marker; saves so far $saves"
        }
    } else {
        $now = ([regex]::Matches((ReadShared $srv), 'Finished Saving')).Count
        if ($now -gt $saves) {
            Note "autosave finished ($now); stopping the server for D21"
            Stop-Process -Name VRisingServer -Force
            Note 'server stopped'
            exit 0
        }
    }
}
