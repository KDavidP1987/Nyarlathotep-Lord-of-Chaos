<#
.SYNOPSIS
    Preflight checks for Nyarlathotep, Lord of Chaos.

.DESCRIPTION
    Every check is a function named Test-Check<Name> that takes -Root (the repository root, or a fixture
    directory during -SelfTest) and returns @{ Pass; Line }. tools/preflight-checks.json lists every
    check with the inputs it reads and its fixture directory tools/preflight-fixtures/<Name>/: good/
    holds copies of real repository files (plus a synthetic file where the real one does not exist yet),
    bad/ and any bad-<n>/ the same with one planted fault each (described under "plant"), empty/ nothing.
    -SelfTest proves each check passes good and fails every bad and empty, that every manifest entry is
    complete, and that the manifest and the Test-Check functions in this file are the same set (Epic D10).
    -Verbose prints each fixture's result line.

    Default run  : every check whose manifest mode is "default". Exit 1 if any fails.
    -Paths       : the paths walk (Test-CheckPaths) — run after dotnet build, tcli build and a deploy.
    -SelfTest    : every check against its three fixtures.

    Checks read files relative to -Root. In the real repository the file set is git's
    (git ls-files --cached --others --exclude-standard) minus tools/preflight-fixtures/, whose bad
    fixtures deliberately break the rules (the secrets scan alone includes the fixtures); in a fixture
    the file set is every file under the fixture. Git-derived inputs (commit subjects, tags, the path
    walk) come from git in the real repository and from captured text files (git-log.txt, tags.txt,
    remote-tags.txt, walked.txt) in a fixture, written in the format the real collection produces.

.EXAMPLE
    pwsh tools/preflight.ps1
    pwsh tools/preflight.ps1 -Paths
    pwsh tools/preflight.ps1 -SelfTest
#>

[CmdletBinding()]
param(
    [switch]$SelfTest,
    [switch]$Paths,
    [string]$ServerPath = 'C:\Program Files (x86)\Steam\steamapps\common\VRisingDedicatedServer'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repoRoot 'tools/preflight-checks.json'
$script:FixtureRoot = $null   # set while a check runs against a fixture

$PkgRel = 'Nyarlathotep/Nyarlathotep'

# ---------------------------------------------------------------- helpers

function New-Result([bool]$Pass, [string]$Line) { [pscustomobject]@{ Pass = $Pass; Line = $Line } }

function Test-IsFixture([string]$Root) { $null -ne $script:FixtureRoot -and $Root -eq $script:FixtureRoot }

# Every file of the tree as forward-slash paths relative to $Root.
function Get-TreeFiles([string]$Root, [switch]$IncludeFixtures) {
    if (Test-IsFixture $Root) {
        if (-not (Test-Path $Root)) { return @() }
        $base = (Resolve-Path $Root).Path.TrimEnd('\', '/')
        return @(Get-ChildItem -Path $base -Recurse -File -Force | ForEach-Object {
            $_.FullName.Substring($base.Length + 1).Replace('\', '/')
        })
    }
    $list = git -C $Root ls-files --cached --others --exclude-standard 2>$null
    if ($LASTEXITCODE -ne 0) { return @() }
    return @($list | ForEach-Object { $_.Trim('"') } |
        Where-Object { $IncludeFixtures -or $_ -notlike 'tools/preflight-fixtures/*' } |
        Where-Object { Test-Path -LiteralPath (Join-Path $Root $_) -PathType Leaf })
}

function Get-CsFiles([string]$Root) { @(Get-TreeFiles $Root | Where-Object { $_ -like '*.cs' }) }

function Read-Text([string]$Root, [string]$Rel) {
    $p = Join-Path $Root $Rel
    if (Test-Path -LiteralPath $p -PathType Leaf) { return [IO.File]::ReadAllText($p) }
    return $null
}

# Remove // line comments and /* */ block comments so documentation never counts as a call.
function Remove-CsComments([string]$Text) {
    $t = [regex]::Replace($Text, '/\*.*?\*/', '', 'Singleline')
    return [regex]::Replace($t, '(?m)//.*$', '')
}

# The body of the first method whose declaration starts at or after $Index: the text between its
# first "{" and the matching "}".
function Get-MethodBody([string]$Text, [int]$Index) {
    $open = $Text.IndexOf('{', $Index)
    if ($open -lt 0) { return '' }
    $depth = 0
    for ($i = $open; $i -lt $Text.Length; $i++) {
        if ($Text[$i] -eq '{') { $depth++ }
        elseif ($Text[$i] -eq '}') { $depth--; if ($depth -eq 0) { return $Text.Substring($open, $i - $open + 1) } }
    }
    return $Text.Substring($open)
}

function Get-Frontmatter([string]$Text) {
    $h = @{}
    if ($Text -match '(?s)^---\r?\n(.*?)\r?\n---') {
        foreach ($l in ($Matches[1] -split '\r?\n')) { if ($l -match '^(\w+):\s*(.*)$') { $h[$Matches[1]] = $Matches[2].Trim() } }
    }
    return $h
}

# Children of the Epic whose own plan says status: done.
function Get-DoneChildren([string]$Root) {
    $epic = Read-Text $Root 'docs/dod/nyarlathotep.md'
    if ($null -eq $epic) { return $null }
    $sec = [regex]::Match($epic, '(?ms)^## Children\s*\r?\n(.*?)(?=^## |\z)')
    $slugs = @()
    if ($sec.Success) { $slugs = @([regex]::Matches($sec.Groups[1].Value, '(?m)^- ([a-z0-9-]+) · ') | ForEach-Object { $_.Groups[1].Value }) }
    # The leading comma keeps an empty list a list (PowerShell unrolls @() to $null on return).
    return , @($slugs | Where-Object {
        $plan = Read-Text $Root "docs/dod/$_.md"
        $plan -and (Get-Frontmatter $plan)['status'] -eq 'done'
    })
}

# "tracked: <glob>" / "ignored: <glob>" / "server: <glob>" lines of tools/paths-manifest.txt.
function Get-PathsManifest([string]$Root) {
    $t = Read-Text $Root 'tools/paths-manifest.txt'
    if ($null -eq $t) { return $null }
    return @($t -split '\r?\n' | Where-Object { $_ -match '^(tracked|ignored|server):\s*(\S.*)$' } |
        ForEach-Object { $null = $_ -match '^(tracked|ignored|server):\s*(\S.*)$'; [pscustomobject]@{ Kind = $Matches[1]; Glob = $Matches[2].Trim() } })
}

function ConvertTo-GlobRegex([string]$Glob) {
    $sb = [Text.StringBuilder]::new('^')
    for ($i = 0; $i -lt $Glob.Length; $i++) {
        $c = $Glob[$i]
        if ($c -eq '*' -and $i + 1 -lt $Glob.Length -and $Glob[$i + 1] -eq '*') {
            if ($i + 2 -lt $Glob.Length -and $Glob[$i + 2] -eq '/') { [void]$sb.Append('(?:.*/)?'); $i += 2 }
            else { [void]$sb.Append('.*'); $i++ }
        }
        elseif ($c -eq '*') { [void]$sb.Append('[^/]*') }
        elseif ($c -eq '?') { [void]$sb.Append('[^/]') }
        else { [void]$sb.Append([regex]::Escape([string]$c)) }
    }
    [void]$sb.Append('$')
    return $sb.ToString()
}

function Test-GlobMatch([string]$Path, [string]$Glob) {
    $rx = ConvertTo-GlobRegex $Glob
    if ($Path.EndsWith('/')) { return ($Path + 'x') -match $rx -or $Path.TrimEnd('/') -match $rx }
    return $Path -match $rx
}

# ---------------------------------------------------------------- checks: release surfaces

function Get-Version([string]$Root) {
    $csproj = Read-Text $Root "$PkgRel/Nyarlathotep.csproj"
    if ($null -eq $csproj) { return $null }
    if ($csproj -match '<Version>([^<]+)</Version>') { return $Matches[1].Trim() }
    return $null
}

function Test-CheckVersion([string]$Root) {
    $csv = Get-Version $Root
    $toml = Read-Text $Root "$PkgRel/thunderstore.toml"
    if ($null -eq $csv -or $null -eq $toml) { return New-Result $false 'version: csproj <Version> or thunderstore.toml not found' }
    $tv = if ($toml -match 'versionNumber\s*=\s*"([^"]+)"') { $Matches[1] } else { $null }
    if ($csv -ne $tv) { return New-Result $false "version: MISMATCH csproj=$csv thunderstore.toml=$tv" }
    return New-Result $true "version: $csv (csproj = thunderstore.toml)"
}

function Test-CheckChangelogs([string]$Root) {
    $v = Get-Version $Root
    if ($null -eq $v) { return New-Result $false 'changelogs: no version to look for (csproj not found)' }
    $e = [regex]::Escape($v)
    $missing = @()
    foreach ($rel in @('CHANGELOG.md', "$PkgRel/CHANGELOG.md")) {
        $t = Read-Text $Root $rel
        if ($null -eq $t -or $t -notmatch "(?m)^##\s*\[?$e\]?") { $missing += $rel }
    }
    if ($missing) { return New-Result $false "changelogs: no entry for $v in $($missing -join ', ')" }
    return New-Result $true "changelogs: $v in both"
}

function Test-CheckDescription([string]$Root) {
    $toml = Read-Text $Root "$PkgRel/thunderstore.toml"
    if ($null -eq $toml -or $toml -notmatch 'description\s*=\s*"([^"]*)"') { return New-Result $false 'description: thunderstore.toml description not found' }
    $len = $Matches[1].Length
    if ($len -eq 0 -or $len -gt 250) { return New-Result $false "description: $len chars (must be 1-250)" }
    return New-Result $true "description: $len/250 chars"
}

function Test-CheckTemplatesJson([string]$Root) {
    if ($null -eq (Read-Text $Root "$PkgRel/Nyarlathotep.csproj")) { return New-Result $false 'templates: package directory not found' }
    $files = @(Get-TreeFiles $Root | Where-Object { $_ -like "$PkgRel/Resources/*.json" })
    foreach ($f in $files) {
        try { Read-Text $Root $f | ConvertFrom-Json | Out-Null }
        catch { return New-Result $false "templates: $f is not valid JSON" }
    }
    return New-Result $true "templates: $($files.Count) valid"
}

# ---------------------------------------------------------------- checks: safety rules (Epic D4-D9)

function Test-CheckPillarDefaults([string]$Root) {
    $settings = Read-Text $Root "$PkgRel/Config/Settings.cs"
    if ($null -eq $settings) { return New-Result $false 'pillar defaults: Config/Settings.cs not found' }
    $binds = [regex]::Matches((Remove-CsComments $settings), 'Bind\(\s*"Pillars"\s*,\s*"([^"]+)"\s*,\s*([^,\s)]+)')
    if ($binds.Count -eq 0) { return New-Result $false 'pillar defaults: no [Pillars] switches found' }
    $on = @($binds | Where-Object { $_.Groups[2].Value -ne 'false' } | ForEach-Object { $_.Groups[1].Value })
    if ($on) { return New-Result $false "pillar defaults: ON by default: $($on -join ', ')" }
    $templates = @(Get-TreeFiles $Root | Where-Object { $_ -like "$PkgRel/Resources/*.json" })
    foreach ($f in $templates) {
        if ((Read-Text $Root $f) -match '"enabled"\s*:\s*true') { return New-Result $false "pillar defaults: $f ships an enabled event" }
    }
    return New-Result $true "pillar defaults: all off ($($binds.Count) switches, $($templates.Count) templates)"
}

$script:PublicCommands = @('nyar', 'status', 'help')

function Test-CheckCommands([string]$Root) {
    $cs = Get-CsFiles $Root
    $admin = 0; $public = 0; $bad = @()
    foreach ($f in $cs) {
        $text = Remove-CsComments (Read-Text $Root $f)
        foreach ($m in [regex]::Matches($text, '\[Command\s*\((?<args>(?:[^()]|\((?:[^()])*\))*)\)\s*\]')) {
            $a = $m.Groups['args'].Value
            $name = if ($a -match '^\s*(?:name\s*:\s*)?"([^"]*)"') { $Matches[1] } else { '?' }
            if ($f -notlike "$PkgRel/Commands/*") { $bad += "$name outside Commands/ ($f)"; continue }
            if ($a -match 'adminOnly\s*:\s*true') { $admin++ }
            elseif ($script:PublicCommands -contains $name) { $public++ }
            else { $bad += "$name not adminOnly ($f)" }
        }
    }
    if ($admin + $public + $bad.Count -eq 0) { return New-Result $false 'commands: none found' }
    if ($bad) { return New-Result $false "commands: $($bad -join '; ')" }
    return New-Result $true "commands: $admin admin-only, $public public (allow-listed)"
}

# Start index of the method declaration that encloses $Index: the last "<modifier> ... name(" signature
# line before it, where the text between that signature's "{" and $Index is still inside its braces.
function Get-EnclosingMethodStart([string]$Text, [int]$Index) {
    $sigs = [regex]::Matches($Text.Substring(0, $Index), '(?m)^[ \t]*(?:(?:public|internal|private|protected|static|override|virtual|unsafe)\s+)+[\w<>\[\],.? ]+?\s+\w+\s*(?:<[^>]*>)?\s*\(')
    for ($k = $sigs.Count - 1; $k -ge 0; $k--) {
        $body = Get-MethodBody $Text $sigs[$k].Index
        $open = $Text.IndexOf('{', $sigs[$k].Index)
        if ($open -ge 0 -and $open -lt $Index -and $open + $body.Length -gt $Index) { return $sigs[$k].Index }
    }
    return -1
}

# True when $Before (a method's text from its signature up to a structural call on $Entity) holds an early
# refusal of Prefab entities: an "if (<cond>) return ..." (or "if (<cond>) { ... return ... }") that sits
# directly in the method body, not nested in another block, whose condition has "$Entity.Has<Prefab>()" or
# "<x>.HasComponent<Prefab>($Entity)" as one whole top-level "||" term — so no "&&", negation or
# comparison can switch it off — and after which $Entity is not reassigned.
function Test-PrefabRefusal([string]$Before, [string]$Entity) {
    $e = [regex]::Escape($Entity)
    $termRx = "^($e\s*\.\s*Has(?:Component)?<Prefab>\s*\(\s*\)|[\w.]+\s*\.\s*HasComponent<Prefab>\s*\(\s*$e\s*\))$"
    $open = $Before.IndexOf('{')
    if ($open -lt 0) { return $false }
    $depth = 0
    for ($i = $open; $i -lt $Before.Length; $i++) {
        $ch = $Before[$i]
        if ($ch -eq '{') { $depth++; continue }
        if ($ch -eq '}') { $depth--; continue }
        if ($depth -ne 1 -or $ch -ne 'i' -or ($i -gt 0 -and $Before[$i - 1] -match '\w')) { continue }
        $m = [regex]::Match($Before.Substring($i), '^if\s*\(')
        if (-not $m.Success) { continue }
        if ($Before.Substring(0, $i) -match 'else\s*$') { continue }   # an "else if" runs only on some paths
        # The condition: from the "(" to its matching ")".
        $start = $i + $m.Length; $p = 1; $j = $start
        while ($j -lt $Before.Length -and $p -gt 0) { if ($Before[$j] -eq '(') { $p++ } elseif ($Before[$j] -eq ')') { $p-- }; $j++ }
        if ($p -ne 0) { return $false }
        $cond = $Before.Substring($start, $j - 1 - $start)
        if ($Before.Substring($j) -notmatch '^\s*(return\b|\{[^{}]*?\breturn\b)') { continue }
        # Split the condition on "||" at parenthesis depth 0.
        $terms = @(); $q = 0; $last = 0
        for ($k = 0; $k -lt $cond.Length; $k++) {
            if ($cond[$k] -eq '(') { $q++ } elseif ($cond[$k] -eq ')') { $q-- }
            elseif ($q -eq 0 -and $k + 1 -lt $cond.Length -and $cond[$k] -eq '|' -and $cond[$k + 1] -eq '|') { $terms += $cond.Substring($last, $k - $last); $last = $k + 2; $k++ }
        }
        $terms += $cond.Substring($last)
        if (-not (@($terms | Where-Object { $_.Trim() -match $termRx }))) { continue }
        # The guarded entity must reach the call unchanged.
        if ($Before.Substring($j) -match "(?<![\w.])$e\s*=(?!=)") { continue }
        return $true
    }
    return $false
}

function Test-CheckStructuralEdits([string]$Root) {
    $cs = Get-CsFiles $Root
    if ($cs.Count -eq 0) { return New-Result $false 'structural edits: no source found' }
    # An ECS structural call always takes the entity as an argument; Unity's GameObject.AddComponent<T>()
    # (the coroutine host in Core.cs) takes none and is not a structural edit.
    $rx = '\.(AddComponent\w*|RemoveComponent\w*|AddBuffer|DestroyEntity)\s*(<[^>]*>)?\s*\(\s*(?<arg>[^,)\s]+)'
    $fence = "$PkgRel/EntityExtensions.cs"
    $bad = @(); $guarded = 0
    foreach ($f in $cs) {
        $text = Remove-CsComments (Read-Text $Root $f)
        $calls = [regex]::Matches($text, $rx)
        if ($calls.Count -eq 0) { continue }
        if ($f -ne $fence) { $bad += "$($calls[0].Groups[1].Value) in $f"; continue }
        # Inside the fence, every structural call must be preceded, in its own method, by an early
        # refusal of Prefab entities.
        foreach ($c in $calls) {
            $start = Get-EnclosingMethodStart $text $c.Index
            if ($start -lt 0) { $bad += "$($c.Groups[1].Value) in $f outside a method"; continue }
            $before = $text.Substring($start, $c.Index - $start)
            if (-not (Test-PrefabRefusal $before $c.Groups['arg'].Value)) { $bad += "$($c.Groups[1].Value) in $f without an earlier Prefab refusal" }
            else { $guarded++ }
        }
    }
    if ($bad) { return New-Result $false "structural edits: $($bad -join '; ')" }
    return New-Result $true "structural edits: fenced ($guarded Prefab-guarded calls in EntityExtensions.cs)"
}

function Test-CheckFileWrites([string]$Root) {
    $cs = Get-CsFiles $Root
    if ($cs.Count -eq 0) { return New-Result $false 'file writes: no source found' }
    $rx = '\b(File\.(Write\w*|AppendAll\w*|Replace|Move|Delete|Copy|Create\w*|Open\w*)|Directory\.(Create\w*|Delete|Move)|new\s+(FileStream|StreamWriter)\s*\()'
    $fence = "$PkgRel/Services/Persistence.cs"
    $bad = @(); $inFence = 0
    foreach ($f in $cs) {
        $calls = [regex]::Matches((Remove-CsComments (Read-Text $Root $f)), $rx)
        if ($calls.Count -eq 0) { continue }
        if ($f -eq $fence) { $inFence += $calls.Count } else { $bad += "$($calls[0].Value) in $f" }
    }
    # The fence itself (Epic D7): no non-private member takes a path, file, name or directory string, every
    # file-name literal is one of the three data files or their .bak/.tmp siblings, and the directory is
    # built from BepInEx's config path plus "Nyarlathotep".
    $pers = Read-Text $Root $fence
    $persNote = 'no Persistence.cs yet'
    if ($pers) {
        $p = Remove-CsComments $pers
        foreach ($m in [regex]::Matches($p, '(?m)^\s*(?:(?:public|internal|protected)\s+)(?:static\s+)?[\w<>\[\],.? ]+?\s+(\w+)\s*\(([^)]*)\)')) {
            if ($m.Groups[2].Value -match '(?i)\bstring\s+\w*(path|file|name|dir|folder)\w*') { $bad += "Persistence.$($m.Groups[1].Value) takes a path or file-name parameter" }
        }
        # Outside log calls, the only string literals allowed are the data-file names, their .bak/.tmp
        # suffixes and the folder name, so no other file name can be spelled or assembled (an interpolated
        # string outside a log call fails too).
        $noLogs = [regex]::Replace($p, '\bLog\w*\s*\((?:[^()]|\((?:[^()]|\([^()]*\))*\))*\)', 'LOG()')
        $allowed = '^((events|zones|state)\.json(\.bak|\.tmp)?|\.bak|\.tmp|Nyarlathotep)$'
        foreach ($m in [regex]::Matches($noLogs, '(\$@|@\$|\$|@)?"((?:[^"\\]|\\.)*)"')) {
            if ($m.Groups[1].Value -match '\$') { $bad += "Persistence.cs builds a string outside a log call ($($m.Value))"; continue }
            if ($m.Groups[2].Value -notmatch $allowed) { $bad += "Persistence.cs names '$($m.Groups[2].Value)'" }
        }
        # The folder: members defined as Path.Combine(Paths.ConfigPath, "Nyarlathotep"); every Path.Combine
        # must start from one of them (or be that definition itself).
        $folders = @([regex]::Matches($p, '(\w+)\s*(?:=>|=)\s*Path\.Combine\(\s*Paths\.ConfigPath\s*,\s*"Nyarlathotep"\s*\)') | ForEach-Object { $_.Groups[1].Value })
        if ($inFence -gt 0 -and $folders.Count -eq 0) { $bad += 'Persistence.cs does not define its folder as Path.Combine(Paths.ConfigPath, "Nyarlathotep")' }
        foreach ($m in [regex]::Matches($p, 'Path\.Combine\(\s*([^,()]+(?:\([^()]*\))?)\s*,\s*([^,)]+)')) {
            $first = $m.Groups[1].Value.Trim()
            $isDef = $first -eq 'Paths.ConfigPath' -and $m.Groups[2].Value.Trim() -eq '"Nyarlathotep"'
            if (-not $isDef -and $folders -notcontains $first) { $bad += "Persistence.cs combines a path from '$first'" }
        }
        # Every path handed to a write call must come from the folder: the folder member itself, a local
        # assigned Path.Combine(<folder>, ...), or one of those plus ".bak"/".tmp".
        $pathVars = @($folders)
        foreach ($m in [regex]::Matches($p, '\b(?:var|string)\s+(\w+)\s*=\s*Path\.Combine\(\s*(\w+)\s*,')) { if ($folders -contains $m.Groups[2].Value) { $pathVars += $m.Groups[1].Value } }
        foreach ($m in [regex]::Matches($p, '\b(?:var|string)\s+(\w+)\s*=\s*(\w+)\s*\+\s*"\.(?:bak|tmp)"\s*;')) { if ($pathVars -contains $m.Groups[2].Value) { $pathVars += $m.Groups[1].Value } }
        $okArg = '^(' + (@($pathVars | ForEach-Object { [regex]::Escape($_) }) -join '|') + ')(\s*\+\s*"\.(bak|tmp)")?$'
        foreach ($m in [regex]::Matches($p, $rx)) {
            $open = $m.Index + $m.Length - 1
            while ($open -lt $p.Length -and $p[$open] -ne '(') { $open++ }
            # Split the argument list at depth-0 commas.
            $argv = @(); $q = 0; $last = $open + 1; $k = $open + 1
            for (; $k -lt $p.Length; $k++) {
                if ($p[$k] -eq '(') { $q++ } elseif ($p[$k] -eq ')') { if ($q -eq 0) { break }; $q-- }
                elseif ($p[$k] -eq ',' -and $q -eq 0) { $argv += $p.Substring($last, $k - $last).Trim(); $last = $k + 1 }
            }
            $argv += $p.Substring($last, $k - $last).Trim()
            # Replace/Move/Copy take paths in every position; the rest take the path first.
            $paths = if ($m.Value -match 'File\.(Replace|Move|Copy)|Directory\.Move') { $argv } else { @($argv[0]) }
            foreach ($a in $paths) { if ($pathVars.Count -eq 0 -or $a -notmatch $okArg) { $bad += "Persistence.cs writes to '$a', not a path from its folder" } }
        }
        foreach ($m in [regex]::Matches($p, '\b(Path\.GetTempPath|Path\.GetFullPath|Environment\.\w+|Directory\.GetCurrentDirectory|AppContext\.BaseDirectory|AppDomain\.\w+|Application\.\w+Path)\b')) {
            $bad += "Persistence.cs reaches another location ($($m.Value))"
        }
        $persNote = 'no path parameter, constant names'
    }
    if ($bad) { return New-Result $false "file writes: $($bad -join '; ')" }
    return New-Result $true "file writes: fenced ($inFence calls in Services/Persistence.cs, $persNote)"
}

# The one patch that runs before Core.IsReady (it is what sets it): its guard is the inverse,
# "if (Core.IsReady) return;" (Epic D8, amendment A1).
$script:InitPatchFile = "$PkgRel/Patches/GameDataInitializedPatch.cs"

# True when $Body (a method body including its outer braces) is exactly: the guard statement, then one
# try block, then one or more catch blocks and an optional finally, and nothing else.
function Test-GuardedBody([string]$Body, [string]$GuardRx) {
    $inner = $Body.Substring(1, $Body.Length - 2).Trim()
    $g = [regex]::Match($inner, "^$GuardRx")
    if (-not $g.Success) { return $false }
    $rest = $inner.Substring($g.Length).TrimStart()
    if ($rest -notmatch '^try\s*\{') { return $false }
    $open = $rest.IndexOf('{'); $rest = $rest.Substring($open + (Get-MethodBody $rest 0).Length).TrimStart()
    $catches = 0
    while ($rest -match '^(catch\b(\s*\([^)]*\))?(\s*when\s*\([^)]*\))?|finally\b)\s*\{') {
        if ($Matches[1] -like 'catch*') { $catches++ }
        $open = $rest.IndexOf('{'); $rest = $rest.Substring($open + (Get-MethodBody $rest 0).Length).TrimStart()
    }
    return $catches -gt 0 -and $rest -eq ''
}

function Test-CheckPatchGuards([string]$Root) {
    $files = @(Get-CsFiles $Root | Where-Object { $_ -like "$PkgRel/Patches/*.cs" })
    $total = 0; $ok = 0; $bad = @()
    $normal = 'if\s*\(\s*!\s*Core\.IsReady\s*\)\s*(\{\s*)?return\b[^;]*;\s*(\}\s*)?'
    $inverse = 'if\s*\(\s*Core\.IsReady\s*\)\s*(\{\s*)?return\b[^;]*;\s*(\}\s*)?'
    foreach ($f in $files) {
        $text = Remove-CsComments (Read-Text $Root $f)
        foreach ($m in [regex]::Matches($text, '\[Harmony(Prefix|Postfix)\]')) {
            $total++
            $body = Get-MethodBody $text $m.Index
            $guard = if ($f -eq $script:InitPatchFile) { $inverse } else { $normal }
            if (Test-GuardedBody $body $guard) { $ok++ } else { $bad += $f }
        }
    }
    if ($total -eq 0) { return New-Result $false 'patch guards: no patches found' }
    if ($bad) { return New-Result $false "patch guards: $ok/$total (not 'IsReady guard, then try/catch around the rest' in $($bad -join ', '))" }
    return New-Result $true "patch guards: $ok/$total"
}

$script:SecretPatterns = @(
    'tss_[A-Za-z0-9]{8,}',
    'gh[pousr]_[A-Za-z0-9]{20,}',
    'github_pat_[A-Za-z0-9_]{20,}',
    'Authorization:\s*Bearer\s+[A-Za-z0-9._~+/=-]{8,}',
    '(TCLI_AUTH_TOKEN|GH_TOKEN)\s*[=:]\s*\S+'
)
$script:BinaryExt = @('.png', '.jpg', '.jpeg', '.gif', '.dll', '.pdb', '.exe', '.ico')

function Find-Secret([string]$Text) {
    foreach ($p in $script:SecretPatterns) { if ($Text -match $p) { return $p } }
    return $null
}

function Test-CheckSecrets([string]$Root) {
    # Secrets are scanned in the fixtures too (captured logs live there); no fixture stores a raw token.
    $files = @(Get-TreeFiles $Root -IncludeFixtures)
    if (-not (Test-IsFixture $Root)) {
        foreach ($d in @("$PkgRel/dist", "$PkgRel/build")) {
            $abs = Join-Path $Root $d
            if (Test-Path $abs) {
                $base = (Resolve-Path $Root).Path.TrimEnd('\', '/')
                $files += @(Get-ChildItem $abs -Recurse -File | ForEach-Object { $_.FullName.Substring($base.Length + 1).Replace('\', '/') })
            }
        }
    }
    if ($files.Count -eq 0) { return New-Result $false 'secrets: no files scanned' }
    $hits = @(); $scanned = 0
    foreach ($f in $files) {
        $abs = Join-Path $Root $f
        $ext = [IO.Path]::GetExtension($f).ToLowerInvariant()
        if ($ext -eq '.zip') {
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            $zip = [IO.Compression.ZipFile]::OpenRead($abs)
            try {
                foreach ($e in $zip.Entries) {
                    if ($script:BinaryExt -contains [IO.Path]::GetExtension($e.Name).ToLowerInvariant()) { continue }
                    $r = [IO.StreamReader]::new($e.Open()); $t = $r.ReadToEnd(); $r.Dispose(); $scanned++
                    if (Find-Secret $t) { $hits += "$f!$($e.FullName)" }
                }
            } finally { $zip.Dispose() }
            continue
        }
        if ($script:BinaryExt -contains $ext) { continue }
        $t = [IO.File]::ReadAllText($abs); $scanned++
        if (Find-Secret $t) { $hits += $f }
        if ($ext -eq '.cs' -and (Remove-CsComments $t) -match 'Environment\.GetEnvironmentVariable') { $hits += "$f (reads the environment)" }
    }
    # The index can hold content the working tree no longer shows (a token staged or committed, then
    # edited out or deleted only on disk), so every blob in the index is searched as well.
    $indexNote = ''
    if (-not (Test-IsFixture $Root)) {
        $blobs = @(git -C $Root ls-files --cached 2>$null).Count
        foreach ($p in $script:SecretPatterns) {
            $found = git -C $Root grep --cached -I -l -P $p 2>&1
            if ($LASTEXITCODE -gt 1) { return New-Result $false "secrets: git grep --cached failed: $found" }
            $hits += @($found | Where-Object { $_ } | ForEach-Object { "$_ (index)" })
        }
        $found = git -C $Root grep --cached -I -l -P 'Environment\.GetEnvironmentVariable' -- '*.cs' 2>&1
        if ($LASTEXITCODE -gt 1) { return New-Result $false "secrets: git grep --cached failed: $found" }
        $hits += @($found | Where-Object { $_ } | ForEach-Object { "$_ (index, reads the environment)" })
        $indexNote = ", $blobs index blobs"
    }
    if ($hits) { return New-Result $false "secrets: FOUND in $(@($hits | Sort-Object -Unique) -join ', ')" }
    return New-Result $true "secrets: none ($scanned files scanned$indexNote)"
}

# ---------------------------------------------------------------- checks: process records (Epic D17-D19, D21)

function Test-CheckAudits([string]$Root) {
    $done = Get-DoneChildren $Root
    if ($null -eq $done) { return New-Result $false 'audits: docs/dod/nyarlathotep.md not found' }
    $bad = @()
    foreach ($s in $done) {
        $t = Read-Text $Root "docs/audits/$s.md"
        if ($null -eq $t) { $bad += "$s (no docs/audits/$s.md)"; continue }
        foreach ($mk in @('## Pre-audit', '## Post-audit', 'Codex verdict:')) { if (-not $t.Contains($mk)) { $bad += "$s (lacks '$mk')" } }
    }
    if ($bad) { return New-Result $false "audits: $($done.Count - @($bad | ForEach-Object { ($_ -split ' ')[0] } | Sort-Object -Unique).Count)/$($done.Count) done children recorded; $($bad -join '; ')" }
    return New-Result $true "audits: $($done.Count)/$($done.Count) done children recorded"
}

function Test-CheckFeatureResults([string]$Root) {
    $done = Get-DoneChildren $Root
    if ($null -eq $done) { return New-Result $false 'feature results: docs/dod/nyarlathotep.md not found' }
    $mt = Read-Text $Root 'tools/preflight-checks.json'
    if ($null -eq $mt) { return New-Result $false 'feature results: tools/preflight-checks.json not found' }
    $map = ($mt | ConvertFrom-Json).childDocs
    $bad = @()
    foreach ($s in $done) {
        $docs = @($map.$s)
        if ($docs.Count -eq 0) { $bad += "$s (no childDocs mapping)"; continue }
        foreach ($d in $docs) {
            $t = Read-Text $Root $d
            # The date must sit inside the section: from its heading to the next "## " heading.
            $sec = if ($t) { [regex]::Match($t, '(?ms)^## Test results[ \t]*\r?$(.*?)(?=^## |\z)') } else { $null }
            if ($null -eq $sec -or -not $sec.Success -or $sec.Groups[1].Value -notmatch '\b\d{4}-\d{2}-\d{2}\b') { $bad += "$s ($d lacks a dated ## Test results entry)" }
        }
    }
    if ($bad) { return New-Result $false "feature results: $($bad -join '; ')" }
    return New-Result $true "feature results: $($done.Count)/$($done.Count)"
}

function Test-CheckIcon([string]$Root) {
    $p = Join-Path $Root "$PkgRel/icon.png"
    if (-not (Test-Path -LiteralPath $p)) { return New-Result $false 'icon: icon.png not found' }
    $b = [IO.File]::ReadAllBytes($p)
    if ($b.Length -lt 24 -or $b[0] -ne 0x89 -or $b[1] -ne 0x50 -or $b[2] -ne 0x4E -or $b[3] -ne 0x47) { return New-Result $false 'icon: icon.png is not a PNG' }
    $w = ([int]$b[16] -shl 24) -bor ([int]$b[17] -shl 16) -bor ([int]$b[18] -shl 8) -bor [int]$b[19]
    $h = ([int]$b[20] -shl 24) -bor ([int]$b[21] -shl 16) -bor ([int]$b[22] -shl 8) -bor [int]$b[23]
    if ($w -ne 256 -or $h -ne 256) { return New-Result $false "icon: ${w}x${h} (must be 256x256)" }
    return New-Result $true 'icon: 256x256'
}

# Release commits, tags and remote tags as "<sha> <subject>", "<name> <objecttype> <commit sha>",
# "<name> <commit sha>".
function Get-ReleaseInputs([string]$Root) {
    if (Test-IsFixture $Root) {
        $log = Read-Text $Root 'git-log.txt'
        if ($null -eq $log) { return $null }
        return [pscustomobject]@{
            Log    = @($log -split '\r?\n' | Where-Object { $_ })
            Tags   = @((Read-Text $Root 'tags.txt') -split '\r?\n' | Where-Object { $_ })
            Remote = { @((Read-Text $Root 'remote-tags.txt') -split '\r?\n' | Where-Object { $_ }) }.GetNewClosure()
        }
    }
    $log = git -C $Root log --format='%H %s' 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    $tags = git -C $Root for-each-ref refs/tags --format='%(refname:short) %(objecttype) %(*objectname)%(objectname)' 2>$null
    $r = $Root
    return [pscustomobject]@{
        Log    = @($log)
        Tags   = @($tags | ForEach-Object { $p = $_ -split ' '; if ($p[1] -eq 'tag') { "$($p[0]) tag $($p[2].Substring(0, 40))" } else { "$($p[0]) $($p[1]) $($p[2])" } })
        # "<name> <commit sha>": for an annotated tag, the peeled ("^{}") line gives the commit it points at.
        Remote = {
            $o = git -C $r ls-remote --tags origin 2>$null; if ($LASTEXITCODE -ne 0) { return $null }
            $map = [ordered]@{}
            foreach ($l in @($o)) {
                $sha, $ref = $l -split '\s+', 2
                $name = $ref -replace '^refs/tags/', ''
                if ($name.EndsWith('^{}')) { $map[$name.Substring(0, $name.Length - 3)] = $sha }
                elseif (-not $map.Contains($name)) { $map[$name] = $sha }
            }
            , @($map.Keys | ForEach-Object { "$_ $($map[$_])" })
        }.GetNewClosure()
    }
}

function Test-CheckReleaseTags([string]$Root) {
    $in = Get-ReleaseInputs $Root
    if ($null -eq $in -or $in.Log.Count -eq 0) { return New-Result $false 'release tags: no git history' }
    $rel = @($in.Log | Where-Object { $_ -match '^[0-9a-f]{40} chore\(release\): v(\d+\.\d+\.\d+)\b' })
    if ($rel.Count -eq 0) {
        $v = Get-Version $Root
        if ($v -and [version]$v -gt [version]'0.1.0') { return New-Result $false "release tags: version is $v but no chore(release) commit exists" }
        return New-Result $true 'release tags: 0/0'
    }
    $remote = & $in.Remote
    if ($null -eq $remote) { return New-Result $false 'release tags: origin unreachable' }
    $bad = @()
    foreach ($c in $rel) {
        $null = $c -match '^([0-9a-f]{40}) chore\(release\): v(\d+\.\d+\.\d+)'
        $sha = $Matches[1]; $tag = "v$($Matches[2])"
        $t = $in.Tags | Where-Object { ($_ -split ' ')[0] -eq $tag } | Select-Object -First 1
        if (-not $t) { $bad += "$tag missing"; continue }
        $p = $t -split ' '
        if ($p[1] -ne 'tag') { $bad += "$tag not annotated"; continue }
        if ($p[2] -ne $sha) { $bad += "$tag not on its release commit"; continue }
        $rt = $remote | Where-Object { ($_ -split ' ')[0] -eq $tag } | Select-Object -First 1
        if (-not $rt) { $bad += "$tag not pushed"; continue }
        if (($rt -split ' ')[1] -ne $sha) { $bad += "$tag on origin points elsewhere" }
    }
    if ($bad) { return New-Result $false "release tags: $($rel.Count - $bad.Count)/$($rel.Count) ($($bad -join ', '))" }
    return New-Result $true "release tags: $($rel.Count)/$($rel.Count)"
}

# ---------------------------------------------------------------- checks: data and paths (Epic D33, D34)

function Test-CheckDataInventory([string]$Root) {
    $inv = Read-Text $Root 'tools/data-inventory.json'
    if ($null -eq $inv) { return New-Result $false 'data inventory: tools/data-inventory.json not found' }
    $manifest = Get-PathsManifest $Root
    if ($null -eq $manifest) { return New-Result $false 'data inventory: tools/paths-manifest.txt not found' }
    $entries = @(($inv | ConvertFrom-Json).entries)
    if ($entries.Count -eq 0) { return New-Result $false 'data inventory: no entries' }
    $required = @($manifest | Where-Object { $_.Kind -ne 'tracked' } | ForEach-Object { $_.Glob })
    $pers = Read-Text $Root "$PkgRel/Services/Persistence.cs"
    if ($pers) { $required += @([regex]::Matches((Remove-CsComments $pers), 'const\s+string\s+\w+\s*=\s*"([^"]+)"') | ForEach-Object { $_.Groups[1].Value }) }
    $bad = @()
    foreach ($e in $entries) {
        foreach ($k in @('location', 'owner', 'retention', 'deletion', 'singleCopy')) {
            if ($null -eq $e.$k -or "$($e.$k)" -eq '') { $bad += "entry '$($e.artifact)' lacks $k" }
        }
    }
    foreach ($r in $required) {
        if (-not ($entries | Where-Object { @($_.covers) -contains $r })) { $bad += "'$r' has no entry" }
    }
    if ($bad) { return New-Result $false "data inventory: $($bad -join '; ')" }
    return New-Result $true "data inventory: $($required.Count)/$($required.Count) complete"
}

# Walked paths as "<kind> <path>" (kind tracked|ignored|server).
function Get-WalkedPaths([string]$Root) {
    if (Test-IsFixture $Root) {
        $w = Read-Text $Root 'walked.txt'
        if ($null -eq $w) { return @() }
        return @($w -split '\r?\n' | Where-Object { $_ })
    }
    $out = @()
    $out += @(Get-TreeFiles $Root -IncludeFixtures | ForEach-Object { "tracked $_" })
    $ign = git -C $Root status --ignored --porcelain 2>$null
    $out += @($ign | Where-Object { $_ -like '!! *' } | ForEach-Object { 'ignored ' + $_.Substring(3).Trim('"') })
    if (Test-Path $ServerPath) {
        $sp = (Resolve-Path $ServerPath).Path.TrimEnd('\')
        $cands = @()
        $cands += @(Get-ChildItem (Join-Path $sp 'BepInEx/plugins') -Filter 'Nyarlathotep*' -Force -ErrorAction SilentlyContinue)
        $cands += @(Get-ChildItem (Join-Path $sp 'BepInEx/config') -Filter '*Nyarlathotep*' -File -Force -ErrorAction SilentlyContinue)
        $cands += @(Get-ChildItem (Join-Path $sp 'BepInEx/config/Nyarlathotep') -Recurse -File -Force -ErrorAction SilentlyContinue)
        $cands += @(Get-ChildItem $sp -Directory -Filter 'save-data-nyar*' -Force -ErrorAction SilentlyContinue)
        $out += @($cands | ForEach-Object {
            $rel = $_.FullName.Substring($sp.Length + 1).Replace('\', '/')
            if ($_.PSIsContainer) { $rel += '/' }
            "server $rel"
        })
    }
    return $out
}

function Test-CheckPaths([string]$Root) {
    $manifest = Get-PathsManifest $Root
    if ($null -eq $manifest -or $manifest.Count -eq 0) { return New-Result $false 'paths: tools/paths-manifest.txt missing or empty' }
    $walked = @(Get-WalkedPaths $Root)
    if ($walked.Count -eq 0) { return New-Result $false 'paths: nothing walked' }
    $bad = @()
    foreach ($w in $walked) {
        $kind, $path = $w -split ' ', 2
        if (-not ($manifest | Where-Object { $_.Kind -eq $kind -and (Test-GlobMatch $path $_.Glob) })) { $bad += "$kind $path" }
    }
    if ($bad) { return New-Result $false "paths: $($bad.Count) outside the manifest: $(($bad | Select-Object -First 10) -join ', ')" }
    return New-Result $true "paths: $($walked.Count) walked, all in manifest"
}

# ---------------------------------------------------------------- runner

function Get-Manifest {
    if (-not (Test-Path $manifestPath)) { throw "tools/preflight-checks.json not found" }
    return Get-Content $manifestPath -Raw | ConvertFrom-Json
}

function Invoke-Check([string]$Function, [string]$Root) {
    try { return & $Function -Root $Root }
    catch { return New-Result $false "$Function threw: $($_.Exception.Message)" }
}

# Copy one stored fixture (good/, bad/ or empty/) into a scratch directory. ".gitkeep" only keeps an
# empty fixture in git and is never copied. A file named "<name>.b64plant" is written as <name> with its
# base64-decoded bytes: the secrets fixture plants its token that way so the repository itself never
# holds a token-shaped string.
function Copy-Fixture([string]$From, [string]$To) {
    if (-not (Test-Path $From)) { throw "fixture directory $From not found" }
    $base = (Resolve-Path $From).Path.TrimEnd('\', '/')
    foreach ($f in Get-ChildItem -Path $base -Recurse -File -Force) {
        if ($f.Name -eq '.gitkeep') { continue }
        $rel = $f.FullName.Substring($base.Length + 1)
        $dest = Join-Path $To $rel
        New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null
        if ($f.Name.EndsWith('.b64plant')) {
            $dest = $dest.Substring(0, $dest.Length - '.b64plant'.Length)
            [IO.File]::WriteAllBytes($dest, [Convert]::FromBase64String(([IO.File]::ReadAllText($f.FullName)).Trim()))
        } else { Copy-Item -LiteralPath $f.FullName -Destination $dest -Force }
    }
}

function Invoke-SelfTest {
    $manifest = Get-Manifest
    $checks = @($manifest.checks)
    $problems = @()
    # The manifest and the Test-Check functions defined in this file must be the same set.
    $src = Get-Content $PSCommandPath -Raw
    $defined = @([regex]::Matches($src, '(?m)^function (Test-Check\w+)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    $listed = @($checks | ForEach-Object { $_.function } | Sort-Object -Unique)
    if ($defined.Count -eq 0 -or $listed.Count -eq 0) { $problems += 'no checks defined or listed' }
    foreach ($d in $defined) { if ($listed -notcontains $d) { $problems += "$d is not in tools/preflight-checks.json" } }
    foreach ($l in $listed) { if ($defined -notcontains $l) { $problems += "$l is listed but not defined" } }
    # Every entry is complete and unique: name, function Test-Check<name>, mode, the fixture directory
    # tools/preflight-fixtures/<name>, a plant description per bad fixture, and the inputs it reads.
    foreach ($dup in @($checks | Group-Object name | Where-Object Count -gt 1)) { $problems += "check name '$($dup.Name)' listed twice" }
    foreach ($dup in @($checks | Group-Object function | Where-Object Count -gt 1)) { $problems += "function '$($dup.Name)' listed twice" }
    foreach ($c in $checks) {
        if ($c.function -ne "Test-Check$($c.name)") { $problems += "$($c.name): function must be Test-Check$($c.name)" }
        if (@('default', 'paths') -notcontains $c.mode) { $problems += "$($c.name): mode '$($c.mode)' is not default or paths" }
        if ($c.fixtures -ne "tools/preflight-fixtures/$($c.name)") { $problems += "$($c.name): fixtures must be tools/preflight-fixtures/$($c.name)" }
        if (@($c.inputs | Where-Object { "$_" -match '\S' }).Count -eq 0) { $problems += "$($c.name): no inputs listed" }
        if (@($c.plant.PSObject.Properties).Count -eq 0) { $problems += "$($c.name): no plant descriptions" }
    }

    $tmp = Join-Path ([IO.Path]::GetTempPath()) "nyar-selftest-$PID"
    $passed = 0; $extra = 0
    foreach ($c in $checks) {
        $ok = $true
        $fx = Join-Path $repoRoot $c.fixtures
        # good and empty, plus every bad fixture: bad/ and any bad-<n>/ (one planted fault each).
        $bads = @(Get-ChildItem $fx -Directory -Filter 'bad*' -ErrorAction SilentlyContinue | ForEach-Object Name | Sort-Object)
        if ($bads -notcontains 'bad') { $bads = @('bad') + $bads }
        $extra += $bads.Count - 1
        foreach ($b in $bads) { if (-not $c.plant.$b) { $ok = $false; $problems += "$($c.name): no plant description for $b" } }
        foreach ($kind in @(@('good') + $bads + @('empty'))) {
            if (-not (Test-Path (Join-Path $fx $kind))) { $ok = $false; $problems += "$($c.name): $kind fixture missing ($($c.fixtures)/$kind)"; continue }
            $dir = Join-Path $tmp "$($c.name)-$kind"
            if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
            New-Item -ItemType Directory -Force -Path $dir | Out-Null
            Copy-Fixture (Join-Path $fx $kind) $dir
            $script:FixtureRoot = $dir
            $r = Invoke-Check $c.function $dir
            $script:FixtureRoot = $null
            Write-Verbose "$($c.name) $kind -> $($r.Line)"
            $want = $kind -eq 'good'
            if ($r.Pass -ne $want) { $ok = $false; $problems += "$($c.name) $kind fixture: expected $(if ($want) {'pass'} else {'fail'}), got '$($r.Line)'" }
            if ($r.Line -notmatch '\S') { $ok = $false; $problems += "$($c.name) $kind fixture printed nothing" }
        }
        if ($ok) { $passed++ }
    }
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
    if ($problems) {
        Write-Host "selftest: $passed/$($checks.Count) checks — FAILED" -ForegroundColor Red
        $problems | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
        exit 1
    }
    Write-Host "selftest: $passed/$($checks.Count) checks, 3 fixtures each, $extra extra bad fixtures" -ForegroundColor Green
    exit 0
}

if ($SelfTest) { Invoke-SelfTest }

$manifest = Get-Manifest
$mode = if ($Paths) { 'paths' } else { 'default' }
$failures = @()
foreach ($c in @($manifest.checks | Where-Object { $_.mode -eq $mode })) {
    $r = Invoke-Check $c.function $repoRoot
    if ($r.Pass) { Write-Host $r.Line } else { Write-Host $r.Line -ForegroundColor Red; $failures += $r.Line }
}

if (-not $Paths) {
    # Informational only (not checks): README prose budget (docs/DOC_STYLE.md) and working-tree state.
    $pkgReadme = Join-Path $repoRoot "$PkgRel/README.md"
    if (Test-Path $pkgReadme) {
        $prose = @(Get-Content $pkgReadme | Where-Object { $t = $_.Trim(); $t -ne '' -and $t -notmatch '^(\||!\[|#|---|>)' }).Count
        if ($prose -gt 120) { Write-Host "note: Thunderstore README has $prose prose lines (soft budget ~120)" -ForegroundColor Yellow }
    }
    $dirty = git -C $repoRoot status --porcelain 2>$null
    if ($dirty) { Write-Host "note: working tree has uncommitted changes" } else { Write-Host 'note: working tree clean' }
}

if ($failures.Count -gt 0) {
    Write-Host ''
    Write-Host "PREFLIGHT FAILED: $($failures.Count) check(s)" -ForegroundColor Red
    exit 1
}
Write-Host ''
Write-Host 'PREFLIGHT OK. Manual reminder: scan both READMEs (root=GitHub, package=Thunderstore) for staleness and keep docs to docs/DOC_STYLE.md.' -ForegroundColor Green
exit 0
