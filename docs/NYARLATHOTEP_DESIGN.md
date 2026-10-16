# Nyarlathotep, Lord of Chaos — design

Status: **decisions settled** (2026-09-23). Nothing below is implemented except the scaffold. Decisions
are recorded in §9; the build is tracked by the DoD Epic `docs/dod/nyarlathotep.md`.

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
- **Server-side only.** No client mod required. (Raphael, the companion client UI, reads and drives it through `.nyar api` chat commands; see `docs/RAPHAEL_INTEGRATION_CONTRACT.md`.)

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
| A. Faction empowerment | Schedule, VBloodKilled, GameTime, Manual | `Empower { factions, includeUnits, excludeUnits, includeVBloods, stats }` |
| B1. Sieges | Schedule, Manual (later: faction heat) | `SpawnWaves { location: CastleOf(target), behaviour: Assault }` |
| B2. Defended zones | ZoneActivity | `SpawnWaves { location: Zone, behaviour: Guard }` |
| C. Boss reinforcements | BossEngaged, BossHealthPhase | `SpawnWaves { location: AroundBoss, behaviour: JoinFight }` |
| D. Event spawns | Schedule, Manual, any | `SpawnWaves { location: Point/Zone/AroundPlayer, behaviour: Guard/Hunt }` |

`SpawnWaves` carries: unit composition (weighted list of unit GUIDs × count), wave count, interval between
waves, spawn radius, modifiers (level delta or absolute, HP ×, power ×, move speed ×, loot on/off), behaviour,
and per-unit lifetime.

`Empower` (0.4.0, docs/features/FACTION_EMPOWERMENT.md) carries 1–5 `Faction_*` names, optional `includeUnits` and
`excludeUnits` (up to 20 `CHAR_` names each), `includeVBloods` (default false) and `stats`: `physicalPower`, `spellPower`,
`maxHealth`, `attackSpeed`, `moveSpeed`, each a multiplier 1.0–3.0 with at least one above 1.0. Each affected NPC gets one
timed carrier buff whose LifeTime ends with the event. A `visual` key is rejected as unknown: the owner chose no visible
aura for 0.4.0 (faction-empowerment S-2).

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

1. **Spawn:** `InstantiateEntityImmediate` → `UnitSetup` → set finite `LifeTime` (the unit's due time + the despawn queue's drain time, D17) and
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
- Live edits: `.nyar event reload` re-reads JSON; `.nyar event set <id> <field> <value>` edits one validated
  field and reloads (VCF 0.10 splits on spaces, so a value with spaces is quoted).

## 6. Commands

The one command reference. The Thunderstore README and `docs/RAPHAEL_INTEGRATION_CONTRACT.md` quote
it. **Who:** *anyone*, or *admin* (VCF `adminOnly`, Epic D5). **Child:** the child plan that builds it.

| Command | Who | Purpose | Child |
|---|---|---|---|
| `.nyar` | anyone | Overview and the commands the caller may run | foundation |
| `.nyar status` | anyone | Active events and time left; tracked-unit count for admins; never positions | foundation |
| `.nyar event list [page]` / `info <id>` | admin | Definitions, 10 per page, each with its readiness (`ready`, `off (purge\|mod\|pillar\|event)`, `full (cap)`, `invalid: <reason>`) and running (readiness: event-library); one event's trigger, action and time left | foundation |
| `.nyar event start <id>` / `stop <id>` | admin | Manual trigger (an `Admin` location spawns around you) / early end, units despawned | foundation |
| `.nyar event enable\|disable <id>` | admin | Toggle without editing JSON | foundation |
| `.nyar event unstick <id>` | admin | Mark a running event's units that have not fought lately as stuck: each is moved back unseen, and after 2 failed tries removed with no kill credit. The unit watch's cfg keys, section `[UnitWatch]`: `Enabled` (true), `StuckSeconds` (90, 30-600), `StallMinutes` (10, 0-60, 0 turns the backstop off) | unit-watch |
| `.nyar event set <id> <field> <value>` | admin | Edit one validated field of the settable-field table below; `location here` takes your position | foundation; event-library (trigger, action and location fields) |
| `.nyar event new <id> <pillar>` | admin | A disabled skeleton definition of that pillar, Manual trigger, 600 s | event-library |
| `.nyar event copy <id> <newId>` | admin | A disabled verbatim copy under a new id | event-library |
| `.nyar event delete <id> [confirm]` | admin | Delete a definition (two-step, 30 s; never a running one) | event-library |
| `.nyar template list [pillar] [page]` | admin | The built-in templates, 10 per page, marked when already in events.json | event-library |
| `.nyar template info <template>` | admin | One template's trigger, conditions and action | event-library |
| `.nyar template use <template> [as <id>]` | admin | Copy a template into events.json, disabled | event-library |
| `.nyar pillar list` | admin | The five pillar switches, on or off | event-library |
| `.nyar pillar <name> on\|off` | admin | Switch a pillar and save it to the cfg; off ends that pillar's running events | event-library |
| `.nyar event reload` | admin | Re-read events.json | foundation |
| `.nyar spawn <unit> [count] [level] [hp×] [power×]` | admin | One-off test spawn through the full pipeline | foundation |
| `.nyar purge [confirm]` | admin | Kill switch: end everything, despawn all tracked units (two-step) | foundation |
| `.nyar debug here` | admin | Faction, territory, zone and nearest boss at your position | foundation |
| `.nyar region list\|here` | admin | The map's regions with their event counts; the region you stand in | regions |
| `.nyar announce <text\|digest>` | admin | Broadcast now: free text, or the stats digest | foundation (digest: stats) |
| `.nyar zone add\|remove\|list` | admin | Defended zones at your position | defended-zones |
| `.nyar me` | anyone | Your own stats: today, week, all-time | stats |
| `.nyar top <stat> [today\|week\|all] [page] [share]` | anyone | Leaderboard, 10 per page; `share` broadcasts your line (rate-limited, admin-enabled) | stats |
| `.nyar stats hide\|show` | anyone | Leave or rejoin boards and digests | stats |
| `.nyar stats reset <player\|all> [confirm]` | admin | Clear stats (two-step) | stats |
| `.nyar api version` | anyone | Raphael handshake `[NYAR:version]` | foundation |
| `.nyar api status\|me\|top …` | anyone | Machine-readable twins of the player reads | raphael-api-core (status); stats (me, top) |
| `.nyar api events\|zones [page]` | admin | Machine-readable definitions and zones | raphael-api-core (events); defended-zones (zones) |
| `.nyar api regions [page]` | anyone | Machine-readable regions and their active events (contract §3) | regions |
| `.nyar api sub on\|off` | anyone | Push events `[NYAR:ev]` to this player | raphael-api-core |
| `.nyar api event <verb> …` / `.nyar api template use <template> [as <id>]` / `.nyar api pillar <name> on\|off` / `.nyar api purge [confirm]` | admin | Wire twins of the admin actions: the human command's flow, answered by one `[NYAR:ok]` or `[NYAR:err]` line; 5 a second per admin (contract §5a) | raphael-api-admin |
| `.nyar api templates [pillar] [page]` / `.nyar api template info <template>` / `.nyar api pillar list` / `.nyar api killswitch` | admin | Machine-readable template catalogue, pillar switches and kill switch (contract §3) | raphael-api-admin |

Stats: `kills` (event units), `events` (joined), `waves` (survived), `defences` (sieges won),
`bossadds` (boss adds killed) and `deaths` (to our units). Counting rules are in the Epic plan's
Business rules 11.

### Settable fields

Every field `.nyar event set` takes (event-library D20). A trigger field needs its trigger type, and an action field needs its
action type, except `trigger.scope` and `action.scope` (regions D9), which fit every type; `trigger.type` replaces the trigger with that type's default. The event-spawns fields (event-spawns D18) take `none` to remove a modifier or the behaviour; `action.units.N.chance` names unit entry N (1-10); `location` also takes `aroundplayer <minDist> <maxDist>`. The automation fields (automation D16) belong to the Interval (`trigger.minMinutes`, `trigger.maxMinutes`), RegionEntered (`trigger.playerCooldownMinutes`) and FactionKills (`trigger.factions`, `trigger.kills`, `trigger.windowSeconds`, `trigger.shared`) triggers. The revenge fields (revenge D11) belong to the Revenge trigger: `trigger.heat` takes 3-500, `trigger.warnAt` 1-3 ascending values below the heat, comma separated, or `none`, `trigger.vbloodHeat` 0-100, and `trigger.factions`, `trigger.windowSeconds` (60-3600 here) and `trigger.playerCooldownMinutes` fit it too; `trigger.type Revenge` gives Faction_Bandits, heat 30 in 900 s, warnings at 10 and 20, V Blood kills +10 and a 30 min cooldown. `action.fanOut` takes `none` or `<maxInstances> <minSpacing>` and needs an AroundPlayer location. The wave-sets fields (wave-sets D14) belong to a SpawnWaves action: `action.scoreboard` takes `true` or `false`; `action.waveList.N.units` takes the units as `action.units` does (a prefab may appear twice), and on a units-form event only N = 1 is taken and converts it (its units become wave 1 with the action's modifiers on each entry; units, waves, intervalSeconds and modifiers are removed and named in the reply); N up to the list's length + 1 appends a wave; `action.waveList.N.afterSeconds` takes 10-3600 or `none`, `action.waveList.N.whenCleared` `true` or `false` (neither on wave 1); `action.waveList.N.units.M.<modifier>` sets entry M's level, levelDelta or multiplier, or `none`; `action.waveList.N none` removes wave N, never the last one. The swarm fields (swarm D9) also belong to a SpawnWaves action: `action.arena` takes 15-60 or `none`; `action.winner` takes `killdeath` (any case, written `KillDeath`) or `none`; `action.growth` takes `none` only, which removes the growth and leaves the units form as it was; `action.growth.addUnits` takes 0-10, `action.growth.addLevels` 0-5 and `action.growth.whenCleared` `true` or `false`, and the first of them on an event without growth creates it. A growth key is refused on a waveList event, an `action.waveList` field on a growth event, and any edit that would leave the action with an arena, winner or growth conflict (the validator's reason). The hunting-grounds values (hunting-grounds D14) belong to a SpawnWaves action too: `location` also takes `region` (spawn anywhere in the scope regions); `action.regionLock` takes `true`, `false` or `none`; `action.behaviour` also takes `regionhunt`. `action.scope global` on an event with a Region location or a lock, `action.arena` on a locked event, `action.regionLock false` or `none` under RegionHunt and `action.fanOut` on a Region location are refused with the validator's reason. The apocalypse fields (apocalypse D16) belong to an Apocalypse action: `action.goal` takes `here` (the admin's x, y and z as a Point goal), `action.goal.radius` 10-100, `action.goal.name` 1-32 characters, `action.hordes.count` 1-10, `action.hordes.everyMinutes` 5-1440, `action.hordes.size` 5-50, `action.roamers.max` 1-100 and `action.marchSpeed` 0.5-5.0; `action.bosses` fits an Apocalypse too (revenge D31's form, on the last horde); `durationSeconds` takes 600-604800 on an Apocalypse and 30-7200 on every other kind, and an edit that would make the last horde rise at or after the end is refused with the validator's reason; every other Apocalypse key is edited in events.json and loaded with `.nyar event reload`. The wave-variety fields (wave-variety D10) belong to a SpawnWaves action: `action.pursuit` takes 15-600 (afterSeconds) or `none`, and is refused without `behaviour` Hunt, beside fanOut or beside regionLock true; `action.pools.<name>` (a name of 1-24 of a-z, 0-9 and "-") takes 1-30 distinct prefabs, comma separated, `+<prefab>` or `-<prefab>` to add or remove one member (a pool longer than one chat line is built by appends; never a 31st or a repeat, never the last member), or `none`, refused while a unit entry or boss roll still names the pool; `action.bosses` and `action.waveList.N.bosses` take 1-3 entries `<prefab>:<count>` (count 1-3) or `pool:<name>:<min>-<max>` (1-5), at most 5 bosses in all on a wave's own list or a list holding a roll, or `none`; `action.units` and `action.waveList.N.units` also take a `pool:<name>:<count>` token (count 1-50). A pool and the entry naming it can be set in either order: the definition is disabled in between with the validator's reason. Values are checked before any write, and names on reload.

| Field | Family | Who |
|---|---|---|
| `name` | definition | admin |
| `durationSeconds` | definition | admin |
| `conditions.minPlayers` | definition | admin |
| `conditions.cooldownMinutes` | definition | admin |
| `conditions.chancePercent` | definition | admin |
| `trigger.type` | trigger | admin |
| `trigger.days` | trigger | admin |
| `trigger.times` | trigger | admin |
| `trigger.phase` | trigger | admin |
| `trigger.bosses` | trigger | admin |
| `trigger.minMinutes` | trigger | admin |
| `trigger.maxMinutes` | trigger | admin |
| `trigger.playerCooldownMinutes` | trigger | admin |
| `trigger.factions` | trigger | admin |
| `trigger.kills` | trigger | admin |
| `trigger.windowSeconds` | trigger | admin |
| `trigger.shared` | trigger | admin |
| `trigger.heat` | trigger | admin |
| `trigger.warnAt` | trigger | admin |
| `trigger.vbloodHeat` | trigger | admin |
| `trigger.scope` | trigger | admin |
| `action.stats.physicalPower` | empower action | admin |
| `action.stats.spellPower` | empower action | admin |
| `action.stats.maxHealth` | empower action | admin |
| `action.stats.attackSpeed` | empower action | admin |
| `action.stats.moveSpeed` | empower action | admin |
| `action.factions` | empower action | admin |
| `action.waves` | spawn action | admin |
| `action.intervalSeconds` | spawn action | admin |
| `action.radius` | spawn action | admin |
| `action.units` | spawn action | admin |
| `action.modifiers.level` | spawn action | admin |
| `action.modifiers.levelDelta` | spawn action | admin |
| `action.modifiers.maxHealth` | spawn action | admin |
| `action.modifiers.power` | spawn action | admin |
| `action.modifiers.moveSpeed` | spawn action | admin |
| `action.modifiers.attackSpeed` | spawn action | admin |
| `action.loot` | spawn action | admin |
| `action.allowTerritory` | spawn action | admin |
| `action.behaviour` | spawn action | admin |
| `action.units.N.chance` | spawn action | admin |
| `action.fanOut` | spawn action | admin |
| `action.scoreboard` | spawn action | admin |
| `action.waveList.N` | spawn action | admin |
| `action.waveList.N.units` | spawn action | admin |
| `action.waveList.N.afterSeconds` | spawn action | admin |
| `action.waveList.N.whenCleared` | spawn action | admin |
| `action.waveList.N.bosses` | spawn action | admin |
| `action.waveList.N.units.M.level` | spawn action | admin |
| `action.waveList.N.units.M.levelDelta` | spawn action | admin |
| `action.waveList.N.units.M.maxHealth` | spawn action | admin |
| `action.waveList.N.units.M.power` | spawn action | admin |
| `action.waveList.N.units.M.moveSpeed` | spawn action | admin |
| `action.waveList.N.units.M.attackSpeed` | spawn action | admin |
| `action.arena` | spawn action | admin |
| `action.winner` | spawn action | admin |
| `action.growth` | spawn action | admin |
| `action.growth.addUnits` | spawn action | admin |
| `action.growth.addLevels` | spawn action | admin |
| `action.growth.whenCleared` | spawn action | admin |
| `action.regionLock` | spawn action | admin |
| `action.bosses` | spawn action | admin |
| `action.pursuit` | spawn action | admin |
| `action.pools.NAME` | spawn action | admin |
| `action.goal` | apocalypse action | admin |
| `action.goal.radius` | apocalypse action | admin |
| `action.goal.name` | apocalypse action | admin |
| `action.hordes.count` | apocalypse action | admin |
| `action.hordes.everyMinutes` | apocalypse action | admin |
| `action.hordes.size` | apocalypse action | admin |
| `action.roamers.max` | apocalypse action | admin |
| `action.marchSpeed` | apocalypse action | admin |
| `location` | location | admin |
| `action.scope` | action | admin |

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
| **2b. Stats** | Per-player counters, `.nyar me` / `top` / `stats`, daily digest and login stats (runs after event spawns) | Announcer + kill hooks |
| **5. Sieges (MVP)** | Pillar B1: harassment raids at castle perimeter with target eligibility rules | S1 result |
| **5b. Raphael API** | `.nyar api` reads, paging and errors, push events per the contract | Every pillar |
| Backlog | Boss encounters: timed boss/mob spawns in a point or region, timer paused while engaged, admin-designated reward (`docs/features/BOSS_ENCOUNTERS.md`; enters the Epic as a requested amendment) | Event-spawns pipeline |
| 6. Later | Structure damage (HookDOTS, raid windows, RaidForge deferral); faction "heat"; BloodMoon trigger; map markers | — |

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

## 9. Decisions

All settled with the user on 2026-09-23 in plan mode and recorded as validated assumptions S-4 to S-17 (D13–D15 and the D12 change: S-20, S-22 to S-24) in
`docs/dod/nyarlathotep.md`. A change after this point is a DoD amendment, not an edit here.

| # | Decision | Resolved |
|---|---|---|
| D1 | Chat command root | `.nyar` |
| D2 | Event definition format | One unified `events.json` (trigger + action mix freely), `SchemaVersion` 1 |
| D3 | What a siege does to a castle | Harassment MVP first (defenders, servants, exposed pieces under vanilla rules); structure damage later behind its own off-by-default switch, deferring to RaidForge |
| D4 | Persistent marker for our units | Own inert marker buff with a distinct magic value (validated in spike S2) |
| D5 | How empowerment reaches NPCs | Start sweep + re-sweep every ~15 s with remaining time; spawn hook only if the sweep proves too coarse |
| D6 | Wave engine | Own spawner (`InstantiateEntityImmediate`); War Events maybe later as a "Rift" action |
| D7 | Loot / XP from event spawns | Per event; loot default **off**, XP vanilla |
| D8 | Events across a restart | Cancel on boot (v1); marked survivors swept |
| D9 | Player visibility | Announcements + player `.nyar status` (active events, time left, no positions) |
| D10 | Siege target eligibility | **All admin-configurable:** owner or clan member online **or last online within `RecentlyOnlineHours`** (so logging out does not dodge a siege); optional **minimum castle-heart level** (not gear level — gear can be swapped); never sealed/decaying hearts; PvE/PvP availability per event; re-checked every tick |
| D11 | Schedule time basis | Both: real server-local clock and in-game day/night |
| D12 | Raphael (client UI) integration | **In v1.0** (changed 2026-09-23, Epic A5): `[NYAR:*]` wire behind `.nyar api …`, human replies unchanged; handshake in foundation, reads and push events in the `raphael-api-core` child (next after foundation, Epic A20), later pillars adding their own rows; contract `docs/RAPHAEL_INTEGRATION_CONTRACT.md` |
| D13 | Player names on boards | Allowed on leaderboards, stat replies and digests, never positions; admins excluded by default, admin ignore list, player opt-out `.nyar stats hide` (Epic A3, S-22) |
| D14 | Stats persistence | `stats.json`, 30 daily buckets + all-time, admin reset (Epic A6, S-23) |
| D15 | Mod-initiated messages | Wave warnings + event banners, daily banner with digest, admin on-demand banner, private login stats, rate-limited player share; each off by default (Epic A7, S-24) |
| D16 | How siege waves reach a castle | **Short chase** (owner, 2026-09-24, after spike S1): waves spawn outside the walls 40–50 m from an online defender and walk in on an aggro chase; the approach is conveyed by the D15 warning. Units with no target for 15 s are re-targeted on the nearest defender or despawned. A long march (hidden relay, or hooking the system that drops targets beyond about 86 m) is a later research item beside the Phase 2 HookDOTS decision |
| D17 | Who removes an ended event's units | **The despawn budget** (owner, 2026-09-25, foundation A16, after session 15): units are queued at event end + grace and drained at MaxDespawnsPerTick; LifeTime is set to that time + ceil(MaxTrackedUnits / MaxDespawnsPerTick) s + 60 s, a backstop for when the mod stops, so the game's lifetime system no longer removes a large event's units all at once. Extended to every unit (owner, 2026-09-25, foundation A21): a unit whose own lifetime or `.nyar spawn` lifetime decides its due time is queued by the ledger at that time the same way |
| D18 | Paths a build step creates and deletes within the step | **Outside the declared-paths check** (owner, 2026-09-27, event-library Review 11 F1): `-Paths -DeclaredOf` counts what a step leaves (git changes, new files, present and tool-named %TEMP% folders); a path written and deleted inside one step leaves nothing to roll back, and each step's `/code-review` and Codex cross-inspection read the code that would write it. A write journal was declined as noisy and costly for no lasting effect. Widened by the owner on 2026-09-27 (event-library Review 15 F2): a file copied with its old timestamp into an ignored or server path, and a %TEMP% folder created and removed between -Paths' two listings, are the same case — the observation boundary is what a step leaves, not a filesystem watcher |
| D19 | First Thunderstore publication of the event engine | **0.5.0 now, 0.5.1 after** (owner, 2026-09-28, Decision 1): 0.5.0 already runs scheduled and triggered events; its deep-water known issue is on the page and walkable-spawns fixes it in 0.5.1 |
| D20 | Order of the remaining children | **walkable-spawns, raphael-api-admin, regions, event-spawns, boss-reinforcements, anti-farming, outbreak, sieges, spawn-extras, stats** (owner, 2026-09-28, Decision 2; Epic A26-A32) |
| D21 | Global or regional events | **Every trigger and action takes `scope: Global` or a list of the game's world regions** (owner, 2026-09-28; child regions, Epic A28). A trigger scope means the kill's region for a V Blood kill, and "at least one online player is in a named region" for Schedule, GameTime and Manual triggers (owner, 2026-09-28, regions Review 1 F1) |
| D22 | Anti-farming | **A child of its own that absorbs defended-zones** (owner, 2026-09-28, Decision 4; Epic A29): dwell plus kills trips escalating reinforcements that march on the farming group, V Blood copies at a high tier, ending when the group leaves or dies |
| D23 | Horde modes | **Outbreak with spawning rules, horde waves and boss-linked empowerment** (owner, 2026-09-28, Decision 5; Epic A30): origins where the faction resides, spreading outward; waves of 30-100 led by empowered boss copies; the horde loses its empowerment when its boss dies |
| D24 | Castle takeover by hordes | **Allowed as the documented exception to reversibility, off by default** (owner, 2026-09-28, Decision 6 option A; Epic A31): AllowCastleDestruction=false and a per-event flag, raid windows only, never offline-protected castles, a 5-minute clan warning, the game's own damage path, stopped by purge; built in sieges |
| D25 | Spawn extras | **Ambush, Guard and the spawn visual leave event-spawns for a later child spawn-extras** (owner, 2026-09-28, Decision 3; Epic A32) |
| D26 | Plan reviewer | **A fresh-context Claude subagent is the primary plan reviewer, up to 3 rounds; Codex keeps the cross-inspection of the code after every build step** (owner, 2026-09-28, Decision 7). A Codex or reviewer finding that repeats a settled decision (D18 and the like) is rejected by reference without a new round |
| D27 | Spawns and regions | **AroundPlayer takes the action scope; Hunt ignores it** (owner, 2026-09-28, event-spawns plan mode, decisions 1A and 2A; event-spawns A8): with a regional action.scope only players inside it are picked and the wave centre must lie in it (other ring angles tried; none usable skips the wave), while Hunt seeds players near the centre wherever they stand |
| D28 | Automation before stats | **A new child `automation` (0.8.0) comes next, before stats: the Interval trigger, per-player fan-out of AroundPlayer waves, and the triggers RegionEntered and FactionKills** (owner, 2026-09-29, plan mode Decisions 1A and 2A; Epic A34). Blood Moon, PvP-kill and castle-heart triggers wait until after 1.0 |
| D29 | Scale promise for 1.0 | **The tick budget stays promised at the default caps (150 tracked); fan-out never exceeds the caps; 500 tracked is proven at Epic close (Epic D24)** (owner, 2026-09-29, Decision 3A) |
| D30 | Regroup of AroundPlayer waves | **An AroundPlayer group has no regroup anchor: its units stay where the game grounds them at their ring points; Point and Admin waves keep the regroup (event-spawns A23)** (owner, 2026-09-29, automation plan mode option A; automation A4). The ring point takes the player's height, which is not its ground's level on uneven terrain, so a regroup there buried the units |
| D31 | Reachable spawn points | **A checked spawn point needs a walkable straight line from its origin (the picked player of an AroundPlayer group, the wave centre otherwise): free and grounded every 1 m at a level that follows the terrain (D32); a blocked line gives the farthest reachable sample at least minDist out, and a search with no such sample the farthest reachable one found (A10, confirmed by the owner after Session 1e); automation templates that ambush a player carry Hunt** (owner, 2026-09-29, automation plan mode option A; automation A7, A9). The spot test alone placed units across ponds and rivers and on scenery no one walks onto, where V Rising NPCs neither see nor reach a player |
| D32 | Walk line levels | **The walk line follows the terrain's height level: it starts at the player's level (the first of h, h-1, h+1, h-2, h+2 where the player's spot is free and grounded) and each 1 m sample may be one level up or down from the last; two or more is a cliff** (owner, 2026-09-29, automation plan mode option A after Session 1e; automation A13). **When the player's own spot is walkable at no such level, the lines start at the nearest walkable spot 1-3 m away; when none is, each point is checked alone, with no line ("spot only")** (owner, 2026-09-29, option A after Session 1f; automation A16): on a bridge deck and at a water's edge the player's spot read blocked. One level per wave read the owner's own spot by a cliff or pond as not grounded or blocked, so every line failed and the wave fell back to unchecked points |
| D33 | Halloween track | **Five deliverables for Halloween 2026, in this order: close automation as 0.8.0; wave sets (each wave its own units, stats per unit, next wave when cleared or after N s) with a per-event scoreboard as 0.9.0; a contained spider swarm with a K/D winner; revenge (per-player heat with warnings, a faction alert, growing ambush tiers); a zombie-apocalypse MVP** (owner, 2026-10-04, at the request of the players testing it with the owner). Boss-reinforcements, anti-farming's group rule, sieges, spawn-extras, NPC conversion and the Dracul horde wait until after Halloween |
| D34 | Procedure for the Halloween track | **One dod plan per deliverable with one review round (two at most); Codex cross-inspects each deliverable's diff once, not every step; vrclient scenarios (tools/vrclient, ported from Beelzebub) are the in-game evidence; the owner tests look and feel and anything needing a second player** (owner, 2026-10-04, option A). Spawn and buff safety (CLAUDE.md) is unchanged. Supersedes P1 and the per-step Codex rule of D26 for these deliverables |
| D35 | Event rewards | **Scoreboard only: the mod counts kills, deaths and participation per event and announces the top players; admins hand out rewards themselves** (owner, 2026-10-04, option A). Automatic item grants wait until after Halloween |
| D36 | Revenge target | **Ambush near the player only: growing hunt squads around the player, never in claimed territory** (owner, 2026-10-04, option A). A raid party on the player's castle belongs to sieges |
| D37 | Long events across a restart | **The zombie apocalypse resumes after a restart: its run, wave number and scoreboard persist in state.json and its units are rebuilt fresh after the boot sweep; every other event still ends on boot** (owner, 2026-10-04, option A). Narrows D8 and Epic S-11 for that one event type |
| D38 | Scoreboard names | **The end-of-event scoreboard names the top 3 players by kills with their character names, plus totals; a cfg `Scoreboard.IncludeAdmins` (default false) leaves admins out; the opt-out `.nyar stats hide` waits for the stats child and the README says so** (owner, 2026-10-04, option A). Applies Epic D16 to the wave-sets scoreboard |
| D39 | Scoreboard counting | **A kill is an event unit killed by a player or the player's familiar or summon; a death is a player killed by one of the event's units; participants are the players with at least one of either; ranked by kills, ties by fewer deaths** (owner, 2026-10-04, option A). No damage hook |
| D40 | Wave start in a wave set | **Each `waveList` wave may set `afterSeconds` (from the previous wave's start), `whenCleared: true`, or both, starting at whichever comes first; with neither it starts when the previous wave is cleared; cleared means every unit of the previous wave has died or been removed; the event's duration still ends everything** (owner, 2026-10-04, option A) |
| D41 | Wave-set victory | **When the last wave is cleared before the event's time is up, the event ends at once with "all waves defeated" and the scoreboard; a natural end and an admin stop also show the scoreboard; a purge or a restart shows none** (owner, 2026-10-04, option A) |
| D42 | Swarm growth | **A swarm's waves grow from a `growth` key that generates them from one base wave (more units and levels per wave, under the caps), not only from a hand-written waveList** (owner, 2026-10-11, option B) |
| D43 | Revenge model | **A revenge's ambush tiers are the waves of that one revenge (wave-sets' waveList); one revenge runs per definition and per player at a time, others wait while their heat lasts; the server-wide alert names the faction only, never the player or the place; heat is kept in memory** (owner, 2026-10-11, option A) |
| D44 | Apocalypse ending and spread | **A horde reaching the goal ends the run as lost; the duration running out is "survived"; any victim of an apocalypse unit, player or NPC, spreads more roamers under the caps** (owner, 2026-10-11, option A) |
| D45 | Apocalypse resume data | **The resume record keeps each scoreboard row as platform id, kills, deaths and first credit, with no name or position (names are read when the scoreboard is shown); the run's clock keeps running while the server is down, and a horde missed while down rises once at the resume** (owner, 2026-10-11, option A) |
| D46 | Revenge hunt targets | **A revenge's Hunt seeds only its target player; a companion who attacks the squad is fought through the game's own aggro but is never seeded** (owner, 2026-10-11, option A; revenge review 1 F1) |
| D47 | Revenge scoreboard | **A revenge event shows the normal end-of-event scoreboard (top 3 by name, D38): the target stays anonymous only while the hunt runs; naming players after the end is a recorded exception to the anonymous alert** (owner, 2026-10-11, option A; revenge review 1 F2) |
| D48 | Region-bound placement | **A new child, hunting-grounds (0.11.0), built after swarm and before revenge: a region lock that keeps an event's own units inside its scope regions, a Region location that spreads spawns over the regions, and a region hunt that sends units after every player inside the regions by a mover over our own units; with AroundPlayer it gives the region ambush, with a Point the spread from a point. Revenge becomes 0.12.0 and apocalypse 0.13.0** (owner, 2026-10-12, recommended option; after swarm's Session 3) |
| D49 | Apocalypse map-wide roaming | **Apocalypse roamers leave their hotspots and hunt the nearest players anywhere on the map, with no region lock, under the existing caps (roamers.max, perMinute); the hordes still march on the goal; the roaming uses hunting-grounds' mover. An amendment of the apocalypse plan at its start** (owner, 2026-10-12, recommended option) |
| D50 | Hunting-grounds behaviour | **A unit outside its regions is brought back to a walkable spot just inside the border near where it left and forgets players outside (as swarm's arena); the long-range hunt moves our units in hidden jumps only while no player is within about 100 m, and within about 60 m the game's own pathing and aggro take over; each group goes after the nearest eligible player in its regions (castle territory skipped); a region ambush picks only players standing inside the regions. Real pathing for apocalypse hordes in busy areas, where hidden jumps cannot happen, is a follow-up to study at apocalypse's start** (owner, 2026-10-12, recommended options; the owner wrote "for the sake of apocalypse, I would like to look at the ability to do passing", read here as pathing, "especially in areas with high player population or activity that they couldn't easily teleport into or around") |
| D51 | Boss in the last wave | **For Halloween, built with revenge (0.12.0): a wave event can name boss units that join its final wave, growth waves included, so apocalypse hordes and the templates can end on a boss (e.g. CHAR_Spider_Queen_VBlood for spiders, an undead V Blood for undead); players feed on its V Blood as on any V Blood** (owner, 2026-10-13, after the 0.11.0 test run) |
| D52 | Scaling to the players | **After Halloween (1.0): unit counts and levels scaled to the number of players in the scope and their average level; for Halloween, admins set the start level, HP and damage and per-wave growth** (owner, 2026-10-13) |
| D53 | Replacing units that never engaged | **After Halloween (1.0): a unit that dies before it has hit a player is replaced in its set, under a per-event replacement budget; once it has hit a player it counts as engaged. Border play is left to the players' honour** (owner, 2026-10-13) |
| D54 | Stuck units | **Before Halloween, its own child (unit-watch): a unit of ours that has not moved and has not fought for about 90 s, or stands where the walk check finds no walkable ground, is first moved back (hidden, never beside a player or inside claimed territory) to walkable ground near its group or the nearest player in scope; still stuck after 2 tries, it is removed and counts as gone for its wave, with no kill credit. A wave that waits to be cleared and sees no kill or hit for N minutes (default 10) clears its leftovers the same way, and `.nyar event unstick <id>` does it by hand. Applies to every event's units: waves, swarm, revenge, hunting-grounds and apocalypse hordes and roamers** (owner, 2026-10-14, recommended options; the owner: "if one of those units gets stuck in a place where it can't be attacked or where the players can't reach it … the wave would be stuck until the timer ran down", "either due to the teleporting process or due to the spawning process") |
| D55 | Order of the 1.0 work | **After unit-watch: D52 scaling to the players first, then D53 replacements, bosses for other wave types with the faction-compatibility checks, ambush spacing at hemmed-in spots, the pathing study (D50); then the Epic's remaining children** (owner, 2026-10-14, chose "Scaling first" over the recommended replacements-first) |
| D56 | Which units the stuck checks mark | **The standing rule (90 s) and the still-at-spawn check mark only a unit holding a player as its aggro target, never an idle unit however near a player stands; off-ground and the stall backstop are unchanged** (owner, 2026-10-14, after Session 1 showed idle spiders beside a player marked; unit-watch A2) |
| D57 | How scaling works (D52) | **Opt-in per event (off unless an admin adds a scaling block), for every spawner (SpawnWaves incl. hunting-grounds, revenge and swarm growth; apocalypse hordes and roamers). Players counted: living online players inside the event's action scope (every online player for a Global event), admins counted as the scoreboard's IncludeAdmins switch says, re-read before each wave or horde. Counts: each entry gets an admin-set number of extra units per player beyond the first, always clamped by MaxUnitsPerWave and MaxTrackedUnits. Level: the players' average gear level plus an admin offset, per-wave growth on top, clamped 1-120; V Blood bosses keep their own level unless the admin sets one** (owner, 2026-10-14, all four recommendations) |
| D58 | How replacements work (D53) | **Opt-in per event (off unless an admin adds a `replacements` block with a budget of 0-100 replacements per event run), for every SpawnWaves form (waves, growth, swarm, hunting grounds, revenge, final-wave bosses) and apocalypse hordes and bosses; apocalypse roamers keep their own refill. A unit is engaged once it has hit a player or a player has hit it; an engaged unit's death is final. A unit of ours that dies unengaged (sunlight, water, a fall, other NPCs) is replaced at its group's spawn point, walk-checked, with its own level and stats, while budget remains; stuck-unit removals and despawns are never replaced** (owner, 2026-10-14, recommended options) |
| D59 | Raphael live operations and communications (contract §10.5-§10.9) | **Two new children, live-ops then communications. L1: admins get exact coordinates (x, y, z) of every unit the mod spawned, per event or across all events in bulk, of each event's map spots (hotspots, horde fronts, the goal, a Point location) and of native NPCs holding an empowerment carrier; never a player's position or a hunt target, and players never get coordinates. For admins only this amends the apocalypse and hunting-grounds rule that hotspots, fronts, the goal and hunts never reach the wire. L2: the next-wave countdown goes to admins always and to players only for events whose chat wave warnings are on. L3: a `unit goto` twin teleports only the requesting admin. L4: `mine` sends the requester's own kills, deaths and rank. L5: the `notice` push is a copy; the chat line is still sent. L6: scheduled and recurring messages live in Nyarlathotep with event placeholders, off by default, at most 50. L7: live-ops is planned next, beside the 0.13.0 test weekend, on its own branch; communications after the 0.13.0 release; scaling and replacements keep their places after live-ops. L8: every player can read the global board (time left, wave count, the next-wave countdown per L2, today's scheduled starts, public notices) and their own personal alerts, a revenge that warned them or runs against them and an event whose units are after them, with no unit count, position, direction or distance and never another player; for the requester only this amends revenge's rule that the hunted player never reaches the wire and automation D13's focus rule; Raphael may poll it for an overlay** (owner, 2026-10-15, every recommended option, with L1 and L8 asked for by the owner: "admins see the exact coordinates of every unit spawned from the mod, per event or in bulk"; L8: "if the player themselves has any revenge against them, or if there are units coming after them ... not to see where the units are, but to simply know") |
| D60 | Faction wave templates (Epic A43) and mixed-faction gauntlets | **Six Manual waveList templates join the library: militia-muster, silverlight-crusade, legion-incursion, gloomrot-assault, cursed-night and realm-gauntlet. Apocalypse D38 is amended: a template may name units of several allied groups only when each wave is one group, every later wave starts only when the last is cleared (whenCleared with no afterSeconds), and its final-wave bosses are of the last wave's group; every other template stays in one group. Each new group's units join FactionGroups only after a faction-mix run shows them allied** (owner, 2026-10-16, the recommended option; the request of 2026-10-15: "waves of increasing numbers and types of other factions, such as militia, Silverlight units, paladins, and units from other regions, Draculin group") |
| D61 | Pursuit, timed waves, onslaught templates and unit pools | **A new child, wave-variety, for Halloween. (1) Pursuit, opt-in per SpawnWaves event: once no eligible player has been within the wave's area (its arena, else its Hunt range, around the wave centre) for an admin-set time (default 60 s, 15-600 s), the arena lifts and every living unit of the run, and every later wave, chases the nearest eligible player anywhere on the map by the apocalypse roamers' hidden jumps, then the game's pathing and aggro, until the event ends; players in claimed castle territory or in PvP combat are skipped (a castle is a refuge; sieges stay their own pillar). (2) Timed variants: every wave on a fixed beat, never early, pursuit on, one per wave faction (spiders, undead, militia, Church of Light, Legion, Gloomrot, cursed; realm-gauntlet has none). (3) Onslaught variants: units rolled from the faction's unit pool at about 1.5x the base counts within MaxUnitsPerWave, a boss roll mid-run and at the finale, the timed beat and pursuit; a boss roll may draw several distinct bosses at once (a complementary group), and a single boss is tested first. (4) Named pools per template: `pools` names lists of prefabs; a wave entry draws `count` units from a pool with repeats, a boss roll draws min..max distinct bosses; rolled fresh at each wave and logged; every pool member must be in one allied group. (5) The allied groups grow by in-game mix runs over each faction's combat units and V Bloods (ghosts, skeletons and other undead together; Legion and the Dracula units; Church of Light; Dunley militia; Gloomrot; cursed; the Oakveil Blackfangs), each run checking that the units neither fight each other while left alone nor once a player stands among them** (owner, 2026-10-16, the recommended options; the owner on bosses: "we also need the ability to designate that maybe it's multiple bosses at once, not just one boss. It's a group of bosses complementary to one another, as well as mobs. However, one boss is, of course, simpler and probably should be tested first"; on mixes: "spawn in groups of units and see what they are and are not compatible with, as well as the bosses, do they start attacking each other? If so, they're incompatible") |
| P1 | Development procedure | Pre-audit / build / post-audit with Codex cross-inspection on every step (CLAUDE.md) |
| P2 | Icon | Whole dragon artwork scaled to 256×256 (not cropped); same image as README cover |
| P3 | First Thunderstore publication | **Foundation 0.2.0 as a public beta** (owner, 2026-09-25, Epic A18): scheduled and triggered wave events with the safety rails; event spawns (0.3.0) and faction empowerment (0.4.0) follow as updates. Was: after foundation + faction empowerment + event spawns (~0.4.0) |
