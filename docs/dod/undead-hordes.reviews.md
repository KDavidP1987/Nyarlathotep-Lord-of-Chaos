# Reviews — undead-hordes

## Review 1 · 2026-10-17 · subagent · plan commit 17a190b · plan 57587 B · 32 items · files 0 · e3b0c44298fc · prompt e84a79b4bdc5
F1 blocking · blocks: outcome · 13.1/R13 · R13 is the owner's complaint that "only one or two had shown up at my location after 5 or 10 minutes", but D24 passes on "at least one hunter within 100 m of Chaos within 5 minutes", the result the owner rejected; D9 fixes the hunter share, not how many arrive — name an arrival count in D24 (at least half the hunter share) or map R13 to D27
F2 advisory · 13.2 · D4 may set MaxTrackedUnits below 0.13.1's 500, clamping operators after the upgrade — say whether 500 is a floor or a result below it is the owner's logged decision
F3 advisory · 6.2 · the Hostility probe's CHAR_Wolf is not a prefab (CHAR_Forest_Wolf is); add CHAR_Legion_Shadowkin_Lesser, recorded as killed by undead, so D24 always has a hostile native
F4 advisory · 12.4/14.4 · `preflight.ps1 -TimingSpan … -MinTracked` (D25) and `rollback-gate.ps1` (D28) exist but were not dry-run; Paths walked lacks the ceiling-run scenario behind D26
F5 advisory · 4.5 · the infestation's living count has no unexplained-share clause; no item checks pillar off or fault for the two new modes
F6 advisory · 7.1/7.3 · whether a held front counts toward D6's 120 s stall; what a stalled HordeMarch does; D3's drain bar mixes ticks and seconds
F7 advisory · 14.3 · the Rollback route does not say what happens to a running infestation's units on a 0.13.1 boot
missing components: none
undeclared families: stop, purge, pillar off, fault, duration, won, restart
15/15 layers · 50/50 probes
VERDICT: REVISE
### Dispositions
- F1 · accepted · D24 now needs at least half of the hunter share (15 of 30) within 100 m of Chaos within 5 minutes; R13 also maps to D27, where the owner judges how many reach the players
- F2 · accepted · D4: the ceiling never drops below 0.13.1's 500 unless the owner decides it, with their line in the Log
- F3 · accepted · D4's probe uses CHAR_Forest_Wolf and adds CHAR_Legion_Shadowkin_Lesser
- F4 · accepted · both dry runs in the Log; tools/vrclient/scenarios/ceiling-run.vrs in step 4 and Paths walked
- F5 · accepted · D9 gains the unexplained-share clause for the infestation; D17 checks every end path (stop, purge, pillar off, fault, duration, won, restart) for both new modes
- F6 · accepted · D6: a held front's time does not count toward the stall; D11: a stalled horde infests where it stands; D3's drain bar in seconds at the one-second tick
- F7 · accepted · Rollout › Rollback and D28: 0.13.1's boot orphan sweep drains a running infestation's units, checked by the gate's drill
- UF1 · accepted · D17

## Review 2 · 2026-10-17 · subagent · plan commit 30b194c · plan 58937 B · 32 items · files 0 · e3b0c44298fc · prompt 5268ddf31dfb
F1 blocking · blocks: limit · 4.4/13.1 · D26 needs a horde march and an infestation to grow together, but D13 refuses a second apocalypse-type event, so D26 can never pass and the 1000-unit claim has no in-play evidence — let one horde march grow and infest to the ceiling, with destination SpawnWaves events beside it if needed
F2 advisory · 13.1/S-4 · the dev server runs at ServerFps 30, so a meter reading the frame interval sees about 33.3 ms idle and "frame average under 33 ms" fails at step 100 — say what the meter measures and set D3's bars to match
F3 advisory · 12.4 · D25's -MinTracked 450 cannot pass if the owner accepts a ceiling below 500 — use a share of the horde's size
F4 advisory · 7.1 · D10's Hotspot start has no refusal when no hotspot qualifies
F5 advisory · 9.2 · an Admin destination in the template has no meaning without an admin's start
missing components: none
undeclared families: none
15/15 layers · 50/50 probes
VERDICT: REVISE
### Dispositions
- F1 · accepted · D26 is one horde march at the MaxUnitsPerWave ceiling that grows and infests until the run holds the MaxTrackedUnits ceiling, destination waves beside it only to fill
- F2 · accepted · D2 measures the interval between frames; D3, Performance and S-4 bar the interval average under 36 ms (28 of 30 fps) and p95 under 50 ms
- F3 · accepted · D25's -MinTracked is 90 % of the horde's size
- F4 · accepted · D10 refuses with "no <faction> natives found at least <minDistance> m away", with a fails-when
- F5 · accepted · D10: an Admin start or destination needs a Manual trigger and an admin's start, refused otherwise, as Admin locations today
