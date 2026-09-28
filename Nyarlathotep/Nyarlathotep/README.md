# Nyarlathotep, Lord of Chaos

![Nyarlathotep, Lord of Chaos](https://raw.githubusercontent.com/KDavidP1987/Nyarlathotep-Lord-of-Chaos/main/docs/img/nyarlathotep-cover.jpg)

A **server-side** event layer for V Rising. Admins stage NPC events the base game doesn't have: waves of
enemies and empowered factions on a schedule, at nightfall or after a V Blood falls, with warnings and banners
for players, and in later releases castle sieges, defended zones and boss-fight adds.

> **Public beta (0.5.2).** Every pillar and automatic announcement is off by default; no event runs until an
> admin turns on its pillar and enables it.

## What it does

Every feature is an *event*: a trigger starts an action for a set time, and everything the event spawned is
removed after it ends. Features not yet released are marked *in development*.

<details>
<summary><b>Event spawns</b> · <i>0.2.0; modifiers in development</i></summary>

Waves of chosen units appear at a map point or around the admin who started them: up to 10 unit types, up to
10 waves, a set interval between waves. An event starts on a schedule (days and times, server-local), when
night falls or day breaks, when a V Blood dies (any, or named ones), or by command. Conditions can hold it back:
minimum players online, a cooldown, a chance, a time window. After the event ends, its units stay for
`GraceSeconds` (30 s by default), then are removed a few at a time; each also carries a timer, so none is left
behind, even across a restart. Every event sits under one pillar switch.

Each unit is placed on walkable ground at the centre's level: a spawn point in water, against a cliff or a wall
moves to the nearest walkable one (0.5.1), and one the game drops past a cliff or plateau edge is moved back beside
the centre.

*In development:* waves at chosen levels, health and damage, spawn areas by zone or around players, and loot
only if the event allows it.
</details>

<details>
<summary><b>Announcements</b> · <i>0.2.0</i></summary>

Wave warnings before each wave (by default 5 min, 1 min and 10 s ahead), start and end banners with your own
text or a stock line, and a daily banner naming the events still due that day, each behind its own switch.
Admins can also broadcast on demand with `.nyar announce`.
</details>

<details>
<summary><b>Raphael companion panel</b> · <i>0.3.0 (server side)</i></summary>

The optional client mod [Raphael](https://thunderstore.io/c/v-rising/p/TheShadowRealm/Raphael/) can show a live
events board with wave countdowns, and give admins an events list and a kill-switch button. From 0.3.0 the server
answers Raphael's reads (active events, event definitions for admins) and pushes updates when an event starts or
ends, a wave comes or is warned, the kill switch fires or the definitions change. The pushes never tell a player
more than chat or `.nyar status` does, and no line carries a position. From 0.5.2 the admin buttons (start or
stop an event, use a template, switch a pillar, purge) have machine-readable versions that answer one line each.
The panels arrive with a Raphael update.
</details>

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
<summary><b>Event templates and chat authoring</b> · <i>0.5.0</i></summary>

Six ready-made events ship inside the mod. `.nyar template use <id>` copies one into your `events.json`,
disabled, ready to adjust and enable.

| Template | What it does | Starts |
|---|---|---|
| `legion-weekend-surge` | Legion ×1.5 physical power and max health, 30 min | Saturday 20:00 |
| `bandit-vengeance` | Bandits ×1.3 physical power and attack speed, 10 min | A bandit V Blood dies (30 min cooldown) |
| `undead-nightfall` | Undead ×1.25 physical and spell power, 20 min | Nightfall |
| `militia-crackdown` | Militia and Church ×1.3 max health, 15 min | A Militia or Church V Blood dies (30 min cooldown) |
| `bandit-ambush` | 3 waves of 4 thugs and 2 hunters | By command |
| `undead-rising` | 2 waves of 5 armoured skeletons and 2 crossbowmen | By command |

You can build and change events without touching the file. Use `.nyar event new <id> <pillar>`,
`.nyar event copy <id> <newId>` and `.nyar event delete <id>` (then `confirm`). `.nyar event set` also changes
the trigger, factions, units and location. `location here` stores your position, height included.
`.nyar pillar <name> on|off` switches a pillar and saves the cfg. Every chat change is an ordinary edit of
`events.json` followed by a reload, and one `.bak` copy is kept. `.nyar event list` shows why each event would
or would not start (`ready`, `off (pillar)`, `full (cap)`, …).
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

If anything goes wrong, `.nyar purge` then `.nyar purge confirm` ends everything. Announcements stay off until
you turn on `[Announcements] WaveWarnings` and `EventBanners` in `BepInEx/config/kdpen.Nyarlathotep.cfg`.

Scheduled and triggered events spawn at a `Point` location (world `x` and `z` in `events.json`); an `Admin`
location spawns around the admin and works for manual starts only; `.nyar event set <id> location here` stores
your position as a `Point`. Edit the file, then `.nyar event reload`. A
bad entry is disabled with a log line naming the event and the reason; a file that doesn't parse leaves the last good
set running.

## Commands

`.nyar` lists the commands you are allowed to run. Commands marked *(admin)* need server admin rights.

<details>
<summary><b>For everyone</b></summary>

| Command | What it does |
|---|---|
| `.nyar` | Overview and your available commands |
| `.nyar status` | Active events and minutes left (admins also see tracked units and any degraded hook) |
| `.nyar api version` | Machine-readable handshake for the Raphael client |
| `.nyar api status` / `api sub on\|off` | Active events and live updates as machine-readable lines, for Raphael |
</details>

<details>
<summary><b>Events</b> <i>(admin)</i></summary>

| Command | What it does |
|---|---|
| `.nyar event list [page]` / `info <id>` | Event definitions and their state |
| `.nyar event start <id>` / `stop <id>` | Start now / end early |
| `.nyar event enable <id>` / `disable <id>` | Switch an event on or off (saved to `events.json`) |
| `.nyar event set <id> <field> <value>` | Change `name`, `durationSeconds`, `conditions.minPlayers`, `conditions.cooldownMinutes`, `conditions.chancePercent`, `trigger.type`, `trigger.days`, `trigger.times`, `trigger.phase`, `trigger.bosses`, `action.factions`, `action.units` (`CHAR_<name>[:<count>]`), `action.waves`, `action.intervalSeconds`, `action.radius` or `location here`; on an empowerment, `action.stats.<stat>` (1.0-3.0) |
| `.nyar event reload` | Re-read `events.json` |
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
<summary><b>Kill switch</b> <i>(admin)</i></summary>

`.nyar purge`, then `.nyar purge confirm`, ends every event, removes every unit the mod spawned and every
empowerment buff, and holds off new events for `PurgeCooldownSeconds` (60 s). To take the mod out entirely, stop the server and delete
`Nyarlathotep.dll`; the mod's units expire on their own.
</details>

## Configuration

`BepInEx/config/kdpen.Nyarlathotep.cfg`. Event definitions live in `BepInEx/config/Nyarlathotep/events.json`.
Out-of-range values are clamped at load, with a log line.

| Section | Key | Default | Effect |
|---|---|---|---|
| General | Enabled | `true` | Master switch |
| Pillars | FactionEmpowerment · SiegeWaves · DefendedZones · BossReinforcements · EventSpawns | `false` | One switch per pillar; an event runs only while its pillar is on |
| Limits | MaxTrackedUnits | `150` | Most mod units alive at once (1-500) |
| Limits | MaxUnitsPerWave | `20` | Most units in one wave (1-50) |
| Limits | MaxConcurrentEvents | `3` | Most events running at once (1-10) |
| Limits | MaxSpawnsPerTick · MaxDespawnsPerTick | `10` · `5` | Spawns and removals per server tick (1-20 each), so big waves never land in one frame |
| Limits | GraceSeconds | `30` | How long units outlive their event before removal (0-600) |
| Limits | PurgeCooldownSeconds | `60` | Pause after a purge (0-3600) |
| Limits | ManualSpawnLifetimeSeconds | `300` | Lifetime of a `.nyar spawn` unit (30-3600) |
| Limits | EmpowerBatchPerTick | `200` | Empowerment buffs applied or removed per server tick (50-1000) |
| Announcements | WaveWarnings · EventBanners · DailyBanner | `false` | Wave warnings, start/end banners, the daily banner |
| Announcements | WarningOffsets | `300,60,10` | Seconds before a wave at which warnings fire (1-5 values, each 5-3600) |
| Announcements | DailyBannerTime | `20:00` | Server-local time of the daily banner |
| Announcements | LoginStats · PlayerShare · ShareCooldownSeconds · ShareMaxPerMinute | `false` · `false` · `300` · `3` | Reserved for stats (later release); cooldown 10-86400, per minute 1-20 |
| Debug | VerboseLogging · TimingLog | `false` | Log every spawn, removal and announcement · the scheduler's tick time |

**Upgrading from 0.1.0:** `General.AnnounceEvents` is retired and ignored; use the `[Announcements]` switches.

**Upgrading from 0.2 or 0.3:** an `example-empowerment` written by an earlier release now loads disabled ("pillar
empowerment takes an Empower action"). Change its `pillar` to `spawns`, or replace it with the template under
Faction empowerment above.

## Uninstall

Stop the server and delete `Nyarlathotep.dll`. The mod's units carry a timer and expire on their own, and so
do empowerment buffs. To downgrade to 0.3.0, run `.nyar purge confirm` first: 0.3.0 does not remove the buffs,
which otherwise stay until their event's time runs out. To
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
