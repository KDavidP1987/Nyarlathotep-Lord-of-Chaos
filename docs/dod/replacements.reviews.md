# Reviews: replacements

## Review 1 · 2026-10-14 · subagent · plan uncommitted · plan 94763 B · 28 items · files 0 · e3b0c44298fc · prompt ac4db2696e60
L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · F3
L4 · Considered · F4, F6
L5 · Considered · F3
L6 · Considered · F1
L7 · Considered · F5
L8 · Considered · no finding
L9 · Considered · no finding
L10 · Considered · no finding
L11 · Considered · no finding
L12 · Considered · no finding
L13 · Considered · no finding
L14 · Considered · F7
L15 · Considered · no finding

F1 · blocking · blocks: outcome. Probes 6.1 and the Requirements rule. The request and the North Star promise that units dying to sunlight, water, a fall and other NPCs are replaced. The only in-game deaths the plan makes are `.devperish` deaths (D19, D20, D21). The world causes appear only in D23 "(as the session offers)". If the game delivers a drowning or a fall as an entity removal instead of a DeathEvent, that unit is never replaced and D1-D28 all still pass. Fix: a Claude-run vrclient check in step 4 per cause (pond, ledge, native or another event's unit, sunlight where it hurts), recording whether the death pass sees a death and whether a replacement follows, and a stated rule for a cause that turns out to be a game removal.

F2 · advisory · L6, probe 6.1. Engaged rule (a) follows EntityOwner only; a player's familiar is linked by Follower, so a unit it hit and that then dies to the world is replaced. Fix: follow the owner/follower chain as ScoreReader.Players does, or record the exclusion.

F3 · advisory · L3/L5, probes 3.2 and 5.2. SpawnTracker.Release removes `_spawnSpot`; step 2's kept order leaves out the unit's own spawn spot that S-6's last fallback needs. Fix: keep it in the kept order.

F4 · advisory · L4, probe 4.5. Business rule 7 says D15's nine end paths; D15 lists ten and ApocalypseEnd also has Disable. Fix: list the set against EndPath and ApocalypseEnd and add the disable path to D15.

F5 · advisory · L7, probe 7.3. An apocalypse replacement's due time should be the run's rolling lifetime at queue time; D5's fails-when does not check it. Fix: one clause on D5.

F6 · advisory · L4, probe 4.1. D7's "above what the wave queued" is unclear for growth and swarm waves and clipped waves. Fix: define it as the Queued result of that wave's request(s) in this instance.

F7 · advisory · L14, probe 14.4. D21's "wave 2<k><j>" cannot be grepped as written. Fix: "wave <200+k×10+j>", e.g. wave 221.

missing components: none
undeclared families: none
15/15 layers · 50/50 probes
VERDICT: REVISE

### Dispositions
- F1 · accepted · new D29 (World deaths in game, Session 3 in step 4, scenario replacements-world.vrs, per-cause table) in C3 and W1.3; S-16 states the rule: a cause delivered as a removal is not replaced in 0.15.0, named in the table and told to the owner, a later amendment if they want it replaced
- F2 · accepted · D2 (a) and its fails-when now include a unit following a player (Follower, as ScoreReader.Players reads)
- F3 · accepted · step 2's kept order includes the unit's own spawn spot
- F4 · accepted · D15 adds the disable path; Business rule 7 names D15's eleven against EndPath and ApocalypseEnd
- F5 · accepted · D5's fails-when checks the run's rolling lifetime at queue time
- F6 · accepted · D7 defines what the wave queued as the Queued result of its request(s) in this instance, after the caps clipped it
- F7 · accepted · D21 writes the line as "wave <200 + k × 10 + j>" (wave 221)

## Review 2 · 2026-10-14 · subagent · plan uncommitted · plan 97530 B · 29 items · files 0 · e3b0c44298fc · prompt aeb9c8de9323
L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · no finding
L4 · Considered · F3
L5 · Considered · no finding
L6 · Considered · F1
L7 · Considered · F2
L8 · Considered · no finding
L9 · Considered · no finding
L10 · Considered · no finding
L11 · Considered · no finding
L12 · Considered · F4
L13 · Considered · no finding
L14 · Considered · no finding
L15 · Considered · no finding

Review 1 F1 resolved: D29 adds Session 3 per world cause; S-16 states the rule for a cause delivered as a removal (not replaced in 0.15.0, named, told to the owner, a later amendment can change it); D58 already says despawns are never replaced.

F1 · advisory · L6, probe 6.1 / R4-R5. If Session 3 finds water, a fall or sunlight arrives only as a removal, that cause stays unreplaced and D29 still passes. Fix: step 4's post-audit puts the S-16 outcome in front of the owner before step 5.

F2 · advisory · L7, probe 7.2. WaveCleared reads the queue and in-flight (SpawnLedger.cs:262-268), so D4 holds; a cap-skipped death leaves the wave clearable. Fix: D4's test includes a cap-skipped death in the same tick as a WaveSchedule.Due evaluation.

F3 · advisory · L4, probe 4.5. Business rule 7 now matches EndPath plus ApocalypseEnd. Fix: D15's test enumerates both enums by reflection.

F4 · advisory · L12, probe 12.4. The manual items depend on `.devperish` reading as a non-player killer; the spike stops the build if not. Fix: record the spike verdict in the audit as S-10's source.

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
