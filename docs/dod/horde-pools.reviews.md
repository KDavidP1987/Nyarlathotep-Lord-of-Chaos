# Reviews: horde-pools

## Review 1 · 2026-10-24 · subagent · plan uncommitted · plan 46309 B · 18 items · files 0 · e3b0c44298fc · prompt ca7b277d5880

L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · F9
L4 · Gap · F1, F2, F3
L5 · Considered · F6
L6 · Considered · no finding
L7 · Considered · F10
L8 · Considered · no finding
L9 · Considered · F2, F7
L10 · Considered · F11
L11 · Considered · no finding
L12 · Considered · F12
L13 · Considered · F8
L14 · Considered · F5, F13
L15 · Considered · no finding

F1 · blocking · blocks: outcome · 4.5 / R2 — With allowTerritory true, D3/D4/D12 can all pass while a horde still never fights a castle player. Business rule 5's claim that every territory decision in the family goes through IApocalypseGame.IsClaimed or the march step is false: horde units get a HuntTag in Apocalypse.Queue (Logic/Apocalypse.cs ~1224), and Services/HuntAction.cs Candidate (line 355, HuntRule filter at Spawning.cs:939) marks castle players InTerritory and never seeds aggro on them, reading no AllowTerritory. UnitWatch.Context (Logic/UnitWatch.cs:976) reads Definition.Action?.AllowTerritory, always false for an apocalypse definition. So with the switch on, the roam jumps units into the castle but the hunt layer excludes the player; D12's tally line is the infest roam's target count, which would read "targeted". Fix: add HuntAction seeding for apocalypse-family units and UnitWatch's landing rule to the 4.5 every-set and the paths walked, and make D4/D12 fail when a castle player gets no aggro from horde units with the switch on.
F2 · blocking · blocks: outcome · 4.4 / 9.2 — Maximal stretch 9.2 promises "a library pool mixing fighting factions makes events naming it fail the mixing check", and D6 leans on it; but FactionGroups.Problem has no load or runtime caller (only Nyarlathotep.Tests/FactionGroupsTests.cs). An operator's mixed pools.json pool would load and its units would fight in game. Making it a load check is a policy choice (it would also refuse any unit outside the nine groups). Fix: decide whether a library pool (or an event naming it) spanning two groups or holding an ungrouped unit is refused at load, write it into pools.json/event validation and D5 or D6's fails-when, or drop the 9.2 claim.
F3 · blocking · blocks: outcome · 4.1 / R5 — D7's random count is undefined for two of the three horde types: in Apocalypse and Infestation a units entry's count is a weight (RoamerPlan.Draw, count × chance, Logic/Apocalypse.cs:288-322), and size comes from hordes/roamers; "roll a count in min..max then draw that many" has no meaning there. Only SpawnWaves and HordeMarch sum entry counts. The schema is part of the Raphael handoff (D9), so the builder must not pick it alone. Fix: state what min/max means on a weighted entry (refused with a named reason, weight = max, or other) and add that case to D7's fails-when.
F4 · advisory · 4.1 — HordeMarch's 10-500 bound is checked on the sum of maxes; a horde whose mins sum to 2 passes and can rise with 2 units. Fix: also bound the sum of mins by 10, or state a small roll is allowed.
F5 · advisory · 14.2 / R1 — Rollback to 0.14.0 holds (OnlyKeys/PoolEntryKeys/NoPool refuse the new keys). But R1 on the owner's own server: G-Portal's cfg has MaxTrackedUnits = 150 written, so the owner's 200-horde still comes out at 150 after the upgrade. Fix: add the G-Portal cfg raise to 500 (with the owner's OK) as a step 4 line.
F6 · advisory · 5.1 — "Reference Data's V Blood flag as already carried by the unit catalog" is false: IUnitCatalog exposes only IsKnown/IsDenied, and unit_index.tsv is gitignored and absent on servers. Fix: make the bosses/units split a compiled table, checked by a test against unit_index.
F7 · advisory · 9.1 — With hordes held only by MaxTrackedUnits, one 500-horde takes every slot and starves waves, revenge and other events while it runs. Fix: state it is accepted, or reserve slots.
F8 · advisory · 13.2 — SpawnWaves' PastWaveSize check (Validation.cs) sums Count; with min/max entries it must sum max. Fix: name it in step 2.
F9 · advisory · 3.3 / 10.1 — "pools.json and its siblings" in the Persistence file-write fence is vague. Fix: list the exact names the fence allows.
F10 · advisory · 7.3 — A run's kept territory map (MapTerritory) is built once at start; a castle claimed later is missing from it. Fix: state it is accepted, or rebuild per refresh.
F11 · advisory · 10.1 — Raphael's editor may strip or refuse min/max and horde-type allowTerritory when saving an event back. Fix: in D9's handoff, require Raphael to keep unknown keys.
F12 · advisory · 12.4 — Planted faults are scheduled only for D1, D3, D5, D6 and D10; D2, D4, D7, D8, D9 and D11 have none. Fix: add one planned plant each.
F13 · advisory · 14.4 — Step 2's paths miss Precedence.cs (StartCut), Model.cs, DefinitionEditor.cs, Apocalypse.cs (HordeSchedule), Persistence.cs, HealthMonitor.cs, EventStore.cs and the boot wiring; D16's paths check would catch it. Fix: add them to Paths walked.

missing components: none
undeclared families: SpawnWaves, HordeMarch, Infestation, Apocalypse; ApocalypseHost roam and infest, HuntAction seeding, UnitWatch landing, march step, spawn placement

14/15 layers · 49/50 probes
VERDICT: REVISE

### Dispositions
- F1 · accepted · Business rules 5 now names five territory decision sites (the roam and infest moves, the march step and arrival, spawn placement, HuntAction's candidate filter, UnitWatch's landing rule), all reading the switch; D3 says the hunt layer seeds no aggro on a castle player when off; D4 and its fails-when require aggro seeded by horde units in hunt range when on; D12 checks both tallies; step 1 and Paths walked name HuntAction.cs, Spawning.cs, UnitWatch.cs, WaveMarches.cs, Destination.cs
- F2 · accepted · decided as S-10 (reversible): a library pool spanning groups or holding an ungrouped unit loads with a boot warning "[nyar] pool <name> mixes groups: <groups>", shown again by `.nyar pool info`, matching event pools today (no load refusal); 9.2 rewritten; D5 and D8 fails-when cover the warning; D6 now names the test-time template check only
- F3 · accepted · D7: min/max applies where count is a number of units (SpawnWaves, HordeMarch); on an Apocalypse or Infestation entry it is refused with a named reason, added to D7's fails-when; the handoff (D9) describes the same
- F4 · accepted · D7: a horde's sum of mins must be ≥ 10 and its sum of maxes ≤ 500, in the fails-when
- F5 · accepted · step 4: with the owner's OK and the server stopped by them, G-Portal's MaxTrackedUnits raised from 150 to 500 and read back
- F6 · accepted · D5 and Interfaces 5.1: a compiled bosses table beside the groups, compared with unit_index.tsv by the test where Reference Data exists
- F7 · accepted · Business rules 1 states it, S-11 records it as reversible with a reserved-slots fallback
- F8 · accepted · D7 and step 2 name SpawnWaves' wave-size check summing maxes
- F9 · accepted · Security 10.1 lists pools.json, .bak, .tmp and .corrupt (DataFile.Pools)
- F10 · accepted · States 7.3 and S-12: the map is built at start and resume, as in 0.14.0
- F11 · accepted · D9: the handoff requires Raphael to keep keys it does not know when saving an event back
- F12 · accepted · deferred dry-run notes plant faults for D2, D4, D7, D8, D9 and D11
- F13 · accepted · Paths walked: step 1 also and step 2 lists extended
- UF1 · accepted · D7 (SpawnWaves, HordeMarch counted; Apocalypse, Infestation refused)
- UF2 · accepted · D3 D4 (the five territory decision sites of Business rules 5)

## Review 2 · 2026-10-24 · subagent · plan uncommitted · plan 50508 B · 18 items · files 0 · e3b0c44298fc · prompt 593b335a5cf6

L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · F1
L4 · Considered · F1, F2
L5 · Considered · no finding
L6 · Considered · no finding
L7 · Considered · F3
L8 · Considered · no finding
L9 · Considered · F1
L10 · Considered · no finding
L11 · Considered · no finding
L12 · Considered · F5
L13 · Considered · no finding
L14 · Considered · F4
L15 · Considered · no finding

F1 · advisory · 9.2 / 4.4 — UnitDenyList is admin-extensible (Validation.cs:15 doc comment); an admin denying one seeded member makes the whole pools.json invalid under D5, dropping all 17 library pools and invalidating every event naming one. Consistent with the stated policy but harsh. Fix: state whether a denied member is dropped (with the warning line), only its pool refused, or the whole file refused as now, and add the case to D5's fails-when.
F2 · advisory · 4.1 — D3 refuses a start whose rise point ("a Point or the admin's spot") is on claimed ground, but says nothing of an Apocalypse hotspot or Infestation origin other than the first lying on claimed ground; D3's "no spawn lands on a claimed block" protects castles, but a start may then rise short with no reply naming why. Fix: say whether such a hotspot or origin is skipped (and logged) or refuses the start.
F3 · advisory · 7.3 — S-7 re-resolves pools at resume from the current file; the plan does not say what a resumed event does when a library pool it drew from has since vanished or become invalid (a HordeMarch mid-march). Fix: state that the resumed event follows the existing invalid-at-resume path (stopped, named reason) and add it to D6 or D8's fails-when.
F4 · advisory · 14.2 — Nothing checks that the seed file this release writes validates under this release's own rules (name rule, member limit, deny list, the service catalog's component exclusions). Fix: add "the seeded file fails its own validation" to D5's fails-when.
F5 · advisory · 12.4 — D12's command runs scenarios/horde-pools.vrs relative to the cwd; it resolves only from tools/vrclient. Fix: name the cwd or the full path in D12's evidence command.

missing components: none
undeclared families: none
EARLIER: all resolved

15/15 layers · 50/50 probes
VERDICT: READY

### Dispositions
- F1 · accepted · advisory on a READY review, logged as deferred: decided in step 2 (S-10's warning path), the build's choice kept within D5
- F2 · accepted · advisory, logged as deferred: a claimed hotspot or origin is skipped and named in the start reply, built in step 1
- F3 · accepted · advisory, logged as deferred: a resumed event whose library pool is gone follows the existing invalid-at-resume path, built in step 2
- F4 · accepted · advisory, logged as deferred: PoolLibraryTests File also validates the seed under the release's rules, in step 2
- F5 · accepted · advisory, logged as deferred: the scenario runs from tools/vrclient (python vrclient.py run scenarios/horde-pools.vrs), as every scenario does
