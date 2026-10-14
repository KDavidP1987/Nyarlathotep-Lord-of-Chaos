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

## Review 2 · 2026-10-14 · subagent · plan commit ca3d670 · plan 102500 B · 33 items · files 0 · e3b0c44298fc · prompt 1bda60c97143
L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · no finding
L4 · Considered · F1, F3
L5 · Considered · no finding
L6 · Considered · F6
L7 · Considered · F1
L8 · Considered · no finding
L9 · Considered · F2, F5
L10 · Considered · no finding
L11 · Considered · F2
L12 · Considered · F6
L13 · Considered · no finding
L14 · Considered · no finding
L15 · Considered · no finding

Probe 6.2 (blocking in Review 1) is answered: Interfaces › External › 6.2 names each dependency's failure (a throwing position, fight or walk read skips the unit with a streak line and a health entry, D18; a throwing move or despawn is a failed try; a spent walk budget waits, D10; no landing is a failed try, D6; a player who never leaves is bounded by S-10; the fight patch is guarded and never skips the system's work or SpawnTracker.Died; sibling plugins untouched; the other movers first), each with a fails-when, and D20 adds a dependency suite with a planted fault.

F1 · advisory · probe 4.1, 7.3 · a unit stuck by standing lands 50 m or more from every player, so the post-move window finds it stuck by neither rule and it is cleared with no tries (D6, S-8); a unit that keeps walking back to the ledge is recovered again and again and never removed, ended only by the stall backstop on waits-for-clear waves. Promises hold. Fix: say in S-8 the loop is accepted and bounded by D9, or count recoveries over the unit's life (S-8's fallback).
F2 · advisory · S-9 fallback, D4/D6 · under the fallback a ranged unit shooting a player who never hits back has not fought; it is marked, cannot be moved with the player within 100 m, and is removed in view after about 3 × StuckSeconds, not "recovered and keeps fighting" as S-9 says. Fix: correct S-9, or count a player target within attack range as fought.
F3 · advisory · probe 4.1, D9 · Engine.WaveSchedule.Due reads the next wave's own WaitsForClear (true for WhenCleared or AfterSeconds absent, Logic/Model.cs:70); state D9's condition as "the next wave's AfterSeconds is null, or this is the last wave" so StallClock's test copies the Engine's rule.
F4 · advisory · D24 evidence · "the end scoreboard holds Chaos's .devkill kills only, one fewer than wave 1's count" is ambiguous once wave 2 arrives. Fix: name the number, or read the scoreboard before wave 2 is engaged.
F5 · advisory · probe 9.2, D2/D22 · an idle unit within 40 m of a player inside their own claimed territory is marked by standing, moved away, brought back by the movers, and the cycle repeats; churn, not a broken outcome. Fix: decide whether only Hunt-eligible players count for the 40 m reach and record it in S-5.
F6 · advisory · probe 6.2, D18 · a throwing despawn request on the removal itself has no try left to fail. Fix: the removal is retried at the next check within MaxDespawnsPerTick, the "move failing" health entry held until it goes through.

missing components: none
undeclared families: none
EARLIER: all resolved
15/15 layers · 50/50 probes
VERDICT: READY

### Dispositions
- F1 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 2 F1`
- F2 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 2 F2`
- F3 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 2 F3`
- F4 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 2 F4`
- F5 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 2 F5`
- F6 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 2 F6`
