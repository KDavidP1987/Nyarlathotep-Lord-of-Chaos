# Nyarlathotep, Lord of Chaos — design

Status: **draft for review** (2026-09-23). Nothing below is implemented except the scaffold. Decisions
marked *Open* in §9 must be settled before the code that depends on them.

## 1. Vision

V Rising's world is static between the player's own actions: enemies sit where their spawn tables put them,
and you meet more only by walking into them. Nyarlathotep, the crawling chaos, lets server admins make the
world **react** — factions that surge after their champion falls, war parties that march on castles, zones
that call for help when vampires move in, bosses that don't fight alone, and scheduled waves that turn a
quiet evening into an event.

Principles:

- **Admin-authored, off by default.** Every pillar and every event is opt-in. Installing the mod changes nothing.
- **Everything is temporary and reversible.** Buffs expire on their own; spawned units carry their own
  lifetime. If the server crashes mid-event, or the mod is removed, the world returns to vanilla.
- **Bounded.** Hard caps on units, waves, and concurrent events protect tick time and the save file.
- **Server-side only.** No client mod required. (A Raphael UI can come later through chat commands.)

## 2. The unifying model: Event = Trigger → Action → Duration

All four pillars are the same shape, so they share one engine:

```
EventDefinition
  id, name, enabled
  trigger:   Schedule | VBloodKilled | BossEngaged | BossHealthPhase | ZoneActivity | BloodMoon | Manual
  conditions (optional): min online players, time-of-day window, PvE/PvP mode, cooldown, chance %
  action:    Empower | SpawnWaves
  duration:  seconds (hard end — everything the event created is reverted/despawned)
  announce:  start/end message pools (optional)
```

| Pillar | Trigger(s) | Action |
|---|---|---|
| A. Faction empowerment | Schedule, VBloodKilled, BloodMoon, Manual | `Empower { factions, include/exclude units, stat mods, visual }` |
| B1. Sieges | Schedule, Manual (later: faction heat) | `SpawnWaves { location: CastleOf(target), behaviour: Assault }` |
| B2. Defended zones | ZoneActivity | `SpawnWaves { location: Zone, behaviour: Guard }` |
| C. Boss reinforcements | BossEngaged, BossHealthPhase | `SpawnWaves { location: AroundBoss, behaviour: JoinFight }` |
| D. Event spawns | Schedule, Manual, any | `SpawnWaves { location: Point/Zone/AroundPlayer, behaviour: Guard/Hunt }` |

`SpawnWaves` carries: unit composition (weighted list of unit GUIDs × count), wave count, interval between
waves, spawn radius, modifiers (level delta or absolute, HP ×, power ×, move speed ×, loot on/off), behaviour,
and per-unit lifetime.

## 3. Architecture

```
Plugin.Load ─ Settings ─ Harmony ─ VCF
   │
Core.TryInitialize (after SpawnTeamSystem_OnPersistenceLoad)
   ├─ EventStore        JSON event definitions + active-event state   (BepInEx/config/Nyarlathotep/)
   ├─ SpawnTracker      spawn, tag, cap, register, staged despawn, boot orphan sweep
   ├─ UnitSetup         make-it-fight recipe, modifiers, team, lifetime, marker
   ├─ TriggerBus        normalizes hooks → TriggerFired(kind, context)
   ├─ EventScheduler    1 s coroutine tick: schedules, durations, wave timers, zone sampling
   ├─ EventRuntime      active event instances; start/advance/end; enforces caps
   ├─ EmpowerAction     carrier buffs on faction units (+ catch new spawns during the window)
   ├─ WaveAction        waves + behaviours (Guard / Hunt / JoinFight / Assault)
   └─ Announcer         chat broadcasts (≤480 bytes, connected users only)
Patches: DeathEventListenerSystem (kills), VBloodSystem (feeds), PlayerCombatBuffSystem_OnAggro (boss
engaged), BehaviourStateChanged (leash override for our units), SpawnTransformSystem_OnSpawn (units
spawning during an empowerment window)
Commands: .nyar …
```

Patterns inherited: `Core`/`IsReady` gate and coroutine host (Faust), registry-is-truth spawn tracking
(Uriel), staged despawns and carrier buffs (Beelzebub). Sources for everything: `RESEARCH_NOTES.md`.

## 4. Lifecycle & cleanup (the part that must never be wrong)

1. **Spawn:** `InstantiateEntityImmediate` → `UnitSetup` → set finite `LifeTime` (event end + grace) and
   `DestroyWhenDisabled` → apply marker buff → register in `SpawnTracker` (session `HashSet` + persisted
   event id/prefab/position) → spawn visual 0.25 s later.
2. **Run:** caps checked before every spawn; units that die are pruned via the death hook.
3. **End (normal):** event duration elapses or admin stops it → despawn queue drains N units/tick;
   empowerment buffs expire on their own `LifeTime` (end also removes them explicitly).
4. **Crash / restart:** on boot, sweep for our marker → destroy (or re-adopt, if the event is still inside
   its window and resumable — Decision D8). Units also self-expire via `LifeTime`.
5. **Mod removed:** units expire via `LifeTime`/`DestroyWhenDisabled`; buffs expire via `LifeTime`. No
   prefab was ever structurally edited. Nothing permanent is left behind.
6. **Kill switch:** `.nyar purge` ends every event and despawns everything we track, staged.

## 5. Configuration

- **`BepInEx/config/kdpen.Nyarlathotep.cfg`** (BepInEx scalars, exists now): master switch, pillar switches
  (all `false`), caps (`MaxTrackedUnits` 150, `MaxUnitsPerWave` 20, `MaxConcurrentEvents` 3), announcements.
- **`BepInEx/config/Nyarlathotep/events.json`** (planned): the list of `EventDefinition`s, `SchemaVersion`
  field, validated on load (unknown GUIDs, deny-listed units, out-of-range values → the event is disabled
  with a logged reason, never a crash). Seeded on first run with **disabled examples** from embedded
  `Resources/events.default.json` (one per pillar) so admins have templates.
- **`BepInEx/config/Nyarlathotep/zones.json`** (planned): named circles `{name, x, z, radius}` created
  in-game via `.nyar zone add <name> <radius>` at the admin's position.
- **`BepInEx/config/Nyarlathotep/state.json`** (planned): active event instances (id, start, end), tracked
  units (for the boot sweep), per-event cooldown timestamps.
- Live edits: `.nyar event reload` re-reads JSON; `.nyar event set <id> k=v,k=v` for quick tweaks (Faust
  `ConfigEditor` pattern — VCF 0.10 splits on spaces).

## 6. Commands (planned, admin-only unless noted)

| Command | Purpose |
|---|---|
| `.nyar` | Overview (anyone) |
| `.nyar status` | Active events, time left, tracked unit count (anyone? — Decision D9) |
| `.nyar event list` / `info <id>` | Definitions and their state |
| `.nyar event start <id>` / `stop <id>` | Manual trigger / early end |
| `.nyar event enable|disable <id>` | Toggle without editing JSON |
| `.nyar event reload` | Re-read JSON |
| `.nyar zone add|remove|list` | Manage defended zones at your position |
| `.nyar spawn <unit> [count] [level] [hp×] [power×]` | One-off test spawn through the full pipeline |
| `.nyar purge [confirm]` | Kill switch: end everything, despawn all tracked units (two-step confirm) |
| `.nyar debug here` | Faction/territory/zone/nearest boss at your position |

## 7. Build order

Spikes run first because they decide whether the riskier pillars are feasible at all.

| Phase | Deliverable | Validates |
|---|---|---|
| **S1 spike** | Can we make a squad walk ~100 m to a point and fight there? (anchor + `Follower`, `PreCombatPosition` steps, leash override) | Sieges, zone reinforcements arriving from a distance |
| **S2 spike** | Does a marker buff + `LifeTime` + `DestroyWhenDisabled` on a spawned unit survive/clean up correctly across a restart? Does `DontSaveEntity` keep it out of the save? | SpawnTracker restart story |
| **S3 spike** | Carrier buff with `ModifyUnitStatBuff_DOTS` on a native NPC: stat change visible, reverts on expiry, survives the NPC streaming out/in | Empowerment |
| **0. Foundation** | EventStore + validation, SpawnTracker, UnitSetup, TriggerBus (death + V Blood kill), EventScheduler, EventRuntime, Announcer, `.nyar status/event/spawn/purge` | Everything |
| **1. Faction empowerment** | Pillar A end to end (Schedule + VBloodKilled + Manual) | Buff pipeline |
| **2. Event spawns** | Pillar D: waves at a point/zone with modifiers, Guard/Hunt behaviours | Spawn pipeline |
| **3. Boss reinforcements** | Pillar C: BossEngaged + BossHealthPhase triggers, AroundBoss spawning, cleanup on boss death/reset | Boss hooks |
| **4. Defended zones** | Pillar B2: zone activity sampling, thresholds, cooldowns, reinforcements | Activity sampling |
| **5. Sieges (MVP)** | Pillar B1: harassment raids at castle perimeter with target eligibility rules | S1 result |
| 6. Later | Structure damage (HookDOTS, raid windows, RaidForge deferral); faction "heat"; BloodMoon trigger; map markers; Raphael UI API | — |

## 8. Risks

| Risk | Mitigation |
|---|---|
| No API to move NPCs to a point; castle walls block pathing | Spike S1 first; siege MVP = perimeter harassment |
| Save pollution / orphans after crash | Marker + `LifeTime` + `DestroyWhenDisabled` + boot sweep (S2) |
| Performance with many units | Caps, staged spawns and despawns, one world query per tick |
| Interop churn on game patches (system/query names) | Own `EntityQuery`s, try/catch every hook, feature-flag each pillar |
| Conflicts with Bloodcraft/KindredCommands/BloodyBoss/RaidForge | Compatibility table in `RESEARCH_NOTES.md`; test with Bloodcraft installed |
| Sieges seen as griefing | Off by default, eligibility rules, community-admin pre-clearance |
| Level-gap scaling makes units unkillable | Cap level delta (default ±5), prefer HP/power multipliers |

## 9. Open decisions

| # | Decision | Options | Recommendation | Status |
|---|---|---|---|---|
| D1 | Chat command root | `.nyar` · `.chaos` · `.nya` | `.nyar` — unambiguous, matches `.beelz`/`.faust`/`.uriel`; `.chaos` risks colliding with other mods | *Open* (scaffold uses `.nyar`) |
| D2 | Event definition format | One unified `events.json` (trigger+action) · separate file per pillar | **Unified** — one engine, one validator, triggers and actions mix freely (e.g. boss kill → spawn waves) | *Open* |
| D3 | What a siege does to a castle | (a) Harassment: fight defenders/servants/exposed pieces under vanilla rules · (b) Real structure damage via `DealDamageSystem` hook, only in raid windows · (c) Both, (b) opt-in | **(c)**, shipping (a) first; (b) later behind its own switch, deferring to RaidForge when present | *Open* |
| D4 | Persistent marker for our units | `BlockFeedBuff` (Bloodcraft treats it as "familiar" — conflict) · own inert marker buff with magic value (Bloodcraft pattern) · `NameableInteractable` suffix (BloodyBoss) | **Own inert marker buff**, validated in spike S2 | *Open* |
| D5 | How empowerment reaches NPCs | Sweep at start only · sweep + hook `SpawnTransformSystem_OnSpawn` · periodic re-sweep every ~15 s | **Start sweep + periodic re-sweep**; add the spawn hook only if the sweep proves too coarse | *Open* |
| D6 | Wave engine for event spawns | Own spawner (`InstantiateEntityImmediate`) · reuse vanilla War Event (Rift) pipeline · both | **Own spawner** for control and cleanup; keep War Events as a possible later "Rift" action type | *Open* |
| D7 | Loot / XP from event spawns | None · vanilla · per-event setting | **Per-event**, default loot **off** (anti-farm), XP vanilla | *Open* |
| D8 | Events across a restart | Cancel everything on boot · resume events still inside their window | **Cancel on boot** for v1 (simplest, safest); resume later | *Open* |
| D9 | Player visibility | Players see nothing but announcements · `.nyar status` for players · full event list | **Announcements + player `.nyar status`** (active events and time left, no positions) | *Open* |
| D10 | Siege target eligibility | Any claimed castle · only if owner/clan online · also level/region gates | **Owner or clan member online**, heart not sealed/decaying, PvE/PvP availability per event, optional min gear level | *Open* |
| D11 | Schedule time basis | Real-world clock (server local) · in-game day/night · both | **Both**: real clock for "every Saturday 20:00", in-game for "each night" / "each blood moon" | *Open* |
| D12 | Raphael (client UI) integration | None · reserve `[NYAR:*]` wire + `.nyar api` later | **Defer**, but keep command replies parse-friendly | *Open* |
