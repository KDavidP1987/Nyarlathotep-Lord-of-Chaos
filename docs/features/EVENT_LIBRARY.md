# Event library — templates and in-game authoring

**Status:** in build (docs/dod/event-library.md, audit docs/audits/event-library.md); steps 1 and 2 of 6 (logic, services,
commands and checks) done, step 3 (Session 1, an unattended boot, and the soak tool) next. Nothing of it ships yet; 0.4.0 is
the current release.

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

### Session 1 · 2026-09-26 · event-library step 3 (build 13a123a, dev world nyardev, unattended)

Setup: `pwsh tools/dev-snapshot.ps1 -Save s1` (28 files), the step 2 Release DLL deployed (hash equal to bin),
`python tools/ingame/session-events.py el1`: events.json holds a copy of each of the six built-in templates, all disabled
except legion-weekend-surge (Faction_Legion, pp 1.5, maxHealth 1.5, 1800 s, retimed to Sat 23:16) and undead-nightfall
(GameTime night, Faction_Undead, pp 1.25, sp 1.25, 1200 s, as shipped); cfg: Pillars.FactionEmpowerment,
Pillars.EventSpawns and Debug.TimingLog on. One boot, 23:10–23:48. No player connected.

- [x] D3: "templates: 6/6 valid" at boot, and "events: reloaded: 6 valid, 0 disabled" for the six copies
- [x] boot sweeps: "boot marker sweep: 0 found, 0 queued for despawn (0 listed in state.json)", "boot carrier sweep: 0 found, 0 queued for removal"
- [x] legion-weekend-surge "started by Schedule 2026-09-26 23:16 (ends 2026-09-27 03:46:00Z)"; "query 1 of 1 faction entities", "sweep 0 applied, 1 skipped (vblood 1)" (no ordinary Legion unit loaded near the world's live chunks); "ended (0 carriers expire with it)" at 23:46
- [x] undead-nightfall "started by GameTime night (ends 2026-09-27 03:47:45Z)" at the next in-game night, unprompted (S-12); "query 75 of 75 faction entities", "sweep 74 applied, 1 skipped (vblood 1)"; the next night check logged "not started by GameTime night: already active"; "ended (74 carriers expire with it)" at 23:47:45, and no "outlived the end" line followed
- [x] tick timing: 37 lines, avg ≤ 1.032 ms per minute, max 29.3 ms (a sweep tick)
- [x] soak-report dry run over this log (`-Templates` the six ids, `-MinMinutes 30`): "soak: 37 timing minutes, 2 starts, 2 ends, 0 cancelled by restart, 0 unpaired, 0 unhandled, tick avg max 1.032 ms, templates 2/6" and "soak: fail - never started: bandit-vengeance, militia-crackdown, bandit-ambush, undead-rising", the expected failure of a session that enabled two templates (step 5's soak enables all six)
- [x] `pwsh tools/dev-snapshot.ps1 -Restore` → "snapshot restored; hashes equal (s1, … deleted)"; %TEMP%\nyar-session (the el1 backups of events.json and the cfg) deleted; `-Paths -DeclaredOf event-library` → "paths: 960 walked, all in manifest; declared: 212/212 in event-library"
- logs: the BepInEx log has only the three known warnings (Il2CppInterop Class::Init, Beelzebub TUNE ×2); the Unity log's warnings are the 226 PrefabLookupMap lines at save load, the game's RepairVBloodProgressionSystem lookup notice and the Crashpad and temp-memory notices of the hard stop; none ours
- not covered here: the authoring smoke and the owner's chat (Session 2, step 4); the 4-hour soak (Session 3, step 5)
