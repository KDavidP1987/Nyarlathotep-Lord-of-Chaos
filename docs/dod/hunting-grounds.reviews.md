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
