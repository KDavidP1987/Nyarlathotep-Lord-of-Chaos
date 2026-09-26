# Raphael api — the machine interface

**Status:** in development (docs/dod/raphael-api-core.md, step 6 of 6: released as 0.3.0 (GitHub pre-release); Sessions 1–7 clean). Ships in 0.3.0 as api 2.

## What it provides

The hidden `.nyar api …` commands the Raphael client mod sends silently, and whose `[NYAR:*]` replies it hides and
parses. The wire grammar, every tag and every key are specified in docs/RAPHAEL_INTEGRATION_CONTRACT.md; this doc
says what Nyarlathotep implements and how it was tested.

- **`.nyar api status`** (anyone): one `[NYAR:event]` row per active event and per ended event whose units still
  wait out the grace, then `[NYAR:end] cmd=status count=`. The unit count goes to admins only; no row carries a
  position.
- **`.nyar api events [page]`** (admin): one `[NYAR:def]` row per definition, 10 per page, then
  `[NYAR:end] cmd=events page=<cur>/<total> count=`.
- **`.nyar api sub on|off`** (anyone): push lines `[NYAR:ev]` for event start and end, waves, wave warnings, the
  kill switch and config changes. Subscriptions live in memory only, at most 128, and end on `sub off`, a
  disconnect, being found offline at a send or at the cap, or a restart. Lines wait in a queue of 50 (oldest dropped) and leave
  5 a tick. `wave-warn` is pushed only when the chat warning would fire (WaveWarnings on, the event's
  announce.warnings true, at the WarningOffsets).

## Code map

| File | Role |
|---|---|
| `Logic/Wire.cs` | Line builders: grammar, 480-byte cap, the tag builders |
| `Logic/Paging.cs` | Contract §4 paging: page parsing, the end line, badarg |
| `Logic/ApiLines.cs` | The `status` and `events` rows from the engine's state |
| `Logic/Subscriptions.cs` | The subscription set: on, off, disconnect, the offline prune, delivery, count-only log lines |
| `Logic/PushQueue.cs` | IPushSink, the six push lines, the queue of 50, and PushHub: the guarded entry points and the push tick |
| `Logic/Engine.cs`, `Logic/EventCatalog.cs` | Report each start, end, wave, purge and applied load to the sink where it happens |
| `Logic/DefinitionEditor.cs` | The reload and edit flows EventStore runs; only an applied load reaches the catalog |
| `Services/Pusher.cs` | The one PushHub, attached to the engine and the catalog; the scheduler's push phase |
| `Patches/UserDisconnectPatch.cs` | Ends the leaving user's subscription (hook UserDisconnect) |
| `Commands/ApiCommands.cs` | `.nyar api version`, `status`, `events` and `sub` |

## Test results

### 2026-09-25 · step 1 · unit tests
- `dotnet test Nyarlathotep/Nyarlathotep.Tests`: 600 passed, 0 failed (61 new, after the post-audit fixes).
- ApiLinesTests (D1, D2): status rows in contract order, units for admins only, ending rows for events waiting out
  the grace (latest cleanup, one row per id, none for an id active again), the four definition states, reasons
  mapped to the wire grammar and sent only with state=disabled, a duplicate id never active, an action named
  for a definition without one, no ending row for a cleanup already due.
- PagingTests (D3): "0", "-1", "+1", " 1", "x", "1.5", "99999999999", "2147483648" and a non-ASCII digit are badarg;
  empty is page=1/1 count=0; page 2 of 10 rows is the end line alone; page 2 of 11 rows is the eleventh row.
- WireFormatTests (D4): every contract example line of event, def, end, err, ok and ev equals what the builder
  sends for the same values; a 32-character id with a 200-character name and reason of 4-byte characters keeps
  every key, the name cut to 64 bytes and the reason to 120. Examples are read from the section that documents each tag, and
  the optional forms (paged end, err with secs and with arg, ev with wave) must each have one. api=2 is asserted in step 2, when Wire.Api moves.

### 2026-09-25 · step 2 · unit tests and checks
- `dotnet test Nyarlathotep/Nyarlathotep.Tests`: 648 passed, 0 failed (ContractDocTests, ApiAccessTests and the
  Subscribe row of AuthorizationTests added; api=2 asserted).
- `pwsh tools/preflight.ps1`: "wire contract: 7 tags, 3 api commands, all documented (api 2)"; "admin list: 6 admin
  commands, equal to the commands check; 10 commands documented"; "ready guard: 10/10"; PREFLIGHT OK.
- `pwsh tools/preflight.ps1 -ListCommands admin` lists ".nyar api events".
- `pwsh tools/preflight.ps1 -SelfTest`: 27/27 checks (WireContract good, bad to bad-5 and empty; AdminList bad-2 the
  swapped two-group file; Secrets bad-3 `gh auth token` in a tools/ script).
- `pwsh tools/preflight.ps1 -AuthSuite`: fails only on ".nyar api sub not found once", as expected until step 3 (A1).

### 2026-09-25 · step 3 · unit tests and checks
- `dotnet test Nyarlathotep/Nyarlathotep.Tests`: 683 passed, 0 failed (SubscriptionTests, PushTests, the push cases
  of ApiAccessTests, and the HookUserDisconnect and PushDelivery cases of DependencyFailureTests added).
- Mutation check: 12 mutants of Logic/Subscriptions.cs and Logic/PushQueue.cs (no collapse, the newest dropped, 6
  lines a tick, either S-1 gate ignored, an end keeping its wave-warn, a 129th id, no offline prune, a disconnect
  keeping the entry, an id in the log, an unguarded entry point, a skipped line kept) each fail at least one test.
- `pwsh tools/preflight.ps1`: "wire contract: 7 tags, 4 api commands, all documented (api 2)"; "gateway: only
  ActionGateway mutates"; "ready guard: 11/11"; PREFLIGHT OK.
- `pwsh tools/preflight.ps1 -AuthSuite`: "auth suite: pass (tests, commands, admin list, gateway)".
- `pwsh tools/preflight.ps1 -SelfTest`: 27/27 checks; WireContract bad-9 (sub missing while the table marks it
  IMPLEMENTED) fails.
- Post-audit (A6): the engine and the catalog report their own transitions, so PushTests drive the real start,
  wave, stop, expiry, purge and reload paths: a refused start, an end of nothing and a rejected file push nothing,
  a purge pushes one killswitch. 13 more mutants (each report removed, a per-event end on purge, a rejected file
  reported, the overflow streak, one guard for the tick, no prune at the cap, an exception message logged) each fail
  a test. 692 passed.
- Post-audit (A7): the reload and edit flows run in Logic/DefinitionEditor; ConfigChangedTests show the boot load and
  every failed reload, set, enable and disable push nothing and an applied one pushes one config-changed.
- Checked in the server sessions only (the game-bound services cannot load in the test host): the disconnect patch
  applies (Session 1) and ends a subscription; `sub` reaches the hub through the gateway; EventStore's two one-line
  delegates reach DefinitionEditor: with `sub on`, `.nyar event reload`, `enable`, `disable` and `set` each push one
  config-changed, and a refused `set` (unknown event) pushes none. All but the first need a connected player and run
  in Session 2 (step 5). 703 passed after the enable and name cases.

### Session 1 · 2026-09-25
Step 4, unattended. c744f1b deployed (Release, 0 warnings) to the dev world (save-data-nyardev); dev cfg General.Enabled
true, TimingLog true, [Announcements] all off, MaxDespawnsPerTick 5. `session-events.py a21` wrote t-own (10 units, own
lifetime 30 s, 300 s event) and t-end (6 units, 60 s) at 22:42.
- Boot: "Harmony patches applied: 5 method(s) patched"; "triggers: all hooks available" (UserDisconnect is one of
  them: TriggerBus.RequirePatched finds OnUserDisconnected patched); "push: ready (queue 50, 5 lines a tick, at most 128
  subscribers)", after "announcements: all off", before "Nyarlathotep initialized … (attempt #1)".
- The boot marker sweep found 5 units the previous world save still held ("5 found, 5 queued for despawn (0 listed in
  state.json)") and drained them in one batch ("5 of 5 destroyed, 0 requeued, 0 left").
- 22:42 both started by Schedule; t-own "10 units queued, due in 30s", t-end "6 units queued, due in 90s"; spawn
  batches 10 then 6. t-own's units left by their own lifetime: "10 units due for despawn", batches 5 and 5 to "0
  left". t-end ended (1 of 1 waves); at end + grace "6 units due for despawn", batches 5 and 1 to "0 left". t-own
  ended (1 of 1 waves) at 22:47.
- Tick timing: avg 0.040–1.184 ms, max 28.3 ms (the spawn and despawn minutes); idle max under 0.7 ms.
- Stopped after the next "Finished Saving" (AutoSave_413, 22:47:39), so the saved world holds no event unit.
- `-LogCheck`: 0 unhandled, 28 nyar lines, 0 orphan errors, 0 unity errors. Every [Warning] line read: Il2CppInterop
  "Class::Init signatures have been exhausted" and two Beelzebub TUNE lines (sibling mods, the same every boot); the
  server log's 226 PrefabLookupMap warnings (224 "unknown state", 2 "converted but does not exist") all come before
  "Startup Completed", as in foundation session 12. No error line.

### Session 2 · 2026-09-25
Step 5, the owner at 127.0.0.1:9876 ("Nyar Dev"). Session config: `session-events.py rac2`, WaveWarnings on, TimingLog
on, the plugin built with `-p:Version=0.3.0`.

Steps given to the owner (D12, D13, D22 and the step 3 checks):
0. In the mod manager, switch Raphael off for this session, so the `[NYAR:` lines stay visible in chat.
1. Direct Connect to 127.0.0.1:9876 ("Nyar Dev") and load in. Open the console (`~`) and run `adminauth`.
2. `.nyar api version` → expect `[NYAR:version] api=2 plugin=0.3.0 …admin=1…`.
3. `.nyar api status` → expect the end line with count=0 (no event runs).
4. `.nyar api events`, then `.nyar api events 2`, then `.nyar api events x` → 10 def rows and an end line; 1 row
   (the 11th) and an end line; a `[NYAR:err] … code=badarg` line.
5. `.nyar api sub on` → `[NYAR:ok] cmd=sub on=1`.
6. `.nyar event reload` → the reload reply and one `[NYAR:ev] type=config-changed id=- secs=0`.
7. `.nyar event enable t-spare`, then `.nyar event disable t-spare`, then `.nyar event set t-spare durationSeconds 90`
   → each reply followed by one config-changed line.
8. `.nyar event set nope durationSeconds 90` → a refusal and no config-changed line.
9. `.nyar event start example-spawns` → `type=event-start id=example-spawns secs=120`, `type=wave … wave=1`; 7
   bandits spawn around you (fight them or step away). While it runs, `.nyar api status` → one row for
   example-spawns. About 40 s later `type=wave … wave=2`; about 2 minutes after the start `type=event-end`.
10. Wait 40 s after the event-end line, then `.nyar api status` → count=0.
11. `.nyar purge confirm` → `type=killswitch id=- secs=240`.
12. `.nyar api sub off` → `on=0`; then `.nyar event reload` → the reply only, no `[NYAR:ev]` line.
13. `.nyar api sub on`, then quit to the main menu (disconnect). Wait 10 s, Direct Connect to 127.0.0.1:9876 again,
    run `adminauth` in the console, do NOT run `sub on`, and run `.nyar event reload` → the reply only, no `[NYAR:ev]`
    line.
14. At least 4 minutes after step 11 (the purge cooldown): `.nyar api sub on`, then `.nyar event start t-150`. It
    spawns 10 waves of 15 far from you over 3 minutes and ends at 5 minutes: expect wave-warn lines (secs=10) before
    waves 2–10, wave lines 1–10 and event-end, plus the chat warnings. About 1 and 3 minutes in, run
    `.nyar api status` and `.nyar api events`, and note whether each reply appears within about a second.
15. After t-150's event-end, `.nyar api sub off`, then disconnect. Send back what each step showed (screenshots are
    fine), and anything unexpected.

Observed, part 1 (2026-09-26 07:52–08:10; 16 owner screenshots, BepInEx log):
- `.nyar api events`: 10 def rows in id order (example-boss … t-fill-2, then t-fill-3, t-fill-4 on the next screen
  as page 1 of the 11), each with the D2 keys; example-spawns and t-150 `state=idle reason=-`, the disabled ones
  `state=disabled reason=disabled`; end line `[NYAR:end] cmd=events page=1/2 count=11`.
- `sub on` → `[NYAR:ok] cmd=sub on=1`; the log "push: 1 subscribed (on)".
- Step 3 checks: `.nyar event reload` → "reloaded: 11 valid, 0 disabled" then one `[NYAR:ev] type=config-changed
  id=- secs=0`; `enable t-spare`, `disable t-spare` and `set t-spare durationSeconds 90` → each reply then one
  config-changed; `set nope durationSeconds 90` → "unknown event nope" and no config-changed line.
- example-spawns: `type=event-start id=example-spawns secs=120`, `type=wave … secs=0 wave=1`; `.nyar api status` →
  `[NYAR:event] id=example-spawns kind=waves name=Bandit_raid state=active faction=- left=96 wave=1/2 units=7` and
  `[NYAR:end] cmd=status count=1`; `wave=2` about 40 s later; status `left=24 wave=2/2 units=14`; then
  `type=event-end id=example-spawns secs=0`; `.nyar api status` → `count=0`. The log: 7 of 7 spawned per wave, and at
  end + grace "14 units due for despawn", batches 5, 5, 4 to "0 left".
- `.nyar purge confirm` replied "nothing to purge": the steps put the purge after the event had ended and drained,
  so no killswitch was pushed (a step-order error, repeated in part 2).
- `sub off` → `on=0`, log "push: 0 subscribed (off)". Step 12's reload after it was not run (part 2 repeats it).
- D12: `sub on`, disconnect → log "[nyar] push: 0 subscribed (disconnect)". After reconnecting and `adminauth`,
  without `sub on`, `.nyar event reload` → "reloaded: 11 valid, 0 disabled" and no `[NYAR:ev]` line.
- t-150 (D22) with the owner subscribed: 10 waves of 15, all spawned ("10 of 10 spawned, 5 waiting", then "5 of 5");
  on screen for waves 8–10 the chat warning "Wave <n> of 10 of the Test 150 is almost here.", then
  `type=wave-warn id=t-150 secs=10 wave=<n>` and `type=wave id=t-150 secs=0 wave=<n>`; `.nyar api status` →
  `… state=active faction=- left=118 wave=10/10 units=150` and count=1; `.nyar api events` during the event shows
  t-150 `state=active`. Tick timing during the event: avg 0.512, 0.465, 0.426, 0.213 ms (max 5.3 ms); during the
  150-unit drain avg 0.975 ms (max 3.9 ms); idle avg 0.03–0.05 ms. Every average is under D24's 5 ms.
- The owner unsubscribed at 08:07, before t-150's end (08:08:07), so its event-end was logged ("ended (10 of 10
  waves)") but not pushed to the owner. The 150 units drained 5 a tick to "0 left".
- Part 1 has no screenshot of `api version`, `api events 2` or `api events x`, and no statement of reply times; part 2
  covers them.

Part 2 (08:33–08:38, same boot; the owner reconnected with Raphael off and ran `adminauth`; 2 screenshots):
- `.nyar api version` → `[NYAR:version] api=2 plugin=0.3.0 ready=1 admin=1 enabled=1 killswitch=0 empower=1 waves=1
  boss=1 zones=1 sieges=1 stats=0 annwarn=1 annbanner=0 anndaily=0 annlogin=0 annshare=0` (annwarn=1: WaveWarnings on).
- `.nyar api events 2` → `[NYAR:def] id=t-spare name=t-spare enabled=0 trigger=manual action=waves duration=90
  state=disabled reason=disabled` (the 11th row, carrying part 1's `set … 90`) and `[NYAR:end] cmd=events page=2/2
  count=11`; `.nyar api events x` → `[NYAR:err] cmd=events code=badarg arg=page`.
- `sub on`, `.nyar event start example-spawns`: the log shows 7 of 7 spawned per wave. The owner: "Everything worked
  correctly", answering step 4 (the bandits appeared around them) and step 8 (the `api status` and `api events`
  replies, in part 1 and part 2, came back within about a second).
- `.nyar purge confirm` without `.nyar purge` first was refused (the owner: "it said I had to run Purge first").
  Purge asks, then confirms within 30 s, by design (foundation D20, command table); the part 1 and part 2 steps
  omitted `.nyar purge`. With it: "purge ends 1 events and despawns 13 units; run .nyar purge confirm within 30 s",
  then "purged: 1 events, 13 units queued" and `[NYAR:ev] type=killswitch id=- secs=240`, no event-end line. The log:
  "purge: 1 events ended, 13 units queued, 0 spawns cancelled, cooldown 240s" (13: one of the 14 bandits was
  already dead), batches 5, 5, 3 to "0 left".
- `sub off` → `on=0`; `.nyar event reload` → "reloaded: 11 valid, 0 disabled" and no `[NYAR:ev]` line.
- Stopped after "Finished Saving" (08:41). `-LogCheck`: 0 unhandled, 807 nyar lines (the overnight idle ticks
  included), 0 orphan errors, 0 unity errors. [Warning] lines: Il2CppInterop Class::Init, two Beelzebub TUNE lines,
  and the mod's own purge line (logged at Warning by design); the server log's 226 PrefabLookupMap warnings all come
  before "Startup Completed". No error line. Dev cfg WaveWarnings set back to false.

### Session 3 · 2026-09-26
Step 6, unattended: practice runs of tools/rollback-drill.ps1 on the existing tags (`-From v0.2.1 -To v0.2.0`), before
the release tag exists. Dev world only; the drill saves and restores the plugin DLL and BepInEx/config/Nyarlathotep/.
- Run A: N booted once and wrote no state.json ("boot v0.2.1 did not write state.json"): the mod writes it only on a
  change, so an idle boot leaves nothing for N-1 to read. Recorded as A9 before the fix; the drill now adds a
  one-unit scheduled drill-mark event and boots N until it fires.
- Run B passed, but its edit made a 43-character name, and v0.2.0 disabled that event ("name must be 1-40
  characters"; "5 valid, 1 disabled"). The drill now writes a valid name, and fails when N-1's "events: reloaded"
  counts differ from N's.
- Run C passed: range v0.2.0..v0.2.1 17 paths, all in the manifest; both releases read "6 valid, 0 disabled";
  v0.2.0 logged "boot marker sweep: 0 found, 0 queued for despawn (1 listed in state.json)" and "event drill-mark
  cancelled by restart"; the config and DLL restored byte for byte; no worktree or %TEMP%\nyar-drill-* left.
- The boots of runs A and B before their last one were not log-checked on their own (each boot overwrites the log);
  runs B's and C's last boots were: 0 unhandled, 6 nyar lines, 0 orphan errors, 0 unity errors. Their [Warning] lines:
  Il2CppInterop Class::Init and the two Beelzebub TUNE lines; run B's also the name warning above. Run D (Session 4)
  checks every boot.

### Session 4 · 2026-09-26
Step 6, unattended: run D of the drill, which now runs `preflight.ps1 -LogCheck` after each of its three boots.
- "boot v0.2.1 (seed): log check: 0 unhandled, 6 nyar lines, 0 orphan errors, 0 unity errors"; "boot v0.2.1
  (drill-mark): log check: 0 unhandled, 9 nyar lines, …"; "boot v0.2.0: log check: 0 unhandled, 6 nyar lines, …";
  "events.json: v0.2.1 and v0.2.0 both read '6 valid, 0 disabled'"; "rollback drill: pass".
- The selftest: "drill selftest: 3/3"; a mutant without the "marker sweep" check passes the bad fixture and one without
  the empty-log check reports the empty fixture as another failure, so each fails the selftest (2/3).

### Session 5 · 2026-09-26
Step 6, unattended: the release drill `pwsh tools/rollback-drill.ps1 -From v0.3.0 -To v0.2.1` on the local tag v0.3.0
(ee36a7d), before the push. Dev world only.
- "range v0.2.1..v0.3.0: 158 paths, all in the manifest; v0.2.1 is an ancestor of v0.3.0"; both tags built.
- "boot v0.3.0 (seed): log check: 0 unhandled, 7 nyar lines, 0 orphan errors, 0 unity errors"; the drill renamed
  example-empowerment to "Renamed by the drill" and scheduled drill-mark.
- "boot v0.3.0 (drill-mark): log check: 0 unhandled, 11 nyar lines, …"; "drill-mark fired; files: events.json,
  state.json"; "stats.json: absent (no release writes it)".
- "boot v0.2.1: log check: 0 unhandled, 5 nyar lines, …"; "events.json: v0.3.0 and v0.2.1 both read '6 valid, 0
  disabled'"; "initialized on v0.3.0's files; events.json and state.json loaded without a read-only warning; marker
  sweep logged"; "restored the saved plugin DLL and config"; "rollback drill: pass".
- [Warning] lines of the last boot: Il2CppInterop Class::Init and the two Beelzebub TUNE lines, as in every session.
- Review 5 then found that a crashed drill's saved copy would be deleted by the next run (A11); the fixed script is
  rerun in Session 6.

### Session 6 · 2026-09-26
Step 6, unattended: the drill again after the push, with A11's leftover refusal, so D16's evidence comes from the
current script. Same range line; "boot v0.3.0 (seed): log check: 0 unhandled, 6 nyar lines, …"; "boot v0.3.0
(drill-mark): log check: 0 unhandled, 10 nyar lines, …"; "boot v0.2.1: log check: 0 unhandled, 5 nyar lines, …";
both read '6 valid, 0 disabled'; "rollback drill: pass". The same run of D18's command on the pushed tag:
"rollback: clean" (539 tests; v0.2.1's preflight OK). /code-review then found four drill defects (A13); Session 7
reruns the fixed script.

### Session 7 · 2026-09-26
Step 6, unattended: the drill with A13's fixes, on a dev server set up for the two cases A13 fixes: the plugin DLL
moved aside (a server without Nyarlathotep) and a hidden file `.drill-hidden` planted in BepInEx/config/Nyarlathotep/.
- drill-mark scheduled past the boot timeout (09:36, seven minutes out) and fired; "boot v0.3.0 (seed): log check: 0
  unhandled, 6 nyar lines, …"; "boot v0.3.0 (drill-mark): … 15 nyar lines …"; "boot v0.2.1: … 5 nyar lines …"; both read
  '6 valid, 0 disabled'; "restored the saved state (no plugin DLL) and config"; "rollback drill: pass".
- After the drill: no Nyarlathotep.dll in BepInEx/plugins (the N-1 DLL was removed), and `.drill-hidden` was back with
  its content. Then the planted file was removed and the DLL put back: config and DLL hashes equal the copies taken
  before the session.
- [Warning] lines: Il2CppInterop Class::Init and the two Beelzebub TUNE lines. No error line.

## Open questions

None.
