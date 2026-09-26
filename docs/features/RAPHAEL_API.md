# Raphael api — the machine interface

**Status:** in development (docs/dod/raphael-api-core.md, step 3 of 6 built, in post-audit). Ships in 0.3.0 as api 2.

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
- Checked in step 4's session only (the game-bound services cannot load in the test host): the disconnect patch
  applies and ends a subscription; `sub` reaches the hub through the gateway; EventStore's two one-line delegates
  reach DefinitionEditor: with `sub on`, `.nyar event reload`, `enable`, `disable` and `set` each push one
  config-changed, and a refused `set` (unknown event) pushes none. 703 passed after the enable and name cases.

## Open questions

None.
