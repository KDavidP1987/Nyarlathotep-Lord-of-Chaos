# Audit — automation

Build plan steps 1–4 of docs/dod/automation.md. Each step has one "### Step <n>" entry under "## Pre-audit" and one
under "## Post-audit"; every post-audit entry carries a "Codex verdict:" line. Session log checks are lines
"- session <n> log check: …". Rollback base: v0.7.0.

## Pre-audit
### Step 1 · 2026-09-29 · 37eb8a1
- pre-child: event-spawns closed and 0.7.0 released; the plan approved after Review 4 (READY) and started at 37eb8a1 (A1, A2)
- git status: clean at 37eb8a1
- compile: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` → 0 Warning(s), 0 Error(s)
- tests: 2173 passed
- preflight: PREFLIGHT OK
- dod status: automation 0/31 verified (just started)
- feature doc read: docs/features/AUTOMATION.md is created by this step (the plan's Build plan step 1); docs/features/EVENT_SPAWNS.md read for the AroundPlayer pick and the wave gate the fan-out extends
- in-game baseline: not needed (step 1 has no in-game part; Session 1 is step 2)

## Post-audit
### Step 1 · 2026-09-29 · 3b8613a
- built: the schema and validation of the Interval, RegionEntered and FactionKills triggers and action.fanOut (D1, D4, D8, D10), IntervalClock (D2), Logic/PlayerTriggers.cs (RegionEntries, KillRule, KillWindows, PlayerTriggerGate, Phantoms; D9, D11, D14, D29), PlayerPick.ChooseMany and WaveGate.DecideGroups (D5, D6, D13), state.json NextInterval (D31), the chat fields and `event info` lines (D16, D27), api 6 with the contract and design §6 (D17), ControlCases rows and pending controls (D30), the registries, this record and docs/features/AUTOMATION.md
- compile: 0 Warning(s), 0 Error(s); tests: 2388 passed
- preflight: PREFLIGHT OK; -AuthSuite → "auth suite: pass (tests, commands, admin list, gateway, entity writes, vcf)"; -SelfTest → "selftest: 40/40 checks, 7/7 external selftests"
- found by the new tests while building: a FactionKills `event info` line of 628 bytes with five 96-character factions → EventLines.Fit shortens it to "factionkills <n> factions …" with "factions:" lines (D27); "needs a Interval trigger" → "needs an Interval trigger"
- /code-review round 1 (fresh subagent, read-only, on 3b8613a): VERDICT READY, 9 advisory findings — F1 Serialize nulled an empty NextInterval in place → fixed (the document is left unchanged; StateNextInterval test); F2 the D17 push test checked a test helper → fixed (EventEngine.WaveDecided, the one report step 2's WaveAction calls, is what the test drives); F3 no reason defined for a fanned-out wave dealt 0 by MaxTrackedUnits → A3 (discovered); F4 a skipped wave logged "0 units rolled" without "skipped:" → fixed (WaveLines.ZeroRolledSkip, D6's form); F5 the phantom anchor ignored territory and scope → fixed (Phantoms.Place takes the pick's test); F6 trigger.factions refused with another wording than D10's → fixed (TriggerFactionsRule); F7 aged-out kill counters stayed until the bounds → fixed (KillWindows.Prune, called per scan in step 2); F8 a picked wave with no centre spawned at the origin → fixed (treated as no eligible player); F9 the contract's api 5 sentence → reworded
- /code-review round 2 (fresh subagent, read-only, on 37eb8a1..8848be3): VERDICT READY, 7 advisory findings — F1 WaveDecided counted a blocked (NoWave) wave as skipped → fixed (neither counted nor pushed, as 0.7.0); F2 RegionEntries dropped cooldown rows when a definition left the scan (disable, reload, regions unavailable) → fixed (rows keep their own expiry and end only on expiry, the bound or restart, as Design › Data says); F3 a unit killing itself counted for its owner → fixed (KillFacts.VictimIsKiller, D11); F4 `location here` moved a fanned-out event off AroundPlayer and the reload disabled it → fixed (refused: "action.fanOut needs an AroundPlayer location: set action.fanOut none first"); F5 `event info` promised a next start for a disabled Interval definition → fixed; F6 the D17 push control cannot see WaveAction yet → carried to step 2 (WaveAction reports every decided wave through EventEngine.WaveDecided exactly once, checked in step 2's code review and Session 1); F7 NextIntervalConverter shifted an unspecified time by the server offset → fixed (written as UTC unchanged)
- no round 3: both rounds READY; round 2's fixes are covered by new cases in AutomationTests, AuthoringTests Automation and PushTests Automation
