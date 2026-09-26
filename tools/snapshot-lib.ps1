<#
.SYNOPSIS
    Snapshot and restore of dev-server files, shared by tools/rollback-drill.ps1 (D23) and tools/dev-snapshot.ps1 (D28).

.DESCRIPTION
    Dot-source it: `. (Join-Path $PSScriptRoot 'snapshot-lib.ps1')`.

    A snapshot is a folder `saved\` holding one copy per entry (a file or a directory, named by the entry's Name) and
    `saved\manifest.json`, written through a .tmp and a move after every copy, so a `saved\` without a manifest is an
    unfinished snapshot and the live files were never touched after it. An entry that was absent at the save is
    recorded as absent, and the restore makes it absent again.

      Save-Snapshot -Saved <dir> -Entries @(@{ Name; Path; Kind = 'File'|'Dir' }) [-Extra <ordered>] [-Restore <text>]
      @(Restore-Snapshot -Saved <dir>) → the entries that differ after the restore (empty when every hash matches)
      Get-FolderHashes <dir>          → "<\relative path> <SHA-256>" per file, hidden files included
      Get-LeftoverRefusal <root>      → the drill's refusal for a complete %TEMP%\nyar-drill-* snapshot, or $null
      Get-HeldSnapshots <root> <filter> → the folders under <root> matching <filter> whose saved\manifest.json exists
#>

# "<\relative path> <SHA-256>" for every file under $Dir, hidden ones included; @() when $Dir is absent.
function Get-FolderHashes([string]$Dir) {
    if (-not (Test-Path -LiteralPath $Dir)) { return @() }
    @(Get-ChildItem -LiteralPath $Dir -File -Recurse -Force | Sort-Object FullName | ForEach-Object {
        "$($_.FullName.Substring($Dir.Length)) $((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)"
    })
}

# The manifest's file list of one entry: { path, sha256 } per file ("" for a File entry), sorted.
function Get-EntryFiles([string]$Path, [string]$Kind) {
    if ($Kind -eq 'File') {
        if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return @() }
        return @([ordered]@{ path = ''; sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash })
    }
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) { return @() }
    @(Get-ChildItem -LiteralPath $Path -File -Recurse -Force | Sort-Object FullName | ForEach-Object {
        [ordered]@{ path = $_.FullName.Substring($Path.Length); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
}

function Format-EntryFiles($Files) { @($Files | ForEach-Object { "$($_.path) $($_.sha256)" }) }

# Copies every entry into $Saved (hidden files included), checks each copy against the live file's hash, then writes
# saved\manifest.json through a .tmp and a move, last. Returns the manifest. Throws when a copy differs.
function Save-Snapshot {
    param(
        [Parameter(Mandatory)][string]$Saved,
        [Parameter(Mandatory)][object[]]$Entries,
        [System.Collections.IDictionary]$Extra,
        [string]$Restore
    )
    New-Item -ItemType Directory -Path $Saved -Force | Out-Null
    $records = foreach ($e in $Entries) {
        $copy = Join-Path $Saved $e.Name
        $present = if ($e.Kind -eq 'File') { Test-Path -LiteralPath $e.Path -PathType Leaf } else { Test-Path -LiteralPath $e.Path -PathType Container }
        $files = @(Get-EntryFiles $e.Path $e.Kind)
        if ($e.Kind -eq 'File') {
            if ($present) { Copy-Item -LiteralPath $e.Path -Destination $copy -Force }
        }
        else {
            New-Item -ItemType Directory -Path $copy -Force | Out-Null
            if ($present) { Get-ChildItem -LiteralPath $e.Path -Force | Copy-Item -Destination $copy -Recurse -Force }
        }
        if ($present -and (Compare-Object @(Format-EntryFiles $files) @(Format-EntryFiles (Get-EntryFiles $copy $e.Kind)))) {
            throw "the saved copy of $($e.Path) differs from the live one"
        }
        [ordered]@{ name = $e.Name; path = $e.Path; kind = $e.Kind; present = $present; files = $files }
    }
    $manifest = [ordered]@{ complete = $true }
    if ($Extra) { foreach ($k in $Extra.Keys) { $manifest[$k] = $Extra[$k] } }
    if ($Restore) { $manifest['restore'] = $Restore }
    $manifest['entries'] = @($records)
    $mTmp = Join-Path $Saved 'manifest.json.tmp'
    [IO.File]::WriteAllText($mTmp, ($manifest | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $mTmp -Destination (Join-Path $Saved 'manifest.json')
    return $manifest
}

# Makes every entry of saved\manifest.json equal to its copy: a present file or directory is replaced by the copy
# (anything added since is removed, changed and removed files are put back), an absent one is removed. Then verifies
# every hash against the manifest and that no unlisted file remains. Returns one object { Name; Path; Reason } per
# entry that differs ('differs' or 'present' for one that was absent); @() when all match.
function Restore-Snapshot {
    param([Parameter(Mandatory)][string]$Saved)
    $m = Get-Content -LiteralPath (Join-Path $Saved 'manifest.json') -Raw | ConvertFrom-Json
    $wrong = @()
    foreach ($e in @($m.entries)) {
        $copy = Join-Path $Saved $e.name
        try {
            if ($e.kind -eq 'File') {
                if ($e.present) {
                    $parent = Split-Path $e.path -Parent
                    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
                    Copy-Item -LiteralPath $copy -Destination $e.path -Force
                }
                elseif (Test-Path -LiteralPath $e.path) { Remove-Item -LiteralPath $e.path -Force }
            }
            else {
                if (Test-Path -LiteralPath $e.path) { Get-ChildItem -LiteralPath $e.path -Force | Remove-Item -Recurse -Force }
                if ($e.present) {
                    if (-not (Test-Path -LiteralPath $e.path)) { New-Item -ItemType Directory -Path $e.path | Out-Null }
                    Get-ChildItem -LiteralPath $copy -Force -ErrorAction SilentlyContinue | Copy-Item -Destination $e.path -Recurse -Force
                }
                elseif (Test-Path -LiteralPath $e.path) { Remove-Item -LiteralPath $e.path -Recurse -Force }
            }
        } catch { }   # the verification below reports what the copy could not put back
        $reason = $null
        if ($e.present) {
            $exists = if ($e.kind -eq 'File') { Test-Path -LiteralPath $e.path -PathType Leaf } else { Test-Path -LiteralPath $e.path -PathType Container }
            if (-not $exists -or (Compare-Object @(Format-EntryFiles $e.files) @(Format-EntryFiles (Get-EntryFiles $e.path $e.kind)))) { $reason = 'differs' }
        }
        elseif (Test-Path -LiteralPath $e.path) { $reason = 'present' }
        if ($reason) { $wrong += [pscustomobject]@{ Name = $e.name; Path = $e.path; Reason = $reason } }
    }
    return $wrong   # callers wrap the call in @(): nothing when all match
}

# The folders under $Root matching $Filter that hold a complete snapshot (saved\manifest.json).
function Get-HeldSnapshots([string]$Root, [string]$Filter) {
    @(Get-ChildItem -LiteralPath $Root -Directory -Filter $Filter -Force -ErrorAction SilentlyContinue |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'saved\manifest.json') })
}

# The rollback drill's leftover check. Returns $null when no nyar-drill-* folder under $Root holds a complete snapshot,
# else the refusal. A snapshot is complete once saved\manifest.json exists: the drill writes it (through a .tmp and a
# rename) after the copy and before it changes the server, so a saved\ without it means the server was never touched
# (raphael-api-core A15).
function Get-LeftoverRefusal([string]$Root) {
    $held = @(Get-ChildItem -LiteralPath $Root -Directory -Filter 'nyar-drill-*' -ErrorAction SilentlyContinue |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'saved\manifest.json') })
    if (-not $held) { return $null }
    $steps = foreach ($h in $held) {
        $m = try { Get-Content -LiteralPath (Join-Path $h.FullName 'saved\manifest.json') -Raw | ConvertFrom-Json } catch { $null }
        $dll = if (-not $m) { 'unreadable manifest' } elseif ($m.hadDll) { "copy saved\Nyarlathotep.dll to BepInEx\plugins (SHA-256 $($m.dllHash))" } else { 'remove BepInEx\plugins\Nyarlathotep.dll (there was none)' }
        $cfg = if (-not $m) { 'restore by hand' } elseif ($m.hadConfig) { 'replace BepInEx\config\Nyarlathotep with saved\config\*' } else { 'remove BepInEx\config\Nyarlathotep (there was none)' }
        "$($h.FullName): $dll; $cfg"
    }
    "an earlier drill left a saved snapshot of the dev server: $($steps -join ' | '); then delete the folder"
}
