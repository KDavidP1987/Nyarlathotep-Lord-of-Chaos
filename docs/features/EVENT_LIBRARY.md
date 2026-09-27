# Event library — templates and in-game authoring

**Status:** in build (docs/dod/event-library.md, audit docs/audits/event-library.md); steps 1–3 of 6 (logic, services,
commands, checks, Session 1 and the soak tool) done; step 4's Session 2 ran on 2026-09-27 (D15, D23 and Epic D11 pass; D21
cases 1 and 12 and D22's four silent refusals open); Session 3 passed both after the fixes; Session 4's probe led to
A23 (waves regroup on the centre's level), confirmed in Session 5 (D35 passes). Nothing of it ships
yet; 0.4.0 is the current release.

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
    `action.units` (`CHAR_<name>[:<count>]` entries) and `location here`, which stores your x, height and z (a Point
    without `y`, written by hand or by an earlier version, still spawns at height 0).
- **Waves stay on the centre's level (A23):** the game drops each unit onto the terrain under its ring point. A unit that
  lands on another level than the wave's centre (more than 2 m above or below it, e.g. past a plateau's edge) is moved
  once, about a second after it appears, to within 1 m of the centre. `.nyar spawn` does the same around the admin; a
  Point without a stored height is left as it lands.
- **Pillar commands:** `.nyar pillar list` and `.nyar pillar <name> on|off`, saved to the cfg.
- **Readiness column:** `.nyar event list` shows why each event would or would not start: `ready`, `off (purge)`,
  `off (mod)`, `off (pillar)`, `full (cap)`, `invalid: <reason>` or `off (event)`.
- **Long triggers:** a list or info line that would pass one chat message (480 bytes) shows a V Blood trigger as
  `vbloodkilled <n> bosses`; `template info` and `event info` then list the bosses on `bosses:` lines.

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

Decisions are recorded as the plan's assumptions S-1 to S-14 and amendments. Open:

- Ground height on uneven terrain: settled. The Session 4 probe showed the game snaps each unit to the ground itself;
  the owner chose to regroup units that land on another level than the centre (A23, D35), confirmed in Session 5.
- Ambush stealth (owner, Session 3): units that stay hidden until a player passes. Owner decision 2A: new scope for a
  child plan after 0.5.0 (the game's AB_Bandit_Ambush_Buff / RevealBuff and Deadeye Camouflage are the leads);
  bandit-ambush ships unchanged.

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

Observed (owner Chaos on 127.0.0.1:9876, 14:58–16:14 server time; two boots of the build at 31e73ea, DLL 93eddc69b87e9521):

**Part A — D23 passes, with one deviation.**
- The five commands, as sent: `.nyar template list` (six templates), `.nyar template use undead-nightfall` → "template undead-nightfall
  added as undead-nightfall (disabled); .nyar event enable undead-nightfall to arm it", `.nyar pillar empowerment on` → "pillar
  empowerment on (saved to cfg)", `.nyar event enable undead-nightfall` → "event undead-nightfall enabled". Two `.nyar status`
  before dusk → "No active events." / "tracked units: 0 (spawning 0, despawning 0)". The owner then moved the clock with the
  game's admin time commands (not a file edit). The log read "event undead-nightfall started by GameTime night" and "sweep 107
  applied, 3 skipped", and `.nyar status` at 15:10 → "Undead nightfall: 20 min left" / "tracked units: 0 (spawning 0, despawning 0)".
  No file was edited by hand between boot and that reply.
- Damage. A Rotting Ghoul before `pillar empowerment on` hit 4, 4, 4 in armour, too small to show ×1.25. Retest without armour
  against CHAR_Undead_SkeletonSoldier_Withered (sword): 8 per hit while undead-nightfall ran, 7 per hit after
  `.nyar event stop undead-nightfall` ended it. 8 / 7 = ×1.14, inside ×1.25 ±10 % (1.125–1.375) with whole-number damage.
  Deviation: the unbuffed number was measured after the event, not before `pillar empowerment on`; same unit type and gear
  for both. Attack and move speed: no change seen, as expected — undead-nightfall sets physicalPower and spellPower 1.25 only
  (no speed or health modifier); the step's "visibly faster" wording was wrong.

**Part B — D21: 15 of 17 cases pass; cases 1 and 12 fail.** Each block below is the chat verbatim (timestamps dropped).
1. FAIL — `.nyar template list` gave the six lines in three messages. Five are whole. The militia-crackdown line is cut:
   "militia-crackdown empowerment vbloodkilled CHAR_ChurchOfLight_Sommelier_VBlood, … ,CHAR_ChurchOfLight_Cardinal_VBlood,CHAR_"
   — the rest of its boss list and its title are eaten (chat message length). After case 4 undead-nightfall's line gained
   "(in events.json)".
2. pass — "no templates for pillar zones".
3. pass — "bandit-ambush "Bandit ambush" disabled pillar spawns trigger manual duration 600s" / "conditions: minPlayers 0,
   cooldown 0 min, chance 100%, window none, mode any" / "action: 3 waves every 60s, radius 10, at the admin, units 4
   CHAR_Bandit_Thug, 2 CHAR_Bandit_Hunter" / "not running".
4. pass — "template bandit-ambush added as bandit-ambush (disabled); .nyar event enable bandit-ambush to arm it".
5. pass — "event bandit-ambush already exists; use .nyar template use bandit-ambush as new-id".
6. pass — "template bandit-ambush added as ambush-2 (disabled); .nyar event enable ambush-2 to arm it".
7. pass — "event my-surge created (disabled, empowerment); set its fields with .nyar event set".
8. pass — "event ambush-2 copied to ambush-3 (disabled)".
9. pass — "event my-surge trigger.type = Schedule", "event my-surge trigger.days = Sat,Sun".
10. pass — "event my-surge action.factions = Faction_Legion".
11. pass — "event ambush-2 action.units = CHAR_Bandit_Thug:3".
12. FAIL — every reply whole: "event ambush-2 action.location = Point -1805.0, -1850.9", "event ambush-2 enabled", "pillar
    spawns on (saved to cfg)", "event ambush-2 started". The owner, back at the marked spot, saw no bandits appear. Cause
    (code): `location here` stores only x and z, and WaveAction.cs:34 spawns a Point location at height 0, under the terrain
    of a spot above sea level. A defect amendment follows; the case is rerun after the fix.
13. pass — "event ambush-2 is running; stop it first".
14. pass — "event ambush-2 stopped", "delete ambush-3? run .nyar event delete ambush-3 confirm within 30 s", "event ambush-3
    deleted (events.json.bak keeps the previous file)".
15. pass — five lines: "empowerment on (Pillars.FactionEmpowerment)", "spawns on (Pillars.EventSpawns)", "boss off
    (Pillars.BossReinforcements)", "zones off (Pillars.DefendedZones)", "sieges off (Pillars.SiegeWaves)".
16. pass — "pillar spawns off (saved to cfg)".
17. pass — "page 1/1" and nine lines; "bandit-ambush off (pillar) spawns manual", "my-surge off (event) empowerment schedule
    Sat,Sun 20:00", "undead-nightfall ready empowerment gametime night RUNNING".

**Part C — D22: nothing changed; not every command gave a refusal.** A non-admin client (no `adminauth`) ran the eleven
commands at 15:50–15:52, then the ones without a reply again at 15:58–15:59. Seven were refused with "[vcf] [denied]
template", "… pillar" or "… event" (template list, template info, pillar list, template use, event new, event copy, pillar
spawns on); four gave no reply in either pass (event delete, delete confirm and both event set). The silence is not per
command: template info and event copy were silent in the first pass and refused in the second, all eleven go to one
`[Command]` method each (EventCommands.Event takes the verb as a parameter, so VCF has no overloads to resolve), and the mod
patches no chat system; VCF 0.10.4 logs nothing for a denial, so the cause of the dropped replies is not established. The
SHA-256 of events.json (5B049CF8…852F) and kdpen.Nyarlathotep.cfg (C7416CCE…CFEF3) were equal before part C, after the
first pass and after the second, and the log had no "admin ran" line between them. The owner accepted the missing replies
("okay as long as it's not running anything"); D22's "gets VCF's refusal for each" is recorded as not met for those four.

**Part D — D15 passes.** Claude copied the cfg and set Debug.VerboseLogging = false → true by hand (1 byte shorter, no BOM
before or after). `.nyar pillar spawns on` → "pillar spawns on (saved to cfg)". After round 3 the diff against the copy was
exactly two lines, "VerboseLogging = true" and "EventSpawns = true". After the restart `.nyar pillar list` → "spawns on
(Pillars.EventSpawns)", and the second boot logged "empower tick: 106 removals (batch 200), 0 still queued", a line
EmpowerAction.cs:104 writes only with VerboseLogging on — the hand edit held.

**Part E — Epic D11 (the kill switch with a spawn event and an empowerment event) passes.**
- The four `event set` / `enable` replies were as expected. `.nyar event start undead-nightfall` → "already active" (the night
  trigger had restarted it). `.nyar event start bandit-ambush` → "event bandit-ambush started"; the log shows wave 1/2 and
  wave 2/2, 15 units each, in spawn batches of 10 and 5 (MaxSpawnsPerTick 10): 30 spawned. The owner killed 8.
- `.nyar purge` → "purge ends 2 events and despawns 22 units; run .nyar purge confirm within 30 s"; `.nyar purge confirm` →
  "purged: 2 events, 22 units queued"; the log "purge: 2 events ended, 22 units queued, 0 spawns cancelled, cooldown 60s",
  "empower undead-nightfall stopped: 106 removed, 0 left to expire", and despawn batches 5, 5, 5, 5, 2 (MaxDespawnsPerTick 5)
  ending "0 left". `.nyar status` in the same minute → "No active events." / "tracked units: 0 (spawning 0, despawning 0)".
- The second `.nyar purge confirm` → "nothing to purge".

**Restart after a hard stop.** Claude force-stopped the server after part E. Its last autosave (AutoSave_1115, 16:07:57) was
taken after both waves and before the purge, so the second boot's sweeps found "boot marker sweep: 30 found, 30 queued for
despawn (0 listed in state.json)" and "boot carrier sweep: 106 found, 106 queued for removal", despawned in batches of 5 to
"0 left"; `.nyar status` then showed 0 tracked units. The orphan sweep is what makes a hard stop safe; next time a server is
stopped only after an autosave that follows the purge.

**Logs.** The second boot's -LogCheck: "0 unhandled, 15 nyar lines, 0 orphan errors, 0 unity errors"; BepInEx has the three
known warnings (Il2CppInterop, two Beelzebub TUNE); the Unity log has 224 + 2 PrefabLookupMap "unknown state" lines, all
before "Startup Completed", and 0 exceptions. The first boot's BepInEx and Unity logs were overwritten by that restart
before a copy was taken; its lines above are the reads Claude took during the session (no [Error] line and no
Nyarlathotep warning other than the purge summary in them), not a full scan. Then `pwsh tools/dev-snapshot.ps1 -Restore` →
"snapshot restored; hashes equal (s2, C:\Users\<user>\AppData\Local\Temp\nyar-snap-s2 deleted)".

Follow-ups: the two D21 failures become amendments (template list line length, a discovered gap in D4; the Point spawn
height, a defect against D11), each fixed and rerun in a short Session 3 before step 5.

### Session 3 · 2026-09-27 · event-library step 4 retest (owner, 127.0.0.1:9876, build c95fcbe)

Setup (Claude): `pwsh tools/dev-snapshot.ps1 -Save s3` (28 files); the Release DLL of c95fcbe deployed (ACDC9D8264A48658);
the dev world booted: "templates: 6/6 valid", both boot sweeps 0 found, warnings only the three known ones and the dev
world's old example-empowerment ("pillar empowerment takes an Empower action", the 0.4.0 migration note). No restart in
this session; both logs are copied before the stop.

**Round 1 — admin: D21 cases 1 and 12 again (A21, A20).**
- R1. `.nyar template list` → six lines, each whole; militia-crackdown reads
  `militia-crackdown empowerment vbloodkilled 15 bosses "Militia crackdown"`.
- R2. `.nyar template info militia-crackdown` → the head line ending "trigger vbloodkilled 15 bosses duration 900s", then a
  "bosses: …" line naming all 15, then conditions, action and "not running".
- R3. `.nyar pillar spawns on`, `.nyar template use bandit-ambush as ambush-h`,
  `.nyar event set ambush-h action.units CHAR_Bandit_Thug:3`, `.nyar event set ambush-h action.waves 1`.
- R4. Stand on a recognisable spot on raised ground and run `.nyar event set ambush-h location here` → "event ambush-h
  action.location = Point <x>, <z> at height <y>" with y not 0.
- R5. `.nyar event enable ambush-h`, walk about 30 m away, `.nyar event start ambush-h` → 3 bandit thugs appear on the
  ground at the marked spot.
- R6. `.nyar event info ambush-h` → the action line ends "at <x> <z> height <y>, units 3 CHAR_Bandit_Thug".
- R7. `.nyar event stop ambush-h`, then `.nyar status` → no active events, tracked units 0.

**Round 2 — non-admin: D22's four silent commands, spaced.** Claude hashes events.json and the cfg first. Quit to the
main menu, Direct Connect again without `adminauth`, and send each command about 15 s after the previous reply (or 15 s
after sending, if none comes): `.nyar event delete ambush-h`, `.nyar event delete ambush-h confirm`,
`.nyar event set ambush-h durationSeconds 60`, `.nyar event set ambush-h location here`. Claude hashes both files again.

Observed (owner Chaos, 16:34–16:50 server time, one boot of c95fcbe):

**Round 1 — D21 cases 1 and 12 pass.**
- R1: six whole lines; "militia-crackdown empowerment vbloodkilled 15 bosses "Militia crackdown"" arrived in the second
  message; bandit-vengeance (eight bosses, it fits) still names them.
- R2: "militia-crackdown "Militia crackdown" disabled pillar empowerment trigger vbloodkilled 15 bosses duration 900s", then
  one "bosses: CHAR_ChurchOfLight_Sommelier_VBlood, … ,CHAR_ChurchOfLight_Paladin_VBlood" line with all 15 names, then
  "conditions: minPlayers 0, cooldown 30 min, chance 100%, window none, mode any", "action: empower Militia, ChurchOfLum:
  maxHealth x1.3", "not running".
- R3: "pillar spawns already on" (the restored dev cfg has it on), "template bandit-ambush added as ambush-h (disabled); …",
  "event ambush-h action.units = CHAR_Bandit_Thug:3", "event ambush-h action.waves = 1".
- R4: "event ambush-h action.location = Point -1833.5, -1823.6 at height 0.3" — low, nearly flat ground.
- R5: "event ambush-h enabled", "event ambush-h started"; log "wave 1/1: 3 units queued", "spawn batch: 3 of 3 spawned". The
  owner: "Ambush units showed up appropriately."
- R6: "action: 1 waves every 60s, radius 10, at -1833.5 -1823.6 height 0.3, units 3 CHAR_Bandit_Thug" and "running: started
  by manual, 586s left, wave 1/1".
- R7: "event ambush-h stopped" (log "3 units queued", then "despawn batch: 3 of 3 destroyed, 0 requeued, 0 left"); `.nyar
  status` → "No active events." / "tracked units: 0 (spawning 0, despawning 0)".
- Session 2's case 12 reread: that log had shown both waves spawned ("spawn batch: 3 of 3 spawned" twice, six units
  despawned at the stop), so the units existed; with the ground here at 0.3, height 0 alone does not explain why none was
  seen. A20 stays right on raised ground (Round 3), but the Session 2 cause is not established.

**Round 2 — D22's four commands: each refused; a repeat within about a minute is not shown.** As a non-admin: `event delete
ambush-h` → "[vcf] [denied] event" (16:42); `event delete ambush-h confirm` → nothing at 16:42, "[vcf] [denied] event" on the
resend at 16:43; `event set ambush-h durationSeconds 60` → nothing twice at 16:44, refused on the third send at 16:45; `event
set ambush-h location here` → refused (16:48). Every silent send came within about 60 s of the previous identical "[vcf]
[denied] event" line, as in Session 2, where refusals of other groups (template, pillar) showed in between — consistent with
an identical system reply being suppressed for about a minute (not in the mod: it patches no chat system). The log has no
"admin ran" line between 16:36 and 16:49; the cfg hash is unchanged, and events.json.bak (written by the admin's 16:49 write)
equals the events.json hash taken before this round (F9B59B4E…C707), so nothing changed while the non-admin typed.

**Round 3 — raised ground: a finding.** As admin on a hilltop: "event ambush-h action.location = Point -1771.6, -1808.6 at
height 5.0"; `event start` → 3 of 3 spawned. The owner: "They showed up on the side of the higher ground, so on the cliff face
where they were stuck. Only saw one of them." Every unit of a wave spawns on a ring of action.radius (10 here) at the centre's
height, so on a small rise the ring points lie beyond its edge at 5 m, inside the slope or above lower ground. `event stop`
→ "3 of 3 destroyed, 0 left"; `.nyar status` → tracked units 0. Follow-up: an amendment for units placed on the ground at
each ring point (research on the game's height lookup is under way).

**Owner note (design):** "Ideally, for an ambush, it should utilize units with stealth … that appear as invisible until you
cross past them." Recorded under Open questions for a decision; the bandit-ambush template is unchanged.

**Logs.** Both logs copied before the stop, which came after AutoSave_1127 (16:51:44) followed the last action (16:50).
-LogCheck: "0 unhandled, 74 nyar lines, 0 orphan errors, 0 unity errors"; BepInEx: the three known warnings, the dev world's
old example-empowerment line at each reload, and the two stop summaries; Unity: 0 exceptions, no PrefabLookupMap line after
"Startup Completed". Then `pwsh tools/dev-snapshot.ps1 -Restore` → "snapshot restored; hashes equal (s3,
C:\Users\<user>\AppData\Local\Temp\nyar-snap-s3 deleted)".

### Session 4 · 2026-09-27 · A22 ground-height probe (owner, 127.0.0.1:9876, build 86f6f53)

Setup (Claude): `pwsh tools/dev-snapshot.ps1 -Save s4`; `Debug.VerboseLogging = true` in the dev cfg (the snapshot restores
it); the Release DLL of 86f6f53 deployed (189334244498E48D). The build adds only Services/GroundProbe.cs, a log-only probe: for each
spawned unit, at spawn and about 1 s and 5 s later, its position against its ring point's height, the game's `Height`
component and whether it has `SnapToHeight` or `FallToHeight`. Both logs are copied before the stop.

**Round 1 — admin, on the Round 3 hilltop (about -1771.6, -1808.6, height 5).**
- P1. `.nyar spawn CHAR_Bandit_Thug 3` → "spawned 3 CHAR_Bandit_Thug"; note where each appears (radius 3).
- P2. `.nyar pillar spawns on`, `.nyar template use bandit-ambush as ambush-p`,
  `.nyar event set ambush-p action.units CHAR_Bandit_Thug:3`, `.nyar event set ambush-p action.waves 1`,
  `.nyar event set ambush-p location here` (on the hilltop, near its edge).
- P3. `.nyar event enable ambush-p`, walk about 20 m away down the hill, `.nyar event start ambush-p`; note how many
  thugs you see and whether each stands on the surface, is inside the slope, or is floating.
- P4. `.nyar event stop ambush-p`, then `.nyar status` → tracked units 0 (the three manual thugs may still count until
  their lifetime ends; say what it shows).

Observed (owner Chaos, 17:08–17:11 server time, one boot of 86f6f53):
- P1: "spawned 3 CHAR_Bandit_Thug"; the owner: "the bandits spawned in nearby to me in the center of the platform". Probe:
  pass 0 each at y 5.00 with Height 0.00 level 0 (not yet computed), SnapToHeight present, no FallToHeight; passes 1 and 2
  each at y 5.00, Height 5.00, level 11.
- P2: "pillar spawns already on", "template bandit-ambush added as ambush-p (disabled); …", "… action.units =
  CHAR_Bandit_Thug:3", "… action.waves = 1"; `location here` → "event ambush-p action.location = Point -1764.9, -1807.4 at
  height 5.0" (the plateau's edge).
- P3: "event ambush-p enabled", "event ambush-p started"; log "wave 1/1: 3 units queued", "spawn batch: 3 of 3 spawned".
  Probe: all three at planned y 5.00 in pass 0; after about 1 s the unit at -1774.9, -1808.2 stayed at y 5.00 (level 11),
  and the units at -1760.6, -1798.3 and -1759.2, -1815.6 were at y 0.00 (Height 0.00, level 10), where they stayed at
  pass 2. The owner: "one unit spawned in on top of the hill, and the other two units spawned in on the ground nearby the
  hill"; "None of them were blocked or spawned in on the edge of the hill. However, they were on two different planes that
  don't interact."
- P4: "event ambush-p stopped" (log "3 units queued", "despawn batch: 3 of 3 destroyed, 0 requeued, 0 left"); `.nyar status`
  → "No active events." / "tracked units: 0 (spawning 0, despawning 0)".

**Finding.** The game already puts each spawned unit on the ground at its own ring point: every unit has SnapToHeight, and
within about 1 s HeightCorrectionSystem moves it to the height of the terrain level under it (ServerHeightLevel 11 → y 5.0,
10 → y 0.0). The mod's planned y does not bury a unit. What A22 has to solve is different: on a plateau, a ring point past
the edge puts its unit on the level below, cut off from the others and from the players above. Session 3 Round 3's unit in
the cliff face was likely a ring point on the slope itself (not reproduced here).

**Logs.** Both logs copied before the stop, which came after AutoSave_1133 (17:13:06) followed the last action (17:11).
-LogCheck: "0 unhandled, 80 nyar lines, 0 orphan errors, 0 unity errors"; BepInEx: the three known warnings, the dev world's
old example-empowerment line at each reload, one stop summary; Unity: 0 exceptions, no PrefabLookupMap line after "Startup
Completed". Then `pwsh tools/dev-snapshot.ps1 -Restore` → "snapshot restored; hashes equal (s4,
C:\Users\<user>\AppData\Local\Temp\nyar-snap-s4 deleted)".

### Session 5 · 2026-09-27 · A23 confirm, D35 (owner, 127.0.0.1:9876, build b4beecf)

Setup (Claude): `pwsh tools/dev-snapshot.ps1 -Save s5` (28 files); `Debug.VerboseLogging = true` in the dev cfg (the
snapshot restores it), so each regroup logs "regrouped <unit> from height <y> to its centre at height <y>"; the Release DLL
of b4beecf deployed (B665B0AE108E1ECB). Both logs are copied before the stop.

**Round 1 — admin, at the Session 4 plateau edge (about -1764.9, -1807.4, height 5).**
- C1. `.nyar template use bandit-ambush as ambush-q`, `.nyar event set ambush-q action.units CHAR_Bandit_Thug:3`,
  `.nyar event set ambush-q action.waves 1`, and at the plateau's edge `.nyar event set ambush-q location here`.
- C2. `.nyar event enable ambush-q`, step back onto the plateau about 10 m, `.nyar event start ambush-q` → all 3 thugs end
  on the plateau within about 2 s (one may flash below first), and stay near the centre.
- C3. `.nyar event stop ambush-q`, `.nyar event start ambush-q` again, and again after that: three starts in all, each
  checked as in C2; then `.nyar event stop ambush-q` and `.nyar status` → tracked units 0.

Observed (owner Chaos, 17:28–17:30 server time, one boot of b4beecf):
- C1: "template bandit-ambush added as ambush-q (disabled); …", "event ambush-q action.units = CHAR_Bandit_Thug:3",
  "event ambush-q action.waves = 1", "event ambush-q action.location = Point -1764.7, -1807.2 at height 5.0".
- C2: "event ambush-q enabled", "event ambush-q started"; log "wave 1/1: 3 units queued", "spawn batch: 3 of 3 spawned",
  then "regrouped CHAR_Bandit_Thug from height 0.0 to its centre at height 5.0" twice: two of the three ring points lay
  past the edge, and both units were brought back. "event ambush-q stopped" (log "despawn batch: 3 of 3 destroyed, 0
  requeued, 0 left").
- C3: second start "event ambush-q started", "3 of 3 spawned", one "regrouped … from height 0.0 to its centre at height
  5.0"; the third `event start` came while the second ran and replied "already active", so two waves ran (D35 asks for
  at least two). `event stop` → "event ambush-q stopped", "3 of 3 destroyed"; `.nyar status` → "No active events." /
  "tracked units: 0 (spawning 0, despawning 0)". The owner: "Everything seemed to work this time. All three of the mobs
  showed up on the plateau."

**D35 passes** (A23): on both waves every unit ended on the plateau; the three units the game had put on the ground below
were moved back to the centre's level.

**Logs.** Both logs copied before the stop, which came after AutoSave_1137 (17:31:29) followed the last action (17:30).
-LogCheck: "0 unhandled, 69 nyar lines, 0 orphan errors, 0 unity errors"; BepInEx: the three known warnings, the dev world's
old example-empowerment line at each reload, two stop summaries; Unity: 0 exceptions, no PrefabLookupMap line after
"Startup Completed". Then `pwsh tools/dev-snapshot.ps1 -Restore` → "snapshot restored; hashes equal (s5,
C:\Users\<user>\AppData\Local\Temp\nyar-snap-s5 deleted)".
