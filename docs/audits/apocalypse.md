# Audit — apocalypse

## Pre-audit
### Step 1 · 2026-10-13 · a2771a8 (re-checked at 71342dd, docs only)
- git status: clean at a2771a8 (the plan started with A1-A6); 71342dd adds Review 4's dispositions and A7-A10, docs only
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s); tests 3134/3134
- preflight: fails one check only, "release tags: 13/14 (v0.12.0 not pushed)": revenge 0.12.0 is tagged locally and its push waits on the owner's OK; "secrets: none (1995 files scanned, 1941 index blobs)"
- dod status: 0/38 verified (just started); review pending (Review 4 REVISE dispositioned, round 2 running)
- S-11: wave-sets 0.9.0, swarm 0.10.0 and hunting-grounds 0.11.0 are released on GitHub; revenge 0.12.0 is released locally only (commit 8ee59c5, tag v0.12.0), so its changes are in this child's base and its push does not change this diff
- 13.1: wave-sets A7's 176.8 ms Hunt tick did not come back: revenge step 2's timing baseline (24 tracked spiders, 25 min) read "timing span: 10/10 windows under 5 ms, tracked >= 10, targets >= 1, 0 slow ticks"; D29 measures the apocalypse's own load
- feature doc read: docs/features/OUTBREAK.md (the owner's brief, design §9 D23, D24, D44-D45, D49-D51); docs/features/APOCALYPSE.md created in this step; docs/features/HUNTING_GROUNDS.md (the mover) and docs/features/REVENGE.md (final-wave bosses)

### Step 2 · 2026-10-13 · 515a020
- git status: clean at 515a020 (Reviews 4-8 dispositioned, review: subagent; the spike tools and scenarios committed)
- compile: 0 Warning(s), 0 Error(s); tests 3134/3134
- preflight: fails one check only, "release tags: 13/14 (v0.12.0 not pushed)" (revenge's push waits on the owner)
- dod status: 0/38 verified; step 1's spike verdicts (D24, D37) are being recorded in parallel: step 2 is the Logic layer only (schema, rules, record, chat) and none of it reads the march or pathing verdict; a usable walk would come back to the owner as a D6 amendment before step 3, and step 2's MarchRule is the front's position and hold rule, the same under hop or relay
- feature doc read: docs/features/APOCALYPSE.md (Status: step 1 in progress; open questions: the march and pathing verdicts, the faction mixes)
- built by a builder subagent in its own worktree; the orchestrator reviews, tests and merges

### Step 3 · 2026-10-13 · 1841fee
- git status: clean at 1841fee (step 2 merged, its review fixed, post-audit written)
- compile: 0 Warning(s), 0 Error(s); tests 3337/3337
- preflight: fails one check only, "release tags: 13/14 (v0.12.0 not pushed)" (revenge's push waits on the owner)
- dod status: 18/38 verified (D1-D8, D10, D11, D13-D17, D19, D20, D36); D9, D12, D23 open until step 3 wires the end paths, the boot resume and the dependency suite; D24, D37 wait on the owner
- feature doc read: docs/features/APOCALYPSE.md (Status: step 2 merged; open questions: the pathing study is 1.0, D6 keeps the hop with the path cleared)
- baseline boot: the 0.12.0 DLL booted twice in step 1's session with 0 unhandled (Post-audit › Step 1); the in-game sessions of this step start from a fresh snapshot (apo1)
- built by a builder subagent in its own worktree; the orchestrator reviews, tests, runs the vrclient sessions and merges

### Step 5 (drill part) · 2026-10-14 · 334df0d
- git status: clean at 334df0d (A13 Review 10 READY; the D30 soak running on the dev server with the 2b4d93e DLL)
- compile: 0 Warning(s), 0 Error(s); tests 3392/3392
- preflight: fails one check only, "release tags: 13/14 (v0.12.0 not pushed)" (revenge's push waits on the owner)
- dod status: 26/38 verified; D32's drill selftest is built now, its rollback gate (-From v0.12.0 -To v0.13.0) waits on the v0.13.0 tag after the test weekend
- feature doc read: docs/features/APOCALYPSE.md (Sessions 1-2 recorded, the test weekend sheet written, Session 3 running)
- the rest of step 5 (Codex, surfaces, release) waits on step 4 (the owner's test weekend)

### Step 5 (release) · 2026-10-16 · adba3b3 (+ the release surfaces, uncommitted)
- git status: clean at adba3b3 but for this step's release surfaces, plan log notes and the tester package (the release commit)
- compile: 0 Warning(s), 0 Error(s); tests 3830/3830
- preflight: PREFLIGHT OK, version 0.13.0 on csproj and thunderstore.toml, both changelogs carry 0.13.0
- dod status: 26/38 verified; step 4 (the test weekend, D31) has not run: the owner asked to release first (Log, 2026-10-16)
- feature doc read: docs/features/APOCALYPSE.md (Status: released in 0.13.0; D31 open)
- baseline boot: the rc.3 DLL (same code, version suffix only) booted 2026-10-16 for tester-setups.vrs 79/80, every [Error/[Warning read (baseline kinds only), snapshot setups restored "hashes equal"

## Post-audit

### Step 1 · 2026-10-13 · spike session (boots 1-2, 515a020 tools)
- compile: NyarDevTools 0 Warning(s), 0 Error(s); the mod unchanged (0.12.0 DLL)
- log check boot 1 (`pwsh tools/preflight.ps1 -LogCheck -ServerPath <dev server>` before the restart): 0 unhandled, 712 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons
- log check boot 2: 0 unhandled, 499 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons
- every [Error]/[Warning] read: Beelzebub's TUNE warning, Il2CppInterop's Class::Init warning, example-empowerment's known pillar warning, the purge warning line (a purge in spike run 2); the Unity log's 226 missing-prefab lookups appear at boot in the previous session's log too (226), so they are not this session's
- scenarios: apocalypse-spike.vrs run 3 SCENARIO PASS 83/83; faction-mix.vrs run 4 SCENARIO PASS 89/89; earlier runs failed on scenario design (records in docs/features/APOCALYPSE.md › Spike)
- verdicts recorded: "march verdict: go — hop", "pathing verdict: walk not usable"; D24 and D37 wait on the owner's acceptance (rubric 3), not checked
- snapshot restored; hashes equal (apo0); dev cfg back to VerboseLogging false, ManualSpawnLifetimeSeconds 300
- Codex verdict: deferred to step 5 (Halloween track: once per child before its release)

### Step 2 · 2026-10-13 · merge 038c8c4 + review fixes
- compile: 0 Warning(s), 0 Error(s); tests 3337/3337 (3332 at the merge, plus 5 from the review fixes)
- preflight: fails one check only, "release tags: 13/14 (v0.12.0 not pushed)" (revenge's push waits on the owner)
- /code-review (a fresh read-only subagent on `git diff 4c974d6 038c8c4`): VERDICT: ISSUES (3), all fixed in this pass
  - M1: the record took bosses only from the current last horde, so raising `hordes.count` after a resume dropped a living boss; each front now keeps the boss prefabs it raised (set at the rise, kept from the record) — ApocalypseTests Bosses `a_grown_hordes_count_drops_a_living_boss_from_the_record`
  - M2: Engine.Resume skipped the master switch, the pillar and the definition's switch; it takes the controls (the purge cooldown and MaxConcurrentEvents do not apply to a run that never ended) — Resume `a_pillar_or_master_switch_turned_off_while_down_is_ignored`; step 3's boot resume must pass the controls
  - M3: a purge during the drain wait kept ResumePending, refusing every later apocalypse; CancelAll clears it — Resume `a_purge_in_the_drain_wait_keeps_the_slot`
  - L1 fixed: a record without its goal key reads as "goal missing" (Goal is nullable) — Resume `a_record_without_its_goal_key_resumes_to_the_origin`
  - L2 fixed: the rebuild finds a boss's modifiers by entry index first — Bosses `two_entries_of_one_prefab_rebuild_with_the_wrong_modifiers`
  - L3 (restored totals omit players past the 200-row cap) and L4 (the record-removal test is circular; step 3 tests the real end paths) logged in the plan for step 3
- planted faults: docs/audits/apocalypse-step2-planted.txt (21 faults, each failing its named test)
- Codex verdict: deferred to step 5 (Halloween track: once per child before its release)

### Step 3 · 2026-10-14 · merge 3f607e4 + review and session fixes (be6af26)
- compile: 0 Warning(s), 0 Error(s); tests 3392/3392 (3373 at the merge)
- preflight: fails one check only, "release tags: 13/14 (v0.12.0 not pushed)" (revenge's push waits on the owner)
- /code-review 1 (a fresh read-only subagent on `git diff 7390a68 3f607e4`): VERDICT: ISSUES (5) — H1 scenario fronts
  held from the goal, M1 the tick's fault guard could not fire, M2 a failed resume left its orders queued, M3 a straggler
  could be hopped beside a player, M4 a resumed run's status line, L1-L5; fixed in fe94add with tests
- /code-review 2 (a fresh read-only subagent on `git diff 3f607e4 5bda54f -- Nyarlathotep/`, the review and session fixes):
  VERDICT: ISSUES (3) — M1 a spent walk budget let an unobserved front cross to an unread point (the crossing holds on an
  unread point; stragglers stop below HopRule.FrontReserve), L1 a step reaching an off-axis goal stopped a float step
  short, L2 a failed resume kept its restored scoreboard rows; all fixed in 2b4d93e with tests
- planted faults: docs/audits/apocalypse-step3-planted.txt (11 at the merge, then one per session finding and review fix,
  each failing its named test); the roam-move guard planted alone fails D22's march-read category (Review 6 F2)
- dependency suite: "dependency suite: apocalypse 5/5 (native-read, spread-read, march-read, resume-read, state-write)"
- in game: Session 1 (D25-D27) apocalypse.vrs run 8 105/106, the miss being chat OCR of a line the server log shows sent;
  Session 2 (D28) prep-restart 34/34 and boot2 23/23; D29 timing run 3 22/22 and "timing span: 10/10 windows under 5 ms"
  (docs/features/APOCALYPSE.md › Test results); Session 3 (D30) the 3 h soak: rises on schedule, renewals for all 65 tracked,
  record gaps at most 60.6 s, -LogCheck "0 unhandled, 3467 nyar lines, 0 orphan errors, 0 unity errors", one slow tick
  of ours (2298 ms, cause unproven, none in the 2 h 50 min after) — D30 waits on the owner
- every [Error]/[Warning] of Sessions 1-2 read: ours are the stop, purge and pillar-off end lines and example-empowerment's known pillar
  warning; Beelzebub's TUNE warning and Il2CppInterop's Class::Init warning are not ours
- dod: D9, D12, D18, D21, D22, D23, D29, D38 verified; D25-D28 wait on the owner's acceptance (rubric 3)
- Codex verdict: deferred to step 5 (Halloween track: once per child before its release)

### Codex cross-inspection, early (before step 4; step 5 keeps its own post-audit) · 2026-10-14 · d88dbbc + fixes
- Codex (`codex exec -s read-only`, prompt via stdin) on `git diff v0.12.0..HEAD -- . ':!tools/vrclient' ':!docs'`, run before
  the test weekend so its fixes are in the weekend DLL: VERDICT: ISSUES (3)
  - H1 fixed: a resume record whose living counts exceed the tracked-unit cap's maximum (500) is refused as unreadable
    ("the living counts exceed the tracked-unit cap"), and the roamer rebuild stops at the first unit the caps refuse —
    ApocalypseStateTests `Record_fails_when_a_record_with_a_bad_field_is_kept` (planted: the bound removed → it fails)
  - M1 fixed: without a walk check, Walkable throws instead of answering walkable, so no hop, jump or front step lands on
    unread ground; the march-read category records it (D21), as hunting-grounds' mover does
  - M2 rejected: `game.Announce` only enqueues a line in memory (Services/Announcer.Apocalypse) and cannot fail on the game
- compile: 0 Warning(s), 0 Error(s); tests 3395/3395
- in game on the 6e0b901 DLL: apocalypse-timing.vrs SCENARIO PASS 22/22 (apocalypse-timing-20261014-063516.json); no
  "walk check: none" or read-failing line, horde hops "56 of 59 stragglers moved", 6 roam lines; `-TimingSpan` → "timing span:
  10/10 windows under 5 ms, tracked >= 50, targets >= 1, 0 slow ticks"; every [Warning] known (stop line, example-empowerment,
  Beelzebub, Il2CppInterop), no [Error], 0 exceptions in the Unity log
- Codex runs once more on the release diff only if code changes after the test weekend

## Post-audit · 2026-10-16 · the G-Portal fixes and A14 (admin goal, fresh-world wait, early-departure log, D60 groups, six wave templates)

- compile: 0 Warning(s), 0 Error(s); tests 3586/3586; `pwsh tools/preflight.ps1` → PREFLIGHT OK; dod --check apocalypse
  and nyarlathotep: problems 0
- Codex (`codex exec -s read-only -c features.experimental_windows_sandbox=true`, prompt via stdin, on the uncommitted
  code diff over f01f18f): VERDICT: PASS (no blocking defects)
- in game: wave-templates.vrs SCENARIO PASS 54/54 (wave-templates-20261016-012328.json); faction-mix-waves.vrs 131/132
  then the cursed pass 26/26 after CHAR_Cursed_Mosquito was dropped (docs/features/APOCALYPSE.md › Session 4); a
  brand-new world boot (docs/features/FOUNDATION.md › Test results); every [Warning] known (stop lines, the dev
  example-empowerment, Beelzebub, HarmonyX, Il2CppInterop), no [Error]; the 224 PrefabLookupMap notices at save load
  appear on every boot of the dev world
