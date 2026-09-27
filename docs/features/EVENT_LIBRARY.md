# Event library — templates and in-game authoring

**Status:** in build (docs/dod/event-library.md, audit docs/audits/event-library.md); steps 1–3 of 6 (logic, services,
commands, checks, Session 1 and the soak tool) done, step 4 (Session 2 with the owner) in progress. Nothing of it ships yet;
0.4.0 is the current release.

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

### Session 2 · 2026-09-27 · event-library step 4 (owner, 127.0.0.1:9876)

Setup (Claude): `pwsh tools/dev-snapshot.ps1 -Save s2`; BepInEx/config/kdpen.Nyarlathotep.cfg and
BepInEx/config/Nyarlathotep/ deleted (a fresh install); the current Release DLL deployed; the dev world booted. The owner
connects by Direct Connect to 127.0.0.1:9876 and runs `adminauth` in the console (F1 / `~`) unless a round says otherwise.
After every numbered case, take a screenshot (F12) of the chat so each reply line can be recorded verbatim. The session
runs in three rounds; after each one the owner says "round N done", and Claude takes the file hashes and the cfg edit
before saying "go".

**Round 1 — part A, the milestone on a fresh install (D23).** No file is edited by hand from boot to step A6.
- A1. Find one melee Undead unit (a Rotting Ghoul, level 4, in Farbane Woods' graveyards, or any Skeleton Warrior). Let
  it hit you 3 times with the same gear you keep for A6, and note the three damage numbers.
- A2. `.nyar template list`
- A3. `.nyar template use undead-nightfall`
- A4. `.nyar pillar empowerment on`
- A5. `.nyar event enable undead-nightfall`
- A6. Wait for in-game night (the event starts at dusk; about every 2 minutes run `.nyar status` until it lists
  undead-nightfall). Then run `.nyar status` once more for the record, and let a unit of the same type as A1 hit you 3
  times with the same gear. Note the three numbers and whether it attacks and moves visibly faster. Expected: the
  numbers are 1.25× A1's, ±10 %.

**Round 1 — part B, the authoring smoke (D21).** One screenshot per case, holding every line the case printed.
- B1. `.nyar template list` → six lines.
- B2. `.nyar template list zones` → "no templates for pillar zones".
- B3. `.nyar template info bandit-ambush`
- B4. `.nyar template use bandit-ambush` → "added as bandit-ambush (disabled)".
- B5. `.nyar template use bandit-ambush` again → "already exists" with the `as` hint.
- B6. `.nyar template use bandit-ambush as ambush-2`
- B7. `.nyar event new my-surge empowerment`
- B8. `.nyar event copy ambush-2 ambush-3`
- B9. `.nyar event set my-surge trigger.type Schedule`, then `.nyar event set my-surge trigger.days Sat,Sun`
- B10. `.nyar event set my-surge action.factions Faction_Legion`
- B11. `.nyar event set ambush-2 action.units CHAR_Bandit_Thug:3`
- B12. Stand on a spot you can recognise (a rock, a tree), run `.nyar event set ambush-2 location here`, walk about
  30 m away, then `.nyar event enable ambush-2`, `.nyar pillar spawns on`, `.nyar event start ambush-2` → 3 bandit
  thugs appear at the marked spot, not at you.
- B13. `.nyar event delete ambush-2` while it runs → "is running; stop it first".
- B14. `.nyar event stop ambush-2`, `.nyar event delete ambush-3`, `.nyar event delete ambush-3 confirm` → deleted.
- B15. `.nyar pillar list` → five lines.
- B16. `.nyar pillar spawns off` → "pillar spawns off (saved to cfg)".
- B17. `.nyar event list` → bandit-ambush shows `off (pillar)`.
- Say "round 1 done" and send the screenshots.

**Round 2 — part C, the non-admin walk (D22).** Quit to the main menu, Direct Connect to 127.0.0.1:9876 again and do
**not** run `adminauth`. Each command must get VCF's refusal ("[vcf] [denied] …"), one screenshot each:
- C1. `.nyar template list` · C2. `.nyar template info bandit-ambush` · C3. `.nyar pillar list`
- C4. `.nyar template use bandit-raid` · C5. `.nyar event new x-test spawns` · C6. `.nyar event copy bandit-ambush x-copy`
- C7. `.nyar event delete my-surge` · C8. `.nyar event delete my-surge confirm`
- C9. `.nyar event set my-surge durationSeconds 60` · C10. `.nyar event set bandit-ambush location here`
- C11. `.nyar pillar spawns on`
- Run `adminauth` now, and say "round 2 done". Claude hashes both files again, copies the cfg and hand-edits
  Debug.VerboseLogging = false → true in it, then says "go".

**Round 3 — part D, the pillar switch survives a hand edit (D15), and part E, the kill switch (Epic D11).**
- D1. `.nyar pillar spawns on` → "pillar spawns on (saved to cfg)".
- E1. `.nyar event set bandit-ambush action.units CHAR_Bandit_Thug:15`, then `.nyar event set bandit-ambush action.waves 2`,
  then `.nyar event set bandit-ambush action.intervalSeconds 10`, then `.nyar event enable bandit-ambush`.
- E2. `.nyar event start undead-nightfall` (or, if it is still running from A6, nothing) — the empowerment event.
- E3. `.nyar event start bandit-ambush` → 15 thugs around you, 15 more 10 s later (30 units). Keep moving.
- E4. Right after the second wave: `.nyar purge`, then `.nyar purge confirm`, then within 10 s `.nyar status` → 0 active
  events, 0 tracked units.
- E5. `.nyar purge confirm` again → "nothing to purge".
- Say "round 3 done". Claude stops the server, diffs the cfg against the copy (exactly two lines: VerboseLogging = true
  and EventSpawns = true), restarts it and says "go".
- D2. Reconnect, `adminauth`, `.nyar pillar list` → "spawns on (Pillars.EventSpawns)". Disconnect; Session 2 is done.

Observed: (recorded when the session runs)
