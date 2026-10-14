# Reviews: scaling

## Review 1 · 2026-10-14 · subagent · plan uncommitted · plan 90271 B · 27 items · files 0 · e3b0c44298fc · prompt 97622bb6132e
L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · F6
L4 · Considered · F1, F8
L5 · Considered · F4
L6 · Considered · no finding
L7 · Considered · F6
L8 · Considered · no finding
L9 · Considered · F2, F9
L10 · Considered · F5
L11 · Considered · F9
L12 · Considered · F7
L13 · Considered · F2, F3
L14 · Considered · no finding
L15 · Considered · no finding

F1 · advisory · probe 4.1 · Hordes don't scale "per entry". R6 says "each entry gets" extra units per extra player. An apocalypse pool's `Count` is only a draw weight (Logic/Apocalypse.cs `RoamerPlan.Draw`: count × chance), so D5 adds u × max(0, n − 1) to the horde's total instead. That reading is sensible, but no assumption records it. Only roamers have one (S-11). Fix: add an S-n (reversible, probe 4.1) saying a horde's extra is per horde, not per pool entry. Fallback: u × (n − 1) × the number of entries. Cite it in D5.
F2 · advisory · probe 13.1 · D22's sc-timing run uses four scaled SpawnWaves events plus apo-test, five at once. `Limits.MaxConcurrentEvents` defaults to 3 (Logic/Limits.cs:25), and Precedence refuses starts past it ("skipped by MaxConcurrentEvents"). The dev-cfg changes in step 4 and in 6.3 don't raise it, so the run could refuse events before it reaches 50 tracked units. Fix: add MaxConcurrentEvents ≥ 5 to step 4's dev-cfg list and the restored-after list, or cut sc-timing to three events.
F3 · advisory · probe 13.1 · 13.1 says roamer top-ups run "every 60 s". The code tops up at the start and at each horde rise (RoamerPlan summary; `ApocalypseRun` Rise `if (topUp) roamerLines = TopUp(...)`). 60 s is the retry wait for a horde that queued nothing. Fix: correct the cadence in 13.1 and Business rules 3. D10's "only when n changed" rule stays as written.
F4 · advisory · probe 5.1 · Several places cite "Limits.MaxRoamers (100)". The constant is `ApocalypseLimits.MaxRoamers` (Logic/Limits.cs:131). Fix: use the right symbol in D5, 13.2 and Interfaces 5.1.
F5 · advisory · probe 10.4 · With one counted player, "gear <A>" in D10 and "scaling now … gear" in D15 are that player's own gear level. Both are admin-only (`.nyar event` is `adminOnly: true`), and D57 settled showing the average. But D11's "a single player's gear level is shown other than as the average" can never fail when n = 1, which hides this. Fix: one sentence in Security 10.4 accepting that an average over one player is that player's level, admin-only.
F6 · advisory · probe 3.1 · On resume, `ResumeRule.Rebuild` builds regular horde units and roamers with `Modifiers` null. A scaled run's rebuilt units therefore lose their scaled level as well as their scaled count. D5 only says "unscaled again". Fix: make D5's text and fails-when name both count and level reverting for rebuilt units, so the builder doesn't add a level record to state.json, which D11 forbids.
F7 · advisory · probe 12.4 · D1-D12 and D14-D16 have dry runs that print "No test matches". These are not fault runs; the failing cases first run as planted faults in steps 2 and 3. Under rubric 3 this is method, not done. The cmd items (D13, D22, D26) do fail correctly on empty input. Fix: none needed beyond step 2's planned `note · planted` lines. Keep D17's Pending entries until each plant is logged.
F8 · advisory · probe 4.1 · D4 counts "the players within fanOut.minSpacing" of the group's player. It doesn't say those players must pass D2's filter (alive, in scope, the IncludeAdmins rule). Fix: add "counted by D2's rule" to D4, plus one fails-when case: an admin with IncludeAdmins false inside minSpacing.
F9 · advisory · probe 9.2 · For revenge and AroundPlayer waves on a Global-scope event, D57's rule (settled, not contested) counts every online player on the server, so a wave aimed at one player can grow by the whole server's count. Admins should be told. Fix: in D25's README Scaling section, add one line advising a regional scope on player-aimed events that should scale only to nearby players.

missing components: none
undeclared families: none
15/15 layers · 50/50 probes
VERDICT: READY

### Dispositions
- F1 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 1 F1`
- F2 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 1 F2`
- F3 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 1 F3`
- F4 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 1 F4`
- F5 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 1 F5`
- F6 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 1 F6`
- F7 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 1 F7`
- F8 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 1 F8`
- F9 · accepted · deferred to the Log (advice on a READY review, no item edited): `note · deferred · Review 1 F9`
