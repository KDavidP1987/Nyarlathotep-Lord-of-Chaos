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
