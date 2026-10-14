# Audit — unit-watch

## Pre-audit
### Step 1 · 2026-10-14 · 9d570f8
- git status: clean at 9d570f8 before the step began (the step's own dev tools and scenario were then added: tools/vrclient/NyarDevTools/DevFight.cs, Plugin.cs, scenarios/unit-watch-spike.vrs)
- compile: 0 errors, 0 warnings (`dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__`)
- preflight: exit 0, PREFLIGHT OK, release tags 14/14
- dod status: 0/33 verified
- feature doc read: docs/features/UNIT_WATCH.md (Status: planned, step 1 next; no open questions blocking the spike)
- baseline boot (in-game steps): dev server booted the current DLL (6e0b901 code) with NyarDevTools; "push: ready" after 15 s; LogOutput.log checked, no [Error lines at boot; dev snapshot uw0 saved before

### Step 2 · 2026-10-14 · 010b7f7
- git status: clean (step 1 committed dc4b0bd, handoff 010b7f7)
- compile: 0 errors, 0 warnings
- preflight: exit 0, PREFLIGHT OK
- dod status: 0/33 verified (D19 waits on the owner)
- feature doc read: docs/features/UNIT_WATCH.md (Spike verdicts recorded: fight = apply + target clause; off-ground = S-12 fallback)
- baseline boot: not an in-game step (Logic, commands, docs and tests only)

### Step 3 · 2026-10-14 · f53e8b4
- git status: step 3's code uncommitted (built by the builder subagent, reviewed); otherwise clean
- compile: 0 errors, 0 warnings; tests 3560/3560
- preflight: exit 0, PREFLIGHT OK; dependency suite unit-watch 2/2
- dod status: 0/34 verified (A1 added D34)
- feature doc read: docs/features/UNIT_WATCH.md (D34 in Detection and Config)
- baseline boot: the pathing probe's boot of the same session (0.12.0+ DLL at 6e0b901 code with NyarDevTools): -LogCheck 0 unhandled, only known warnings

## Post-audit
### Step 1 · 2026-10-14 · (this commit)
- compile / preflight: mod 0 errors 0 warnings; NyarDevTools builds 0 errors (dev server only, never shipped); preflight OK
- /code-review: the step's diff is the dev-only NyarDevTools (.devfight diagnostic patches, .devstrand, .devground) and the
  spike scenario; read in full: the fight patches return at once unless `.devfight` runs, are unpatched on unload and log
  no names; `.devstrand` moves the nearest unit of the given prefab, native or ours, which is acceptable for a dev-server
  tool (step 3 scenarios use it only on our spawned units). No findings.
- Codex verdict: not run for this step (Halloween track D34: once per child, before its release, step 5)
- in-game: unit-watch-spike.vrs SCENARIO PASS 14/14; fight verdict apply (+ S-9's target clause for hits on a player);
  off-ground verdict not usable (3 false in 7) so S-12's fallback; purge 6/6 silent; strand under 30 m works. Recorded
  in docs/features/UNIT_WATCH.md › Test results › Spike (D19). -LogCheck 0 unhandled; only known warnings.
- dod status: 0/33 verified; D19 waits on the owner's acceptance (manual, rubric 3)

### Step 2 · 2026-10-14 · (this commit)
- compile / preflight: 0 errors, 0 warnings; PREFLIGHT OK; full suite 3536/3536 (3395 before the step)
- built by a builder subagent; 18 planted faults in docs/audits/unit-watch-step2-planted.txt, plus 3 for the review fixes
- /code-review (a fresh read-only subagent on the diff): ISSUES (3), all fixed:
  1. high: a throwing walk check, claim, region or focus read inside the landing choice escaped the check and stopped
     the watch for every event (the oldest mark failing first each time) → caught per unit, logged as a read failure,
     counted as a "no landing" try (UnitWatch.cs Marked; test Tries_fails_when_a_throwing_landing_halts_the_watch)
  2. medium-high: a marked unit not yet moved, or waiting for its removal, was never cleared by a fight, so a seen unit a
     player was fighting could be removed in view → a fight after the mark clears every mark first, removal included
     (test Tries_fails_when_a_seen_marked_unit_that_fought_is_removed)
  3. medium: NoCredit's scoreboard check could not fail (nothing fed the board) → a stray death event is now run through
     the real death pass (ScoreFeed Before/After) after the removal, and fails when the removal leaves the unit tracked
  - left for step 3 (noted by the review, not defects yet): OpUnstick's default refusal and `event info`'s zero counts
    until the phase is wired; the shallow-water risk of "no grounded level at all" (plan Log, checked in Session 1)
- Codex verdict: not run for this step (Halloween track D34: once per child, before its release, step 5)
- in-game: none in this step (Logic only); step 3 runs Sessions 1-2
- existing tests changed (D22): ContractDocTests.cs, .Api4.cs, .Automation.cs and WireFormatTests.cs (api 7 → 8);
  ContractDocTests.Revenge.cs and .Apocalypse.cs (api read as "below" instead of "equal", the contract's new sentence);
  ConfigClampTests, EventAdminTests, ApiTwinTests made partial; FakeStores.cs FakeAdminOps gains the watch fields and
  OpUnstick; ControlCases.cs and ControlCaseTests.cs rows for unit-watch
- dod status: 0/33 verified (step 2's items are checked once the services exist and D21's control rows run green in step 3's post-audit)
