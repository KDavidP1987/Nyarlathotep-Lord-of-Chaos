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

### Step 5 (release) · 2026-10-16 · adba3b3 (+ the release surfaces, uncommitted)
- git status: clean at adba3b3 but for this step's release surfaces, plan log notes and the tester package (the release commit)
- compile: 0 Warning(s), 0 Error(s); tests 3830/3830
- preflight: PREFLIGHT OK, version 0.13.0 on csproj and thunderstore.toml, both changelogs carry 0.13.0
- dod status: see dod status; step 4 (the test weekend, D23 and D29) has not run: the owner asked to release first (Log, 2026-10-16)
- feature doc read: docs/features/UNIT_WATCH.md (Status: released with apocalypse in 0.13.0)
- baseline boot: the rc.3 DLL (same code, version suffix only) booted 2026-10-16 for tester-setups.vrs 79/80, every [Error/[Warning read (baseline kinds only), snapshot setups restored "hashes equal"

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

### Step 3 · 2026-10-14 · (this commit)
- compile / preflight: 0 errors, 0 warnings; PREFLIGHT OK; full suite 3560/3560; NyarDevTools builds (dev only, never shipped)
- /code-review: step 3's services diff reviewed at 5e4659f (fresh read-only subagent; findings fixed before that commit);
  A2's change (TargetsPlayer replacing the 40 m rule, the first-move line's player field, the `low` diagnostic under
  VerboseLogging) read in full here: the rule reads only the unit's own aggro target, no name in any line. Planted
  faults for A2 in docs/audits/unit-watch-step3-planted.txt (old 40 m rule → 2/19 fail; always-true → 4/19 fail)
- change reviews: A2 and A3 each reviewed by a fresh read-only subagent (REVISE; every finding dispositioned in the plan's Log)
- Codex verdict: not run for this step (Halloween track D34: once per child, before its release, step 5)
- in-game (docs/features/UNIT_WATCH.md › Test results › Session 1): unit-watch.vrs 23/23, unit-watch-remove.vrs 23/23,
  unit-watch-stall.vrs 25/25, unit-watch-ends.vrs 28/33 + tail 7/7 + ends-off 10/10, unit-watch-apocalypse.vrs 15/16
  (removal timeout, raised) with the restart's resume line read, unit-watch-timing.vrs run 3 27/27 with D28's span 10/10.
  The positive standing and still cases and D34's first-move measurement need a vulnerable player: test weekend (A3)
- logs: boots a- to p- in %TEMP%\nyar-uw1-logs read in full for [Error/[Warning: no [Error] line; warnings are the known
  example-empowerment, Il2CppInterop, Beelzebub TUNE, event stop/purge summaries, and HarmonyX type-load warnings from the
  dev-only DevHold type scan (boots f- on); NyarDev.log has the same 226 boot-time missing-prefab traces in every boot
- server: snapshot uw1 restored ("snapshot restored; hashes equal"), then the current DLL redeployed (hash equal to the build)
- dod status: 4/34 verified (D18, D20, D22, D28); D24-D27 ready for the owner's acceptance; D23 and D34's measurement at the test weekend (A3)
