# Reviews: revenge

## Review 1 · 2026-10-11 · subagent · plan commit 8b80ba2 · plan 76090 B · 27 items · files 0 · e3b0c44298fc · prompt 92e3f7c95241
L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · no finding
L4 · Considered · F1, F6
L5 · Considered · F7
L6 · Considered · no finding
L7 · Considered · F4
L8 · Considered · no finding
L9 · Considered · F1, F8
L10 · Considered · F2
L11 · Considered · F2
L12 · Gap · F5
L13 · Considered · F8
L14 · Considered · F3
L15 · Considered · no finding

F1 · blocking · blocks: outcome. C3 promises that "growing hunt squads come for the target only", and Business rule 5 says "every tier around the target only". That fails when the target has a companion within Hunt range. The plan reuses Hunt unchanged, and `HuntPlan.Targets` (`Logic/Spawning.cs:909`) seeds up to 5 of the nearest eligible players within `range` of the wave centre, whoever they are. A second player near the target is hunted too. D6 only limits where the centre is, and D22 only checks where tiers are centred. Business rule 9's "every wave pick" set (probe 4.5) names `WaveAction.Pick` but leaves out Hunt's target choice, which is the other half of who a squad comes for.
Fix: the owner decides one of two things. Either a Revenge instance's Hunt seeds the focus player only, in which case add an item whose test fails when a non-focus player is seeded and extend D22. Or bystanders within range are hunted too, in which case reword C3 and Business rule 5 and record it in §9.

F2 · blocking · blocks: outcome. C4 promises "the server learns that a faction seeks revenge without learning on whom". That fails at the end of every template revenge. `bandit-revenge` (D14) and rv-test set `scoreboard: true`. `Announcer.Scoreboard` queues the scoreboard as a server-wide Info line naming the top 3 players by kills (design §9 D38). The target is almost always the top killer, so the server learns the target's name right after the anonymous alert. D43 settles only the alert's wording, not the scoreboard, so this does not repeat a settled decision (probe 10.4).
Fix: an owner decision recorded in §9. The options are: a Revenge scoreboard goes to participants privately; or it goes server-wide without names; or `bandit-revenge` ships `scoreboard: false`; or the owner accepts it as a documented exception to C4. Then add a D-item whose test fails if a Revenge scoreboard line naming the target is queued server-wide, unless the owner accepted the exception.

F3 · advisory. S-15 says swarm's 0.10.0 ships with `Wire.Api` 6, so revenge gets api 7. But `Wire.Api` is already 6 in v0.8.0 (`git show v0.8.0:…/Wire.cs`). Wave-sets and swarm may each raise it, so revenge's integer is probably 8 or 9. D15's test is relative ("previous release's plus one") and S-15 has a fallback, so nothing fails.
Fix: correct the numbers in S-15 now so the D15, D24 and D25 amendments aren't forced at step 3.

F4 · advisory. D5 says a due player starts only "when the controls allow it", but the hold reasons are only `territory|busy|target busy|ineligible`. A refusal from the controls (MaxConcurrentEvents full, pillar off, MaxTrackedUnits) has no reason in the hold line. The plan also doesn't say whether that refusal keeps the player due, as D5 implies, or resets the heat the way a conditions refusal does.
Fix: name a `controls` hold reason that keeps the player due, and add it to the D5 test.

F5 · advisory (L12 Gap on 12.4, but no product outcome fails). The test items D1–D15 and D17 have dry runs that show only that the existing class passes or that "No test matches". Their failing inputs were never run against a real check before approval; the plan schedules them as step 1 planted-fault notes. The cmd items (D16, D23, D24, D26) do have real failing dry runs.
Fix: no change needed beyond the planned `note · planted` lines. Optionally run the failing cases that today's classes can already express (D1 bounds on the FactionKills branch, D11 window bounds) now.

F6 · advisory. Some per-line checks cover only part of a family. D4 checks the warning for 480 bytes, names, digit pairs and "<". D7 checks the alert and start line for bytes and names. RevengeEnd (D8) has no byte or name check. D9's privacy test covers logs, pushes and replies, not chat text.
Fix: declare the four builders as siblings and add `twins:` lines, or give one test the 480-byte, no-name and no-"<" checks for all four.

F7 · advisory. D3 bounds heat rows by `TriggerLimits.CountersInAll` (2000), but doesn't say whether that pool is shared with FactionKills' `KillWindows` or is a second, separate 2000. If shared, FactionKills counters could be evicted by heat rows. The plan says D3 and D5 "keep it byte-for-byte".
Fix: state "separate bound, same constants" (or shared) in D3 and in Performance 13.2.

F8 · advisory. The recon commit is e4f0594, but HEAD is 8b80ba2, which includes the wave-sets step 2 fixes (hunt phase, ScoreRule). Performance 13.1's baseline is a 2026-10-05 measurement that predates them. It is not a new recon spike.
Fix: move `recon_commit` to the current HEAD before approval. The D23 measurement already gates the release.

F9 · advisory. In D18–D21 the steps can't all run in the listed order in one session. The 5-minute cooldown after D19's revenge blocks D20's V Blood heat and the D21 tier-2 purge and restart unless time passes or the order changes. This is scenario work, not a plan decision.
Fix: order the scenarios, or note `playerCooldownMinutes 0` for a second rv-test definition.

missing components: none
undeclared families: RevengeWarning, RevengeStart, RevengeEnd, RevengeAlertPool; offline, dead, unusable position, claimed territory, PvP combat, outside regional scope

14/15 layers · 49/50 probes

VERDICT: REVISE

### Dispositions
- F1 · accepted · owner decision design §9 D46 (option A): a Revenge instance's Hunt seeds only its focus player; D6 states it and its fails-when gains "a non-focus player within Hunt range is among a Revenge instance's hunt targets"; a companion who attacks is fought through the game's own aggro, never seeded (D6, Business rules 5); Business rules 9's "every wave pick" adds HuntPlan.Targets; Interfaces 5.2 and step 1 name the focus-only HuntPlan.Targets; D22 checks a non-attacking second player within range is not hunted
- F2 · accepted · owner decision design §9 D47 (documented exception): C4, Business rules 6 and Security 10.4 say the target stays anonymous while the revenge runs and the end-of-event scoreboard (top 3 by name, D38) is shown as for any event; D7's fails-when covers the alert, the start line and the warnings naming the target; D7 and D9 state the scoreboard naming is the recorded exception, so no test forbids it; D22 notes the scoreboard may name both players
- F3 · accepted · S-15: v0.8.0 already ships api 6; revenge's api is the previous release's plus one, likely 7, 8 or 9 depending on whether wave-sets and swarm raise it, read from v0.10.0's Wire.cs at step 1
- F4 · accepted · D5: hold reason `controls` (pillar off, MaxConcurrentEvents or MaxTrackedUnits full, the kill switch's cooldown) keeps the player due with the heat untouched; D5's fails-when gains a start under refusing controls and a controls refusal not logged "held (controls)" or resetting the heat
- F5 · accepted · Failure & observability 12.4 states that the new test classes' failing cases (D1-D15, D17) run as step 1's `note · planted` lines and only the cmd items failed for real before approval; no other change
- F6 · accepted · D9 checks the four builders together as one family: each fits 480 bytes, holds no player name and no "<", and its chat text is in the planted-name scan (UF1)
- F7 · accepted · D3 and Performance 13.2: a separate bound with the same constants; heat rows and FactionKills' KillWindows never evict each other; D3's fails-when gains cross-eviction
- F8 · accepted · recon_commit moved to 8b80ba2; Performance 13.1 notes the 2026-10-05 measurement predates it and D23 gates the release
- F9 · accepted · D18: a second definition rv-test0 (playerCooldownMinutes 0) enabled after D19 with rv-test disabled; the scenario order is D18, D19, D20, D21; D20 and D21 run on rv-test0; step 2 adds rv-test0 to the dev events.json
- UF1 · accepted · D9 (the four builders checked together for 480 bytes, no name, no "<")
- UF2 · accepted · D5 (every ineligibility reason in its fails-when, unusable position added; D6's held-wave fails-when names all six)

## Review 2 · 2026-10-11 · subagent · plan commit d8cdade · plan 80149 B · 27 items · files 0 · e3b0c44298fc · prompt 808d01aab200

**Probes that blocked in Review 1:**
- **4.5 (Review 1 F1): answered.** Design §9 D46 is now in force. D6 says a Revenge instance's Hunt seeds only its focus player. Its fails-when includes "a non-focus player within Hunt range is among a Revenge instance's hunt targets". Business rule 5 and the "every wave pick" set in Business rule 9 now name `HuntPlan.Targets` (`Logic/Spawning.cs:909`, which today seeds up to 5 of the nearest players). Interfaces 5.2 and Build step 1 name the focus-only Targets. D22 checks that a second player who stands in range without attacking is not hunted.
- **10.4 (Review 1 F2): answered.** Design §9 D47 records the exception. C4, Business rule 6, Security 10.4, D7 and D9 say the target stays anonymous while the revenge runs, and that the end-of-event scoreboard (D38) is the recorded exception. D7's fails-when covers the alert, the start line and the warnings naming or locating the target.

**Earlier advisories F3–F9 (and UF1, UF2), checked against the text:**
- Review 1 F3: S-15 now gives api 7, 8 or 9 and says it is read from v0.10.0's `Wire.cs`. Today `Wire.Api` is 6.
- Review 1 F4: D5 has the `controls` hold reason. It keeps the player due with the heat untouched, and its fails-when covers it.
- Review 1 F5: section 12.4 says the step 1 planted-fault notes are where the failing cases run.
- Review 1 F6 and UF1: D9 checks the four builders as one family, for 480 bytes, no name and no "<".
- Review 1 F7: D3 and 13.2 say "a separate bound with the same constants" (`TriggerLimits` 200 / 2000, `Limits.cs:98-99`), and D3's fails-when covers eviction across the two.
- Review 1 F8: `recon_commit` is now 8b80ba2, and 13.1 notes that the measurement predates it.
- Review 1 F9: D18 adds rv-test0, with the order D18, D19, D20, D21.
- Review 1 UF2: D5 and D6 list all six ineligibility reasons.

**Layers:**
- L1 · Considered · no finding
- L2 · Considered · no finding
- L3 · Considered · no finding
- L4 · Considered · F2
- L5 · Considered · F1
- L6 · Considered · F3
- L7 · Considered · F4
- L8 · Considered · no finding
- L9 · Considered · no finding
- L10 · Considered · no finding
- L11 · Considered · no finding
- L12 · Gap · F5
- L13 · Considered · no finding
- L14 · Considered · no finding
- L15 · Considered · no finding

**Findings:**

**F1 · advisory.** Focus-only Hunt (D6, D46) needs a way to match the focus to a Hunt candidate. `HuntCandidate.Key` is a `long` entity handle (`Spawning.cs:860`), but `ActiveEvent.Focus` is a platform-id string (`PlayerPick.ChooseMany`'s `focus`). No plan item names how the two are joined. This is builder work, and D6's FocusOnly test already fails if the join is wrong.
Fix: in step 1, have the Hunt tick resolve the focus's platform id to its candidate key. Record it as an amendment if it needs a new `HuntCandidate` field.

**F2 · advisory.** The "every end path" set in Business rule 9 (six paths, D8) does not cover `.nyar event disable`, or an edit that makes the running definition invalid. Neither is said to stop the instance, and if one does, the plan does not say whether it sends the private end line. That depends on what EventRuntime does today. It is not a new policy.
Fix: in D8, say that a disable that stops a running instance takes the "admin stop" path (line and log), or that it leaves the instance running until its end.

**F3 · advisory.** D10 gives revenge its own failure line ("revenge: kill skipped") and health entry. The heat, however, rides the shared FactionKills read, which already logs "[nyar] faction kills: death skipped" once per `KillFaults` streak (`DeathEventPatch.cs:53,104`). The plan does not say whether one throwing read prints one line or both.
Fix: in D10, name the streak that owns the shared read's failure (for example, the KillFaults line, plus the revenge health entry only while a Revenge definition is startable).

**F4 · advisory.** The dry-run notes in the Log were run on an isolated copy of e4f0594, while `recon_commit` is now 8b80ba2. Nothing fails, because every one of those checks re-runs at step 1.
Fix: say in the next Log note that the dry runs predate 8b80ba2, or re-run them there.

**F5 · advisory (L12 Gap on 12.4; no product outcome fails).** As in Review 1, D1–D15 and D17 have not had a real failing run before approval. Section 12.4 states this and schedules the step 1 `note · planted` lines. This is not blocking in either round, so it stands as a stated assumption.
Fix: none beyond the planned planted-fault notes.

**Scenarios hunted** (all handled or advisory):
- **Minimal:** the focus logs out during tier 1. The waves are held until the duration ends (D6), and no end line is sent while the player is offline (D8, "when online").
- **Concurrent:** a FactionKills instance and a Revenge instance focus the same player at once. The plan only forbids two Revenge instances on one player (D5), and nothing promises more.
- **Unauthorized:** a player runs `event start`. The admin-only refusal applies (D13, Epic D3, AuthSuite in D11).

**Summary:**
missing components: none
undeclared families: none
- Earlier findings: all resolved

14/15 layers · 49/50 probes

VERDICT: READY

### Dispositions
- F1 · accepted · D6 and Build plan step 1: the Hunt tick resolves the focus's platform id (ActiveEvent.Focus) to its HuntCandidate key (Logic/Spawning.cs:860); a new HuntCandidate field the join needs is recorded as an amendment before it is built
- F2 · accepted · checked against today's code: `.nyar event disable` is an events.json edit and a reload (Logic/AdminFlows.cs Enable → OpEdit), and a reload, valid or invalidating, never stops a running instance, which keeps the definition it started with (Logic/EventCatalog.cs, Logic/Engine.cs:319, foundation D6); D8 now says a disable or an invalidating reload is not an end path: the instance runs on to one of the six paths, with no line or log of its own, and D8's fails-when gains "a disable or an invalidating reload ends a running instance"
- F3 · accepted · D10: a throw in the shared FactionKills read is owned by the existing KillFaults streak line "[nyar] faction kills: death skipped" (Patches/DeathEventPatch.cs:53,104), so one throwing read prints one line; "revenge: kill skipped" is the heat update's own streak; the "revenge: kill read failing" health entry appears only while a Revenge definition is startable; D10's fails-when gains both
- F4 · accepted · Log note: the dry runs ran on an isolated copy of e4f0594, predate recon_commit 8b80ba2, and re-run at step 1
- F5 · accepted · a stated assumption, no change; Failure & observability 12.4 already schedules the step 1 `note · planted` lines

## Review 3 · 2026-10-11 · subagent · plan commit bec06ff · plan 82030 B · 27 items · files 0 · e3b0c44298fc · prompt 26f75055d861
F1 · advisory · L12, probe 12.4. The new test classes (D1-D15, D17) were not failed for real before approval. The Log shows only "existing class passes" or "No test matches". The scoring note counts a failing case that was never run as a Gap. This does not block anything: the controls exist as fails-when clauses, and their planted-fault runs are set for step 1.
Fix: keep the step-1 `note · planted · D<n>` lines as stated, and have dod status show each one before step 1's post-audit closes.

F2 · advisory · probe 13.1. The recon measurement (wave-sets Log, 2026-10-05) is older than recon_commit 8b80ba2 and includes the open 176.8 ms hunt-phase spike (A7). Revenge adds more Hunt squads.
Fix: none needed before approval, because D23 gates the release. Record a fresh -TimingSpan baseline in step 2's pre-audit so that a D23 failure can be blamed on revenge or on A7.

F3 · advisory · probes 3.3 and 9.1. D3 drops the least recently updated heat row once the bound is reached. The plan does not say whether a player's cooldown (D5) and warning state live in that row. If they do, evicting the row would let a player gather heat again during the cooldown. A due player waiting in their castle could also lose their row the same way.
Fix: in Logic/Revenge.cs, either keep cooldowns outside the evictable rows or record the eviction rule as an amendment. This is builder work, not a decision.

F4 · advisory · probe 7.2. D13 does not cover an admin start on an admin who is already the focus of another running revenge. D5's "target busy" check covers only System starts. It also does not say whether an admin start sets a cooldown for the admin.
Fix: one sentence in D13, for example "an admin start skips the target-busy check and sets no cooldown", tested in AdminStart.

F5 · advisory · probe 7.3 against D6. HuntPlan.Targets only seeds players within `range` of the wave centre (Spawning.cs:909). If the focus moves more than range metres from a squad that has already spawned, the squad has nobody to hunt. The plan says nothing about whether that is accepted.
Fix: say in REVENGE.md that it is accepted (the squad idles or leashes until the event ends or the wave is cleared), or note it as a look-and-feel point for D22.

F6 · advisory · probe 4.3. Business rule 7.3 says an edited definition keeps its heat rows "under the new trigger's window from the reload". It does not say what happens to rows built from factions the edit removed, or when the edit lowers `heat` below a row's current total (the player becomes due at the next scan without a new kill).
Fix: state that a heat or factions edit keeps the existing points and only changes what new kills add, and that the next scan applies the new `heat`. Add one case to RevengeTests Heat.

Code claims checked: HuntCandidate at Spawning.cs:860 (`long Key`); HuntPlan.Targets excludes players in territory or PvP combat and takes the nearest 5; TriggerLimits.CountersPerDefinition/CountersInAll are 200 and 2000; WaveOutcome.NoWave is neither counted nor used (Engine.cs:420); EventEngine keeps the definition an instance started with across a reload (Engine.cs ~317); DeathRule.IsVBloodKill reads the consume source only; KillFaults prints "faction kills: death skipped" at DeathEventPatch.cs:53/98; Announcer.SendPrivate exists; PlayerTriggerFeed.WantsKills exists. All match the plan. Design §9 D42-D47 match how the plan uses them, and no finding repeats one.

missing components: none
undeclared families: none
14/15 layers · 49/50 probes
VERDICT: READY

### Dispositions
- F1 · accepted · deferred to the Log (advice on a READY review, no D-item edited): `note · deferred · Review 3 F1`, the step-1 `note · planted · D<n>` lines for the new test classes' failing cases (D1-D15, D17) run as Failure & observability 12.4 states, and `dod status revenge` shows each one before step 1's post-audit closes
- F2 · accepted · deferred to the Log (advice on a READY review, no D-item edited): `note · deferred · Review 3 F2`, step 2's pre-audit records a fresh `pwsh tools/preflight.ps1 -TimingSpan` baseline, so a D23 failure can be laid on revenge or on wave-sets A7; D23 still gates the release
- F3 · accepted · deferred to the Log (advice on a READY review, no D-item edited): `note · deferred · Review 3 F3`, Logic/Revenge.cs keeps a player's cooldown (D5) and warning state outside the evictable heat rows (D3), so an eviction never lets a player gather heat during the cooldown or drops a due player's row; any other eviction rule is recorded as an amendment before it is built
- F4 · accepted · deferred to the Log (advice on a READY review, no D-item edited): `note · deferred · Review 3 F4`, an admin start (D13) skips the target-busy check and sets no cooldown for the admin, checked in RevengeTests AdminStart; recorded as an amendment if it changes a promise
- F5 · accepted · deferred to the Log (advice on a READY review, no D-item edited): `note · deferred · Review 3 F5`, docs/features/REVENGE.md states that a spawned squad whose focus moved more than `range` from its wave centre idles or leashes until the event ends or the wave is cleared (accepted, HuntPlan.Targets seeds only players within range), and D22's owner look check notes it
- F6 · accepted · deferred to the Log (advice on a READY review, no D-item edited): `note · deferred · Review 3 F6`, a heat or factions edit keeps a row's existing points and changes only what new kills add, and the next scan applies the new `heat` (a player at or above it becomes due without a new kill); one case in RevengeTests Heat; recorded as an amendment if it changes a promise

## Review 4 · 2026-10-13 · subagent · plan uncommitted · plan 91953 B · 32 items · files 0 · e3b0c44298fc · prompt 95d13c4d986d
L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · F7
L4 · Considered · F1, F2, F4, F5
L5 · Considered · F6
L6 · Considered · F2
L7 · Considered · no finding
L8 · Considered · no finding
L9 · Considered · no finding
L10 · Considered · no finding
L11 · Considered · no finding
L12 · Gap · F1, F8 (the author already counts this as 49/50: the failing cases of the new test classes, now including D28-D31, were never run before approval and are planned as step 1 `note · planted` lines; not a new Gap)
L13 · Considered · no finding
L14 · Considered · F3, F6, F7
L15 · Considered · no finding

F1 · advisory · probes 4.1 and 12.4 (D30). D30's evidence cannot catch the most likely way its promise breaks. In `Patches/DeathEventPatch.cs:63-67` the "ours" ledger read (`SpawnTracker.IsOurs`) runs only `if (wantsKills)`, and it runs in `before`. The VBloodKilled path (`:77-80`) runs in `rest`, after `SpawnTracker.Died` has forgotten the victim. If D30 reuses `read.Ours`, a tracked boss still raises VBloodKilled whenever no FactionKills or Revenge definition is startable, which is the default setup. `DeathRuleTests EventUnits` tests only the pure `DeathRule` function, so it passes anyway. D32 can hide the bug too, because nothing stops rv-test or rv-test0 (or bandit-ambush for D23) from being startable when bw-test runs.
  Fix: add to D30's fails-when: "a tracked V Blood raises VBloodKilled while no FactionKills or Revenge definition is startable", tested through `DeathPass` order (ours read before Died, whatever wantsKills says). Pin D32 to run with no Revenge or FactionKills definition startable.

F2 · advisory · probes 4.4 and 6.2 (D10 against D30). When the ours read throws, D10 says a throw "never skips … the V Blood trigger". D30 says an event unit never raises VBloodKilled. One of the two must give way, and the plan does not say which. In practice the throw is rare: `IsOurs` checks the ledger first, so a tracked boss never reaches the buffer read that can throw. The exposed case is a marker-only unit, such as a not-yet-swept orphan.
  Fix: one sentence in D30: "a failed ours read raises VBloodKilled as before (fail open, D10); a ledger hit never does."

F3 · advisory · probe 14.4 (D26). Rollout › Paths walked › Step 3 lists `tools/rollback-drill-fixtures/{pair-revenge,pair-othertrigger}/**` but not `pair-bosses`, which A1, D24 and Build plan step 3 all write. `-Paths -DeclaredOf revenge` would then report an undeclared path at step 3.
  Fix: make it `{pair-revenge,pair-bosses,pair-othertrigger}/**`. Do it before approval, since an edit after READY voids it.

F4 · advisory · probe 4.5 (D28, D29). D28 promises bosses "with any trigger and location". A Region location's wave goes through `RegionWave.Decide`/`Split` (`Logic/HuntingGrounds.cs:228-252`), which deals units round-robin into groups of 5 at separate spots. D29's "beside that wave's own units at its spot or ring" does not say which group a boss joins. Also, `Logic/HuntingGrounds.cs` and `Logic/Precedence.cs` (`WaveSize`) are missing from Paths walked step 1, though the clamp-bosses-last rule may land there.
  Fix: state that a Region wave's bosses join its first group with a spot (or similar), and add both files to step 1's paths.

F5 · advisory · probe 4.1 (D28 wording). D28 says a boss entry's modifiers are "as a units entry's". In the units form, entries are parsed `withModifiers: false` (`Logic/Validation.cs:548`). Only waveList entries carry modifiers. For a growth wave, D29's "else the wave's" leaves open whether a boss without modifiers inherits the generated wave's addLevels.
  Fix: say "as a waveList entry's modifiers" and "a boss without modifiers takes the final wave's modifiers, growth's added levels included", or the opposite.

F6 · advisory · probe 5.3. `Logic/ScoreFeed.cs` (`DeathPass`) and `Logic/DeathPass` ordering may change if the ours read is moved out of the `wantsKills` gate (F1). Neither file is in Paths walked.
  Fix: list `Logic/ScoreFeed.cs` under step 1, or note it is untouched.

F7 · advisory · probe 3.3. Three places still describe the old 0.11.0 release. Design › Data's last row is "tag and GitHub release v0.11.0", but this child creates v0.12.0. S-15 still reasons about 0.11.0 and v0.10.0. Also considered › Localisation says "in 0.11.0".
  Fix: change the Data row to v0.12.0, mark S-15 superseded by A1, and correct the Localisation line.

F8 · advisory · probe 12.4 (D28-D32). The Log has no dry-run note for any A2 item: `BossWaveTests` does not exist yet, and there is nothing for `DeathRuleTests EventUnits` or `TemplateLibraryTests Bosses`. D32's dev event bw-test is described as "3 CHAR_Spider_Forest, waves 2, the second when cleared". The units form has no plain whenCleared (only through growth, which needs adds), so bw-test has to be a two-entry waveList.
  Fix: add `note · dry-run · D28-D31` lines (the current filter output), and write bw-test as a waveList with wave 2 `whenCleared: true`.

Checked and found sound:
- A1: csproj is 0.11.0 and `Wire.Api` is 6, so D15 gives 7. The reason "unknown field action.bosses" matches `Validation.cs:228`'s format exactly. The rollback range is v0.11.0..v0.12.0, the gate is 4/4, and the drill gains three cases.
- A2: `ParseSpawnWaves`' OnlyKeys list is the right place for the new key. `Growth.Generate` and `WavePlan.Split` give the final-wave and clamp seams. `vblood-player-death.vrs:27-32` does rely on a tracked Ungoro raising VBloodKilled, and the plan already moves that half to a native V Blood.

missing components: none
undeclared families: units form, waveList form, growth form; Point, Admin, AroundPlayer, Region
14/15 layers · 49/50 probes
VERDICT: READY
### Dispositions
- F1 · accepted · built: the ours read for the V Blood rule runs before SpawnTracker.Died whatever WantsKills says, and a DeathPass-order case in DeathRuleTests EventUnits fails when a tracked V Blood raises VBloodKilled with no FactionKills or Revenge definition startable; D32's run keeps rv-test, rv-test0 and every FactionKills definition disabled (Log)
- F2 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 4 F2`, a failed ours read raises VBloodKilled as before (fail open, D10); a ledger hit never does
- F3 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 4 F3`, step 3 also writes tools/rollback-drill-fixtures/pair-bosses/**, declared there for -Paths -DeclaredOf
- F4 · accepted · built: a wave's bosses lead its unit list, so a Region wave deals the first boss to its first group with a spot and the bosses are dealt first under the clamp; Logic/HuntingGrounds.cs and Logic/Precedence.cs are read in step 1 (Log)
- F5 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 4 F5`, a boss entry's modifiers are a waveList entry's; a boss without modifiers takes the final wave's tuning, growth's added levels included
- F6 · accepted · deferred to the Log: `note · deferred · Review 4 F6`, Logic/ScoreFeed.cs (DeathPass) is read in step 1 and declared there
- F7 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 4 F7`, Design › Data's release row, S-15 and Localisation's "in 0.11.0" read as superseded by A1 (0.12.0, v0.12.0)
- F8 · accepted · built in the Log: dry-run notes for D28-D31; bw-test is a two-entry waveList, wave 2 `whenCleared: true`
- UF1 · accepted · D29
- UF2 · accepted · D29

## Review 5 · 2026-10-13 · subagent · plan uncommitted · plan 94654 B · 32 items · files 0 · e3b0c44298fc · prompt cc5ae3c70fb5 · scope A1,A2
L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · F3
L4 · Considered · F2, F4
L5 · Considered · F5
L6 · Considered · F1
L7 · Considered · no finding
L8 · Considered · no finding
L9 · Considered · F3
L10 · Considered · no finding
L11 · Considered · no finding
L12 · Considered · F2
L13 · Gap · no finding (frozen context: the 13.1 measurement predates the recon commit and was not re-run; D23 gates the release; outside A1/A2 scope)
L14 · Considered · F5, F6

F1 · advisory · The plan says nothing about what a boss itself summons during a fight. CHAR_Spider_Queen_VBlood has AB_Spider_Queen_SpawnAdds_*, which summon CHAR_Spiderling_Summon (IsMinion, EntityOwner, DestroyAfterDuration 120). Those adds are not SpawnTracker units, they fall outside MaxTrackedUnits, and a purge or restart does not remove them. They disappear on their own within 120 s, so no cap or kill-switch promise actually fails.
  Fix: in docs/features/REVENGE.md, say that a boss's summons belong to the game, are timed by DestroyAfterDuration, and count for no cap, clear or scoreboard. Have D32's scenario check that no CHAR_Spiderling_Summon remains near the pond 120 s after the end.

F2 · advisory · A2's probe 4.2 promises "a clamp to zero room spawns no boss and the wave still runs". D29's fails-when only covers dropping or reordering bosses under a clamp, not the zero-room case.
  Fix: add a BossWaveTests case where MaxTrackedUnits leaves no room: no boss spawns, and the final wave is decided as the existing clamp decides a fully clamped wave.

F3 · advisory · D28 doesn't say whether the bosses count toward validation's existing 50-unit final-wave ceiling (Validation.GrowthLastWave, Growth.LastWaveUnits). Up to 9 bosses are possible (3 entries × count 3). A growth action at 49 units plus bosses could load and then lose units to the runtime clamp.
  Fix: state whether the 50-unit ceiling includes the bosses, and add the matching EventValidationTests Bosses case.

F4 · advisory · Two sets of siblings are checked without being declared. D28/D29 promise "any of the three forms" and "any location", but the `siblings:` line lists only the end paths. D32 runs only one form (waveList) at one location (Admin). The two shipped boss templates use the growth form (spider-swarm) and Region (spider-hunting-grounds).
  Fix: add `siblings: units, waveList, growth; Point, Admin, AroundPlayer, Region` to Business rules, and put `twins:` on D29 and D32 naming the BossWaveTests cases that cover each form and location.

F5 · advisory · Build plan step 1 and Paths walked › Step 1 name Logic/WaveSets.cs, which does not exist (Logic/ holds Growth.cs, Spawning.cs, Scoreboard.cs). The final-wave roll may live in Services/WaveAction.cs or Logic/Spawning.cs. A file touched there and not declared in step 1 would trip D26's -DeclaredOf. Paths walked › Step 3 still names only {pair-revenge,pair-othertrigger}; the pair-bosses fixture appears only in the deferred Log note.
  Fix: replace WaveSets with the real file(s) once step 1's recon finds the roll. Add pair-bosses to the Step 3 fixture glob.

F6 · advisory · D30 changes existing behaviour. A V Blood placed with `.nyar spawn` or by an event used to raise VBloodKilled and so could chain events. After 0.12.0 it cannot. D25's surface list names the bosses key but not this change.
  Fix: have D25 require both changelogs to state that event and `.nyar spawn` V Bloods no longer raise VBloodKilled.

F7 · advisory · Both boss templates are proven in game only through unit tests: spider-swarm's growth final wave and spider-hunting-grounds' Region dealing (Review 4 F4: the first boss goes to the first group with a spot). The level rule "absolute level at or above the final wave's" gives spider-swarm a boss at 42 or more (30 + 4×3) and spider-hunting-grounds one at 30 or more. CHAR_Spider_Queen_VBlood's native level is 63.
  Fix: name the chosen boss levels in D31, and optionally add a Region pass to boss-wave.vrs.

missing components: none
undeclared families: units, waveList, growth; Point, Admin, AroundPlayer, Region
14/15 layers · 49/50 probes
VERDICT: READY
### Dispositions
- F1 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 5 F1`, docs/features/REVENGE.md states that a boss's summons (CHAR_Spiderling_Summon) belong to the game, end through DestroyAfterDuration 120 s and count for no cap, clear or scoreboard; boss-wave.vrs checks that none is left near the pond 120 s after the end
- F2 · accepted · built: BossWaveTests gains the zero-room case (no boss spawns, the final wave decided as a fully clamped wave) (Log)
- F3 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 5 F3`, validation's 50-unit final-wave ceiling stays the wave's own units; the bosses ride the runtime clamp, which D29 keeps them first under
- F4 · accepted · built: BossWaveTests covers the units, waveList and growth forms and the Point, Admin, AroundPlayer and Region locations (Log)
- F5 · accepted · deferred to the Log: `note · deferred · Review 5 F5`, the final wave's roll lives in Services/WaveAction.cs and Logic/Spawning.cs (there is no Logic/WaveSets.cs), declared there for step 1; pair-bosses as Review 4 F3
- F6 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 5 F6`, both changelogs state that an event's or `.nyar spawn`'s V Blood no longer raises VBloodKilled
- F7 · accepted · deferred to the Log: `note · deferred · Review 5 F7`, the templates' boss levels are named in the step 2 Log note when chosen (at or above each final wave's level)
- UF1 · accepted · D29
- UF2 · accepted · D29
