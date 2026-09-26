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
    -ServerWrites -Snapshot <file>
                 : records the server directory and LocalLow\Stunlock Studios tree (path, size, write
                   time, SHA-256 under BepInEx/, logs/, save-data-*/ and LocalLow/) plus the owner's own test
                   server data at -LocalServerPath (C:\VRising-LocalServer, fully hashed, must not change at all)
                   to <file> and exits.
    -ServerWrites -Compare <file> [-AfterCleanup]
                 : Test-CheckServerWrites: every file created, changed or deleted since the snapshot must
                   match a server/external glob of tools/paths-manifest.txt; the only Saves folders that may
                   exist anywhere in the after snapshot (changed or not, manifested or not) are the development
                   world save-data-nyardev and the owner's untouched LocalServer; with -AfterCleanup
                   save-data-nyardev must be gone (spikes D4, foundation A1 and D34).
    -LogCheck    : Test-CheckLogCheck on the live BepInEx/LogOutput.log and the server's logs/NyarDev.log: prints
                   "log check: <n> unhandled, <s> nyar lines, <o> orphan errors, <u> unity errors [kinds]" and exits 1
                   when either log is missing or empty, BepInEx's holds a stack frame from our assembly ("at
                   Nyarlathotep." after any indentation or prefix) or has no "[nyar" line, or the server log holds an
                   orphan error from loading a save (foundation D33, A10). Run it after every in-game session, before
                   the next boot overwrites the logs.
    -SessionsOf <slug>
                 : Test-CheckSessionLogs: every "### Session <n>" under docs/features/<SLUG>.md › Test results has a
                   "- session <n> log check: 0 unhandled, <s> nyar lines, 0 orphan errors, <u> unity errors" line in
                   docs/audits/<slug>.md, one per session; only sessions after A10 count (at least one), an orphan
                   count above 0 fails them, and pre-A10 sessions are listed with their orphan errors named (D33, A10);
                   every listed Unity error kind needs a '  - unity "<kind>": game <why>' sub-bullet, or
                   'ours <why> (A<n>)' citing its amendment (A13).
    -AuthSuite   : the authorization suite (raphael-api-core D7): dotnet test over AuthorizationTests and ApiAccessTests
                   (0 tests run is a failure), then the commands, admin-list and gateway checks, then .nyar api version,
                   status and sub public and .nyar api events adminOnly; prints "auth suite: pass (tests, commands, admin
                   list, gateway)".
    -ListCommands admin
                 : every admin-only command of the commands walk, one per line, then "admin commands: <n>"
                   (foundation D19); Test-CheckAdminList keeps that list equal to the commands check's count.
    -AuditOf <slug>
                 : Test-CheckAuditSteps: docs/audits/<slug>.md has a pre-audit and a post-audit entry
                   with a Codex verdict for every Build plan step of docs/dod/<slug>.md (spikes D17).

    Checks read files relative to -Root. In the real repository the file set is git's
    (git ls-files --cached --others --exclude-standard) minus tools/preflight-fixtures/, whose bad
    fixtures deliberately break the rules (the secrets scan alone includes the fixtures); in a fixture
    the file set is every file under the fixture. Git-derived and live inputs (commit subjects, tags,
    the path walk, the Compile items, the server snapshots, the audited slug) come from git, msbuild or
    the server in the real repository and from captured text files (git-log.txt, tags.txt,
    remote-tags.txt, walked.txt, compile-items.txt, git-files.txt, before.tsv, after.tsv,
    aftercleanup.txt, auditof.txt) in a fixture, written in the format the real collection produces.

.EXAMPLE
    pwsh tools/preflight.ps1
    pwsh tools/preflight.ps1 -Paths
    pwsh tools/preflight.ps1 -SelfTest
    pwsh tools/preflight.ps1 -ServerWrites -Snapshot $env:TEMP\nyarfoundation-before.tsv
    pwsh tools/preflight.ps1 -ServerWrites -Compare $env:TEMP\nyarfoundation-before.tsv
    pwsh tools/preflight.ps1 -AuditOf spikes
    pwsh tools/preflight.ps1 -LogCheck
    pwsh tools/preflight.ps1 -SessionsOf foundation
    pwsh tools/preflight.ps1 -ListCommands admin
#>

[CmdletBinding()]
param(
    [switch]$SelfTest,
    [switch]$Paths,
    [switch]$ServerWrites,          # with -Snapshot <file> (record) or -Compare <file> [-AfterCleanup] (check)
    [string]$Snapshot,
    [string]$Compare,
    [switch]$AfterCleanup,
    [switch]$LogCheck,              # the live BepInEx/LogOutput.log after an in-game session (foundation D33)
    [string]$AuditOf,               # plan slug: check its audit record covers every Build plan step
    [string]$SessionsOf,            # plan slug: check every in-game session has a clean log check line (foundation D33)
    [switch]$AuthSuite,             # the authorization suite: its tests, then the commands, admin-list and gateway checks (raphael-api-core D7)
    [ValidateSet('', 'admin')]
    [string]$ListCommands = '',     # 'admin': print every admin-only command, then "admin commands: <n>" (foundation D19)
    [string]$ServerPath = 'C:\Program Files (x86)\Steam\steamapps\common\VRisingDedicatedServer',
    [string]$LocalLowPath = (Join-Path $env:USERPROFILE 'AppData\LocalLow\Stunlock Studios'),
    [string]$LocalServerPath = 'C:\VRising-LocalServer'
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

# Remove // line comments and /* */ block comments so documentation never counts as a call. One
# left-to-right regex lexes string and char literals too (raw """…""", verbatim @"…", interpolated
# $"…", regular "…", '…'), so "//" or "/*" inside a literal never swallows the code after it. Literals
# are kept as written; a block comment becomes one space so it cannot join two tokens. A raw literal's "$"
# prefix belongs to it, so $$"""…""" is one literal. Known limit: a quote nested inside an interpolation hole
# ($"{(a ? "x" : "y")}") ends the literal early; Get-ParenEnd then fails safe (see there).
$script:CsLexRx = [regex]::new(
    '(?<raw>\$*(?<q>"{3,})[\s\S]*?\k<q>)|(?<vs>(?:@\$?|\$@)"(?:[^"]|"")*")|(?<s>\$?"(?:[^"\\\n]|\\.)*")|(?<c>''(?:[^''\\\n]|\\.)*'')|(?<lc>//[^\n]*)|(?<bc>/\*[\s\S]*?(?:\*/|\z))')
function Remove-CsComments([string]$Text) {
    if (-not $Text) { return $Text }
    return $script:CsLexRx.Replace($Text, {
        param($m)
        if ($m.Groups['lc'].Success) { return '' }
        if ($m.Groups['bc'].Success) { return ' ' }
        return $m.Value
    })
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

# "tracked: <glob>" / "ignored: <glob>" / "server: <glob>" / "external: <glob>" lines of
# tools/paths-manifest.txt.
function Get-PathsManifest([string]$Root) {
    $t = Read-Text $Root 'tools/paths-manifest.txt'
    if ($null -eq $t) { return $null }
    return @($t -split '\r?\n' | Where-Object { $_ -match '^(tracked|ignored|server|external):\s*(\S.*)$' } |
        ForEach-Object { $null = $_ -match '^(tracked|ignored|server|external):\s*(\S.*)$'; [pscustomobject]@{ Kind = $Matches[1]; Glob = $Matches[2].Trim() } })
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

# Every bool Bind in [Announcements] of Config/Settings.cs defaults to false, and the section binds the five switches
# WaveWarnings, EventBanners, DailyBanner, LoginStats and PlayerShare (Epic D41, foundation D17).
$script:AnnouncementSwitches = @('WaveWarnings', 'EventBanners', 'DailyBanner', 'LoginStats', 'PlayerShare')

function Test-CheckAnnouncementDefaults([string]$Root) {
    $settings = Read-Text $Root "$PkgRel/Config/Settings.cs"
    if ($null -eq $settings) { return New-Result $false 'announcement defaults: Config/Settings.cs not found' }
    $binds = [regex]::Matches((Remove-CsComments $settings), 'Bind\(\s*"Announcements"\s*,\s*"([^"]+)"\s*,\s*(true|false)\b')
    $names = @($binds | ForEach-Object { $_.Groups[1].Value })
    $missing = @($script:AnnouncementSwitches | Where-Object { $names -notcontains $_ })
    if ($missing) { return New-Result $false "announcement defaults: switch not bound: $($missing -join ', ')" }
    $on = @($binds | Where-Object { $_.Groups[2].Value -ne 'false' } | ForEach-Object { $_.Groups[1].Value })
    if ($on) { return New-Result $false "announcement defaults: ON by default: $($on -join ', ')" }
    return New-Result $true "announcement defaults: all off ($($binds.Count) switches)"
}

$script:PublicCommands = @('nyar', 'status', 'help', 'me', 'top', 'hide', 'show', 'version', 'sub')   # Epic D5 (A4)

function Test-CheckCommands([string]$Root) {
    $admin = 0; $public = 0; $bad = @()
    foreach ($c in @(Get-CommandWalk $Root)) {
        if (-not $c.InCommands) { $bad += "$($c.Name) outside Commands/ ($($c.File))"; continue }
        if ($c.Admin) { $admin++ }
        elseif ($script:PublicCommands -contains $c.Name) { $public++ }
        else { $bad += "$($c.Name) not adminOnly ($($c.File))" }
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
    # The fenced helpers themselves (AddComponentSafe, RemoveComponentSafe, AddBufferSafe, DestroySafe) are the
    # sanctioned route and may be called anywhere.
    # DestroyUtility.Destroy*(em, entity) is the deferred destroy; its entity is the second argument (spikes A13).
    $rx = '(?:\.(?<op>AddComponent(?!Safe\b)\w*|RemoveComponent(?!Safe\b)\w*|AddBuffer|DestroyEntity)\s*(?:<[^>]*>)?\s*\(\s*|\b(?<op>DestroyUtility\.Destroy\w*)\s*\(\s*(?:[^,()]|\((?:[^()]|\([^()]*\))*\))+,\s*)(?<arg>[^,)\s]+)'
    $fence = "$PkgRel/EntityExtensions.cs"
    $bad = @(); $guarded = 0
    foreach ($f in $cs) {
        $text = Remove-CsComments (Read-Text $Root $f)
        $calls = [regex]::Matches($text, $rx)
        if ($calls.Count -eq 0) { continue }
        if ($f -ne $fence) { $bad += "$($calls[0].Groups['op'].Value) in $f"; continue }
        # Inside the fence, every structural call must be preceded, in its own method, by an early
        # refusal of Prefab entities.
        foreach ($c in $calls) {
            $start = Get-EnclosingMethodStart $text $c.Index
            if ($start -lt 0) { $bad += "$($c.Groups['op'].Value) in $f outside a method"; continue }
            $before = $text.Substring($start, $c.Index - $start)
            if (-not (Test-PrefabRefusal $before $c.Groups['arg'].Value)) { $bad += "$($c.Groups['op'].Value) in $f without an earlier Prefab refusal" }
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
    # file-name literal is one of the four data files or their .bak/.tmp/.corrupt siblings, and the directory is
    # built from BepInEx's config path plus "Nyarlathotep".
    $pers = Read-Text $Root $fence
    $persNote = 'no Persistence.cs yet'
    if ($pers) {
        $p = Remove-CsComments $pers
        foreach ($m in [regex]::Matches($p, '(?m)^\s*(?:(?:public|internal|protected)\s+)(?:static\s+)?[\w<>\[\],.? ]+?\s+(\w+)\s*\(([^)]*)\)')) {
            if ($m.Groups[2].Value -match '(?i)\bstring\s+\w*(path|file|name|dir|folder)\w*') { $bad += "Persistence.$($m.Groups[1].Value) takes a path or file-name parameter" }
        }
        # Outside log calls, the only string literals allowed are the data-file names, their .bak/.tmp/.corrupt
        # suffixes and the folder name, so no other file name can be spelled or assembled (an interpolated
        # string outside a log call fails too).
        $noLogs = [regex]::Replace($p, '\bLog\w*\s*\((?:[^()]|\((?:[^()]|\([^()]*\))*\))*\)', 'LOG()')
        $allowed = '^((events|zones|state|stats)\.json(\.bak|\.tmp|\.corrupt)?|\.bak|\.tmp|\.corrupt|Nyarlathotep)$'
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
            foreach ($a in $paths) {
                # File.Move(src, dst, true) and File.Copy's overwrite flag are not paths.
                if ($a -match '^(overwrite\s*:\s*)?(true|false)$') { continue }
                if ($pathVars.Count -eq 0 -or $a -notmatch $okArg) { $bad += "Persistence.cs writes to '$a', not a path from its folder" }
            }
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
    '(TCLI_AUTH_TOK[E]N|GH_TOK[E]N)\s*[=:]\s*\S+'
)

# A tools/ script may not read a credential or the environment beyond an allow-list (raphael-api-core D10, A4). The
# rule counts, not spells: in a PowerShell script every reference to the environment drive (the drive name followed by
# a colon, in any cmdlet, variable or braced variable) must be an allowed read, $env or ${env} with a name below; in a
# Python script every environ or getenv token must be an allowed os read; in a Node script every env token must be an
# allowed process read; so an import, alias or destructuring leaves a token over and fails. In every script .NET's
# environment reads, the gh credential command (gh or gh.exe), the POSIX environment printer and the tcli token
# variable's name fail. This comment and the patterns are written so that their own text passes the rule.
$script:ToolsEnvAllowed = 'TEMP|TMP|USERPROFILE|LOCALAPPDATA|APPDATA|SteamAppId|NYAR_SESSION_DIR'
$script:ToolsCredentialPatterns = @(
    '\bgh(?:\.exe)?["'']?\s+auth\s+token\b',
    '\bprint[e]nv\b',
    'GetEnvironmentVariabl[e]',
    'TCLI_AUTH_TOK[E]N'
)
# Shell and batch scripts expand any variable with no marker to count, so tools/ holds none (Codex step 2 round 3).
$script:ToolsForbiddenExt = @('.sh', '.bash', '.cmd', '.bat')
# Per language: the token that reaches the environment, and the one form of it that is an allowed read.
$script:ToolsEnvRules = @(
    @{ Ext = @('.ps1', '.psm1'); Any = '(?i)\ben[v]:'; Allowed = "(?i)\`$(?:en[v]:(?:$script:ToolsEnvAllowed)\b|\{en[v]:(?:$script:ToolsEnvAllowed)\})" },
    @{ Ext = @('.py'); Any = '\b(?:enviro[n]|gete[n]v)\b'; Allowed = "\bos\.(?:enviro[n](?:\.get\s*\(|\[)|gete[n]v\s*\()\s*[`"'](?:$script:ToolsEnvAllowed)[`"']" },
    @{ Ext = @('.mjs', '.js'); Any = '\ben[v]\b'; Allowed = "\bprocess\.en[v]\.(?:$script:ToolsEnvAllowed)\b" }
)

# The first credential or environment access in a tools/ script that is not allowed, or $null.
function Find-ToolsEnvAccess([string]$Ext, [string]$Text) {
    if ($script:ToolsForbiddenExt -contains $Ext) { return "a $Ext script (tools/ scripts are PowerShell, Python or Node)" }
    foreach ($p in $script:ToolsCredentialPatterns) { if ($Text -match $p) { return $Matches[0] } }
    foreach ($r in $script:ToolsEnvRules) {
        if ($r.Ext -notcontains $Ext) { continue }
        $any = [regex]::Matches($Text, $r.Any).Count
        $ok = [regex]::Matches($Text, $r.Allowed).Count
        if ($any -gt $ok) { return "$($any - $ok) environment access(es) outside the allow-list" }
    }
    return $null
}
$script:ScriptExt = @('.ps1', '.psm1', '.mjs', '.js', '.py', '.sh', '.bash', '.cmd', '.bat')
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
        if ($ext -eq '.cs' -and (Remove-CsComments $t) -match 'Environment\.GetEnvironmentVariabl[e]') { $hits += "$f (reads the environment)" }
        if ($f -like 'tools/*' -and $f -notlike 'tools/preflight-fixtures/*' -and $script:ScriptExt -contains $ext) {
            $why = Find-ToolsEnvAccess $ext $t
            if ($why) { $hits += "$f (reads a credential or the environment: $why)" }
        }
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
        $found = git -C $Root grep --cached -I -l -P 'Environment\.GetEnvironmentVariabl[e]' -- '*.cs' 2>&1
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
        # Read now: a GetNewClosure() block runs in its own module scope, which cannot see this script's
        # functions when the script is invoked with "& ./tools/preflight.ps1".
        $remoteTags = @((Read-Text $Root 'remote-tags.txt') -split '\r?\n' | Where-Object { $_ })
        return [pscustomobject]@{
            Log    = @($log -split '\r?\n' | Where-Object { $_ })
            Tags   = @((Read-Text $Root 'tags.txt') -split '\r?\n' | Where-Object { $_ })
            Remote = { , $remoteTags }.GetNewClosure()
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

# ---------------------------------------------------------------- checks: the spikes child (spikes D4, D13, D17)

# Spike code is recognised by naming (docs/dod/spikes.md Business rules 5): the namespace
# Nyarlathotep.Spikes, any identifier beginning with "Spike", or a command group or command whose name has
# "spike" as a word, such as "spike" or "nyar spike" (short, namespace- or global::-qualified attribute name, with or without the Attribute suffix, first in its
# attribute list or not, positional or name: argument, regular or verbatim literal, any letter case).
# A using-alias for the attribute and a name that is not one literal count as spike code (Get-SpikeReason).
$script:SpikeCodeRx = '\bnamespace\s+Nyarlathotep\.Spikes\b|\bSpike\w*|[\[,]\s*(?:[\w.:]*[.:])?Command(?:Group)?(?:Attribute)?\s*\(\s*(?:name\s*:\s*)?@?"(?i:(?:[^"\\]*\s)?spike(?:\s[^"\\]*)?)"'

# The project's Compile items as repository-relative paths (outside obj/), and the .cs files git sees.
# A fixture supplies both as captured files: compile-items.txt (one Identity per line, as msbuild prints
# it) and git-files.txt (one path per line); without them the fixture's own .cs files stand in for both.
function Get-SpikeSources([string]$Root) {
    if (Test-IsFixture $Root) {
        $cs = @(Get-CsFiles $Root)
        $ci = Read-Text $Root 'compile-items.txt'
        $gf = Read-Text $Root 'git-files.txt'
        $compile = if ($ci) { @($ci -split '\r?\n' | Where-Object { $_ } | ForEach-Object { "$PkgRel/" + $_.Replace('\', '/') }) } else { $cs }
        $git = if ($gf) { @($gf -split '\r?\n' | Where-Object { $_ }) } else { $cs }
        return [pscustomobject]@{ Compile = $compile; Git = $git; Ok = $true }
    }
    $json = dotnet msbuild (Join-Path $Root "$PkgRel/Nyarlathotep.csproj") -getItem:Compile 2>$null
    if ($LASTEXITCODE -ne 0) { return [pscustomobject]@{ Ok = $false } }
    $items = @((($json -join "`n") | ConvertFrom-Json).Items.Compile | ForEach-Object { $_.Identity.Replace('\', '/') } |
        Where-Object { $_ -notmatch '^obj/' } | ForEach-Object { "$PkgRel/$_" })
    return [pscustomobject]@{ Compile = $items; Git = @(Get-TreeFiles $Root | Where-Object { $_ -like '*.cs' }); Ok = $true }
}

# The name argument of every Command/CommandGroup attribute (the name: argument, else the first
# positional one), as source text. The argument list is read to its matching ")" with string literals
# skipped, then split at top-level commas.
function Get-CommandNameArgs([string]$Code) {
    $names = @()
    foreach ($m in [regex]::Matches($Code, '[\[,]\s*(?:[\w.:]*[.:])?Command(?:Group)?(?:Attribute)?\s*\(')) {
        $parts = [System.Collections.Generic.List[string]]::new(); $cur = [System.Text.StringBuilder]::new()
        $depth = 1; $inStr = $false
        for ($i = $m.Index + $m.Length; $i -lt $Code.Length -and $depth -gt 0; $i++) {
            $ch = $Code[$i]
            if ($inStr) { if ($ch -eq [char]'\') { [void]$cur.Append($ch); $i++; if ($i -lt $Code.Length) { [void]$cur.Append($Code[$i]) }; continue } elseif ($ch -eq [char]'"') { $inStr = $false } }
            elseif ($ch -eq [char]'"') { $inStr = $true }
            elseif ($ch -eq [char]'(') { $depth++ }
            elseif ($ch -eq [char]')') { $depth--; if ($depth -eq 0) { break } }
            elseif ($ch -eq [char]',' -and $depth -eq 1) { $parts.Add($cur.ToString()); [void]$cur.Clear(); continue }
            [void]$cur.Append($ch)
        }
        $parts.Add($cur.ToString())
        $named = @($parts | Where-Object { $_ -match '^\s*name\s*:' })
        $arg = if ($named) { $named[0] -replace '^\s*name\s*:\s*', '' } else { @($parts | Where-Object { $_ -notmatch '^\s*\w+\s*:' })[0] }
        $names += "$arg".Trim()
    }
    return $names
}

# Why a file counts as spike code, or $null. A command or group name that is not one plain string
# literal (a constant, a concatenation, nameof) cannot be checked, so it counts as spike code: every
# command name in this project is written as a literal.
function Get-SpikeReason([string]$Code) {
    # C# reads \uXXXX and \UXXXXXXXX escapes inside identifiers (SpikeHelper is SpikeHelper): decode
    # them everywhere first; decoding inside string literals only makes the check stricter.
    $Code = [regex]::Replace($Code, '\\u([0-9A-Fa-f]{4})|\\U([0-9A-Fa-f]{8})', {
        param($m) [char]::ConvertFromUtf32([Convert]::ToInt32($m.Groups[1].Value + $m.Groups[2].Value, 16)) })
    # An alias for a command attribute (using C = VampireCommandFramework.CommandAttribute;) would hide the
    # attribute's name from the scan below, so any alias for one counts as spike code.
    if ($Code -match '\busing\s+\w+\s*=\s*(?:[\w.:]*[.:])?Command(?:Group)?(?:Attribute)?\s*;') { return "alias for a command attribute: $($Matches[0])" }
    if ($Code -cmatch $script:SpikeCodeRx) { return "spike identifier, namespace or command: '$($Matches[0])'" }
    foreach ($a in Get-CommandNameArgs $Code) {
        if ($a -notmatch '^@?"([^"\\]*)"$') { return "command name not a string literal: $a" }
        if ($Matches[1] -imatch '(^|\s)spike(\s|$)') { return "command named ""$($Matches[0].Trim())"" in ""$a""" }
    }
    return $null
}

function Test-CheckSpikeCode([string]$Root) {
    $src = Get-SpikeSources $Root
    if (-not $src.Ok) { return New-Result $false 'spike code: dotnet msbuild -getItem:Compile failed' }
    $all = @(@($src.Compile) + @($src.Git) | Sort-Object -Unique)
    if ($all.Count -eq 0) { return New-Result $false 'spike code: no source found' }
    # A Compile item git does not see (an ignored file) fails whatever its name.
    $hidden = @($src.Compile | Where-Object { $src.Git -notcontains $_ })
    if ($hidden) { return New-Result $false "spike code: Compile item(s) ignored by git: $($hidden -join ', ')" }
    $found = @($all | ForEach-Object {
        $t = Read-Text $Root $_
        if ($t) { $why = Get-SpikeReason (Remove-CsComments $t); if ($why) { "$_ ($why)" } }
    })
    if ($found.Count -eq 0) { return New-Result $true 'spike code: none' }
    $plan = Read-Text $Root 'docs/dod/spikes.md'
    if ($plan -and (Get-Frontmatter $plan)['status'] -eq 'in-progress') { return New-Result $true 'spike code: present, allowed while spikes is in-progress' }
    return New-Result $false "spike code: present in $($found -join ', ')"
}

# A snapshot line is "<path>`t<size>`t<last-write ticks>`t<sha256 or ->" for a file and
# "<path>/`t-`t-`tdir" for a directory, so an empty folder (another save's Saves/, a leftover
# save-data-nyardev/) is seen too. Paths are relative to the server directory, or "LocalLow/<path>"
# under %USERPROFILE%\AppData\LocalLow\Stunlock Studios, or "LocalServer/<path>" under the owner's own test
# server data (-LocalServerPath; its world1 must come through the spikes byte for byte, spikes A4). Contents
# are hashed where a write matters (BepInEx/, logs/, save-data-*/, LocalLow/, LocalServer/); a file that cannot be hashed after three tries, or a folder
# that cannot be listed, is a problem that fails the snapshot and the comparison, never a silent row.
$script:HashedRx = '^(BepInEx/|logs/|save-data-|LocalLow/|LocalServer/)'
# The one world tests may run in (foundation A1): the only Saves folder besides the owner's LocalServer.
$script:DevWorld = 'save-data-nyardev'
function Get-ServerTree {
    $rows = [System.Collections.Generic.List[string]]::new()
    $problems = [System.Collections.Generic.List[string]]::new()
    foreach ($r in @(@{ Base = $ServerPath; Prefix = '' }, @{ Base = $LocalLowPath; Prefix = 'LocalLow/' }, @{ Base = $LocalServerPath; Prefix = 'LocalServer/' })) {
        if (-not (Test-Path $r.Base)) { continue }
        $base = (Resolve-Path $r.Base).Path.TrimEnd('\')
        $walkErrors = $null
        $items = @(Get-ChildItem $base -Recurse -Force -ErrorAction SilentlyContinue -ErrorVariable walkErrors)
        foreach ($e in @($walkErrors)) { $problems.Add("not listable: $(Format-SafePath ($r.Prefix + "$($e.TargetObject)".Replace($base, '').TrimStart('\').Replace('\', '/')))") }
        foreach ($f in $items) {
            $rel = $r.Prefix + $f.FullName.Substring($base.Length + 1).Replace('\', '/')
            if ($f.PSIsContainer) { $rows.Add("$rel/`t-`t-`tdir"); continue }
            $sha = '-'
            if ($rel -match $script:HashedRx) {
                $sha = $null
                foreach ($try in 1..3) {
                    try { $sha = (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256 -ErrorAction Stop).Hash; break }
                    catch { Start-Sleep -Milliseconds 200 }
                }
                if (-not $sha) { $problems.Add("not hashable: $(Format-SafePath $rel)"); $sha = 'unhashed' }
            }
            $rows.Add("$rel`t$($f.Length)`t$($f.LastWriteTimeUtc.Ticks)`t$sha")
        }
    }
    return [pscustomobject]@{ Rows = $rows; Problems = $problems }
}

# LocalLow/VRising holds CloudSaves/<SteamID>/...: a printed LocalLow path keeps its first two segments only.
function Format-SafePath([string]$Path) {
    if ($Path -match '^(LocalLow/[^/]+)/.') { return "$($Matches[1])/..." }
    return $Path
}

function Read-Snapshot([string]$Text) {
    $h = @{}
    foreach ($l in ($Text -split '\r?\n')) {
        if (-not $l -or $l.StartsWith('#')) { continue }
        $p = $l -split "`t"
        if ($p.Count -eq 4) { $h[$p[0]] = "$($p[1])`t$($p[2])`t$($p[3])" }
    }
    return $h
}

function Test-CheckServerWrites([string]$Root) {
    $problems = @()
    if (Test-IsFixture $Root) {
        $beforeText = Read-Text $Root 'before.tsv'; $afterText = Read-Text $Root 'after.tsv'
        $cleanup = $null -ne (Read-Text $Root 'aftercleanup.txt')
    } else {
        if (-not $Compare -or -not (Test-Path -LiteralPath $Compare)) { return New-Result $false "server writes: snapshot '$Compare' not found (take one with -ServerWrites -Snapshot <file>)" }
        if (-not (Test-Path $ServerPath)) { return New-Result $false "server writes: server directory $ServerPath not found" }
        $tree = Get-ServerTree
        $beforeText = [IO.File]::ReadAllText($Compare); $afterText = $tree.Rows -join "`n"; $cleanup = [bool]$AfterCleanup
        $problems = @($tree.Problems)
    }
    if (-not $beforeText -or -not $afterText) { return New-Result $false 'server writes: a snapshot is missing' }
    $before = Read-Snapshot $beforeText; $after = Read-Snapshot $afterText
    if ($before.Count -eq 0) { return New-Result $false 'server writes: the before snapshot is empty' }
    # A hashed file whose row holds no SHA-256 (locked, unreadable) would compare equal while its content
    # changed: either snapshot holding one fails.
    foreach ($s in @(@{ Name = 'before'; Rows = $before }, @{ Name = 'after'; Rows = $after })) {
        foreach ($k in $s.Rows.Keys) {
            if ($k.EndsWith('/') -or $k -notmatch $script:HashedRx) { continue }
            if (($s.Rows[$k] -split "`t")[2] -notmatch '^[0-9A-F]{64}$') { $problems += "$($s.Name) snapshot has no hash for $(Format-SafePath $k)" }
        }
    }
    if ($problems) { return New-Result $false "server writes: $(@($problems | Sort-Object -Unique | Select-Object -First 10) -join '; ') (stop the server and the game client, then retry)" }
    $manifest = Get-PathsManifest $Root
    if ($null -eq $manifest) { return New-Result $false 'server writes: tools/paths-manifest.txt not found' }
    $globs = @($manifest | Where-Object { $_.Kind -in 'server', 'external' } | ForEach-Object { $_.Glob })
    # Files are compared; directories (keys ending "/") only feed the two existence rules below.
    $files = { param($keys) @($keys | Where-Object { -not $_.EndsWith('/') }) }
    $created = @(& $files $after.Keys | Where-Object { -not $before.ContainsKey($_) })
    $changed = @(& $files $after.Keys | Where-Object { $before.ContainsKey($_) -and $before[$_] -ne $after[$_] })
    $deleted = @(& $files $before.Keys | Where-Object { -not $after.ContainsKey($_) })
    $bad = @()
    # The allow-list is a constant, not the manifest: a server glob that covers another world never authorises
    # it (foundation A1). The owner's own test server data (LocalServer/) exists by design, so it is exempt from
    # the Saves-folder existence rule, but any created, changed or deleted file under it fails.
    $isOtherSave = { param($p) $p -match '(^|/)Saves/' -and $p -notlike "$($script:DevWorld)/*" -and $p -notlike 'LocalServer/*' }
    foreach ($p in @($created + $changed + $deleted)) {
        if ($p -like 'LocalServer/*') { $bad += "owner data touched: $(Format-SafePath $p)"; continue }
        if (& $isOtherSave $p) { $bad += "another save touched: $(Format-SafePath $p)"; continue }
        if (-not ($globs | Where-Object { Test-GlobMatch $p $_ })) { $bad += "unmanifested: $(Format-SafePath $p)" }
    }
    $others = @($after.Keys | Where-Object { & $isOtherSave $_ } | ForEach-Object { ($_ -split '/Saves/')[0] } | Sort-Object -Unique)
    foreach ($o in $others) { $bad += "another Saves folder exists: $(Format-SafePath "$o/Saves")" }
    if ($cleanup -and @($after.Keys | Where-Object { $_ -like "$($script:DevWorld)/*" }).Count -gt 0) { $bad += "$($script:DevWorld) still exists" }
    if ($bad) { return New-Result $false "server writes: $(@($bad | Sort-Object -Unique | Select-Object -First 10) -join '; ')" }
    return New-Result $true "server writes: $($created.Count) created, $($changed.Count) changed, $($deleted.Count) deleted, all in manifest, no other save"
}

# After an in-game session (foundation D33): a stack frame from our assembly fails, and so does a log without
# a "[nyar" line (the wrong log, or the plugin never initialised). The server may still hold the logs open, so
# they are read with shared access. BepInEx.cfg sets WriteUnityLog = false, so Unity's own errors are only in the
# server log (-logFile .\logs\NyarDev.log): an orphan error there (an entity link the game could not restore while
# loading a save) fails too, and the Unity errors are counted and listed by kind for the reader to attribute (A10).
# A fixture holds LogOutput.log and NyarDev.log.
function Read-SharedText([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    $fs = [IO.File]::Open($Path, 'Open', 'Read', 'ReadWrite')
    try { return [IO.StreamReader]::new($fs).ReadToEnd() } finally { $fs.Dispose() }
}

$script:OrphanPattern = 'is trying to attach to Entity\.Null|points at buff\.Target|Failed to remap Entity-field|Could not (re)?map '

function Test-CheckLogCheck([string]$Root) {
    if (Test-IsFixture $Root) { $log = Read-Text $Root 'LogOutput.log'; $server = Read-Text $Root 'NyarDev.log' }
    else {
        $log = Read-SharedText (Join-Path $ServerPath 'BepInEx/LogOutput.log')
        $server = Read-SharedText (Join-Path $ServerPath 'logs/NyarDev.log')
    }
    if ($null -eq $log -or $log -notmatch '\S') { return New-Result $false 'log check: no log (BepInEx/LogOutput.log missing or empty)' }
    if ($null -eq $server -or $server -notmatch '\S') { return New-Result $false 'log check: no server log (logs/NyarDev.log missing or empty)' }
    $lines = $log -split '\r?\n'
    # A frame is "at Nyarlathotep.<type>.<method>(" after any indentation or prefix (IL2CPP, Mono and logger-wrapped
    # traces differ, Mono puts a space before "("); the "(" keeps prose such as "look at Nyarlathotep.Core" out (foundation step 2 Codex rounds 1-3).
    $n = @($lines | Where-Object { $_ -match '(?<![\w.])at\s+Nyarlathotep\.[\w.`+<>\[\],]*[ \t]*\(' }).Count
    $s = @($lines | Where-Object { $_.Contains('[nyar') }).Count
    $slines = $server -split '\r?\n'
    $o = @($slines | Where-Object { $_ -match $script:OrphanPattern }).Count
    # A Unity error is a message, then "UnityEngine.DebugLogHandler:LogFormat", "UnityEngine.Logger:Log" and
    # "UnityEngine.Debug:LogError"; its kind is the message's last line with numbers masked (and never a SteamID).
    $kinds = [Collections.Generic.List[string]]::new(); $u = 0
    for ($i = 2; $i -lt $slines.Count; $i++) {
        if (-not $slines[$i].StartsWith('UnityEngine.Debug:LogError(')) { continue }
        $u++
        $j = $i - 1; while ($j -ge 0 -and $slines[$j].StartsWith('UnityEngine.')) { $j-- }
        $msg = if ($j -ge 0) { $slines[$j] } else { '' }
        $kind = ($msg -replace '-?\d+', 'N' -replace '\|', '/').Trim()
        if ($kind.Length -gt 90) { $kind = $kind.Substring(0, 90) + '...' }
        if (-not $kinds.Contains($kind)) { $kinds.Add($kind) }
    }
    $line = "log check: $n unhandled, $s nyar lines, $o orphan errors, $u unity errors"
    # Every kind is listed: -SessionsOf needs each one attributed in the session's audit line (A13).
    if ($kinds.Count) { $line += " [$($kinds -join ' | ')]" }
    if ($s -eq 0) { return New-Result $false "$line (the wrong log, or the plugin did not initialise)" }
    return New-Result ($n -eq 0 -and $o -eq 0) $line
}

# The Build plan's numbered steps and, in docs/audits/<slug>.md, one "### Step <n>" entry per step under
# a "## Pre-audit" heading and under a "## Post-audit" heading, each post-audit entry with a
# "Codex verdict:" line. A fixture names the slug in auditof.txt.
function Test-CheckAuditSteps([string]$Root) {
    $slug = if (Test-IsFixture $Root) { "$(Read-Text $Root 'auditof.txt')".Trim() } else { $AuditOf }
    if (-not $slug) { return New-Result $false 'audit steps: no plan named (-AuditOf <slug>)' }
    $plan = Read-Text $Root "docs/dod/$slug.md"
    if ($null -eq $plan) { return New-Result $false "audit steps: docs/dod/$slug.md not found" }
    $bp = [regex]::Match($plan, '(?ms)^## Build plan\s*$(.*?)(?=^## |\z)')
    $steps = if ($bp.Success) { @([regex]::Matches($bp.Groups[1].Value, '(?m)^(\d+)\. ') | ForEach-Object { [int]$_.Groups[1].Value } | Sort-Object -Unique) } else { @() }
    if ($steps.Count -eq 0) { return New-Result $false "audit steps: docs/dod/$slug.md has no Build plan steps" }
    # Steps are numbered 1..N without a gap. Removing steps from the plan is a plan edit, which the dod
    # skill records as an amendment; this check audits the plan as it stands.
    if ($steps[-1] -ne $steps.Count) { return New-Result $false "audit steps: docs/dod/$slug.md Build plan steps are not numbered 1..$($steps[-1]) without a gap ($($steps -join ', '))" }
    $audit = Read-Text $Root "docs/audits/$slug.md"
    if ($null -eq $audit) { return New-Result $false "audit steps: docs/audits/$slug.md not found" }
    $pre = @{}; $post = @{}; $codex = @{}
    foreach ($sec in [regex]::Matches($audit, '(?ms)^## (Pre-audit|Post-audit)\s*$(.*?)(?=^## |\z)')) {
        foreach ($e in [regex]::Matches($sec.Groups[2].Value, '(?ms)^### Step (\d+)\b(.*?)(?=^### |\z)')) {
            $n = [int]$e.Groups[1].Value
            if ($sec.Groups[1].Value -eq 'Pre-audit') { $pre[$n] = $true }
            else { $post[$n] = $true; if ($e.Groups[2].Value -match '(?m)^- Codex verdict:\s*\S') { $codex[$n] = $true } }
        }
    }
    $np = @($steps | Where-Object { $pre[$_] }).Count; $nq = @($steps | Where-Object { $post[$_] }).Count; $nc = @($steps | Where-Object { $codex[$_] }).Count
    $line = "audit steps: $slug $np/$($steps.Count) pre, $nq/$($steps.Count) post, $nc/$($steps.Count) Codex verdicts"
    return New-Result ($np -eq $steps.Count -and $nq -eq $steps.Count -and $nc -eq $steps.Count) $line
}

# ---------------------------------------------------------------- checks: the foundation child (foundation D11, D17-D19, D33)

# An interpolated string literal with its text masked to spaces and its holes kept: the holes are code, so a
# call inside one still counts, while a "(" or a name in the text can neither unbalance a parenthesis span nor
# count as a use (step 3 Codex rounds 2-3). Handles $"…", $@"…", @$"…" and raw $$"""…""" (a hole opens with as
# many "{" as the literal has "$"). Inside a hole a nested string's text is masked too. Newlines are kept, so line
# numbers still match the source.
function Hide-InterpolatedText([string]$Lit) {
    $i = 0; $dollars = 0; $verbatim = $false
    while ($i -lt $Lit.Length -and ($Lit[$i] -eq '$' -or $Lit[$i] -eq '@')) { if ($Lit[$i] -eq '$') { $dollars++ } else { $verbatim = $true }; $i++ }
    $q = 0; while ($i + $q -lt $Lit.Length -and $Lit[$i + $q] -eq '"') { $q++ }
    $raw = $q -ge 3
    $quote = if ($raw) { $q } else { 1 }
    $end = $Lit.Length - $quote
    $sb = [Text.StringBuilder]::new()
    [void]$sb.Append($Lit.Substring(0, $i + $quote))
    $need = if ($raw) { $dollars } else { 1 }
    $j = $i + $quote
    while ($j -lt $end) {
        $c = $Lit[$j]
        if ($c -eq '{') {
            $run = 0; while ($j + $run -lt $end -and $Lit[$j + $run] -eq '{') { $run++ }
            if ((-not $raw) -and $run -ge 2 -and $run % 2 -eq 0) { [void]$sb.Append(' ' * $run); $j += $run; continue }   # {{ escapes
            if ($run -lt $need) { [void]$sb.Append(' ' * $run); $j += $run; continue }
            [void]$sb.Append('{' * $run); $j += $run
            # the hole: copy code up to the "}" that closes it, masking any string nested inside it
            $depth = 1; $inStr = $false
            while ($j -lt $end -and $depth -gt 0) {
                $h = $Lit[$j]
                if ($inStr) {
                    if ($h -eq '\') { [void]$sb.Append('  '); $j += 2; continue }
                    if ($h -eq '"') { $inStr = $false; [void]$sb.Append('"') } else { [void]$sb.Append($(if ($h -eq "`n" -or $h -eq "`r") { $h } else { ' ' })) }
                    $j++; continue
                }
                if ($h -eq '"') { $inStr = $true }
                elseif ($h -eq '{') { $depth++ }
                elseif ($h -eq '}') { $depth--; if ($depth -eq 0) { break } }
                [void]$sb.Append($h); $j++
            }
            while ($j -lt $end -and $Lit[$j] -eq '}') { [void]$sb.Append('}'); $j++ }
            continue
        }
        if ((-not $raw) -and (-not $verbatim) -and $c -eq '\') { [void]$sb.Append('  '); $j += 2; continue }
        [void]$sb.Append($(if ($c -eq "`n" -or $c -eq "`r") { $c } else { ' ' }))
        $j++
    }
    [void]$sb.Append($Lit.Substring([Math]::Max($end, $j)))
    return $sb.ToString()
}

# Every plain string and char literal blanked to "" / '' after comments are removed, so an identifier, a "(" or a
# directive inside a literal never counts. An interpolated string keeps its holes (Hide-InterpolatedText).
function Remove-CsLiterals([string]$Text) {
    if (-not $Text) { return $Text }
    $clean = Remove-CsComments $Text
    return $script:CsLexRx.Replace($clean, {
        param($m)
        if ($m.Groups['c'].Success) { return "''" }
        if ($m.Groups['lc'].Success -or $m.Groups['bc'].Success) { return $m.Value }
        if ($m.Value.StartsWith('$') -or $m.Value.StartsWith('@$')) { return Hide-InterpolatedText $m.Value }
        return '""'
    })
}

# Index of the ")" matching the "(" at $Open, or -1 when it is unmatched. An unmatched span contains nothing, so
# a use after it counts as outside Gateway.Run: the check fails rather than passes when parsing goes wrong.
function Get-ParenEnd([string]$Text, [int]$Open) {
    $depth = 0
    for ($i = $Open; $i -lt $Text.Length; $i++) {
        if ($Text[$i] -eq '(') { $depth++ }
        elseif ($Text[$i] -eq ')') { $depth--; if ($depth -eq 0) { return $i } }
    }
    return -1
}

# The services the gateway dispatches to, by name (plan D11 lists EventRuntime, SpawnTracker, WaveAction and
# Persistence; EventStore holds the definition load). Only these may declare [Mutating] methods, and they may call
# each other directly ("other than Logic/ActionGateway.cs and the services it dispatches to"). A [Mutating]
# declaration anywhere else fails, so no file can exempt itself by declaring one. UnitSetup is SpawnTracker's setup
# step for a unit it has just spawned (foundation step 4); its Apply is [Mutating], so only these services call it.
# Announcer runs ActionKind.Announce (`.nyar announce`, foundation step 6).
$script:DispatchedServices = @('EventRuntime', 'SpawnTracker', 'UnitSetup', 'WaveAction', 'Persistence', 'EventStore', 'Announcer') |
    ForEach-Object { "$PkgRel/Services/$_.cs" }

# Every method marked [Mutating] in a dispatched service is a mutating method. Any other file under Commands/,
# Patches/ or Services/ may name a mutating method only inside the parentheses of a Gateway.Run(...) call; every named use inside such a call, in any of the
# walked files, is a call site. A use is the identifier anywhere except its own declaration, so a method group
# captured outside Gateway.Run and passed in later fails too (foundation D11, Epic D36).
function Test-CheckGatewayOnly([string]$Root) {
    $dirs = @("$PkgRel/Commands/", "$PkgRel/Patches/", "$PkgRel/Services/")
    $files = @(Get-CsFiles $Root | Where-Object { $f = $_; @($dirs | Where-Object { $f.StartsWith($_) }).Count -gt 0 })
    $texts = @{}
    foreach ($f in $files) { $texts[$f] = Remove-CsLiterals (Read-Text $Root $f) }
    $decl = '\[Mutating\]\s*(?:\[[^\]]*\]\s*)*(?:(?:public|internal|private|protected|static|override|virtual|async|sealed)\s+)+[\w<>\[\],.? ]+?\s+(\w+)\s*\('
    $mutating = @{}    # name -> declaring files
    $declAt = @{}      # "<file>|<index>" of each declaration's name
    foreach ($f in $files) {
        foreach ($m in [regex]::Matches($texts[$f], $decl)) {
            $n = $m.Groups[1].Value
            if (-not $mutating.ContainsKey($n)) { $mutating[$n] = @() }
            $mutating[$n] += $f
            $declAt["$f|$($m.Groups[1].Index)"] = $true
        }
    }
    if ($mutating.Count -eq 0) { return New-Result $false 'gateway: no [Mutating] method found under Commands/, Patches/ or Services/' }
    $dispatched = $script:DispatchedServices
    $strays = @($mutating.Values | ForEach-Object { $_ } | Sort-Object -Unique | Where-Object { $dispatched -notcontains $_ })
    if ($strays) { return New-Result $false "gateway: [Mutating] declared outside the dispatched services in $($strays -join ', ')" }
    $sites = 0; $bad = @()
    foreach ($f in $files) {
        $t = $texts[$f]
        $spans = @(foreach ($g in [regex]::Matches($t, '\bGateway\s*\.\s*Run\s*\(')) {
            $open = $g.Index + $g.Length - 1
            , @($open, (Get-ParenEnd $t $open))
        })
        foreach ($name in $mutating.Keys) {
            foreach ($u in [regex]::Matches($t, "\b$name\b")) {
                if ($declAt.ContainsKey("$f|$($u.Index)")) { continue }
                $inside = @($spans | Where-Object { $_[1] -ge 0 -and $u.Index -gt $_[0] -and $u.Index -lt $_[1] }).Count -gt 0
                if ($inside) { $sites++ }
                elseif ($dispatched -notcontains $f) { $bad += "$name used outside Gateway.Run in $f" }
            }
        }
    }
    if ($bad) { return New-Result $false "gateway: $(@($bad | Select-Object -Unique) -join '; ')" }
    return New-Result $true "gateway: only ActionGateway mutates ($sites call sites)"
}

# Every "FaultInjection" in a .cs file (comments and literals aside) lies inside an "#if DEBUG" branch, so a
# Release DLL has no such key and no code reading it (foundation D17, D25; Epic D25). Nested #if blocks are
# tracked; the #else or #elif branch of "#if DEBUG" is not debug-only.
function Test-CheckFaultInjection([string]$Root) {
    $cs = Get-CsFiles $Root
    if ($cs.Count -eq 0) { return New-Result $false 'fault injection: no source files found' }
    $refs = 0; $bad = @()
    foreach ($f in $cs) {
        $stack = New-Object System.Collections.Generic.List[bool]   # per open #if: is this branch DEBUG-only
        $lineNo = 0
        foreach ($line in ((Remove-CsLiterals (Read-Text $Root $f)) -split '\r?\n')) {
            $lineNo++
            $d = $line.Trim()
            if ($d -match '^#\s*if\s+(.+)$') { $stack.Add($Matches[1].Trim() -eq 'DEBUG'); continue }
            if ($d -match '^#\s*(else|elif)\b') { if ($stack.Count) { $stack[$stack.Count - 1] = $false }; continue }
            if ($d -match '^#\s*endif\b') { if ($stack.Count) { $stack.RemoveAt($stack.Count - 1) }; continue }
            $n = [regex]::Matches($line, '\bFaultInjection\b').Count
            if ($n -eq 0) { continue }
            $refs += $n
            if (-not $stack.Contains($true)) { $bad += "$f line $lineNo" }
        }
    }
    if ($bad) { return New-Result $false "fault injection: outside #if DEBUG at $($bad -join ', ')" }
    return New-Result $true "fault injection: debug-only ($refs references)"
}

# The commands walk shared by the commands check, the admin list and -ListCommands: every [Command] in any .cs
# file git sees, with its full chat form (the name of the nearest [CommandGroup] above it in its file, if any, then
# the command name), so a file holding two groups labels each command with its own (raphael-api-core D9).
function Get-CommandWalk([string]$Root) {
    foreach ($f in Get-CsFiles $Root) {
        $text = Remove-CsComments (Read-Text $Root $f)
        $groups = @([regex]::Matches($text, '\[CommandGroup(?:Attribute)?\s*\(\s*(?:name\s*:\s*)?"([^"]*)"'))
        foreach ($m in [regex]::Matches($text, '\[Command(?:Attribute)?\s*\((?<args>(?:[^()]|\((?:[^()])*\))*)\)\s*\]')) {
            $above = @($groups | Where-Object { $_.Index -lt $m.Index })
            $group = if ($above.Count) { $above[-1].Groups[1].Value } else { '' }
            $a = $m.Groups['args'].Value
            $name = if ($a -match '^\s*(?:name\s*:\s*)?"([^"]*)"') { $Matches[1] } else { '?' }
            [pscustomobject]@{
                Name       = $name
                Full       = '.' + ((@($group, $name) | Where-Object { $_ }) -join ' ')
                Admin      = $a -match 'adminOnly\s*:\s*true'
                File       = $f
                InCommands = $f -like "$PkgRel/Commands/*"
            }
        }
    }
}

# Every wire tag the plugin builds and every `.nyar api` command it answers is in the "Tags and commands" table of
# docs/RAPHAEL_INTEGRATION_CONTRACT.md as IMPLEMENTED with an api no newer than "**Current api:** <n>", and Wire.Api
# equals that n (Epic D38; raphael-api-core D8, A4). A tag is the literal first argument of a Wire.Record or Wire.Line
# call anywhere in the plugin (Record( or Line( inside Logic/Wire.cs itself); a call with a non-literal tag fails, an
# alias or static import of Wire fails,
# and so does any "[NYAR:" string literal in the plugin outside Logic/Wire.cs, so no line can bypass the builder.
function Test-CheckWireContract([string]$Root) {
    $wireRel = "$PkgRel/Logic/Wire.cs"
    # The plugin's own sources; Nyarlathotep.Tests holds expected wire lines as literals by design (A3).
    $cs = @(Get-CsFiles $Root | Where-Object { $_ -like "$PkgRel/*" })
    if ($cs.Count -eq 0) { return New-Result $false 'wire contract: no source files found' }
    $tags = @{}; $bad = @()
    foreach ($f in $cs) {
        $raw = Read-Text $Root $f
        $code = Remove-CsComments $raw
        if ($f -ne $wireRel) {
            foreach ($lit in $script:CsLexRx.Matches($code)) {
                if (-not ($lit.Groups['lc'].Success -or $lit.Groups['bc'].Success -or $lit.Groups['c'].Success) -and $lit.Value.Contains('[NYAR:')) {
                    $bad += "a [NYAR: literal in $f"
                }
            }
        }
        # Wire is reached only as "Wire." outside its own file: an alias (using W = …Wire;) or a static import
        # (using static …Wire;) would hide a call from this check, so either fails (A4).
        if ($f -ne $wireRel -and $code -match '(?m)^\s*(?:global\s+)?using\s+(?:static\s+|\w+\s*=\s*)[\w.:\s]*?\bWire\s*;') {
            $bad += "an alias or static import of Wire in $f"
        }
        $rx = if ($f -eq $wireRel) { '(?<![\w.])(?<!string\s)(?<m>Record|Line)\s*\(' } else { '\bWire\s*\.\s*(?<m>Record|Line)\s*\(' }
        foreach ($call in [regex]::Matches($code, $rx)) {
            $rest = $code.Substring($call.Index + $call.Length)
            $arg = [regex]::Match($rest, '^\s*"([a-z][a-z0-9-]*)"\s*[,)]')
            if (-not $arg.Success) { $bad += "a $($call.Groups['m'].Value) call in $f whose tag is not a literal"; continue }
            $tags[$arg.Groups[1].Value] = $true
        }
    }
    $apiCmds = @(Get-CommandWalk $Root | Where-Object { $_.Full -like '.nyar api *' } | ForEach-Object { $_.Name } | Sort-Object -Unique)
    if ($tags.Count -eq 0) { return New-Result $false 'wire contract: no tag found' }
    $wire = Read-Text $Root $wireRel
    $contract = Read-Text $Root 'docs/RAPHAEL_INTEGRATION_CONTRACT.md'
    if (-not $contract) { return New-Result $false 'wire contract: docs/RAPHAEL_INTEGRATION_CONTRACT.md not found' }
    $codeApi = if ($wire -and $wire -match 'const\s+int\s+Api\s*=\s*(\d+)\s*;') { [int]$Matches[1] } else { -1 }
    $docApi = if ($contract -match '\*\*Current api:\*\*\s*(\d+)') { [int]$Matches[1] } else { -1 }
    if ($codeApi -lt 0) { $bad += 'Wire.Api not found' }
    if ($docApi -lt 0) { $bad += 'the contract has no "**Current api:** <n>"' }
    if ($codeApi -ge 0 -and $docApi -ge 0 -and $codeApi -ne $docApi) { $bad += "Wire.Api is $codeApi but the contract's current api is $docApi" }
    $table = [regex]::Match($contract, '(?ms)^### Tags and commands.*?(?=^#|\z)')
    $rows = @{}
    foreach ($r in [regex]::Matches($table.Value, '(?m)^\|\s*`([^`]+)`\s*\|\s*(tag|command)\s*\|\s*([^|]*?)\s*\|\s*([^|]*?)\s*\|')) {
        $rows["$($r.Groups[2].Value) $($r.Groups[1].Value)"] = @($r.Groups[3].Value, $r.Groups[4].Value)
    }
    foreach ($item in @(@($tags.Keys | Sort-Object | ForEach-Object { "tag $_" }) + @($apiCmds | ForEach-Object { "command $_" }))) {
        if (-not $rows.ContainsKey($item)) { $bad += "$item is not in the contract's Tags and commands table"; continue }
        $status, $api = $rows[$item]
        if ($status -ne 'IMPLEMENTED') { $bad += "$item is $status in the contract"; continue }
        if ($api -notmatch '^\d+$' -or ($docApi -ge 0 -and [int]$api -gt $docApi)) { $bad += "$item has api '$api' in the contract" }
    }
    if ($bad) { return New-Result $false "wire contract: $(@($bad | Select-Object -Unique) -join '; ')" }
    return New-Result $true "wire contract: $($tags.Count) tags, $($apiCmds.Count) api commands, all documented (api $codeApi)"
}

# The admin list (-ListCommands admin) holds every admin-only command of the walk, wherever it is declared; the
# commands check counts only those under Commands/. The two counts must be equal, so an admin command declared
# anywhere else fails here even when it is admin-only (foundation D19).
function Test-CheckAdminList([string]$Root) {
    $walk = @(Get-CommandWalk $Root)
    if ($walk.Count -eq 0) { return New-Result $false 'admin list: no commands found' }
    # Every command's full form, admin or public, is the start of a command documented in docs/NYARLATHOTEP_DESIGN.md
    # § 6, so a command labelled with the wrong group (".nyar events" for ".nyar api events") fails (raphael-api-core D9).
    $documented = @(Get-DocumentedCommands $Root)
    if ($documented.Count -eq 0) { return New-Result $false 'admin list: no command table in docs/NYARLATHOTEP_DESIGN.md § 6' }
    $undocumented = @($walk | Where-Object {
            $words = @($_.Full -split ' ')
            @($documented | Where-Object { $_.Count -ge $words.Count -and (($_[0..($words.Count - 1)]) -join ' ') -eq ($words -join ' ') }).Count -eq 0
        } | ForEach-Object { "$($_.Full) ($($_.File))" })
    if ($undocumented) { return New-Result $false "admin list: not in the design doc's command table: $($undocumented -join '; ')" }
    $listed = @($walk | Where-Object Admin).Count
    $counted = @($walk | Where-Object { $_.Admin -and $_.InCommands }).Count
    if ($listed -ne $counted) {
        $outside = @($walk | Where-Object { $_.Admin -and -not $_.InCommands } | ForEach-Object { "$($_.Full) ($($_.File))" })
        return New-Result $false "admin list: $listed listed but the commands check counts $counted; outside Commands/: $($outside -join '; ')"
    }
    return New-Result $true "admin list: $listed admin commands, equal to the commands check; $($walk.Count) commands documented"
}

# The commands of the table in docs/NYARLATHOTEP_DESIGN.md § 6, each as its word list up to its first argument:
# every backticked form in a row's first cell that starts with ".nyar", with "a|b" words expanded into each choice.
function Get-DocumentedCommands([string]$Root) {
    $doc = Read-Text $Root 'docs/NYARLATHOTEP_DESIGN.md'
    if (-not $doc) { return }
    $sec = [regex]::Match($doc, '(?ms)^## 6\. Commands.*?(?=^## |\z)')
    if (-not $sec.Success) { return }
    foreach ($row in [regex]::Matches($sec.Value, '(?m)^\|((?:[^|\n\\]|\\.)*)\|')) {
        foreach ($code in [regex]::Matches($row.Groups[1].Value, '`(\.nyar[^`]*)`')) {
            $words = @()
            foreach ($w in ($code.Groups[1].Value.Replace('\|', '|') -split '\s+')) {
                if ($w -eq '' -or $w -match '^[\[<…]' -or $w -eq '...') { break }
                $words += , @($w -split '\|')
            }
            $forms = @(, @())
            foreach ($choices in $words) { $forms = @(foreach ($f in $forms) { foreach ($c in $choices) { , (@($f) + $c) } }) }
            foreach ($f in $forms) { , $f }
        }
    }
}

# Every [Command] method, in any .cs file git sees, has as its first statement
# "if (!Core.IsReady) { <ctx>.Reply(Messages.StillLoading); return; }", <ctx> being its first parameter, so a command
# typed before the mod is ready replies "still loading" and does nothing (foundation D39, A12). Literals and comments
# are blanked first, so a "(" in a description cannot move the parameter list; an expression body fails.
function Test-CheckReadyGuard([string]$Root) {
    $n = 0; $bad = @()
    $guard = '\G\s*if\s*\(\s*!\s*Core\s*\.\s*IsReady\s*\)\s*\{\s*{0}\s*\.\s*Reply\s*\(\s*Messages\s*\.\s*StillLoading\s*\)\s*;\s*return\s*;\s*\}'
    foreach ($f in Get-CsFiles $Root) {
        $t = Remove-CsLiterals (Read-Text $Root $f)
        foreach ($m in [regex]::Matches($t, '\[Command(?:Attribute)?\s*\((?:[^()]|\((?:[^()])*\))*\)\s*\]')) {
            $n++
            $at = $m.Index + $m.Length
            $skip = [regex]::new('\G\s*(?:\[(?:[^\[\]]|\[[^\]]*\])*\]\s*)*').Match($t, $at)
            $open = $t.IndexOf('(', $skip.Index + $skip.Length)
            $name = if ($open -gt 0 -and $t.Substring($at, $open - $at) -match '(\w+)\s*$') { $Matches[1] } else { '?' }
            $close = if ($open -gt 0) { Get-ParenEnd $t $open } else { -1 }
            if ($close -lt 0) { $bad += "$f ${name}: no parameter list"; continue }
            $params = $t.Substring($open + 1, $close - $open - 1)
            if ($params -notmatch '^\s*(?:this\s+)?[\w.<>]+\s+(\w+)') { $bad += "$f ${name}: no context parameter"; continue }
            $ctx = $Matches[1]
            $brace = [regex]::new('\G\s*\{').Match($t, $close + 1)
            if (-not $brace.Success) { $bad += "$f ${name}: no block body"; continue }
            $rx = [regex]::new($guard.Replace('{0}', [regex]::Escape($ctx)))
            if (-not $rx.Match($t, $brace.Index + $brace.Length).Success) {
                $bad += "$f ${name}: first statement is not the IsReady guard"
            }
        }
    }
    if ($n -eq 0) { return New-Result $false 'ready guard: no commands found' }
    if ($bad) { return New-Result $false "ready guard: $($n - $bad.Count)/$n commands start with the IsReady guard; $($bad -join '; ')" }
    return New-Result $true "ready guard: $n/$n commands start with the IsReady guard"
}

# Every "### Session <n> · <date>" under "## Test results" in docs/features/<SLUG>.md has exactly one line
# "- session <n> log check: 0 unhandled, <s> nyar lines, 0 orphan errors, <u> unity errors" in docs/audits/<slug>.md
# (foundation D33). Only sessions after the plan's last pre-A10 session (below; a plan not listed has none) count as
# checked, and there must be at least one. Pre-A10 lines may omit both counts and give the server log's orphan count
# in a "  - before A10" sub-bullet; they are listed with any orphan errors named, never counted as clean (Review 10).
# From the first line carrying the counts on, every line must carry them. A line with Unity errors carries -LogCheck's
# "[kind | kind]" list, and each kind has a sub-bullet '  - unity "<kind>": game <why>' or '... ours <why> (A<n>)',
# ours citing the amendment that records it (A13).
# A fixture names the slug in sessionsof.txt.
$script:SessionsBeforeA10 = @{ 'foundation' = 7 }

function Test-CheckSessionLogs([string]$Root) {
    $slug = if (Test-IsFixture $Root) { "$(Read-Text $Root 'sessionsof.txt')".Trim() } else { $SessionsOf }
    if (-not $slug) { return New-Result $false 'session logs: no plan named (-SessionsOf <slug>)' }
    $docRel = "docs/features/$($slug.ToUpperInvariant().Replace('-', '_')).md"
    $doc = Read-Text $Root $docRel
    $audit = Read-Text $Root "docs/audits/$slug.md"
    if ($null -eq $doc) { return New-Result $false "session logs: $docRel not found" }
    if ($null -eq $audit) { return New-Result $false "session logs: docs/audits/$slug.md not found" }
    $results = [regex]::Match($doc, '(?ms)^## Test results\s*$(.*?)(?=^## |\z)')
    $sessions = if ($results.Success) { @([regex]::Matches($results.Groups[1].Value, '(?m)^### Session (\d+) · ') | ForEach-Object { [int]$_.Groups[1].Value } | Sort-Object -Unique) } else { @() }
    if ($sessions.Count -eq 0) { return New-Result $false "session logs: $slug has no sessions under $docRel › Test results" }
    $checks = @{}; $dupes = @(); $unattributed = @()
    $pattern = '(?m)^- session (\d+) log check: (\d+) unhandled, (\d+) nyar lines(?:, (\d+) orphan errors, (\d+) unity errors)?(?<rest>[^\r\n]*)(?:\r?\n  - before A10[^\r\n]*?\b(\d+) orphan errors)?'
    foreach ($m in [regex]::Matches($audit, $pattern)) {
        $n = [int]$m.Groups[1].Value
        if ($m.Groups[5].Success -and [int]$m.Groups[5].Value -gt 0) {
            $list = [regex]::Match($m.Groups['rest'].Value, '^ \[(.+)\]\s*$')
            $kinds = if ($list.Success) { @($list.Groups[1].Value -split ' \| ' | ForEach-Object { $_.Trim() } | Where-Object { $_ }) } else { @() }
            $subs = [regex]::Match($audit.Substring($m.Index), '^[^\r\n]*((?:\r?\n  - [^\r\n]*)*)').Groups[1].Value
            $bare = @($kinds | Where-Object { $subs -notmatch ('(?m)^  - unity "' + [regex]::Escape($_) + '": (game \S|ours [^\r\n]*\bA\d+\b)') })
            if ($kinds.Count -eq 0 -or $bare.Count) { $unattributed += $n }
        }
        # Two lines for one session: a clean copy must not hide a dirty one (Codex A9 round 1).
        if ($checks.ContainsKey($n)) { $dupes += $n }
        $orphans = if ($m.Groups[4].Success) { [int]$m.Groups[4].Value } else { -1 }
        $before = if ($m.Groups[6].Success) { [int]$m.Groups[6].Value } else { 0 }
        $checks[$n] = @([int]$m.Groups[2].Value, [int]$m.Groups[3].Value, $orphans, $before)
    }
    $cutoff = if ($script:SessionsBeforeA10.ContainsKey($slug)) { $script:SessionsBeforeA10[$slug] } else { 0 }
    $pre = @($sessions | Where-Object { $_ -le $cutoff })
    $post = @($sessions | Where-Object { $_ -gt $cutoff })
    $missing = @($sessions | Where-Object { -not $checks.ContainsKey($_) })
    $first = @($sessions | Where-Object { $checks.ContainsKey($_) -and $checks[$_][2] -ge 0 } | Select-Object -First 1)
    # Every line needs 0 unhandled and at least one [nyar line (-LogCheck fails a log without one); after A10 also no
    # orphan error and both counts.
    $dirty = @($sessions | Where-Object { $checks.ContainsKey($_) -and ($checks[$_][0] -ne 0 -or $checks[$_][1] -eq 0 -or ($_ -gt $cutoff -and $checks[$_][2] -gt 0)) })
    $old = @($sessions | Where-Object { $checks.ContainsKey($_) -and $checks[$_][2] -lt 0 -and ($_ -gt $cutoff -or ($first -and $_ -gt $first[0])) })
    $dupes = @($dupes | Sort-Object -Unique)
    $unattributed = @($unattributed | Sort-Object -Unique)
    $bad = @($dirty + $old + $dupes + $unattributed | Sort-Object -Unique)
    $ok = @($post | Where-Object { $checks.ContainsKey($_) -and $bad -notcontains $_ }).Count
    $preOrphans = @($pre | Where-Object { $checks.ContainsKey($_) -and ([math]::Max($checks[$_][2], 0) + $checks[$_][3]) -gt 0 })
    $preNote = if ($pre.Count) { "; $($pre.Count) before A10 not counted$(if ($preOrphans) { " (orphan errors in session $($preOrphans -join ', '))" })" } else { '' }
    if ($missing -or $bad -or $post.Count -eq 0) {
        $why = @()
        if ($post.Count -eq 0) { $why += "no session after A10 (session $cutoff)" }
        if ($missing) { $why += "no log check line for session $($missing -join ', ')" }
        if ($dirty) { $why += "unhandled exceptions, no nyar lines or orphan errors in session $($dirty -join ', ')" }
        if ($old) { $why += "no orphan count in session $($old -join ', ')" }
        if ($dupes) { $why += "more than one log check line for session $($dupes -join ', ')" }
        if ($unattributed) { $why += "unity error kinds not listed or not attributed in session $($unattributed -join ', ')" }
        return New-Result $false "session logs: $slug $ok/$($post.Count) checked after A10$preNote ($($why -join '; '))"
    }
    return New-Result $true "session logs: $slug $ok/$($post.Count) checked after A10$preNote"
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
        if (@('default', 'paths', 'serverwrites', 'auditof', 'logcheck', 'sessionsof') -notcontains $c.mode) { $problems += "$($c.name): mode '$($c.mode)' is not default, paths, serverwrites, auditof, logcheck or sessionsof" }
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

if ($ServerWrites -and $Snapshot) {
    if (-not (Test-Path $ServerPath)) { Write-Host "server writes: server directory $ServerPath not found" -ForegroundColor Red; exit 1 }
    $tree = Get-ServerTree
    if ($tree.Problems.Count) {
        Write-Host "server writes: snapshot not written: $(@($tree.Problems | Select-Object -First 10) -join '; ') (stop the server and the game client, then retry)" -ForegroundColor Red
        exit 1
    }
    @("# nyar server snapshot $(Get-Date -Format o)") + $tree.Rows | Set-Content -LiteralPath $Snapshot -Encoding utf8
    Write-Host "server writes: snapshot of $($tree.Rows.Count) files and folders written to $Snapshot"
    exit 0
}

if ($ListCommands -eq 'admin') {
    $admins = @(Get-CommandWalk $repoRoot | Where-Object Admin | Sort-Object Full)
    $admins | ForEach-Object { Write-Host $_.Full }
    Write-Host "admin commands: $($admins.Count)"
    exit 0
}

if ($AuthSuite) {
    # Every direct and indirect path of the actor matrix in one run (raphael-api-core D7): the ActionKind × actor and
    # row/push access tests, then the checks that keep adminOnly, the admin list and the gateway honest, then the
    # public/admin split of the api commands. A filter that runs no test is a failure.
    $parts = @(); $fail = @()
    # Each class runs on its own, so a deleted, renamed or fully skipped class is "no tests ran", not hidden by the other.
    $testsOk = $true
    foreach ($cls in @('AuthorizationTests', 'ApiAccessTests')) {
        $out = & dotnet test (Join-Path $repoRoot 'Nyarlathotep/Nyarlathotep.Tests') --filter "FullyQualifiedName~Nyarlathotep.Tests.$cls." 2>&1 | Out-String
        $code = $LASTEXITCODE
        $ran = if ($out -match 'Passed:\s*(\d+)') { [int]$Matches[1] } else { 0 }
        $failed = if ($out -match 'Failed:\s*(\d+)') { [int]$Matches[1] } else { 0 }
        $skipped = if ($out -match 'Skipped:\s*(\d+)') { [int]$Matches[1] } else { 0 }
        if ($code -ne 0 -or $failed -gt 0) { $fail += "$cls failed ($failed)"; $testsOk = $false }
        elseif ($ran -eq 0) { $fail += "${cls}: no tests ran"; $testsOk = $false }
        elseif ($skipped -gt 0) { $fail += "${cls}: $skipped skipped"; $testsOk = $false }
    }
    if ($testsOk) { $parts += 'tests' }
    foreach ($c in @(@('commands', 'Test-CheckCommands'), @('admin list', 'Test-CheckAdminList'), @('gateway', 'Test-CheckGatewayOnly'))) {
        $r = Invoke-Check $c[1] $repoRoot
        if ($r.Pass) { $parts += $c[0] } else { $fail += $r.Line }
    }
    $walk = @(Get-CommandWalk $repoRoot)
    foreach ($want in @(@('.nyar api version', $false), @('.nyar api status', $false), @('.nyar api sub', $false), @('.nyar api events', $true))) {
        $cmd = @($walk | Where-Object Full -eq $want[0])
        if ($cmd.Count -ne 1) { $fail += "$($want[0]) not found once"; continue }
        if ($cmd[0].Admin -ne $want[1]) { $fail += "$($want[0]) must be $(if ($want[1]) { 'adminOnly' } else { 'public' })" }
    }
    if ($fail) { Write-Host "auth suite: FAILED — $($fail -join '; ')" -ForegroundColor Red; exit 1 }
    Write-Host "auth suite: pass ($($parts -join ', '))" -ForegroundColor Green
    exit 0
}

$manifest = Get-Manifest
$mode = if ($Paths) { 'paths' } elseif ($ServerWrites) { 'serverwrites' } elseif ($AuditOf) { 'auditof' } elseif ($LogCheck) { 'logcheck' } elseif ($SessionsOf) { 'sessionsof' } else { 'default' }
$failures = @()
foreach ($c in @($manifest.checks | Where-Object { $_.mode -eq $mode })) {
    $r = Invoke-Check $c.function $repoRoot
    if ($r.Pass) { Write-Host $r.Line } else { Write-Host $r.Line -ForegroundColor Red; $failures += $r.Line }
}

if ($mode -eq 'default') {
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
