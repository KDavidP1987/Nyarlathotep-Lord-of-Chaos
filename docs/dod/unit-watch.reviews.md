# Reviews: unit-watch

## Review 1 · 2026-10-14 · subagent · plan uncommitted · plan 99661 B · 33 items · files 0 · e3b0c44298fc · prompt ef9f13ecd18f
L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · F5
L4 · Considered · F2
L5 · Considered · no finding
L6 · Considered · F1
L7 · Considered · no finding
L8 · Considered · no finding
L9 · Considered · F3
L10 · Considered · no finding
L11 · Considered · no finding
L12 · Considered · F6
L13 · Considered · F7
L14 · Considered · no finding
L15 · Considered · no finding

F1 · blocking · blocks: outcome · probe 6.2 · Under S-9's fallback (no fight signal), "a unit holding a player target within 30 m counts as fighting", so the plan's own rock case (a spider on a rock above a player) goes to the stall backstop: about 10 min, not D54's about 90 s; D23 puts Chaos 15-30 m from the rock unit, so under the fallback D23's standing line never appears. Fix: narrow the fallback (e.g. a player target whose health changed, or within attack reach) or accept the delay as an owner-visible risk and rewrite D23; D23 must hold under both spike verdicts.
F2 · advisory · probe 4.1 · S-5's "only while a player is within 40 m or holds a player target" adds to D54; say whether 40 m is on x and z or 3D, and show S-5 to the owner at D29.
F3 · advisory · probe 9.2 · the walk check refuses heights below WalkHeight.MinY (-100), so under D3's "unanswered never marks" a unit fallen far beneath the map is never stuck off the ground. Fix: a height below the lowest grounded level or below MinY is off the ground; add it to D3's fails-when.
F4 · advisory · probe 6.1 · D19(c) reads "`.devfight`'s ground column", but step 1's `.devfight` has no ground column.
F5 · advisory · probe 3.1 · `.devstrand … under 30` where ground is below about -70 leaves the walk check's range; choose marks with ground - 30 above -100 and record the ground height.
F6 · advisory · probe 12.4 · the dry runs for D1-D18 say "No test matches"; the planted-fault runs are the failing-case evidence, D18's too.
F7 · advisory · probe 13.1 · the fight read's cost is unmeasured and a damage-system patch runs on every hit server-wide; give D19(a) a per-tick ceiling (e.g. 0.5 ms average) so the spike can say "not usable" on cost.
F8 · advisory · probe 3.3 · roamers have no in-game item; add a roamer strand case to unit-watch-apocalypse.vrs under D26, or note the gap for D29.
F9 · advisory · probe 12.2 · an allowed in-view removal (S-10) logs nothing that says so; add "(in view)" to D16's removed line when a player was within 100 m.

missing components: none
undeclared families: wave-sets, swarm, revenge, hunting-grounds
15/15 layers · 50/50 probes
VERDICT: REVISE

### Dispositions
- F1 · accepted · fixed: S-9's fallback is narrowed to "fought = the unit's own health fell in the window, or it holds a player target within 5 m on x and z"; D4 states it, D23 holds under both spike verdicts (a melee unit on a rock 15-30 m from Chaos, who does not attack, neither loses health nor has its target within 5 m), and the Typical use case says so.
- F2 · accepted · fixed: the 40 m (and every distance of this plan unless stated) is measured on x and z; S-5 is listed for the owner at D29.
- F3 · accepted · fixed: D3 counts a unit below WalkHeight.MinY, or below the lowest grounded level at its x and z, as off the ground; D3's fails-when gains the case.
- F4 · accepted · fixed: step 1's `.devfight` gains a ground column (the walk check's grounded/free reading at the unit).
- F5 · accepted · fixed: the scenarios strand units at marks whose ground height minus 30 stays above -100 and record the ground height.
- F6 · accepted · no change: step 2's and step 3's `note · planted` lines cover D1-D18, D18 included.
- F7 · accepted · fixed: D19(a) gains a ceiling: the fight read averages under 0.5 ms a tick in the spike, else it is "not usable" and the fallback is chosen.
- F8 · accepted · fixed: unit-watch-apocalypse.vrs (Session 2, D26) strands a roamer as well as a horde unit.
- F9 · accepted · fixed: D16's removed line ends "(in view)" when a player was within 100 m at the removal.
- UF1 · accepted · D2 (also D3, D5, D7): the twins lines of D2, D3, D5 and D7 name wave-sets, swarm, revenge and hunting-grounds as families the one rule covers, with D5's landing limits per family (the arena circle, the scope regions, revenge's focus player as the nearest eligible player).
