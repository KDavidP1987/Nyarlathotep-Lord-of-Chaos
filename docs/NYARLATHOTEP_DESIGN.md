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

1. **Spawn:** `InstantiateEntityImmediate` → `UnitSetup` → set finite `LifeTime` (event end + grace + the despawn queue's drain time, D17) and
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
| `.nyar event list [page]` / `info <id>` | admin | Definitions, 10 per page, enabled or why disabled, running; one event's trigger, action and time left | foundation |
| `.nyar event start <id>` / `stop <id>` | admin | Manual trigger (an `Admin` location spawns around you) / early end, units despawned | foundation |
| `.nyar event enable\|disable <id>` | admin | Toggle without editing JSON | foundation |
| `.nyar event set <id> <field> <value>` | admin | Edit one validated field: `name`, `durationSeconds`, `conditions.minPlayers\|cooldownMinutes\|chancePercent`, `action.waves\|intervalSeconds\|radius` | foundation |
| `.nyar event reload` | admin | Re-read events.json | foundation |
| `.nyar spawn <unit> [count] [level] [hp×] [power×]` | admin | One-off test spawn through the full pipeline | foundation |
| `.nyar purge [confirm]` | admin | Kill switch: end everything, despawn all tracked units (two-step) | foundation |
| `.nyar debug here` | admin | Faction, territory, zone and nearest boss at your position | foundation |
| `.nyar announce <text\|digest>` | admin | Broadcast now: free text, or the stats digest | foundation (digest: stats) |
| `.nyar zone add\|remove\|list` | admin | Defended zones at your position | defended-zones |
| `.nyar me` | anyone | Your own stats: today, week, all-time | stats |
| `.nyar top <stat> [today\|week\|all] [page] [share]` | anyone | Leaderboard, 10 per page; `share` broadcasts your line (rate-limited, admin-enabled) | stats |
| `.nyar stats hide\|show` | anyone | Leave or rejoin boards and digests | stats |
| `.nyar stats reset <player\|all> [confirm]` | admin | Clear stats (two-step) | stats |
| `.nyar api version` | anyone | Raphael handshake `[NYAR:version]` | foundation |
| `.nyar api status\|me\|top …` | anyone | Machine-readable twins of the player reads | raphael-api |
| `.nyar api events\|zones [page]` | admin | Machine-readable definitions and zones | raphael-api |
| `.nyar api sub on\|off` | anyone | Push events `[NYAR:ev]` to this player | raphael-api |

Stats: `kills` (event units), `events` (joined), `waves` (survived), `defences` (sieges won),
`bossadds` (boss adds killed) and `deaths` (to our units). Counting rules are in the Epic plan's
Business rules 11.

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
| D12 | Raphael (client UI) integration | **In v1.0** (changed 2026-09-23, Epic A5): `[NYAR:*]` wire behind `.nyar api …`, human replies unchanged; handshake in foundation, reads and push events in the `raphael-api` child; contract `docs/RAPHAEL_INTEGRATION_CONTRACT.md` |
| D13 | Player names on boards | Allowed on leaderboards, stat replies and digests, never positions; admins excluded by default, admin ignore list, player opt-out `.nyar stats hide` (Epic A3, S-22) |
| D14 | Stats persistence | `stats.json`, 30 daily buckets + all-time, admin reset (Epic A6, S-23) |
| D15 | Mod-initiated messages | Wave warnings + event banners, daily banner with digest, admin on-demand banner, private login stats, rate-limited player share; each off by default (Epic A7, S-24) |
| D16 | How siege waves reach a castle | **Short chase** (owner, 2026-09-24, after spike S1): waves spawn outside the walls 40–50 m from an online defender and walk in on an aggro chase; the approach is conveyed by the D15 warning. Units with no target for 15 s are re-targeted on the nearest defender or despawned. A long march (hidden relay, or hooking the system that drops targets beyond about 86 m) is a later research item beside the Phase 2 HookDOTS decision |
| D17 | Who removes an ended event's units | **The despawn budget** (owner, 2026-09-25, foundation A16, after session 15): units are queued at event end + grace and drained at MaxDespawnsPerTick; LifeTime is set to that time + ceil(MaxTrackedUnits / MaxDespawnsPerTick) s + 60 s, a backstop for when the mod stops, so the game's lifetime system no longer removes a large event's units all at once |
| P1 | Development procedure | Pre-audit / build / post-audit with Codex cross-inspection on every step (CLAUDE.md) |
| P2 | Icon | Whole dragon artwork scaled to 256×256 (not cropped); same image as README cover |
| P3 | First Thunderstore publication | After foundation + faction empowerment + event spawns pass in-game (~0.4.0); GitHub releases before |
