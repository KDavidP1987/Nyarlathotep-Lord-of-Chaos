# Raphael api 4 — admin action twins and admin reads

**Status:** in build (docs/dod/raphael-api-admin.md, audit docs/audits/raphael-api-admin.md); steps 1 (typed outcomes)
and 2 (twins, reads, api 4) built; step 3: 0.5.2 committed and deployed for Session 1, tag and push after it. 0.5.1 is the current published release.

## Goal

Every admin action Nyarlathotep has in chat gets a machine-readable twin, so the Raphael client can press a button,
read one answer line and show success or the reason it failed. Admins without Raphael see no change: the human
commands reply exactly as in 0.5.1. The wire contract is docs/RAPHAEL_INTEGRATION_CONTRACT.md §3 (reads), §4 (codes)
and §5a (twins).

## What ships

- **Step 1 (no behaviour change):** every admin mutation runs through Logic/AdminFlows.cs, whose flows reach the game
  only through IAdminOps (Services/AdminOps.cs, one call per member). Every Logic path of the flows returns an
  `Outcome` (the human text plus a refusal code, argument and reason), and each human command replies its
  `Human` text. A capture of 0.5.1's replies (Nyarlathotep.Tests/Fixtures/human-replies-0.5.1.txt) pins them.
- **Step 2:** `.nyar api event|template|pillar|purge …` twins answering one `[NYAR:ok]` or `[NYAR:err]` line; the
  reads `templates`, `template info`, `pillar list` and `killswitch`; a rate gate of 5 twins per admin per second;
  contract api 4. Logic/ApiCommandTable.cs copies the `nyar api` signatures, which preflight's WireContract check holds
  equal to the command classes, so ApiOverloadTests can prove no two commands share a VCF overload.
- **Step 3:** Session 1 in game, then release 0.5.2.

## Test plan

- **Unit tests:** HumanReplyTests and OutcomeCodeTests (step 1); ApiTwinTests, RateGateTests, ApiLinesTests
  AdminReads, WireFormatTests AdminTwins, the push, dependency-failure and privacy tests (step 2).
- **Preflight:** HumanReplies and OutcomeReturns (step 1), GatewayOnly's AdminOps rule, WireContract and Commands
  fixtures (steps 1–2).
- **Session 1 (owner, step 3):** each twin on bandit-ambush, its human effect in `.nyar event info` or
  `.nyar pillar list`, start twice, purge ask and confirm with the killswitch push, the four reads, and every line
  whole in chat (D13).

## Open questions

- None yet.

## Test results

### Session 1 · steps (owner, 0.5.2, snapshot raa1)

Server **Nyar Dev** at **127.0.0.1:9876** (the dev world, save-data-nyardev). Connect as the admin, stand in an open
spot away from your castle (the waves spawn around you, radius 10), and type each command in chat. After each,
check the reply against the expected line; `…` stands for any value. Report any reply that differs, is cut off, or
is missing, with its step number.

1. `.nyar api sub on` → `[NYAR:ok] cmd=sub on=1`
2. `.nyar api version` → `[NYAR:version] api=4 plugin=0.5.2 ready=1 admin=1 …`
3. `.nyar api templates` → six `[NYAR:tpl] id=…` rows, then an `[NYAR:end] cmd=templates …` line
4. `.nyar api template info bandit-ambush` → one `[NYAR:tpl] id=bandit-ambush pillar=spawns …` row, then `[NYAR:end] cmd=template count=1`
5. `.nyar api template use bandit-ambush` → `[NYAR:ok] cmd=template verb=use id=bandit-ambush tpl=bandit-ambush`; then `.nyar event info bandit-ambush` shows it **disabled**
6. `.nyar api template use bandit-ambush` again → `[NYAR:err] cmd=template verb=use code=exists arg=id`
7. `.nyar api event enable bandit-ambush` → `[NYAR:ok] cmd=event verb=enable id=bandit-ambush changed=1`; `.nyar event info bandit-ambush` shows it **enabled**
8. `.nyar api event set bandit-ambush action.waves 2` → `[NYAR:ok] cmd=event verb=set id=bandit-ambush field=action.waves value=2`
9. `.nyar api event set bandit-ambush location here` → `… field=location value=<x>,<z>` (one decimal each)
10. `.nyar api event start bandit-ambush` → `[NYAR:ok] cmd=event verb=start id=bandit-ambush`, a `[NYAR:ev] …` push, and the first wave spawns around you
11. `.nyar api event start bandit-ambush` again → `[NYAR:err] cmd=event verb=start code=state arg=id reason=already_active`
12. `.nyar api event stop bandit-ambush` → `[NYAR:ok] cmd=event verb=stop id=bandit-ambush`, a push, and the bandits despawn over a few seconds
13. `.nyar api event copy bandit-ambush ba-copy` → `[NYAR:ok] cmd=event verb=copy id=ba-copy from=bandit-ambush`
14. `.nyar api event new ba-new spawns` → `[NYAR:ok] cmd=event verb=new id=ba-new pillar=spawns`; `.nyar event list` shows ba-copy and ba-new, both disabled
15. `.nyar api event delete ba-copy` → `… verb=delete id=ba-copy confirm=30`; then `.nyar api event delete ba-copy confirm` → `… verb=delete id=ba-copy done=1`
16. `.nyar api event delete ba-new` then `.nyar api event delete ba-new confirm` → the same two lines for ba-new
17. `.nyar api event delete Bad!` → `[NYAR:err] cmd=event verb=delete code=badarg arg=id`
18. `.nyar api event reload` → `[NYAR:ok] cmd=event verb=reload id=- count=…`
19. `.nyar api pillar list` → five `[NYAR:pillar] …` rows (empowerment, spawns, boss, zones, sieges)
20. `.nyar api pillar spawns off` → `[NYAR:ok] cmd=pillar verb=set id=spawns on=0 changed=1`; `.nyar pillar list` shows spawns **off**
21. `.nyar api pillar spawns on` → `… id=spawns on=1 changed=1`; `.nyar pillar list` shows spawns **on**
22. `.nyar api event start bandit-ambush` → the ok line; wait for the first wave
23. `.nyar api purge` → `[NYAR:ok] cmd=purge verb=ask id=- confirm=30`
24. `.nyar api purge confirm` → `[NYAR:ok] cmd=purge verb=confirm id=- events=1 units=… secs=…`, a kill-switch `[NYAR:…]` push, and the bandits despawn
25. `.nyar api killswitch` → one `[NYAR:ks] …` row with the cooldown left
26. `.nyar api event disable bandit-ambush` → `[NYAR:ok] cmd=event verb=disable id=bandit-ambush changed=1`
27. Stay connected about 2 minutes (for the autosave), then tell me you are done. I stop the server, check the logs and restore the snapshot.
