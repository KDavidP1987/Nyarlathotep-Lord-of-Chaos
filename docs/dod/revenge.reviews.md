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
