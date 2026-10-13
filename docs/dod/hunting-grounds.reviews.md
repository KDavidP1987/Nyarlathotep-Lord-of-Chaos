# Reviews: hunting-grounds

## Review 1 · 2026-10-12 · subagent · plan uncommitted · plan 94643 B · 32 items · files 0 · e3b0c44298fc · prompt b5ae2b0cdbe7
L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · no finding
L4 · Considered · F3
L5 · Considered · no finding
L6 · Considered · F1
L7 · Considered · no finding
L8 · Considered · no finding
L9 · Considered · F2
L10 · Considered · no finding
L11 · Considered · no finding
L12 · Considered · F4
L13 · Considered · F2
L14 · Considered · no finding
L15 · Considered · no finding

What I checked in the code: `Limits.MaxUnitsPerWave` is 20 (range 1-50); `HuntPlan.MaxTargets` 5 and `HuntPlan.IntervalSeconds` 5 (Logic/Spawning.cs:904-905); `MaxHuntRange` 60 (Validation.cs:732); Hunt's eligibility (online, alive, usable position, not in claimed territory, not in PvP combat) in `HuntPlan.Targets`; `DunleyFarmlands` in Logic/Regions.cs; swarm D18 passed the same "no yo-yo" bar with the drop-and-return method D5 reuses.

F1 · advisory · 6.1 · D20's spike does not test the condition it names ("moved while Chaos stands more than 100 m away"): `.devhop` moves only units within its radius (default 30 m) of the sender, so every spike move happens with a player close by, while the real mover (D8) moves only with no player within 100 m. Fix: a `.devhop` mode independent of the sender's position (a mark name, or a radius around a mark), or reword D20 to "moved, then observed from more than 100 m".

F2 · advisory · 9.1 / 13.2 · Use cases › Maximal stretch says "at most 30 groups a wave"; Performance 13.2 says "at most 10 groups a wave at the ceiling of 50 units", which matches the code (50 / 5). Fix: 10 groups a wave in Maximal stretch.

F3 · advisory · 4.5 · D7's hunt groups are an undeclared family (one group per Point wave, Admin wave, AroundPlayer group or Region spot); D7's fails-when does not check the grouping per location, and D24's `twins:` names D23 and D11, neither of which runs RegionHunt. Fix: a `siblings:` line, a D7 fails-when per location, and D24's twins corrected.

F4 · advisory · 12.4 · D24's "`.nyar debug here 49` at the first check after their last jump lists none" is timing-dependent: units land 50-60 m out and the Hunt seeding walks them in within seconds. Fix: check the landing distance from a counts-only log value, or read `debug here 49` in the same tick as the jump line.

F5 · advisory · 6.2 / 7.1 · A target in a concave or multi-polygon scope can sit where the straight line leaves the regions; every landing on that line fails "inside a scope region", the group waits, and D26's "arrive within about 2 minutes" fails on such a map. D8 handles it (the unit waits), so not a blocker. Fix: an Open question in HUNTING_GROUNDS.md; a sidestep landing as an amendment if a session sees it.

missing components: none
undeclared families: Point hunt group, Admin hunt group, AroundPlayer hunt group, Region hunt group

15/15 layers · 50/50 probes

VERDICT: READY

### Dispositions
- F1 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 1 F1`, the builder writes `.devhop` with a mark-anchored mode (Build plan step 1) so every spike hop runs with Chaos more than 100 m away; D20's wording governs the verdict.
- F2 · accepted · applied: Maximal stretch reads 10 groups a wave.
- F3 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 1 F3`, the four hunt groups are built and unit-tested per location in D7's test class at step 2 (a test per location that fails when a group is split or merged); D24's twins line stays as frozen text, the Admin and AroundPlayer RegionHunt runs are covered by D7's tests, not in game.
- F4 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 1 F4`, the builder adds a counts-only "landed <d> m" value to the region hunt log line, and D24's scenario reads it in place of a late `debug here 49`.
- F5 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 1 F5`, recorded as an Open question in docs/features/HUNTING_GROUNDS.md when it is created (step 1); a sidestep landing is an amendment if a session sees a stuck group.
- UF1 · accepted · D7 (deferred to the Log with F3, `note · deferred · Review 1 F3`): D7's test class gets one test per hunt group (Point, Admin, AroundPlayer, Region) that fails when a group is split or merged; no item is edited after the READY

## Review 2 · 2026-10-13 · subagent · plan uncommitted · plan 133610 B · 32 items · files 0 · e3b0c44298fc · prompt 2ef73e0ec8cd · scope A4,A5,A6
L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · no finding
L4 · Considered · F1, F4
L5 · Considered · F2
L6 · Considered · F1, F2
L7 · Considered · F5
L8 · Considered · no finding
L9 · Considered · no finding
L10 · Considered · F3
L11 · Considered · no finding
L12 · Considered · F1, F2
L13 · Considered · no finding
L14 · Considered · no finding
L15 · Considered · no finding

F1 · advisory · 12.4 / 4.2. A6's test (`Lock_fails_when_a_return_lands_in_a_castle`) protects only half of the control. Its first loop records the last inside spot at x 60, 30 m from the castle edge, so a return within 10 m can never reach the castle. The second loop calls `LockRule.ReturnSpot` with the test's own `InsideFree` lambda, not the one `LockPhase.Run` builds. If you remove `&& !castle(px, pz)` from LockPhase's return filter (`HuntingGrounds.cs:741`) and keep the record check, the test still passes. Yet the return filter is what stops a return landing in a castle when the last unclaimed spot is within 10 m of a castle wall. The code is correct today; only the guard is incomplete.
Fix: add a LockPhase case whose last unclaimed spot sits within 10 m of the claimed edge (say x 35 against a castle at 0–30), and plant "return filter dropped" separately from "record check dropped" in the Log.

F2 · advisory · 5.2 / 12.4. A5 writes `PreCombatPosition` and `SpawnTransform.Position` in `HuntAction.Rehome` (Services, no test seam). The planted fault was only `HuntRule.Home` returning null. If the `Rehome(...)` call at `HuntAction.cs:259` were removed, or one of its two writes dropped, no unit test would fail. Only the vrclient `debug here 15` step in `hunting-grounds-hunt.vrs` would catch it.
Fix: say in A5 or the Log that the Services half is guarded in game by that scenario step (Session 3, PASS 19/19), as the plan does for its other Services-only writes. Or move the "write only when it moves 2 m or more" choice behind the `IRegionWorld`-style seam.

F3 · advisory · 10.4. A5 puts the nearest seeded player's exact position (x, y, z) into the unit's `AggroConsumer.PreCombatPosition` and `SpawnTransform.Position`. It stays there until the next check or the unit's despawn, and can be written into the save while the unit lives. D13 says "the hunt target and the players' positions are held for one check in memory only". No log, reply, push or state.json carries it, so D13's evidence and outcome still hold, but its sentence no longer does.
Fix: one clause in A5 or D13: "a seeded unit's return point is its nearest seeded player's position, an entity value of our own unit that no line, reply, push or file of the mod reads out".

F4 · advisory · 4.1 (text outside the re-review scope). After A4, other plan text still says what A4 replaced:
- Business rules 4 says "Within 60 m the existing Hunt seeding hands the unit to the game … Between 60 and 100 m nothing moves the unit … a unit that lost its target waits".
- S-17 says "between 60 and 100 m nothing moves a unit".
- D7 carries A4's 100 m rule but not A5's return-point rule or A5's fails-when.

The `HuntTargets` filter already runs A5's test (the D7 pass shows 11/11), so no item would pass while A5 fails.
Fix: make Business rules 4 and S-17 say "seeded within 100 m (A4), its return point at that player (A5)". Fold A5's sentence and fails-when into D7, as A4's already is.

F5 · advisory · 7.3. In A6, when no other free spot is found, `LockRule.ReturnSpot` falls back to `last` without a claimed check. Last spots are recorded unclaimed, but the territory map is rebuilt at each wave. If a castle heart claims ground around a recorded spot mid-event, a unit with no other free spot nearby can be returned onto newly claimed ground.
Fix: build work, not a decision. Check `last` against the event's current map in the fallback and use the spawn point when it is claimed now. Record it as a Log note or amendment if built.

Checked and found sound:
- **A4 boundaries match.** Seeding uses `<= 100` (`HuntPlan.Targets`) and the watch uses `<= WatchRange`, so a unit at exactly 100 m is watched and seeded, and at 100.5 m it is neither. Hunt keeps `MaxHuntRange` 60 (`SeedReach` is used only in `HuntRule.SeedTargets`).
- **A5 does not affect the lock's spawn point.** `u.Spawn` comes from SpawnTracker's `_meta`, not from `SpawnTransform`, so A5's write leaves the lock's first inside spot alone.
- **A5 points stay out of castles.** The player read sets each player's territory flag from that event's own map, so a new return point is never outside the regions or in claimed territory.
- **A6 matches what it claims.** Every event without allowTerritory builds a map (`WaveFacts.NeedsMap … || !AllowTerritory`). With allowTerritory no map is built and `castle` is constantly false, as A6 says. A failed build makes every point count as claimed, so a return goes back to the spawn point (degraded, but never into a castle).

missing components: none
undeclared families: none
15/15 layers · 50/50 probes
VERDICT: READY
### Dispositions
- F1 · accepted · built: Lock_fails_when_a_return_lands_in_a_castle gains a LockPhase case with the last spot 5 m from the claimed edge (x 35 against 0-30), each return outside the castle and within 10 m of that spot; the return filter dropped and the record check dropped are planted separately (Log)
- F2 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 2 F2`, A5's Services half (HuntAction.Rehome) is guarded in game by hunting-grounds-hunt.vrs's `debug here 15` step (Session 3, PASS 19/19), as the plan's other Services-only writes are
- F3 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 2 F3`, a seeded unit's return point is its nearest seeded player's position, an entity value of our own unit that no line, reply, push or file of the mod reads out; D13's outcome holds
- F4 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 2 F4`, Business rules 4 and S-17 read as superseded by A4 (seeded within 100 m) and A5 (the return point at that player); A4 and A5 govern, and their tests run under D7's HuntTargets filter
- F5 · accepted · built: when the return spot is the last spot and it is claimed now (a heart placed mid-event), the unit goes to its spawn point, or is not returned while that is claimed too; Lock_fails_when_a_return_lands_in_a_castle's grown-castle cases, planted (Log)
