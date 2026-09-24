# Foundation — the event engine

**Status:** in build (docs/dod/foundation.md, step 3 of 9). Ships in 0.2.0. Nothing here is enabled by
default: pillar switches are off, seeded events are disabled, announcement switches are off.

## What it provides

The shared engine every pillar builds on:

- **Event definitions** in `BepInEx/config/Nyarlathotep/events.json` (EventDefinition v1): an id, a trigger
  (Manual, Schedule, GameTime, VBloodKilled), optional conditions, a duration and an action. Foundation's only
  action is the core **SpawnWaves** (units × count, waves, interval, radius, at a point or at the admin);
  event-spawns extends it and faction-empowerment adds Empower.
- **Validation** that never stops the server: a file that does not parse is rejected whole with its line and
  position; an invalid event is disabled with one reason naming its field; cfg values outside their range are
  clamped at load with a log line.
- **Precedence**: purge > General.Enabled > the pillar switch > Limits caps > the definition. No command,
  trigger or definition field can exceed a cap. A unit never outlives its event end + GraceSeconds.
- **Schedules** on the server-local clock, fired once per occurrence (local date + HH:mm) — including across
  a daylight-saving repeat and a restart — and never replayed after downtime. GameTime triggers fire on the
  day/night edge.
- **Duplicate-action rules**: one instance per definition, a 5 s trigger dedupe, a stale-file refusal for
  `event set`/`enable`/`disable`, and a two-step purge (`.nyar purge`, then `.nyar purge confirm` within 30 s).

- **Persistence** (step 2): `Services/Persistence.cs` is the only file writer. A write goes to `<file>.tmp` and is
  promoted in one step; events.json keeps one `.bak`; a stale `.tmp` is removed at load; an unparsable state.json
  is renamed `state.json.corrupt` and an empty state used; a newer SchemaVersion loads read-only; state.json is
  written at most once per second, and a failing write is retried every second with one log line per streak.
- **First run** seeds events.json from `Resources/events.default.json`: one example per pillar, every one disabled.
- **One door for changes** (step 3): every mutating operation is an ActionKind run through `Logic/ActionGateway`.
  Admin may run every kind; the operator's file load runs as Operator; the scheduler and triggers (System) may only
  start and end enabled definitions; players get none in this child. A denial is logged as "gateway: denied <kind>
  for <actor>". A service method that changes something is marked `[Mutating]`, and preflight fails when one is
  called outside `Gateway.Run`.
- **Wire lines for Raphael** (step 3): `Logic/Wire` builds `[NYAR:<tag>] key=value …` lines of at most 480 bytes,
  including the `[NYAR:version]` handshake with every key of docs/RAPHAEL_INTEGRATION_CONTRACT.md §2 (the command
  that sends it comes in step 6).
- **Clean text** (step 3): `.nyar announce` text must be 1-200 characters with no `<`, `>` or control characters;
  player and clan names lose those characters and are cut to 20; in a wire value spaces become `_` and `=`, `;`,
  `:` are removed.
- **No positions in player lines** (step 3): every player reply and announcement comes from `Logic/Messages`, whose
  builders take no position or radius, and whose templates use only {faction}, {minutes}, {event}, {zone}, {wave},
  {waves}.

Later steps add the spawner, the scheduler and runtime, the announcer and the admin commands (see the plan's
Build plan).

## Code map

| Area | Files |
|---|---|
| Pure logic (no game types; compiled into the tests) | `Nyarlathotep/Nyarlathotep/Logic/` — Model, Validation, Limits, CommandArgs, Schedule, Precedence, Idempotency, EventCatalog, Dependency, Paths, IFileStore, DataStore, Hooks, ActionGateway, Wire, TextSink, Messages |
| Files and definitions | `Services/Persistence.cs` (disk), `Services/EventStore.cs` (seed, load, reload), `Resources/events.default.json` |
| Gateway | `Services/Gateway.cs` (the one `ActionGateway`; `Gateway.Run` wraps every `[Mutating]` call) |
| Startup | `Patches/GameDataInitializedPatch.cs` — the save-loaded trigger and, for a brand-new world, `ServerStartupPatch` (A5) |
| Preflight checks (step 3) | `tools/preflight.ps1` — GatewayOnly, FaultInjection, AdminList (`-ListCommands admin`), SessionLogs (`-SessionsOf <slug>`) |
| Unit tests | `Nyarlathotep/Nyarlathotep.Tests/` (xUnit 2.9.3, Microsoft.NET.Test.Sdk 17.8.0, net6.0) |

## Test results

### 2026-09-24 · step 1 · unit tests
- `dotnet test Nyarlathotep/Nyarlathotep.Tests -c Release` → Passed 190, Failed 0 (ControlPrecedenceTests,
  ScheduleTests, EventValidationTests, ConfigClampTests, CommandArgTests, IdempotencyTests).
- Mutation check: swapping the purge and General.Enabled checks in `Precedence.StartBlocker` failed 8
  precedence cases; removing the occurrence-key comparison in `Schedule.Due` failed 8 schedule cases; restored
  code passes 190/190.

### 2026-09-24 · step 2 · unit tests
- `dotnet test Nyarlathotep/Nyarlathotep.Tests -c Release` → Passed 233, Failed 0 (adds PersistencePathTests,
  PersistenceWriteTests, SeedTests, DependencyFailureTests).
- Mutation check: removing the one-second interval in `StateStore.Flush` and the per-streak log gate failed 2
  cases; removing the `..` and reparse-point checks in `DataPaths.Check` failed 2 cases; restored code passes.
- After the Codex cross-inspection fixes (31cca9e): Passed 235, Failed 0. The seed now never overwrites a file
  that appears while it writes, and a failure during command discovery is contained. Mutations: reverting
  `Seed` to an overwriting promote failed 1 case; unguarding the discovery step failed 1 case.

### 2026-09-24 · step 3 · unit tests
- `dotnet test Nyarlathotep/Nyarlathotep.Tests -c Release` → Passed 440, Failed 0 (adds AuthorizationTests,
  WireFormatTests, TextSinkTests, PrivacyTests; the authorization matrix is every ActionKind × actor × enabled).
- Mutation check: each of 9 planted faults failed its class. The faults: Player granted Announce, System's enabled
  check removed, a denial not logged, wire values unmapped, the 480-byte cap removed, a handshake key dropped,
  control characters kept, the announcement length unchecked, and the status line leaking the radius.
- `pwsh tools/preflight.ps1 -SelfTest` → "selftest: 24/24 checks, 3 fixtures each, 50 extra bad fixtures".
- After the code review and Codex rounds 1–3 (40d0e30): Passed 449, and "selftest: 24/24 checks, 3 fixtures each,
  56 extra bad fixtures". The fixes: suffixed command attributes, a 0-nyar-line session, Unicode format and
  separator characters judged per scalar, required wire lines that throw instead of dropping a field, named
  dispatched services, and interpolated strings masked with their holes kept.

### Session 1 · 2026-09-24
Dev world "Nyar Dev" (save-data-nyardev), created fresh, 127.0.0.1:9876, build 8865800.
- First launch without SteamAppId: "App ID missmatch!", Direct Connect refused (A4); the mod also never
  initialised on the brand-new world (A5). Fixed, world deleted and recreated.
- Boot: "Nyarlathotep initialized via ServerStartupPatch"; no BepInEx/config/Nyarlathotep/ before → "first run:
  seeded events.json from the default templates (every event disabled)"; events.json holds SchemaVersion 1 and 5
  events, all `"enabled": false`; no .tmp left. The seed's zones unit was not spawnable (A6, fixed for later
  first runs).
- As a non-admin player: `.nyar` → the overview with v0.1.0; `.nyar status` → "No active events." (D28).
- "still loading" was not observable: the server accepts connections only after startup completes.

### Session 2 · 2026-09-24
Boot with example-spawns' first unit changed to CHAR_Not_A_Real_Unit (D23, boot half): log "event
example-spawns: unknown unit CHAR_Not_A_Real_Unit", "events: reloaded: 3 valid, 2 disabled"; the mod initialised
and the server kept running.

### Session 3 · 2026-09-24
Boot with a comma removed on line 30 of events.json (D23, boot half): log "events.json rejected: line 30 position
20; the last valid set stays (0 events)" — the exact line and column of the planted fault; the mod initialised
and the server kept running. events.json restored from the seed afterwards.

## Open questions

- D28's "still loading" reply cannot be seen in game (players connect only after startup); step 5, where the
  command classes are built, decides how it is evidenced.
