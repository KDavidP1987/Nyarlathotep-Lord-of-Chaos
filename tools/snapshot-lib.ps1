<#
.SYNOPSIS
    Snapshot and restore of dev-server files, shared by tools/rollback-drill.ps1 (D23) and tools/dev-snapshot.ps1 (D28).

.DESCRIPTION
    Dot-source it: `. (Join-Path $PSScriptRoot 'snapshot-lib.ps1')`.

    A snapshot is a folder `saved\` holding one copy per entry (a file or a directory, named by the entry's Name) and
    `saved\manifest.json`, written through a .tmp and a move after every copy, so a `saved\` without a manifest is an
    unfinished snapshot and the live files were never touched after it. An entry that was absent at the save is
    recorded as absent, and the restore makes it absent again. A directory entry records its subdirectories too, so an
    empty one is saved and restored. The save re-reads each live entry after copying it and fails when it changed; the
    restore checks every saved copy against the manifest before it touches any live path.

      Save-Snapshot -Saved <dir> -Entries @(@{ Name; Path; Kind = 'File'|'Dir' }) [-Extra <ordered>] [-Restore <text>]
      @(Restore-Snapshot -Saved <dir>) → the entries that differ (empty when every hash matches); Reason 'copy' means a
                                         saved copy differs from the manifest and nothing live was touched
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

# A function returning @() passes $null, so nulls are dropped: an empty tree formats as no rows, not one blank row.
function Format-EntryFiles($Files) { @($Files | Where-Object { $null -ne $_ } | ForEach-Object { "$($_.path) $($_.sha256)" }) }

# The subdirectories of a Dir entry as relative paths, sorted (empty ones included); @() for a File entry or an absent path.
function Get-EntryDirs([string]$Path, [string]$Kind) {
    if ($Kind -eq 'File' -or -not (Test-Path -LiteralPath $Path -PathType Container)) { return @() }
    @(Get-ChildItem -LiteralPath $Path -Directory -Recurse -Force | ForEach-Object { $_.FullName.Substring($Path.Length) } | Sort-Object)
}

# $true when $Path is of its kind (a file for File, a directory for Dir) and holds exactly $Files and (when $Dirs is not
# $null) exactly $Dirs.
function Test-EntryMatches([string]$Path, [string]$Kind, $Files, $Dirs) {
    if (-not (Test-Path -LiteralPath $Path -PathType $(if ($Kind -eq 'File') { 'Leaf' } else { 'Container' }))) { return $false }
    if (Compare-Object @(Format-EntryFiles $Files) @(Format-EntryFiles (Get-EntryFiles $Path $Kind))) { return $false }
    if ($null -ne $Dirs -and (Compare-Object @($Dirs | Where-Object { $null -ne $_ }) @(Get-EntryDirs $Path $Kind))) { return $false }
    $true
}

# Copies every entry into $Saved (hidden files and empty directories included), checks each copy against the record
# taken before the copy and re-reads the live entry, then writes saved\manifest.json through a .tmp and a move, last.
# Once every entry is copied, re-reads them all again, so a change to an early entry while a later one was copying is
# caught too. Returns the manifest. Throws when a copy differs or a live entry changed during the save.
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
        $dirs = @(Get-EntryDirs $e.Path $e.Kind)
        if ($e.Kind -eq 'File') {
            if ($present) { Copy-Item -LiteralPath $e.Path -Destination $copy -Force }
        }
        else {
            New-Item -ItemType Directory -Path $copy -Force | Out-Null
            if ($present) { Get-ChildItem -LiteralPath $e.Path -Force | Copy-Item -Destination $copy -Recurse -Force }
        }
        if ($present -and -not (Test-EntryMatches $copy $e.Kind $files $dirs)) {
            throw "the saved copy of $($e.Path) differs from the live one"
        }
        $still = if ($e.Kind -eq 'File') { Test-Path -LiteralPath $e.Path -PathType Leaf } else { Test-Path -LiteralPath $e.Path -PathType Container }
        if ($still -ne $present -or ($present -and -not (Test-EntryMatches $e.Path $e.Kind $files $dirs))) {
            throw "$($e.Path) changed during the save; stop whatever writes to it and save again"
        }
        [ordered]@{ name = $e.Name; path = $e.Path; kind = $e.Kind; present = $present; files = $files; dirs = $dirs }
    }
    foreach ($r in @($records)) {
        $still = if ($r.kind -eq 'File') { Test-Path -LiteralPath $r.path -PathType Leaf } else { Test-Path -LiteralPath $r.path -PathType Container }
        if ($still -ne $r.present -or ($r.present -and -not (Test-EntryMatches $r.path $r.kind $r.files $r.dirs))) {
            throw "$($r.path) changed during the save; stop whatever writes to it and save again"
        }
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

# First checks every saved copy against the manifest; when one differs, returns it with Reason 'copy' and touches no
# live path. Otherwise makes every entry equal to its copy: a present file or directory is replaced by the copy
# (anything added since is removed, changed and removed files are put back), an absent one is removed. Then verifies
# every hash (and a directory entry's subdirectories) against the manifest and that no unlisted file remains. Returns
# one object { Name; Path; Reason } per entry that differs ('differs', or 'present' for one that was absent); @() when
# all match. A manifest without `dirs` (written before they were recorded) is checked on files alone.
function Restore-Snapshot {
    param([Parameter(Mandatory)][string]$Saved)
    $m = Get-Content -LiteralPath (Join-Path $Saved 'manifest.json') -Raw | ConvertFrom-Json
    $dirsOf = { param($e) if ($e.PSObject.Properties.Name -contains 'dirs') { , @($e.dirs) } else { $null } }
    $bad = @(foreach ($e in @($m.entries)) {
        if ($e.present -and -not (Test-EntryMatches (Join-Path $Saved $e.name) $e.kind $e.files (& $dirsOf $e))) {
            [pscustomobject]@{ Name = $e.name; Path = $e.path; Reason = 'copy' }
        }
    })
    if ($bad) { return $bad }
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
            if (-not $exists -or -not (Test-EntryMatches $e.path $e.kind $e.files (& $dirsOf $e))) { $reason = 'differs' }
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
