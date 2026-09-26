<#
.SYNOPSIS
    Dev-server snapshot (faction-empowerment D28): wraps every in-game session so BepInEx/plugins and BepInEx/config
    return to their pre-session state, hash-verified, with anything added during the session removed.

.DESCRIPTION
    pwsh tools/dev-snapshot.ps1 -Save <label>
      Refuses while a VRisingServer process runs, and while a %TEMP%\nyar-snap-* folder holding saved\manifest.json
      exists (a session not yet restored, or a restore that did not match), printing that snapshot's recorded restore.
      A nyar-snap-* folder without a manifest is a partial snapshot (the save was interrupted before the server was
      touched) and is removed. Then copies the whole BepInEx/plugins and BepInEx/config trees, hidden files included,
      into %TEMP%\nyar-snap-<label>\saved\ and writes saved\manifest.json (every file's relative path and SHA-256)
      through a .tmp and a move, last. An absent tree is recorded as absent.
      → "snapshot saved: <label> (<n> files) at <folder>"

    pwsh tools/dev-snapshot.ps1 -Restore
      Refuses while a VRisingServer process runs. Makes each tree equal to the one held snapshot (files added since,
      such as another mod's DLL and cfg, are removed; changed and removed files are put back; a tree absent at the save
      is removed), verifies every hash against the manifest and that no unlisted file remains, and deletes the folder
      only when all match. It first checks the saved copies against the manifest and, when one differs, stops before
      touching the live trees ("saved copy of BepInEx\<tree> differs from its manifest; live trees untouched"). → "snapshot restored; hashes equal", else "snapshot restore: fail — …" (folder kept), exit 1.

    pwsh tools/dev-snapshot.ps1 -SelfTest
      Six cases on a scratch install under %TEMP%\nyar-snaptest-<guid> (removed when done): a save and restore round
      trip with a hidden file, where a DLL and a cfg added after the save are removed by the restore; a leftover
      manifest refused (and not overwritten), and a restore refused while two snapshots are held or for another server
      path; a partial snapshot without its manifest removed; a restore whose copy is corrupted (a changed file, or an
      empty saved directory replaced by a file) keeps the folder and leaves the live trees unchanged; an absent config folder (and an empty directory) restored as saved; a running
      server refuses both the save and the restore. Every refusal also checks the live trees and the held snapshot are
      unchanged, and each case evaluates all its assertions before it reports.
      → "snapshot selftest: 6/6" (no scratch install → "snapshot selftest: 0/6", a failure).

    -ServerDir defaults to the dev server the other tools use.
#>
[CmdletBinding(DefaultParameterSetName = 'Save')]
param(
    [Parameter(ParameterSetName = 'Save', Mandatory)][string]$Save,
    [Parameter(ParameterSetName = 'Restore', Mandatory)][switch]$Restore,
    [Parameter(ParameterSetName = 'SelfTest', Mandatory)][switch]$SelfTest,
    [string]$ServerDir = 'C:\Program Files (x86)\Steam\steamapps\common\VRisingDedicatedServer'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'snapshot-lib.ps1')

$script:DefaultServer = 'C:\Program Files (x86)\Steam\steamapps\common\VRisingDedicatedServer'   # the param default
$SnapFilter = 'nyar-snap-*'
$Trees = @('plugins', 'config')   # under <server>\BepInEx; each is saved as saved\<name>

function Test-ServerRunning { [bool](Get-Process VRisingServer -ErrorAction SilentlyContinue) }

function Get-SnapEntries([string]$Server) {
    foreach ($t in $Trees) { @{ Name = $t; Path = Join-Path $Server "BepInEx\$t"; Kind = 'Dir' } }
}

# The restore a held snapshot records, for a refusal.
function Get-RecordedRestore($Folder) {
    $m = try { Get-Content -LiteralPath (Join-Path $Folder.FullName 'saved\manifest.json') -Raw | ConvertFrom-Json } catch { $null }
    if (-not $m) { return "$($Folder.FullName): unreadable manifest; restore BepInEx\plugins and BepInEx\config by hand from saved\" }
    "$($Folder.FullName) (label $($m.label), saved $($m.saved)): $($m.restore)"
}

# -Save. Returns @{ Ok; Message }.
function Invoke-Save([string]$Label, [string]$Server, [string]$TempRoot, [scriptblock]$IsRunning) {
    if ($Label -notmatch '^[A-Za-z0-9_-]{1,32}$') { return @{ Ok = $false; Message = "snapshot save: refused — label '$Label' must be 1-32 letters, digits, - or _" } }
    if (& $IsRunning) { return @{ Ok = $false; Message = 'snapshot save: refused — a VRisingServer process is running; stop it first' } }
    if (-not (Test-Path -LiteralPath (Join-Path $Server 'BepInEx') -PathType Container)) { return @{ Ok = $false; Message = "snapshot save: refused — no BepInEx folder under $Server" } }
    $held = @(Get-HeldSnapshots $TempRoot $SnapFilter)
    if ($held) {
        return @{ Ok = $false; Message = "snapshot save: refused — an earlier snapshot is not restored: $(@($held | ForEach-Object { Get-RecordedRestore $_ }) -join ' | '); delete the folder by hand only after restoring" }
    }
    $notes = @()
    foreach ($p in @(Get-ChildItem -LiteralPath $TempRoot -Directory -Filter $SnapFilter -Force -ErrorAction SilentlyContinue)) {
        Remove-Item -LiteralPath $p.FullName -Recurse -Force
        $notes += "partial snapshot removed (no manifest): $($p.FullName)"
    }
    $folder = Join-Path $TempRoot "nyar-snap-$Label"
    $restore = "stop the server, then run pwsh tools/dev-snapshot.ps1 -Restore$(if ($Server -ne $script:DefaultServer) { " -ServerDir '$Server'" }); by hand: replace $Server\BepInEx\plugins and $Server\BepInEx\config with saved\plugins and saved\config (a tree the manifest lists as absent is deleted)"
    try {
        $m = Save-Snapshot -Saved (Join-Path $folder 'saved') -Entries @(Get-SnapEntries $Server) `
            -Extra ([ordered]@{ label = $Label; saved = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss'); server = $Server }) -Restore $restore
    }
    catch {
        # No manifest was written, so the live trees are untouched and the partial copy is removed now.
        Remove-Item -LiteralPath $folder -Recurse -Force -ErrorAction SilentlyContinue
        return @{ Ok = $false; Message = "snapshot save: fail — $($_.Exception.Message)" }
    }
    $n = (@($m.entries) | ForEach-Object { @($_.files).Count } | Measure-Object -Sum).Sum
    $absent = @($m.entries | Where-Object { -not $_.present } | ForEach-Object { $_.name })
    $notes += "snapshot saved: $Label ($n files$(if ($absent) { "; absent: $($absent -join ', ')" })) at $folder"
    return @{ Ok = $true; Message = $notes -join "`n"; Folder = $folder }
}

# -Restore. Returns @{ Ok; Message }.
function Invoke-Restore([string]$Server, [string]$TempRoot, [scriptblock]$IsRunning) {
    if (& $IsRunning) { return @{ Ok = $false; Message = 'snapshot restore: refused — a VRisingServer process is running; stop it first' } }
    $held = @(Get-HeldSnapshots $TempRoot $SnapFilter)
    if ($held.Count -eq 0) { return @{ Ok = $false; Message = "snapshot restore: fail — no $SnapFilter folder with saved\manifest.json under $TempRoot" } }
    if ($held.Count -gt 1) { return @{ Ok = $false; Message = "snapshot restore: fail — more than one snapshot is held ($(@($held | ForEach-Object Name) -join ', ')); restore by hand" } }
    $folder = $held[0].FullName
    $m = Get-Content -LiteralPath (Join-Path $folder 'saved\manifest.json') -Raw | ConvertFrom-Json
    $want = @($Trees | ForEach-Object { Join-Path $Server "BepInEx\$_" })
    $have = @($m.entries | ForEach-Object { $_.path })
    if (Compare-Object $want $have) { return @{ Ok = $false; Message = "snapshot restore: fail — $folder was saved from $($m.server), not $Server" } }
    $wrong = @(Restore-Snapshot -Saved (Join-Path $folder 'saved'))
    if ($wrong) {
        return @{ Ok = $false; Message = "snapshot restore: fail — $(@($wrong | ForEach-Object { $w = $_; switch ($w.Reason) { 'present' { "BepInEx\$($w.Name) present (absent at the save)" } 'copy' { "saved copy of BepInEx\$($w.Name) differs from its manifest; live trees untouched" } default { "BepInEx\$($w.Name) differs from the manifest" } } }) -join '; '); the copy is kept at $folder" }
    }
    Remove-Item -LiteralPath $folder -Recurse -Force
    return @{ Ok = $true; Message = "snapshot restored; hashes equal ($($m.label), $folder deleted)" }
}

if ($SelfTest) {
    $total = 6; $ok = 0
    $scratch = Join-Path $env:TEMP "nyar-snaptest-$([guid]::NewGuid().ToString('N'))"
    $notRunning = { $false }
    function Put([string]$Path, [string]$Text) {
        New-Item -ItemType Directory -Path (Split-Path $Path -Parent) -Force | Out-Null
        [IO.File]::WriteAllText($Path, $Text)
    }
    function Tree-State([string]$Server) {
        @($Trees | ForEach-Object { $d = Join-Path $Server "BepInEx\$_"; if (Test-Path -LiteralPath $d) { "$_ present"; Get-FolderHashes $d | ForEach-Object { "$_" } } else { "$_ absent" } })
    }
    function Check([string]$Case, [bool]$Pass, [string]$Detail) {
        if ($Pass) { $script:ok++ } else { Write-Host "  - ${Case}: $Detail" }
    }
    $srv = Join-Path $scratch 'server'; $tmpRoot = Join-Path $scratch 'temp'
    $plugins = Join-Path $srv 'BepInEx\plugins'; $config = Join-Path $srv 'BepInEx\config'
    # A fresh scratch install and an empty scratch %TEMP% for every case, so one failing case does not decide the next.
    function Install {
        foreach ($d in @($srv, $tmpRoot)) { if (Test-Path -LiteralPath $d) { Remove-Item -LiteralPath $d -Recurse -Force } }
        New-Item -ItemType Directory -Path $tmpRoot -Force | Out-Null
        Put (Join-Path $plugins 'Nyarlathotep.dll') 'nyar dll v1'
        Put (Join-Path $plugins 'VampireCommandFramework.dll') 'vcf'
        Put (Join-Path $plugins 'sub\.hidden-state') 'hidden'
        (Get-Item -LiteralPath (Join-Path $plugins 'sub\.hidden-state') -Force).Attributes = 'Hidden'
        Put (Join-Path $plugins '.hidden-top') 'hidden at the top'
        (Get-Item -LiteralPath (Join-Path $plugins '.hidden-top') -Force).Attributes = 'Hidden'
        Put (Join-Path $config 'kdpen.Nyarlathotep.cfg') "[Pillars]`nFactionEmpowerment = false`n"
        Put (Join-Path $config 'Nyarlathotep\events.json') '{"SchemaVersion":1,"events":[]}'
    }
    $installed = try { Install; Test-Path -LiteralPath (Join-Path $plugins 'sub\.hidden-state') } catch { Write-Host "  - scratch install: $($_.Exception.Message)"; $false }
    function Run-Case([string]$Case, [scriptblock]$Body) {
        try { Install; & $Body } catch { Write-Host "  - ${Case}: aborted — $($_.Exception.Message)" }
    }

    if ($installed) {
        Run-Case 'round trip' {
            # A DLL and a cfg added after the save, a changed file and a removed hidden file are all undone.
            $before = Tree-State $srv
            $s = Invoke-Save 's1' $srv $tmpRoot $notRunning
            Put (Join-Path $plugins 'Bloodcraft.dll') 'bloodcraft'
            Put (Join-Path $config 'io.zfolmt.Bloodcraft.cfg') 'bloodcraft cfg'
            Put (Join-Path $config 'Nyarlathotep\events.json') '{"SchemaVersion":1,"events":[{"id":"fe-short"}]}'
            Remove-Item -LiteralPath (Join-Path $plugins 'sub\.hidden-state') -Force
            $r = Invoke-Restore $srv $tmpRoot $notRunning
            $after = Tree-State $srv
            $hidden = Get-Item -LiteralPath (Join-Path $plugins 'sub\.hidden-state') -Force -ErrorAction SilentlyContinue
            Check 'round trip' ($s.Ok -and $r.Ok -and $r.Message -like 'snapshot restored; hashes equal*' -and -not (Compare-Object $before $after) -and
                -not (Test-Path -LiteralPath (Join-Path $plugins 'Bloodcraft.dll')) -and -not (Test-Path -LiteralPath (Join-Path $config 'io.zfolmt.Bloodcraft.cfg')) -and
                $hidden -and -not (Test-Path -LiteralPath $s.Folder)) "save '$($s.Message)', restore '$($r.Message)'"
        }
        Run-Case 'leftover manifest' {
            # A held snapshot is refused (same label or another), printing its restore, and its manifest is not overwritten.
            $a = Invoke-Save 'a' $srv $tmpRoot $notRunning
            $mPath = Join-Path $a.Folder 'saved\manifest.json'
            $mHash = (Get-FileHash -LiteralPath $mPath -Algorithm SHA256).Hash
            $b = Invoke-Save 'a' $srv $tmpRoot $notRunning
            $c = Invoke-Save 'b' $srv $tmpRoot $notRunning
            # The restore refuses a snapshot saved from another server path, and refuses while two snapshots are held;
            # neither refusal changes the live trees or the held snapshots.
            Put (Join-Path $plugins 'Bloodcraft.dll') 'added after the save'
            $live = Tree-State $srv
            $other = Invoke-Restore (Join-Path $scratch 'other-server') $tmpRoot $notRunning
            Copy-Item -LiteralPath $a.Folder -Destination (Join-Path $tmpRoot 'nyar-snap-z') -Recurse
            $two = Invoke-Restore $srv $tmpRoot $notRunning
            $unchanged = -not (Compare-Object $live (Tree-State $srv)) -and (Get-FileHash -LiteralPath $mPath -Algorithm SHA256).Hash -eq $mHash -and
                (Test-Path -LiteralPath (Join-Path $tmpRoot 'nyar-snap-z\saved\manifest.json'))
            Check 'leftover manifest' ($a.Ok -and -not $b.Ok -and -not $c.Ok -and $c.Message -match 'not restored' -and $c.Message -match '-Restore' -and
                $c.Message -match [regex]::Escape($a.Folder) -and -not (Test-Path -LiteralPath (Join-Path $tmpRoot 'nyar-snap-b')) -and
                -not $other.Ok -and $other.Message -match 'was saved from' -and -not $two.Ok -and $two.Message -match 'more than one snapshot' -and
                $unchanged) "save '$($c.Message)', other server '$($other.Message)', two held '$($two.Message)', unchanged $unchanged"
        }
        Run-Case 'partial snapshot' {
            # A saved\ without its manifest is removed and the save proceeds.
            Put (Join-Path $tmpRoot 'nyar-snap-crashed\saved\plugins\Nyarlathotep.dll') 'half'
            $p = Invoke-Save 'p' $srv $tmpRoot $notRunning
            $pr = Invoke-Restore $srv $tmpRoot $notRunning
            Check 'partial snapshot' ($p.Ok -and $p.Message -match 'partial snapshot removed' -and -not (Test-Path -LiteralPath (Join-Path $tmpRoot 'nyar-snap-crashed')) -and $pr.Ok) "save '$($p.Message)', restore '$($pr.Message)'"
        }
        Run-Case 'corrupted copy' {
            # A restore whose copy is corrupted reports the difference and keeps the folder.
            # The check runs before any live path is touched, so a file added after the save is still there.
            $d = Invoke-Save 'd' $srv $tmpRoot $notRunning
            Put (Join-Path $d.Folder 'saved\config\Nyarlathotep\events.json') '{"corrupted":true}'
            Put (Join-Path $plugins 'Bloodcraft.dll') 'added after the save'
            $live = Tree-State $srv
            $dr = Invoke-Restore $srv $tmpRoot $notRunning
            $first = $d.Ok -and -not $dr.Ok -and $dr.Message -match 'saved copy of BepInEx\\config differs from its manifest; live trees untouched' -and
                -not (Compare-Object $live (Tree-State $srv)) -and (Test-Path -LiteralPath (Join-Path $d.Folder 'saved\manifest.json'))
            # An empty config saved, then its saved directory replaced by a file: the copy check still refuses first.
            Remove-Item -LiteralPath $d.Folder -Recurse -Force
            Get-ChildItem -LiteralPath $config -Force | Remove-Item -Recurse -Force
            $d2 = Invoke-Save 'd2' $srv $tmpRoot $notRunning
            Remove-Item -LiteralPath (Join-Path $d2.Folder 'saved\config') -Recurse -Force
            Put (Join-Path $d2.Folder 'saved\config') 'not a directory'
            Put (Join-Path $config 'written-by-a-boot.cfg') 'kept'
            $live2 = Tree-State $srv
            $dr2 = Invoke-Restore $srv $tmpRoot $notRunning
            $second = $d2.Ok -and -not $dr2.Ok -and $dr2.Message -match 'saved copy of BepInEx\\config differs from its manifest; live trees untouched' -and
                -not (Compare-Object $live2 (Tree-State $srv))
            Check 'corrupted copy' ($first -and $second) "restore '$($dr.Message)', dir-to-file restore '$($dr2.Message)'"
        }
        Run-Case 'absent config' {
            # A config folder absent at the save is absent after the restore.
            # An empty plugins directory removed during the session comes back too.
            Remove-Item -LiteralPath $config -Recurse -Force
            New-Item -ItemType Directory -Path (Join-Path $plugins 'empty') | Out-Null
            $e = Invoke-Save 'e' $srv $tmpRoot $notRunning
            Put (Join-Path $config 'kdpen.Nyarlathotep.cfg') 'written by a boot'
            Remove-Item -LiteralPath (Join-Path $plugins 'empty') -Force
            $er = Invoke-Restore $srv $tmpRoot $notRunning
            Check 'absent config' ($e.Ok -and $e.Message -match 'absent: config' -and $er.Ok -and -not (Test-Path -LiteralPath $config) -and
                (Test-Path -LiteralPath (Join-Path $plugins 'Nyarlathotep.dll')) -and (Test-Path -LiteralPath (Join-Path $plugins 'empty') -PathType Container)) "save '$($e.Message)', restore '$($er.Message)'"
        }
        Run-Case 'running server' {
            # A running server refuses the save (nothing is written) and the restore (the live trees and the held snapshot
            # are unchanged).
            $f = Invoke-Save 'f' $srv $tmpRoot { $true }
            $nothing = -not @(Get-ChildItem -LiteralPath $tmpRoot -Force)
            $g = Invoke-Save 'g' $srv $tmpRoot $notRunning
            Put (Join-Path $plugins 'Bloodcraft.dll') 'added after the save'
            $live = Tree-State $srv
            $gr = Invoke-Restore $srv $tmpRoot { $true }
            Check 'running server' (-not $f.Ok -and $f.Message -match 'VRisingServer process is running' -and $nothing -and $g.Ok -and
                -not $gr.Ok -and $gr.Message -match 'restore: refused .* VRisingServer process is running' -and -not (Compare-Object $live (Tree-State $srv)) -and
                (Test-Path -LiteralPath (Join-Path $g.Folder 'saved\manifest.json'))) "save '$($f.Message)', restore '$($gr.Message)'"
        }
    }
    Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $scratch) { Write-Host "  - scratch folder not removed: $scratch"; $ok = [math]::Min($ok, $total - 1) }
    Write-Host "snapshot selftest: $ok/$total"
    exit ([int]($ok -ne $total))
}

$result = if ($Restore) { Invoke-Restore $ServerDir $env:TEMP ${function:Test-ServerRunning} } else { Invoke-Save $Save $ServerDir $env:TEMP ${function:Test-ServerRunning} }
Write-Host $result.Message
exit ([int](-not $result.Ok))
