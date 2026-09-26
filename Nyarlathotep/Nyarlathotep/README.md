# Nyarlathotep, Lord of Chaos

![Nyarlathotep, Lord of Chaos](https://raw.githubusercontent.com/KDavidP1987/Nyarlathotep-Lord-of-Chaos/main/docs/img/nyarlathotep-cover.jpg)

A **server-side** event layer for V Rising. Admins stage NPC events the base game doesn't have: waves of
enemies on a schedule, at nightfall or after a V Blood falls, with warnings and banners for players, and in
later releases empowered factions, castle sieges, defended zones and boss-fight adds.

> **Public beta (0.2.0).** Every pillar and automatic announcement is off by default; no event runs until an
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
<summary><b>Faction empowerment</b> · <i>in development</i></summary>

Every NPC of a faction is buffed for a set time, on a schedule or after a trigger such as a V Blood kill.
It works like a Blood Moon for the NPCs. The buff rides on a timed effect that expires on its own, so nothing
stays changed after the event.
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
- **Faction empowerment in progress:** *coming with faction empowerment*
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

1. In `BepInEx/config/kdpen.Nyarlathotep.cfg`, turn on a pillar: `[Pillars] EventSpawns = true`.
2. Optionally turn on `[Announcements] WaveWarnings` and `EventBanners`.
3. Start the server. The first boot writes five disabled example events to `BepInEx/config/Nyarlathotep/events.json`.
4. In game, as an admin: `.nyar event list`, `.nyar event enable example-spawns`, then stand where you want the
   raid and type `.nyar event start example-spawns`.
5. Watch it with `.nyar status`. If anything goes wrong, `.nyar purge` then `.nyar purge confirm` ends everything.

Scheduled and triggered events spawn at a `Point` location (world `x` and `z` in `events.json`); an `Admin`
location spawns around the admin and works for manual starts only. Edit the file, then `.nyar event reload`. A
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
</details>

<details>
<summary><b>Events</b> <i>(admin)</i></summary>

| Command | What it does |
|---|---|
| `.nyar event list [page]` / `info <id>` | Event definitions and their state |
| `.nyar event start <id>` / `stop <id>` | Start now / end early |
| `.nyar event enable <id>` / `disable <id>` | Switch an event on or off (saved to `events.json`) |
| `.nyar event set <id> <field> <value>` | Change `name`, `durationSeconds`, `conditions.minPlayers`, `conditions.cooldownMinutes`, `conditions.chancePercent`, `action.waves`, `action.intervalSeconds` or `action.radius` |
| `.nyar event reload` | Re-read `events.json` |
| `.nyar spawn <unit> [count] [level\|+n\|-n] [hp] [power]` | One-off test spawn beside you, removed after `ManualSpawnLifetimeSeconds` |
| `.nyar debug here [radius]` | The mod's units near you, with lifetime, level and stats |
| `.nyar announce <text>` | Broadcast a line to everyone (quote text longer than 16 words) |
</details>

<details>
<summary><b>Kill switch</b> <i>(admin)</i></summary>

`.nyar purge`, then `.nyar purge confirm`, ends every event, removes every unit the mod spawned and holds off
new events for `PurgeCooldownSeconds` (60 s). To take the mod out entirely, stop the server and delete
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
| Announcements | WaveWarnings · EventBanners · DailyBanner | `false` | Wave warnings, start/end banners, the daily banner |
| Announcements | WarningOffsets | `300,60,10` | Seconds before a wave at which warnings fire (1-5 values, each 5-3600) |
| Announcements | DailyBannerTime | `20:00` | Server-local time of the daily banner |
| Announcements | LoginStats · PlayerShare · ShareCooldownSeconds · ShareMaxPerMinute | `false` · `false` · `300` · `3` | Reserved for stats (later release); cooldown 10-86400, per minute 1-20 |
| Debug | VerboseLogging · TimingLog | `false` | Log every spawn, removal and announcement · the scheduler's tick time |

**Upgrading from 0.1.0:** `General.AnnounceEvents` is retired and ignored; use the `[Announcements]` switches.

## Uninstall

Stop the server and delete `Nyarlathotep.dll`. The mod's units carry a timer and expire on their own. To
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
