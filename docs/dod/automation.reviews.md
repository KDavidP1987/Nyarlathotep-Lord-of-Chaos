## Review 1 · 2026-09-29 · subagent · plan uncommitted · plan 72990 B · 31 items · files 0 · e3b0c44298fc · prompt 2f3a16e9ee7b
Reviewer: fresh-context Claude subagent (general-purpose), read-only
F1 blocking · 4.4 — S-4 lets each fanned-out group take up to MaxUnitsPerWave, so one wave can queue maxInstances × MaxUnitsPerWave units (10 × 20 = 200 before the tracked cap); that contradicts Config/Settings.cs ("Hard cap on units in a single wave, regardless of what an event definition requests") and the Epic's automation constraint ("every unit still under MaxUnitsPerWave"), and Business rules 8 says "the caps come before the fan-out's own limits"; a child cannot redefine an Epic safety cap by a reversible assumption — take it to the owner and record it validated: either (a) the wave total is clamped to MaxUnitsPerWave and split across groups, or (b) the owner approves the per-group meaning with the cfg description and Epic constraint updated and D6's fails-when matched.
F2 advisory · 9.2 / 4.2 — the per-player key is HuntAction.KeyOf(character) (entity Index+Version); DEV_REMINDERS #29 says entity handles are not stable across relog, so "a relog does not reset the cooldown" (D9, Business rules 3, Maximal stretch) will likely fail in game and D9's test with stable fake keys cannot catch it — key the cooldown rows and kill counters on an identity that survives relog (the User entity or the platform id, in memory only under D19) and add "relog inside the cooldown starts nothing" to D12's manual steps.
F3 advisory · 12.2 / 13.2 — D14's refusal throttle lives in PlayerTriggerGate, but EventRuntime.StartEvent logs every refusal itself (EventRuntime.cs:111 and :120), so the one-line-per-60-s promise fails in Services and TriggerRules cannot detect it — in step 2 StartEvent takes a suppress flag or returns the reason unlogged for player-action triggers; say so in D14.
F4 advisory · 3.1 — D10's reason "faction <name> cannot be named" differs from the existing validator's text for the same rule, "faction <name> is deny-listed" (Validation.cs:420) — reuse the existing text or say why not.
F5 advisory · 7.3 / 9.3 — the plan does not say what happens to a FactionKills counter that fires while its event is active (PlayerTriggerGate drops it); whether it is emptied or kept decides a burst of starts right after the end or none — state that a dropped fire still empties the counter and add the case to D11 or D14.
F6 advisory · 4.1 — D9 requires the previous region to be "known" without saying whether RegionNames.None (the unmapped gaps) counts as known; a player crossing a gap into the scope would not trigger if it does not — state that None is a known region outside every scope; only a row missing from the scan is unknown.
F7 advisory · 9.1 / 13.2 — kill memory is bounded per definition (200 counters × kills) but not across definitions: 100 FactionKills definitions at kills 500 hold 10 M timestamps — cite an existing definition-count limit or add a total bound covered by D11.
F8 advisory · 4.4 evidence — D14's TriggerRules fails-when has no case where swapping "cooldown before dedupe" or "active-drop before conditions" changes the result — add one fixture where the order changes the outcome, or state that the order has no observable effect.
F9 advisory · 14.4 — docs/dod/automation.md gets evidence lines in steps 1–3 but Paths walked lists it only under step 4 — add it under "Steps 1-4".
F10 advisory · manual verifiability — D7's "its units stand in five groups" cannot be checked from logs, since D19 keeps positions out of every wave line — say how the phantom groups are observed (the owner travels to each 200 m step with `.nyar debug here`, or a Debug-only line gives group centres for phantoms only).
F11 advisory · 3.4 / D31 — StateDocument holds dates as Dictionary<string, DateTime>, and System.Text.Json rejects the whole document on one bad value, so "one bad entry drops only that entry" needs a custom converter — name the converter in step 1 so D31's test has a real seam.
Scenarios: F2's relog (9.2); a RegionEntered definition with a chance condition re-rolled by walking in and out every 5 s, since the cooldown counts only from an entry that started the event (9.2: start the cooldown from any accepted entry, or accept and state it); a FactionKills fire while the event is active (F5, 7.2).
Gating evidence: 2.1 and 10.1 D20; 3.3 and 14.4 D25; 4.4 D14 (partial, F8); 6.2 D15 and D28; 10.3 D26; 12.4 D30 and the check table; 14.3 D23.
14/15 layers · 48/49 probes
VERDICT: REVISE
### Dispositions
- F1 · accepted · the owner chose option A in plan mode 2026-09-29: the whole fanned-out wave is clamped to MaxUnitsPerWave and the free MaxTrackedUnits and dealt round robin to the groups; S-4 is now validated; D6, D7, D18 (roaming-hunters 3 × 6) and Business rules 2 and 8 matched
- F2 · accepted · cooldown rows and kill counters keyed on the User entity's platform id held in memory only (D19), the character key kept for the focus pick only; D9 and D11 say so; D12 gains the relog-inside-the-cooldown step
- F3 · accepted · D14: StartEvent returns the refusal unlogged for a player-action start (a quiet flag), and PlayerTriggerGate's throttle writes the one line; step 2 names it
- F4 · accepted · D10 reuses "faction <name> is deny-listed"
- F5 · accepted · D11: a fire dropped because the event is active still empties the counter
- F6 · accepted · D9: RegionNames.None is a known region outside every scope; only a key missing from the scan is unknown
- F7 · accepted · D11: a total bound of 2000 counters across all definitions, the least recently updated dropped first
- F8 · accepted · D14 gains the fails-when case "an active event's trigger reaches the conditions (the chance roll is consumed)" and "a cooldown-dropped entry sets the dedupe key"
- F9 · accepted · Paths walked › Steps 1-4 lists docs/dod/automation.md
- F10 · accepted · D7: a Debug-only verbose line "fanout <id> wave <k>: phantom group <i> at <x>,<z>" names phantom centres only (never a real player's); the owner checks the real group with `.nyar debug here`
- F11 · accepted · step 1 names a StateDocument NextInterval converter (NextIntervalConverter) that drops a bad entry; D31's test uses it
- Scenario (chance re-roll) · accepted · D9: the per-player cooldown starts at any entry that reached a start attempt, whether the conditions allowed it or not

## Review 2 · 2026-09-29 · subagent · plan uncommitted · plan 75690 B · 31 items · files 0 · e3b0c44298fc · prompt f20e0653943c
Reviewer: fresh-context Claude subagent (general-purpose), read-only
F1 advisory · 13.1 — D21 does not measure the per-death cost of the kill rule: the kill feed runs in the DeathEventListenerSystem postfix, outside the EventScheduler tick the timing windows time, and with only the owner killing few deaths occur — say in Performance that D21 bounds only the scheduler tick and that the per-death cost is bounded by construction, or add a Debug timing counter.
F2 advisory · 5.1/5.2 — PlayerQuery.Read does not return a platform id today: PlayerRow (Services/HuntAction.cs:203) carries an entity Index/Version key, while D9, D13 and D14 key on the platform id and D5's focus must match a candidate by it, so PlayerRow and PickCandidate change, which Interfaces › Writes does not list, and step 1's tests depend on it — add it to 5.2 and move the model change into step 1.
F3 advisory · 5.3 — StateDocument serializes camelCase (Logic/DataStore.cs:116-120), so a NextInterval property is written nextInterval, reads are case-sensitive, a fixture spelled NextInterval would be silently ignored, and an initialized empty dictionary writes {} against D31's "gains no key" — give the key as nextInterval or pin it with [JsonPropertyName("NextInterval")], null or ignored when empty.
F4 advisory · 12.4/14.3 — nothing makes the drill's 0.8.0 state.json contain the new key, so "the N-1 boot fails on the new state key" can pass without testing anything — pair-interval plants a real NextInterval entry written by 0.8.0 and the drill asserts its presence.
F5 advisory · 7.1/8.1 — a player crossing a narrow region inside one 5 s scan is never seen inside it and makes no entry — state in Business rules 3 that a transit shorter than a scan is not an entry, by design.
F6 advisory · 4.3 — a wall-clock jump forward makes every Interval next due at once; D2 guards only boot and a shortened definition — state that a clock step forward fires the due nexts once, bounded by MaxConcurrentEvents, the rest redrawing.
F7 advisory · 9.2/2.3 — with shared true the last killer is the focus, so one player can take the last kill to steer the wave — state it under Abuse as accepted.
F8 advisory · 3.3 — the Data table's kill-counter row omits D11's 2000-counter bound and "definition removed" — add both.
15/15 layers · 49/49 probes
EARLIER: all resolved
VERDICT: READY
### Dispositions
- F1 · accepted · Performance: D21 times the scheduler tick only; the kill rule's per-death cost is bounded by construction (returns at once without a startable FactionKills definition, then a fixed number of reads and a set lookup per definition, bounded counters)
- F2 · accepted · Interfaces › Writes: PickCandidate and PlayerRow gain the platform id (memory only); PickCandidate's change moves into step 1
- F3 · accepted · D31 and 5.3: the key is pinned with [JsonPropertyName("NextInterval")], null and left out when empty; D31's fails-when gains the spelling and "{}" cases
- F4 · accepted · D23: the gate's 0.8.0 run holds a waiting Interval definition so its state.json carries a NextInterval entry, and the gate asserts it before the N-1 boot; step 4 and Paths walked name tools/rollback-gate.ps1
- F5 · accepted · Business rules 3: a transit shorter than one scan is not an entry, by design
- F6 · accepted · Business rules 1: a clock stepped forward fires each passed next once, bounded by MaxConcurrentEvents, refused ones redraw
- F7 · accepted · Maximal stretch › Abuse: the shared last-killer focus is stated and accepted
- F8 · accepted · Design › Data kill-counter row gains the 2000 bound and the definition leaving the startable set

## Review 3 · 2026-09-29 · subagent · plan uncommitted · plan 77692 B · 31 items · files 0 · e3b0c44298fc · prompt 5b9298c650d0
Reviewer: fresh-context Claude subagent (general-purpose), read-only
F1 · advisory · 10.4 / D7 against D19. The Debug-only verbose line "fanout <id> wave <k>: phantom group <i> at <x>,<z>" gives away where the real player is. D29 places each phantom at the first real player's z and height, at x + 200·i, so every phantom group centre sits within maxDist of a spot that is a fixed offset from the real player. That breaks the inherited Epic D16 rule ("no line names or locates a player"), and D19's PrivacyTests would either miss this line or fail on it. Fix: log each phantom group's offset from its phantom (or its distance from it), not absolute coordinates. Alternatively, state that Debug-only verbose lines are exempt from D19 and have PrivacyTests confirm that exemption is limited to `#if DEBUG`.
F2 · advisory · 14.4 / D23 and step 4. The step puts "an enabled Interval definition in the 0.8.0 run and the NextInterval presence assertion" in tools/rollback-gate.ps1. That script only orchestrates. The events.json edit, the N boot until state.json is written, and the N-1 boot all happen in tools/rollback-drill.ps1 (lines 279-310), and the gate calls the drill without -Plan. Fix: put the Interval seeding and the NextInterval assertion in rollback-drill.ps1. Say whether it runs for every future pair (N ≥ 0.8.0) or only when the gate is given a plan, and add a drill selftest case for it.
F3 · advisory · 14.4. Step 1 changes the positional record struct `PickCandidate`, but several files that construct it or `PlayerRow` are not in Paths walked: - Nyarlathotep.Tests/DependencyFailureTests.Spawns.cs (5 sites) - Nyarlathotep.Tests/PrivacyTests.EventSpawns.cs - Services/WaveAction.cs, which is listed only under step 2, yet step 1 would not compile without it Fix: declare these files under step 1, or give the new platform-id field a default value so that existing call sites compile unchanged. Also declare Logic/Limits.cs if the 5 s scan, 60 s throttle or 200/2000 bounds become constants there.
F4 · advisory · 12.4 / D21. `-TimingSpan` checks tracked count, hunt targets and window averages. It cannot tell whether the "player triggers" phase ran or whether a FactionKills definition was live. A span recorded with both disabled still prints the success line, so D21's claim "(so the scan phase and the kill feed run)" rests on prose. Fix: add a condition that fails D21 when the span holds no "player triggers: <p> players" verbose line, or add a `-Phase "player triggers"` requirement to -TimingSpan.
F5 · advisory · 9.2 / 4.1 (RegionEntered). A player who dies outside the scope and respawns at a coffin inside it counts as an entry: the dead player's row is updated and the next scan sees them alive in scope. A waygate or teleport into the region counts the same way. The plan is silent on whether that is intended. Fix: state the rule (teleport and respawn count as entries, by design), or treat a death as clearing the region row so the respawn is a first sighting. Add the case to D9's fixture.
F6 · advisory · 12.4 check table. The table gives failing, silent and empty inputs only for the AutomationTests checks. The validation tests (D1, D4, D8, D10), D16 and D17 give only fails-when conditions, with no silent-input or empty-input case (for example an empty `factions` array, or an empty `trigger` object). Fix: add rows for EventValidationTests IntervalTrigger, FanOutKey, RegionEnteredTrigger and FactionKillsTrigger, and for CommandArgTests Automation.
F7 · advisory · D6 against 0.7.0's WaveOutcome.ZeroRolled. D6 says a fanned-out wave with no spawned group "counts as skipped with 'wave <n> of <id> skipped: <reason>'". In 0.7.0 a wave where every roll comes up empty is its own outcome, ZeroRolled, with its own line. The plan does not say which line a fanned-out wave gives when every group rolled 0. Fix: state that every group rolling 0 keeps the ZeroRolled line, and a mix of claimed and zero-rolled groups gives the first reason in pick order.
F8 · advisory · 13.2 / 3.3. Region rows are bounded by the number of connected players, but cooldown rows ("kept until they expire whatever the player's presence") have no count bound. They can grow up to (distinct entrants within 1440 min) × (RegionEntered definitions). Fix: add a bound the way D11 has one (for example 2000 rows in all, stalest dropped first) and include it in the D9 test.
F9 · advisory · S-7 is labelled reversible. Its fallback, persisting cooldown rows in state.json keyed by a hashed player key, would add a per-player field to state.json. D19 and Epic D16 forbid that, so the fallback is not cheap: it needs an owner decision. Fix: change the fallback to "cooldown rows stay in memory; a restart resetting them is accepted". Or mark the persistence route as needing an owner decision that amends Epic D16.
F10 · advisory · D12. With only the owner online in Session 1, "its first wave centres within maxDist of the owner" cannot tell the focus rule apart from a random pick. The focus rule is proven only by D13's unit test. Fix: note that D12 confirms the start path only and D13 carries the focus proof, or run D12's FactionKills case in Session 2 with phantoms:4.
F11 · advisory · D7. Phantoms are placed at 200 m steps along +x from the owner. Some may land in claimed territory, off the map or in water, in which case fewer than 5 groups spawn and "around 5 players" fails for reasons unrelated to fan-out. Fix: have Session 2 choose an open spot and record it, or have Phantoms.Place skip positions where `PlayerPosition.Usable` fails and log how many phantoms were placed.
F12 · advisory · 9.2. Several accounts in one clan can each set off a RegionEntered event in turn, because cooldowns are keyed per player id. The one-instance rule and the conditions' cooldownMinutes bound this, but the plan does not name the case. Fix: add one line under Abuse naming the bound (one instance at a time plus conditions.cooldownMinutes) and leave per-clan cooldowns to anti-farming.
Gating evidence: 2.1 and 10.1: D20; 3.3: D25 and D31; 4.4: D14; 6.2: D15 and D28; 10.3: D26; 12.4: D30; 14.3: D23; 14.4: D25.
15/15 layers · 49/49 probes
VERDICT: READY
### Dispositions
- F1 · accepted · D7: the Debug-only phantom line gives "<d> m from its phantom", never a coordinate
- F2 · accepted · D23 and step 4: tools/rollback-drill.ps1 seeds drill-interval for every newer release of 0.8.0 or later and asserts the NextInterval entry before the N-1 boot; selftest case interval-seed, 17/17; rollback-gate.ps1 unchanged
- F3 · accepted · PickCandidate's platform id defaults to "", so existing call sites compile; step 1 and Paths walked declare Logic/Limits.cs and the two test files that build it
- F4 · accepted · D21: VerboseLogging on, -TimingSpan counts "player triggers:" lines ("player scans >= 10") and fails without them; step 3 and Paths walked declare tools/preflight.ps1 and its TimingSpan fixture
- F5 · accepted · D9: a respawn or waygate trip into the scope from a known region outside it is an entry, by design; the fixture gains the case
- F6 · accepted · the check table gains rows for IntervalTrigger, FanOutKey, RegionEnteredTrigger, FactionKillsTrigger, CommandArgTests Automation and ContractDocTests Automation
- F7 · accepted · D6: every group rolling 0 keeps the ZeroRolled outcome and line; a skip gives the first group's reason in pick order
- F8 · accepted · D9 and the Data table: cooldown rows bounded at 2000 in all, the stalest dropped first
- F9 · accepted · S-7: the fallback keeps the rows in memory; persisting them would amend Epic D16 and needs the owner's decision
- F10 · accepted · D12 states it confirms the start path; D13's test proves the focus rule
- F11 · accepted · D29: Phantoms.Place skips positions failing PlayerPosition.Usable and logs "phantoms: <placed> of <n> placed"; D7 needs 4 of 4 at an open spot the owner records
- F12 · accepted · Maximal stretch › Abuse names the clan-rotation bound (one instance, conditions.cooldownMinutes) and leaves per-clan cooldowns to anti-farming

## Review 4 · 2026-09-29 · subagent · plan uncommitted · plan 81043 B · 31 items · files 0 · e3b0c44298fc · prompt 8f812e3b7ced
Reviewer: fresh-context Claude subagent (general-purpose), read-only
F1 · advisory · 12.4: the check table leaves out AutomationDependencyFailureTests (D15), PrivacyTests Automation (D19), TemplateLibraryTests Automation (D18), CommandArgTests ChatBytes (D27), PushTests/AuthoringTests Automation and the new `-TimingSpan` player-scan count. Each item has a fails-when, but the table has no empty-input row for them. Fix: add one row per check with its failing, silent and empty input (for example, "no deaths: no read, no streak" for D15, and "no lines: fails" for ChatBytes).
F2 · advisory · 5.2/14.2: step 3 makes `-TimingSpan` print and require "player scans >= <n>". If that is unconditional, event-spawns' existing TimingSpan fixtures and its closed D24 success string ("…targets >= 1, 0 slow ticks") break, because they have no "player triggers:" lines. Fix: gate it behind a new `-MinScans` parameter (default 0, so the existing output stays the same), and have D21's command pass `-MinScans 10`.
F3 · advisory · 6.2/7.3: Patches/DeathEventPatch.cs wraps the whole death loop in one try/catch. If `TriggerBus.Died` (placed before `SpawnTracker.Died`) ever throws past its own guard, `SpawnTracker.Died` is skipped for that death and every later death in the batch. That is a ledger leak, and D15's Logic-level test cannot see it. Fix: capture ledger membership first, call `SpawnTracker.Died`, then call `TriggerBus.Died` with the captured flag in its own try/catch. Or give `TriggerBus.Died` its own try/catch inside the loop, and state the order in step 2.
F4 · advisory · 4.1/9.2: D11 does not say how a kill by a castle servant (or another castle-owned or team-owned killer whose `EntityOwner` is not a player character) is counted. Fix: state that it resolves to no player and counts nothing, and add that case to KillWindows' fixture.
F5 · advisory · 4.1: fan-out says "a HuntTag per group", but not whether each group hunts its own centre player or any player within Hunt range. Fix: add one sentence to Business rules 2 and a check in D7 ("the group of centre g seeds its own player first" or "Hunt's nearest-player rule applies per group").
F6 · advisory · 13.1: the 5 s player scan and HuntAction's 5 s tick each call `PlayerQuery.Read`, so the query runs twice every 5 s. Fix: share one read per 5 s (a cached read in the tick) or note that D21 measures both, so the duplicate is accepted.
F7 · advisory · 14.4: the paths list leaves out the existing feature docs whose behaviour changes: the event-spawns feature doc (AroundPlayer gains fanOut) and possibly docs/DEV_REMINDERS.md or §9 if a decision lands there. CLAUDE.md requires "any docs/features/*.md whose behaviour changed" in the release. Fix: add docs/features/EVENT_SPAWNS.md to step 4's paths and to D24's surfaces.
F8 · advisory · S-7: the fallback reads "fallback: none needed", which is a restatement, not a fallback. The substance (moving to anti-farming, amending Epic D16 by owner decision) is fine. Fix: reword it as "fallback: the anti-farming child persists per-player cooldowns after an owner decision amending Epic D16".
F9 · advisory · 9.2 (hunt scenario): a player can sit just outside a RegionEntered boundary, let the cooldown expire, and step in to draw a wave on demand, repeatedly, every `playerCooldownMinutes`. With the default of 30 that is bounded, but at 0 it becomes "walk in and out every 5 s". Only one instance at a time and `conditions.cooldownMinutes` bound it. Fix: note in Maximal stretch that `playerCooldownMinutes` 0 relies entirely on `conditions.cooldownMinutes` and the one-instance rule, or raise the minimum to 1.
F10 · advisory · 7.2 (hunt scenario): the plan does not say what happens when `.nyar event set` changes an Interval's min/max while a next start is stored in state.json. D2 redraws a next beyond the new max, but a next that is still valid under the old and new ranges is kept, even if the admin shortened the minimum. Fix: one line in States 7.3: "a set that narrows the range keeps a next still inside it", or redraw on any change to the range.
Gating evidence: 2.1 and 10.1 → D20; 3.3 and 14.4 → D25; 4.4 → D14; 6.2 → D15 and D28; 10.3 → D26; 12.4 → D30 and the fails-when of every item; 14.3 → D23.
15/15 layers · 49/49 probes
VERDICT: READY
### Dispositions
- F1 · accepted · the check table gains rows for AutomationDependencyFailureTests, TemplateLibraryTests, PrivacyTests, ChatBytes, PushTests, AuthoringTests and the -TimingSpan player-scan count
- F2 · accepted · applied after approval as amendment A1 (~D21: -MinScans, default 0, D21 passes -MinScans 10), since it changes D21's command
- F3 · accepted · Interfaces and step 2: DeathEventPatch reads ledger membership, calls SpawnTracker.Died, then TriggerBus.Died with the flag in its own try/catch
- F4 · accepted · applied after approval as amendment A2 (~D11: a killer whose owner is not a player character, such as a castle servant, counts nothing)
- F5 · accepted · Business rules 2: each group hunts by Hunt's nearest-player rule under its own HuntTag; D7 is left as is, the rule being HuntAction's existing one
- F6 · accepted · Performance: the duplicate player read is accepted and D21 measures both
- F7 · accepted · Paths walked › Step 4 lists docs/features/EVENT_SPAWNS.md and docs/features/AUTOMATION.md; D24 is left as is, CLAUDE.md's release rule already covers feature docs
- F8 · accepted · S-7's fallback reworded after approval, meaning unchanged, with a Log note
- F9 · accepted · Maximal stretch › Abuse names the playerCooldownMinutes 0 case and its bounds
- F10 · accepted · States 7.3: a next still inside a changed range is kept

## Review 5 · 2026-10-04 · subagent · plan uncommitted · plan 144938 B · 34 items · files 0 · e3b0c44298fc · prompt a219ee77ee7a · scope A18
Reviewer: fresh-context Claude subagent (general-purpose), read-only
F1 · blocking · 14.4: A18 says the manifest gains the temp names, but tools/paths-manifest.txt gained only the two tracked lines and -DeclaredOf never compares temp names with temp: globs, so the clause cannot fail. Fix: add temp: lines and a check, or reword A18 so temp names are checked against Paths walked and the leftover scan only.
F2 · blocking · 14.4: what vrclient wrote outside the repository is undeclared: the dev server's BepInEx/plugins/NyarDevTools.dll and DevChatEcho.dll, and %TEMP%\vrclient\results and shots (cited as Session 1g's evidence); the -Paths walk sees none of them. Fix: list each with its owner and keep/remove rule, and say why each is outside the check.
F3 · advisory · 14.4: %TEMP%\nyarpf.ps1, a scratch copy of preflight, is left over and undeclared. Fix: delete it and name it.
F4 · advisory · 3.3: the new temp folders have no Design › Data row. Fix: add one.
F5 · advisory · 4.5: AUTOMATION.md writes "%TEMP% nyar-s1d-logs" with a space, which the record regex misses. Fix: write %TEMP%\nyar-….
F6 · advisory · 14.4: the review prompts %TEMP%\dod-review-*.txt are in Design › Data but not in Paths walked › Review process. Fix: add them.
F7 · advisory · 4.5: docs/lessons/*.md, tools/vrclient/** and .gitignore* are shared globs, so declared k/k no longer separates this child's writes. Fix: name the files, or say the globs are accepted on purpose.
F8 · advisory · 12.4: A18's fails-when has no observed run. Fix: record a planted run and the current success line.
14/15 layers · 48/49 probes
VERDICT: REVISE
### Dispositions
- F1 · accepted · option (b): A18 now says a temp name is checked against Paths walked and the leftover scan, never the manifest's temp: globs; its fails-when names the three failing cases the check has
- F2 · accepted · Paths walked › Steps 1-4 (A18) names the two dev plugins (dev server only, kept for later vrclient sessions, outside the walk whose server: lines cover only this mod's paths) and vrclient's results and shots (the tool's own output, kept as evidence, outside the nyar-* scan); Design › Data gains the row
- F3 · accepted · deleted; named beside tools/_pf.ps1
- F4 · accepted · Design › Data gains "session and selftest scratch (A18)" and "vrclient outputs (A18, Epic A36)"
- F5 · accepted · AUTOMATION.md Sessions 1d-1f writes %TEMP%\nyar-s1d-logs, -s1e-, -s1f- in full
- F6 · accepted · Review process names %TEMP%\dod-review-*.txt
- F7 · accepted · the shared globs are kept on purpose, said in the A18 bullet
- F8 · accepted · Log: the plant (TestSupport.cs dropped) → "declared: 132/133 in automation, not in its Paths walked: tracked Nyarlathotep/Nyarlathotep.Tests/TestSupport.cs", PREFLIGHT FAILED; restored → "declared: 133/133 in automation"

## Review 6 · 2026-10-04 · subagent · plan uncommitted · plan 147021 B · 34 items · files 0 · e3b0c44298fc · prompt b9d804dd9764 · scope A18
Reviewer: fresh-context Claude subagent (general-purpose), read-only
F1 · advisory · 14.4: the dry-run note's "declared: 133/133" is now 134/134 (an uncommitted change since). Fix: take D25's pass line from the release-time run.
F2 · advisory · 14.4: the dev plugins and vrclient's results and shots are declared in prose only, outside every check; acceptable, dev-only and never shipped. Fix: none for this child.
F3 · advisory · 4.5: the tool temp-name scan leaves out tools/vrclient/**; vrclient.py writes only %TEMP%\vrclient today. Fix: scan it or state the exclusion.
F4 · advisory · 3.3: %TEMP%\nyar_diff.txt (2026-09-29, inside this child's build) is undeclared scratch. Fix: delete or name it.
EARLIER: all resolved
15/15 layers · 49/49 probes
VERDICT: READY
### Dispositions
- F1 · accepted · D25's pass line is taken from the run at release
- F2 · accepted · no change
- F3 · accepted · the exclusion is stated in the A18 bullet
- F4 · accepted · deleted after reading it (a step 2 review diff); named in the A18 bullet
