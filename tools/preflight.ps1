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
    -Paths       : the paths walk (Test-CheckPaths) — run after dotnet build, tcli build and a deploy. It also fails on
                   any %TEMP%\nyar-* folder, any git worktree besides the main one, and any tag on origin or GitHub
                   release that no remote-tag: / remote-release: line of tools/paths-manifest.txt declares
                   (faction-empowerment D26).
    -RollbackOf <slug> [-From <tag> -To <tag>]
                 : Test-CheckRollbackRoutes (event-library D29): docs/dod/<slug>.md › Rollout › Rollback must hold the
                   bullets **In the repository:** with `git revert --no-edit <From>..<To>`, **On the dev server during
                   the build:** naming tools/dev-snapshot.ps1, **On a server:** with "install the <From version> DLL"
                   and "after data is written", **Published release:** with "never deleted" and "withdrawn by
                   retitling", and **Commit range:** exactly <From>..<To>. From and To are those of the plan's own
                   `rollback-gate.ps1 -From <a> -To <b> -Plan <slug>` command; -From and -To, when given (the rollback
                   gate passes its own), must equal them. → "rollback routes: <slug> 5/5".
    -Paths -DeclaredOf <slug>
                 : the paths walk, then every path the child's build created or changed must be covered by a path
                   token of docs/dod/<slug>.md › Rollout › Paths walked, whatever the manifest globs say
                   (event-library D30, A13). Created or changed: git diff --name-only against the commit of the
                   audit's "### Step 1 · <date> · <sha>" pre-audit heading, the untracked paths, the walk's ignored
                   and server paths holding a file written after that commit, and the %TEMP%\nyar-* folders present.
                   → "paths: <n> walked, all in manifest; declared: <k>/<k> in <slug>".
    -SelfTest    : every check against its three fixtures; then every command of the externalSelfTests registry
                   (tools/preflight-checks.json: the tools scripts' -SelfTest and the unit-test run), each of which must
                   exit 0 and print its success line; then the secrets check on the real tracked tree. Prints "selftest:
                   <n>/<n> checks, <k>/<k> external selftests (...)" (faction-empowerment D22).
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
                   status and sub public and .nyar api events adminOnly, then Test-CheckVcfDependency (mode authsuite:
                   VCF a hard 0.10.x dependency, event-library D17); prints "auth suite: pass (tests, commands, admin
                   list, gateway, vcf dependency)".
    -Tests <Class>[,<Class>...]
                 : Invoke-ClassTests, one `dotnet test` per class, fail-closed (a non-zero exit, a failure, a skip, or no
                   test run fails; a filter never uses the word "or"); prints "tests: <k>/<k> classes, <n> passed"
                   (event-library D34; the TestRuns fixtures, mode fixture, prove the verdict).
    -ControlSuite <slug>
                 : -Tests over controlSuites.<slug> of tools/preflight-checks.json, then the -SelfTest body in the same
                   process; prints "control suite: <slug> tests <k>/<n> classes, selftest <n>/<n> checks" (D34).
    -DependencySuite <slug>
                 : every category of dependencySuites.<slug>: tests rows one control at a time, check rows over their
                   fixtures and the real tree, selftests rows with their success lines; prints "dependency suite: <slug>
                   <n>/<n> (<categories>)", or "dependency suite: <slug> has no categories" (event-library D33).
    -TimingSpan <log copy> [-MinTracked 140] [-MinTargets 1] [-Windows 10]
                 : Test-CheckTimingSpan (event-spawns D24) on a copy of a session's BepInEx/LogOutput.log: after the first
                   health line with at least MinTracked tracked, one warm-up timing window is skipped, then Windows timing
                   windows must each average under 5 ms and follow a "hunt targets: <n>" line with n >= MinTargets, with no
                   health line below MinTracked and no "slow tick:" line among them; prints "timing span: <w>/<w> windows
                   under 5 ms, tracked >= <t>, targets >= <n>, 0 slow ticks", or "timing span: no span (...)" as a failure.
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
    remote-tags.txt, walked.txt, temp.txt, worktrees.txt, ls-remote-tags.txt, releases.json, compile-items.txt,
    git-files.txt, before.tsv, after.tsv, aftercleanup.txt, auditof.txt) in a fixture, written in the format the real collection produces.

.EXAMPLE
    pwsh tools/preflight.ps1
    pwsh tools/preflight.ps1 -Paths
    pwsh tools/preflight.ps1 -Paths -DeclaredOf event-library
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
    [string]$DeclaredOf,            # with -Paths: plan slug whose Rollout › Paths walked must cover the child's writes (event-library D30)
    [string]$RollbackOf,            # plan slug: its Rollout › Rollback routes (event-library D29)
    [string]$From,                  # with -RollbackOf: the rollback gate's -From, which must equal the plan's
    [string]$To,                    # with -RollbackOf: the rollback gate's -To, which must equal the plan's
    [switch]$ServerWrites,          # with -Snapshot <file> (record) or -Compare <file> [-AfterCleanup] (check)
    [string]$Snapshot,
    [string]$Compare,
    [switch]$AfterCleanup,
    [switch]$LogCheck,              # the live BepInEx/LogOutput.log after an in-game session (foundation D33)
    [string]$AuditOf,               # plan slug: check its audit record covers every Build plan step
    [string]$SessionsOf,            # plan slug: check every in-game session has a clean log check line (foundation D33)
    [switch]$AuthSuite,             # the authorization suite: its tests, then the commands, admin-list and gateway checks (raphael-api-core D7), and the VCF dependency (event-library D17)
    [string]$Tests,                 # comma-separated test classes, each run on its own, fail-closed (event-library D34)
    [string]$ControlSuite,          # plan slug: its controlSuites classes, then the selftest body (event-library D34)
    [string]$DependencySuite,       # plan slug: every dependency-failure category of dependencySuites.<slug> (event-library D33)
    [string]$TimingSpan,            # a copy of a session's BepInEx/LogOutput.log: the tick budget with behaviours (event-spawns D24)
    [int]$MinTracked = 140,         # with -TimingSpan
    [int]$MinTargets = 1,           # with -TimingSpan
    [int]$Windows = 10,             # with -TimingSpan
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
$script:DeclaredOf = $DeclaredOf
$script:RollbackOf = $RollbackOf
$script:RollbackRange = @($From, $To)

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

# "tracked: <glob>" / "ignored: <glob>" / "server: <glob>" / "external: <glob>" / "temp: <glob>" (a %TEMP% folder a
# tool may hold while it runs, never after it) / "remote-tag: <name>" / "remote-release: <name>" lines of
# tools/paths-manifest.txt.
$script:ManifestLineRx = '^(tracked|ignored|server|external|temp|remote-tag|remote-release):\s*(\S.*)$'
function Get-PathsManifest([string]$Root) {
    $t = Read-Text $Root 'tools/paths-manifest.txt'
    if ($null -eq $t) { return $null }
    return @($t -split '\r?\n' | Where-Object { $_ -match $script:ManifestLineRx } |
        ForEach-Object { $null = $_ -match $script:ManifestLineRx; [pscustomobject]@{ Kind = $Matches[1]; Glob = $Matches[2].Trim() } })
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
    # 0.5.1 fixed the 0.5.0 known issue (units in water, walkable-spawns D12): from 0.5.1 on neither README carries it.
    $parsed = $null
    if ([version]::TryParse(($v -replace '[-+].*$', ''), [ref]$parsed) -and $parsed -ge [version]'0.5.1') {
        foreach ($rel in @('README.md', "$PkgRel/README.md")) {
            $t = Read-Text $Root $rel
            if ($t -and $t -match 'Known issue \(0\.5\.0\)|Known issue in 0\.5\.0') { return New-Result $false "changelogs: $rel still carries the 0.5.0 known issue" }
        }
    }
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
        try { $json = Read-Text $Root $f | ConvertFrom-Json }
        catch { return New-Result $false "templates: $f is not valid JSON" }
        # Every template ships disabled (Epic D4, faction-empowerment D22): any "enabled": true, at any depth, fails.
        $on = @(Find-EnabledTrue $json '')
        if ($on) { return New-Result $false "templates: $f ships enabled: $($on -join ', ')" }
    }
    return New-Result $true "templates: $($files.Count) valid"
}

# The ids (or JSON paths) of every object under $Node whose "enabled" property is the boolean true.
function Find-EnabledTrue($Node, [string]$Path) {
    if ($Node -is [System.Collections.IEnumerable] -and $Node -isnot [string]) {
        $i = 0
        foreach ($n in $Node) { Find-EnabledTrue $n "$Path[$i]"; $i++ }
    }
    elseif ($Node -is [pscustomobject]) {
        foreach ($p in $Node.PSObject.Properties) {
            if ($p.Name -eq 'enabled' -and $p.Value -is [bool] -and $p.Value) { if ($Node.id) { "$($Node.id)" } else { "$Path" } }
            else { Find-EnabledTrue $p.Value "$Path.$($p.Name)" }
        }
    }
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

$script:PublicCommands = @('nyar', 'status', 'help', 'me', 'top', 'hide', 'show', 'version', 'sub', 'regions')   # Epic D5 (A4); regions D11

# The command walks read `[Command(` and `[CommandGroup(` written bare, each alone in its brackets. A qualified attribute
# ([VampireCommandFramework.Command(…)], [global::…CommandAttribute(…)]), a using alias of either type, a space after
# the bracket ([ Command(…)]), an escaped name ([@CommandAttribute(…)]) or an entry of an attribute list
# ([Obsolete, Command(…)]) would register a command the walks never see, so each fails here; so does a Unicode escape in
# an identifier ([Comm\u0061ndGroup(…)]), found as any \u or \U left once literals and comments are blanked
# (raphael-api-admin D11; Codex step 2 rounds 2, 7, 8 and 10). The last pattern holds them all: the attribute name
# must follow its [ directly, so a target ([method: Command(…)]) fails too (round 11).
function Get-HiddenCommandAttributes([string]$Root) {
    foreach ($f in Get-CsFiles $Root) {
        $text = Remove-CsComments (Read-Text $Root $f)
        foreach ($m in [regex]::Matches($text, '[\[,]\s*(?:[\w.:\s@]+?\s*(?:\.|::)\s*)@?(?:Command|CommandGroup)(?:Attribute)?\s*[\(\],]')) { "a qualified command attribute in $f" }
        foreach ($m in [regex]::Matches((Remove-CsLiterals (Read-Text $Root $f)), '(?:\[\s*@|\[\s+|,\s*@?)(?:Command|CommandGroup)(?:Attribute)?\s*[\(\],]')) { "a command attribute not written [Command(…)] alone in its brackets in $f" }
        if ((Remove-CsLiterals (Read-Text $Root $f)) -match '\\[uU][0-9A-Fa-f]') { "a Unicode escape in an identifier in $f" }
        foreach ($m in [regex]::Matches((Remove-CsLiterals (Read-Text $Root $f)), '(?<!\[)\b(?:Command|CommandGroup)(?:Attribute)?\s*[\(\],]')) { "a Command or CommandGroup attribute not directly after its [ in $f" }
        foreach ($m in [regex]::Matches($text, '(?m)^\s*(?:global\s+)?using\s+@?\w+\s*=\s*[\w.:\s@]*?@?\b(?:Command|CommandGroup)(?:Attribute)?\s*;')) { "a using alias of a command attribute in $f" }
    }
}

function Test-CheckCommands([string]$Root) {
    $admin = 0; $public = 0; $bad = @(Get-HiddenCommandAttributes $Root)
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

# -AuthSuite's command inventory (event-spawns D22): the commands that can activate or change a spawn event, the ones
# event-spawns inherits and that now run its new keys (Design › Actor matrix). Each row is the chat command, the verb its
# usage must offer ('' for none) and the handler that must stand in the command method's body (Codex step 1 F4: a verb
# kept in the usage with its branch deleted fails). An event verb's handler is its flow call inside its own case section,
# between its label and the next case or default (Codex step 1 round 3 F2: a label kept with its call deleted fails).
# The [Mutating] calls they reach are the gateway check's, run by the same suite. Known limit (Review 24 F4): the match is
# textual, so a call made unreachable in its section ("return; flows.Start(...)") still counts; the authorization
# control is each command's adminOnly, checked separately.
function Get-EventVerbHandler([string]$Verb, [string]$Flow) {
    return 'case\s+(?:"\w+"\s+or\s+)*"' + $Verb + '"(?:\s+or\s+"\w+")*\s*:(?:(?!\bcase\s+"|\bdefault\s*:)[\s\S])*?\bflows\s*\.\s*' + $Flow + '\s*\('
}
$script:SpawnChangingCommands = @(
    @('.nyar event', 'start', (Get-EventVerbHandler 'start' 'Start')), @('.nyar event', 'stop', (Get-EventVerbHandler 'stop' 'Stop')),
    @('.nyar event', 'enable', (Get-EventVerbHandler 'enable' 'Enable')), @('.nyar event', 'disable', (Get-EventVerbHandler 'disable' 'Enable')),
    @('.nyar event', 'set', (Get-EventVerbHandler 'set' 'Set')), @('.nyar event', 'copy', (Get-EventVerbHandler 'copy' 'Copy')),
    @('.nyar event', 'delete', (Get-EventVerbHandler 'delete' 'DeleteEvent')), @('.nyar template', 'use', '\bTemplateUse\s*\('),
    @('.nyar spawn', '', '\bSpawnManual\s*\(')
)

# $Text with every string literal blanked to spaces (same length, so indices hold) except a plain literal that is a
# switch label ("case "start"" or "case "enable" or "disable""), so a handler named only inside a string is no handler
# (Codex step 1 round 2 F2).
function Hide-CsStringsButCases([string]$Text) {
    return $script:CsLexRx.Replace($Text, {
        param($m)
        if ($m.Groups['lc'].Success -or $m.Groups['bc'].Success -or $m.Groups['c'].Success) { return $m.Value }
        if ($m.Groups['s'].Success -and -not $m.Value.StartsWith('$') -and
            $Text.Substring(0, $m.Index) -match '\bcase\s+(?:"\w+"\s+or\s+)*$') { return $m.Value }
        return '"' + (' ' * ($m.Value.Length - 2)) + '"'
    })
}

# Every spawn-changing command is found exactly once in the command walk ([Command] attributes of every .cs file) and is
# adminOnly, its verb is a word of its usage, and its handler is in the command method's body, so none is reachable by
# a player who is not an admin and none is listed but unhandled.
function Test-CheckAuthSuite([string]$Root) {
    $walk = @(Get-CommandWalk $Root)
    if ($walk.Count -eq 0) { return New-Result $false 'command inventory: no commands found' }
    $bad = @()
    foreach ($want in $script:SpawnChangingCommands) {
        $full, $verb, $handler = $want
        $label = (@($full, $verb) | Where-Object { $_ }) -join ' '
        $cmd = @($walk | Where-Object Full -eq $full)
        if ($cmd.Count -ne 1) { $bad += "$label found $($cmd.Count) times"; continue }
        if (-not $cmd[0].Admin) { $bad += "$label not adminOnly ($($cmd[0].File))" }
        $text = Remove-CsComments (Read-Text $Root $cmd[0].File)
        $attr = [regex]::Match($text, "\[Command(?:Attribute)?\s*\(\s*(?:name\s*:\s*)?`"$([regex]::Escape($cmd[0].Name))`"[^\]]*?(?:usage\s*:\s*`"([^`"]*)`"[^\]]*)?\]")
        if (-not $attr.Success) { $bad += "$label has no readable [Command] attribute"; continue }
        if ($verb -and $attr.Groups[1].Value -notmatch "(?<![\w-])$verb(?![\w-])") { $bad += "$label is not in the usage of $full" }
        if ((Get-MethodBody (Hide-CsStringsButCases $text) ($attr.Index + $attr.Length)) -notmatch $handler) { $bad += "$label has no handler in $($cmd[0].File)" }
    }
    if ($bad) { return New-Result $false "command inventory: $($bad -join '; ')" }
    return New-Result $true "command inventory: $($script:SpawnChangingCommands.Count)/$($script:SpawnChangingCommands.Count) spawn-changing commands adminOnly and handled"
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
    # DestroyUtility.Destroy* is allowed only in the bodies of RemoveBuffSafe (the carrier-buff removal) and DestroySafe
    # (the unit despawn helper, spikes A13); and the empowerment service never calls DestroySafe or KillOrDestroyEntity, so
    # a carrier or its unit cannot be destroyed another way (faction-empowerment D8, A3).
    $destroyHosts = @('RemoveBuffSafe', 'DestroySafe')
    $empower = "$PkgRel/Services/EmpowerAction.cs"
    $bad = @(); $guarded = 0
    foreach ($f in $cs) {
        $text = Remove-CsComments (Read-Text $Root $f)
        if ($f -eq $empower -and $text -match '\b(?<k>DestroySafe|KillOrDestroyEntity)\b') { $bad += "$($Matches.k) in $f (a carrier is removed only by RemoveBuffSafe)" }
        $calls = [regex]::Matches($text, $rx)
        if ($calls.Count -eq 0) { continue }
        if ($f -ne $fence) { $bad += "$($calls[0].Groups['op'].Value) in $f"; continue }
        # Inside the fence, every structural call must be preceded, in its own method, by an early
        # refusal of Prefab entities.
        foreach ($c in $calls) {
            $start = Get-EnclosingMethodStart $text $c.Index
            if ($start -lt 0) { $bad += "$($c.Groups['op'].Value) in $f outside a method"; continue }
            if ($c.Groups['op'].Value -like 'DestroyUtility.Destroy*') {
                $name = [regex]::Match($text.Substring($start), '^[^(]*?(\w+)\s*(?:<[^>]*>)?\s*\(').Groups[1].Value
                if ($destroyHosts -notcontains $name) { $bad += "$($c.Groups['op'].Value) in $f method $name (only RemoveBuffSafe and DestroySafe)"; continue }
                # The carrier removal is the game's buff removal: its destroy names DestroyDebugReason.TryRemoveBuff (D8).
                $stmt = $text.Substring($c.Index); $stmt = $stmt.Substring(0, [Math]::Max(0, $stmt.IndexOf(';')))
                if ($name -eq 'RemoveBuffSafe' -and $stmt -notmatch ',\s*DestroyDebugReason\.TryRemoveBuff\s*\)\s*$') { $bad += "$($c.Groups['op'].Value) in $f method RemoveBuffSafe without DestroyDebugReason.TryRemoveBuff"; continue }
            }
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
    '(TCLI_AUTH_TOK[E]N|GH_TOK[E]N)\s*[=:]\s*\S+',
    # The release step builds the zip with the tcli token variable set to this sentinel, one random suffix per build, and
    # the check then finds it wherever the build leaked the variable: the zip, dist/, build/ or the build log
    # build/tcli-build.log (event-spawns D31). Spelled so that this line is no match.
    'tcli-sentin[e]l-[0-9a-f]{8}'
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
# tools/ holds PowerShell, Python and Node scripts and their data only: a file of any other type (a shell or batch
# script, or an extensionless one run through a shebang) expands variables with no marker to count, so it fails.
$script:ToolsAllowedExt = @('.ps1', '.psm1', '.py', '.mjs', '.js', '.json', '.txt', '.md')
$script:ToolsScriptExt = @('.ps1', '.psm1', '.py', '.mjs', '.js')
# Per language: the token that reaches the environment, and the one form of it that is an allowed read.
$script:ToolsEnvRules = @(
    @{ Ext = @('.ps1', '.psm1'); Any = '(?i)\ben[v]:'; Allowed = "(?i)\`$(?:en[v]:(?:$script:ToolsEnvAllowed)\b|\{en[v]:(?:$script:ToolsEnvAllowed)\})" },
    @{ Ext = @('.py'); Any = '\b(?:enviro[n]|gete[n]v)\b'; Allowed = "\bos\.(?:enviro[n](?:\.get\s*\(|\[)|gete[n]v\s*\()\s*[`"'](?:$script:ToolsEnvAllowed)[`"']" },
    @{ Ext = @('.mjs', '.js'); Any = '\ben[v]\b'; Allowed = "\bprocess\.en[v]\.(?:$script:ToolsEnvAllowed)\b" }
)

# The first credential or environment access in a tools/ script that is not allowed, or $null.
function Find-ToolsEnvAccess([string]$Ext, [string]$Text) {
    if ($script:ToolsAllowedExt -notcontains $Ext) { return "a file of type '$Ext' (tools/ holds PowerShell, Python or Node scripts and their data)" }
    if ($script:ToolsScriptExt -notcontains $Ext) { return $null }
    foreach ($p in $script:ToolsCredentialPatterns) { if ($Text -match $p) { return $Matches[0] } }
    foreach ($r in $script:ToolsEnvRules) {
        if ($r.Ext -notcontains $Ext) { continue }
        $any = [regex]::Matches($Text, $r.Any).Count
        $ok = [regex]::Matches($Text, $r.Allowed).Count
        if ($any -gt $ok) { return "$($any - $ok) environment access(es) outside the allow-list" }
    }
    return $null
}
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
        # Only the owner's own shell reads the tcli token: no script or source file names it (event-spawns D31; docs may).
        if (@('.ps1', '.psm1', '.py', '.mjs', '.js', '.cs') -contains $ext -and $f -notlike 'tools/preflight-fixtures/*' -and
            $(if ($ext -eq '.cs') { Remove-CsComments $t } else { $t }) -match 'TCLI_AUTH_TOK[E]N') { $hits += "$f (reads the tcli token)" }
        if ($f -like 'tools/*' -and $f -notlike 'tools/preflight-fixtures/*') {
            $why = Find-ToolsEnvAccess $ext $t
            if ($why) { $hits += "$f (reads a credential or the environment: $why)" }
        }
    }
    # Files but no text among them (only binaries, or a zip with no entries): nothing was scanned, which is no pass (D31).
    if ($scanned -eq 0) { return New-Result $false 'secrets: nothing scanned' }
    # The index can hold content the working tree no longer shows (a token staged or committed, then
    # edited out or deleted only on disk), so every blob in the index is searched as well.
    $indexNote = ''
    if (-not (Test-IsFixture $Root)) {
        $ix = Get-IndexSecretHits $Root
        if ($ix.Error) { return New-Result $false "secrets: $($ix.Error)" }
        $hits += $ix.Hits
        $indexNote = ", $($ix.Blobs) index blobs"
    }
    if ($hits) { return New-Result $false "secrets: FOUND in $(@($hits | Sort-Object -Unique) -join ', ')" }
    return New-Result $true "secrets: none ($scanned files scanned$indexNote)"
}

# The index side of the secrets check: every rule the working-tree scan applies, over the staged blobs, so content
# staged and then edited out on disk still fails (Codex step 1 round 5 F1): the secret patterns, a C# environment
# read, a script or source naming the tcli token, and a tools/ script reading a credential or the environment.
function Get-IndexSecretHits([string]$Root) {
    $hits = @()
    $blobs = @(git -C $Root ls-files --cached 2>$null).Count
    if ($blobs -eq 0) { return @{ Error = 'index empty: no staged blob to search' } }    # never a silent pass (Review 27 F2)
    foreach ($p in $script:SecretPatterns) {
        $found = git -C $Root grep --cached -I -l -P $p 2>&1
        if ($LASTEXITCODE -gt 1) { return @{ Error = "git grep --cached failed: $found" } }
        $hits += @($found | Where-Object { $_ } | ForEach-Object { "$_ (index)" })
    }
    $found = git -C $Root grep --cached -I -l -P 'Environment\.GetEnvironmentVariabl[e]' -- '*.cs' 2>&1
    if ($LASTEXITCODE -gt 1) { return @{ Error = "git grep --cached failed: $found" } }
    $hits += @($found | Where-Object { $_ } | ForEach-Object { "$_ (index, reads the environment)" })
    $found = git -C $Root grep --cached -I -l -P 'TCLI_AUTH_TOK[E]N' -- '*.ps1' '*.psm1' '*.py' '*.mjs' '*.js' '*.cs' ':(exclude)tools/preflight-fixtures/*' 2>&1
    if ($LASTEXITCODE -gt 1) { return @{ Error = "git grep --cached failed: $found" } }
    $hits += @($found | Where-Object { $_ } | ForEach-Object { "$_ (index, reads the tcli token)" })
    foreach ($f in @(git -C $Root ls-files --cached -- tools ':(exclude)tools/preflight-fixtures/*' 2>$null)) {
        $ext = [IO.Path]::GetExtension($f).ToLowerInvariant()
        $t = if ($script:ToolsScriptExt -contains $ext) { (git -C $Root show ":$f" 2>$null) -join "`n" } else { '' }
        $why = Find-ToolsEnvAccess $ext $t
        if ($why) { $hits += "$f (index, reads a credential or the environment: $why)" }
    }
    return @{ Hits = $hits; Blobs = $blobs; Error = $null }
}

# Proof that the index side runs (Codex step 1 round 5 F1): a scratch repository stages a script naming the tcli token,
# then cleans it on disk; Get-IndexSecretHits must find it, and must find nothing once the clean copy is staged.
# The token name is assembled at run time so this file does not name it. Returns $null on success, else the reason.
function Test-IndexSecretProbe {
    $dir = Join-Path ([IO.Path]::GetTempPath()) "nyar-secrets-index-$PID"
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    try {
        New-Item -ItemType Directory -Force (Join-Path $dir 'tools') | Out-Null
        git -C $dir init -q 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) { return 'index probe: scratch repository setup failed (git init)' }
        # Each case stages a leak, cleans it on disk and must be found by its label (Review 27 F1: both rules).
        $cases = @(
            @(('$x = $' + 'env' + ':' + 'TCLI_AUTH_TOK' + 'EN'), 'tools/leak.ps1 (index, reads the tcli token)', 'a staged tcli token read'),
            @(('$x = ' + 'gh ' + 'auth ' + 'token'), 'tools/leak.ps1 (index, reads a credential or the environment*', 'a staged credential read in tools/')
        )
        $leak = Join-Path $dir 'tools/leak.ps1'
        foreach ($c in $cases) {
            Set-Content -LiteralPath $leak -Value $c[0] -NoNewline
            git -C $dir add -- tools/leak.ps1 2>$null | Out-Null
            if ($LASTEXITCODE -ne 0) { return 'index probe: scratch repository setup failed (git add)' }
            Set-Content -LiteralPath $leak -Value '$x = $null' -NoNewline
            $ix = Get-IndexSecretHits $dir
            if ($ix.Error) { return "index probe: $($ix.Error)" }
            if (-not @($ix.Hits | Where-Object { $_ -like $c[1] })) { return "index probe: $($c[2]) passed" }
        }
        git -C $dir add -- tools/leak.ps1 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) { return 'index probe: scratch repository setup failed (git add)' }
        $ix = Get-IndexSecretHits $dir
        if ($ix.Error -or $ix.Hits) { return "index probe: the clean staged copy failed ($($ix.Error)$($ix.Hits -join ', '))" }
        return $null
    } finally { if (Test-Path $dir) { Remove-Item $dir -Recurse -Force } }
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

# Every rollback route of a plan's Rollout › Rollback, machine-checked (event-library D29). The slug is -RollbackOf's, or a
# fixture's rollback-of.txt; the range is the plan's own rollback-gate command, and the gate's -From and -To must match it.
function Test-CheckRollbackRoutes([string]$Root) {
    $slug = if (Test-IsFixture $Root) { "$(Read-Text $Root 'rollback-of.txt')".Trim() } else { $script:RollbackOf }
    if (-not $slug) { return New-Result $false 'rollback routes: no plan slug' }
    $plan = Read-Text $Root "docs/dod/$slug.md"
    if ($null -eq $plan) { return New-Result $false "rollback routes: docs/dod/$slug.md not found" }
    $sec = [regex]::Match($plan, '(?ms)^### Rollback[ \t]*\r?$(.*?)(?=^##)')
    if (-not $sec.Success) { return New-Result $false "rollback routes: $slug has no Rollout › Rollback section" }
    # Every rollback-gate command for this plan must name one range, so a stale quote elsewhere cannot set it.
    $cmds = @([regex]::Matches($plan, "rollback-gate\.ps1 -From (v[\w.-]+) -To (v[\w.-]+) -Plan $([regex]::Escape($slug))\b") |
        ForEach-Object { "$($_.Groups[1].Value)..$($_.Groups[2].Value)" } | Sort-Object -Unique)
    if ($cmds.Count -eq 0) { return New-Result $false "rollback routes: $slug has no rollback-gate.ps1 -From <tag> -To <tag> -Plan $slug command" }
    if ($cmds.Count -gt 1) { return New-Result $false "rollback routes: $slug's rollback-gate commands name $($cmds.Count) ranges ($($cmds -join ', '))" }
    $from, $to = $cmds[0] -split '\.\.', 2
    $gate = if (Test-IsFixture $Root) { @('', '') } else { $script:RollbackRange }
    if (($gate[0] -and $gate[0] -ne $from) -or ($gate[1] -and $gate[1] -ne $to)) {
        return New-Result $false "rollback routes: the gate's range $($gate[0])..$($gate[1]) is not the plan's $from..$to"
    }
    $bullets = @{}
    foreach ($m in [regex]::Matches($sec.Groups[1].Value, '(?m)^- \*\*([^*\r\n]+?):\*\*[ \t]*([^\r\n]*)')) { $bullets[$m.Groups[1].Value.Trim()] = $m.Groups[2].Value }
    $range = "$from..$to"
    $routes = [ordered]@{
        'In the repository'                = @("git revert --no-edit $range")
        'On the dev server during the build' = @('tools/dev-snapshot.ps1')
        'On a server'                      = @("install the $($from.TrimStart('v')) DLL", 'after data is written')
        'Published release'                = @('never deleted', 'withdrawn by retitling')
    }
    $failed = @()   # one reason per route, so 5 minus its count is the routes that pass
    foreach ($name in $routes.Keys) {
        if (-not $bullets.ContainsKey($name)) { $failed += "$name missing"; continue }
        $lack = @($routes[$name] | Where-Object { -not $bullets[$name].Contains($_) })
        if ($lack) { $failed += "$name lacks '$($lack -join "', '")'" }
    }
    if (-not $bullets.ContainsKey('Commit range')) { $failed += 'Commit range missing' }
    elseif ($bullets['Commit range'].Trim().TrimEnd('.').Trim('`') -ne $range) { $failed += "Commit range is '$($bullets['Commit range'].Trim())', not $range" }
    if ($failed) { return New-Result $false "rollback routes: $slug $(5 - $failed.Count)/5, failed: $($failed -join '; ')" }
    return New-Result $true "rollback routes: $slug 5/5"
}

# ---------------------------------------------------------------- checks: data and paths (Epic D33, D34)

function Test-CheckDataInventory([string]$Root) {
    $inv = Read-Text $Root 'tools/data-inventory.json'
    if ($null -eq $inv) { return New-Result $false 'data inventory: tools/data-inventory.json not found' }
    $manifest = Get-PathsManifest $Root
    if ($null -eq $manifest) { return New-Result $false 'data inventory: tools/paths-manifest.txt not found' }
    $entries = @(($inv | ConvertFrom-Json).entries)
    if ($entries.Count -eq 0) { return New-Result $false 'data inventory: no entries' }
    # Every ignored, server, external and temp glob needs an entry, so the expected set comes from the manifest
    # (faction-empowerment D26); remote-tag and remote-release lines declare git refs, not stored data.
    $required = @($manifest | Where-Object { $_.Kind -in 'ignored', 'server', 'external', 'temp' } | ForEach-Object { $_.Glob })
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
    # Every row of the Design › Data artifact table of each plan preflight-checks.json lists under dataTables needs an
    # entry naming it in rows as "<slug> › <artifact>" (raphael-api-core D19).
    $rows = @()
    $mt = Read-Text $Root 'tools/preflight-checks.json'
    $tables = if ($mt) { @(($mt | ConvertFrom-Json).dataTables | Where-Object { $_ }) } else { @() }
    foreach ($planRel in $tables) {
        $plan = Read-Text $Root $planRel
        $slug = [IO.Path]::GetFileNameWithoutExtension($planRel)
        $data = if ($plan) { [regex]::Match($plan, '(?ms)^### Data[ \t]*\r?$(.*?)(?=^##)') } else { $null }
        $table = if ($data -and $data.Success) { [regex]::Match($data.Groups[1].Value, '(?m)^\| Artifact \|[^\r\n]*\r?\n\|[-| ]+\|[ \t]*\r?\n((?:\|[^\r\n]*\r?\n?)+)') } else { $null }
        if ($null -eq $table -or -not $table.Success) { $bad += "$planRel has no Design › Data artifact table"; continue }
        foreach ($line in @($table.Groups[1].Value -split '\r?\n' | Where-Object { $_ -match '^\|' })) {
            $name = ($line -split '\|')[1].Trim()
            $rows += "$slug › $name"
            if (-not ($entries | Where-Object { @($_.rows) -contains "$slug › $name" })) { $bad += "$slug row '$name' has no entry" }
        }
        # The reverse direction (event-library D30): an entry's row name of this plan must be one of its table's rows,
        # so a row dropped or renamed in the plan cannot leave a stale inventory name behind.
        foreach ($named in @($entries | ForEach-Object { @($_.rows) } | Where-Object { $_ -like "$slug › *" })) {
            if ($rows -notcontains $named) { $bad += "inventory row '$named' matches no row of $planRel" }
        }
    }
    if ($bad) { return New-Result $false "data inventory: $($bad -join '; ')" }
    return New-Result $true "data inventory: $($entries.Count) entries; $($required.Count)/$($required.Count) globs and files, $($rows.Count)/$($rows.Count) plan rows"
}

# Every listing the paths walk reads, raw as its source prints it, through this one function: in the real repository
# from git, the server directory, %TEMP% and gh; in a fixture from captured files. A listing whose source failed (or
# whose file a fixture lacks) is $null, and the check fails on it.
#   Walked     "<kind> <path>" lines (kind tracked|ignored|server)                        walked.txt
#   Temp       the names of the %TEMP% folders named nyar-*                                temp.txt
#   Worktrees  `git worktree list --porcelain`                                             worktrees.txt
#   RemoteTags `git ls-remote --tags origin`                                               ls-remote-tags.txt
#   Releases   `gh release list --json tagName`                                            releases.json
#   Declared   with -DeclaredOf <slug> only (else $null): the slug; the created or changed paths as
#              "<kind> <path>" lines (kind tracked|ignored|server|temp), $null when a source failed;       declared-of.txt, changed.txt
#              and the parent of the commit that created docs/audits/<slug>.md, $null when git failed    audit-added-parent.txt
function Get-PathsListings([string]$Root) {
    if (Test-IsFixture $Root) {
        $w = Read-Text $Root 'walked.txt'
        $slug = Read-Text $Root 'declared-of.txt'
        $changed = Read-Text $Root 'changed.txt'
        $parent = Read-Text $Root 'audit-added-parent.txt'
        return [pscustomobject]@{
            Walked     = if ($null -eq $w) { @() } else { @($w -split '\r?\n' | Where-Object { $_ }) }
            Temp       = Read-Text $Root 'temp.txt'
            Worktrees  = Read-Text $Root 'worktrees.txt'
            RemoteTags = Read-Text $Root 'ls-remote-tags.txt'
            Releases   = Read-Text $Root 'releases.json'
            Declared   = if ($null -eq $slug) { $null } else {
                [pscustomobject]@{ Slug = $slug.Trim(); Changed = if ($null -eq $changed) { $null } else { @($changed -split '\r?\n' | Where-Object { $_ }) }
                    AddedParent = if ($null -eq $parent) { $null } else { $parent.Trim() } } }
        }
    }
    $wt = git -C $Root worktree list --porcelain 2>$null
    $wt = if ($LASTEXITCODE -eq 0) { @($wt) -join "`n" } else { $null }
    $rt = git -C $Root ls-remote --tags origin 2>$null
    $rt = if ($LASTEXITCODE -eq 0) { @($rt) -join "`n" } else { $null }
    $rel = $null
    Push-Location -LiteralPath $Root
    try { $rel = gh release list --limit 1000 --json tagName 2>$null; $rel = if ($LASTEXITCODE -eq 0) { @($rel) -join "`n" } else { $null } }
    catch { $rel = $null }
    finally { Pop-Location }
    $tempDir = [IO.Path]::GetTempPath()   # nyar-temp: lists the nyar-* folders present, creates nothing
    # An unreadable %TEMP% is a failed listing ($null), never an empty one (step 2 review).
    try { $temp = @(Get-ChildItem -LiteralPath $tempDir -Directory -Filter 'nyar-*' -Force -ErrorAction Stop | ForEach-Object Name) -join "`n" }
    catch { $temp = $null }
    $walked = @(Get-WalkedPaths $Root)
    $declared = if ($script:DeclaredOf) {
        [pscustomobject]@{ Slug = $script:DeclaredOf; Changed = Get-ChangedPaths $Root $script:DeclaredOf $walked $temp
            AddedParent = Get-AuditAddedParent $Root $script:DeclaredOf }
    } else { $null }
    return [pscustomobject]@{ Walked = $walked; Temp = $temp; Worktrees = $wt; RemoteTags = $rt; Releases = $rel; Declared = $declared }
}

# The base of a child's build: the commit of its first pre-audit ("### Step 1 · <date> · <sha>" under ## Pre-audit of
# docs/audits/<slug>.md), or $null (event-library D30, A13).
function Get-AuditBase([string]$Root, [string]$Slug) {
    $audit = Read-Text $Root "docs/audits/$Slug.md"
    if ($null -eq $audit) { return $null }
    $pre = [regex]::Match($audit, '(?ms)^## Pre-audit[ \t]*\r?$(.*?)(?=^## )')
    $m = if ($pre.Success) { [regex]::Match($pre.Groups[1].Value, '(?m)^### Step 1 · [^·\r\n]+ · ([0-9a-f]{7,40})\b') } else { $null }
    if ($null -eq $m -or -not $m.Success) { return $null }
    return $m.Groups[1].Value
}

# Git's own record of that base (event-library A14): the parent of the oldest commit that added docs/audits/<slug>.md,
# which step 1 creates. The heading must name it, so a heading moved to a later commit cannot hide this child's changes.
# $null when git fails or never saw the file added.
function Get-AuditAddedParent([string]$Root, [string]$Slug) {
    $added = @(git -C $Root log --diff-filter=A --format=%H -- "docs/audits/$Slug.md" 2>$null)
    if ($LASTEXITCODE -ne 0 -or $added.Count -eq 0) { return $null }
    $parent = git -C $Root rev-parse "$($added[-1])^" 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $parent) { return $null }
    return "$parent".Trim()
}

# The paths a child's build created or changed, as "<kind> <path>" (event-library D30, A13, A14); only Get-PathsListings
# calls it. Git knows what changed in tracked paths since the base (Get-AuditBase); an untracked, ignored or server path
# counts when it holds a file written after the base commit (a directory git collapses by its newest file); every
# %TEMP%\nyar-* folder present counts. Test-CheckPaths adds the %TEMP% folders the tools can create (Get-ToolTempNames).
# $null when the audit, the base or git fails.
function Get-ChangedPaths([string]$Root, [string]$Slug, [string[]]$Walked, [string]$Temp) {
    $base = Get-AuditBase $Root $Slug
    if ($null -eq $base) { return $null }
    $ct = git -C $Root log -1 --format=%ct $base 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $ct) { return $null }
    $since = [DateTimeOffset]::FromUnixTimeSeconds([long]"$ct".Trim()).UtcDateTime
    $diff = @(git -C $Root diff --name-only $base 2>$null); if ($LASTEXITCODE -ne 0) { return $null }
    $new = @(git -C $Root ls-files -o --exclude-standard 2>$null); if ($LASTEXITCODE -ne 0) { return $null }
    $out = @($diff | Where-Object { $_ } | ForEach-Object { "tracked $_" })
    # An unreadable file or folder throws, so the whole listing is unreadable ($null), never "not written".
    $written = {
        param($full)
        if (-not (Test-Path -LiteralPath $full)) { return $false }
        $i = Get-Item -LiteralPath $full -Force -ErrorAction Stop
        if (-not $i.PSIsContainer) { return $i.LastWriteTimeUtc -gt $since }
        return [bool](Get-ChildItem -LiteralPath $full -Recurse -File -Force -ErrorAction Stop |
            Where-Object { $_.LastWriteTimeUtc -gt $since } | Select-Object -First 1)
    }
    try {
    $out += @($new | Where-Object { $_ -and (& $written (Join-Path $Root $_)) } | ForEach-Object { "tracked $_" })
    $sp = if (Test-Path $ServerPath) { (Resolve-Path $ServerPath).Path.TrimEnd('\') } else { $null }
    foreach ($w in $Walked) {
        $kind, $path = $w -split ' ', 2
        $full = switch ($kind) { 'ignored' { Join-Path $Root $path } 'server' { if ($sp) { Join-Path $sp $path } } default { $null } }
        if ($full -and (& $written $full)) { $out += "$kind $path" }
    }
    } catch { return $null }
    $out += @("$Temp" -split '\r?\n' | ForEach-Object { $_.Trim() } | Where-Object { $_ -like 'nyar-*' } | ForEach-Object { "temp $_" })
    return $out
}

# Every %TEMP% folder name the tools can create (event-library A14): the nyar-<name> literals of tools/*.ps1, tools/*.py and
# tools/ingame/*, as "temp <name>" ("temp nyar-snap-x" for a prefix such as "nyar-snap-"). Read from source, so a
# folder a tool made and removed before the check still counts. Fixtures under tools/ are not read. A listed source
# that cannot be read comes back as "unreadable <file>", so the check fails closed (step 3 Codex cross-inspection).
function Get-ToolTempNames([string]$Root) {
    $names = @(); $out = @()
    foreach ($f in @(Get-ToolSources $Root)) {
        $t = Read-ToolSource $Root $f
        if ($null -eq $t) { $out += "unreadable $f"; continue }
        $names += @([regex]::Matches($t, '(?<![\w-])nyar-[a-z][a-z0-9]*(?:-[a-z0-9]+)*-?(?!\w|-|:)') | ForEach-Object { $_.Value })
    }
    return @($out + @($names | Sort-Object -Unique | ForEach-Object { if ($_.EndsWith('-')) { "temp $($_)x" } else { "temp $_" } }))
}

# Every %TEMP% folder the child's own records name (event-library A24, Review 18): a %TEMP%\nyar-<name> token in its
# audit (docs/audits/<slug>.md) or its feature docs (childDocs, else the slug-derived name), as "temp <name>" ("temp
# nyar-soak-x" for nyar-soak-*). A folder made by hand in a session and deleted before the check leaves no trace on
# disk, but its session record names it, so an omission from Paths walked still fails.
function Get-RecordTempNames([string]$Root, [string]$Slug) {
    $mt = Read-Text $Root 'tools/preflight-checks.json'
    $mj = if ($mt) { $mt | ConvertFrom-Json } else { $null }
    $entry = if ($mj -and $mj.childDocs) { $mj.childDocs.PSObject.Properties[$Slug] } else { $null }
    $rels = @("docs/audits/$Slug.md") + $(if ($entry) { @($entry.Value | Where-Object { $_ }) } else { @("docs/features/$($Slug.ToUpperInvariant().Replace('-', '_')).md") })
    $names = @()
    foreach ($rel in $rels) {
        $t = Read-Text $Root $rel
        if ($null -eq $t) { continue }
        $names += @([regex]::Matches($t, '%TEMP%\\(nyar-[a-z0-9]+(?:-[a-z0-9]+)*(?:-\*)?)') | ForEach-Object { $_.Groups[1].Value })
    }
    return @($names | Sort-Object -Unique | ForEach-Object { if ($_.EndsWith('-*')) { "temp $($_.Substring(0, $_.Length - 1))x" } else { "temp $_" } })
}

# The tool sources the %TEMP% name scans read: tools/*.ps1, *.psm1, *.py, *.mjs, *.js and tools/ingame/*.
function Get-ToolSources([string]$Root) {
    return @(Get-TreeFiles $Root | Where-Object { $_ -match '^tools/(ingame/[^/]+|[^/]+\.(ps1|psm1|py|mjs|js))$' })
}

# One tool source's text, or $null when it vanished or cannot be read (a lock throws in ReadAllText).
function Read-ToolSource([string]$Root, [string]$Rel) {
    try { return Read-Text $Root $Rel } catch { return $null }
}

# The places a tool composes a %TEMP% name instead of spelling its prefix (event-library A15), as "<file>:<line>": a
# bare "nyar-" or a whole nyar-<name> literal followed at once by $ { % or a backtick (an interpolation or format
# hole), or by a closing quote and a + (a concatenation); and a quoted nyar literal closed at once and concatenated with
# a hyphen and a variable. Get-ToolTempNames cannot read such a name, so the paths check fails on
# it; a tool spells the prefix ("nyar-snap-" + $id), which the scan reads as nyar-snap-*.
function Get-ComposedTempNames([string]$Root) {
    $out = @()
    foreach ($f in @(Get-ToolSources $Root)) {
        $t = Read-ToolSource $Root $f
        if ($null -eq $t) { $out += "unreadable $f"; continue }
        $lines = $t -split '\r?\n'
        for ($i = 0; $i -lt $lines.Count; $i++) {
            foreach ($m in [regex]::Matches($lines[$i], '(?<![\w-])nyar-(?:[a-z][a-z0-9]*(?:-[a-z0-9]+)*-?)?')) {
                $rest = $lines[$i].Substring($m.Index + $m.Length)
                # a spelled prefix ("nyar-snap-") may be followed by anything; the bare "nyar-" or a whole name may not
                if ($m.Value -ne 'nyar-' -and $m.Value.EndsWith('-')) { continue }
                if ($rest -match '^([${%`]|[''"]\s*\+)') {
                    $out += "$($f):$($i + 1)"
                }
            }
            if ($lines[$i] -match '(?<![\w-])nyar[''"]\s*\+') { $out += "$($f):$($i + 1)" }
        }
    }
    return @($out | Sort-Object -Unique)
}

# The tool lines that take the %TEMP% root without naming their folder (event-library A18), as "<file>:<line>". A code
# line of a tool source that reads the temp root ($env:TEMP or $env:TMP, [IO.Path]::GetTempPath(), Python's
# os.environ TEMP/TMP (spaces allowed around its dots, as around Node's process.env.TEMP), tempfile.gettempdir(), and the anonymous makers mkdtemp, mkstemp, TemporaryDirectory,
# NamedTemporaryFile, GetTempFileName, New-TemporaryFile, Node's os.tmpdir) must spell a nyar-<name> literal on that
# line's code, or end with the registration comment "# nyar-temp: <reason>" ("// nyar-temp:" in Node) (a listing, or the
# root handed to a function that spells the name). Each line is split at its trailing comment, found outside quotes
# (Split-CodeComment), so a literal or an API in the comment does not count and a marker in a string is no comment. A name built wholly from variables therefore cannot reach %TEMP% unseen. Comment lines and
# PowerShell <# #> help blocks are not code. An unreadable source is reported as "unreadable <file>".
function Get-UnmarkedTempRoots([string]$Root) {
    $api = '\$\{?en[v]:(TEMP|TMP)\b|\bgete[n]v\s*\(\s*[''"](TEMP|TMP)[''"]|process\s*\.\s*en[v]\s*\.\s*(TEMP|TMP)\b|\bGetTempPath\s*\(\s*\)|\benviron\s*(\.\s*get\s*\(\s*|\[\s*)[''"](TEMP|TMP)[''"]|\bgettempdir\s*\(|\bmkdtemp\s*\(|\bmkstemp\s*\(|\bTemporaryDirectory\s*\(|\bNamedTemporaryFile\s*\(|\bGetTempFileName\s*\(|(?<![\w-])New-TemporaryFile\b|\btmpdir\s*\(\s*\)'   # nyar-temp: the pattern itself, it opens no folder
    $out = @()
    foreach ($f in @(Get-ToolSources $Root)) {
        $t = Read-ToolSource $Root $f
        if ($null -eq $t) { $out += "unreadable $f"; continue }
        $lines = $t -split '\r?\n'; $block = [ref]$false; $ps = $f -match '\.psm?1$'
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $code, $comment, $strings = Split-CodeComment $lines[$i] ($f -match '\.m?js$') $ps $block
            if ($code -notmatch $api) { continue }
            if ($strings -match '(?<![\w-])nyar-(?!temp:)[a-z]') { continue }
            if ($comment -match '^(#|//)\s*nyar-temp:\s*\S') { continue }
            $out += "$($f):$($i + 1)"
        }
    }
    return @($out | Sort-Object -Unique)
}

# A source line as (code, trailing comment, the text of its string literals), read left to right: a quoted string is
# code (inside it a backslash in Python and Node, or a backtick in PowerShell, escapes the next character); outside
# strings a PowerShell <# ... #> block comment is dropped, and may run over lines ($Block carries it); the trailing
# comment starts at the first # (// in Node). No trailing comment: ''. A nyar-<name> counts only inside a string.
function Split-CodeComment([string]$Line, [bool]$Node, [bool]$Ps, [ref]$Block) {
    $code = [Text.StringBuilder]::new(); $str = [Text.StringBuilder]::new(); $q = [char]0; $k = 0
    $esc = if ($Ps) { [char]96 } else { [char]92 }
    while ($k -lt $Line.Length) {
        if ($Ps -and $Block.Value) {
            $c = $Line.IndexOf('#>', $k)
            if ($c -lt 0) { return @($code.ToString(), '', $str.ToString()) }
            $Block.Value = $false; $k = $c + 2; [void]$code.Append(' '); continue
        }
        $ch = $Line[$k]
        if ($q -ne [char]0) {
            [void]$code.Append($ch)
            if ($ch -eq $esc -and $k + 1 -lt $Line.Length) { [void]$code.Append($Line[$k + 1]); [void]$str.Append($Line[$k + 1]); $k += 2; continue }
            if ($ch -eq $q) { $q = [char]0; [void]$str.Append(' ') } else { [void]$str.Append($ch) }
            $k++; continue
        }
        if ($ch -eq [char]39 -or $ch -eq [char]34) { $q = $ch; [void]$code.Append($ch); $k++; continue }
        if ($Ps -and $ch -eq [char]60 -and $k + 1 -lt $Line.Length -and $Line[$k + 1] -eq [char]35) { $Block.Value = $true; $k += 2; continue }
        if ((-not $Node -and $ch -eq [char]35) -or ($Node -and $ch -eq [char]47 -and $k + 1 -lt $Line.Length -and $Line[$k + 1] -eq [char]47)) {
            return @($code.ToString(), $Line.Substring($k), $str.ToString())
        }
        [void]$code.Append($ch); $k++
    }
    return @($code.ToString(), '', $str.ToString())
}

# The path tokens of a plan's Rollout › Paths walked (event-library D30): words holding a / or \, a * or a file name,
# braces expanded ({a,b} and {,.bak} forms, several groups), globs kept. %TEMP%\<name> becomes the temp token "%TEMP%\<name>".
function Get-DeclaredTokens([string]$Plan) {
    $sec = [regex]::Match($Plan, '(?ms)^### Paths walked[ \t]*\r?$(.*?)(?=^##)')
    if (-not $sec.Success) { return @() }
    # Markdown bold markers (**Step 1:**) are not globs: one opening before a letter, one closing after a word or a colon.
    $text = [regex]::Replace($sec.Groups[1].Value, '(?<![^ \t(`\n])[*][*](?=[A-Za-z])|(?<=[:.)A-Za-z0-9])[*][*](?=[ \t,.;:)`]|$)', '', 'Multiline')
    $words = [Collections.Generic.List[string]]::new()
    $cur = [Text.StringBuilder]::new(); $depth = 0
    foreach ($ch in $text.ToCharArray()) {
        $c = [string]$ch
        if ($c -eq '{') { $depth++ } elseif ($c -eq '}' -and $depth -gt 0) { $depth-- }
        if ($c -match '[\w.*%\\/{}+~$-]' -or ($c -eq ',' -and $depth -gt 0) -or ($c -eq '}')) { [void]$cur.Append($c); continue }
        if ($cur.Length) { $words.Add($cur.ToString()) }
        [void]$cur.Clear(); $depth = 0
    }
    if ($cur.Length) { $words.Add($cur.ToString()) }
    $tokens = @()
    foreach ($w in $words) {
        # Sentence punctuation is trimmed, but one leading dot before a name is a dotfile or dot-folder (.claude/,
        # .gitignore; event-spawns A48) and stays.
        $t = $w.TrimEnd('.', '-', '+').TrimStart('-', '+')
        $t = if ($t -match '^\.[\w]') { $t } else { $t.TrimStart('.') }
        if (-not $t -or $t -notmatch '[/\\*]|^\.?[\w-]+(\.[\w-]+)*\.[A-Za-z]\w*$') { continue }
        # A token of only * and / (a stray ** or a bare glob) would cover every path.
        if ($t -match '^[*/]+$') { continue }
        $tokens += @(Expand-Braces $t)
    }
    return @($tokens | ForEach-Object { $_.Replace('\', '/') } | Sort-Object -Unique)
}

function Expand-Braces([string]$Token) {
    $m = [regex]::Match($Token, '\{([^{}]*)\}')
    if (-not $m.Success) { return @($Token) }
    $head = $Token.Substring(0, $m.Index); $tail = $Token.Substring($m.Index + $m.Length)
    return @($m.Groups[1].Value -split ',' | ForEach-Object { Expand-Braces "$head$_$tail" })
}

# The created or changed paths no declared token covers: a temp folder by a %TEMP%/<glob> token, any other path by a
# token glob (Test-GlobMatch). Returns @{ Total; Uncovered }.
function Get-UndeclaredPaths([string[]]$Changed, [string[]]$Tokens) {
    $temps = @($Tokens | Where-Object { $_ -like '%TEMP%/*' } | ForEach-Object { $_.Substring(7) })
    $globs = @($Tokens | Where-Object { $_ -notlike '%TEMP%/*' })
    $un = @()
    foreach ($c in @($Changed | Sort-Object -Unique)) {
        $kind, $path = $c -split ' ', 2
        $set = if ($kind -eq 'temp') { $temps } else { $globs }
        if (-not ($set | Where-Object { Test-GlobMatch $path $_ })) { $un += $c }
    }
    return @{ Total = @($Changed | Sort-Object -Unique).Count; Uncovered = $un }
}

# Walked paths of the real repository as "<kind> <path>" (kind tracked|ignored|server); only Get-PathsListings calls it.
function Get-WalkedPaths([string]$Root) {
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

# The classes each slug's `dataTests` entry must list (event-spawns A57): the end-path tests D27 names.
$script:DataTestFloor = @{ 'event-spawns' = @('EndPathTests') }

# The test classes tools/preflight-checks.json `dataTests` lists for $Slug (event-spawns D27), names only.
function Get-DataTestClasses([string]$Root, [string]$Slug) {
    $cfg = Read-Text $Root 'tools/preflight-checks.json'
    if ($null -eq $cfg) { return @() }
    try { $entry = ($cfg | ConvertFrom-Json).dataTests.$Slug } catch { return @() }
    return @($entry | Where-Object { "$_" -match '^\w+$' } | Sort-Object -Unique)
}

# One row per class, Class, Passed and Why ($null when it passed): in the real repository from Invoke-ClassTests, each
# class run on its own; in a fixture from class-tests.txt, "<class> <passed>" or "<class> <passed> <why>" per line, as
# Invoke-ClassTests would report it. $null when the fixture lacks the file.
function Get-DataTestRuns([string]$Root, [string[]]$Classes) {
    if (Test-IsFixture $Root) {
        $t = Read-Text $Root 'class-tests.txt'
        if ($null -eq $t) { return $null }
        return @(foreach ($l in @($t -split '\r?\n' | Where-Object { $_ -match '^(\w+) (\d+)(?: (.+))?$' })) {
            $null = $l -match '^(\w+) (\d+)(?: (.+))?$'
            [pscustomobject]@{ Class = $Matches[1]; Passed = [int]$Matches[2]; Why = $Matches[3] }
        })
    }
    $rows = @(Invoke-ClassTests @($Classes | ForEach-Object { Get-ClassFilter $_ }))
    return @(for ($i = 0; $i -lt $Classes.Count; $i++) { [pscustomobject]@{ Class = $Classes[$i]; Passed = $rows[$i].Passed; Why = $rows[$i].Why } })
}

function Test-CheckPaths([string]$Root) {
    $manifest = Get-PathsManifest $Root
    if ($null -eq $manifest -or $manifest.Count -eq 0) { return New-Result $false 'paths: tools/paths-manifest.txt missing or empty' }
    $in = Get-PathsListings $Root
    $walked = @($in.Walked)
    if ($walked.Count -eq 0) { return New-Result $false 'paths: nothing walked' }
    $bad = @()
    foreach ($w in $walked) {
        $kind, $path = $w -split ' ', 2
        if (-not ($manifest | Where-Object { $_.Kind -eq $kind -and (Test-GlobMatch $path $_.Glob) })) { $bad += "$kind $path" }
    }
    $problems = @()
    if ($bad) { $problems += "$($bad.Count) outside the manifest: $(($bad | Select-Object -First 10) -join ', ')" }
    # Writes outside the walked trees (faction-empowerment D26). A tool's %TEMP%\nyar-* folder must be gone once it
    # finishes, so any one fails, declared by a temp: glob or not.
    foreach ($l in @('Temp', 'Worktrees', 'RemoteTags', 'Releases')) { if ($null -eq $in.$l) { $problems += "$l listing unreadable" } }
    foreach ($t in @("$($in.Temp)" -split '\r?\n' | ForEach-Object { $_.Trim() } | Where-Object { $_ -like 'nyar-*' })) { $problems += "leftover temp $t" }
    # Porcelain entries start "worktree <path>"; the first is the main worktree.
    $trees = @("$($in.Worktrees)" -split '\r?\n' | Where-Object { $_ -match '^worktree (.+)$' } | ForEach-Object { $_.Substring(9).Trim() })
    if ($null -ne $in.Worktrees -and $trees.Count -eq 0) { $problems += 'worktree listing has no main worktree' }
    foreach ($t in @($trees | Select-Object -Skip 1)) { $problems += "leftover worktree $t" }
    # Remote tags ("<sha>\trefs/tags/<name>", plus "<name>^{}" peel lines for annotated tags) and releases.
    $remote = @()
    $remote += @("$($in.RemoteTags)" -split '\r?\n' | Where-Object { $_ -match '\srefs/tags/(\S+?)(\^\{\})?\s*$' } |
        ForEach-Object { $null = $_ -match '\srefs/tags/(\S+?)(\^\{\})?\s*$'; "tag $($Matches[1])" })
    if ($null -ne $in.Releases) {
        try { $remote += @(@("$($in.Releases)" | ConvertFrom-Json) | ForEach-Object { $_ } | Where-Object { $_.tagName } | ForEach-Object { "release $($_.tagName)" }) }
        catch { $problems += 'Releases listing is not JSON' }
    }
    foreach ($r in @($remote | Sort-Object -Unique)) {
        $rk, $rn = $r -split ' ', 2
        if (-not ($manifest | Where-Object { $_.Kind -eq "remote-$rk" -and (Test-GlobMatch $rn $_.Glob) })) { $problems += "undeclared remote $r" }
    }
    # -DeclaredOf <slug> (event-library D30, A13): the child's writes against its own Rollout › Paths walked.
    $declLine = ''
    if ($null -ne $in.Declared) {
        $slug = $in.Declared.Slug
        $plan = Read-Text $Root "docs/dod/$slug.md"
        $tokens = if ($plan) { @(Get-DeclaredTokens $plan) } else { @() }
        if ($null -eq $plan) { $problems += "declared: docs/dod/$slug.md not found" }
        elseif ($tokens.Count -eq 0) { $problems += "declared: $slug has no Rollout › Paths walked tokens" }
        elseif ($null -eq $in.Declared.Changed) { $problems += "declared: the created or changed paths of $slug are unreadable (audit Step 1 base or git)" }
        elseif ($null -eq ($base = Get-AuditBase $Root $slug)) { $problems += "declared: docs/audits/$slug.md has no ### Step 1 · <date> · <sha> pre-audit" }
        elseif ($null -eq $in.Declared.AddedParent) { $problems += "declared: git cannot say which commit created docs/audits/$slug.md" }
        elseif (-not $in.Declared.AddedParent.StartsWith($base)) { $problems += "declared: the Step 1 pre-audit base $base is not $($in.Declared.AddedParent.Substring(0, [Math]::Min(12, $in.Declared.AddedParent.Length))), the parent of the commit that created docs/audits/$slug.md" }
        elseif (($unread = @(@(Get-ToolTempNames $Root) + @(Get-ComposedTempNames $Root) + @(Get-UnmarkedTempRoots $Root) | Where-Object { $_ -like 'unreadable *' } | ForEach-Object { $_.Substring(11) } | Sort-Object -Unique)).Count) { $problems += "declared: a tool source is unreadable, so its %TEMP% names are unknown: $(($unread | Select-Object -First 10) -join ', ')" }
        elseif (($unmarked = @(Get-UnmarkedTempRoots $Root)).Count) { $problems += "declared: a tool takes the %TEMP% root on a line that names no nyar-<name> folder and carries no '# nyar-temp: <reason>' registration: $(($unmarked | Select-Object -First 10) -join ', ')" }
        elseif (($composed = @(Get-ComposedTempNames $Root)).Count) { $problems += "declared: a tool composes a %TEMP% nyar- name the scan cannot read; spell its prefix: $(($composed | Select-Object -First 10) -join ', ')" }
        else {
            $u = Get-UndeclaredPaths @(@($in.Declared.Changed) + @(Get-ToolTempNames $Root) + @(Get-RecordTempNames $Root $slug)) $tokens
            if ($u.Uncovered.Count) { $problems += "declared: $($u.Total - $u.Uncovered.Count)/$($u.Total) in $slug, not in its Paths walked: $(($u.Uncovered | Select-Object -First 10) -join ', ')" }
            else { $declLine = "; declared: $($u.Total)/$($u.Total) in $slug" }
        }
        # The slug's data tests (event-spawns D27): the classes tools/preflight-checks.json `dataTests` lists for it, the
        # persisted artifacts' and runtime state's tests (EndPathTests, D33), so probe 3.3 is this one command. A plan that
        # names `dataTests` needs its entry; the plans closed before it (event-library, regions, ...) keep their commands.
        $classes = @(Get-DataTestClasses $Root $slug)
        $wantsData = $null -ne $plan -and $plan.Contains('dataTests')
        # The floor (event-spawns A57): the entry is editable, so the classes a plan names cannot be dropped from it.
        $missFloor = @($script:DataTestFloor[$slug] | Where-Object { $_ -and $classes -cnotcontains $_ })
        if ($classes.Count -eq 0) { if ($wantsData -or $missFloor) { $problems += "$slug names no data tests" } }
        elseif ($missFloor) { $problems += "$slug data tests miss $($missFloor -join ', ') (floor)" }
        else {
            $runs = Get-DataTestRuns $Root $classes
            if ($null -eq $runs) { $problems += "data tests of $slug unreadable" }
            else {
                $failed = @(foreach ($c in $classes) {
                    $r = @($runs | Where-Object Class -eq $c)[0]
                    if ($null -eq $r -or $r.Passed -eq 0) { "tests: $c ran 0 tests" } elseif ($r.Why) { "tests: $c failed ($($r.Why))" }
                })
                if ($failed) { $problems += $failed }
                else { $declLine += "; data tests $(($runs | Measure-Object Passed -Sum).Sum) passed" }
            }
        }
    }
    # The real tree's -DeclaredOf also runs every planted Paths fixture, and each must fail (event-library A19), so
    # the gating command cannot pass with a scan removed. A second listing of %TEMP% after the checks catches a nyar-*
    # folder made while they ran.
    if ($null -ne $in.Declared -and -not (Test-IsFixture $Root)) {
        $plantDir = Join-Path $Root 'tools/preflight-fixtures/Paths'
        $plants = @(Get-ChildItem -LiteralPath $plantDir -Directory -Filter 'bad*' -ErrorAction SilentlyContinue | Sort-Object Name)
        $need = @('bad-base', 'bad-composed', 'bad-datatests', 'bad-datatests-floor', 'bad-record', 'bad-scratch', 'bad-tempvar', 'bad-transient', 'bad-undeclared')
        $absent = @($need | Where-Object { $plants.Name -notcontains $_ })
        $passing = @()
        foreach ($p in $plants) {
            $script:FixtureRoot = $p.FullName
            try { $r = Test-CheckPaths $p.FullName } finally { $script:FixtureRoot = $null }
            if ($r.Pass) { $passing += $p.Name }
            # bad-datatests-floor would fail for another fault too, so it must fail for its floor (Review 28 F1).
            elseif ($p.Name -eq 'bad-datatests-floor' -and $r.Line -notmatch 'miss EndPathTests \(floor\)') { $passing += "$($p.Name) (not for its floor: $($r.Line))" }
        }
        # bad-tempvar holds many unmarked %TEMP% roots, and one would mask another, so each is asserted on its own
        # (event-library A24): the scan must report exactly the lines its unmarked.txt lists.
        $tvRoot = Join-Path $plantDir 'bad-tempvar'
        $tvWant = @(Get-Content -LiteralPath (Join-Path $tvRoot 'unmarked.txt') -ErrorAction SilentlyContinue | Where-Object { $_.Trim() } | Sort-Object -Unique)
        $tvGot = @(Get-UnmarkedTempRoots $tvRoot)
        $tvMiss = @($tvWant | Where-Object { $tvGot -notcontains $_ })
        $tvExtra = @($tvGot | Where-Object { $tvWant -notcontains $_ })
        if ($absent) { $problems += "plants: missing $($absent -join ', ')" }
        elseif ($passing) { $problems += "plants: $($passing -join ', ') passed" }
        elseif (-not $tvWant.Count) { $problems += "plants: bad-tempvar/unmarked.txt missing or empty" }
        elseif ($tvMiss -or $tvExtra) { $problems += "plants: bad-tempvar lines not reported: $($tvMiss -join ', '); reported but not listed: $($tvExtra -join ', ')" }
        else { $declLine += "; plants: $($plants.Count)/$($plants.Count) fail, tempvar $($tvWant.Count)/$($tvWant.Count) lines" }
        try { $late = @(Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Directory -Filter 'nyar-*' -Force -ErrorAction Stop | ForEach-Object Name) }   # nyar-temp: the second listing, creates nothing
        catch { $late = $null; $problems += 'Temp listing unreadable (second pass)' }
        $first = @("$($in.Temp)" -split '\r?\n' | ForEach-Object { $_.Trim() })
        foreach ($t in @($late | Where-Object { $_ -and $first -notcontains $_ })) { $problems += "leftover temp $t (second pass)" }
    }
    if ($problems) { return New-Result $false "paths: $($problems -join '; ')" }
    return New-Result $true "paths: $($walked.Count) walked, all in manifest$declLine"
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
    # regions D2 (A2, A14): a log that loads Nyarlathotep 0.6.0 or later carries the boot's "regions: <p> polygons"
    # line with p > 0 and no names-differ warning; a log with nyar lines whose version cannot be read fails rather
    # than skip the check. The last load wins (a log may hold one boot only, but a restart could append).
    $regionsWhy = $null
    if ($s -gt 0) {
        $loads = [regex]::Matches($log, 'Loading \[Nyarlathotep (\d+)\.(\d+)\.(\d+)[^\]]*\]')
        if ($loads.Count -eq 0) { $regionsWhy = 'the Nyarlathotep version is unreadable' }
        else {
            $v = $loads[$loads.Count - 1]
            $boot = $log.Substring($v.Index)                          # the last boot's lines only (regions A37)
            if ([version]"$($v.Groups[1].Value).$($v.Groups[2].Value).$($v.Groups[3].Value)" -ge [version]'0.6.0') {
                $rl = [regex]::Matches($boot, '\[nyar\] regions: (\d+) polygons')
                if ($rl.Count -eq 0) { $regionsWhy = 'no "regions:" boot line' }
                elseif ([int]$rl[$rl.Count - 1].Groups[1].Value -eq 0) { $regionsWhy = 'the "regions:" line names 0 polygons' }
                elseif ($boot -match '\[nyar\] regions: names differ from the game') { $regionsWhy = 'regions: names differ from the game' }
                else { $line += ", regions $([int]$rl[$rl.Count - 1].Groups[1].Value) polygons" }
            }
        }
    }
    # Every kind is listed: -SessionsOf needs each one attributed in the session's audit line (A13).
    if ($kinds.Count) { $line += " [$($kinds -join ' | ')]" }
    if ($s -eq 0) { return New-Result $false "$line (the wrong log, or the plugin did not initialise)" }
    if ($regionsWhy) { return New-Result $false "$line ($regionsWhy)" }
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
# Pusher runs ActionKind.Subscribe (`.nyar api sub`, raphael-api-core step 3).
# EmpowerAction is the empowerment service EventRuntime and SpawnTracker call directly (faction-empowerment D21).
# AdminOps is the game side of Logic/AdminFlows (raphael-api-admin D11): its Op members are one-call shims.
# HuntAction writes the AggroBuffer seeds of our own Hunt units, driven by the scheduler's "hunt" phase (event-spawns D13, D22, A70).
$script:DispatchedServices = @('EventRuntime', 'SpawnTracker', 'UnitSetup', 'WaveAction', 'Persistence', 'EventStore', 'Announcer', 'Pusher', 'EmpowerAction', 'TemplateLibrary', 'PillarSwitches', 'AdminOps', 'HuntAction') |
    ForEach-Object { "$PkgRel/Services/$_.cs" }

# A member access of an AdminOps or IAdminOps member: ".Op<Name>", called or taken as a method group (raphael-api-admin
# D11). A declaration has no ".".
$script:OpAccessRx = '\.\s*(Op[A-Z]\w*)\b'

# The spans of the gate calls in $Text: each "(" … ")" of a match of $Rx, whose last character is the "(".
function Get-GateSpans([string]$Text, [string]$Rx) {
    return , @(foreach ($g in [regex]::Matches($Text, $Rx)) {
        $open = $g.Index + $g.Length - 1
        , @($open, (Get-ParenEnd $Text $open))
    })
}

# The span of argument $Arg (0-based) of each gate call in $Text: the text between the top-level commas of the call's
# parentheses, brackets, braces and type argument lists ($script:GenericArgsRx) balanced.
function Get-GateArgSpans([string]$Text, [string]$Rx, [int]$Arg) {
    return , @(foreach ($g in [regex]::Matches($Text, $Rx)) {
        $open = $g.Index + $g.Length - 1
        $close = Get-ParenEnd $Text $open
        if ($close -lt 0) { continue }
        $depth = 0; $n = 0; $from = $open
        for ($i = $open + 1; $i -le $close; $i++) {
            $c = $Text[$i]
            if ($i -eq $close -or ($c -eq ',' -and $depth -eq 0)) {
                if ($n -eq $Arg) { , @($from, $i); break }
                $n++; $from = $i
            }
            elseif ($c -eq '<' -and $i -gt 0 -and ([char]::IsLetterOrDigit($Text[$i - 1]) -or $Text[$i - 1] -eq '_')) {
                $gen = $script:GenericArgsRx.Match($Text, $i)
                if ($gen.Success) { $i += $gen.Length - 1 }                                 # a type argument list: skip it whole
            }
            elseif ($c -eq '(' -or $c -eq '[' -or $c -eq '{') { $depth++ }
            elseif ($c -eq ')' -or $c -eq ']' -or $c -eq '}') { $depth-- }
        }
    })
}

# A C# type argument list at a "<" right after a name: "<A, B<C, D>[]?>", nested to two levels, and followed, as C#'s
# own disambiguation requires, by one of ( ) ] } : ; , . ? = ! >; so a pair of comparisons in two arguments,
# "a < b, c > d", is not one ("d" follows its ">").
$script:GenericArgsRx = [regex]'\G<[\w\s,.?\[\]]*(?:<[\w\s,.?\[\]]*(?:<[\w\s,.?\[\]]*>[\w\s,.?\[\]]*)*>[\w\s,.?\[\]]*)*>(?=\s*[()\]}:;,.?=!>])'

function Test-InSpan([int]$Index, $Spans) {
    foreach ($s in $Spans) { if ($s[1] -ge 0 -and $Index -gt $s[0] -and $Index -lt $s[1]) { return $true } }
    return $false
}

# The System actor (event-spawns D22, A23): the scheduler tick, the deferred init and the Harmony patches run with no
# chat identity, so EventScheduler.cs, Core.cs and every Patches/ file may call these [Mutating] tick and boot entry
# points directly: SpawnTracker.Tick and EventRuntime.Tick (the scheduler's phases), EmpowerAction.BeginCarrierTick and
# TickCarriers (EmpowerAction's tick), HuntAction.Tick (the scheduler's "hunt" phase, A70) and SpawnTracker.BootSweep
# (the boot marker sweep, from Core). Only these names:
# any other [Mutating] name in those files still fails outside Gateway.Run.
# Each is "<service>.<method>": the use must name its service right before the method (SpawnTracker.Tick, or
# Services.SpawnTracker.BootSweep), so another service's [Mutating] Tick is no entry point (Codex step 1 F2).
$script:SystemEntryPoints = @('SpawnTracker.Tick', 'SpawnTracker.BootSweep', 'EventRuntime.Tick', 'EmpowerAction.BeginCarrierTick', 'EmpowerAction.TickCarriers', 'HuntAction.Tick')
# True when the use of $Name at $Index in $Text is written "<Type>.<Name>" and that pair is a System-actor entry point.
# A use written "<Type>.<Name>" whose type's own file, Services/<Type>.cs, declares no [Mutating] <Name> is that type's
# method, not a mutating one (Announcer.Tick beside SpawnTracker.Tick), so a System-actor file may name it too.
function Test-SystemEntryUse([string]$Text, [int]$Index, [string]$Name, $Mutating, $Files) {
    $m = [regex]::Match($Text.Substring(0, $Index), '(\w+)\s*\.\s*$')
    if (-not $m.Success) { return $false }
    $type = $m.Groups[1].Value
    if ($script:SystemEntryPoints -contains "$type.$Name") { return $true }
    $own = "$PkgRel/Services/$type.cs"
    return $Files -contains $own -and @($Mutating[$Name]) -notcontains $own
}
# A method declaration's return type: words, generics, arrays, nullables, and tuples such as "(int Queued, int
# Cancelled)" (Review 25 F1: SpawnTracker.RequestWave, EndEventUnits and PurgeUnits return tuples and were never seen).
$script:MethodTypeRx = '(?:[\w<>\[\],.?]|\([^()\n]*\))(?:[\w<>\[\],.? ]|\([^()\n]*\))*?'
$script:MethodModsRx = '(?:(?:public|internal|private|protected|static|override|virtual|async|sealed|new|extern)\s+)+'
# A [Mutating] method declaration; group 1 is its name.
$script:MutatingDeclRx = '\[Mutating\]\s*(?:\[[^\]]*\]\s*)*' + $script:MethodModsRx + $script:MethodTypeRx + '\s+(\w+)\s*(?:<[^<>()]*>)?\s*\('
# Every [Mutating] method of the real tree, by service (A55). Test-CheckMutatingFloor fails when a listed service or
# method is missing, when a listed method is declared without [Mutating], and when a [Mutating] method is not listed,
# so the list cannot fall behind the tree and removing the attribute cannot drop a writer out of the gateway check.
$script:MutatingFloor = [ordered]@{
    'AdminOps'        = @('OpAuthor', 'OpDelete', 'OpEdit', 'OpPurge', 'OpReload', 'OpSetPillar', 'OpStartEvent', 'OpStopEvent', 'OpUseTemplate')
    'Announcer'       = @('AdminAnnounce')
    'EmpowerAction'   = @('BeginCarrierTick', 'EndCarriers', 'QueueBootCarriers', 'StartCarriers', 'StopAllCarriers', 'StopCarriers', 'TickCarriers')
    'EventRuntime'    = @('Purge', 'StartEvent', 'StopEvent', 'Tick')
    'EventStore'      = @('Author', 'DeleteDefinition', 'Edit', 'Reload')
    'HuntAction'      = @('Tick')
    'Persistence'     = @('Delete', 'Promote', 'PromoteNew', 'Rename', 'WriteFile')
    'PillarSwitches'  = @('SetPillar')
    'Pusher'          = @('Subscribe', 'Unsubscribe')
    'SpawnTracker'    = @('BootSweep', 'EndEventUnits', 'PurgeUnits', 'RequestWave', 'SpawnManual', 'Tick')
    'TemplateLibrary' = @('UseTemplate')
    'UnitSetup'       = @('Apply', 'StatModifiers')
    'WaveAction'      = @('QueueDueWave')
}
$script:SystemCallers = @("$PkgRel/Services/EventScheduler.cs", "$PkgRel/Core.cs")
function Test-IsSystemCaller([string]$File) { $script:SystemCallers -contains $File -or $File -like "$PkgRel/Patches/*" }

# The [Mutating] floor (event-spawns D22, A55): every service of $script:MutatingFloor exists, each listed method is
# declared there with [Mutating] (a rename or a removed attribute fails), no [Mutating] method of any .cs file of the
# plugin is unlisted, and every [Mutating] attribute in code belongs to a declaration the check can read.
function Test-CheckMutatingFloor([string]$Root) {
    $files = @(Get-CsFiles $Root | Where-Object { $_.StartsWith("$PkgRel/") -and -not $_.StartsWith("$PkgRel/Logic/") })
    if ($files.Count -eq 0) { return New-Result $false 'mutating floor: no source files' }
    $anyDecl = "(?:\[[^\]]*\]\s*)*$($script:MethodModsRx)$($script:MethodTypeRx)\s+({0})\s*(?:<[^<>()]*>)?\s*\("
    $bad = @(); $listed = 0; $found = @{}
    foreach ($f in $files) {
        $t = Remove-CsLiterals (Read-Text $Root $f)
        $decls = @([regex]::Matches($t, $script:MutatingDeclRx))
        # any attribute list naming Mutating, as the entity-writes check reads it ([Mutating()], [Mutating, Obsolete],
        # [MutatingAttribute]): a spelling the declaration regex cannot read fails as unreadable (Review 26 F1)
        $attrs = [regex]::Matches($t, '\[[^\]]*\bMutating(?:Attribute)?\b[^\]]*\]').Count
        if ($attrs -ne $decls.Count) { $bad += "$f has $attrs [Mutating] attributes but $($decls.Count) readable declarations" }
        foreach ($d in $decls) { $found["$f|$($d.Groups[1].Value)"] = $true }
    }
    foreach ($svc in $script:MutatingFloor.Keys) {
        $f = "$PkgRel/Services/$svc.cs"
        if ($files -notcontains $f) { $bad += "service $svc is missing"; continue }
        $t = Remove-CsLiterals (Read-Text $Root $f)
        foreach ($name in $script:MutatingFloor[$svc]) {
            $listed++
            if (-not $found.ContainsKey("$f|$name")) {
                $bad += if ([regex]::IsMatch($t, ($anyDecl -f $name))) { "$svc.$name is declared without [Mutating]" } else { "$svc.$name is not declared" }
            }
        }
    }
    foreach ($k in $found.Keys) {
        $f, $name = $k -split '\|', 2
        $svc = [IO.Path]::GetFileNameWithoutExtension($f)
        if ($f -ne "$PkgRel/Services/$svc.cs" -or -not $script:MutatingFloor.Contains($svc) -or $script:MutatingFloor[$svc] -notcontains $name) {
            $bad += "$svc.$name is [Mutating] but not in the floor"
        }
    }
    if ($bad) { return New-Result $false "mutating floor: $(@($bad | Sort-Object -Unique) -join '; ')" }
    return New-Result $true "mutating floor: $listed/$listed listed methods [Mutating] in $($script:MutatingFloor.Count) services, none unlisted"
}

# Every method marked [Mutating] in a dispatched service is a mutating method. Any other .cs file of the plugin outside
# Logic/ (Plugin.cs, Core.cs, Config/ and EntityExtensions.cs included, event-spawns D22 and A2; Get-TreeFiles, so a new
# untracked file is seen) may name a mutating method only inside the parentheses of a Gateway.Run(...) call, or, in a
# System-actor file, name one of $script:SystemEntryPoints; every named use inside such a call, in any of the walked
# files, is a call site. A use is the identifier anywhere except its own declaration, so a method group captured
# outside Gateway.Run and passed in later fails too (foundation D11, Epic D36), and so does a method of another type
# that merely shares a [Mutating] name (the names are matched bare, event-spawns A16).
# raphael-api-admin D11 (A1, A2): an access ".Op<Name>" of AdminOps or IAdminOps also fails outside the work argument
# (the third) of a Gateway.Run in every scanned file but the dispatched services; every Logic/*.cs file is scanned for
# it too, where the gate is the work argument of "<g>.Run<…>(" and <g> a name the file declares as an ActionGateway
# (AdminFlows reaches IAdminOps only there: not in the denied argument, not in another type's Run<T>).
function Test-CheckGatewayOnly([string]$Root) {
    $files = @(Get-CsFiles $Root | Where-Object { $_.StartsWith("$PkgRel/") -and -not $_.StartsWith("$PkgRel/Logic/") })
    $texts = @{}
    foreach ($f in $files) { $texts[$f] = Remove-CsLiterals (Read-Text $Root $f) }
    $decl = $script:MutatingDeclRx
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
    if ($mutating.Count -eq 0) { return New-Result $false 'gateway: no [Mutating] method found' }
    $dispatched = $script:DispatchedServices
    $strays = @($mutating.Values | ForEach-Object { $_ } | Sort-Object -Unique | Where-Object { $dispatched -notcontains $_ })
    if ($strays) { return New-Result $false "gateway: [Mutating] declared outside the dispatched services in $($strays -join ', ')" }
    $sites = 0; $system = 0; $bad = @()
    # A using alias named after a service ("using SpawnTracker = ...Persistence;", global or not, in any file of the
    # plugin) would make "<Type>.<Name>" name another type, so the System-actor pairs could not be trusted (Codex step 1
    # round 2 F1).
    $serviceTypes = @($files | Where-Object { $_ -like "$PkgRel/Services/*.cs" } | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_) })
    foreach ($f in @(Get-CsFiles $Root | Where-Object { $_.StartsWith("$PkgRel/") })) {
        foreach ($a in [regex]::Matches((Remove-CsLiterals (Read-Text $Root $f)), '(?m)^\s*(?:global\s+)?using\s+(\w+)\s*=')) {
            if ($serviceTypes -contains $a.Groups[1].Value) { $bad += "using alias $($a.Groups[1].Value) names a service in $f" }
        }
    }
    # Likewise a field, property, local or parameter named after a service in a System-actor file ("static Store
    # SpawnTracker = ...;", "var Announcer = Persistence.Disk;"): "<Type>.<Name>" would name that value (Review 24 F1).
    $notTypes = @('return', 'new', 'is', 'as', 'in', 'out', 'ref', 'case', 'typeof', 'nameof', 'throw', 'await', 'yield', 'else', 'using', 'class', 'namespace', 'static')
    foreach ($f in @($files | Where-Object { Test-IsSystemCaller $_ })) {
        foreach ($d in [regex]::Matches($texts[$f], '(?<![.\w])([A-Za-z_][\w.]*(?:<[^<>;{}()]*>)?\??(?:\[\])?)\s+(\w+)\s*(?:=(?!=)|;|,|\)|\{|=>)')) {
            if ($notTypes -notcontains $d.Groups[1].Value -and $serviceTypes -contains $d.Groups[2].Value) {
                $bad += "$($d.Groups[2].Value) is declared as a value in $f, shadowing the service"
            }
        }
    }
    foreach ($f in $files) {
        $t = $texts[$f]
        $spans = Get-GateSpans $t '\bGateway\s*\.\s*Run\s*\('
        $work = Get-GateArgSpans $t '\bGateway\s*\.\s*Run\s*\(' 2
        $sysFile = Test-IsSystemCaller $f
        foreach ($name in $mutating.Keys) {
            foreach ($u in [regex]::Matches($t, "\b$name\b")) {
                if ($declAt.ContainsKey("$f|$($u.Index)")) { continue }
                if (Test-InSpan $u.Index $spans) { $sites++ }
                elseif ($dispatched -contains $f) { continue }
                elseif ($sysFile -and (Test-SystemEntryUse $t $u.Index $name $mutating $files)) { $system++ }
                else { $bad += "$name used outside Gateway.Run in $f" }
            }
        }
        if ($dispatched -notcontains $f) {
            foreach ($u in [regex]::Matches($t, $script:OpAccessRx)) {
                if (-not (Test-InSpan $u.Index $work)) { $bad += "$($u.Groups[1].Value) used outside Gateway.Run's work in $f" }
            }
        }
    }
    $opSites = 0
    foreach ($f in @(Get-CsFiles $Root | Where-Object { $_ -like "$PkgRel/Logic/*.cs" })) {
        $t = Remove-CsLiterals (Read-Text $Root $f)
        $gates = @([regex]::Matches($t, '\bActionGateway\s+(\w+)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
        $work = if ($gates) { Get-GateArgSpans $t "\b(?:$($gates -join '|'))\s*\.\s*Run\s*<[^()]*?>\s*\(" 2 } else { @() }
        foreach ($u in [regex]::Matches($t, $script:OpAccessRx)) {
            if (Test-InSpan $u.Index $work) { $opSites++ } else { $bad += "$($u.Groups[1].Value) used outside ActionGateway.Run<T>'s work in $f" }
        }
    }
    if ($bad) { return New-Result $false "gateway: $(@($bad | Select-Object -Unique) -join '; ')" }
    return New-Result $true "gateway: only ActionGateway mutates ($sites call sites, $opSites admin ops inside Run<T>, $system System-actor entry calls)"
}

# ---------------------------------------------------------------- entity writes (event-spawns D34)

# The entity-write patterns one regex finds, by label (D34). Each match is one write site; no form is excluded.
$script:EntityWriteRx = [ordered]@{
    '.Write'             = '\.\s*Write\s*[(<]'
    'EntityExtensions'   = '\b(?:AddComponentSafe|RemoveComponentSafe|AddBufferSafe|DestroySafe|RemoveBuffSafe)\b'
    'instantiate'        = '\b(?:InstantiateEntityImmediate|TryInstantiateBuffEntityImmediate)\b'
    'EntityManager'      = '\bEntityManager\s*\.\s*(?:(?:Set|Add|Remove|Destroy|Instantiate)\w*|CreateEntity\b)'
    '.ValueRW'           = '\.\s*ValueRW\b'
    'DestroyUtility'     = '\bDestroyUtility\s*\.\s*Destroy\w*\s*\('   # the deferred destroy, as StructuralEdits spells it (A49)
    'SystemAPI'          = '\bSystemAPI\s*\.\s*(?:SetComponent|SetBuffer|SetComponentEnabled)\b'
}
# A pointer write cannot be classified, so any of these fails the check outright.
$script:EntityUnsafeRx = '\bunsafe\b|\bGetUnsafePtr\b|\bGetUnsafeReadOnlyPtr\b|\bUnsafeUtility\b'
# A write through a buffer: a mutating call or an (compound) indexer assignment.
$script:BufferWriteTail = '\s*(?:\.\s*(?:Add|AddRange|Clear|RemoveAt|RemoveRange|RemoveAtSwapBack|Insert|InsertRange|ResizeUninitialized|Resize|ElementAt|CopyFrom|TrimExcess|EnsureCapacity)\s*\(|\[[^\]]*\]\s*(?:[-+*/%&|^]|<<|>>|\?\?)?=(?![=>]))'
$script:EcbMethodRx = '\.\s*(?:AddComponent|SetComponent|SetComponentEnabled|RemoveComponent|DestroyEntity|AppendToBuffer|SetBuffer|AddBuffer|Instantiate)\b'
$script:GenericTail = '<(?:[^<>;{}()]|<(?:[^<>;{}()]|<[^<>;{}()]*>)*>)*>'

# The index of the character that ends the expression statement starting inside $T at $From: a ";" or a "{" outside
# every bracket opened after $From (a ")" of a bracket opened before it does not end it), or the "}" of the block.
function Get-EwStatementEnd([string]$T, [int]$From) {
    $pd = 0; $bd = 0
    for ($i = $From; $i -lt $T.Length; $i++) {
        $c = $T[$i]
        if ($c -eq '(' -or $c -eq '[') { $pd++ }
        elseif ($c -eq ')' -or $c -eq ']') { $pd-- }
        elseif ($c -eq '{') { if ($pd -le 0 -and $bd -eq 0) { return $i }; $bd++ }
        elseif ($c -eq '}') { if ($bd -eq 0) { return $i }; $bd-- }
        elseif ($c -eq ';' -and $bd -eq 0 -and $pd -le 0) { return $i }
    }
    return $T.Length
}

# The index just after the ";", "{" or "}" that starts the statement holding $At.
function Get-EwStatementStart([string]$T, [int]$At) {
    for ($i = $At - 1; $i -ge 0; $i--) { if ($T[$i] -eq ';' -or $T[$i] -eq '{' -or $T[$i] -eq '}') { return $i + 1 } }
    return 0
}

# $Text with comments blanked and every literal masked to "_" except an interpolated string's holes, which stay code
# (Hide-InterpolatedText). Every step keeps the length, so offsets and line numbers match the source.
function Get-EwCodeText([string]$Text) {
    return $script:CsLexRx.Replace((Hide-CsSpans $Text), {
        param($m)
        if ($m.Value.StartsWith('$') -or $m.Value.StartsWith('@$')) { return Hide-InterpolatedText $m.Value }
        return [regex]::Replace($m.Value, '[^\r\n]', '_')
    })
}

# The (open, close) of the innermost "{ }" block around $At, or (-1, length) at the top.
function Get-EwBlock([string]$T, [int]$At) {
    $depth = 0
    for ($i = $At - 1; $i -ge 0; $i--) {
        if ($T[$i] -eq '}') { $depth++ }
        elseif ($T[$i] -eq '{') { if ($depth -eq 0) { $close = Get-CloseIndex $T $i '{' '}'; return @($i, $(if ($close -lt 0) { $T.Length } else { $close })) }; $depth-- }
    }
    return @(-1, $T.Length)
}

# Every entity write site of $T (Get-EwCodeText: comments and literals masked, interpolation holes kept), a hashtable
# index -> label. Statements and blocks are found in $S (Hide-CsSpans -Literals, holes masked too), which has the same
# offsets, so a quote nested in a hole (the known lexer limit) cannot unbalance a brace.
function Find-EntityWrites([string]$T, [string]$S) {
    $sites = @{}
    foreach ($k in $script:EntityWriteRx.Keys) {
        foreach ($m in [regex]::Matches($T, $script:EntityWriteRx[$k])) { if (-not $sites.ContainsKey($m.Index)) { $sites[$m.Index] = $k } }
    }
    # An EntityManager held in a local or field: its Set*, Add*, Remove*, Destroy*, Instantiate* and CreateEntity calls.
    $ems = @([regex]::Matches($T, '\bEntityManager\s+(\w+)\s*[=;,)]') | ForEach-Object { $_.Groups[1].Value }) +
        @([regex]::Matches($T, '(\w+)\s*=\s*[\w.]*\bEntityManager\s*;') | ForEach-Object { $_.Groups[1].Value })
    foreach ($n in @($ems | Where-Object { $_ -and $_ -ne 'EntityManager' } | Sort-Object -Unique)) {
        foreach ($m in [regex]::Matches($T, "\b$n\s*\.\s*(?:(?:Set|Add|Remove|Destroy|Instantiate)\w*|CreateEntity\b)")) { if (-not $sites.ContainsKey($m.Index)) { $sites[$m.Index] = 'EntityManager' } }
    }
    # GetBuffer<…>(…) written in the same statement: .Add(, .Clear(, another mutating call or an indexer assignment.
    foreach ($g in [regex]::Matches($T, '\bGetBuffer\s*<')) {
        $end = Get-EwStatementEnd $S $g.Index
        $w = [regex]::new($script:BufferWriteTail).Match($T.Substring($g.Index, $end - $g.Index))
        if ($w.Success -and -not $sites.ContainsKey($g.Index + $w.Index)) { $sites[$g.Index + $w.Index] = 'GetBuffer' }
    }
    # A buffer local: assigned from GetBuffer< or ReadBuffer (any declared type, var included), taken as TryGetBuffer's out
    # var, or declared DynamicBuffer<…>; written later in its block.
    $locals = @()
    foreach ($g in [regex]::Matches($T, '\bGetBuffer\s*<|\bReadBuffer\b|\bTryGetBuffer\b')) {
        $st = Get-EwStatementStart $S $g.Index
        $end = Get-EwStatementEnd $S $g.Index
        $a = [regex]::Matches($T.Substring($st, $g.Index - $st), '(\w+)\s*(?<![=!<>])=(?![=>])')
        if ($a.Count) { $locals += , @($a[$a.Count - 1].Groups[1].Value, $st) }
        foreach ($o in [regex]::Matches($T.Substring($g.Index, $end - $g.Index), '\bout\s+var\s+(\w+)')) { $locals += , @($o.Groups[1].Value, $g.Index) }
    }
    foreach ($d in [regex]::Matches($T, "\bDynamicBuffer\s*$($script:GenericTail)\s*(\w+)")) { $locals += , @($d.Groups[1].Value, $d.Index) }
    foreach ($l in $locals) {
        $blk = Get-EwBlock $S $l[1]
        $from = $l[1]; $to = $blk[1]
        foreach ($m in [regex]::Matches($T.Substring($from, $to - $from), "\b$($l[0])$($script:BufferWriteTail)")) {
            $i = $from + $m.Index
            if (-not $sites.ContainsKey($i)) { $sites[$i] = 'buffer local' }
        }
    }
    # ComponentLookup / BufferLookup / ComponentDataFromEntity locals: an indexer assignment.
    $lookups = @([regex]::Matches($T, "\b(?:ComponentLookup|BufferLookup|ComponentDataFromEntity|BufferFromEntity)\s*$($script:GenericTail)\s*(\w+)") | ForEach-Object { $_.Groups[1].Value }) +
        @([regex]::Matches($T, '(\w+)\s*=\s*[^;]*?\b(?:GetComponentLookup|GetBufferLookup|GetComponentDataFromEntity|GetBufferFromEntity)\b') | ForEach-Object { $_.Groups[1].Value })
    foreach ($n in @($lookups | Sort-Object -Unique)) {
        foreach ($m in [regex]::Matches($T, "\b$n\s*\[[^\]]*\]\s*(?:[-+*/%&|^]|<<|>>|\?\?)?=(?![=>])")) { if (-not $sites.ContainsKey($m.Index)) { $sites[$m.Index] = 'lookup indexer' } }
    }
    # An EntityCommandBuffer or CommandBuffer receiver's structural or component call.
    $ecbs = @([regex]::Matches($T, '\bEntityCommandBuffer(?:\s*\.\s*ParallelWriter)?\s+(\w+)') | ForEach-Object { $_.Groups[1].Value }) +
        @([regex]::Matches($T, '(\w+)\s*=\s*[^;]*?\b(?:CreateCommandBuffer|AsParallelWriter)\s*\(') | ForEach-Object { $_.Groups[1].Value })
    foreach ($m in [regex]::Matches($T, $script:EcbMethodRx)) {
        $pre = $T.Substring([Math]::Max(0, $m.Index - 300), [Math]::Min(300, $m.Index))
        $r = [regex]::Match($pre, '([\w.]+(?:\s*\([^()]*\))?)\s*$')
        if (-not $r.Success) { continue }
        $root = ([regex]::Match($r.Groups[1].Value, '\w+')).Value
        if ($r.Groups[1].Value -match '(?i)CommandBuffer|\becb\b' -or $ecbs -contains $root) { if (-not $sites.ContainsKey($m.Index)) { $sites[$m.Index] = 'command buffer' } }
    }
    return $sites
}

# The type declarations of $T: Name, Open, Close, Private (no public/internal/protected in its header) and Parent (an
# index into the list, -1 at the top level). A declaration with no body (a positional record) is skipped.
function Get-EwTypes([string]$T) {
    $types = [Collections.Generic.List[object]]::new()
    foreach ($m in [regex]::Matches($T, '\b(?:record\s+(?:class|struct)|class|struct|record|interface|enum)\s+(\w+)')) {
        $open = -1
        for ($i = $m.Index + $m.Length; $i -lt $T.Length; $i++) { if ($T[$i] -eq '{') { $open = $i; break }; if ($T[$i] -eq ';') { break } }
        if ($open -lt 0) { continue }
        $close = Get-CloseIndex $T $open '{' '}'
        if ($close -lt 0) { $close = $T.Length }
        $hs = Get-EwStatementStart $T $m.Index
        $head = [regex]::Replace($T.Substring($hs, $m.Index - $hs), '\[[^\]]*\]', ' ')
        $types.Add([pscustomobject]@{ Name = $m.Groups[1].Value; Open = $open; Close = $close; Private = $head -notmatch '\b(?:public|internal|protected)\b'; Parent = -1 })
    }
    for ($a = 0; $a -lt $types.Count; $a++) {
        $best = -1
        for ($b = 0; $b -lt $types.Count; $b++) {
            if ($a -eq $b) { continue }
            if ($types[$b].Open -lt $types[$a].Open -and $types[$b].Close -gt $types[$a].Close -and ($best -lt 0 -or $types[$b].Open -gt $types[$best].Open)) { $best = $b }
        }
        $types[$a].Parent = $best
    }
    return , $types
}

# The members of the type body ($Open, $Close) of $T: each declaration at depth 0 of the body up to its ";" or its
# closing "}". Start, Body (the index of its first "{" or "=" at depth 0, where the header ends), End, Name, NonPrivate,
# Mutating and IsType.
function Get-EwMembers([string]$T, [int]$Open, [int]$Close) {
    $out = [Collections.Generic.List[object]]::new()
    $bd = 0; $pd = 0; $start = $Open + 1; $head = -1
    for ($i = $Open + 1; $i -le $Close; $i++) {
        $c = $T[$i]; $endAt = -1
        if ($i -eq $Close) { $endAt = $i }
        elseif ($c -eq '(' -or $c -eq '[') { $pd++ }
        elseif ($c -eq ')' -or $c -eq ']') { $pd-- }
        elseif ($c -eq '{') { if ($bd -eq 0 -and $pd -eq 0 -and $head -lt 0) { $head = $i }; $bd++ }
        elseif ($c -eq '}') { $bd--; if ($bd -eq 0 -and $pd -eq 0) { $endAt = $i } }
        elseif ($c -eq '=' -and $bd -eq 0 -and $pd -eq 0 -and $head -lt 0) { $head = $i }
        elseif ($c -eq ';' -and $bd -eq 0 -and $pd -eq 0) { $endAt = $i }
        if ($endAt -lt 0) { continue }
        $hEnd = if ($head -ge 0) { $head } else { $endAt }
        $header = $T.Substring($start, $hEnd - $start)
        if ($header -match '\S') {
            $mut = $header -match '\[[^\]]*\bMutating\b[^\]]*\]'
            $plain = [regex]::Replace($header, '\[[^\]]*\]', ' ')
            $isType = $plain -match '\b(?:class|struct|record|interface|enum)\s+\w+'
            $name = ''
            $p = $plain.IndexOf('(')
            if ($p -ge 0) { $nm = [regex]::Match($plain.Substring(0, $p), "(\w+)\s*(?:$($script:GenericTail))?\s*$"); if ($nm.Success) { $name = $nm.Groups[1].Value } }
            else { $nm = [regex]::Matches($plain, '\w+'); if ($nm.Count) { $name = $nm[$nm.Count - 1].Value } }
            $out.Add([pscustomobject]@{ Start = $start; Body = $hEnd; End = $endAt; Name = $name; NonPrivate = $plain -match '\b(?:public|internal|protected)\b'; Mutating = $mut; IsType = $isType })
        }
        $start = $endAt + 1; $head = -1
    }
    return , $out
}

# Entity writes stay in services (event-spawns D34). Every .cs file of the plugin outside Logic/ (Get-TreeFiles, so a
# new untracked file is seen) is scanned for the write patterns above, independent of any annotation. A write may sit in
# EntityExtensions.cs, or in a dispatched service's class (the one named after its file): inside a private member (a
# member of a private nested class, such as EmpowerAction.Ops, counts as private), or inside a non-private member marked
# [Mutating]. A non-private member that names a private writing member of the same file writes too (transitively), so a
# write moved into a private helper still needs [Mutating] on the member that reaches it. An unsafe block or a
# GetUnsafePtr, GetUnsafeReadOnlyPtr or UnsafeUtility call fails outright. Logic/ is compiled into the test project,
# which references no game assembly, so it is not scanned; a Logic/ file with a using of Unity*, ProjectM* or
# Stunlock* fails instead.
function Test-CheckEntityWrites([string]$Root) {
    $all = @(Get-CsFiles $Root | Where-Object { $_.StartsWith("$PkgRel/") })
    $logic = @($all | Where-Object { $_.StartsWith("$PkgRel/Logic/") })
    $files = @($all | Where-Object { -not $_.StartsWith("$PkgRel/Logic/") })
    if ($files.Count -eq 0) { return New-Result $false 'entity writes: no source files' }
    $bad = @()
    foreach ($f in $logic) {
        $t = Hide-CsSpans (Read-Text $Root $f) -Literals
        foreach ($u in [regex]::Matches($t, '(?m)^\s*(?:global\s+)?using\s+(?:static\s+)?(?:\w+\s*=\s*)?((?:Unity|ProjectM|Stunlock)[\w.]*)')) {
            $bad += "$f has using $($u.Groups[1].Value) (Logic/ holds no game code)"
        }
    }
    $dispatched = $script:DispatchedServices
    $sites = 0; $mutating = 0
    foreach ($f in $files) {
        $raw = Read-Text $Root $f
        if (-not $raw) { continue }
        $t = Get-EwCodeText $raw
        $s = Hide-CsSpans $raw -Literals
        $lineOf = { param($i) ([regex]::Matches($t.Substring(0, $i), "`n")).Count + 1 }
        foreach ($u in [regex]::Matches($t, $script:EntityUnsafeRx)) { $bad += "$($u.Value) in ${f}:$(& $lineOf $u.Index) (a pointer write cannot be classified)" }
        $found = Find-EntityWrites $t $s
        $sites += $found.Count
        if ($found.Count -eq 0 -or $f -eq "$PkgRel/EntityExtensions.cs") { continue }
        if ($dispatched -notcontains $f) {
            foreach ($i in @($found.Keys | Sort-Object)) { $bad += "entity write ($($found[$i])) in ${f}:$(& $lineOf $i), outside the dispatched services" }
            continue
        }
        # A dispatched service: attribute each site to its member.
        $svc = [IO.Path]::GetFileNameWithoutExtension($f)
        $types = Get-EwTypes $s
        $members = [Collections.Generic.List[object]]::new()
        for ($k = 0; $k -lt $types.Count; $k++) {
            $top = $k; $priv = $false
            while ($types[$top].Parent -ge 0) { if ($types[$top].Private) { $priv = $true }; $top = $types[$top].Parent }
            foreach ($m in (Get-EwMembers $s $types[$k].Open $types[$k].Close)) {
                if ($m.IsType) { continue }
                $m | Add-Member -NotePropertyName Private -NotePropertyValue ($priv -or -not $m.NonPrivate)
                $m | Add-Member -NotePropertyName InService -NotePropertyValue ($types[$top].Name -eq $svc)
                $m | Add-Member -NotePropertyName Writes -NotePropertyValue $false
                $members.Add($m)
            }
        }
        foreach ($i in @($found.Keys | Sort-Object)) {
            $own = $null
            foreach ($m in $members) { if ($i -ge $m.Start -and $i -le $m.End -and ($null -eq $own -or $m.Start -gt $own.Start)) { $own = $m } }
            if ($null -eq $own -or -not $own.InService) { $bad += "entity write ($($found[$i])) in ${f}:$(& $lineOf $i), outside the $svc class"; continue }
            $own.Writes = $true
        }
        # A member that names a private writing member writes too, to a fixpoint.
        do {
            $grew = $false
            $names = @($members | Where-Object { $_.Writes -and $_.Private -and $_.Name } | ForEach-Object Name | Sort-Object -Unique)
            if (-not $names) { break }
            $rx = [regex]"\b(?:$($names -join '|'))\b"
            foreach ($m in $members) {
                if ($m.Writes -or -not $m.InService) { continue }
                if ($rx.IsMatch($t.Substring($m.Body, $m.End - $m.Body + 1))) { $m.Writes = $true; $grew = $true }
            }
        } while ($grew)
        foreach ($m in @($members | Where-Object { $_.Writes -and -not $_.Private })) {
            if ($m.Mutating) { $mutating++ } else { $bad += "$svc.$($m.Name) writes an entity but is not [Mutating] (${f}:$(& $lineOf $m.Body))" }
        }
    }
    if ($bad) { return New-Result $false "entity writes: $(@($bad | Select-Object -Unique) -join '; ')" }
    return New-Result $true "entity writes: only dispatched services ($sites sites, $mutating [Mutating] methods)"
}

# ---------------------------------------------------------------- human replies and thin shims (raphael-api-admin D1)

# $Text with every comment blanked to spaces and, with -Literals, every string and char literal blanked to "_", newlines
# kept, so offsets and line numbers still match the source.
function Hide-CsSpans([string]$Text, [switch]$Literals) {
    $hideLiterals = [bool]$Literals
    return $script:CsLexRx.Replace($Text, {
        param($m)
        $comment = $m.Groups['lc'].Success -or $m.Groups['bc'].Success
        if (-not $comment -and -not $hideLiterals) { return $m.Value }
        return [regex]::Replace($m.Value, '[^\r\n]', $(if ($comment) { ' ' } else { '_' }))
    }.GetNewClosure())
}

# Index of the $Close matching the $Open at $At, or -1.
function Get-CloseIndex([string]$Text, [int]$At, [char]$Open, [char]$Close) {
    $depth = 0
    for ($i = $At; $i -lt $Text.Length; $i++) {
        if ($Text[$i] -eq $Open) { $depth++ }
        elseif ($Text[$i] -eq $Close) { $depth--; if ($depth -eq 0) { return $i } }
    }
    return -1
}

# Index of the ";" that ends the statement starting at $From (brackets balanced), or -1.
function Get-StatementEnd([string]$Text, [int]$From) {
    $depth = 0
    for ($i = $From; $i -lt $Text.Length; $i++) {
        $c = $Text[$i]
        if ($c -eq '(' -or $c -eq '{' -or $c -eq '[') { $depth++ }
        elseif ($c -eq ')' -or $c -eq '}' -or $c -eq ']') { $depth-- }
        elseif ($c -eq ';' -and $depth -eq 0) { return $i }
    }
    return -1
}

# The (start, end) spans of member $Member of type $Class in $B, a text whose comments and literals are hidden: each
# declaration at depth 1 of the type's body (every part of a partial type, after a primary constructor), to the end of
# its block or expression.
function Get-MemberSpans([string]$B, [string]$Class, [string]$Member) {
    $spans = @()
    foreach ($c in [regex]::Matches($B, "\b(?:class|record|struct|interface)\s+$Class\b")) {
        $k = $c.Index + $c.Length
        while ($k -lt $B.Length -and [char]::IsWhiteSpace($B[$k])) { $k++ }
        if ($k -lt $B.Length -and $B[$k] -eq '(') { $k = (Get-CloseIndex $B $k '(' ')') + 1; if ($k -le 0) { continue } }
        $open = $B.IndexOf('{', $k)
        if ($open -lt 0) { continue }
        $end = Get-CloseIndex $B $open '{' '}'
        if ($end -lt 0) { continue }
        $body = $B.Substring($open, $end - $open)
        foreach ($m in [regex]::Matches($body, "[\w>\]\)?]\s+$Member\s*(?:<[^<>()]*>)?\s*(\(|=>|=(?!=)|\{|;)")) {
            $pre = $body.Substring(0, $m.Index)
            if (($pre.Length - $pre.Replace('{', '').Length) - ($pre.Length - $pre.Replace('}', '').Length) -ne 1) { continue }
            $p = $open + $m.Groups[1].Index
            $e = switch ($m.Groups[1].Value) {
                '(' {
                    $q = (Get-CloseIndex $B $p '(' ')') + 1
                    while ($q -gt 0 -and $q -lt $B.Length -and [char]::IsWhiteSpace($B[$q])) { $q++ }
                    if ($q -le 0) { -1 }
                    elseif ($B[$q] -eq '{') { Get-CloseIndex $B $q '{' '}' }
                    elseif ($q + 1 -lt $B.Length -and $B[$q] -eq '=' -and $B[$q + 1] -eq '>') { Get-StatementEnd $B $q }
                    else { $q }
                }
                '{' { Get-CloseIndex $B $p '{' '}' }
                ';' { $p }
                default { Get-StatementEnd $B $p }
            }
            $spans += , @(($open + $m.Index), $e)
        }
    }
    return , $spans
}

# The reply literals of member $Spec ("<path> <Class>.<Member>") in $Text (A3): every plain, verbatim, interpolated or
# raw string literal inside the member that holds a space or is the word denied, outside the arguments of log(, info(,
# warn( and Log…( calls (LogAdmin( included). Each is @{ Line; Literal }; $null when the member is not found.
function Get-ReplyLiterals([string]$Text, [string]$Class, [string]$Member) {
    $kept = Hide-CsSpans $Text
    $b = Hide-CsSpans $Text -Literals
    $spans = Get-MemberSpans $b $Class $Member
    if ($spans.Count -eq 0) { return $null }
    $logs = Get-GateSpans $b '\b(?:log|info|warn|Log\w*)\s*\('
    $found = @()
    foreach ($lt in $script:CsLexRx.Matches($kept)) {
        if (-not ($lt.Groups['raw'].Success -or $lt.Groups['vs'].Success -or $lt.Groups['s'].Success)) { continue }
        $i = $lt.Index
        $inMember = $false
        foreach ($s in $spans) { if ($s[1] -ge 0 -and $i -ge $s[0] -and $i -le $s[1]) { $inMember = $true; break } }
        if (-not $inMember -or (Test-InSpan $i $logs)) { continue }
        $content = $lt.Value -replace '^[$@]*"+', '' -replace '"+$', ''
        if ($content.Contains(' ') -or $content -ceq 'denied') {
            $line = $Text.Substring(0, $i).Split("`n").Count
            $found += [pscustomobject]@{ Line = $line; Literal = $lt.Value }
        }
    }
    return , $found
}

# The text of $Rel at git tag $Tag, or $null. A fixture holds the tag's files under <tag>/.
function Read-TagText([string]$Root, [string]$Tag, [string]$Rel) {
    if (Test-IsFixture $Root) { return Read-Text $Root "$Tag/$Rel" }
    $prev = [Console]::OutputEncoding
    try {
        [Console]::OutputEncoding = [Text.Encoding]::UTF8
        $out = git -C $Root show "${Tag}:$Rel" 2>$null
        if ($LASTEXITCODE -ne 0) { return $null }
        return (@($out) -join "`n")
    } finally { [Console]::OutputEncoding = $prev }
}

# raphael-api-admin D1 (A3, A4): each row of Nyarlathotep.Tests/Fixtures/human-replies-<version>.txt, "<scenario id> ·
# <template> · <path>:<line>[,<line>]", has every literal part of its template (split on its {holes} and \n, each part
# as written or trimmed of " ;:,") on its cited lines at tag v<version>, in the template's order (the cited lines read
# in the order cited); the base file, the one of the entry's tag, cites the line of every reply literal
# (Get-ReplyLiterals) of the members the entry lists, at that tag; and a capture file is never edited once committed:
# one that is at HEAD equals the text of the commit that added it, followed through renames (git log --follow
# --diff-filter=A), and fails when no such commit is found; so a later reply change is a new file (A4). A fixture
# lists its committed captures in tracked.txt and gives each one's added text as added/<path>, and may list its
# members in members.txt.
function Test-CheckHumanReplies([string]$Root) {
    $entry = @((Get-Manifest).checks | Where-Object name -eq 'HumanReplies')[0]
    $tag = [string]$entry.tag
    $dir = 'Nyarlathotep/Nyarlathotep.Tests/Fixtures/'
    $files = @(Get-TreeFiles $Root | Where-Object { $_.StartsWith($dir) -and $_.Substring($dir.Length) -match '^human-replies-\d+\.\d+\.\d+\.txt$' })
    $rows = @(); $problems = @()
    foreach ($f in $files) {
        $ver = [regex]::Match($f, 'human-replies-(\d+\.\d+\.\d+)\.txt$').Groups[1].Value
        foreach ($l in ((Read-Text $Root $f) -split "`r?`n")) {
            if ($l -notmatch '\S' -or $l.StartsWith('#')) { continue }
            $parts = $l.Split([string[]]@(' · '), [StringSplitOptions]::None)
            $cite = if ($parts.Count -eq 3) { [regex]::Match($parts[2], '^(.+):(\d+(?:,\d+)*)$') } else { $null }
            if (-not $cite -or -not $cite.Success) { $problems += "$f row '$l' is not <id> · <template> · <path>:<line>"; continue }
            $rows += [pscustomobject]@{ File = $f; Tag = "v$ver"; Id = $parts[0]; Template = $parts[1]; Path = $cite.Groups[1].Value
                Lines = @($cite.Groups[2].Value.Split(',') | ForEach-Object { [int]$_ }) }
        }
    }
    if ($rows.Count -eq 0 -and $problems.Count -eq 0) { return New-Result $false 'human replies: no rows' }
    $tracked = if (Test-IsFixture $Root) { @((Read-Text $Root 'tracked.txt') -split "`r?`n" | Where-Object { $_ -match '\S' } | ForEach-Object { $_.Trim() }) } else { $null }
    foreach ($f in $files) {
        if (Test-IsFixture $Root) {
            if ($tracked -notcontains $f) { continue }                                     # new in this change
            $added = Read-Text $Root "added/$f"
        }
        else {
            git -C $Root cat-file -e "HEAD:$f" 2>$null
            if ($LASTEXITCODE -ne 0) { continue }                                          # not committed yet
            $log = @(git -C $Root log --follow --diff-filter=A --format=%H --name-only -- $f 2>$null | Where-Object { $_ -match '\S' })
            $added = if ($log.Count -ge 2) { Read-TagText $Root $log[-2] $log[-1] } else { $null }
        }
        if ($null -eq $added) { $problems += "$f is committed but no commit adding it was found: a capture is never renamed (A4)" }
        elseif (($added -replace "`r`n", "`n").TrimEnd("`n") -cne ((Read-Text $Root $f) -replace "`r`n", "`n").TrimEnd("`n")) {
            $problems += "$f changed since the commit that added it: add a new capture file instead (A4)"
        }
    }

    $cache = @{}
    $source = { param($t, $p) $k = "$t|$p"; if (-not $cache.ContainsKey($k)) { $cache[$k] = Read-TagText $Root $t $p }; $cache[$k] }
    foreach ($r in $rows) {
        $text = & $source $r.Tag $r.Path
        if ($null -eq $text) { $problems += "row $($r.Id): $($r.Path) is not at $($r.Tag)"; continue }
        $src = $text -split "`n"
        $cited = @(foreach ($n in $r.Lines) { if ($n -ge 1 -and $n -le $src.Count) { $src[$n - 1] } else { $problems += "row $($r.Id): $($r.Path):$n is past the end at $($r.Tag)" } })
        $joined = $cited -join "`n"; $at = 0
        foreach ($part in ($r.Template -split '\{[^}]*\}|\\n')) {
            $trim = $part.Trim(' ', ';', ':', ',')
            if (-not $trim) { continue }
            $i = $joined.IndexOf($part, $at, [StringComparison]::Ordinal)
            if ($i -lt 0) { $i = $joined.IndexOf($trim, $at, [StringComparison]::Ordinal); $len = $trim.Length } else { $len = $part.Length }
            if ($i -lt 0) {
                $where = if (@($cited | Where-Object { $_.Contains($trim) }).Count) { 'out of order on' } else { 'not on' }
                $problems += "row $($r.Id): '$part' is $where $($r.Path):$($r.Lines -join ',') at $($r.Tag)"
            }
            else { $at = $i + $len }
        }
    }

    $baseFile = "${dir}human-replies-$($tag.TrimStart('v')).txt"
    $base = @($rows | Where-Object File -eq $baseFile)
    if ($base.Count -eq 0) { $problems += "no rows in $baseFile" }
    $membersFile = if (Test-IsFixture $Root) { Read-Text $Root 'members.txt' } else { $null }
    $members = if ($membersFile) { @($membersFile -split "`r?`n" | Where-Object { $_ -match '\S' }) } else { @($entry.members) }
    $citedAt = @{}
    foreach ($r in $base) { foreach ($n in $r.Lines) { $citedAt["$($r.Path):$n"] = $true } }
    $total = 0; $covered = 0
    foreach ($spec in $members) {
        $sp = $spec.Trim() -split '\s+'
        $text = & $source $tag $sp[0]
        $cm = $sp[1] -split '\.'
        $lits = if ($null -ne $text) { Get-ReplyLiterals $text $cm[0] $cm[1] } else { $null }
        if ($null -eq $lits) { $problems += "member $($sp[1]) not found in $($sp[0]) at $tag"; continue }
        foreach ($lit in $lits) {
            $total++
            if ($citedAt.ContainsKey("$($sp[0]):$($lit.Line)")) { $covered++ }
            else { $problems += "$($sp[0]):$($lit.Line) $($lit.Literal) is cited by no row" }
        }
    }
    if ($problems) { return New-Result $false "human replies: $(@($problems | Select-Object -First 6) -join '; ')$(if ($problems.Count -gt 6) { " (+$($problems.Count - 6) more)" })" }
    return New-Result $true "human replies: $covered/$total at $tag"
}

# raphael-api-admin D1: Services/AdminOps.cs is thin. Every AdminOps member marked [Mutating] returns Outcome, and so does
# its declaration in IAdminOps (Logic/AdminFlows.cs); every method or property body of AdminOps is one expression (=>)
# or a block of one return statement, with no if, switch, ?, ?? or loop, so every decision lives in Logic.
function Test-CheckOutcomeReturns([string]$Root) {
    $ops = Read-Text $Root "$PkgRel/Services/AdminOps.cs"
    if (-not $ops) { return New-Result $false 'outcome returns: no AdminOps' }
    $flows = Read-Text $Root "$PkgRel/Logic/AdminFlows.cs"
    $t = Hide-CsSpans $ops -Literals
    $decl = '(?<attrs>(?:\[[^\]]*\]\s*)*)(?:(?:public|internal|private|protected|static|override|virtual|async|sealed|readonly|new|extern|unsafe)\s+)+(?<type>[\w<>\[\],.?() ]+?)\s+(?<name>\w+)\s*(?<kind>\(|=>|\{|=|;)'
    $cls = [regex]::Match($t, '\bclass\s+AdminOps\b[^{]*\{')
    if (-not $cls.Success) { return New-Result $false 'outcome returns: no AdminOps' }
    $open = $cls.Index + $cls.Length - 1
    $body = $t.Substring($open, (Get-CloseIndex $t $open '{' '}') - $open)
    $branch = '\bif\b|\bswitch\b|\?|\bfor\b|\bforeach\b|\bwhile\b|\bdo\b|\bgoto\b'
    $checked = 0; $problems = @(); $mutating = @()
    foreach ($m in [regex]::Matches($body, $decl)) {
        $pre = $body.Substring(0, $m.Index)
        if (($pre.Length - $pre.Replace('{', '').Length) - ($pre.Length - $pre.Replace('}', '').Length) -ne 1) { continue }
        $name = $m.Groups['name'].Value; $type = $m.Groups['type'].Value.Trim(); $kind = $m.Groups['kind'].Value
        if ($kind -eq '=' -or $kind -eq ';') { continue }                                  # a field: no body
        $p = $m.Groups['kind'].Index
        if ($kind -eq '(') {
            $q = (Get-CloseIndex $body $p '(' ')') + 1
            while ($q -lt $body.Length -and [char]::IsWhiteSpace($body[$q])) { $q++ }
            $kind = if ($body[$q] -eq '{') { '{' } else { '=>' }
            $p = $q
        }
        $code = if ($kind -eq '{') { $body.Substring($p + 1, (Get-CloseIndex $body $p '{' '}') - $p - 1).Trim() }
                else { $body.Substring($p + 2, (Get-StatementEnd $body $p) - $p - 2).Trim() }
        $checked++
        if ($kind -eq '{' -and $code -notmatch '^(?:return\b[^;]*;|get\s*=>[^;]*;)$') { $problems += "$name is not one expression or one return" }
        elseif ($code -match $branch) { $problems += "$name branches ($($Matches[0]))" }
        if ($m.Groups['attrs'].Value -match '\[Mutating\]') {
            $mutating += $name
            if ($type -cne 'Outcome') { $problems += "$name returns $type, not Outcome" }
        }
    }
    if ($checked -eq 0) { return New-Result $false 'outcome returns: AdminOps has no member' }
    $f = if ($flows) { Hide-CsSpans $flows -Literals } else { '' }
    $if = [regex]::Match($f, '\binterface\s+IAdminOps\b[^{]*\{')
    if (-not $if.Success) { $problems += 'no IAdminOps in Logic/AdminFlows.cs' }
    else {
        $ib = $f.Substring($if.Index, (Get-CloseIndex $f ($if.Index + $if.Length - 1) '{' '}') - $if.Index)
        foreach ($name in $mutating) {
            $d = [regex]::Match($ib, "(?<type>[\w<>\[\],.?()]+(?: [\w<>\[\],.?()]+)*?)\s+$name\s*\(")
            if (-not $d.Success) { $problems += "IAdminOps does not declare $name" }
            elseif (($d.Groups['type'].Value -split '\s+')[-1] -cne 'Outcome') { $problems += "IAdminOps.$name returns $($d.Groups['type'].Value), not Outcome" }
        }
    }
    if ($problems) { return New-Result $false "outcome returns: $($problems -join '; ')" }
    return New-Result $true "outcome returns: $checked/$checked"
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
            # The method's parameter names after the context, in order (raphael-api-admin D11: the api command table).
            $sig = [regex]::Match($text.Substring($m.Index + $m.Length), '^\s*(?:\[[^\]]*\]\s*)*(?:(?:public|internal|private|static)\s+)+[\w<>]+\s+\w+\s*\(([^)]*)\)')
            $after = if ($sig.Success) { @(@($sig.Groups[1].Value -split ',') | Select-Object -Skip 1) } else { @() }
            $params = @($after | ForEach-Object { (($_ -replace '=.*$', '').Trim() -split '\s+')[-1] })
            # Parameters with no default: VCF then registers fewer argument counts than ApiCommandTable.Overloads lists.
            $required = @($after | Where-Object { $_ -notmatch '=' } | ForEach-Object { ($_.Trim() -split '\s+')[-1] })
            [pscustomobject]@{
                Name       = $name
                Params     = $params
                Required   = $required
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
        if ($f -ne $wireRel -and $code -match '(?m)^\s*(?:global\s+)?using\s+(?:static\s+|@?\w+\s*=\s*)[\w.:\s]*?\bWire\s*;') {
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
    # The reverse direction (A4): every tag and command the table marks IMPLEMENTED exists in the plugin, so the
    # contract never promises Raphael a line or a command the server does not have.
    foreach ($key in @($rows.Keys | Sort-Object)) {
        if ($rows[$key][0] -ne 'IMPLEMENTED') { continue }
        $kind, $name = $key -split ' ', 2
        $present = if ($kind -eq 'tag') { $tags.ContainsKey($name) } else { $apiCmds -contains $name }
        if (-not $present) { $bad += "$key is IMPLEMENTED in the contract but not in the plugin" }
    }
    # Logic/ApiCommandTable.cs is a copy of every `.nyar api` command's word, adminOnly and parameters, which
    # ApiOverloadTests reads without the game (raphael-api-admin D11); from api 4 it must exist, and when it exists it
    # equals the walked commands both ways.
    $tableText = Read-Text $Root "$PkgRel/Logic/ApiCommandTable.cs"
    if (-not $tableText -and $docApi -ge 4) { $bad += 'Logic/ApiCommandTable.cs not found (api 4 and later)' }
    if ($tableText) {
        # A name twice on either side fails: VCF would route one word to two commands, and a keyed comparison would
        # hide the second declaration.
        # Every entry of the Commands initializer must be a row the check reads (`new(…)` or `new ApiCommand(…)`); an
        # entry in any other form fails, so a row cannot hide from the comparison (Codex step 2 round 6 F1).
        $copy = @{}
        $rowRx = 'new\s*(?:ApiCommand\s*)?\(\s*"([^"]+)"\s*,\s*(true|false)\s*,\s*\[([^\]]*)\]\s*\)'
        $init = [regex]::Match((Remove-CsComments $tableText), '(?s)\bCommands\s*=\s*\[(.*?)\]\s*;')
        if (-not $init.Success) { $bad += 'Logic/ApiCommandTable.cs has no Commands = [ … ]; initializer' }
        elseif (([regex]::Replace($init.Groups[1].Value, $rowRx, '') -replace '[\s,]', '') -ne '') {
            $bad += 'Logic/ApiCommandTable.cs holds a Commands entry that is not a new(name, adminOnly, [parameters]) row'
        }
        foreach ($e in [regex]::Matches($init.Groups[1].Value, $rowRx)) {
            $ps = @([regex]::Matches($e.Groups[3].Value, '"([^"]*)"') | ForEach-Object { $_.Groups[1].Value })
            if ($copy.ContainsKey($e.Groups[1].Value)) { $bad += "Logic/ApiCommandTable.cs lists api $($e.Groups[1].Value) twice" }
            $copy[$e.Groups[1].Value] = "adminOnly=$($e.Groups[2].Value) ($($ps -join ', '))"
        }
        $walked = @{}
        foreach ($c in @(Get-CommandWalk $Root | Where-Object { $_.Full -like '.nyar api *' })) {
            if ($walked.ContainsKey($c.Name)) { $bad += "api $($c.Name) is declared twice ($($c.File))" }
            # Every parameter after the context is optional, so VCF answers each count the table lists (Codex step 2 round 7 F2).
            if (@($c.Required).Count) { $bad += "api $($c.Name) parameter $(@($c.Required) -join ', ') has no default ($($c.File))" }
            $walked[$c.Name] = "adminOnly=$(if ($c.Admin) { 'true' } else { 'false' }) ($(@($c.Params) -join ', '))"
        }
        foreach ($n in @($walked.Keys | Sort-Object)) {
            if (-not $copy.ContainsKey($n)) { $bad += "api $n is not in Logic/ApiCommandTable.cs" }
            elseif ($copy[$n] -cne $walked[$n]) { $bad += "api $n is $($walked[$n]) but Logic/ApiCommandTable.cs says $($copy[$n])" }
        }
        foreach ($n in @($copy.Keys | Sort-Object | Where-Object { -not $walked.ContainsKey($_) })) { $bad += "Logic/ApiCommandTable.cs lists api $n, which no command declares" }
    }
    if ($bad) { return New-Result $false "wire contract: $(@($bad | Select-Object -Unique) -join '; ')" }
    $tableNote = if ($tableText) { '; command table equal' } else { '' }
    return New-Result $true "wire contract: $($tags.Count) tags, $($apiCmds.Count) api commands, all documented (api $codeApi)$tableNote"
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

# The `.nyar debug` command ships no temporary verb (walkable-spawns D9, A7): for every `debug` [Command], its usage
# text and the string literals its method compares one of its own parameters with name only "here": ==, !=, an `is`
# pattern (with or/and/not), Equals in either form, a case label of a switch statement on it, or an arm of a switch
# expression on it (code review F1). A temporary verb added for a test session (as step 1's `walk` was) fails the check until it
# is removed. The method is its parameter list and its brace-balanced body; one that cannot be parsed fails.
function Test-CheckDebugCommands([string]$Root) {
    $files = @(Get-CsFiles $Root | Where-Object { $_ -like 'Nyarlathotep/Nyarlathotep/Commands/*' })
    if ($files.Count -eq 0) { return New-Result $false 'debug commands: no command files' }
    $n = 0; $bad = @()
    foreach ($f in $files) {
        $t = Remove-CsComments (Read-Text $Root $f)
        foreach ($attr in [regex]::Matches($t, '\[Command(?:Attribute)?\s*\(\s*(?:name\s*:\s*)?"debug"(?<args>(?:[^()]|\((?:[^()])*\))*)\)\s*\]')) {
            $n++
            $usage = [regex]::Match($attr.Groups['args'].Value, 'usage\s*:\s*"(?<u>[^"]*)"')
            foreach ($w in [regex]::Matches($usage.Groups['u'].Value, '[A-Za-z]+')) {
                if ($w.Value -cne 'here' -and $w.Value -cne 'radius') { $bad += "$f usage names '$($w.Value)'" }
            }
            $open = $t.IndexOf('(', $attr.Index + $attr.Length)
            $close = if ($open -gt 0) { Get-ParenEnd $t $open } else { -1 }
            $brace = if ($close -gt 0) { [regex]::new('\G\s*\{').Match($t, $close + 1) } else { $null }
            if (-not $brace -or -not $brace.Success) { $bad += "$f debug: method not parsed"; continue }
            $depth = 0; $end = -1
            for ($i = $brace.Index + $brace.Length - 1; $i -lt $t.Length; $i++) {
                if ($t[$i] -eq '{') { $depth++ } elseif ($t[$i] -eq '}') { $depth--; if ($depth -eq 0) { $end = $i; break } }
            }
            if ($end -lt 0) { $bad += "$f debug: method body not closed"; continue }
            $params = @([regex]::Matches($t.Substring($open + 1, $close - $open - 1), '(\w+)\s*(?:=\s*[^,]*)?(?:,|$)') | ForEach-Object { $_.Groups[1].Value })
            $body = $t.Substring($brace.Index, $end - $brace.Index + 1)
            foreach ($prm in $params) {
                $e = [regex]::Escape($prm)
                $rx = "\b$e\s*(?:==|!=)\s*""(?<v>[^""]*)""|""(?<v>[^""]*)""\s*(?:==|!=)\s*$e\b"
                $vals = @([regex]::Matches($body, $rx) | ForEach-Object { $_.Groups['v'].Value })
                $lit = '"(?<v>[^"]*)"'
                foreach ($pat in [regex]::Matches($body, "\b$e\s+is\s+(?<pat>[^;{}&|]*)")) {
                    $vals += @([regex]::Matches($pat.Groups['pat'].Value, $lit) | ForEach-Object { $_.Groups['v'].Value })
                }
                $eq = "\b$e\s*\.\s*Equals\s*\(\s*$lit|$lit\s*\.\s*Equals\s*\(\s*$e\b|\bEquals\s*\(\s*$e\s*,\s*$lit|\bEquals\s*\(\s*$lit\s*,\s*$e\b"
                $vals += @([regex]::Matches($body, $eq) | ForEach-Object { $_.Groups['v'].Value })
                foreach ($sx in [regex]::Matches($body, "\b$e\s+switch\s*\{")) {
                    $d = 0; $sxEnd = -1
                    for ($i = $sx.Index + $sx.Length - 1; $i -lt $body.Length; $i++) {
                        if ($body[$i] -eq '{') { $d++ } elseif ($body[$i] -eq '}') { $d--; if ($d -eq 0) { $sxEnd = $i; break } }
                    }
                    if ($sxEnd -lt 0) { $bad += "$f debug: switch expression on $prm not closed"; continue }
                    $arms = $body.Substring($sx.Index, $sxEnd - $sx.Index + 1)
                    foreach ($arm in [regex]::Matches($arms, '(?:\{|,)\s*(?<pat>[^,{}]*?)=>')) {
                        $vals += @([regex]::Matches($arm.Groups['pat'].Value, $lit) | ForEach-Object { $_.Groups['v'].Value })
                    }
                }
                foreach ($sw in [regex]::Matches($body, "switch\s*\(\s*$e\s*\)\s*\{")) {
                    $d = 0; $swEnd = -1
                    for ($i = $sw.Index + $sw.Length - 1; $i -lt $body.Length; $i++) {
                        if ($body[$i] -eq '{') { $d++ } elseif ($body[$i] -eq '}') { $d--; if ($d -eq 0) { $swEnd = $i; break } }
                    }
                    if ($swEnd -lt 0) { $bad += "$f debug: switch on $prm not closed"; continue }
                    $vals += @([regex]::Matches($body.Substring($sw.Index, $swEnd - $sw.Index + 1), 'case\s+"(?<v>[^"]*)"') | ForEach-Object { $_.Groups['v'].Value })
                }
                foreach ($v in $vals) { if ($v -cne 'here' -and $v -cne '') { $bad += "$f debug compares $prm with '$v'" } }
            }
        }
    }
    if ($n -eq 0) { return New-Result $false 'debug commands: no debug command found' }
    if ($bad) { return New-Result $false "debug commands: temporary verb present: $($bad -join '; ')" }
    return New-Result $true 'debug commands: none temporary'
}

# Every "### Session <n> · <date>" under "## Test results" in docs/features/<SLUG>.md has exactly one line
# "- session <n> log check: 0 unhandled, <s> nyar lines, 0 orphan errors, <u> unity errors" in docs/audits/<slug>.md
# (foundation D33). Only sessions after the plan's last pre-A10 session (below; a plan not listed has none) count as
# checked, and there must be at least one. Pre-A10 lines may omit both counts and give the server log's orphan count
# in a "  - before A10" sub-bullet; they are listed with any orphan errors named, never counted as clean (Review 10).
# From the first line carrying the counts on, every line must carry them. A line with Unity errors carries -LogCheck's
# "[kind | kind]" list, and each kind has a sub-bullet '  - unity "<kind>": game <why>' or '... ours <why> (A<n>)',
# ours citing the amendment that records it (A13).
# A plan listed in tools/preflight-checks.json snapshotSessions (slug: first session) also needs, in each of those
# sessions' blocks, the save "dev-snapshot.ps1 -Save <label>" and the restore "snapshot restored; hashes equal (<label>"
# with the same label (event-library D30, A19); the listed first session must exist.
# A fixture names the slug in sessionsof.txt.
$script:SessionsBeforeA10 = @{ 'foundation' = 7 }

function Test-CheckSessionLogs([string]$Root) {
    $slug = if (Test-IsFixture $Root) { "$(Read-Text $Root 'sessionsof.txt')".Trim() } else { $SessionsOf }
    if (-not $slug) { return New-Result $false 'session logs: no plan named (-SessionsOf <slug>)' }
    # The feature docs are the ones childDocs maps the slug to; the slug-derived name only without a mapping (A8).
    $mt = Read-Text $Root 'tools/preflight-checks.json'
    $mj = if ($mt) { $mt | ConvertFrom-Json } else { $null }
    $entry = if ($mj -and $mj.childDocs) { $mj.childDocs.PSObject.Properties[$slug] } else { $null }
    $docRels = if ($entry) { @($entry.Value | Where-Object { $_ }) } else { @("docs/features/$($slug.ToUpperInvariant().Replace('-', '_')).md") }
    if ($docRels.Count -eq 0) { return New-Result $false "session logs: childDocs maps $slug to no doc" }
    $audit = Read-Text $Root "docs/audits/$slug.md"
    # One session number belongs to one doc: the audit names sessions by number only.
    $owner = @{}; $shared = @(); $blocks = @{}
    foreach ($docRel in $docRels) {
        $doc = Read-Text $Root $docRel
        if ($null -eq $doc) { return New-Result $false "session logs: $docRel not found" }
        $results = [regex]::Match($doc, '(?ms)^## Test results\s*$(.*?)(?=^## |\z)')
        if (-not $results.Success) { continue }
        foreach ($bm in [regex]::Matches($results.Groups[1].Value, '(?ms)^### Session (\d+) · .*?(?=^### |\z)')) {
            $n = [int]$bm.Groups[1].Value
            if ($owner.ContainsKey($n)) { $shared += "session $n in $($owner[$n]) and $docRel" } else { $owner[$n] = $docRel; $blocks[$n] = $bm.Value }
        }
    }
    $docRel = $docRels -join ', '
    if ($shared) { return New-Result $false "session logs: $slug numbers a session twice ($($shared -join '; '))" }
    if ($null -eq $audit) { return New-Result $false "session logs: docs/audits/$slug.md not found" }
    $sessions = @($owner.Keys | Sort-Object)
    if ($sessions.Count -eq 0) { return New-Result $false "session logs: $slug has no sessions under $docRel › Test results" }
    $checks = @{}; $dupes = @(); $unattributed = @()
    $pattern = '(?m)^- session (\d+) log check: (\d+) unhandled, (\d+) nyar lines(?:, (\d+) orphan errors, (\d+) unity errors)?(?<rest>[^\r\n]*)(?:\r?\n  - before A10[^\r\n]*?\b(\d+) orphan errors)?'
    foreach ($m in [regex]::Matches($audit, $pattern)) {
        $n = [int]$m.Groups[1].Value
        if ($m.Groups[5].Success -and [int]$m.Groups[5].Value -gt 0) {
            $list = [regex]::Match($m.Groups['rest'].Value, '^(?:, regions \d+ polygons)? \[(.+)\]\s*$')
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
    $snapFrom = if ($mj -and $mj.snapshotSessions) { $mj.snapshotSessions.PSObject.Properties[$slug] } else { $null }
    $unwrapped = @()
    if ($snapFrom) {
        if ($sessions -notcontains [int]$snapFrom.Value) { $unwrapped += [int]$snapFrom.Value }
        foreach ($n in @($sessions | Where-Object { $_ -ge [int]$snapFrom.Value })) {
            $save = [regex]::Match("$($blocks[$n])", 'dev-snapshot\.ps1 -Save ([\w-]+)')
            if (-not $save.Success -or $blocks[$n] -notmatch ('snapshot restored; hashes equal \(' + [regex]::Escape($save.Groups[1].Value) + '[,)]')) { $unwrapped += $n }
        }
    }
    # A plan listed in probeRecords (slug: session) has labelled walk readings and a go/no-go line per source in that
    # session's block; the verdict is recomputed and must agree (walkable-spawns D1, D10, A4, A8, A9).
    $probeFrom = if ($mj -and $mj.probeRecords) { $mj.probeRecords.PSObject.Properties[$slug] } else { $null }
    $probeWhy = $null
    if ($probeFrom) { $probeWhy = Get-ProbeRecordProblem $slug ([int]$probeFrom.Value) $blocks }
    $bad = @($dirty + $old + $dupes + $unattributed + $unwrapped | Sort-Object -Unique)
    $ok = @($post | Where-Object { $checks.ContainsKey($_) -and $bad -notcontains $_ }).Count
    $preOrphans = @($pre | Where-Object { $checks.ContainsKey($_) -and ([math]::Max($checks[$_][2], 0) + $checks[$_][3]) -gt 0 })
    $preNote = if ($pre.Count) { "; $($pre.Count) before A10 not counted$(if ($preOrphans) { " (orphan errors in session $($preOrphans -join ', '))" })" } else { '' }
    $after = if ($cutoff) { ' after A10' } else { '' }
    if ($missing -or $bad -or $post.Count -eq 0 -or $probeWhy) {
        $why = @()
        if ($probeWhy) { $why += $probeWhy }
        if ($post.Count -eq 0) { $why += "no session after A10 (session $cutoff)" }
        if ($missing) { $why += "no log check line for session $($missing -join ', ')" }
        if ($dirty) { $why += "unhandled exceptions, no nyar lines or orphan errors in session $($dirty -join ', ')" }
        if ($old) { $why += "no orphan count in session $($old -join ', ')" }
        if ($dupes) { $why += "more than one log check line for session $($dupes -join ', ')" }
        if ($unattributed) { $why += "unity error kinds not listed or not attributed in session $($unattributed -join ', ')" }
        if ($unwrapped) { $why += "no snapshot save and matching restore in session $($unwrapped -join ', ') (a snapshotSessions start that is no session counts)" }
        return New-Result $false "session logs: $slug $ok/$($post.Count) checked$after$preNote ($($why -join '; '))"
    }
    # The snapshot count shows an entry dropped from snapshotSessions (Review 16 F2): the line then has no "snapshots".
    $snapNote = if ($snapFrom) { $w = @($sessions | Where-Object { $_ -ge [int]$snapFrom.Value }).Count; "; snapshots $w/$w from session $($snapFrom.Value)" } else { '' }
    $probeNote = if ($probeFrom) { '; probe records 1/1' } else { '' }
    return New-Result $true "session logs: $slug $ok/$($post.Count) checked$after$preNote$snapNote$probeNote"
}

# The probe record of one session (walkable-spawns D1, D10, A4, A8): readings "- <label>: walk <x> <z> h <n> r <r>:
# <free|blocked> grounded <yes|no> (<source>)" with the labels dry, pond, water (a second water body), cliff, wall (a
# building's outer wall), floor and ledge (floor and ledge recorded, not counted; A12), and one line
# "- go/no-go (<source>): <go|no-go|incomplete>" per source. Per source the verdict is incomplete without a dry, pond,
# water, cliff and wall reading; else go when every dry reading is free and grounded and every pond, water, cliff
# and wall reading is blocked (ledge and floor are not counted), else no-go. An incomplete record fails: the session is
# repeated. Returns the problem, or $null.
function Get-ProbeRecordProblem([string]$Slug, [int]$Session, $Blocks) {
    if (-not $Blocks.ContainsKey($Session)) { return "probe records: $Slug session $Session not found" }
    $block = "$($Blocks[$Session])"
    $readings = @([regex]::Matches($block, '(?m)^- (dry|pond|water|cliff|wall|floor|ledge): walk -?\d+\.\d -?\d+\.\d h \d+ r \d\.\d\d: (free|blocked) grounded (yes|no) \(([^)\r\n]+)\)\s*$'))
    if ($readings.Count -eq 0) { return "probe records: $Slug session $Session has no readings" }
    $lines = New-Object System.Collections.Hashtable ([StringComparer]::Ordinal)
    $problems = @()
    # The sources AdminLines.WalkSources names, compared case-sensitively (step 1 code review F6).
    $known = @('singleton world', 'singleton tile')
    foreach ($g in [regex]::Matches($block, '(?m)^- go/no-go \(([^)\r\n]+)\): (go|no-go|incomplete)\s*$')) {
        if ($lines.ContainsKey($g.Groups[1].Value)) { $problems += "two go/no-go lines for $($g.Groups[1].Value)" }
        $lines[$g.Groups[1].Value] = $g.Groups[2].Value
    }
    # Both sources are required: a record that drops one is not a record of the probe (Codex cross-inspection F4).
    $sources = @(@($known) + @($readings | ForEach-Object { $_.Groups[4].Value }) + @($lines.Keys) | Sort-Object -Unique -CaseSensitive)
    foreach ($source in $sources) {
        if ($known -cnotcontains $source) { $problems += "unknown source $source"; continue }
        $mine = @($readings | Where-Object { $_.Groups[4].Value -ceq $source })
        $labels = @($mine | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
        $want = if (@('dry', 'pond', 'water', 'cliff', 'wall' | Where-Object { $labels -notcontains $_ }).Count) { 'incomplete' }
            elseif (@($mine | Where-Object { ($_.Groups[1].Value -eq 'dry' -and ($_.Groups[2].Value -ne 'free' -or $_.Groups[3].Value -ne 'yes')) -or
                ($_.Groups[1].Value -in 'pond', 'water', 'cliff', 'wall' -and $_.Groups[2].Value -ne 'blocked') }).Count) { 'no-go' }
            else { 'go' }
        if (-not $lines.ContainsKey($source)) { $problems += "no go/no-go line for $source"; continue }
        if ($lines[$source] -ne $want) { $problems += "$source reads $($lines[$source]), the readings give $want" }
        elseif ($want -eq 'incomplete') { $problems += "$source is incomplete (repeat the session)" }
    }
    if ($problems) { return "probe records: $Slug session ${Session}: $($problems -join '; ')" }
    return $null
}

# The externalSelfTests registry of tools/preflight-checks.json (faction-empowerment D22): every check that is not a
# Test-Check function (a tools script's -SelfTest, the unit-test run) with its command (an argument array run from the
# repository root), its success line ("success", exact, or "successPattern", a regex) and its named bad, good and empty
# cases. Returns @{ Entries; Problems }.
function Get-SelfTestRegistry([string]$Root) {
    $path = Join-Path $Root 'tools/preflight-checks.json'
    if (-not (Test-Path -LiteralPath $path)) { return @{ Entries = @(); Problems = @('no tools/preflight-checks.json') } }
    $entries = @((Get-Content -LiteralPath $path -Raw | ConvertFrom-Json).externalSelfTests | Where-Object { $_ })
    $problems = @()
    if ($entries.Count -eq 0) { $problems += 'no externalSelfTests entries' }
    foreach ($dup in @($entries | Group-Object name | Where-Object Count -gt 1)) { $problems += "entry '$($dup.Name)' listed twice" }
    foreach ($e in $entries) {
        $n = if ($e.name) { $e.name } else { '(unnamed)' }
        if (@($e.command).Count -lt 2) { $problems += "${n}: no command" }
        if (-not $e.success -and -not $e.successPattern) { $problems += "${n}: no success line" }
        foreach ($k in 'bad', 'good', 'empty') { if ("$($e.cases.$k)" -notmatch '\S') { $problems += "${n}: no $k case" } }
    }
    # Every tools/*.ps1 that declares a -SelfTest switch (preflight.ps1 itself aside) is registered by its path.
    $named = @($entries | ForEach-Object { @($_.command) } | Where-Object { "$_" -like 'tools/*.ps1' })
    foreach ($f in @(Get-ChildItem -LiteralPath (Join-Path $Root 'tools') -Filter '*.ps1' -File -ErrorAction SilentlyContinue)) {
        if ($f.Name -eq 'preflight.ps1') { continue }
        if ((Get-Content -LiteralPath $f.FullName -Raw) -match '\[switch\]\s*\$SelfTest\b' -and $named -notcontains "tools/$($f.Name)") {
            $problems += "unregistered tools/$($f.Name)"
        }
    }
    return @{ Entries = $entries; Problems = $problems }
}

function Test-CheckSelfTestRegistry([string]$Root) {
    $reg = Get-SelfTestRegistry $Root
    if ($reg.Problems) { return New-Result $false "selftest registry: $($reg.Problems -join '; ')" }
    return New-Result $true "selftest registry: $($reg.Entries.Count) external selftests, each with its cases; every tools -SelfTest registered"
}

# Runs one registered external selftest from the repository root. Returns $null or why it failed.
function Invoke-ExternalSelfTest($Entry) {
    $cmd = @($Entry.command)
    Push-Location $repoRoot
    try { $out = @(& $cmd[0] @($cmd | Select-Object -Skip 1) 2>&1 | ForEach-Object { "$_" }); $code = $LASTEXITCODE }
    finally { Pop-Location }
    $hit = if ($Entry.success) { $out | Where-Object { $_.Trim() -eq $Entry.success } } else { $out | Where-Object { $_ -match $Entry.successPattern } }
    Write-Verbose "$($Entry.name) -> exit $code; $(($out | Select-Object -Last 1))"
    if ($code -ne 0) { return "$($Entry.name): exit $code ($(($out | Where-Object { $_ -match '\S' } | Select-Object -Last 1)))" }
    if (-not $hit) { return "$($Entry.name): no success line '$($Entry.success)$($Entry.successPattern)'" }
    return $null
}

# ---------------------------------------------------------------- checks: event-library (D17, D18, D33, D34)

# The [Pillars] switches of Config/Settings.cs are set, and the cfg reloaded, only by Services/PillarSwitches.cs, and
# nothing calls ConfigFile.Save: BepInEx saves the cfg itself (event-library D18, S-11). A reload rewrites every entry in
# memory (Review 6 F3), so one outside PillarSwitches fails too. The count is PillarSwitches' entry writes and reloads.
$script:PillarEntryRx = '\b(?:EmpowermentEnabled|EventSpawnsEnabled|BossReinforcementsEnabled|DefendedZonesEnabled|SiegeWavesEnabled)\s*\.\s*Value\s*=(?!=)'
$script:CfgSaveRx = '(?i)\bconfig(?:file)?\s*\.\s*Save\s*\('
$script:CfgReloadRx = '(?i)\bconfig(?:file)?\s*\.\s*Reload\s*\('

function Test-CheckCfgWrites([string]$Root) {
    $owner = "$PkgRel/Services/PillarSwitches.cs"
    if ($null -eq (Read-Text $Root $owner)) { return New-Result $false "cfg writes: $owner not found" }
    $sites = 0; $bad = @()
    foreach ($f in @(Get-CsFiles $Root)) {
        $t = Remove-CsLiterals (Read-Text $Root $f)
        if ([regex]::IsMatch($t, $script:CfgSaveRx)) { $bad += "ConfigFile.Save in $f" }
        $writes = [regex]::Matches($t, $script:PillarEntryRx).Count
        $reloads = [regex]::Matches($t, $script:CfgReloadRx).Count
        if ($f -eq $owner) { $sites += $writes + $reloads; continue }
        if ($writes) { $bad += "a [Pillars] entry set in $f" }
        if ($reloads) { $bad += "ConfigFile.Reload in $f" }
    }
    if ($bad) { return New-Result $false "cfg writes: $($bad -join '; ')" }
    if ($sites -eq 0) { return New-Result $false "cfg writes: $owner sets no [Pillars] entry" }
    return New-Result $true "cfg writes: only PillarSwitches ($sites call sites)"
}

# VCF, the command-ingress and authorization boundary, is a hard dependency of 0.10.x only (event-library D17): Plugin.cs
# declares [BepInDependency("gg.deca.VampireCommandFramework", ">=0.10.0 <0.11.0")], never a SoftDependency, and the
# csproj references VRising.VampireCommandFramework 0.10.*. The fifth part of -AuthSuite.
function Test-CheckVcfDependency([string]$Root) {
    $plugin = Read-Text $Root "$PkgRel/Plugin.cs"
    $proj = Read-Text $Root "$PkgRel/Nyarlathotep.csproj"
    if ($null -eq $plugin -or $null -eq $proj) { return New-Result $false 'vcf dependency: Plugin.cs or Nyarlathotep.csproj not found' }
    $deps = @([regex]::Matches((Remove-CsComments $plugin), '\[\s*BepInDependency\s*\(([^\]]*)\)\s*\]') |
        Where-Object { $_.Groups[1].Value -match '"gg\.deca\.VampireCommandFramework"' })
    if ($deps.Count -ne 1) { return New-Result $false "vcf dependency: Plugin.cs declares the VCF BepInDependency $($deps.Count) times, not once" }
    $args0 = $deps[0].Groups[1].Value
    if ($args0 -match 'SoftDependency') { return New-Result $false 'vcf dependency: VCF is a SoftDependency' }
    if ($args0 -notmatch '^\s*"gg\.deca\.VampireCommandFramework"\s*,\s*">=0\.10\.0 <0\.11\.0"\s*$') {
        return New-Result $false 'vcf dependency: the VCF BepInDependency lacks its range ">=0.10.0 <0.11.0"'
    }
    if ($proj -notmatch '<PackageReference\s+Include="VRising\.VampireCommandFramework"\s+Version="0\.10\.\*"') {
        return New-Result $false 'vcf dependency: Nyarlathotep.csproj lacks PackageReference VRising.VampireCommandFramework 0.10.*'
    }
    return New-Result $true 'vcf dependency: hard, >=0.10.0 <0.11.0, PackageReference 0.10.*'
}

# One `dotnet test` run, fail-closed (event-library D34): $null when it passed, else why. A non-zero exit, a Failed count
# above 0, a Passed count of 0 or none at all (a filter matching nothing exits 0 with "Passed: 0" or no summary), a
# skipped test, and no output fail.
function Get-TestRunVerdict([string]$Output, [int]$Code) {
    if ($Output -notmatch '\S') { return 'no output' }
    $passed = if ($Output -match 'Passed:\s*(\d+)') { [int]$Matches[1] } else { 0 }
    $failed = if ($Output -match 'Failed:\s*(\d+)') { [int]$Matches[1] } else { 0 }
    $skipped = if ($Output -match 'Skipped:\s*(\d+)') { [int]$Matches[1] } else { 0 }
    if ($failed -gt 0) { return "$failed failed" }
    if ($Code -ne 0) { return "exit code $Code" }
    if ($passed -eq 0) { return 'no tests ran' }
    if ($skipped -gt 0) { return "$skipped skipped" }
    return $null
}

# The fixture half of D34: output.txt and exitcode.txt of a canned run, judged by Get-TestRunVerdict (selftest only).
function Test-CheckTestRuns([string]$Root) {
    $out = "$(Read-Text $Root 'output.txt')"
    $code = if ("$(Read-Text $Root 'exitcode.txt')" -match '-?\d+') { [int]$Matches[0] } else { 0 }
    $why = Get-TestRunVerdict $out $code
    if ($why) { return New-Result $false "test run: $why" }
    $n = if ($out -match 'Passed:\s*(\d+)') { $Matches[1] } else { '0' }
    return New-Result $true "test run: $n passed"
}

# The tick budget with behaviours (event-spawns D24): in a copy of a session's BepInEx/LogOutput.log (a fixture's
# LogOutput.log), after the first "nyar health: <n> events, <m> tracked" line with m >= MinTracked, one warm-up
# "tick timing" window is skipped; the next Windows timing lines must each have avg < 5 ms and be preceded, since the
# previous timing line, by a "hunt targets: <n>" line with n >= MinTargets; from the warm-up line to the last of them
# every health line shows m >= MinTracked and no "slow tick:" line appears ("slowest tick:", A70's window line, is not
# one), the last window's own tick included: the scheduler logs a slow tick after the timing line of the tick that closed
# the window, so the lines right after the last timing line are read too (Codex step 3 F1).
# → "timing span: <w>/<w> windows under 5 ms, tracked >= <t>, targets >= <n>, 0 slow ticks".
$script:TimingSpanLog = $null
$script:TimingSpanArgs = @{ MinTracked = 140; MinTargets = 1; Windows = 10 }
function Test-CheckTimingSpan([string]$Root) {
    $path = if (Test-IsFixture $Root) { Join-Path $Root 'LogOutput.log' } else { $script:TimingSpanLog }
    $a = $script:TimingSpanArgs
    if ($a.MinTracked -lt 0 -or $a.MinTargets -lt 0 -or $a.Windows -lt 1) { return New-Result $false 'timing span: -MinTracked and -MinTargets must be 0 or more, -Windows 1 or more' }
    if (-not $path -or -not (Test-Path -LiteralPath $path -PathType Leaf)) { return New-Result $false 'timing span: no span (no log)' }
    $lines = @([IO.File]::ReadAllLines($path))
    $health = '\[nyar\] nyar health: \d+ events, (\d+) tracked'
    $timing = '\[nyar\] tick timing: avg ([\d.]+) ms, max [\d.]+ ms over \d+ ticks'
    $start = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match $health -and [int]$Matches[1] -ge $a.MinTracked) { $start = $i; break }
    }
    if ($start -lt 0) { return New-Result $false "timing span: no span (no health line with $($a.MinTracked) tracked)" }
    $warm = -1; $windows = 0; $targets = -1
    for ($i = $start + 1; $i -lt $lines.Count -and $windows -lt $a.Windows; $i++) {
        $l = $lines[$i]
        if ($warm -lt 0) { if ($l -match $timing) { $warm = $i; $targets = -1 }; continue }
        if ($l -match '\[nyar\] hunt targets: (\d+)\s*$') { $targets = [int]$Matches[1]; continue }
        if ($l -match '\[nyar\] slow tick: ') { return New-Result $false "timing span: slow tick in the span (line $($i + 1))" }
        if ($l -match $health -and [int]$Matches[1] -lt $a.MinTracked) { return New-Result $false "timing span: $($Matches[1]) tracked in the span (line $($i + 1)), below $($a.MinTracked)" }
        if ($l -match $timing) {
            $windows++
            $avg = [double]::Parse($Matches[1], [Globalization.CultureInfo]::InvariantCulture)
            if ($avg -ge 5) { return New-Result $false "timing span: window $windows avg $($Matches[1]) ms, not under 5 ms (line $($i + 1))" }
            if ($targets -lt $a.MinTargets) {
                $why = if ($targets -lt 0) { 'no hunt targets line' } else { "hunt targets $targets" }
                return New-Result $false "timing span: window $windows has $why, below $($a.MinTargets) (line $($i + 1))"
            }
            $targets = -1
        }
    }
    if ($warm -lt 0) { return New-Result $false 'timing span: no span (no warm-up window after the health line)' }
    if ($windows -lt $a.Windows) { return New-Result $false "timing span: $windows/$($a.Windows) windows after the warm-up" }
    # $i is the line after the last timing line: its own tick's "slowest tick:" and "slow tick:" lines follow it at once.
    for (; $i -lt $lines.Count -and $lines[$i] -match '\[nyar\] slow(est)? tick: '; $i++) {
        if ($lines[$i] -match '\[nyar\] slow tick: ') { return New-Result $false "timing span: slow tick in the span (line $($i + 1))" }
    }
    return New-Result $true "timing span: $windows/$($a.Windows) windows under 5 ms, tracked >= $($a.MinTracked), targets >= $($a.MinTargets), 0 slow ticks"
}

function Get-ClassFilter([string]$Class) { "FullyQualifiedName~Nyarlathotep.Tests.$Class." }

# Runs `dotnet test` once per VSTest filter, each written with | and & only (the word "or" is refused), building once
# first; one row per filter: Filter, Passed and Why ($null when it passed).
function Invoke-ClassTests([string[]]$Filters) {
    $project = Join-Path $repoRoot 'Nyarlathotep/Nyarlathotep.Tests'
    $built = $false
    foreach ($f in $Filters) {
        if ($f -match '(?i)\bor\b') { [pscustomobject]@{ Filter = $f; Passed = 0; Why = "filter uses the word 'or'" }; continue }
        $out = if ($built) { & dotnet test $project --no-build --filter $f 2>&1 | Out-String } else { & dotnet test $project --filter $f 2>&1 | Out-String }
        $code = $LASTEXITCODE
        $built = $true
        $n = if ($out -match 'Passed:\s*(\d+)') { [int]$Matches[1] } else { 0 }
        [pscustomobject]@{ Filter = $f; Passed = $n; Why = (Get-TestRunVerdict $out $code) }
    }
}

# The dependency suite's category table (event-library D33): tools/preflight-checks.json dependencySuites.<slug> is
# { "categories": [...], "rows": [...] }. categories names the slug's required categories (event-spawns D21: read per
# slug, no longer one fixed list), so a row deleted from rows is still missed; each row is of kind "tests" (class,
# control), "check" (function, run over its fixtures and the real tree) or "selftests" (names of externalSelfTests entries).
# Each listed slug's required categories, kept here as a floor so a category removed from the entry together with its
# row still fails (event-spawns A52). event-spawns' five joined in step 2 with its entry (D21).
$script:SuiteFloor = @{
    'event-library' = @('events-write', 'events-promote', 'state-write', 'cfg-save', 'catalogue', 'location-context', 'phase-source', 'vcf', 'release-tools')
    'event-spawns'  = @('territory', 'hunt-seed', 'player-query', 'unit-recipe', 'release-tools')
}

function Get-DependencySuite([string]$Root, [string]$Slug) {
    $path = Join-Path $Root 'tools/preflight-checks.json'
    if (-not (Test-Path -LiteralPath $path)) { return @{ Categories = @(); Rows = @() } }
    $s = (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json).dependencySuites.$Slug
    return @{ Categories = @($s.categories | Where-Object { "$_" -match '\S' }); Rows = @($s.rows | Where-Object { $_ }) }
}

function Get-DependencyTable([string]$Root, [string]$Slug) { return @((Get-DependencySuite $Root $Slug).Rows) }

# Every way the table of $Slug is wrong, statically: no categories, a required category missing or doubled among the rows,
# a row outside the categories, a tests row whose class has no <Control>_ test method under Nyarlathotep.Tests, a check
# row naming no Test-Check function of the manifest, a selftests row naming no externalSelfTests entry.
function Get-DependencyTableProblems([string]$Root, [string]$Slug) {
    $suite = Get-DependencySuite $Root $Slug
    $rows = @($suite.Rows)
    if ($suite.Categories.Count -eq 0) { return @("dependency suite: $Slug has no categories") }
    $p = @()
    foreach ($f in @($script:SuiteFloor[$Slug])) { if ($f -and $suite.Categories -notcontains $f) { $p += "category $f missing from the entry (floor)" } }
    foreach ($r in $rows) { if ($suite.Categories -notcontains $r.name) { $p += "row $($r.name) is not a category of $Slug" } }
    foreach ($want in $suite.Categories) {
        $n = @($rows | Where-Object name -eq $want).Count
        if ($n -eq 0) { $p += "category $want missing" } elseif ($n -gt 1) { $p += "category $want listed $n times" }
    }
    $json = Get-Content -LiteralPath (Join-Path $Root 'tools/preflight-checks.json') -Raw | ConvertFrom-Json
    $tests = @(Get-TreeFiles $Root | Where-Object { $_ -like 'Nyarlathotep/Nyarlathotep.Tests/*.cs' } | ForEach-Object { Remove-CsComments (Read-Text $Root $_) })
    foreach ($r in $rows) {
        switch ($r.kind) {
            'tests' {
                $cls = "$($r.class)"; $ctl = "$($r.control)"
                if ($cls -notmatch '^\w+$' -or $ctl -notmatch '^\w+$') { $p += "$($r.name): class and control must be names"; break }
                $hit = @($tests | Where-Object { $_ -match "\bclass\s+$cls\b" -and $_ -match "\bvoid\s+${ctl}_\w+\s*\(" })
                if ($hit.Count -eq 0) { $p += "$($r.name): no test $cls.${ctl}_*" }
            }
            'check' { if (@($json.checks | Where-Object function -eq $r.function).Count -ne 1) { $p += "$($r.name): no check $($r.function)" } }
            'selftests' {
                if (@($r.selftests).Count -eq 0) { $p += "$($r.name): no selftests named" }
                foreach ($s in @($r.selftests)) { if (@($json.externalSelfTests | Where-Object name -eq $s).Count -ne 1) { $p += "$($r.name): no external selftest $s" } }
            }
            default { $p += "$($r.name): kind '$($r.kind)' is not tests, check or selftests" }
        }
    }
    return $p
}

# Every slug of dependencySuites, each against its own categories (event-spawns D21).
function Test-CheckDependencySuite([string]$Root) {
    $path = Join-Path $Root 'tools/preflight-checks.json'
    # @(...) around the if: its output unrolls a one-slug array to a string, which + would then concatenate.
    $slugs = @(if (Test-Path -LiteralPath $path) { (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json).dependencySuites.PSObject.Properties.Name })
    if ($slugs.Count -eq 0) { return New-Result $false 'dependency table: no dependency suites' }
    $slugs = @($slugs + @($script:SuiteFloor.Keys | Where-Object { $slugs -notcontains $_ } | Sort-Object))   # a floor slug without an entry fails
    $problems = @(); $seen = @()
    foreach ($s in $slugs) {
        $sp = @(Get-DependencyTableProblems $Root $s)
        if ($sp) { $problems += @($sp | ForEach-Object { "${s}: $_" }) } else { $seen += "$s $((Get-DependencySuite $Root $s).Categories.Count)/$((Get-DependencySuite $Root $s).Categories.Count)" }
    }
    if ($problems) { return New-Result $false "dependency table: $($problems -join '; ')" }
    return New-Result $true "dependency table: $($seen -join ', ') categories"
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
# holds a token-shaped string. A directory named "<name>.ignored" is written as <name>: a fixture holds dist/, build/ and
# artifacts/ that way, which .gitignore would otherwise keep out of the repository (event-spawns D31).
function Copy-Fixture([string]$From, [string]$To) {
    if (-not (Test-Path $From)) { throw "fixture directory $From not found" }
    $base = (Resolve-Path $From).Path.TrimEnd('\', '/')
    foreach ($f in Get-ChildItem -Path $base -Recurse -File -Force) {
        if ($f.Name -eq '.gitkeep') { continue }
        $rel = @($f.FullName.Substring($base.Length + 1) -split '[\\/]' | ForEach-Object { $_ -replace '\.ignored$', '' }) -join '\'
        $dest = Join-Path $To $rel
        New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null
        if ($f.Name.EndsWith('.b64plant')) {
            $dest = $dest.Substring(0, $dest.Length - '.b64plant'.Length)
            [IO.File]::WriteAllBytes($dest, [Convert]::FromBase64String(([IO.File]::ReadAllText($f.FullName)).Trim()))
        } else { Copy-Item -LiteralPath $f.FullName -Destination $dest -Force }
    }
}

$script:CheckModes = @('default', 'paths', 'serverwrites', 'auditof', 'logcheck', 'sessionsof', 'authsuite', 'fixture', 'rollbackof')

# One check against its fixtures in scratch directories under $Tmp: good/ and every good-<name>/ must pass (regions A37),
# every bad/ and bad-<n>/ and empty must fail, each printing a line. Returns Ok, Problems and Extra (the bad fixtures beyond bad/).
function Invoke-FixtureBattery($Check, [string]$Tmp) {
    $ok = $true; $problems = @()
    $fx = Join-Path $repoRoot $Check.fixtures
    # good and empty, plus every bad fixture: bad/ and any bad-<n>/ (one planted fault each).
    $bads = @(Get-ChildItem $fx -Directory -Filter 'bad*' -ErrorAction SilentlyContinue | ForEach-Object Name | Sort-Object)
    if ($bads -notcontains 'bad') { $bads = @('bad') + $bads }
    # Every empty-<name>/ fails too, like empty (event-spawns D27, D31), and is described like a bad fixture.
    $empties = @(Get-ChildItem $fx -Directory -Filter 'empty-*' -ErrorAction SilentlyContinue | ForEach-Object Name | Sort-Object)
    foreach ($b in $bads + $empties) { if (-not $Check.plant.$b) { $ok = $false; $problems += "$($Check.name): no plant description for $b" } }
    $goods = @(Get-ChildItem $fx -Directory -Filter 'good-*' -ErrorAction SilentlyContinue | ForEach-Object Name | Sort-Object)
    foreach ($kind in @(@('good') + $goods + $bads + @('empty') + $empties)) {
        if (-not (Test-Path (Join-Path $fx $kind))) { $ok = $false; $problems += "$($Check.name): $kind fixture missing ($($Check.fixtures)/$kind)"; continue }
        $dir = Join-Path $Tmp "$($Check.name)-$kind"
        if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
        Copy-Fixture (Join-Path $fx $kind) $dir
        $script:FixtureRoot = $dir
        $r = Invoke-Check $Check.function $dir
        $script:FixtureRoot = $null
        Write-Verbose "$($Check.name) $kind -> $($r.Line)"
        $want = $kind -eq 'good' -or $kind -like 'good-*'
        if ($r.Pass -ne $want) { $ok = $false; $problems += "$($Check.name) $kind fixture: expected $(if ($want) {'pass'} else {'fail'}), got '$($r.Line)'" }
        if ($r.Line -notmatch '\S') { $ok = $false; $problems += "$($Check.name) $kind fixture printed nothing" }
        # A check whose manifest entry names emptyLine must print exactly that line on its empty fixture (walkable-spawns D9).
        if ($kind -eq 'empty' -and $Check.emptyLine -and $r.Line -cne $Check.emptyLine) {
            $ok = $false; $problems += "$($Check.name) empty fixture: expected '$($Check.emptyLine)', got '$($r.Line)'"
        }
    }
    return @{ Ok = $ok; Problems = $problems; Extra = $bads.Count - 1 }
}

# The -SelfTest body, returning Passed, Total, Extra, ExtOk, ExtTotal, SecLine and Problems (-SelfTest and -ControlSuite).
function Get-SelfTestResult {
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
        if ($script:CheckModes -notcontains $c.mode) { $problems += "$($c.name): mode '$($c.mode)' is not $($script:CheckModes -join ', ')" }
        if ($c.fixtures -ne "tools/preflight-fixtures/$($c.name)") { $problems += "$($c.name): fixtures must be tools/preflight-fixtures/$($c.name)" }
        if (@($c.inputs | Where-Object { "$_" -match '\S' }).Count -eq 0) { $problems += "$($c.name): no inputs listed" }
        if (@($c.plant.PSObject.Properties).Count -eq 0) { $problems += "$($c.name): no plant descriptions" }
    }

    $tmp = Join-Path ([IO.Path]::GetTempPath()) "nyar-selftest-$PID"
    $passed = 0; $extra = 0
    foreach ($c in $checks) {
        $b = Invoke-FixtureBattery $c $tmp
        $problems += $b.Problems
        $extra += $b.Extra
        if ($b.Ok) { $passed++ }
    }
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }

    # Every registered external selftest runs and must print its success line (faction-empowerment D22).
    $reg = Get-SelfTestRegistry $repoRoot
    $problems += @($reg.Problems | ForEach-Object { "selftest registry: $_" })
    $extOk = 0
    foreach ($e in $reg.Entries) {
        $why = Invoke-ExternalSelfTest $e
        if ($why) { $problems += "external selftest $why" } else { $extOk++ }
    }
    # Then the secrets check on the real tracked tree: a token in the repository fails the selftest too (D22).
    $sec = Invoke-Check 'Test-CheckSecrets' $repoRoot
    if (-not $sec.Pass) { $problems += "real tree: $($sec.Line)" }
    $probe = Test-IndexSecretProbe
    if ($probe) { $problems += $probe } else { $sec.Line += '; index probe: a staged tcli token read and a staged tools/ credential read fail' }
    return @{ Passed = $passed; Total = $checks.Count; Extra = $extra; ExtOk = $extOk; ExtTotal = $reg.Entries.Count; SecLine = $sec.Line; Problems = $problems }
}

function Invoke-SelfTest {
    $st = Get-SelfTestResult
    $ext = "$($st.ExtOk)/$($st.ExtTotal) external selftests"
    if ($st.Problems) {
        Write-Host "selftest: $($st.Passed)/$($st.Total) checks, $ext — FAILED" -ForegroundColor Red
        $st.Problems | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
        exit 1
    }
    Write-Host "selftest: $($st.Passed)/$($st.Total) checks, $ext (3 fixtures each, $($st.Extra) extra bad fixtures; $($st.SecLine) on the real tree)" -ForegroundColor Green
    exit 0
}

# Usage, before any mode runs (step 3 code review, Codex cross-inspection round 4): one mode at a time, and each
# qualifier only with its own mode, so no combination silently runs the first mode and drops the rest.
$modes = @(@{ SelfTest = [bool]$SelfTest; Paths = [bool]$Paths; RollbackOf = [bool]$RollbackOf; ServerWrites = [bool]$ServerWrites
    LogCheck = [bool]$LogCheck; AuditOf = [bool]$AuditOf; SessionsOf = [bool]$SessionsOf; AuthSuite = [bool]$AuthSuite; Tests = [bool]$Tests
    ControlSuite = [bool]$ControlSuite; DependencySuite = [bool]$DependencySuite; ListCommands = [bool]$ListCommands; TimingSpan = [bool]$TimingSpan }.GetEnumerator() |
    Where-Object { $_.Value } | ForEach-Object { "-$($_.Key)" } | Sort-Object)
if ($modes.Count -gt 1) { Write-Host "usage: one mode at a time, not $($modes -join ' ')" -ForegroundColor Red; exit 2 }
if ($DeclaredOf -and -not $Paths) { Write-Host 'usage: -DeclaredOf <slug> needs -Paths' -ForegroundColor Red; exit 2 }
if (($From -or $To) -and -not $RollbackOf) { Write-Host 'usage: -From and -To go with -RollbackOf' -ForegroundColor Red; exit 2 }
if ([bool]$From -xor [bool]$To) { Write-Host 'usage: -From and -To name a range together, or neither is given' -ForegroundColor Red; exit 2 }
if (($Snapshot -or $Compare -or $AfterCleanup) -and -not $ServerWrites) { Write-Host 'usage: -Snapshot, -Compare and -AfterCleanup go with -ServerWrites' -ForegroundColor Red; exit 2 }
if (@('MinTracked', 'MinTargets', 'Windows' | Where-Object { $PSBoundParameters.ContainsKey($_) }).Count -and -not $TimingSpan) { Write-Host 'usage: -MinTracked, -MinTargets and -Windows go with -TimingSpan' -ForegroundColor Red; exit 2 }
if ($TimingSpan -and ($MinTracked -lt 0 -or $MinTargets -lt 0 -or $Windows -lt 1)) { Write-Host 'usage: -MinTracked and -MinTargets must be 0 or more, -Windows 1 or more' -ForegroundColor Red; exit 2 }

if ($SelfTest) { Invoke-SelfTest }

if ($Tests) {
    # One run per class, fail-closed (event-library D34).
    $classes = @($Tests -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    $rows = @(Invoke-ClassTests @($classes | ForEach-Object { Get-ClassFilter $_ }))
    $bad = @($rows | Where-Object Why)
    $n = ($rows | Measure-Object Passed -Sum).Sum
    if ($classes.Count -eq 0 -or $bad) {
        Write-Host "tests: $($rows.Count - $bad.Count)/$($classes.Count) classes — FAILED: $(@($bad | ForEach-Object { "$($_.Filter) $($_.Why)" }) -join '; ')" -ForegroundColor Red
        exit 1
    }
    Write-Host "tests: $($classes.Count)/$($classes.Count) classes, $n passed" -ForegroundColor Green
    exit 0
}

if ($ControlSuite) {
    # The D31 classes (controlSuites.<slug> of tools/preflight-checks.json), then the selftest body in this process
    # (event-library D34): both parts must pass.
    $classes = @((Get-Manifest).controlSuites.$ControlSuite | Where-Object { $_ })
    $rows = @(Invoke-ClassTests @($classes | ForEach-Object { Get-ClassFilter $_ }))
    $bad = @($rows | Where-Object Why)
    $st = Get-SelfTestResult
    $line = "control suite: $ControlSuite tests $($rows.Count - $bad.Count)/$($classes.Count) classes, selftest $($st.Passed)/$($st.Total) checks"
    $why = @($bad | ForEach-Object { "$($_.Filter) $($_.Why)" }) + @($st.Problems)
    if ($classes.Count -eq 0) { $why = @("$ControlSuite has no classes") + $why }
    if ($why) {
        Write-Host "$line — FAILED" -ForegroundColor Red
        $why | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
        exit 1
    }
    Write-Host $line -ForegroundColor Green
    exit 0
}

if ($DependencySuite) {
    # Every dependency-failure category of the slug in one command (event-library D33): its tests one control at a time,
    # the VCF check over its fixtures and the real tree, and the release tools' selftests with their success lines.
    $slug = $DependencySuite
    $static = @(Get-DependencyTableProblems $repoRoot $slug)
    if ($static.Count -eq 1 -and $static[0] -eq "dependency suite: $slug has no categories") { Write-Host $static[0] -ForegroundColor Red; exit 1 }
    if ($static) { Write-Host "dependency suite: $slug — FAILED: $($static -join '; ')" -ForegroundColor Red; exit 1 }
    $manifest = Get-Manifest
    $reg = Get-SelfTestRegistry $repoRoot
    $tmp = Join-Path ([IO.Path]::GetTempPath()) "nyar-depsuite-$PID"
    $done = @(); $fail = @()
    $categories = @((Get-DependencySuite $repoRoot $slug).Categories)
    foreach ($want in $categories) {
        $r = @(Get-DependencyTable $repoRoot $slug | Where-Object name -eq $want)[0]
        $why = @()
        switch ($r.kind) {
            'tests' {
                $row = @(Invoke-ClassTests @("FullyQualifiedName~Nyarlathotep.Tests.$($r.class).$($r.control)_"))[0]
                if ($row.Why) { $why += "$($r.class).$($r.control)_ $($row.Why)" }
            }
            'check' {
                $c = @($manifest.checks | Where-Object function -eq $r.function)[0]
                $b = Invoke-FixtureBattery $c $tmp
                $why += $b.Problems
                $real = Invoke-Check $r.function $repoRoot
                if (-not $real.Pass) { $why += $real.Line }
            }
            'selftests' {
                foreach ($s in @($r.selftests)) {
                    $e = @($reg.Entries | Where-Object name -eq $s)[0]
                    $w = Invoke-ExternalSelfTest $e
                    if ($w) { $why += $w }
                }
            }
        }
        if ($why) { $fail += "${want}: $($why -join ', ')" } else { $done += $want }
    }
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
    $total = $categories.Count
    if ($fail) {
        Write-Host "dependency suite: $slug $($done.Count)/$total — FAILED" -ForegroundColor Red
        $fail | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
        exit 1
    }
    Write-Host "dependency suite: $slug $total/$total ($($done -join ', '))" -ForegroundColor Green
    exit 0
}

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
    # row/push access tests, then the checks that keep adminOnly, the admin list, the gateway and the entity writes
    # (event-spawns D34) honest, then the public/admin split of the api commands. A filter that runs no test is a failure.
    $parts = @(); $fail = @()
    # Each class runs on its own, so a deleted, renamed or fully skipped class is "no tests ran", not hidden by the other.
    $testsOk = $true
    foreach ($row in @(Invoke-ClassTests @(Get-ClassFilter 'AuthorizationTests'; Get-ClassFilter 'ApiAccessTests'))) {
        if ($row.Why) { $fail += "$($row.Filter) $($row.Why)"; $testsOk = $false }
    }
    if ($testsOk) { $parts += 'tests' }
    # "commands" is the adminOnly walk and the spawn-changing command inventory together (event-spawns D22).
    foreach ($c in @(@('commands', @('Test-CheckCommands', 'Test-CheckAuthSuite')), @('admin list', @('Test-CheckAdminList')), @('gateway', @('Test-CheckGatewayOnly', 'Test-CheckMutatingFloor')),
            @('entity writes', @('Test-CheckEntityWrites')), @('vcf', @('Test-CheckVcfDependency')))) {
        $ok = $true
        foreach ($fn in $c[1]) { $r = Invoke-Check $fn $repoRoot; if (-not $r.Pass) { $fail += $r.Line; $ok = $false } }
        if ($ok) { $parts += $c[0] }
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

if ($TimingSpan) {
    $script:TimingSpanLog = $TimingSpan
    $script:TimingSpanArgs = @{ MinTracked = $MinTracked; MinTargets = $MinTargets; Windows = $Windows }
    $r = Test-CheckTimingSpan $repoRoot
    if ($r.Pass) { Write-Host $r.Line -ForegroundColor Green; exit 0 }
    Write-Host $r.Line -ForegroundColor Red
    exit 1
}

$manifest = Get-Manifest
$mode = if ($Paths) { 'paths' } elseif ($RollbackOf) { 'rollbackof' } elseif ($ServerWrites) { 'serverwrites' } elseif ($AuditOf) { 'auditof' } elseif ($LogCheck) { 'logcheck' } elseif ($SessionsOf) { 'sessionsof' } else { 'default' }
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
