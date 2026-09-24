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

### Session 4 · 2026-09-24
Baseline boot of the step 3 DLL before step 4 (pre-audit): initialised in 30 s, "events: reloaded: 5 valid,
0 disabled", log check 0 unhandled.

### Session 5 · 2026-09-24
Step 4 build 8e6aeaa..3470b25 (the spawner), ManualSpawnLifetimeSeconds = 60 in the dev cfg (D27, D20 unit half).
- `.nyar spawn CHAR_Bandit_Thug` and `.nyar spawn CHAR_Bandit_Thug 3 +2 1.5 1.2`: all four attacked the owner, and
  killed ones dropped no loot. `debug here` showed the level held (16 → 18). The multipliers did not hold (hp 54 → 57
  instead of ×1.5, pp 14 → 14), because the game recalculates both from the level (A7). The client showed 2 of 4
  `debug here` replies although the log has all 4 (A8).
- The units expired on their own; `status` then showed tracked 0 (spawning 0, despawning 0), and state.json held
  no unit.
- `.nyar purge`, then `.nyar purge confirm`: 20 units drained in batches of 5, and `status` returned to 0. A spawn
  during the purge cooldown replied that the cooldown is active. The second `purge confirm` ("nothing to purge")
  was not run.

### Session 6 · 2026-09-24
Boot of a541373 (A7, A8 fixed); no in-game testing. The BepInEx log was clean, but the server log
(logs/NyarDev.log, which -LogCheck did not read then) held 120 Unity errors. They were orphans from session 5's
autosave: child entities of the DontSaveEntity bandits (ability groups, casts, combat and wound buffs, the marker)
were saved without their unit ("is trying to attach to Entity.Null", "Buff … points at buff.Target 0:0",
modifiable remap failures). The game cleaned them up. The fix is A9: units now save normally. A10 makes
-LogCheck read the server log.

### Session 7 · 2026-09-24
Reboot of a541373, no spawns. No Unity errors, but 15 "Could not map an old modification source entity" warnings
remained from session 5's save (A9). Because the orphans recurred, the dev world's save
(save-data-nyardev/Saves/v4/nyardev) was deleted before session 8, keeping Settings and adminlist.txt, as the
owner decided.

### Session 8 · 2026-09-24
Build acc8c3d (A9: no DontSaveEntity; `debug here` shows the recipe) on the reset world; fresh-world boot with 0
orphan errors and 0 Unity errors; ManualSpawnLifetimeSeconds back at 300.
- `.nyar spawn CHAR_Bandit_Thug` and `.nyar spawn CHAR_Bandit_Thug 3 +2 1.5 1.2`; `debug here` gave all 4 lines in
  one message (A8), each "recipe ok". Tuned units: lvl 18, hp 86/86, pp 17; the plain one lvl 16, hp 54, pp 14.
  Against session 5's untuned level-18 bandit (hp 57, pp 14) that is ×1.51 and ×1.21, so the A7 multipliers hold.
- One unit killed: no loot. `status` showed the other 3 tracked. Autosaves ran with the 3 alive; the server was
  stopped (restart 1).

### Session 9 · 2026-09-24
Boot after restart 1: "boot sweep: 3 marked units queued for despawn (3 listed in state.json)", then "despawn batch: 3
of 3 destroyed". The server log held 0 orphan errors, so the saved units and their children loaded whole (A9). In
game the bandits were gone and `status` showed nothing tracked. Two autosaves ran after the drain; state.json held
no unit; the server was stopped (restart 2).

### Session 10 · 2026-09-24
Boot after restart 2: "boot sweep: 0 marked units queued" and 0 orphan errors, so nothing of ours survived into the
second save. `.nyar spawn CHAR_Bandit_Thug 5`, then `.nyar purge` and `.nyar purge confirm`: "purge: 0 events
ended, 5 units queued, 0 spawns cancelled, cooldown 60s" and "despawn batch: 5 of 5 destroyed". A second
`purge confirm` replied "nothing to purge", and `status` showed nothing tracked. Warnings in the BepInEx log came only
from Beelzebub and Il2CppInterop; the server log held only the game's baseline warnings (PrefabLookupMap,
RepairVBloodProgressionSystem, Crashpad), the same as on the fresh world.

## Open questions

- D28's "still loading" reply cannot be seen in game (players connect only after startup); step 5, where the
  command classes are built, decides how it is evidenced.
