---
dod: 2
rubric: 2
id: dod-20260923-spk1
slug: spikes
title: Spikes S1-S3, march, restart marker, carrier buff
status: in-progress
size: M
parent: nyarlathotep
kind: feature
created: 2026-09-23
baselined: 2026-09-23
closed: none
commit: 4761cec
coverage_author: 15/15 layers · 49/49 probes
coverage_reviewer: 15/15 layers · 49/49 probes
review: human
---

# DoD: Spikes S1-S3, march, restart marker, carrier buff

**Size:** M. This is one deliverable: three throwaway in-game experiments plus their recorded verdicts. It is not S because it adds a new admin command surface. It is not L because it adds no dependency or schema, and no spike code ships.
**Planned:** interactively. This is a child of the approved Epic `nyarlathotep`. Both the Epic's `## Child constraints` (the spikes entry) and its inherited `## Business rules` govern this plan; the rules this plan touches are restated in its own Business rules.
**Request:** From the Epic's Child constraints: "**spikes** — S1 movement, S2 restart/marker/DontSaveEntity, S3 carrier buff on a native NPC; each spike's result recorded in its feature doc with a go/no-go line; no spike code ships (it lives behind a debug command removed before the child closes). Samples the component and system contracts each later hook depends on."

## Definition of Done
- [ ] D1 · **Compiles with spike code** the solution builds in Release with 0 errors and 0 warnings with the spike harness present, without deploying · cmd: dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__ → "0 Warning(s)" and "0 Error(s)" (fails when: a spike file has a compile error or adds a warning)
- [ ] D2 · **Every command admin-only** every [Command] attribute in any .cs file of the repository (git ls-files plus untracked-not-ignored), spike commands included, lives under Commands/ and carries adminOnly: true. The only exceptions are the allow-list {nyar, status, help, me, top, hide, show, version, sub} (Epic A4, A13), which is versioned as $script:PublicCommands in tools/preflight.ps1 · cmd: pwsh tools/preflight.ps1 → line "commands: <n> admin-only, <m> public (allow-listed)" (fails when: any command in any file outside the allow-list lacks adminOnly: true, or a [Command] lies outside Commands/)
- [ ] D3 · **Spike edits fenced** spike code makes structural edits (AddComponent*, RemoveComponent*, AddBuffer, DestroyEntity) only through EntityExtensions.cs helpers. Every method there that makes such a call refuses entities carrying the Prefab component · cmd: pwsh tools/preflight.ps1 → line "structural edits: fenced (<k> Prefab-guarded calls in EntityExtensions.cs)" (fails when: any other .cs file makes one of those calls, or an EntityExtensions method makes one without a Has<Prefab> guard)
- [ ] D4 · **Only the throwaway save written** a snapshot taken before step 3 (`pwsh tools/preflight.ps1 -ServerWrites -Snapshot $env:TEMP\nyarspikes-before.tsv`: path, size, last-write time, and SHA-256 for every file under the server directory's BepInEx/, logs/ and save-data-*/ and under %USERPROFILE%\AppData\LocalLow\Stunlock Studios, and for every file under the owner's own test server data C:\VRising-LocalServer (-LocalServerPath); path, size and time elsewhere under the server directory) is compared with the tree after each session. Every created, changed or deleted file matches a `server:` or `external:` glob in tools/paths-manifest.txt. No Saves folder other than save-data-nyarspikes exists or changed, and no file under C:\VRising-LocalServer is created, changed or deleted. After step 8, save-data-nyarspikes no longer exists · cmd: pwsh tools/preflight.ps1 -ServerWrites -Compare $env:TEMP\nyarspikes-before.tsv → line "server writes: <c> created, <m> changed, <d> deleted, all in manifest, no other save" (fails when: a created, changed or deleted file matches no server or external glob, any file under C:\VRising-LocalServer changed ("owner data touched"), another Saves folder exists or changed, the snapshot is missing or empty, or, with -AfterCleanup, save-data-nyarspikes still exists)
- [ ] D5 · **Spike units audited** `.nyar spike sweep` lists every unit spawned this boot and every marked entity (SpellLevel.Level 1314472274). For each it checks four things: the marker is present, LifeTime is present and at most 600 s, DestroyWhenDisabled is present, and the unit appears in both sets. It replies "sweep: marked <a>, listed <b>, faults 0" or names each fault. At most 30 marked units are alive at once. `.nyar spike clear` queues every unit and destroys at most 5 per frame. While the queue drains, a second clear replies "clear in progress (<k> left)"; once drained it replies "nothing to clear" · manual: in the throwaway save, run `.nyar spike tag 10` three times, then a fourth time (reply "spike limit 30 (30 alive)"); `.nyar spike sweep` shows faults 0; run `.nyar spike clear` twice quickly (second reply "clear in progress"); while it drains run `.nyar spike tag 1` (reply "clear in progress (<k> left)", nothing spawned); after 2 s run clear again (reply "nothing to clear"), and `.nyar spike sweep` shows "marked 0, listed 0, faults 0"; then run `.nyar spike march 2` and `.nyar spike clear` before the group arrives (the log shows the mover stopped before the first destroy, and sweep ends at 0); the log shows clear batches of at most 5
- [ ] D6 · **Arguments and gates** every argument in Design › UX › Argument ranges is range-checked, refused with "<arg> must be <min>-<max>", and changes nothing. A carrier GUID that does not resolve to a prefab with Buff and LifeTime, or is on the GAME_ASSETS do-not-spawn list, is refused. Every spike command except clear shares one 1 s server-wide cooldown and replies "spike cooldown (<ms> ms)" inside it. Refusals follow the precedence in Business rules 4. Each command catches its own exceptions, replies "spike failed: <type>: <message>", and logs one line with no stack. The server keeps ticking · manual: for each argument in the table, run one value below its minimum and one above its maximum (every reply names the range); run `.nyar spike empower 30 10 1227555070` (refused if Buff_InCombat_Npc_Elite lacks LifeTime, else applied and recorded) and `.nyar spike empower 30 10 12345` (refused: unknown prefab). Then run the precedence pairs: with 30 alive run `.nyar spike tag 0` (argument message wins over the limit); set General.Enabled=false, restart, and run `.nyar spike tag 0` (Enabled message wins over the argument), then `.nyar spike clear`, `.nyar spike sweep` and `.nyar spike inspect` (all run). Run `.nyar spike inspect` twice within 1 s (second reply "spike cooldown (<ms> ms)"), then `.nyar spike clear` twice within 1 s (both run: clear has no cooldown). Record each reply in docs/audits/spikes.md
- [ ] D7 · **S1 march verdict** docs/features/SIEGES.md › Test results has a dated S1 entry, readable as a checklist: for each of variants 1, 2, 3, 4 and the wall run: arrived (yes/no, seconds taken), path taken, stuck (where), engaged on arrival (yes/no), behaviour at the wall; the anchor entity used, with its PrefabGUID; a verdict line "S1 verdict: go — <mechanism>" or "S1 verdict: no-go — <reason>" · manual: read the entry; every field above is present for every run, or the entry says why a run was impossible
- [ ] D8 · **S2 restart verdict** docs/features/EVENT_SPAWNS.md › Test results has a dated S2 entry, readable as a checklist: sweep output before the restart, after it, after the LifeTime ran out, and after the 200 m walk-away; marker survives restart (yes/no); DontSaveEntity units present after restart (count); LifeTime after restart (continued, reset or gone); DestroyWhenDisabled removed units once no player was near (yes/no); load errors (quoted, or none); a verdict line "S2 verdict: go — <tagging and restart recipe>" or "S2 verdict: no-go — <reason>" · manual: read the entry; every field above is present
- [ ] D9 · **S3 carrier verdict** docs/features/FACTION_EMPOWERMENT.md › Test results has a dated S3 entry, readable as a checklist: `.nyar spike inspect` output before, during and after the buff; damage dealt and taken with and without the buff, in combat; Health.Value behaviour when the buff applies; stats back to base after expiry (yes/no); buff present after streaming out to 150 m for 60 s and back (yes/no); state after a restart mid-buff; the carrier prefab and GUID, with LifeTime confirmed; a verdict line "S3 verdict: go — <carrier recipe>" or "S3 verdict: no-go — <reason>" · manual: read the entry; every field above is present
- [ ] D10 · **Contracts recorded** docs/RESEARCH_NOTES.md › "## Spike contracts (VampireReferenceAssemblies 1.1.12)" has one row per component and system the spike code read or wrote. Each row gives the fields touched, the prefab GUIDs, the source the recipe came from (path and line), and observed = documented or observed ≠ documented, with a note · manual: list every component and system type named in Spikes/*.cs and Commands/SpikeCommands.cs (before step 8 deletes them); each has a row with all four columns filled
- [ ] D11 · **Verdicts reach the Epic** every no-go verdict, and every go verdict that changes a later child's mechanism, has an amendment in docs/dod/nyarlathotep.md dated before this plan closes. Each of the three feature docs' Status lines names its spike's result. A verdict corrected after it was written follows Business rules 6 · manual: read the three verdict lines, the three Status lines and the Epic's ## Amendments; each no-go or changed mechanism has an amendment that names it, and each correction has its superseding entry
- [ ] D12 · **Spike sessions clean** no spike session crashes the server. No session's BepInEx/LogOutput.log contains a stack frame from our assembly ("   at Nyarlathotep."). Caught spike failures are logged as single "[nyar-spike] spike failed:" lines without a stack, so any such frame means an unhandled exception · cmd: pwsh -NoProfile -Command "$l='C:\Program Files (x86)\Steam\steamapps\common\VRisingDedicatedServer\BepInEx\LogOutput.log'; if (-not (Test-Path $l)) { 'log check: no log'; exit 1 }; $n=(Select-String -Path $l -SimpleMatch '   at Nyarlathotep.').Count; $s=(Select-String -Path $l -SimpleMatch '[nyar-spike]').Count; if ($s -eq 0) { 'log check: no spike lines (wrong log?)'; exit 1 }; \"log check: $n unhandled, $s spike lines\"; exit [int]($n -gt 0)" → "log check: 0 unhandled, <s> spike lines", run after each session and recorded in docs/audits/spikes.md (fails when: an unhandled exception left a Nyarlathotep stack frame, the log is missing, or the log has no spike lines)
- [ ] D13 · **Spike code removed** no .cs file in the repository (git ls-files plus untracked-not-ignored, plus every Compile item of `dotnet msbuild Nyarlathotep/Nyarlathotep/Nyarlathotep.csproj -getItem:Compile`, which includes ignored files) declares or uses an identifier beginning with "Spike", declares namespace Nyarlathotep.Spikes, or registers a command group or command named "spike". Every Compile item outside obj/ must also be a file git sees, so an ignored source file fails on its own whatever its name; a neutral-named tracked file is caught by D19. The check is Test-CheckSpikeCode in tools/preflight.ps1. While docs/dod/spikes.md has status in-progress, spike code prints "spike code: present, allowed while spikes is in-progress" and passes. Otherwise spike code prints "spike code: present in <files>" and fails. No spike code prints "spike code: none" · cmd: pwsh tools/preflight.ps1 → line "spike code: none" (fails when: any spike identifier, namespace or command remains in any .cs file, a Compile item outside obj/ is ignored by git, or spike code exists while docs/dod/spikes.md is not in-progress)
- [ ] D14 · **New checks self-tested** tools/preflight-checks.json has entries for Test-CheckSpikeCode, Test-CheckServerWrites and Test-CheckAuditSteps, each with good, bad and empty fixtures, alongside the Epic's entries for the checks D2, D3, D15 and D16 reuse. Test-CheckSpikeCode has two bad fixtures: a class named SpikeHelper in Services/ with the plan status done, and a neutral-named Services/Helper.cs listed in the captured compile-items.txt but absent from the captured git listing. Test-CheckServerWrites' bad fixture is a before/after snapshot pair where a file under another Saves folder was deleted. Test-CheckAuditSteps' bad fixture drops step 5's Codex verdict line. All fail on empty input · cmd: pwsh tools/preflight.ps1 -SelfTest → "selftest: <n>/<n> checks, 3 fixtures each" (fails when: any listed check passes a bad or empty fixture or fails its good one, or a Test-Check function has no manifest entry)
- [ ] D15 · **Spike paths manifested** after a deploying spike build and one spike session, the path walk finds no path outside tools/paths-manifest.txt. The walk covers every tracked, untracked and ignored path in the repository, plus the server's BepInEx/plugins/Nyarlathotep*, BepInEx/config/*Nyarlathotep*, BepInEx/config/Nyarlathotep/** and save-data-nyar* · cmd: pwsh tools/preflight.ps1 -Paths → line "paths: <n> walked, all in manifest" (fails when: a walked path matches no manifest glob of its kind)
- [ ] D16 · **No secrets in spike work** the secrets scan stays clean with the spike harness, the audit record and the quoted log excerpts present · cmd: pwsh tools/preflight.ps1 → line "secrets: none (<n> files scanned)" (fails when: a spike file, audit record or log excerpt contains a token form, or a .cs file reads the environment)
- [ ] D17 · **Audit matrix complete** docs/audits/spikes.md has a "### Step <n>" entry under a "## Pre-audit" heading and under a "## Post-audit" heading for every Build plan step 1–9 of this plan, and each post-audit entry has a "Codex verdict:" line · cmd: pwsh tools/preflight.ps1 -AuditOf spikes → line "audit steps: spikes 9/9 pre, 9/9 post, 9/9 Codex verdicts" (fails when: any step lacks a pre-audit entry, a post-audit entry or a Codex verdict line, or the plan's Build plan has no steps)
- [ ] D18 · **Throwaway world removed** after D13, the build without spike code is deployed and boots with "Nyarlathotep initialized", `.nyar spike` is unknown to VCF, and save-data-nyarspikes is deleted · manual: stop the server, deploy the D13 build, start it with the step-3 launch line, read the boot log, type `.nyar spike sweep` in game (VCF replies unknown command), stop the server, delete save-data-nyarspikes, then run the D4 command with -AfterCleanup; record it all in docs/audits/spikes.md
- [ ] D19 · **Code back to base** after step 8, the only file under Nyarlathotep/ that differs from the pre-harness commit (the step-2 parent, recorded in docs/audits/spikes.md) is Nyarlathotep/Nyarlathotep/EntityExtensions.cs, which keeps the Prefab-guarded helpers, and the project's Compile items (tracked, untracked or ignored, whatever their names) are exactly the list captured at the step-2 parent · cmd: git diff --name-only <step-2 parent> HEAD -- Nyarlathotep/ → exactly "Nyarlathotep/Nyarlathotep/EntityExtensions.cs"; then pwsh -NoProfile -Command "$now = dotnet msbuild Nyarlathotep/Nyarlathotep/Nyarlathotep.csproj -getItem:Compile | ConvertFrom-Json | % { $_.Items.Compile.Identity } | ? { $_ -notlike 'obj*' } | Sort-Object; $base = Get-Content docs/audits/spikes-compile-base.txt | Sort-Object; if (-not $base) { 'compile items: no base list'; exit 1 }; $d = Compare-Object $base $now; if ($d) { 'compile items: differ: ' + ($d.InputObject -join ', '); exit 1 }; 'compile items: ' + $now.Count + ' = base'" → "compile items: <n> = base" (fails when: any other file under Nyarlathotep/ differs, the diff output is empty because the base commit is wrong, a Compile item was added or removed since the step-2 parent, or the base list is missing)
- [ ] D20 · **Rollback drill passes** in a disposable worktree, reverting the whole child's range restores the pre-child tree, which builds and passes preflight; separately, removing only the kept helpers still builds. The range is two commits recorded in docs/audits/spikes.md before the drill starts: <pre-child> (the parent of the step-1 commit) and <drill base> (HEAD after step 9's pre-audit is committed) · cmd: git worktree add $env:TEMP\nyar-rollback <drill base>; in it: git revert --no-edit <pre-child>..<drill base>; dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__; pwsh tools/preflight.ps1; git diff --quiet <pre-child> HEAD -- . ":!docs/dod/README.md"; then git reset --hard <drill base>; git checkout <step-2 parent> -- Nyarlathotep/Nyarlathotep/EntityExtensions.cs; dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__; then git worktree remove --force $env:TEMP\nyar-rollback → both builds "0 Error(s)", "PREFLIGHT OK", diff exit 0 (fails when: the revert conflicts, the reverted tree does not build or fails preflight, a file other than the regenerated index differs from <pre-child>, the helpers-only tree does not build, or either recorded commit is missing). The world side needs no drill: D4 -AfterCleanup already proves the only server data is gone

## Purpose & typical use
- **Who:** the developer, once, before the foundation child: Claude builds, and the project owner plays as admin on the local server. Their sentence: "Before we build four pillars on these tricks, show me the tricks actually work in this game build."
- **Job:** turn three uncertain mechanisms into verdicts with evidence, so the later children plan on facts:
  - S1: marching NPCs to a point
  - S2: tagging spawned units so they can be found after a restart
  - S3: a timed stat buff on a native NPC

  A no-go verdict is a success here. It moves a design change to before any pillar code exists.
- **Coexists with:** the scaffolded v0.1.0 plugin (Core, EntityExtensions, RootCommands). On the local server it also runs alongside Beelzebub, Faust and Uriel (S-6). It replaces nothing and ships nothing. Every spike file is removed before the plan closes (D13, D19).

## Use cases
### Typical
The admin joins the throwaway save as admin and stands in open ground in Farbane. They run `.nyar spike march 1` and watch five bandit thugs spawn 100 m north and walk to them. Claude reads the log, and the observations and verdict go into SIEGES.md. The same happens for `.nyar spike tag 6` with a restart (S2) and `.nyar spike empower 120` on a nearby bandit camp (S3).
### Minimal stretch
The least an admin can do is run `.nyar spike sweep` on an empty save. It replies "sweep: marked 0, listed 0, faults 0" and changes nothing. A spike run once and never again leaves nothing behind: its units expire at their LifeTime of at most 600 s or are cleared (D5). The whole throwaway save is deleted at the end (D18, D4).
### Maximal stretch
The largest spike load is:
- 30 marked units alive (D5)
- three march groups
- every NPC within a 30 m radius empowered

Out-of-range requests are refused (D6). Nothing runs per frame except the S1 variant-2 anchor mover (1 Hz, one anchor per group) and the clear queue (5 per frame). An admin who scripts repeated commands is bounded by the 1 s cooldown, the 30-alive limit and the ranges.

## Business rules
1. **Limits:**
   - at most 10 units per command
   - at most 30 marked units alive at once
   - LifeTime at most 600 s
   - clear destroys at most 5 per frame (D5; the per-frame limit comes from DEV_REMINDERS #8)
   - query radius at most 30 m (D6)
   - one 1 s server-wide cooldown shared by every spike command except clear, so a scripted admin gets at most one query or spawn per second (D6)
2. **Invariants:**
   - A spike never edits an entity carrying the Prefab component (D3).
   - A spike never writes stats directly on a native NPC. S3 changes native NPCs only through a carrier buff with LifeTime, per the Epic child constraint for faction-empowerment (D6 refuses a carrier without LifeTime).
   - Every spike-spawned unit is marked and is audited by sweep (D5).
3. **Time:** LifeTime counts server seconds. What happens across a restart is itself the S2 question (D8), so no rule assumes it.
4. **Precedence, highest first** (D6):
   1. Core.IsReady=false: every spike command replies "still loading". In practice this is unreachable, because VCF accepts chat only after players connect, which is after load. The guard stays anyway.
   2. General.Enabled=false: march, tag and empower are refused with "General.Enabled is false". Clear, sweep and inspect still run, so the kill path never closes (D6).
   3. The argument ranges.
   4. The 30-alive limit.

   Only the developer decides an exception, and only through an amendment to this plan.
5. **Every X sets:**
   - "Every spike unit" is the union of two sets: the in-memory list of units spawned this boot, and an EntityQuery over Buff+SpellLevel (IncludeDisabled) whose SpellLevel.Level is 1314472274, resolved to each buff's Target. `.nyar spike sweep` reports both sets and every difference between them (D5).
   - "Every spike file" (D13, D19) is the set the compiler actually builds plus every .cs file git sees: the project's Compile items (tracked, untracked and ignored alike) and git's tracked and untracked-not-ignored .cs files. Spike code is recognised by naming (namespace Nyarlathotep.Spikes, "Spike" type and member names, command group "spike"). Two escape routes are closed separately: an ignored Compile item fails D13 whatever its name, and a neutral-named file, tracked or untracked, fails D19, because after step 8 nothing under Nyarlathotep/ but EntityExtensions.cs may differ in git and the Compile items must equal the pre-harness list (D14 plants the first two routes).
   - "Every server write" (D4) is every file created, changed or deleted between the snapshot and the comparison, under the server directory and LocalLow\Stunlock Studios. Content hashes cover BepInEx/, logs/, save-data-* and LocalLow, so a write that restores the timestamp is still seen there. A file created and deleted inside one session leaves nothing behind and is not seen; that is accepted, because what matters for the world is what remains.
6. **Corrections:** an observation found wrong after its verdict was written is corrected by:
   - appending a new dated Test results entry that says "supersedes <date>" (the earlier entry stays)
   - rewriting the verdict line
   - updating the RESEARCH_NOTES contract row
   - recording a `corrected` amendment on the Epic, or a `discovered` one when a later child's plan already relied on it, with any affected child plan amended the same day

   The latest dated entry is authoritative (D11).

## Interfaces
### Internal — reads / writes / changes (paths or symbols)
- Reads: Core.cs (`EntityManager`, `ServerGameManager`, `PrefabCollectionSystem`, `ServerTime`), Config/Settings.cs (`Enabled`), EntityExtensions.cs.
- Adds (removed at close): Nyarlathotep/Nyarlathotep/Spikes/SpikeUnits.cs (spawn, mark, list, sweep, clear queue), Spikes/SpikeMarch.cs (S1), Spikes/SpikeCarrier.cs (S3), and Commands/SpikeCommands.cs (the `.nyar spike` commands). All of it uses namespace Nyarlathotep.Spikes and "Spike" names (Business rules 5).
- Changes (kept):
  - EntityExtensions.cs gains Prefab-refusing helpers for AddComponent, RemoveComponent, AddBuffer and DestroyEntity. The foundation reuses them (D3; Epic D6).
  - tools/preflight.ps1 gains Test-CheckSpikeCode (D13) and Test-CheckServerWrites (D4). Both stay after close: the first keeps guarding against spike leftovers, and the second serves every later in-game test.
  - tools/paths-manifest.txt and tools/data-inventory.json gain the throwaway save and the external writers (D15).
- Writes to other features:
  - the verdicts in docs/features/SIEGES.md, EVENT_SPAWNS.md and FACTION_EMPOWERMENT.md
  - the contract table in docs/RESEARCH_NOTES.md (D10)
  - amendments in docs/dod/nyarlathotep.md (D11)

  A wrong verdict would make a later child build on a false mechanism. That is why each verdict carries its observations (D7–D9) and why corrections follow Business rules 6.
- Contract introduced: the marker, our own buff entity whose SpellLevel.Level is 1314472274 (S-4). The foundation's SpawnTracker inherits it only if S2 says go (D8, D10).
### External — dependencies and their failure behaviour
| Dependency | Version / contract | Slow or down | Garbage or incompatible |
|---|---|---|---|
| V Rising server, VampireReferenceAssemblies 1.1.12-r99041-b2 | components sampled: PrefabGUID, Translation, LifeTime, DestroyWhenDisabled, PersistenceV2.DontSaveEntity, Buff, SpellLevel, ModifyUnitStatBuff_DOTS, Follower, AggroConsumer, BehaviourTreeState, Health, UnitStats | n/a (in-process) | a prefab missing a component → checked with Has<T>() first, reply "prefab <name> lacks <component>", nothing applied (D6); an exception → caught, "spike failed:" reply and log line (D6, D12); a crash → restart, recorded (D12) |
| VCF 0.10.* | adminOnly, int argument parsing | n/a | a non-integer argument → VCF's own usage reply; the command body never runs |
| Local server process | started with the step-3 launch line | not running → no session; nothing is recorded as passed | the DLL is file-locked → the deploying build fails; stop the server first |
| Other plugins (Beelzebub, Faust, Uriel) | unchanged | n/a | a result that could be theirs → repeat with them in BepInEx/plugins-off (S-6) |
| tools/preflight.ps1 (own tooling) | the checks named in D2–D4, D13–D17 | n/a | missing or throwing → the check prints "<Function> threw: …" and fails. A step whose pre-audit or post-audit preflight fails does not proceed (Epic Rollout › Procedure). An empty input never passes (D14) |
| Codex CLI (reviewer) | read-only `codex exec` | timeout or error → the review is retried once, then a fresh subagent reviews, then the owner reviews (dod review.md order). A step never self-approves (D17) | a verdict produced without reading the diff is rejected and rerun with the diff pasted in |
| dod scripts | dod-index.mjs --check | Node missing → the Log says "unverified by script" and closing waits | a check problem blocks `approve` and `close` (D17) |
There is no sandbox mode: the throwaway save is the test bed, and it is deleted at close (D4, D18).

## Design
### Data
| Artifact | Where | Owner | Retention / deletion | Single copy? |
|---|---|---|---|---|
| Throwaway save and its Settings (adminlist.txt, ServerHostSettings.json) | <server>\save-data-nyarspikes\ | the developer | deleted in step 8 (D18); absence checked by D4 -AfterCleanup | yes; nothing in it matters |
| Spike units and carrier buffs | inside the throwaway save | the mod | LifeTime ≤ 600 s, clear, or deleted with the save (D5, D18) | yes |
| Spike harness source | Spikes/, Commands/SpikeCommands.cs | repo | deleted in step 8 (D13, D19); kept in git history | no (git history) |
| Verdicts, contracts, audit record | docs/features/*.md, docs/RESEARCH_NOTES.md, docs/audits/spikes.md | repo | kept in git forever | no (git history) |
| Log excerpts | quoted in docs/audits/spikes.md | repo | kept; no SteamIDs or player positions quoted; token-scanned (D16) | no |
| <server>\logs\NyarSpikes.log, BepInEx/LogOutput.log | server | server operator | NyarSpikes.log is deleted in step 8; LogOutput.log is overwritten each boot | yes |
The mod has no persisted files yet (events.json, state.json), and the spikes create none.
### States
Spike groups: Spawned → Alive → Expired (LifeTime), Cleared (clear queue) or Destroyed (DestroyWhenDisabled).
- Empty: sweep replies "marked 0, listed 0, faults 0" and clear replies "nothing to clear".
- Partial init: "still loading" (Business rules 4).
- Error: one reply and one log line (D6).
- Concurrency: VCF handlers and the coroutines (the 1 Hz march mover, the 5-per-frame clear queue) all run on the server main thread. They never run at the same instant, but a coroutine yields between frames, so commands can land between its steps:
  - Clear first stops every march coroutine, then queues the destroys.
  - A tag or march that arrives while a clear is draining is refused with "clear in progress (<k> left)", so the queue never races a new spawn (D5).
  - Empower does not touch spike units, so it is allowed during a drain.
  - Every coroutine step checks Exists() on each entity before touching it.
  - Two admins share the same 30-unit limit and the same cooldown. Their commands are serialized on the main thread, so two admins behave exactly as one admin sending the same commands in sequence; D5 stages those sequences with one admin.

  D5 exercises a second clear, a tag during a drain, and a clear during a march.
- Restart: the in-memory list is lost. Finding survivors after a restart is exactly the S2 question (D8).
- Corrections to recorded results follow Business rules 6.
### Permissions
Actors:
- the admin in game: every `.nyar spike` command is adminOnly (D2)
- players: VCF answers them with its standard no-permission reply and runs nothing
- the operator: owns the launch line and the throwaway save
- Claude: builds, starts and stops the server, reads logs
- Codex: read-only review

Spike units belong to no player. Any admin may clear any spike group.
### UX
`.nyar spike` lists its subcommands to admins. Replies are one line each, at most 480 bytes, plain text with no colour tags. Positions appear only in replies to the admin who ran the command, and nothing is announced.

Argument ranges (all arguments are integers, parsed by VCF, so no fractional, NaN or infinite value can arrive):

| Command | Argument | Range | Default |
|---|---|---|---|
| march | variant | 1–4 | — |
| march | count | 1–10 (variant 3), 1–9 (variants 1 and 2: the anchor is the tenth unit) | 5 |
| march | distance (m) | 20–200 | 100 |
| tag | count | 1–10 | — |
| tag | lifetime (s) | 30–600 | 600 |
| empower | seconds | 10–600 | — |
| empower | radius (m) | 1–30 | 10 |
| empower | carrierGuid | a PrefabGUID with Buff and LifeTime, not on the GAME_ASSETS do-not-spawn list | -1591883586 |
| inspect | radius (m) | 1–30 | 10 |
| sweep, clear | — | — | — |

Accessibility: the chat window, its font, contrast, keyboard input and scaling belong to the game client. The game offers no screen-reader support, and that platform limitation is accepted for a developer-only tool. The mod's own lever is the one-line, plain-text, colour-free reply format above.

## Security
- Authorization: every spike command is adminOnly, checked repository-wide (D2). Nothing is triggered by a hook, a schedule or another mod.
- Input: integers only, range-checked, and the carrier GUID must resolve to a safe buff prefab (D6). No input reaches a file, shell, URL or query.
- Secrets: none introduced; the scan covers the new files (D16).
- Personal data: the throwaway save holds the admin's SteamID in adminlist.txt and is deleted at close (D18, D4). Audit excerpts quote no SteamIDs.

## Failure & observability
| Failure class | What the admin sees | What they do next |
|---|---|---|
| Argument out of range, unknown or unsafe carrier | one reply naming the range or the reason | fix the argument |
| Exception inside a spike | "spike failed: <type>: <message>" | Claude reads the log; the spike is fixed and redeployed with the server stopped |
| Server crash | the process exits | restart; record it in the audit record and the verdict (D12) |
| Unit left behind or wrongly tagged | sweep shows a fault or a non-zero count | `.nyar spike clear`; at worst the save is deleted (D18) |
- Logs: every spike line is prefixed "[nyar-spike]". Each command logs "<name> <args> → <result>". Every spawned or buffed entity is logged with its index and PrefabGUID. That is enough to rebuild a verdict from the log without redoing the session.
- The per-session log check (D12) and sweep (D5) are how breakage is noticed. There is no dashboard; this is a one-off experiment.
- Checks this plan introduces, with their failing cases:

| Check | Fails on | Silent on | Empty input |
|---|---|---|---|
| Test-CheckSpikeCode (D13) | spike identifiers, namespace or command in any .cs file while this plan is not in-progress; an ignored Compile item | no spike code; spike code while in-progress (the allowed line) | no .cs files → "spike code: no source found", failure |
| Test-CheckCommands (D2, Epic check) | a [Command] outside Commands/ or without adminOnly: true, outside the allow-list | only admin-only and allow-listed commands | no source → failure |
| Test-CheckStructuralEdits (D3, Epic check) | a structural call outside EntityExtensions.cs, or one there without a Prefab guard | calls only in guarded helpers | no source → failure |
| Test-CheckPaths (D15, Epic check) | a walked path matching no manifest glob of its kind | every path manifested | no manifest or an empty walk → failure |
| Test-CheckSecrets (D16, Epic check) | a token form in any scanned file, or a .cs file reading the environment | no token | no files → failure |
| D19 diff | any file under Nyarlathotep/ other than EntityExtensions.cs | exactly EntityExtensions.cs | empty output → failure (wrong base commit) |
| D20 rollback drill | a conflicting revert, a failing build or preflight, a differing file | an empty diff against the pre-child commit | no recorded range → failure |
| Test-CheckServerWrites (D4) | a created, changed or deleted file outside the server/external globs; another Saves folder present or changed; with -AfterCleanup, the save folder still present | changes only under save-data-nyarspikes and external globs | no snapshot, an empty snapshot, or no server directory → failure |
| Test-CheckAuditSteps (D17) | a Build plan step without a pre-audit, a post-audit or a Codex verdict | a complete 1–9 matrix | no audit file, or a plan with no steps → failure |
| D12 log check | a "   at Nyarlathotep." frame | "[nyar-spike] spike failed:" lines | no log, or no spike lines → failure |
| sweep audit (D5) | a unit missing the marker, LifeTime (or LifeTime > 600) or DestroyWhenDisabled, or in only one of the two sets | all four present in both sets | nothing spawned → "marked 0, listed 0, faults 0", which is correct for an empty world and is only accepted after a clear |
- Fixtures for the preflight checks are copies of the real tree with one planted change. The plan fixture is a copy of this plan with only its status changed. The server-writes fixtures are captured snapshot pairs, and the spike-code fixtures carry captured compile-items.txt and git listings, each in the exact format the real collection prints (D14; Epic D10 runs them all). The manual verdict items D5–D11 are human checklists, not checks, and have no fixtures.

## Performance
- Budget: spike commands run once per command. The recurring work is only the variant-2 mover (1 Hz, at most 3 anchors) and the clear queue (at most 5 destroys per frame), far below the Epic's 1 ms idle tick. The costliest command is empower/inspect, one EntityQuery bounded by a 30 m radius, at most once per second under the shared cooldown.
- Limits: 10 per command and 30 alive (D5, D6). They come from the smallest group that answers each question: 5 for a march, 6 for a restart split between saved and DontSaveEntity units. The valid case they exclude is a large-horde march, which the sieges child measures at its own caps.

## Build plan
Every step runs inside the Epic's Rollout › Procedure:
- a pre-audit before and a post-audit after, each written as "### Step <n>" under "## Pre-audit" and "## Post-audit" in docs/audits/spikes.md
- a fresh read-only Codex cross-inspection of the step's diff, with its "Codex verdict:" line

A server-side step stops the server before the deploying build.

1. Precondition: Epic Build step 1 is done, meaning tools/preflight.ps1 has the Epic checks, -SelfTest and -Paths.
   - Add Test-CheckSpikeCode (D13; it reads `dotnet msbuild ... -getItem:Compile` and git's listings, or their captured files in fixture mode), Test-CheckServerWrites with the -ServerWrites -Snapshot <file> and -Compare <file> [-AfterCleanup] modes (D4), and Test-CheckAuditSteps with the -AuditOf <slug> mode (D17). Give each a manifest entry and good, bad and empty fixtures under tools/preflight-fixtures/<Name>/ (D14).
   - Add to tools/paths-manifest.txt: `server: save-data-nyarspikes/**`, `server: logs/NyarSpikes.log`, and `external:` globs for the files the server and the other plugins write (BepInEx/LogOutput.log, BepInEx/ErrorLog.log, BepInEx/config/*.cfg, BepInEx/config/Faust/**, BepInEx/config/Uriel/**, BepInEx/config/kdpen.Beelzebub/**, logs/*.txt, BepInEx/cache/**, BepInEx/interop/**).
   - Add a tools/data-inventory.json entry for each new server and external glob.

   · satisfies D13, D14, D15, D17
2. Harness:
   - Add EntityExtensions.cs helpers AddComponentSafe<T>, RemoveComponentSafe<T>, AddBufferSafe<T> and DestroySafe. Each refuses (returns false and logs) when the entity is missing or has Prefab.
   - Add Spikes/SpikeUnits.cs. It spawns with `ServerGameManager.InstantiateEntityImmediate` (DEV_REMINDERS #13) and, in the same frame, sets:
     - Translation to the target position
     - LifeTime {Duration ≤ 600, EndAction Destroy}
     - DestroyWhenDisabled
     - the marker buff (SpellLevel.Level 1314472274, effects stripped, following the Bloodcraft recipe in RESEARCH_NOTES §Tagging)
     - no drops (DropTableBuffer cleared)

     It also keeps the in-memory list, enforces the limits, runs the sweep audit and the clear queue, and refuses spawns while a clear drains.
   - Add Commands/SpikeCommands.cs with the six subcommands and the argument ranges in Design › UX. Each one checks the precedence in Business rules 4 and catches its own exceptions, logging a single "[nyar-spike] spike failed:" line.
   - Record the step-2 parent commit in docs/audits/spikes.md for D19, and before adding any file capture the Compile items to docs/audits/spikes-compile-base.txt (`dotnet msbuild Nyarlathotep/Nyarlathotep/Nyarlathotep.csproj -getItem:Compile`, Identity values outside obj/, one per line). Then build with the D1 command.

   · satisfies D1, D2, D3, D16
3. Throwaway save:
   1. With the server stopped, take the D4 snapshot (`pwsh tools/preflight.ps1 -ServerWrites -Snapshot $env:TEMP\nyarspikes-before.tsv`) and record its time in docs/audits/spikes.md.
   2. Create <server>\save-data-nyarspikes\Settings\adminlist.txt containing the owner's SteamID (the owner supplies it at this step).
   3. Deploy the step-2 build (`dotnet build Nyarlathotep/Nyarlathotep.sln -c Release`).
   4. Launch from the server directory: `VRisingServer.exe -persistentDataPath .\save-data-nyarspikes -serverName "Nyar Spikes" -saveName nyarspikes -logFile .\logs\NyarSpikes.log`.
   5. Check the boot log for "Nyarlathotep initialized".
   6. Run the D5 and D6 sessions, then the D12 log check and the D4 -Compare check.

   · satisfies D5, D6, D12
4. S1 march (Spikes/SpikeMarch.cs). `.nyar spike march <v> [count] [distance]` spawns `count` CHAR_Bandit_Thug (-301730941, S-5) `distance` m north of the admin. The destination is the admin's position when the command runs.
   - Variant 1: an anchor entity at the destination, with Follower.Followed set to the anchor on each unit.
   - Variant 2: the same anchor, moved 10 m per second along the straight line from spawn to destination by a 1 Hz coroutine.
   - Variant 3: AggroConsumer.PreCombatPosition set to the destination, with raised MaxDistanceFromPreCombatPosition and ProximityRadius, and BehaviourTreeState overridden from Return.
   - Variant 4 (A5): an aggro chase onto the admin. AggroConsumer.MaxDistanceFromPreCombatPosition and ProximityRadius and AggroModifiers.CircleRadiusFactor and ConeRadiusFactor are set to distance + 50 (TideOfWar SpawnForWar/Core.cs:389-412), AggroConsumer.Active is set, and the admin is added to the unit's AggroBuffer with DamageValue 500 (Bloodcraft Utilities/Familiars.cs:826-859).

   The build picks the anchor entity and records it in the D7 entry and D10. Run the variants in order in open ground, then run the best one with a player-built wall across the path. Record the D7 checklist in docs/features/SIEGES.md › Test results. Then run the D12 log check. · satisfies D7
5. S2 restart. Steps, with `.nyar spike sweep` after each action:
   1. `.nyar spike tag 6 600`. Even-numbered units also get PersistenceV2.DontSaveEntity through AddComponentSafe.
   2. Stop the server gracefully (Ctrl-C, so it saves).
   3. Start it with the step-3 line.
   4. Wait out the remaining LifeTime.
   5. `.nyar spike tag 4 600`, walk 200 m away for 120 s, and return.

   Sweep groups the marked units by DontSaveEntity and shows each one's remaining LifeTime. Record the D8 checklist in docs/features/EVENT_SPAWNS.md › Test results. Then run the D12 log check. · satisfies D8
6. S3 carrier (Spikes/SpikeCarrier.cs). `.nyar spike empower <seconds> [radius] [carrierGuid]` applies the carrier to every native NPC within the radius.
   - Native NPC means: not marked, not in Faction_Players*, no VBloodUnit, not a Prefab.
   - The carrier is applied with the Immediate buff API (DEV_REMINDERS #17, #19) and BuffType Replace, with LifeTime set to `seconds`.
   - Its ModifyUnitStatBuff_DOTS buffer is cleared, then given PhysicalPower ×1.5 and MaxHealth ×2.

   `.nyar spike inspect [radius]` prints the nearest native NPC's PhysicalPower, MaxHealth, Health.Value, whether the carrier is present, and its remaining time.
   Session: inspect, then `empower 120`, then inspect; fight the NPC and compare damage; wait for expiry and inspect. Then `empower 300`, move 150 m away for 60 s, return and inspect. Then `empower 600`, restart, and inspect. Record the D9 checklist in docs/features/FACTION_EMPOWERMENT.md › Test results, then run the D12 log check. · satisfies D9, D12
7. Contracts and propagation:
   - Write docs/RESEARCH_NOTES.md › "## Spike contracts (VampireReferenceAssemblies 1.1.12)" from the component and system types in the spike files (D10).
   - Update each feature doc's Status line.
   - For each no-go or changed mechanism, add an amendment to docs/dod/nyarlathotep.md.
   - Regenerate the dod index.

   · satisfies D10, D11
8. Removal:
   1. Delete Nyarlathotep/Nyarlathotep/Spikes/ and Commands/SpikeCommands.cs. The EntityExtensions helpers stay.
   2. Build with the D1 command; preflight must print "spike code: none" and "secrets: none"; run the D19 diff.
   3. With the server stopped, deploy, boot with the step-3 line, and check that `.nyar spike` is unknown.
   4. Stop the server, delete save-data-nyarspikes and logs\NyarSpikes.log, and run the D4 command with -AfterCleanup.
   5. Run `pwsh tools/preflight.ps1 -Paths`.

   · satisfies D13, D15, D18, D19
9. Close:
   1. Write this step's pre-audit.
   2. Run `dod status spikes`.
   3. Obtain the Codex cross-inspection of the whole child's diff and write this step's post-audit with its Codex verdict line.
   4. Commit the pre-audit, record <pre-child> and <drill base> in docs/audits/spikes.md, then run the D20 rollback drill and record its output in the post-audit.
   5. Only then run the D17 command, and after it `dod close spikes`.

   · satisfies D17, D20

## Rollout
- **Ships:** nothing. The harness exists only between step 2 and step 8 and is never released: no version bump, no tag, no changelog entry. A release cut mid-spikes would fail preflight, because Test-CheckSpikeCode fails whenever this plan is not in-progress (D13).
- **Who turns it off:** any admin with `.nyar spike clear`; the developer by stopping the server or setting General.Enabled=false (clear still runs); step 8 removes it for good.
- **Compatibility:** no cfg key, file format or existing reply changes. The only code kept is the EntityExtensions helpers, which add methods and change none (D1, D19).
- **Rollback:**
  - The world side: the throwaway save is deleted (D18), and no other save is touched (D4).
  - The code side: step 8 is the rollback, and D19 proves the code under Nyarlathotep/ returned to the pre-harness commit apart from the kept helpers.
  - To undo the whole child (tooling and docs included): `git revert --no-edit <step-1 commit>^..<step-9 commit>`. The range is recorded in docs/audits/spikes.md, and the dod index is regenerated afterwards. D20 rehearses exactly this in a disposable worktree before close. The revert is safe after data was written, because the only data (the throwaway save) is already gone.
  - To undo only the kept helpers: revert the EntityExtensions.cs hunk of the step-2 commit, provided no later child uses them.
- **Paths walked**, one step at a time:
  - Step 1: tools/preflight.ps1, tools/preflight-checks.json, tools/preflight-fixtures/{SpikeCode,ServerWrites,AuditSteps}/**, tools/paths-manifest.txt, tools/data-inventory.json.
  - Step 2: Nyarlathotep/Nyarlathotep/EntityExtensions.cs, Spikes/*.cs, Commands/SpikeCommands.cs, bin/ and obj/ (ignored), dist/ (ignored).
  - Step 3: <server>\save-data-nyarspikes\**, <server>\logs\NyarSpikes.log, <server>\BepInEx\plugins\Nyarlathotep.dll, and the external writes (LogOutput.log, the plugin configs, logs/*.txt).
  - Steps 4–6: docs/features/SIEGES.md, EVENT_SPAWNS.md, FACTION_EMPOWERMENT.md.
  - Step 7: docs/RESEARCH_NOTES.md, docs/dod/nyarlathotep.md, docs/dod/README.md (generated).
  - Every step: docs/audits/spikes.md (step 2 also writes docs/audits/spikes-compile-base.txt).
  - Steps 3 and 9: $env:TEMP\nyarspikes-before.tsv (the D4 snapshot, outside the repository) and $env:TEMP\nyar-rollback (the D20 worktree, removed by the drill).
  - Review process: docs/dod/spikes.md, docs/dod/spikes.reviews.md and docs/dod/spikes.review.html.

  The repository and server-plugin side is checked by D15. Everything under the server directory and LocalLow is checked by the before/after listing in D4.

## Out of scope
- Everything the foundation builds: SpawnTracker, Persistence, the boot sweep as a hook, EventScheduler, the TriggerBus, and caps from cfg. The spikes use fixed limits and manual commands instead.
- A fault-injection harness for the developer tooling (preflight, Codex, the dod scripts). Each collaborator's failure already blocks the step it gates (Interfaces › External), and each preflight check's empty fixture proves a missing input fails (D14).
- A system-wide file-write tracer. D4 covers the server directory and LocalLow, D15 every repository path, and nothing in this child writes elsewhere except the two $env:TEMP paths in Rollout › Paths walked.
- An automated precedence test. The spike gate is four rules, deleted at close, and exercised pairwise by D6. The permanent resolver and its test are the foundation's (Epic D27).
- Structure damage, HookDOTS and siege eligibility, which belong to the Epic's `structure-damage` plan and the sieges child. Map markers (`map-markers`).
- Tuning the stat numbers: ×1.5 and ×2 are probes, not design values. Horde-scale marching (the sieges child, at its own caps).

## Also considered
- Compliance: the throwaway save holds only the developer's own test world; nothing is published.
- Localisation: developer-only English replies, and the harness is deleted.
- Running cost: none.
- Operational ownership: the developer, for the duration of the spikes.
- Documentation and changelog: verdicts in the feature docs and RESEARCH_NOTES; no changelog entry, since nothing ships.
- Analytics: none.
- Decommissioning: step 8 decommissions the harness and the save.
- Support tooling: `.nyar spike sweep` and `.nyar spike inspect`, for the spikes only.

## Assumptions
- S-1 · validated · The local dedicated server at C:\Program Files (x86)\Steam\steamapps\common\VRisingDedicatedServer has BepInEx with VCF 0.10.4, Beelzebub, Faust and Uriel installed · source: BepInEx/plugins listing and LogOutput.log 2026-09-23
- S-2 · validated · No existing save exists under the server directory or %USERPROFILE%\AppData\LocalLow\Stunlock Studios\VRisingServer, so a dedicated -persistentDataPath touches no one's world; the owner's own test world (world1, launched by C:\VRising-LocalServer\start_server_local.bat with -persistentDataPath C:\VRising-LocalServer) lives outside the server directory and is covered by D4 since A4 · source: directory listing 2026-09-23
- S-3 · reversible · Spikes run on a throwaway save (save-data-nyarspikes) launched by the step-3 line, never on a real world · fallback: to use another world, copy its folder aside before step 3 and restore it in step 8, and add its path to the D4 -Since check as an allowed save
- S-4 · reversible · The marker value is SpellLevel.Level 1314472274 (ASCII "NYAR"), distinct from Bloodcraft's 731002 · fallback: one constant in Spikes/SpikeUnits.cs; change it before the foundation copies it
- S-5 · reversible · The test unit is CHAR_Bandit_Thug -301730941, which is common in Farbane and is the anatomy reference in docs/GAME_ASSETS.md · fallback: swap the constant for another CHAR_* from Reference Data/unit_index.tsv and repeat
- S-6 · reversible · Beelzebub, Faust and Uriel stay installed during the spikes because that is the real server set-up · fallback: move them to BepInEx/plugins-off and repeat any spike whose result could be theirs
- S-7 · reversible · Epic Build step 1 (the preflight checks) is done before this plan's step 1 · fallback: if it is not, this plan waits; the D2, D3, D15 and D16 evidence needs those checks, and nothing here is built on temporary greps
- S-8 · reversible · The owner joins the game as admin for the in-game parts of steps 3–6 and 8; Claude builds, starts and stops the server, and reads the logs · fallback: if the owner can't join, the steps wait. No in-game result is recorded without a player present

## Coverage
| # | Layer | Status | Probes | Pointer / reason |
|---|---|---|---|---|
| 1 | Purpose & typical use | Considered | 3/3 | Purpose & typical use |
| 2 | Actors & permissions | Considered | 3/3 | Design › Permissions › 2.1 D2 D6; 2.2 prose: VCF answers a non-admin with its standard no-permission reply and runs nothing; the foundation child tests it for every command; 2.3 prose: spike units belong to no player and any admin may clear any spike group |
| 3 | Inputs, outputs & data | Considered | 4/4 | Design › Data › 3.1 D6; 3.2 D5 D12; 3.3 D4 D18 D5; 3.4 prose: no persisted schema changes; the mod has no data files yet and the spikes create none |
| 4 | Business rules & invariants | Considered | 5/5 | Business rules › 4.1 D5 D6; 4.2 D3 D5; 4.3 D5 D8; 4.4 D6; 4.5 D13 D14 D19 D5 D4 |
| 5 | Internal interfaces | Considered | 3/3 | Interfaces › 5.1 D1; 5.2 D11 D3; 5.3 D10 D8 |
| 6 | External dependencies & contracts | Considered | 3/3 | Interfaces › 6.1 D10 D1; 6.2 D6 D12 D14; 6.3 D4 D18 |
| 7 | States & lifecycle | Considered | 3/3 | Design › States › 7.1 D5 D6; 7.2 D5 D6; 7.3 D8 D11 D18 |
| 8 | Minimal stretch | Considered | 2/2 | Use cases › Minimal stretch › 8.1 D5; 8.2 D5 D18 |
| 9 | Maximal stretch | Considered | 3/3 | Use cases › Maximal stretch › 9.1 D5; 9.2 D2 D6 D5; 9.3 D5 |
| 10 | Security & privacy | Considered | 4/4 | Security › 10.1 D2; 10.2 D6; 10.3 D16; 10.4 D18 D4 |
| 11 | Design & UX | Considered | 4/4 | Design › UX › 11.1 D2; 11.2 D6 D5; 11.3 prose: chat is the game client's; one-line colour-free replies; no screen reader in the game, accepted for a developer tool; 11.4 prose: runs only when an admin types a spike command; no hook, schedule or trigger |
| 12 | Failure handling & observability | Considered | 4/4 | Failure & observability › 12.1 D6; 12.2 D12 D10; 12.3 D12 D5; 12.4 D14 D13 D4 D17 D19 D20 |
| 13 | Performance & scale | Considered | 2/2 | Performance › 13.1 prose: one-shot commands, a 1 Hz mover and a 5-per-frame queue, far under the 1 ms idle budget; 13.2 D5 D6 |
| 14 | Rollout & compatibility | Considered | 4/4 | Rollout › 14.1 D13 D2; 14.2 D1 D19; 14.3 D20 D19 D18 D13; 14.4 D15 D4 D17 D20 |
| 15 | Out of scope | Considered | 2/2 | Out of scope |
Gate — acceptance & testability: passed — every Considered layer 2–14 maps to ≥ 1 D-item

## Baseline
- [ ] D1 · **Compiles with spike code** the solution builds in Release with 0 errors and 0 warnings with the spike harness present, without deploying · cmd: dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__ → "0 Warning(s)" and "0 Error(s)" (fails when: a spike file has a compile error or adds a warning)
- [ ] D2 · **Every command admin-only** every [Command] attribute in any .cs file of the repository (git ls-files plus untracked-not-ignored), spike commands included, lives under Commands/ and carries adminOnly: true. The only exceptions are the allow-list {nyar, status, help}, which is versioned as $script:PublicCommands in tools/preflight.ps1 · cmd: pwsh tools/preflight.ps1 → line "commands: <n> admin-only, <m> public (allow-listed)" (fails when: any command in any file outside the allow-list lacks adminOnly: true, or a [Command] lies outside Commands/)
- [ ] D3 · **Spike edits fenced** spike code makes structural edits (AddComponent*, RemoveComponent*, AddBuffer, DestroyEntity) only through EntityExtensions.cs helpers. Every method there that makes such a call refuses entities carrying the Prefab component · cmd: pwsh tools/preflight.ps1 → line "structural edits: fenced (<k> Prefab-guarded calls in EntityExtensions.cs)" (fails when: any other .cs file makes one of those calls, or an EntityExtensions method makes one without a Has<Prefab> guard)
- [ ] D4 · **Only the throwaway save written** a snapshot taken before step 3 (`pwsh tools/preflight.ps1 -ServerWrites -Snapshot $env:TEMP\nyarspikes-before.tsv`: path, size, last-write time, and SHA-256 for every file under the server directory's BepInEx/, logs/ and save-data-*/ and under %USERPROFILE%\AppData\LocalLow\Stunlock Studios; path, size and time elsewhere under the server directory) is compared with the tree after each session. Every created, changed or deleted file matches a `server:` or `external:` glob in tools/paths-manifest.txt. No Saves folder other than save-data-nyarspikes exists or changed. After step 8, save-data-nyarspikes no longer exists · cmd: pwsh tools/preflight.ps1 -ServerWrites -Compare $env:TEMP\nyarspikes-before.tsv → line "server writes: <c> created, <m> changed, <d> deleted, all in manifest, no other save" (fails when: a created, changed or deleted file matches no server or external glob, another Saves folder exists or changed, the snapshot is missing or empty, or, with -AfterCleanup, save-data-nyarspikes still exists)
- [ ] D5 · **Spike units audited** `.nyar spike sweep` lists every unit spawned this boot and every marked entity (SpellLevel.Level 1314472274). For each it checks four things: the marker is present, LifeTime is present and at most 600 s, DestroyWhenDisabled is present, and the unit appears in both sets. It replies "sweep: marked <a>, listed <b>, faults 0" or names each fault. At most 30 marked units are alive at once. `.nyar spike clear` queues every unit and destroys at most 5 per frame. While the queue drains, a second clear replies "clear in progress (<k> left)"; once drained it replies "nothing to clear" · manual: in the throwaway save, run `.nyar spike tag 10` three times, then a fourth time (reply "spike limit 30 (30 alive)"); `.nyar spike sweep` shows faults 0; run `.nyar spike clear` twice quickly (second reply "clear in progress"); while it drains run `.nyar spike tag 1` (reply "clear in progress (<k> left)", nothing spawned); after 2 s run clear again (reply "nothing to clear"), and `.nyar spike sweep` shows "marked 0, listed 0, faults 0"; then run `.nyar spike march 2` and `.nyar spike clear` before the group arrives (the log shows the mover stopped before the first destroy, and sweep ends at 0); the log shows clear batches of at most 5
- [ ] D6 · **Arguments and gates** every argument in Design › UX › Argument ranges is range-checked, refused with "<arg> must be <min>-<max>", and changes nothing. A carrier GUID that does not resolve to a prefab with Buff and LifeTime, or is on the GAME_ASSETS do-not-spawn list, is refused. Every spike command except clear shares one 1 s server-wide cooldown and replies "spike cooldown (<ms> ms)" inside it. Refusals follow the precedence in Business rules 4. Each command catches its own exceptions, replies "spike failed: <type>: <message>", and logs one line with no stack. The server keeps ticking · manual: for each argument in the table, run one value below its minimum and one above its maximum (every reply names the range); run `.nyar spike empower 30 10 1227555070` (refused if Buff_InCombat_Npc_Elite lacks LifeTime, else applied and recorded) and `.nyar spike empower 30 10 12345` (refused: unknown prefab). Then run the precedence pairs: with 30 alive run `.nyar spike tag 0` (argument message wins over the limit); set General.Enabled=false, restart, and run `.nyar spike tag 0` (Enabled message wins over the argument), then `.nyar spike clear`, `.nyar spike sweep` and `.nyar spike inspect` (all run). Run `.nyar spike inspect` twice within 1 s (second reply "spike cooldown (<ms> ms)"), then `.nyar spike clear` twice within 1 s (both run: clear has no cooldown). Record each reply in docs/audits/spikes.md
- [ ] D7 · **S1 march verdict** docs/features/SIEGES.md › Test results has a dated S1 entry, readable as a checklist: for each of variants 1, 2, 3 and the wall run: arrived (yes/no, seconds taken), path taken, stuck (where), engaged on arrival (yes/no), behaviour at the wall; the anchor entity used, with its PrefabGUID; a verdict line "S1 verdict: go — <mechanism>" or "S1 verdict: no-go — <reason>" · manual: read the entry; every field above is present for every run, or the entry says why a run was impossible
- [ ] D8 · **S2 restart verdict** docs/features/EVENT_SPAWNS.md › Test results has a dated S2 entry, readable as a checklist: sweep output before the restart, after it, after the LifeTime ran out, and after the 200 m walk-away; marker survives restart (yes/no); DontSaveEntity units present after restart (count); LifeTime after restart (continued, reset or gone); DestroyWhenDisabled removed units once no player was near (yes/no); load errors (quoted, or none); a verdict line "S2 verdict: go — <tagging and restart recipe>" or "S2 verdict: no-go — <reason>" · manual: read the entry; every field above is present
- [ ] D9 · **S3 carrier verdict** docs/features/FACTION_EMPOWERMENT.md › Test results has a dated S3 entry, readable as a checklist: `.nyar spike inspect` output before, during and after the buff; damage dealt and taken with and without the buff, in combat; Health.Value behaviour when the buff applies; stats back to base after expiry (yes/no); buff present after streaming out to 150 m for 60 s and back (yes/no); state after a restart mid-buff; the carrier prefab and GUID, with LifeTime confirmed; a verdict line "S3 verdict: go — <carrier recipe>" or "S3 verdict: no-go — <reason>" · manual: read the entry; every field above is present
- [ ] D10 · **Contracts recorded** docs/RESEARCH_NOTES.md › "## Spike contracts (VampireReferenceAssemblies 1.1.12)" has one row per component and system the spike code read or wrote. Each row gives the fields touched, the prefab GUIDs, the source the recipe came from (path and line), and observed = documented or observed ≠ documented, with a note · manual: list every component and system type named in Spikes/*.cs and Commands/SpikeCommands.cs (before step 8 deletes them); each has a row with all four columns filled
- [ ] D11 · **Verdicts reach the Epic** every no-go verdict, and every go verdict that changes a later child's mechanism, has an amendment in docs/dod/nyarlathotep.md dated before this plan closes. Each of the three feature docs' Status lines names its spike's result. A verdict corrected after it was written follows Business rules 6 · manual: read the three verdict lines, the three Status lines and the Epic's ## Amendments; each no-go or changed mechanism has an amendment that names it, and each correction has its superseding entry
- [ ] D12 · **Spike sessions clean** no spike session crashes the server. No session's BepInEx/LogOutput.log contains a stack frame from our assembly ("   at Nyarlathotep."). Caught spike failures are logged as single "[nyar-spike] spike failed:" lines without a stack, so any such frame means an unhandled exception · cmd: pwsh -NoProfile -Command "$l='C:\Program Files (x86)\Steam\steamapps\common\VRisingDedicatedServer\BepInEx\LogOutput.log'; if (-not (Test-Path $l)) { 'log check: no log'; exit 1 }; $n=(Select-String -Path $l -SimpleMatch '   at Nyarlathotep.').Count; $s=(Select-String -Path $l -SimpleMatch '[nyar-spike]').Count; if ($s -eq 0) { 'log check: no spike lines (wrong log?)'; exit 1 }; \"log check: $n unhandled, $s spike lines\"; exit [int]($n -gt 0)" → "log check: 0 unhandled, <s> spike lines", run after each session and recorded in docs/audits/spikes.md (fails when: an unhandled exception left a Nyarlathotep stack frame, the log is missing, or the log has no spike lines)
- [ ] D13 · **Spike code removed** no .cs file in the repository (git ls-files plus untracked-not-ignored, plus every Compile item of `dotnet msbuild Nyarlathotep/Nyarlathotep/Nyarlathotep.csproj -getItem:Compile`, which includes ignored files) declares or uses an identifier beginning with "Spike", declares namespace Nyarlathotep.Spikes, or registers a command group or command named "spike". Every Compile item outside obj/ must also be a file git sees, so an ignored source file fails on its own whatever its name; a neutral-named tracked file is caught by D19. The check is Test-CheckSpikeCode in tools/preflight.ps1. While docs/dod/spikes.md has status in-progress, spike code prints "spike code: present, allowed while spikes is in-progress" and passes. Otherwise spike code prints "spike code: present in <files>" and fails. No spike code prints "spike code: none" · cmd: pwsh tools/preflight.ps1 → line "spike code: none" (fails when: any spike identifier, namespace or command remains in any .cs file, a Compile item outside obj/ is ignored by git, or spike code exists while docs/dod/spikes.md is not in-progress)
- [ ] D14 · **New checks self-tested** tools/preflight-checks.json has entries for Test-CheckSpikeCode, Test-CheckServerWrites and Test-CheckAuditSteps, each with good, bad and empty fixtures, alongside the Epic's entries for the checks D2, D3, D15 and D16 reuse. Test-CheckSpikeCode has two bad fixtures: a class named SpikeHelper in Services/ with the plan status done, and a neutral-named Services/Helper.cs listed in the captured compile-items.txt but absent from the captured git listing. Test-CheckServerWrites' bad fixture is a before/after snapshot pair where a file under another Saves folder was deleted. Test-CheckAuditSteps' bad fixture drops step 5's Codex verdict line. All fail on empty input · cmd: pwsh tools/preflight.ps1 -SelfTest → "selftest: <n>/<n> checks, 3 fixtures each" (fails when: any listed check passes a bad or empty fixture or fails its good one, or a Test-Check function has no manifest entry)
- [ ] D15 · **Spike paths manifested** after a deploying spike build and one spike session, the path walk finds no path outside tools/paths-manifest.txt. The walk covers every tracked, untracked and ignored path in the repository, plus the server's BepInEx/plugins/Nyarlathotep*, BepInEx/config/*Nyarlathotep*, BepInEx/config/Nyarlathotep/** and save-data-nyar* · cmd: pwsh tools/preflight.ps1 -Paths → line "paths: <n> walked, all in manifest" (fails when: a walked path matches no manifest glob of its kind)
- [ ] D16 · **No secrets in spike work** the secrets scan stays clean with the spike harness, the audit record and the quoted log excerpts present · cmd: pwsh tools/preflight.ps1 → line "secrets: none (<n> files scanned)" (fails when: a spike file, audit record or log excerpt contains a token form, or a .cs file reads the environment)
- [ ] D17 · **Audit matrix complete** docs/audits/spikes.md has a "### Step <n>" entry under a "## Pre-audit" heading and under a "## Post-audit" heading for every Build plan step 1–9 of this plan, and each post-audit entry has a "Codex verdict:" line · cmd: pwsh tools/preflight.ps1 -AuditOf spikes → line "audit steps: spikes 9/9 pre, 9/9 post, 9/9 Codex verdicts" (fails when: any step lacks a pre-audit entry, a post-audit entry or a Codex verdict line, or the plan's Build plan has no steps)
- [ ] D18 · **Throwaway world removed** after D13, the build without spike code is deployed and boots with "Nyarlathotep initialized", `.nyar spike` is unknown to VCF, and save-data-nyarspikes is deleted · manual: stop the server, deploy the D13 build, start it with the step-3 launch line, read the boot log, type `.nyar spike sweep` in game (VCF replies unknown command), stop the server, delete save-data-nyarspikes, then run the D4 command with -AfterCleanup; record it all in docs/audits/spikes.md
- [ ] D19 · **Code back to base** after step 8, the only file under Nyarlathotep/ that differs from the pre-harness commit (the step-2 parent, recorded in docs/audits/spikes.md) is Nyarlathotep/Nyarlathotep/EntityExtensions.cs, which keeps the Prefab-guarded helpers, and the project's Compile items (tracked, untracked or ignored, whatever their names) are exactly the list captured at the step-2 parent · cmd: git diff --name-only <step-2 parent> HEAD -- Nyarlathotep/ → exactly "Nyarlathotep/Nyarlathotep/EntityExtensions.cs"; then pwsh -NoProfile -Command "$now = dotnet msbuild Nyarlathotep/Nyarlathotep/Nyarlathotep.csproj -getItem:Compile | ConvertFrom-Json | % { $_.Items.Compile.Identity } | ? { $_ -notlike 'obj*' } | Sort-Object; $base = Get-Content docs/audits/spikes-compile-base.txt | Sort-Object; if (-not $base) { 'compile items: no base list'; exit 1 }; $d = Compare-Object $base $now; if ($d) { 'compile items: differ: ' + ($d.InputObject -join ', '); exit 1 }; 'compile items: ' + $now.Count + ' = base'" → "compile items: <n> = base" (fails when: any other file under Nyarlathotep/ differs, the diff output is empty because the base commit is wrong, a Compile item was added or removed since the step-2 parent, or the base list is missing)
- [ ] D20 · **Rollback drill passes** in a disposable worktree, reverting the whole child's range restores the pre-child tree, which builds and passes preflight; separately, removing only the kept helpers still builds. The range is two commits recorded in docs/audits/spikes.md before the drill starts: <pre-child> (the parent of the step-1 commit) and <drill base> (HEAD after step 9's pre-audit is committed) · cmd: git worktree add $env:TEMP\nyar-rollback <drill base>; in it: git revert --no-edit <pre-child>..<drill base>; dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__; pwsh tools/preflight.ps1; git diff --quiet <pre-child> HEAD -- . ":!docs/dod/README.md"; then git reset --hard <drill base>; git checkout <step-2 parent> -- Nyarlathotep/Nyarlathotep/EntityExtensions.cs; dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__; then git worktree remove --force $env:TEMP\nyar-rollback → both builds "0 Error(s)", "PREFLIGHT OK", diff exit 0 (fails when: the revert conflicts, the reverted tree does not build or fails preflight, a file other than the regenerated index differs from <pre-child>, the helpers-only tree does not build, or either recorded commit is missing). The world side needs no drill: D4 -AfterCleanup already proves the only server data is gone

## Amendments
- A1 · 2026-09-23 · external · ~D2 · layer: — · the parent Epic amendment A4 (requested) widened the player allow-list to add me, top, hide, show and version; D2 now names the set that $script:PublicCommands in tools/preflight.ps1 already holds (commit 0190e38)
- A2 · 2026-09-23 · external · ~D2 · layer: — · the parent Epic amendment A13 (requested) adds `sub` to the allow-list for the Raphael push subscription; D2 names it and $script:PublicCommands holds it
- A3 · 2026-09-23 · discovered · ~D6 · layer: 4.1 · the march anchor (variants 1 and 2) is itself a spawned spike unit, so count 10 made 11 units in one command against the 10-per-command limit; march count is now 1–9 for variants 1 and 2 and D6 checks that range (Codex cross-inspection, step 2 post-audit)
- A4 · 2026-09-23 · discovered · ~D4 · layer: 6.3 · the owner keeps a test world (world1) at C:\VRising-LocalServer, run by the same server executable with -persistentDataPath; the plan's D4 snapshot covered only the server directory and LocalLow, so a spike touching that world would have gone unseen. D4 now snapshots and hashes C:\VRising-LocalServer and fails on any change there ("owner data touched"; ServerWrites bad-8)
- A5 · 2026-09-24 · discovered · ~D6 ~D7 · layer: 6.1 · sessions 2 and 3 showed that none of the planned march levers walks a unit: variant 1 teleports the follower to the anchor, variant 2 leaves it in place then teleports it, and variant 3 leaves it Idle; the research pass found that the only long walk in the reference mods is an aggro chase onto an entity. Variant 4 (aggro chase onto the admin) is added, march variant is 1–4, and D7 records it

## Log
- 2026-09-23 · status → draft · plan
- 2026-09-23 · note · review 2 (codex) REVISE, 9 findings; D4 snapshot compare, D13 Compile items, 1 s cooldown, D5 interleavings, failing cases for every check, D20 rollback drill · plan
- 2026-09-23 · note · review 3 (codex) REVISE at the 3-round cap; F3 applied (D19 compares the Compile-item set), F6 applied (D20 range recorded before the drill, helpers-only revert drilled); taken to human review
- 2026-09-23 · status → ready · approve · review: human
- 2026-09-23 · status → in-progress · start
- 2026-09-23 · D14 · pass · cmd: pwsh tools/preflight.ps1 -SelfTest → "selftest: 19/19 checks, 3 fixtures each, 36 extra bad fixtures" · 8f4576c · claude
- 2026-09-23 · D1 · pass · cmd: dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__ → "0 Warning(s)" and "0 Error(s)" · dec14e1 · claude
- 2026-09-23 · D2 · pass · cmd: pwsh tools/preflight.ps1 → "commands: 6 admin-only, 1 public (allow-listed)" · dec14e1 · claude
- 2026-09-23 · D3 · pass · cmd: pwsh tools/preflight.ps1 → "structural edits: fenced (3 Prefab-guarded calls in EntityExtensions.cs)" · dec14e1 · claude
- 2026-09-23 · D16 · pass · cmd: pwsh tools/preflight.ps1 → "secrets: none (331 files scanned)" · dec14e1 · claude
- 2026-09-23 · note · step 3 session 1: D5 partly run (tag, limit, sweep and clear worked; mid-drain checks not reachable at 0.25 s batches); `march 2` aborted the server (Burst: entity does not exist) after the first follower was set, so D12 cannot pass on this session; march lever reordered and instrumented, clear batches 1 s apart; see docs/audits/spikes.md Step 3
- 2026-09-24 · note · step 3 sessions 2 and 3: D5 passed except the mid-drain `tag 1`, which the owner waived; `march 3` units were removed by DestroyWhenDisabled when far from players (fixed by CanDisable = false) and then stood Idle; variants 1 and 2 teleport; a second server abort followed the owner picking up the spike rat anchor (a pick-up critter) after a follow-linked bandit was devoured; A5 adds variant 4
