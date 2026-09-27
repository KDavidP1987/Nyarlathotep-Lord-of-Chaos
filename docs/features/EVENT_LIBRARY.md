# Event library — templates and in-game authoring

**Status:** in build (docs/dod/event-library.md, audit docs/audits/event-library.md); step 1 of 6 (records and pure
logic) done, step 2 (services and commands) next. Nothing of it ships yet; 0.4.0 is the current release.

## Goal

An admin runs events without learning the events.json schema. A built-in catalogue of templates is copied into
events.json from chat. Events are created, copied, deleted and edited field by field from chat, and the pillar switches
are turned on and off from chat. The live-use milestone (Epic D47): on a fresh install, five chat commands run an event
with no file edits.

## What ships

- **Template catalogue** (`Resources/templates.json`, compiled into the DLL, read-only). Six starter templates, all
  disabled:
  - `legion-weekend-surge`, `bandit-vengeance`, `undead-nightfall` and `militia-crackdown` (empowerment);
  - `bandit-ambush` and `undead-rising` (spawns).
- **Template commands:** `.nyar template list [pillar] [page]`, `.nyar template info <id>` and
  `.nyar template use <t> [as <id>]` (the last writes a disabled copy).
- **Authoring commands:**
  - `.nyar event new <id> <pillar>`, `.nyar event copy <id> <newId>` and `.nyar event delete <id> [confirm]`;
  - `.nyar event set` gains the trigger fields (`trigger.type`, `days`, `times`, `phase`, `bosses`), `action.factions`,
    `action.units` (`CHAR_<name>[:<count>]` entries) and `location here`.
- **Pillar commands:** `.nyar pillar list` and `.nyar pillar <name> on|off`, saved to the cfg.
- **Readiness column:** `.nyar event list` shows why each event would or would not start: `ready`, `off (purge)`,
  `off (mod)`, `off (pillar)`, `full (cap)`, `invalid: <reason>` or `off (event)`.

Every chat write is a file edit plus a reload (the stale-file and newer-schema refusals, one .bak), so a chat write and a
hand edit give the same result.

## Test plan

- **Unit tests** (Nyarlathotep.Tests): every control of the plan's D-items has a failing, a passing and an empty case,
  listed in `ControlCases.cs` and checked against the plan by ControlCaseTests (D31).
- **Session 1** (unattended): templates load 6/6 valid in game; legion-weekend-surge (retimed) and undead-nightfall
  start and end on their own.
- **Session 2** (owner, 127.0.0.1:9876):
  - part A, the milestone on a fresh install (D23, with the damage-number check moved from faction-empowerment);
  - part B, the authoring smoke list (D21);
  - part C, the non-admin walk (D22);
  - part D, the pillar switch persisting through a hand edit and a restart (D15).
  - Session 2 also reruns the Epic kill-switch check (Epic D11): a 30-unit spawn event and an empowerment event purged
    together, then a second `.nyar purge confirm` replying "nothing to purge".
- **Session 3:** the four-hour soak of all six templates, with a restart mid-event (D25, D26).

## Open questions

None open. Decisions are recorded as the plan's assumptions S-1 to S-14 and amendments.

## Test results
