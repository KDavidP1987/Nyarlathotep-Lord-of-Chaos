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

### Session 1 · 2026-09-28 · api 4 twins in chat (owner, 0.5.2+32cc082, snapshot raa1)

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

**Results (19:25–19:33, owner as admin with the Raphael client installed; SteamIDs redacted).** Every twin answered
its one expected line, whole, and every read its rows and end line, whole (steps 3, 19 and 25, and step 4 in the
follow-up); each line below is quoted from the chat text the owner copied:

- 1–2: `[NYAR:ok] cmd=sub on=1`; `[NYAR:version] api=4 plugin=0.5.2 ready=1 admin=1 enabled=1 killswitch=0 empower=1
  waves=1 boss=1 zones=1 sieges=1 stats=0 annwarn=0 annbanner=0 anndaily=0 annlogin=0 annshare=0`.
- 3: six `[NYAR:tpl]` rows (legion-weekend-surge … undead-rising, e.g. `id=bandit-ambush pillar=spawns trigger=manual
  duration=600 summary=Bandit_ambush`), then `[NYAR:end] cmd=templates page=1/1 count=6`.
- 4: the first run showed only `[NYAR:end] cmd=template count=1`: the client hid the row (the server replies every
  line, AdminFlows → ApiLines.TemplateInfo returns the row and the end line, and the shim sends each); after the owner's
  Raphael update the follow-up showed `[NYAR:tpl] id=undead-rising pillar=spawns trigger=manual duration=600
  summary=Undead_rising` and `[NYAR:tpl] id=bandit-ambush …`, each with its end line.
- 5–9: `cmd=template verb=use id=bandit-ambush tpl=bandit-ambush`, the event listed `enabled=0 state=disabled`; again
  `code=exists arg=id`; `verb=enable … changed=1` (then `enabled=1 state=idle`); `verb=set … field=action.waves
  value=2`; `verb=set … field=location value=-1975.8,-1667.7`.
- 10–12: `verb=start id=bandit-ambush`, then `[NYAR:event] id=bandit-ambush kind=waves … state=active … wave=1/2`
  (log "wave 1/2: 6 units queued (1 moved, 0 unchecked)", "spawn batch: 6 of 6 spawned"); again `code=state arg=id
  reason=already_active`; `verb=stop`, status count=0 (log "stopped: 6 units queued", despawn 5 + 1 of 6, 0 left).
- 13–16: `verb=copy id=ba-copy from=bandit-ambush`; `verb=new id=ba-new pillar=spawns` (both `enabled=0`);
  `verb=delete id=ba-copy confirm=30` and for ba-new. The owner did not send the confirms in the first run (the
  admin log has no `confirm` line); the follow-up sent both: `verb=delete id=ba-copy done=1`, `… id=ba-new done=1`,
  and the definitions went from 14 to 12.
- 17: `[NYAR:err] cmd=event verb=delete code=badarg arg=id`, and no admin log line for it (D3: badarg before the log).
- 18: `verb=reload id=- count=14`.
- 19–21: five `[NYAR:pillar]` rows (all `on=1`) and `[NYAR:end] cmd=pillar count=5`; `id=spawns on=0 changed=1`
  (then the version line shows `waves=0`; log "pillar spawns off (saved to cfg)"); `on=1 changed=1`; a repeated
  `spawns on` answered `on=1 changed=0` (D5).
- 22–25: `verb=start`; `cmd=purge verb=ask id=- confirm=30`; `verb=confirm id=- events=1 units=6 secs=240` (the
  version line then shows `killswitch=1`; log "purge: 1 events ended, 6 units queued, 0 spawns cancelled, cooldown
  240s", despawn 5 + 1, 0 left); `[NYAR:ks] on=1 secs=234 events=0 units=0`, `[NYAR:end] cmd=killswitch count=1`.
- 26: `verb=disable id=bandit-ambush changed=1`. `.nyar event list` then showed every event `off (purge)` during the
  cooldown.
- Pushes: the Raphael client consumed the `[NYAR:ev]` pushes (none showed in chat) and re-read `api version`,
  `api events` and `api status` after each change; those re-reads show a push arrived for each change
  (config-changed, event-start, event-end, killswitch). Some re-reads showed only their end lines: the client's
  display, as in step 4.
- Setup (Claude): the deployed 0.5.1 booted clean as the baseline, then `pwsh tools/dev-snapshot.ps1 -Save raa1`
  before 32cc082 was deployed; 0.5.2 initialized ("events: reloaded: 10 valid, 1 disabled", "templates: 6/6 valid",
  sweeps 0). Stopped after AutoSave_1707, which followed the last delete; -LogCheck "0 unhandled, 194 nyar lines, 0
  orphan errors, 0 unity errors". Then `pwsh tools/dev-snapshot.ps1 -Restore` →
  "snapshot restored; hashes equal (raa1, …nyar-snap-raa1 deleted)".

### Session 2 · 2026-09-28 · twin effects in event info (owner, 0.5.2+aa5a479, snapshot raa2)

A19: each twin is followed by the human read D13 names; quote each twin's `[NYAR:…]` reply and each read. Server
**Nyar Dev** at **127.0.0.1:9876**; connect as the admin and stand in an open spot.

1. `.nyar api sub on` → `[NYAR:ok] cmd=sub on=1`
2. `.nyar api template use bandit-ambush` → `… verb=use id=bandit-ambush tpl=bandit-ambush`; `.nyar event info bandit-ambush` → disabled
3. `.nyar api event set bandit-ambush action.waves 2` → `… field=action.waves value=2`; event info → 2 waves
4. `.nyar api event set bandit-ambush location here` → `… field=location value=<x>,<z>`; event info → action line "… at <x> <z> height <y> …" (info drops a trailing .0 the wire value shows)
5. `.nyar api event enable bandit-ambush` → `… changed=1`; event info → enabled
6. `.nyar api event start bandit-ambush` → `… verb=start id=bandit-ambush`; wait for the first wave; event info → running …, wave 1/2
7. `.nyar api event stop bandit-ambush` → `… verb=stop id=bandit-ambush`; event info → not running
8. `.nyar api event copy bandit-ambush ba-copy` → `… verb=copy id=ba-copy from=bandit-ambush`; `.nyar event info ba-copy` → disabled
9. `.nyar api event new ba-new spawns` → `… verb=new id=ba-new pillar=spawns`; `.nyar event info ba-new` → disabled, pillar spawns, 1 waves … at the admin
10. `.nyar api event delete ba-copy` → `… confirm=30`; `.nyar event info ba-copy` → still there (an ask only arms); `.nyar api event delete ba-copy confirm` → `… done=1`; `.nyar event info ba-copy` → unknown event ba-copy
11. The same for ba-new (→ unknown event ba-new)
12. `.nyar api event reload` → `… verb=reload id=- count=…`; event info bandit-ambush → unchanged (enabled, 2 waves)
13. `.nyar api event disable bandit-ambush` → `… changed=1`; event info → disabled
14. `.nyar api pillar spawns off` → `… id=spawns on=0 changed=1`; `.nyar pillar list` → spawns off
15. `.nyar api pillar spawns on` → `… id=spawns on=1 changed=1`; `.nyar pillar list` → spawns on
16. `.nyar api event enable bandit-ambush`, then `.nyar api event start bandit-ambush`; wait for the first wave; event info → running
17. `.nyar api purge` → `… verb=ask id=- confirm=30`; event info bandit-ambush → still running; `.nyar api purge confirm` → `… verb=confirm id=- events=1 units=… secs=…`; event info bandit-ambush → not running
18. Stay connected about 2 minutes (for the autosave), then say done.

Setup (Claude): `pwsh tools/dev-snapshot.ps1 -Save raa2`; a first boot showed 0.5.1, because restoring raa1 had put
back the DLL it held; the server was stopped with no one connected and 0.5.2 (aa5a479, code as 32cc082) deployed.

**Results (19:54–20:04, owner as admin with the Raphael client; lines quoted from the owner's copied chat, whole).**
The client showed every `[NYAR:def]` row this time.

- 1: `[NYAR:ok] cmd=sub on=1`.
- 2–3: `[NYAR:ok] cmd=template verb=use id=bandit-ambush tpl=bandit-ambush`; `[NYAR:ok] cmd=event verb=set
  id=bandit-ambush field=action.waves value=2` (sent twice; the second also answered ok, A5). The owner skipped the
  read after step 2; the read after step 3 shows both effects: `bandit-ambush "Bandit ambush" disabled pillar spawns
  trigger manual duration 600s` and `action: 2 waves every 60s, radius 10, at the admin, units 4 CHAR_Bandit_Thug, 2
  CHAR_Bandit_Hunter`.
- 4: `… field=location value=-1977.2,-1667.6`; event info `action: 2 waves every 60s, radius 10, at -1977.2 -1667.6
  height 0, …`.
- 5: `verb=enable id=bandit-ambush changed=1`; event info `… enabled pillar spawns …`.
- 6: `verb=start id=bandit-ambush`; event info `running: started by manual, 593s left, wave 1/2`.
- 7: `verb=stop id=bandit-ambush`; event info `not running`.
- 8: `verb=copy id=ba-copy from=bandit-ambush`; `.nyar event info ba-copy` → `ba-copy "Bandit ambush" disabled …`,
  the copied action line.
- 9: `verb=new id=ba-new pillar=spawns`; `ba-new "ba-new" disabled pillar spawns …`, `action: 1 waves every 60s,
  radius 8, at the admin, units 3 CHAR_Bandit_Thug`.
- 10: `verb=delete id=ba-copy confirm=30`; event info still showed ba-copy; `verb=delete id=ba-copy done=1`;
  `unknown event ba-copy`: the delete twin (ask and confirm) whole, with both reads. 11: a repeat of the same twin on
  ba-new, which the owner ran but did not paste; the log has "event ba-new deleted", and the reload in step 12
  counted 12. D13 asks for each twin, which step 10 covers (and Session 1 quoted both deletes' ok lines).
- 12: `verb=reload id=- count=12`; event info unchanged (enabled, 2 waves, the location).
- 13: `verb=disable id=bandit-ambush changed=1`; event info `… disabled pillar spawns …`.
- 14–15: `cmd=pillar verb=set id=spawns on=0 changed=1`, `.nyar pillar list` `spawns off (Pillars.EventSpawns)`;
  `on=1 changed=1`, `spawns on (Pillars.EventSpawns)`.
- 16: `verb=enable … changed=1`, `verb=start id=bandit-ambush` (the owner skipped the read; step 17's first read shows
  it running).
- 17: `cmd=purge verb=ask id=- confirm=30`; event info `running: started by manual, 477s left, wave 2/2`;
  `cmd=purge verb=confirm id=- events=1 units=12 secs=240` (version line `killswitch=1`); event info `not running`.
- Server log: every spawn batch 6 of 6; the stop despawned 6 and the purge 12, each to 0 left.
- Stopped after AutoSave_1716 (20:06), which followed the last action; -LogCheck "0 unhandled, 100 nyar lines, 0
  orphan errors, 0 unity errors". Then `pwsh tools/dev-snapshot.ps1 -Restore` →
  "snapshot restored; hashes equal (raa2, …nyar-snap-raa2 deleted)". raa2 was saved before 0.5.2 was redeployed,
  so the restore put back 0.5.1's DLL; the next session deploys first.
