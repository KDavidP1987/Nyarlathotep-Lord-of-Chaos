# Foundation — the event engine

**Status:** in build (docs/dod/foundation.md, step 1 of 9). Ships in 0.2.0. Nothing here is enabled by
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

Later steps add persistence, the spawner, the scheduler and runtime, the announcer and the commands (see the
plan's Build plan).

## Code map

| Area | Files |
|---|---|
| Pure logic (no game types; compiled into the tests) | `Nyarlathotep/Nyarlathotep/Logic/` — Model, Validation, Limits, CommandArgs, Schedule, Precedence, Idempotency, Dependency |
| Unit tests | `Nyarlathotep/Nyarlathotep.Tests/` (xUnit 2.9.3, Microsoft.NET.Test.Sdk 17.8.0, net6.0) |

## Test results

### 2026-09-24 · step 1 · unit tests
- `dotnet test Nyarlathotep/Nyarlathotep.Tests -c Release` → Passed 190, Failed 0 (ControlPrecedenceTests,
  ScheduleTests, EventValidationTests, ConfigClampTests, CommandArgTests, IdempotencyTests).
- Mutation check: swapping the purge and General.Enabled checks in `Precedence.StartBlocker` failed 8
  precedence cases; removing the occurrence-key comparison in `Schedule.Due` failed 8 schedule cases; restored
  code passes 190/190.

## Open questions

None yet.
