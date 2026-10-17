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
