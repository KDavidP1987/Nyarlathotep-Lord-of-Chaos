# Nyarlathotep, Lord of Chaos

![Nyarlathotep, Lord of Chaos](https://raw.githubusercontent.com/KDavidP1987/Nyarlathotep-Lord-of-Chaos/main/docs/img/nyarlathotep-cover.jpg)

A **server-side** event layer for V Rising. Admins stage NPC events the base game doesn't have: hunting waves
and empowered factions on a schedule, at nightfall or after a V Blood falls, a days-long apocalypse, with warnings
and banners for players, and in later releases castle sieges, defended zones and boss-fight adds.

> **Public beta (0.15.0).** Every pillar and automatic announcement is off by default, but for the login notice of
> running events; no event runs until an admin turns on its pillar and enables it.

## What it does

Every feature is an *event*: a trigger starts an action for a set time, and everything the event spawned is
removed after it ends. Features not yet released are marked *in development*.

<details>
<summary><b>Faction empowerment</b> · <i>0.4.0</i></summary>

Every NPC of up to five factions is buffed for the event's duration, on a schedule, at nightfall or daybreak,
after a V Blood kill, or by command: a Blood Moon for the NPCs. Physical and spell power, max health, attack speed
and move speed each take a multiplier from 1.0 to 3.0. The buff is a timed effect that expires on its own, so no
NPC stays changed after the event, even if the mod is removed mid-event. NPCs that respawn or load in during the
event are buffed within about 15 s. Players' servants, traders, Bloodcraft familiars and the mod's own units are
never buffed, and V Bloods only when `includeVBloods` is true. One empowerment per faction runs at a time. Turn it on
with `[Pillars] FactionEmpowerment = true`.

The seeded `example-empowerment` (off by default) looks like this; `{faction}` in a message names the factions:

```json
{ "id": "example-empowerment", "name": "Bandits rally after a V Blood falls", "enabled": false,
  "pillar": "empowerment", "trigger": { "type": "VBloodKilled", "bosses": ["any"] },
  "conditions": { "minPlayers": 1, "cooldownMinutes": 60 }, "durationSeconds": 600,
  "action": { "type": "Empower", "factions": ["Faction_Bandits"],
    "stats": { "physicalPower": 1.3, "spellPower": 1.3, "maxHealth": 1.5, "attackSpeed": 1.15, "moveSpeed": 1.1 } },
  "announce": { "start": ["The {faction} rally: {event}."], "end": ["The {faction} lose heart. {event} is over."], "warnings": false } }
```

Optional action keys: `includeUnits` / `excludeUnits` (unit names such as `CHAR_Bandit_Thug`) and
`includeVBloods` (default `false`). Faction names are the game's `Faction_*` names.
</details>

<details>
<summary><b>Event spawns</b> · <i>0.2.0; strength, Hunt and AroundPlayer 0.7.0</i></summary>

Waves of chosen units appear at a map point, around the admin, or around a random player: up to 10 unit types, up
to 10 waves, a set interval between waves. An event starts on a schedule (days and times, server-local), when night
falls or day breaks, when a V Blood dies (any, or named ones), or by command. Conditions can hold it back: minimum
players online, a cooldown, a chance, a time window. After the event ends, its units stay for `GraceSeconds` (30 s
by default), then are removed a few at a time; each also carries a timer, so none is left behind, even across a
restart.

Units land on walkable ground at the centre's level: a spawn point in water, against a cliff or a wall moves to the
nearest walkable one. **No wave spawns in claimed castle territory**: a wave centred there is skipped, and a single
spawn point there moves like one in water. Territory is read once per wave, so a castle placed during a wave counts
from the next wave (Hunt, too, uses the territory read at the latest wave).

| Action key | Values | Effect |
|---|---|---|
| `units[].chance` | 0.05–1.0 (default 1.0) | Each copy of the unit joins its wave with this chance |
| `modifiers.level` / `levelDelta` | 1–120 / −5 to +5, not both | A fixed level, or the unit's own level shifted |
| `modifiers.maxHealth` · `power` · `moveSpeed` · `attackSpeed` | ×0.5–3.0 | Multiplies the stat; `power` is physical and spell power |
| `loot` | `true` / `false` (default) | Units drop loot only when `true` |
| `behaviour` | `{ "type": "Hunt", "range": 10–60 }` | Every 5 s, each unit is set on the nearest players (up to 5) within range of the wave |
| `location` | `{ "type": "AroundPlayer", "minDist": 10–60, "maxDist": 15–80 }` | Each wave centres this far from a random online player; with Hunt, maxDist ≤ range |
| `allowTerritory` | `true` / `false` (default) | Lets waves spawn in castle territory |

A player standing in castle territory or in PvP combat is never picked by `AroundPlayer` and never hunted. With a
regional `action.scope`, only players inside it are picked. A warned wave can still be skipped (no eligible
player, a castle centre, or every chance rolled no unit): the warning promises the time, not the units.

```json
{ "id": "night-hunt", "name": "Wolves on the prowl", "enabled": false, "pillar": "spawns",
  "trigger": { "type": "GameTime", "phase": "night" }, "conditions": { "minPlayers": 1 }, "durationSeconds": 600,
  "action": { "type": "SpawnWaves", "waves": 2, "intervalSeconds": 120, "radius": 8,
    "location": { "type": "AroundPlayer", "minDist": 25, "maxDist": 40 },
    "units": [ { "prefab": "CHAR_Forest_Wolf", "count": 4 }, { "prefab": "CHAR_Forest_AngryMoose", "count": 1, "chance": 0.3 } ],
    "modifiers": { "levelDelta": 2, "maxHealth": 1.3 }, "behaviour": { "type": "Hunt", "range": 45 } } }
```
</details>

<details>
<summary><b>Automatic events</b> · <i>0.8.0</i></summary>

Three triggers start an event without anyone typing a command, and one wave key spreads a wave over several players.

| Trigger | Keys | Starts the event |
|---|---|---|
| `Interval` | `minMinutes`, `maxMinutes` (5–1440) | Again a random `minMinutes`–`maxMinutes` after its last run ended. The next start survives a restart; downtime is not replayed |
| `RegionEntered` | `scope` (region names, required), `playerCooldownMinutes` (0–1440, default 30) | When a player walks into one of the regions; the same player can start it again after the cooldown |
| `FactionKills` | `factions` (1–5), `kills` (3–500), `windowSeconds` (10–3600), `shared` (default `false`) | When one player kills `kills` NPCs of those factions within the window; with `shared`, all players' kills count together |

An event started by a player's action is aimed at that player: its `AroundPlayer` waves spawn near them first.

`"fanOut": { "maxInstances": 2–10, "minSpacing": 50–500 }` on an `AroundPlayer` wave spawns one group near each of up
to `maxInstances` players standing at least `minSpacing` m apart. Each group gets the wave's full unit list, so three
players can draw three times the units; all groups together still obey `MaxUnitsPerWave` and `MaxTrackedUnits`, and a
cap shares the allowed units out between the groups evenly.

```json
{ "id": "roaming-hunters", "name": "Roaming hunters", "enabled": false, "pillar": "spawns",
  "trigger": { "type": "Interval", "minMinutes": 60, "maxMinutes": 90 }, "durationSeconds": 600,
  "action": { "type": "SpawnWaves", "waves": 1, "intervalSeconds": 60, "radius": 10,
    "location": { "type": "AroundPlayer", "minDist": 20, "maxDist": 40 }, "fanOut": { "maxInstances": 3, "minSpacing": 150 },
    "units": [ { "prefab": "CHAR_Bandit_Thug", "count": 4 }, { "prefab": "CHAR_Bandit_Hunter", "count": 2 } ],
    "behaviour": { "type": "Hunt", "range": 45 } } }
```

The player rows behind these triggers (regions, cooldowns, kill counts) stay in memory only; no log line or push
names or locates a player.

**Known limit:** each unit's spawn point needs a straight walkable line to the player. Where none exists, for example
on a strip of land inside a pond, a wave without Hunt can stand across the water from the player. With Hunt the units
walk to the player anyway.
</details>

<details>
<summary><b>Wave sets and the event scoreboard</b> · <i>0.9.0</i></summary>

`waveList` replaces a spawn event's `units`, `waves`, `intervalSeconds` and `modifiers` with a list of up to 10
waves, each with its own units:

| Wave key | Values | Effect |
|---|---|---|
| `units` | 1–10 entries: `prefab`, `count`, optional `chance` and `modifiers` | The wave's units; each entry keeps its own level and stats |
| `afterSeconds` | 10–3600 (waves 2+) | Starts this long after the wave before it started |
| `whenCleared` | `true` / `false` (waves 2+) | Starts as soon as every unit of the wave before it is gone; with `afterSeconds`, whichever comes first |

A wave with neither key starts when the one before it is cleared. When the last wave is cleared and every wave was
fought, the event ends at once ("all waves defeated"). The caps still apply to every wave.

`"scoreboard": true` (on either form) counts, while the event runs, each kill of its units by a player (a familiar's
or summon's kill counts for its player) and each player killed by them. At the natural end, after "all waves
defeated" or on `event stop`, chat shows the top 3 by kills (ties go to fewer deaths) and the totals; a purge, a
restart, a fault or a pillar switched off shows nothing. Admins are left out unless `[Scoreboard] IncludeAdmins` is
`true`. Names appear only in those chat lines; nothing is saved. A player opt-out comes with the stats release.

```json
{ "id": "graveyard-waves", "name": "Graveyard waves", "enabled": false, "pillar": "spawns",
  "trigger": { "type": "Manual" }, "durationSeconds": 900,
  "action": { "type": "SpawnWaves", "radius": 10, "scoreboard": true,
    "location": { "type": "AroundPlayer", "minDist": 20, "maxDist": 40 }, "behaviour": { "type": "Hunt", "range": 45 },
    "waveList": [
      { "units": [ { "prefab": "CHAR_Undead_SkeletonSoldier_Armored_Farbane", "count": 6, "modifiers": { "levelDelta": 1 } },
                   { "prefab": "CHAR_Undead_ArmoredSkeletonCrossbow_Farbane", "count": 2, "modifiers": { "levelDelta": 1 } } ] },
      { "units": [ { "prefab": "CHAR_Undead_SkeletonSoldier_Armored_Farbane", "count": 6, "modifiers": { "levelDelta": 2, "maxHealth": 1.2 } },
                   { "prefab": "CHAR_Undead_ArmoredSkeletonCrossbow_Farbane", "count": 3, "modifiers": { "levelDelta": 2, "power": 1.2 } } ],
        "afterSeconds": 120, "whenCleared": true },
      { "units": [ { "prefab": "CHAR_Undead_SkeletonSoldier_Armored_Farbane", "count": 6, "modifiers": { "levelDelta": 3, "maxHealth": 1.4, "power": 1.2 } },
                   { "prefab": "CHAR_Undead_Guardian", "count": 1, "modifiers": { "level": 30, "maxHealth": 2.0, "power": 1.3 } } ],
        "whenCleared": true } ] } }
```
This is the shipped `graveyard-waves` template (its announce lines left out); `.nyar template use graveyard-waves` copies it.
</details>

<details>
<summary><b>Swarms: arenas, growing waves and a winner</b> · <i>0.10.0</i></summary>

Three more optional keys on a spawn event:

| Key | Values | Effect |
|---|---|---|
| `arena` | `{ "radius": 15–60 }`, with a Point location or one started by an admin | Every 5 s, any of the event's units more than 10 m past the edge is brought back near the centre and forgets players outside |
| `growth` | `{ "addUnits": 0–10, "addLevels": 0–5, "whenCleared": true/false }`, with `units` and `waves` 2–10 | Wave n gets each unit's count + (n−1)·addUnits and level + (n−1)·addLevels; level capped at 120; each wave starts `intervalSeconds` after the last, or also when it is cleared with `whenCleared` |
| `winner` | `"KillDeath"`, with `"scoreboard": true` | After the scoreboard, one line names the player with the best kills ÷ deaths (a player needs one kill) |

`event info` lists every generated wave before you start it. The `spider-swarm` template uses all three:
`.nyar template use spider-swarm`, `.nyar event enable spider-swarm`, then `.nyar event start spider-swarm` where the
swarm should be. Rolling back to 0.9.0 disables the events that use these keys.
</details>

<details>
<summary><b>Hunting grounds: events bound to map regions</b> · <i>0.11.0</i></summary>

Three more optional keys bind a spawn event to its `scope` regions (a list of region names, not Global):

| Key | Values | Effect |
|---|---|---|
| `location` | `{ "type": "Region" }` | Each wave spawns in groups of up to 5 at spots spread over the regions, at least 30 m apart, on walkable ground and outside castle territory |
| `regionLock` | `true` / `false` | Every 5 s, a unit that has left the regions is brought back near where it left and forgets players outside |
| `behaviour` | `{ "type": "RegionHunt" }`, with `regionLock: true` | Each group goes after the nearest player inside the regions. A unit more than 60 m away closes in with hidden jumps, only while no player is within 100 m to see it; from there it is set on the player and walks in and fights as normal |

Together with the existing locations they give four modes:

- **Region roam:** a Region location, the lock and RegionHunt. The `spider-hunting-grounds` template is one.
- **Region ambush:** a location around a player with a regional scope picks only players inside the regions, and
  with the lock the units never chase out.
- **Point spread:** a Point location with the lock and RegionHunt: the units start at the point and spread through
  the region after players.
- **Point arena:** swarm's `arena`, above.

The apocalypse (below) spreads map-wide. In chat: `.nyar event set <id> location region`,
`action.regionLock true|false|none` and `action.behaviour regionhunt`. Rolling back to 0.10.0 disables the events
that use these keys.
</details>

<details>
<summary><b>Revenge and final-wave bosses</b> · <i>0.12.0</i></summary>

A spawn event with a `Revenge` trigger lets a faction take revenge on one player:

| Key | Values | Effect |
|---|---|---|
| `trigger` | `{ "type": "Revenge", "factions": [1–5], "heat": 3–500, "windowSeconds": 60–3600, "warnAt": [up to 3 steps below heat], "vbloodHeat": 0–100 (10), "playerCooldownMinutes": 0–1440 (30) }` | Each kill of those factions' NPCs within the window adds 1 heat to the killer, a V Blood kill `vbloodHeat`; units spawned by events and kills during the player's cooldown add none. At each `warnAt` step the player gets a private warning; at `heat` the revenge starts once the player is online, alive, out of PvP combat and outside castle territory |

The revenge sends a server alert that names no one ("The Bandits swear revenge…"), a private line to the player, and
the event's waves around that player only, hunting only that player; killing every wave ends it with a private end
line. Then the player's cooldown runs. The event must be a spawn event with an `AroundPlayer` location (15–40 m) and
Hunt. The heat lives in memory only, so a restart or `.nyar purge confirm` forgets it, and nothing ever spawns in
or is moved into claimed territory. Another player can join the fight; the squads hunt only their target.

`"bosses": [ { "prefab": "CHAR_Spider_Queen_VBlood", "count": 1–3, "modifiers": {...} } ]` (up to 3 entries) on any
spawn event adds bosses to its last wave. They must die for "all waves defeated", and players feed on their V Blood
as usual. A V Blood spawned by an event or `.nyar spawn` never starts a V Blood triggered event.

In chat: `.nyar event set <id> trigger.type Revenge`, `trigger.heat`, `trigger.warnAt 10,20`, `trigger.vbloodHeat`,
`trigger.windowSeconds`, `trigger.playerCooldownMinutes`, `trigger.factions` and `action.bosses
CHAR_Spider_Queen_VBlood:1` (or `none`); `event info` shows "revenge: <n> heat rows, <d> due, <r> running". Rolling back to 0.11.0
disables the events that use these keys.
</details>

<details>
<summary><b>The apocalypse: an outbreak that marches on a goal</b> · <i>0.13.0</i></summary>

An event with `"action": { "type": "Apocalypse", … }` (Spawns pillar) turns one faction into an outbreak that runs
from 10 minutes to 7 days (`durationSeconds` 600–604800):

| Key | Values | Effect |
|---|---|---|
| `faction`, `units` | a `Faction_*` name; 1–10 units, `count` is each one's weight | Who rises |
| `hotspots` | `count` 1–5, `minNatives` 3–50, `minSpacing` 100–3000 m | Found at the start where at least `minNatives` of the faction's own NPCs stand together; those NPCs are never touched |
| `roamers` | `perHotspot` 1–10, `perKillMin`/`perKillMax` 0–3 (or the older `perKill` 0–2), `pool`, `max` 1–500, `perMinute` 1–30, `huntRange` 20–60 | Roamers hunt the nearest player anywhere on the map, closing in with hidden jumps, never into a castle. Each kill a roamer or horde makes, player or NPC, adds `perKillMin`–`perKillMax` roamers (from `pool` when set) where the victim fell, at most `perMinute` a minute and `max` alive |
| `hordes` | `count` 1–10, `firstAfterMinutes` 0–1440, `everyMinutes` 5–1440, `size` 5–500, `growth` 0–100 | Horde k rises at the next hotspot with `size` + (k−1)·`growth` units and marches on the goal; the last brings the action's `bosses` |
| `goal`, `marchSpeed` | `{ "type": "Point", x, z }` or `{ "type": "Admin" }` (where the admin stood at the start; by command only), `radius` 10–100, `name`; 0.5–5.0 m/s | A horde stops and fights while a player stands within 60 m of its front, except close to the goal |

A horde at the goal loses the event ("<goal> has fallen"), destroying every horde wins it, and running out the clock
survives it; then the scoreboard. `event stop` shows only the scoreboard, a purge nothing. Chat announces each horde
rising (with its region), each quarter of its march and each horde destroyed, and `.nyar status` shows the horde, the
march and the roamers.

**Restarts.** The apocalypse is the only event that carries on after a restart. Its progress and scoreboard are saved
in `state.json` every minute (30 s while a horde marches); after the boot its units are rebuilt, and a horde due while
the server was down rises then.

**Big hordes.** Since 0.15.0 a horde is held only by the free `[Limits] MaxTrackedUnits` slots (default 500), never
by `MaxUnitsPerWave`. A cfg written by an earlier release keeps its old value (150): raise it for full-size hordes. A
cut start names the key in its reply and in `.nyar event info` ("capped: 150 of 200 by MaxTrackedUnits 150; raise it
in the cfg"). On the local test server 250 units held a full 30 frames a second with a player among them; 500 ran but dropped to
about 19 with a player in the middle of them (the game's own cost; the mod's own work stayed near 1 ms a tick).

`zombie-apocalypse`: undead of levels 22–38 at 5 hotspots, 4 hordes of 30 to 75 from minute 3, every 20 minutes, the
last with the Bishop of Death, marching at 2 m/s on where the admin stood, for 4 hours. `.nyar template use
zombie-apocalypse`, `.nyar event enable zombie-apocalypse`, then `.nyar event start zombie-apocalypse` where the living
make their stand. In chat: `.nyar event set <id> action.goal here`, `action.goal.radius`, `action.goal.name`,
`action.hordes.count|everyMinutes|size`, `action.roamers.max`, `action.marchSpeed`, `action.bosses` and
`durationSeconds`; every other key in `events.json`, then `.nyar event reload`.
</details>

<details>
<summary><b>Undead hordes: infestations, horde marches and waves that march</b> · <i>0.14.0</i></summary>

**Waves that march.** Any spawn event can send its waves somewhere: `"destination": { "location": { "type": "Point",
x, z } or { "type": "Admin" }, "radius": 10–100, "name": "the gate" }` and `"marchSpeed": 0.5–5.0` (2.0). Each wave
marches as one body from where it spawned and takes its behaviour on arrival. A player in its way holds it: the front
waits for its units while it is watched, and a wave a player has watched for 30 seconds is met and fights where it
stands. Its own location must be a Point or the admin's spot; `fanOut`, `regionLock`, `arena` and `pursuit` don't mix
with it.

**Infestation** (`"type": "Infestation"`): the dead rise at several graveyards (the places the faction's own NPCs
gather), and the infestation spreads from there.

| Key | Values | Effect |
|---|---|---|
| `faction`, `units`, `pools` | as for the apocalypse; pool entries as in wave variety | Who rises |
| `origins` | `count` 1–10, `minNatives` 1–20, `minSpacing` 100–2000 m, `perOrigin` 1–100 | Where, and how many at each |
| `hunters`, `driftRadius` | 0–100 %, 0–2000 m | That share of the living hunts the nearest player anywhere; the rest drift outward from their graveyard |
| `spread` | `perKillMin`/`perKillMax` 0–3, `pool`, `max` 1–500, `perMinute` 1–30 | Each kill they make, player or NPC, raises that many more where the victim fell |

**Horde march** (`"type": "HordeMarch"`): one great horde rises and marches on the living.

| Key | Values | Effect |
|---|---|---|
| `units`, `bosses` | 10–500 units in all; bosses fixed or rolled from a pool | The horde |
| `start` | a Point, `{ "type": "Admin" }`, or `{ "type": "Hotspot", "minDistance": 100–3000 }` | A Hotspot start is the graveyard nearest the destination at least that far from it |
| `destination`, `marchSpeed` | as for waves that march | Where it goes |
| `growth` | a spread block | Each kill on the way joins the horde; its `max` caps the horde's living count, so set it above the horde's size |
| `infest` | `hunters`, `driftRadius`, `spread` | On arrival, after 2 minutes stuck, or once players have watched it for 30 seconds, the horde becomes an infestation where it stands |

Only one apocalypse, infestation or horde march runs at a time ("an apocalypse-type event is running: <name>");
waves that march run beside them. Both new kinds carry on after a restart, like the apocalypse. Chat announces "The
dead rise in <n> places" and "The horde has reached <destination>!".

**How they move.** Hunters close in by hidden jumps in straight lines, never in view of a player and never into a
castle's territory, which stays a refuge. A player behind a river or a cliff band is reached only by the hunters with a
clear line. The last jump lands 50–60 m away, out of sight, and the game does not walk an idle unit the rest of the
way, so they gather there and the fight starts when the player comes closer. The game stops its own NPCs far from every
player, so kills, and the spread and growth they bring, happen only where players are.

Templates (off): `undead-infestation` and `undead-horde-march`, below. Since 0.15.0 they rise at full size on a new cfg
(`MaxTrackedUnits` 500); a cfg kept from 0.14.0 holds 150 until you raise it. In chat: `.nyar event set <id>
action.destination here` (or `none`), `action.destination.radius`, `action.destination.name`, `action.marchSpeed`,
`action.origins.count|minNatives|minSpacing|perOrigin`, `action.hunters`, `action.driftRadius`,
`action.spread.perKillMin|perKillMax|max|perMinute|pool`, `action.start here`, `action.start.minDistance`,
`action.growth.*` and `action.infest.hunters|driftRadius|spread.*`. Rolling back to 0.13.1 disables the events that use
these kinds or keys.
</details>

<details>
<summary><b>Horde pools: full-size hordes, the castle switch and shared unit pools</b> · <i>0.15.0</i></summary>

**Full-size hordes.** A horde march, an infestation and an apocalypse horde rise at the size their event asks for,
held only by `[Limits] MaxTrackedUnits` (default now 500). `MaxUnitsPerWave` (20) still holds ordinary waves. A cfg you
already have keeps its values: raise `MaxTrackedUnits` yourself.

**Castles are safe unless you allow it.** `"allowTerritory": false` (the default) on a horde event: no unit hunts a
player on claimed castle ground, nothing spawns, jumps, drifts or marches onto it, a march stops at the castle's edge,
and a start inside a castle is refused. `true` opens the castles. In chat: `.nyar event set <id> action.allowTerritory
true`; `.nyar event info` then says "claimed territory allowed".

**Shared unit pools.** `BepInEx/config/Nyarlathotep/pools.json` keeps named pools any event can use, like templates.
It starts with 17 faction pools (`undead-units`, `undead-bosses`, `bandits-units`, …); an event's own pool of the same
name wins. `.nyar pool list`, `.nyar pool info <pool>` and `.nyar pool reload` show and reload them. A pool entry in a
wave or a horde march may take `min` and `max` instead of `count` for a random size each time. How to set one up:
[Unit pools](#unit-pools).
</details>

<details>
<summary><b>Unit watch: stuck units recovered, then removed</b> · <i>0.13.0</i></summary>

On by default for every event unit (waves, swarms, revenge, hunting grounds, apocalypse roamers and hordes; never a
`.nyar spawn` unit), so a wave never hangs on one unit nobody can reach. A unit is stuck when, without fighting:

- it has stood within 5 m for `StuckSeconds` (90 s) while trying to reach a player;
- it is under the ground, more than 10 m below walkable ground;
- it has not moved within `SpawnStillSeconds` (10 s) of being spawned or put down, while targeting a player.

An idle unit beside a player is left alone. A stuck unit is moved, only while no player is within 100 m, to walkable
ground at least 50 m from every player and outside castles: beside its group, else near the nearest player it may
hunt, else back to its spot. After two failed tries it is removed: it counts as gone for its wave, so the next wave
comes, but gives no kill credit (no scoreboard kill, V Blood trigger, kill count, revenge heat or spread).

A wave the event waits on that sees no death and no fight for `StallMinutes` (10) sends its leftovers through the same
tries. `.nyar event unstick <id>` does it at once for every unit not fighting (its twin `.nyar api event unstick` serves Raphael),
and `.nyar event info <id>` shows "unit watch: <s> stuck now, <r> recovered, <x> removed". The keys are under
`[UnitWatch]` (Configuration below); `Enabled = false` turns it all off.
</details>

<details>
<summary><b>Wave variety: pursuit, unit pools and boss rolls</b> · <i>0.13.0</i></summary>

Three more optional keys on a spawn event:

| Key | Values | Effect |
|---|---|---|
| `pursuit` | `{ "afterSeconds": 15–600 }`, with Hunt, not with `fanOut` or `regionLock` | Once no player has stood in the event's arena (or Hunt range) for `afterSeconds`, its units go after the nearest player anywhere, with hidden jumps, until the event ends. A castle and PvP combat stay a refuge |
| `pools` | `{ "<name>": [ 1–30 unit names ], … }`, 1–8 pools | Lists to draw from: a unit entry `{ "pool": "troops", "count": 9 }` draws 9 at random, repeats allowed |
| `waveList[n].bosses` | 1–3 entries: `{ "prefab", "count" }` or `{ "pool": "lords", "min": 1, "max": 2 }`; at most 5 bosses | Any wave can bring bosses, fixed or rolled (no repeats); the action's `bosses` stays the last wave's |

Each wave is rolled when it comes, so no two runs look the same, and the caps cut regular units before bosses. Every
shipped template keeps to units that fight on the same side (nine allied groups, checked in game). The timed and
onslaught templates below use these keys. In chat: `action.pursuit 60`, `action.pools.troops <unit>,<unit>,…`
(`+<unit>` or `-<unit>` to add or remove one), `pool:troops:9` in a units list, and `action.waveList.3.bosses
pool:lords:2-2` for two bosses at the end every time.
</details>

<details>
<summary><b>Event templates and chat authoring</b> · <i>0.5.0; three automatic templates 0.8.0; a wave set 0.9.0; a swarm 0.10.0; hunting grounds 0.11.0; revenge 0.12.0; twenty-one more 0.13.0</i></summary>

Thirty-six ready-made events ship inside the mod. `.nyar template use <id>` copies one into your `events.json`,
disabled, ready to adjust and enable.

| Template | What it does | Starts |
|---|---|---|
| `legion-weekend-surge` | Legion ×1.5 physical power and max health, 30 min | Saturday 20:00 |
| `bandit-vengeance` | Bandits ×1.3 physical power and attack speed, 10 min | A bandit V Blood dies (30 min cooldown) |
| `undead-nightfall` | Undead ×1.25 physical and spell power, 20 min | Nightfall |
| `militia-crackdown` | Militia and Church ×1.3 max health, 15 min | A Militia or Church V Blood dies (30 min cooldown) |
| `bandit-ambush` | 3 waves of 4 thugs and 2 hunters | By command |
| `undead-rising` | 2 waves of 5 armoured skeletons and 2 crossbowmen, level +2, ×1.2 health | By command |
| `roaming-hunters` | 4 thugs and 2 hunters near up to 3 players 150 m apart, hunting (0.8.0) | Every 60–90 min |
| `border-watch` | 3 militia and 2 crossbowmen near the player, hunting (0.8.0) | A player enters Dunley Farmlands (30 min per player) |
| `bandit-reprisal` | 4 thugs and 2 hunters near the player, hunting (0.8.0) | A player kills 20 bandits within 5 min |
| `graveyard-waves` | 3 growing undead waves near a player, the last with a Guardian, hunting, with the scoreboard (0.9.0) | By command |
| `spider-swarm` | 5 spider waves growing from 6 to 18 units and level 30 to 42, held in a 40 m arena around the admin, ending on Ungoro, with the scoreboard and a K/D winner (0.10.0; Ungoro 0.12.0) | By command |
| `spider-hunting-grounds` | 3 waves of 6 level 30 spiders spread over Dunley Farmlands, kept inside and hunting the players there, ending on Ungoro, with the scoreboard (0.11.0; Ungoro 0.12.0) | By command |
| `bandit-revenge` | 3 tiers of bandits around a player who killed 30 bandits within 15 minutes, warned at 10 and 20, hunting only that player, with the scoreboard (0.12.0) | Revenge |
| `zombie-apocalypse` | The undead outbreak above: 5 hotspots, 4 hordes marching on the admin's spot, the Bishop of Death, 4 hours (0.13.0; retuned 0.14.0) | By command |
| `undead-infestation` | The dead rise at 5 graveyards, 10 each; half hunt the nearest players, the rest spread outward, and every kill adds 1–3 more, up to 300, for 4 hours (0.14.0) | By command |
| `undead-horde-march` | A horde of 200 undead and the Bishop of Death rises at a graveyard 300 m or more away and marches on the admin's spot, growing with every kill; there it breaks up and hunts the players, for 1 hour (0.14.0) | By command |
| `militia-muster` · `silverlight-crusade` · `legion-incursion` · `gloomrot-assault` · `cursed-night` | 4–5 growing waves of one faction in a 40 m arena around the admin, 150 s apart (legion 180 s) or on a clear, ending on a V Blood (Militia Leader, Paladin, Blood Knight, Voltage, Cursed Witch), with the scoreboard and a K/D winner (0.13.0) | By command |
| `realm-gauntlet` | 5 waves, one faction each (bandits, undead, militia, gloomrot, church), each once the last is cleared, ending on the Overseer (0.13.0) | By command |
| `spider-swarm-timed` · `graveyard-waves-timed` · `militia-muster-timed` · `silverlight-crusade-timed` · `legion-incursion-timed` · `gloomrot-assault-timed` · `cursed-night-timed` | The base template's waves (and its final V Blood, if any) on a fixed 75 s beat, cleared or not, in a 40 m arena around the admin, pursuing after 60 s (0.13.0) | By command |
| `spider-onslaught` · `undead-onslaught` · `militia-onslaught` · `silverlight-onslaught` · `legion-onslaught` · `gloomrot-onslaught` · `cursed-onslaught` | 3–5 waves rolled from the faction's `troops` and `elite` pools, about 1.5 times the base's, one V Blood rolled mid-run and one or two at the end, 75 s beat, 40 m arena, pursuing after 60 s (0.13.0) | By command |

You can build and change events without touching the file. Use `.nyar event new <id> <pillar>`,
`.nyar event copy <id> <newId>` and `.nyar event delete <id>` (then `confirm`). `.nyar event set` also changes
the trigger, factions, units and location. `location here` stores your position, height included.
`.nyar pillar <name> on|off` switches a pillar and saves the cfg. Every chat change is an ordinary edit of
`events.json` followed by a reload, and one `.bak` copy is kept. `.nyar event list` shows why each event would
or would not start (`ready`, `off (pillar)`, `full (cap)`, …).
</details>

<details>
<summary><b>Regions</b> · <i>0.6.0</i></summary>

Any event can be limited to named map regions: StartCave, FarbaneWoods, DunleyFarmlands, CursedForest,
HallowedMountains, SilverlightHills, Gloomrot_South, Gloomrot_North, RuinsOfMortium and Strongblade, read from the
game's own map. Set `trigger.scope` and `action.scope` in `events.json` (`"Global"`, the default, or a list of
names), or in game with `.nyar event set <id> action.scope FarbaneWoods,DunleyFarmlands`.

- **Trigger scope:** a V Blood trigger fires only for a kill inside the regions; a scheduled, nightfall/daybreak or
  manual start needs a player standing in one of them (a scheduled one is skipped, not retried).
- **Action scope:** empowerment buffs only NPCs standing in the regions; waves spawn only there, and an admin
  start from outside is refused.

`.nyar region here` names the region you stand in; `.nyar region list` shows how many events use each region.
Announcements can say `{region}`. If the map cannot be read, regional events are disabled and the rest run.
</details>

<details>
<summary><b>Announcements</b> · <i>0.2.0</i></summary>

Wave warnings before each wave (by default 5 min, 1 min and 10 s ahead), start and end banners with your own
text or a stock line, and a daily banner naming the events still due that day, each behind its own switch.
Admins can also broadcast on demand with `.nyar announce`.

From 0.13.1 a player who logs in while an event is running gets a private line 10 seconds later naming each running
event and its time left (`Events in progress: Bandit raid: 5 min left. Type .nyar status for details.`). It is the one
announcement on by default; set `[Announcements] LoginEventNotice = false` to turn it off.
</details>

<details>
<summary><b>Raphael companion panel</b> · <i>0.3.0 (server side)</i></summary>

The optional client mod [Raphael](https://thunderstore.io/c/v-rising/p/TheShadowRealm/Raphael/) can show a live
events board with wave countdowns, and give admins an events list and a kill-switch button. From 0.3.0 the server
answers Raphael's reads (active events, event definitions for admins) and pushes updates when an event starts or
ends, a wave comes or is warned, the kill switch fires or the definitions change. The pushes never tell a player
more than chat or `.nyar status` does, and no line carries a position. From 0.5.2 the admin buttons (start or
stop an event, use a template, switch a pillar, purge) have machine-readable versions that answer one line each.
From 0.8.0 (api 6) an event's trigger can also read `interval`, `regionentered` or `factionkills`; a Raphael built
for api 5 may show those events' trigger wrongly until it is updated. From 0.13.0 (api 8) the admin twins include `event
unstick`.
The panels arrive with a Raphael update.
</details>

<details>
<summary><b>Boss reinforcements</b> · <i>in development</i></summary>

Adds join a V Blood fight when it starts or when the boss drops below a health threshold, and leave when the
boss dies or resets.
</details>

<details>
<summary><b>Defended zones</b> · <i>in development</i></summary>

Admin-defined areas summon reinforcements when vampire activity inside them crosses a threshold, with a
cooldown between calls.
</details>

<details>
<summary><b>Sieges</b> · <i>in development</i></summary>

NPC war parties harass a castle's perimeter: defenders, servants and exposed pieces, under vanilla rules. Only
eligible castles are targeted (owner or clan recently online, optional minimum castle-heart level), and only
the owning clan is warned.
</details>

<details>
<summary><b>Leaderboards and stats</b> · <i>in development</i></summary>

Per-player counts: event units killed, events joined, waves survived, sieges defended, boss adds killed and
deaths, for today, this week and all time. Players can leave the boards; positions are never shown.
</details>

## Screenshots

<details>
<summary><b>Show screenshots</b></summary>

*Screenshots are added as features reach live servers. Slots so far:*

- **Wave warning, event banner and daily banner:** *coming soon*
- **Faction empowerment in progress:** *coming soon*
- **Leaderboard:** *coming with stats*
- **Raphael panel:** *coming with the Raphael integration*
</details>

## Installation

**Thunderstore Mod Manager / r2modman (recommended)**
1. Select **V Rising** and your dedicated-server profile.
2. Install **Nyarlathotep, Lord of Chaos**; its dependencies install with it.
3. Start the server once to create the config and the example events, then stop it and review the settings.

**Manual:** install BepInExPack V Rising and VampireCommandFramework (table below) on the dedicated server, then
copy `Nyarlathotep.dll` into `BepInEx/plugins`.

**⚠ Stop the server before replacing `Nyarlathotep.dll`.** A running server locks the file.

| Dependency | Version | Role |
|---|---|---|
| [BepInExPack V Rising](https://thunderstore.io/c/v-rising/p/BepInEx/BepInExPack_V_Rising/) | 1.733.2 | Loader (required) |
| [VampireCommandFramework](https://thunderstore.io/c/v-rising/p/deca/VampireCommandFramework/) | 0.10.4 | Chat commands (required) |
| [Raphael, Lord of Wisdom](https://thunderstore.io/c/v-rising/p/TheShadowRealm/Raphael/) | — | Companion client UI (optional) |

## Quick start

Install and start the server once. Then, in game as an admin, no file edits needed:

1. `.nyar template list`
2. `.nyar template use undead-nightfall`
3. `.nyar pillar empowerment on`
4. `.nyar event enable undead-nightfall`
5. `.nyar status`: at the next nightfall the undead are empowered for 20 minutes, and this shows it running.

If anything goes wrong, `.nyar purge` then `.nyar purge confirm` ends everything. Announcements, but for the login
notice, stay off until you turn on `[Announcements] WaveWarnings` and `EventBanners` in `BepInEx/config/kdpen.Nyarlathotep.cfg`.

Scheduled and triggered events spawn at a `Point` location (world `x` and `z` in `events.json`), `AroundPlayer` or
`Region` (spread over the event's regions, see Hunting grounds above);
an `Admin` location spawns around the admin and works for manual starts only; `.nyar event set <id> location here`
stores your position as a `Point`. Edit the file, then `.nyar event reload`. A
bad entry is disabled with a log line naming the event and the reason; a file that doesn't parse leaves the last good
set running.

## Commands

`.nyar` lists the commands you are allowed to run. Commands marked *(admin)* need server admin rights. Replies
are plain text and never rely on colour. Chat is typed and read with the keyboard;
V Rising offers no screen reader for it, a limitation of the game.

<details>
<summary><b>For everyone</b></summary>

| Command | What it does |
|---|---|
| `.nyar` | Overview and your available commands |
| `.nyar status` | Active events and minutes left (admins also see tracked units and any degraded hook) |
| `.nyar api version` | Machine-readable handshake for the Raphael client |
| `.nyar api status` / `api sub on\|off` | Active events and live updates as machine-readable lines, for Raphael |
| `.nyar api regions [page]` | The map's regions and their active events as machine-readable lines, for Raphael (0.6.0) |
</details>

<details>
<summary><b>Events</b> <i>(admin)</i></summary>

| Command | What it does |
|---|---|
| `.nyar event list [page]` / `info <id>` | Event definitions and their state |
| `.nyar event start <id>` / `stop <id>` | Start now / end early |
| `.nyar event enable <id>` / `disable <id>` | Switch an event on or off (saved to `events.json`) |
| `.nyar event set <id> <field> <value>` | Change `name`, `durationSeconds`, `conditions.minPlayers`, `conditions.cooldownMinutes`, `conditions.chancePercent`, `trigger.type`, `trigger.days`, `trigger.times`, `trigger.phase`, `trigger.bosses`, `action.factions`, `action.units` (`CHAR_<name>[:<count>]`), `action.waves`, `action.intervalSeconds`, `action.radius`, `trigger.scope`, `action.scope` (`Global` or `Name,Name`) or `location here`; on an empowerment, `action.stats.<stat>` (1.0-3.0) |
| `.nyar event set <id> <field> <value>` (0.7.0) | On a wave event: `action.modifiers.<level\|levelDelta\|maxHealth\|power\|moveSpeed\|attackSpeed>` (a value or `none`), `action.loot`, `action.allowTerritory` (`true\|false`), `action.behaviour` (`none` or `"hunt <range>"`), `action.units.<n>.chance`, `location "aroundplayer <min> <max>"` |
| `.nyar event set <id> <field> <value>` (0.8.0) | `trigger.type Interval\|RegionEntered\|FactionKills`, `trigger.minMinutes`, `trigger.maxMinutes`, `trigger.playerCooldownMinutes`, `trigger.factions` (`Name,Name`), `trigger.kills`, `trigger.windowSeconds`, `trigger.shared` (`true\|false`), `action.fanOut` (`none` or `"<maxInstances> <minSpacing>"`). `event info` shows an Interval event's next start |
| `.nyar event set <id> <field> <value>` (0.9.0) | `action.scoreboard` (`true\|false`), `action.waveList.<n>.units` (`CHAR_<name>[:<count>][,…]`; n up to one past the last wave appends one; on a units-form event `waveList.1.units` converts it), `action.waveList.<n>.afterSeconds` (10-3600 or `none`), `action.waveList.<n>.whenCleared` (`true\|false`), `action.waveList.<n>.units.<m>.<level\|levelDelta\|maxHealth\|power\|moveSpeed\|attackSpeed>` (a value or `none`), `action.waveList.<n> none` (removes the wave). `event info` shows one line per wave |
| `.nyar event set <id> <field> <value>` (0.12.0) | `trigger.type Revenge`, `trigger.heat`, `trigger.warnAt` (`n,n` or `none`), `trigger.vbloodHeat`, and on a Revenge event `trigger.factions`, `trigger.windowSeconds` and `trigger.playerCooldownMinutes`; `action.bosses` (`CHAR_<name>[:<count>][,…]` or `none`). `event info` shows the heat counts |
| `.nyar event set <id> <field> <value>` (0.13.0) | On an apocalypse: `action.goal here`, `action.goal.radius` (10-100), `action.goal.name`, `action.hordes.count\|everyMinutes\|size`, `action.roamers.max`, `action.marchSpeed` (0.5-5.0), `action.bosses`, `durationSeconds` (600-604800). On a wave event: `action.pursuit` (15-600 or `none`), `action.pools.<name>` (`<unit>,<unit>,…`, `+<unit>`, `-<unit>` or `none`), a `pool:<name>:<count>` token in `action.units` or `action.waveList.<n>.units`, `action.waveList.<n>.bosses` (`<unit>:<count>` or `pool:<name>:<min>-<max>`, comma separated, or `none`) |
| `.nyar event unstick <id>` | Move a running event's units that aren't fighting back unseen, removing any still stuck after two tries (0.13.0) |
| `.nyar event reload` | Re-read `events.json` |
| `.nyar region list` / `region here` | The map's regions with their event counts / the region you stand in (0.6.0) |
| `.nyar spawn <unit> [count] [level\|+n\|-n] [hp] [power]` | One-off test spawn beside you, removed after `ManualSpawnLifetimeSeconds` |
| `.nyar debug here [radius]` | The mod's units near you, with lifetime, level and stats, then up to 10 native NPCs with their empowerment buff |
| `.nyar announce <text>` | Broadcast a line to everyone (quote text longer than 16 words) |
| `.nyar api events [page]` | Event definitions as machine-readable lines, for Raphael |
| `.nyar api event <verb> …`, `api template use`, `api pillar <name> on\|off`, `api purge [confirm]` | The admin commands as machine-readable lines, for Raphael (0.5.2) |
| `.nyar api templates`, `api template info`, `api pillar list`, `api killswitch` | Templates, pillars and the kill switch as machine-readable lines, for Raphael (0.5.2) |
</details>

<details>
<summary><b>Templates and authoring</b> <i>(admin)</i></summary>

| Command | What it does |
|---|---|
| `.nyar template list [pillar] [page]` / `info <id>` | The built-in templates |
| `.nyar template use <id> [as <newId>]` | Copy a template into your events, disabled |
| `.nyar event new <id> <pillar>` | A disabled skeleton event of that pillar |
| `.nyar event copy <id> <newId>` | Copy an event, disabled |
| `.nyar event delete <id>`, then `.nyar event delete <id> confirm` | Delete an event (the confirm is valid for 30 s) |
| `.nyar pillar list` / `.nyar pillar <name> on\|off` | Show or switch the pillars (saved to the cfg; switching one off ends its running events) |
</details>

<details>
<summary><b>Unit pools</b> <i>(admin, 0.15.0)</i></summary>

| Command | What it does |
|---|---|
| `.nyar pool list [page]` | The library's pools, 10 a page: name, size, and `seeded` (as the mod wrote it) or `library` (edited or added) |
| `.nyar pool info <pool> [page]` | A pool's members, 10 a page, the events that name it, and a "mixes groups" warning when its units would fight each other |
| `.nyar pool reload` | Re-read `pools.json`, then reload `events.json` against it; the reply names every event invalid now |
| `.nyar event set <id> action.allowTerritory true\|false` | The castle switch, now also on Apocalypse, Infestation and HordeMarch events |
| `.nyar event set <id> action.units pool:<name>:<min>-<max>` | A random-size pool entry (SpawnWaves, `waveList.<n>.units`, HordeMarch); `pool:<name>:<count>` as before |
</details>

<details>
<summary><b>Kill switch</b> <i>(admin)</i></summary>

`.nyar purge`, then `.nyar purge confirm`, ends every event, removes every unit the mod spawned and every
empowerment buff, and holds off new events for `PurgeCooldownSeconds` (60 s). To take the mod out entirely, stop the server and delete
`Nyarlathotep.dll`; the mod's units expire on their own.
</details>

## Configuration

The cfg is `BepInEx/config/kdpen.Nyarlathotep.cfg`, written at the first start. Event definitions live in
`BepInEx/config/Nyarlathotep/events.json` and the pool library in `BepInEx/config/Nyarlathotep/pools.json` (see
[Setting up events](#setting-up-events) and [Unit pools](#unit-pools)). A number outside its range is clamped at load,
with a `[nyar]` warning in the log naming the key.

**When a change takes effect.** Edit the cfg with the server stopped, or restart after saving it. Every number
(`[Limits]`, `[UnitWatch]` `StuckSeconds` · `StallMinutes` · `SpawnStillSeconds`, `[Announcements]` `WarningOffsets` ·
`DailyBannerTime` · `ShareCooldownSeconds` · `ShareMaxPerMinute`) is read once, at start. The on/off switches are read
from the file at start too; a hand edit saved while the server runs is picked up at the next restart, or the next
time an admin runs `.nyar pillar … on|off` (which re-reads the whole file before it saves). `.nyar pillar <name> on|off`
is the one live change: it saves the cfg itself, and `off` ends that pillar's running events.

<details>
<summary><b>[General] and [Pillars]</b></summary>

| Key | Default | Effect |
|---|---|---|
| `General.Enabled` | `true` | Master switch. `false`: no event starts (admin starts included) and nothing spawns |
| `Pillars.FactionEmpowerment` | `false` | Runs events of pillar `empowerment` (chat name `empowerment`) |
| `Pillars.EventSpawns` | `false` | Runs events of pillar `spawns`: waves, revenge, hunting grounds, Apocalypse, Infestation, HordeMarch (chat name `spawns`) |
| `Pillars.BossReinforcements` | `false` | Runs events of pillar `boss` (chat name `boss`) · *in development* |
| `Pillars.DefendedZones` | `false` | Runs events of pillar `zones` (chat name `zones`) · *in development* |
| `Pillars.SiegeWaves` | `false` | Runs events of pillar `sieges` (chat name `sieges`) · *in development* |

An event runs only while `General.Enabled` and its own pillar's switch are both on. The three pillars in development
already accept a `SpawnWaves` event (the seeded `example-boss`, `example-zones` and `example-sieges` are such), run
under their own switch; their own features arrive later. `General.AnnounceEvents` (0.1.0) is retired and ignored.
</details>

<details>
<summary><b>[Limits]: the safety caps</b></summary>

| Key | Default | Range | Effect |
|---|---|---|---|
| `MaxTrackedUnits` | `500` | 1-500 | Most mod-spawned units alive at once, all events and `.nyar spawn` together. A spawn past it is skipped. The only cap on a horde (HordeMarch, Infestation, Apocalypse) |
| `MaxUnitsPerWave` | `20` | 1-500 | Most units in one `SpawnWaves` wave (all its `fanOut` groups and bosses together), and the most one `.nyar spawn` makes. Never holds a horde |
| `MaxConcurrentEvents` | `3` | 1-10 | Most events running at once, any pillar; an admin's `.nyar event start` obeys it too |
| `MaxSpawnsPerTick` | `10` | 1-40 | Most units spawned per scheduler tick (the mod ticks once a second); a bigger wave or horde rises over the next ticks |
| `MaxDespawnsPerTick` | `5` | 1-40 | Most units removed per tick; removals are always staged, never all in one frame |
| `EmpowerBatchPerTick` | `200` | 50-1000 | Most empowerment buffs applied or removed per tick (removals first) |
| `GraceSeconds` | `30` | 0-600 | How long an event's units outlive its end before they are removed |
| `PurgeCooldownSeconds` | `60` | 0-3600 | After `.nyar purge confirm`, no event starts and nothing spawns for this long |
| `ManualSpawnLifetimeSeconds` | `300` | 30-3600 | Lifetime of a unit made with `.nyar spawn` |

**How the caps work together.**

- *A wave* (`SpawnWaves`) is cut first to `MaxUnitsPerWave`, then to the free `MaxTrackedUnits` slots (500 minus every
  mod unit alive). Bosses are kept before regular units, so a cut drops troops first.
- *A horde* (an Apocalypse horde, a HordeMarch's horde, an Infestation's rise and spread) skips `MaxUnitsPerWave`
  and is held only by the free `MaxTrackedUnits` slots and its own `max` keys (`roamers.max`, `spread.max`,
  `growth.max`). At the default 500 one full 500-unit horde takes every slot while it lives, so no other event can spawn.
- `MaxConcurrentEvents` counts events, not units; only one Apocalypse, Infestation or HordeMarch runs at a time
  whatever this cap says ("an apocalypse-type event is running: <name>"), and one empowerment per faction.
- `MaxSpawnsPerTick` and `MaxDespawnsPerTick` set the pace, not the size: a 500-unit horde at 10 a tick takes about
  50 s to rise. Raise them to rise faster at a cost in tick time.
- When an admin start is cut, the reply and `.nyar event info <id>` name the cap: `capped: 200 of 500 by
  MaxTrackedUnits 200; raise it in the cfg`. The boot log prints the loaded caps (`[nyar] limits: MaxTrackedUnits …`).

**Upgrading from 0.14.x:** an existing cfg keeps the value written in it, so a server that had the old default still
reads `MaxTrackedUnits = 150` until you raise it.

**Scale.** On the local test server 250 units held a full 30 frames a second with a player among them; 500 ran but
dropped to about 19 with a player in the middle (the game's own cost; the mod's work stayed near 1 ms a tick). A tick
of 250 ms or more logs a warning naming the slowest part, at most once a minute.
</details>

<details>
<summary><b>[Announcements]</b></summary>

| Key | Default | Range | Effect |
|---|---|---|---|
| `WaveWarnings` | `false` | | Server-wide warning before each wave of an event whose `announce.warnings` is `true` |
| `WarningOffsets` | `300,60,10` | 1-5 values, each 5-3600 | Seconds before a wave at which its warnings fire. Bad values are dropped with a log line; none left falls back to `300,60,10` |
| `EventBanners` | `false` | | Server-wide line when an event starts and ends: one of its `announce.start`/`end` lines, else a stock line |
| `DailyBanner` | `false` | | One server-wide line a day naming the scheduled events still to come that day |
| `DailyBannerTime` | `20:00` | `HH:mm` | Server-local time of the daily banner; not `HH:mm` falls back to `20:00` |
| `LoginEventNotice` | `true` | | Private line 10 s after login, while events run, naming each and its time left. The one announcement on by default |
| `LoginStats` | `false` | | Reserved for player stats (later release); no effect yet |
| `PlayerShare` | `false` | | Reserved for player stats; no effect yet |
| `ShareCooldownSeconds` | `300` | 10-86400 | Reserved: seconds between two shares by one player |
| `ShareMaxPerMinute` | `3` | 1-20 | Reserved: most shares the server passes in a minute |
</details>

<details>
<summary><b>[Scoreboard], [UnitWatch] and [Debug]</b></summary>

| Key | Default | Range | Effect |
|---|---|---|---|
| `Scoreboard.IncludeAdmins` | `false` | | Count admins' kills and deaths on an event's scoreboard |
| `UnitWatch.Enabled` | `true` | | Stuck event units are moved back unseen, then removed after 2 failed tries (no kill credit). `false` turns it off and refuses `.nyar event unstick` |
| `UnitWatch.StuckSeconds` | `90` | 30-600 | Seconds a unit trying to reach a player may stand still (or stay under the ground) before it is stuck |
| `UnitWatch.StallMinutes` | `10` | 0-60 | Minutes with no death and no fight before the leftovers of a wave the event waits on are treated as stuck; `0` = off |
| `UnitWatch.SpawnStillSeconds` | `10` | 5-60 | Seconds after a spawn or landing within which a unit with a player near must have moved, or it is stuck at once |
| `Debug.VerboseLogging` | `false` | | Log every unit spawned and removed and every announcement sent |
| `Debug.TimingLog` | `false` | | Log the scheduler tick's average and maximum duration once a minute |

A debug build also has `Debug.FaultInjection` (an event id or `hook:<name>`, for testing); the released DLL has no
such key and ignores it.
</details>

**Upgrading from 0.1.0:** `General.AnnounceEvents` is retired and ignored; use the `[Announcements]` switches.

**Upgrading from 0.2 or 0.3:** an `example-empowerment` written by an earlier release now loads disabled ("pillar
empowerment takes an Empower action"). Change its `pillar` to `spawns`, or replace it with the template under
Faction empowerment above.

## Setting up events

An event is one entry of `events.json`: a **trigger** starts an **action** for `durationSeconds`, under a **pillar**
switch; optional **conditions** can hold an automatic start back, and **announce** lines can tell players. Everything
an event spawns is removed after it ends (plus `GraceSeconds`).

<details>
<summary><b>Start from a template (no file editing)</b></summary>

1. `.nyar template list [pillar] [page]`: the 36 built-in templates, ten a page (`(in events.json)` marks one you
   already copied). The table under *Event templates* above says what each does.
2. `.nyar template info <template>`: its trigger, conditions, waves, units and announcements, as `event info` shows them.
3. `.nyar template use <template>`: copies it into `events.json` under the same id, **disabled**. To keep several
   copies, or to rename: `.nyar template use <template> as <new-id>` (id: 1-32 of `a-z 0-9 -`). A copy whose id
   already exists is refused.
4. Adjust it with `.nyar event set <id> <field> <value>` (fields: see [Commands](#commands)); `location here` and
   `action.goal here` / `action.destination here` / `action.start here` store where you stand. Anything chat cannot
   set, edit in `events.json` and run `.nyar event reload`.
5. `.nyar pillar <pillar> on` (for example `spawns`; it saves the cfg).
6. `.nyar event enable <id>`.
7. `.nyar event list`: the event should read `ready`. Other labels say why not: `off (pillar)`, `off (event)`,
   `off (mod)`, `full (cap)`, `invalid: <reason>`.
8. A `Manual` event waits for `.nyar event start <id>` (stand where it should happen if it uses an `Admin` location,
   goal, destination or start); any other trigger starts it on its own. `.nyar status` shows it running;
   `.nyar event info <id>` shows its waves, its next start (Interval) and any cap cut.

More: `.nyar event copy <id> <new-id>` copies any event (disabled); `.nyar event new <id> <pillar>` writes a disabled
skeleton (`.nyar event new <id> revenge` a revenge skeleton); `.nyar event delete <id>`, then `.nyar event delete <id>
confirm` within 30 s. Every chat change is an ordinary edit of `events.json` (one `events.json.bak` kept) followed by a
reload. A chat edit is refused while the file on disk differs from the one last loaded: after a hand edit, run
`.nyar event reload` first.
</details>

<details>
<summary><b>Write your own event in events.json</b></summary>

The file is `BepInEx/config/Nyarlathotep/events.json`, seeded at the first start with five disabled examples. Shape:

```json
{
  "SchemaVersion": 1,
  "events": [ { …event… }, { …event… } ]
}
```

- Top level: exactly `SchemaVersion` (an integer, `1`) and `events` (an array of at most 200 events). Any other
  top-level key, a file over 1 MB, comments or trailing commas reject the **whole file**: the last good set keeps running
  and the log (and the `.nyar event reload` reply) names the line and position.
- An entry that breaks a rule is **kept but loaded disabled** with one reason naming its field, e.g.
  `event night-hunt: action.radius must be 2-30`. The log line, `.nyar event list` (`invalid: <reason>`) and
  `.nyar event info <id>` show it. An entry whose id is not valid is reported as `#<position>`. Two entries with one
  id: the second is disabled (`duplicate id`).
- Unknown keys are refused (`unknown field action.foo`), so a typo never silently does nothing.
- Edit the file while the server runs, then `.nyar event reload`. A running event keeps the definition it started with
  until it ends.

A minimal annotated example (the comments are for reading only; JSON allows none):

```jsonc
{ "SchemaVersion": 1, "events": [
  { "id": "bandit-raid",              // 1-32 of a-z 0-9 -
    "name": "Bandit raid",            // 1-40 characters, shown to players
    "enabled": false,                 // turn on with .nyar event enable bandit-raid
    "pillar": "spawns",               // which [Pillars] switch it runs under
    "trigger": { "type": "Schedule", "days": ["Sat"], "times": ["20:00"] },
    "conditions": { "minPlayers": 2 },// optional; automatic starts only
    "durationSeconds": 900,           // 30-7200 (600-604800 for the horde types)
    "action": { "type": "SpawnWaves",
      "units": [ { "prefab": "CHAR_Bandit_Thug", "count": 5 }, { "pool": "bandits-units", "min": 1, "max": 3 } ],
      "waves": 3, "intervalSeconds": 120, "radius": 10,
      "location": { "type": "Point", "x": -1200, "z": -800 },
      "behaviour": { "type": "Hunt", "range": 40 } },
    "announce": { "start": ["The {faction} gather: {event}."], "warnings": true } } ] }
```

Unit names are the game's `CHAR_*` prefab names, faction names its `Faction_*` names (`.nyar debug here` shows the
units around you). `bandits-units` is a pool of the [pool library](#unit-pools).
</details>

<details>
<summary><b>Event fields, triggers and conditions</b></summary>

**The event object** (only these keys):

| Key | Required | Values | Effect |
|---|---|---|---|
| `id` | yes | 1-32 of `a-z 0-9 -`, unique | The name every command uses |
| `name` | yes | 1-40 characters, no `<` `>` | Shown in banners, status and `{event}` |
| `enabled` | yes | `true` / `false` | Off events never start |
| `pillar` | yes | `empowerment`, `spawns`, `boss`, `zones`, `sieges` | The switch it runs under. `empowerment` takes only `Empower`; `Empower` needs `empowerment`; `Apocalypse`, `Infestation` and `HordeMarch` need `spawns`; `SpawnWaves` takes any pillar but `empowerment` |
| `trigger` | yes | an object, below | What starts it |
| `conditions` | no | an object, below | What can hold an automatic start back |
| `durationSeconds` | yes | 30-7200; 600-604800 (7 days) for `Apocalypse`, `Infestation`, `HordeMarch` | How long it runs |
| `action` | yes | one of the action types below | What it does |
| `announce` | no | an object, below | Its own banner lines |

**Triggers.** Every type also takes an optional `scope` (below).

| `type` | Keys | Starts the event |
|---|---|---|
| `Manual` | | Only by `.nyar event start <id>` (the only trigger an `Admin` location, goal, destination or start accepts) |
| `Schedule` | `days`: 1-7 of `Mon Tue Wed Thu Fri Sat Sun`; `times`: 1-12 distinct `HH:mm` | At each time on each day, server-local |
| `GameTime` | `phase`: `day` or `night` | When day breaks or night falls in game |
| `VBloodKilled` | `bosses`: `["any"]` or 1-20 `CHAR_*` names | When a player kills one of those V Bloods (one spawned by an event or `.nyar spawn` never counts) |
| `Interval` | `minMinutes`, `maxMinutes`: 5-1440, min ≤ max | A random `minMinutes`-`maxMinutes` after its last run ended; survives a restart, downtime is not replayed |
| `RegionEntered` | `scope` (regions, **required**); `playerCooldownMinutes` 0-1440 (default 30) | When a player walks into one of the regions; the same player again only after the cooldown |
| `FactionKills` | `factions`: 1-5 `Faction_*`; `kills` 3-500; `windowSeconds` 10-3600; `shared` `true`/`false` (default false) | When one player (or, with `shared`, all players together) kills `kills` NPCs of those factions within the window |
| `Revenge` | `factions` 1-5; `heat` 3-500; `windowSeconds` 60-3600; `warnAt` up to 3 ascending values below `heat`; `vbloodHeat` 0-100 (default 10); `playerCooldownMinutes` 0-1440 (default 30) | Each kill of those factions within the window adds 1 heat to the killer (a V Blood `vbloodHeat`); a private warning at each `warnAt`; at `heat` waves come for that player alone. Needs a `SpawnWaves` action with an `AroundPlayer` location and `Hunt`, no `fanOut`, no `allowTerritory` |

**Scope** (`trigger.scope` and `action.scope`): `"Global"` (default) or a list of 1-10 region names: StartCave,
FarbaneWoods, DunleyFarmlands, CursedForest, HallowedMountains, SilverlightHills, Gloomrot_South, Gloomrot_North,
RuinsOfMortium, Strongblade (any case). A trigger scope lets a V Blood trigger fire only for a kill inside the regions
and needs a player inside them for any other start. An action scope limits where it acts: empowerment buffs only NPCs
there, waves spawn only there, hotspots and origins are found only there, and an admin start from outside is refused.
If the map cannot be read, regional events load disabled (`regions unavailable`).

**Conditions** (all optional; they gate automatic starts only, never an admin's `.nyar event start`):

| Key | Default | Values | Holds the start back when |
|---|---|---|---|
| `minPlayers` | `0` | 0-100 | Fewer players are online |
| `cooldownMinutes` | `0` | 0-10080 | The event started less than this long ago |
| `chancePercent` | `100` | 1-100 | A 1-100 roll comes out above it |
| `window` | none | `{ "from": "HH:mm", "to": "HH:mm" }` | Server-local time is outside it (`from` included, `to` not; `to` earlier than `from` crosses midnight; equal = all day) |
| `mode` | `any` | `any`, `pve`, `pvp` | The server's game mode differs |

**Announce** (all optional; shown only while `[Announcements] EventBanners` / `WaveWarnings` are on):

| Key | Values | Effect |
|---|---|---|
| `start`, `end` | 0-5 lines of 1-200 characters, no `<` `>` | One line picked at random for the start / end banner; none = a stock line |
| `warnings` | `true` / `false` (default) | Stock warnings before each wave at `WarningOffsets` |

Placeholders: `{event}` the name, `{faction}` the factions (an empowerment's, an apocalypse-type's, else the first
unit's), `{wave}`, `{waves}` (an apocalypse's hordes), `{minutes}`, `{region}` the action scope's regions or "the
world", `{zone}` (reserved, shows `-`). Any other `{…}` is refused.
</details>

<details>
<summary><b>Locations: where things spawn and march</b></summary>

`action.location` of a `SpawnWaves` event:

| `type` | Keys | Each wave centres on |
|---|---|---|
| `Point` | `x`, `z` (world, -10000..10000), optional `y` (height) | That point. Inside a regional `action.scope` it must lie in the regions. `.nyar event set <id> location here` writes your x, y, z |
| `Admin` | | Where the admin who ran `.nyar event start` stands. `Manual` trigger only |
| `AroundPlayer` | `minDist` 10-60, `maxDist` 15-80 (min < max) | A spot that far from a random online player (one in castle territory or PvP combat is never picked); with `Hunt`, `maxDist` ≤ `range` |
| `Region` | | Groups of up to 5 at spots spread over the action's scope regions, 30 m+ apart. Needs a regional `action.scope` |

Units land on walkable ground: a spot in water, on a cliff or in a wall moves to the nearest walkable one. A wave
centred in claimed castle territory is skipped unless `allowTerritory` is `true`.

Targets of the marching keys (`destination.location`, Apocalypse `goal.location`, HordeMarch `start`):

| Form | Keys | Meaning |
|---|---|---|
| `{ "type": "Point", … }` | `x`, `z` (-10000..10000), optional `y` | A fixed place |
| `{ "type": "Admin" }` | | Where the starting admin stands; `Manual` trigger only. A HordeMarch's `start` and `destination` are never both `Admin` |
| `{ "type": "Hotspot", "minDistance": 100-3000 }` | HordeMarch `start` only | The faction's native gathering (5+ natives) nearest the destination but at least `minDistance` m from it |
</details>

<details>
<summary><b>Action: Empower (faction empowerment)</b></summary>

Pillar `empowerment`. Every NPC of the factions gets a timed buff that expires on its own; NPCs that load in during
the event are buffed within about 15 s. Players' units, servants, traders, familiars and the mod's own units are never
buffed. Only one empowerment per faction runs at a time.

| Key | Required | Values | Effect |
|---|---|---|---|
| `type` | yes | `"Empower"` | |
| `factions` | yes | 1-5 distinct `Faction_*` names (not players, traders, critters, prisoners, ignored) | Whose NPCs are buffed |
| `stats` | yes | object, below | The multipliers |
| `includeUnits` | no | 0-20 distinct `CHAR_*` names | Also buff these units, whatever their faction |
| `excludeUnits` | no | 0-20 distinct `CHAR_*` names | Never buff these |
| `includeVBloods` | no | `true` / `false` (default) | Buff V Bloods too |
| `scope` | no | `"Global"` or regions | Only NPCs standing in the regions |

`stats` (each 1.0-3.0 times the base value; a key left out stays 1.0; at least one above 1.0):

| Stat | Multiplies |
|---|---|
| `physicalPower` | Physical power |
| `spellPower` | Spell power |
| `maxHealth` | Maximum health |
| `attackSpeed` | Primary and ability attack speed |
| `moveSpeed` | Movement speed |
</details>

<details>
<summary><b>Action: SpawnWaves (waves, swarms, hunting grounds, revenge, marching waves)</b></summary>

Any pillar but `empowerment` (normally `spawns`). Two forms: the **units form** (one list repeated `waves` times) or
the **waveList form** (each wave its own list). Required in both: `type`, `radius`, `location`.

| Key | Values (default) | Effect |
|---|---|---|
| `units` | 1-10 entries ([Unit entries](#unit-entries-and-modifiers)) | Units form: every wave's units |
| `waves` | 1-10 | Units form: how many waves |
| `intervalSeconds` | 10-600 | Units form: seconds between waves |
| `modifiers` | modifiers object | Units form: level and stats of every unit (and of bosses without their own) |
| `waveList` | 1-10 wave objects, below | Replaces `units`, `waves`, `intervalSeconds` and `modifiers` (refused beside them) |
| `radius` | 2-30 | Metres around the wave's centre the units spread over |
| `location` | see [Locations](#locations-where-things-spawn-and-march) | Where each wave centres |
| `unitLifetimeSeconds` | 30-7200 (none) | A unit leaves after this long, or at the event's end + `GraceSeconds` if sooner |
| `scope` | `"Global"` / regions | Where waves may spawn and players are picked |
| `loot` | `true` / `false` (false) | The units drop loot |
| `allowTerritory` | `true` / `false` (false) | Waves may spawn in claimed castle territory (refused with a Revenge trigger) |
| `behaviour` | `{ "type": "Hunt", "range": 10-60 }` or `{ "type": "RegionHunt" }` (none) | Hunt: every 5 s each unit is set on the nearest players (up to 5) within `range` m of the wave. RegionHunt (needs `regionLock: true`): each group goes after the nearest player inside the regions, closing distance with hidden jumps |
| `fanOut` | `{ "maxInstances": 2-10, "minSpacing": 50-500 }` | `AroundPlayer` only: one full group near each of up to `maxInstances` players at least `minSpacing` m apart; all groups share `MaxUnitsPerWave`. No `bosses` with it |
| `scoreboard` | `true` / `false` (false) | Counts kills of its units and deaths to them; top 3 in chat at the end |
| `winner` | `"KillDeath"` | Needs `scoreboard: true`: names the best kills ÷ deaths after the scoreboard |
| `arena` | `{ "radius": 15-60 }` | `Point` or `Admin` location; `radius` and Hunt `range` within it. Every 5 s a unit 10 m+ outside is brought back near the centre |
| `regionLock` | `true` / `false` (false) | Needs a regional scope, no `arena`: a unit that leaves the regions is brought back |
| `growth` | `{ "addUnits": 0-10, "addLevels": 0-5, "whenCleared": bool }` | Units form, `waves` 2-10, adds something: wave n gets each entry's count + (n−1)·addUnits and level + (n−1)·addLevels (max 120); last wave ≤ 500 units. `whenCleared`: a wave also starts as soon as the last is cleared |
| `bosses` | 1-3 entries, below | Bosses that join the **last** wave; they must die for "all waves defeated" |
| `pools` | `{ "<name>": [1-30 CHAR_ names], … }`, 1-8 pools | The event's own pools ([Unit pools](#unit-pools)); each must be used |
| `pursuit` | `{ "afterSeconds": 15-600 }` | Needs Hunt, no `fanOut`/`regionLock`: once no player has stood in the arena (or Hunt range) that long, the units go after the nearest player anywhere until the event ends |
| `destination` | `{ "location": Point/Admin, "radius": 10-100, "name": 1-40 characters }` | Each wave marches as one body to it and takes its behaviour within `radius`. Needs a `Point` or `Admin` location; no `fanOut`, `regionLock`, `arena`, `pursuit` |
| `marchSpeed` | 0.5-5.0 m/s (2.0) | Needs `destination` |

**A waveList wave** (`units` required):

| Key | Values | Effect |
|---|---|---|
| `units` | 1-10 entries, each with its own optional `modifiers` | The wave's units |
| `afterSeconds` | 10-3600 (not on wave 1) | Starts this long after the wave before it started |
| `whenCleared` | `true` / `false` (not on wave 1) | Starts when every unit of the wave before it is gone; with `afterSeconds`, whichever comes first. `false` needs `afterSeconds`. Neither key = when cleared |
| `bosses` | 1-3 entries | This wave's own bosses. Not beside the action's `bosses` on the last wave, never with `fanOut` |

When the last wave is cleared and every wave was fought, the event ends at once ("all waves defeated"). Each wave is
rolled when it comes, so pool draws differ every run.

**Bosses.** An entry is fixed, `{ "prefab": "CHAR_…", "count": 1-3, "modifiers": {…} }`, or a roll from a pool,
`{ "pool": "<name>", "min": 1-5, "max": min-5, "modifiers": {…} }` (min..max distinct members, never one the list
already holds; `max` may not exceed what the pool has left). A list holding a roll, and any wave's own list, holds at
most 5 bosses in all (counts and maxima). A boss without `modifiers` takes the wave's: the action's `modifiers` in the
units form, the wave's first entry's in a waveList.
</details>

<details>
<summary><b>Action: Apocalypse</b></summary>

Pillar `spawns`, `durationSeconds` 600-604800. One faction rises at hotspots, roamers hunt players map-wide, and hordes
march on a goal. Carries on after a restart. All keys but `bosses`, `scope`, `scoreboard`, `pools` and
`allowTerritory` are required.

| Key | Values (default) | Effect |
|---|---|---|
| `type` | `"Apocalypse"` | |
| `faction` | a `Faction_*` name | Its natives mark the hotspots (and are never touched) |
| `units` | 1-10 entries; `count` 1-500 is a **weight** | Each roamer and horde unit is drawn by weight `count × chance`; a pool entry then draws one member. No `min`/`max` |
| `pools` | 1-8 own pools | As on SpawnWaves |
| `hotspots` | `count` 1-5, `minNatives` 3-50, `minSpacing` 100-3000 m | At the start, up to `count` 100 m cells holding `minNatives`+ of the faction's NPCs, `minSpacing` apart |
| `roamers` | `perHotspot` 1-10; `perKillMin` 0-3 and `perKillMax` perKillMin-3 (or the older `perKill` 0-2, not both); `pool`; `max` 1-500; `perMinute` 1-30; `huntRange` 20-60 | Each hotspot is topped up to `perHotspot` at the start and at each horde. Each kill a roamer or horde unit makes adds `perKillMin`-`perKillMax` roamers where the victim fell (from `pool` if set), at most `perMinute` a minute and `max` alive. `huntRange`: their Hunt range once on a player |
| `hordes` | `count` 1-10, `firstAfterMinutes` 0-1440, `everyMinutes` 5-1440, `size` 5-500, `growth` 0-100 | Horde k rises at the next hotspot after `firstAfterMinutes` + (k−1)·`everyMinutes` with `size` + (k−1)·`growth` units. The last must rise before the duration ends |
| `goal` | `{ "location": Point/Admin, "radius": 10-100, "name": 1-32 characters }` | Where hordes march. A horde within `radius` loses the event ("<name> has fallen") |
| `marchSpeed` | 0.5-5.0 m/s | Required here |
| `bosses` | 1-3 fixed entries `{ prefab, count 1-3, modifiers }` (no pool rolls) | Join the last horde |
| `scope` | `"Global"` / regions | Where hotspots are found |
| `scoreboard` | `true` (default) / `false` | Scoreboard at the end |
| `allowTerritory` | `true` / `false` (false) | The castle switch, below |

Destroying every horde wins, running out the clock survives. **The castle switch** (`allowTerritory`, all three horde
types): off, no unit targets a player on claimed castle ground, nothing spawns, jumps or drifts onto it, a march stops at
the castle's edge, a hotspot or origin on it is skipped, and a Point/Admin start on it is refused. On, castles are part
of the map like any other ground. With it off the game's own AI may still chase a unit into a castle once a fight starts.
</details>

<details>
<summary><b>Action: Infestation</b></summary>

Pillar `spawns`, `durationSeconds` 600-604800. The dead rise at several origins (where the faction's own NPCs gather) and
spread from there. Carries on after a restart. Required: `type`, `faction`, `units`, `origins`, `hunters`,
`driftRadius`, `spread`.

| Key | Values (default) | Effect |
|---|---|---|
| `type` | `"Infestation"` | |
| `faction` | a `Faction_*` name | Its natives mark the origins |
| `units` | 1-10 entries; `count` is a **weight** (as Apocalypse) | What rises; no `min`/`max` |
| `pools` | 1-8 own pools | |
| `origins` | `count` 1-10, `minNatives` 1-20, `minSpacing` 100-2000 m, `perOrigin` 1-100 | Up to `count` origins with `minNatives`+ natives, `minSpacing` apart; `perOrigin` units rise at each |
| `hunters` | 0-100 (%) | That share hunts the nearest player anywhere |
| `driftRadius` | 0-2000 m | The rest drift outward up to this far from their origin |
| `spread` | spread block, below | Each kill they make, player or NPC, raises more |
| `scope`, `scoreboard` (true), `allowTerritory` (false) | | As Apocalypse |

**A spread block** (`spread`, and HordeMarch `growth` and `infest.spread`): `perKillMin` 0-3 and `perKillMax`
perKillMin-3 (units per kill, uniform), `max` 1-500 (most alive from this block), `perMinute` 1-30, optional `pool`
(draw from it, each unit with the modifiers of the first `units` entry naming that pool; else drawn from `units`). All
but `pool` required.
</details>

<details>
<summary><b>Action: HordeMarch</b></summary>

Pillar `spawns`, `durationSeconds` 600-604800. One great horde rises, marches on its destination, grows with every kill
on the way, then becomes an infestation where it stands: on arrival, after 2 minutes stuck, or once players have
watched it for 30 s. Carries on after a restart. Required: `type`, `faction`, `units`, `start`, `destination`,
`growth`, `infest`.

| Key | Values (default) | Effect |
|---|---|---|
| `type` | `"HordeMarch"` | |
| `faction` | a `Faction_*` name | Its natives mark a `Hotspot` start |
| `units` | 1-10 entries whose counts **total 10-500** (a `min`/`max` entry counts its max; the mins must total 10+) | The horde, every copy rolled for `chance` |
| `pools` | 1-8 own pools | |
| `bosses` | 1-3 entries, fixed or pool rolls (as SpawnWaves) | Rise with the horde |
| `start` | Point, Admin or Hotspot ([Locations](#locations-where-things-spawn-and-march)) | Where it rises |
| `destination` | `{ "location": Point/Admin, "radius": 10-100, "name": 1-40 characters }` | Where it marches |
| `marchSpeed` | 0.5-5.0 m/s (2.0) | March speed |
| `growth` | spread block | Each kill on the way joins the horde; its `max` caps the horde's living count, so set it above the horde's size |
| `infest` | `{ "hunters": 0-100, "driftRadius": 0-2000, "spread": spread block }` (all required) | How the infestation behaves after the march |
| `scope`, `scoreboard` (true), `allowTerritory` (false) | | As Apocalypse |
</details>

<details>
<summary><b>Unit entries and modifiers</b></summary>

**A unit entry** is a fixed unit or a draw from a pool, never both:

| Key | Values | Effect | Allowed in |
|---|---|---|---|
| `prefab` | a known `CHAR_*` name, not deny-listed | This unit | all |
| `pool` | a pool name (the event's own, else the library's) | Draw each copy from the pool at random, repeats allowed | all |
| `count` | 1-500 | Copies (SpawnWaves, HordeMarch); draw **weight** (Apocalypse, Infestation) | all |
| `min`, `max` | min 1-500, max min-500, with `pool`, instead of `count` | A random count, rolled uniformly in min..max at each wave (or at the horde's rise) | SpawnWaves (`units`, `waveList`), HordeMarch |
| `chance` | 0.05-1.0 (1.0) | Each copy joins with this chance (Apocalypse/Infestation: it scales the weight) | all |
| `modifiers` | modifiers object | This entry's level and stats | waveList, Apocalypse, Infestation, HordeMarch (the units form uses `action.modifiers`) |

A wave holding a pool entry may total at most 500 units (counting each `max`). At run time `MaxUnitsPerWave` and the
free `MaxTrackedUnits` slots cut it further.

**Modifiers** (an object naming at least one key; on units the mod spawns, removed with them):

| Key | Values | Effect |
|---|---|---|
| `level` | 1-120 | Sets the unit's level |
| `levelDelta` | -5..+5 | Shifts the unit's own level (result kept within 1-120). Not with `level` |
| `maxHealth` | 0.5-3.0, ≤ 2 decimals | Multiplies maximum health |
| `power` | 0.5-3.0, ≤ 2 decimals | Multiplies physical **and** spell power |
| `moveSpeed` | 0.5-3.0, ≤ 2 decimals | Multiplies movement speed |
| `attackSpeed` | 0.5-3.0, ≤ 2 decimals | Multiplies primary and ability attack speed |

1.0 means unchanged; below 1.0 weakens. A swarm's `growth.addLevels` adds on top of the entry's level. Empowerment
uses its own `stats` (Empower, above), which only raise (1.0-3.0).
</details>

## Unit pools

<details>
<summary><b>What a pool is: own pools and the library</b></summary>

A pool is a named list of 1-30 units. A unit entry or boss roll that names a pool draws from it at random, so no two
runs look the same. There are two kinds:

- **Own pools**: an event's `action.pools`, `{ "<name>": [ "CHAR_…", … ] }`, 1-8 pools, each used by at least one entry,
  boss roll or spread `pool` (`action.pools.<name> is not used` otherwise). They belong to that event alone.
- **The library**: `BepInEx/config/Nyarlathotep/pools.json`, pools any event can name without defining them. An
  event's own pool of the same name wins over the library's.

An event resolves its library pools when it loads, and a running event keeps drawing from what it started with.
`.nyar pool info <pool>` lists the events that name a library pool.
</details>

<details>
<summary><b>pools.json: format and the 17 seeded pools</b></summary>

```json
{
  "SchemaVersion": 1,
  "pools": {
    "undead-units": [ "CHAR_Undead_SkeletonSoldier_Armored_Farbane", "CHAR_Undead_GhostBanshee" ],
    "my-bandits":   [ "CHAR_Bandit_Thug", "CHAR_Bandit_Hunter", "CHAR_Bandit_Deadeye" ]
  }
}
```

Rules: top level exactly `SchemaVersion` (`1`) and `pools`; 1-64 pools; a name is 1-24 of `a-z 0-9 -`; each pool 1-30
distinct, known, not deny-listed `CHAR_*` names; at most 256 KB; no comments or trailing commas.

At a start that finds no `pools.json`, the mod writes one with the nine allied groups' units and bosses (only units seen
fighting side by side in game):

| Group | Units pool | Bosses pool (V Bloods) |
|---|---|---|
| undead | `undead-units` | `undead-bosses` |
| spiders | `spiders-units` | `spiders-bosses` |
| bandits | `bandits-units` | — (no bandit V Blood is allied with the group) |
| militia | `militia-units` | `militia-bosses` |
| church | `church-units` | `church-bosses` |
| legion | `legion-units` | `legion-bosses` |
| gloomrot | `gloomrot-units` | `gloomrot-bosses` |
| cursed | `cursed-units` | `cursed-bosses` |
| blackfangs | `blackfangs-units` | `blackfangs-bosses` |

A unit this server does not know is left out of the seed with a log line. After that the file is **yours**: the mod
never writes it again (delete it, then `.nyar pool reload` or a restart writes the seed again). `.nyar pool list` marks a pool `seeded` while it
is exactly as written, else `library`.
</details>

<details>
<summary><b>Add or edit a library pool</b></summary>

1. Open `BepInEx/config/Nyarlathotep/pools.json` and add a pool, or add and remove names in one (the server may run).
2. In game: `.nyar pool reload`. It re-reads `pools.json`, then reloads `events.json` against it.
3. Read the reply: `pools: 18 loaded; events invalid now: none`, or the ids of every event that is now disabled
   (for example one naming a pool you renamed). Fix those and reload again.
4. `.nyar pool list [page]` shows each pool's size; `.nyar pool info <pool> [page]` its members, any "mixes groups"
   warning and the events naming it.

If `pools.json` is broken, **no** library pool loads: the reply and log say `pools.json not loaded: <reason>`,
`.nyar status` (for admins) lists `pools unknown`, and every event that names a library pool loads disabled
(`action.units.1.pool: no pool <name>`) until the file is fixed and reloaded. `.nyar event reload` does not re-read
`pools.json`; only `.nyar pool reload` (or a restart) does.

**"pool <name> mixes groups: undead, legion".** A warning at load and in `pool info`: the pool holds units of more than
one allied group, and units of different groups attack each other when they spawn together. The pool still loads; keep
each pool to one group unless you want them to fight.
</details>

<details>
<summary><b>Use a pool in an event</b></summary>

In `events.json`, name the pool where a prefab would go:

```jsonc
"units": [
  { "pool": "undead-units", "count": 8, "modifiers": { "level": 30 } },   // 8 draws, repeats allowed
  { "pool": "undead-units", "min": 2, "max": 6 }                          // 2-6 draws, rolled each wave
],
"bosses": [ { "pool": "undead-bosses", "min": 1, "max": 2 } ]             // 1-2 distinct V Bloods
```

Where a pool can be named:

| Action | Unit entries | Boss rolls | Spread `pool` |
|---|---|---|---|
| SpawnWaves | `units`, `waveList.<n>.units` (count or min/max) | `bosses`, `waveList.<n>.bosses` | |
| HordeMarch | `units` (count or min/max) | `bosses` | `growth.pool`, `infest.spread.pool` |
| Apocalypse | `units` (count is a weight; no min/max) | — (fixed bosses only) | `roamers.pool` |
| Infestation | `units` (count is a weight; no min/max) | | `spread.pool` |

- **Random counts:** `min`/`max` replace `count` on a pool entry of a SpawnWaves or HordeMarch action; each wave (or
  the horde) rolls a count uniformly in min..max. In Apocalypse and Infestation `count` is a draw weight and
  `min`/`max` are refused.
- **Boss rolls:** `min`-`max` (1-5) distinct members per roll; at most 5 bosses per list; the pool must have at least
  `max` members left after the list's fixed bosses and earlier rolls.
- **Spread and growth pools:** each spread, growth or roamer unit is one random member, with the modifiers of the
  first `units` entry that names the same pool (none if no entry does).

In chat (see [Commands](#commands)): `pool:<name>:<count>` or `pool:<name>:<min>-<max>` in an `action.units` or
`action.waveList.<n>.units` list, `pool:<name>:<min>-<max>` in `action.bosses` or `action.waveList.<n>.bosses`, and
`action.pools.<name> <unit>,<unit>,…` (`+<unit>`, `-<unit>`, `none`) for an event's own pools. Library pools are edited
only in `pools.json`.
</details>

## Uninstall

Stop the server and delete `Nyarlathotep.dll`. The mod's units carry a timer and expire on their own, and so
do empowerment buffs. To downgrade to 0.3.0, run `.nyar purge confirm` first: 0.3.0 does not remove the buffs,
which otherwise stay until their event's time runs out. An older release disables an event using a key it does not
know until you remove the key: 0.14.0 an `allowTerritory` on an Apocalypse, Infestation or HordeMarch, a pool entry
with `min`/`max`, and a pool name only `pools.json` holds (it ignores `pools.json`); 0.12.0 the 0.13.0 Apocalypse action (and any event longer than 7200 s), `pursuit`,
`pools` and a wave's `bosses` (it ignores `[UnitWatch]`), 0.11.0 the 0.12.0 `Revenge` trigger and `bosses`, 0.8.0 the 0.9.0 keys `waveList` and `scoreboard` (it ignores `[Scoreboard]
IncludeAdmins`), 0.7.0 the 0.8.0 triggers (`Interval`, `RegionEntered`, `FactionKills`) and `fanOut`
(it ignores the next starts kept in `state.json`), 0.6.0 the 0.7.0 wave keys (`chance`, `modifiers`, `loot`, `behaviour`,
`allowTerritory`) and the `AroundPlayer` location, 0.5.x `scope` and `{region}`. To
remove its data too, delete `BepInEx/config/kdpen.Nyarlathotep.cfg` and the `BepInEx/config/Nyarlathotep/`
folder.

## Feedback

- **[The Shadow Realm Discord](https://discord.gg/usC9QgBrXK)**: questions, bugs and ideas.
- **GitHub:** [issues](https://github.com/KDavidP1987/Nyarlathotep-Lord-of-Chaos/issues) ·
  [source](https://github.com/KDavidP1987/Nyarlathotep-Lord-of-Chaos)

## Acknowledgements & License

- [VampireCommandFramework](https://thunderstore.io/c/v-rising/p/deca/VampireCommandFramework/) by **deca**, the chat-command framework.
- The V Rising modding community, whose open-source mods this one learned from.

Licensed under **AGPL-3.0-or-later**, © 2026 Kristopher Penland.
