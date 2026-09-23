# Nyarlathotep, Lord of Chaos

![Nyarlathotep, Lord of Chaos](https://raw.githubusercontent.com/KDavidP1987/Nyarlathotep-Lord-of-Chaos/main/docs/img/nyarlathotep-cover.jpg)

A **server-side** event layer for V Rising. Admins stage NPC events the base game doesn't have:
- a faction that grows stronger for twenty minutes after its V Blood falls
- war parties that march on castles
- zones that call for reinforcements when vampires move in
- adds that join boss fights
- scheduled waves of enemies at chosen places and strengths

Players get warnings before waves, event banners, and leaderboards for what they fought off.

> **Pre-1.0 and in active development.** Everything ships switched off; nothing changes on your server until an
> admin turns an event on. The companion client **[Raphael, Lord of Wisdom](https://discord.gg/usC9QgBrXK)**
> will add a panel for it (optional; players need no client mod).

## What it does

Each feature stays marked *in development* until the release that brings it; the changelog names that release.

<details>
<summary><b>Faction empowerment</b> · <i>in development</i></summary>

Every NPC of a faction is buffed for a set time, on a schedule or after a trigger such as a V Blood kill.
It works like a Blood Moon for the NPCs. The buff rides on a timed effect that expires on its own, so nothing
stays changed after the event.
</details>

<details>
<summary><b>Event spawns</b> · <i>in development</i></summary>

Waves of chosen units march into chosen areas at base stats, or with level, health and damage modifiers.
Spawned units despawn when the event ends, and they drop loot only if the event allows it.
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
deaths. Each shows for today, this week and all time. Players check their own numbers, browse the boards and,
if the admin allows it, share their line with the server. Anyone can leave the boards with
`.nyar stats hide`. Positions are never shown.
</details>

<details>
<summary><b>Announcements</b> · <i>in development</i></summary>

A countdown before waves, event start and end banners, and a daily banner with the day's highlights. Admins
can also broadcast on demand, and players can get a private summary of their stats when they log in. Each kind
has its own switch.
</details>

## Screenshots

<details>
<summary><b>Show screenshots</b></summary>

*Screenshots are added as each feature reaches the server. Slots so far:*

- **Wave warning and event banner:** *coming with event spawns*
- **Faction empowerment in progress:** *coming with faction empowerment*
- **Leaderboard and daily banner:** *coming with stats*
- **Raphael panel:** *coming with the Raphael integration*
</details>

## Installation

**Thunderstore Mod Manager / r2modman (recommended)**
1. Select **V Rising** and your dedicated-server profile.
2. Install **Nyarlathotep, Lord of Chaos**; its dependencies install with it.
3. Start the server once to create the config file, then stop it and review the settings.

**Manual:** copy `Nyarlathotep.dll` into `BepInEx/plugins` on the dedicated server.

**⚠ Stop the server before replacing `Nyarlathotep.dll`.** A running server locks the file.

| Dependency | Version | Role |
|---|---|---|
| [BepInExPack V Rising](https://thunderstore.io/c/v-rising/p/BepInEx/BepInExPack_V_Rising/) | 1.733.2 | Loader (required) |
| [VampireCommandFramework](https://thunderstore.io/c/v-rising/p/deca/VampireCommandFramework/) | 0.10.4 | Chat commands (required) |
| [Raphael, Lord of Wisdom](https://discord.gg/usC9QgBrXK) | — | Companion client UI (optional) |

## Quick start

*From the foundation release (0.2.0).*

1. In `BepInEx/config/kdpen.Nyarlathotep.cfg`, turn on the pillar you want, for example `[Pillars] EventSpawns = true`.
2. Start the server. It writes a disabled example event to `BepInEx/config/Nyarlathotep/events.json`.
3. In game, as an admin: `.nyar event list`, then `.nyar event enable <id>` and `.nyar event start <id>`.
4. Watch it with `.nyar status`. If anything goes wrong, `.nyar purge` then `.nyar purge confirm` ends everything.

## Commands

`.nyar` lists the commands you are allowed to run. In v0.1.0 that is only `.nyar`; the others arrive with
their features. Commands marked *(admin)* need server admin rights.

<details>
<summary><b>For everyone</b></summary>

| Command | What it does |
|---|---|
| `.nyar` | Overview and your available commands |
| `.nyar status` | Active events and time left |
| `.nyar me` | Your stats: today, this week, all time |
| `.nyar top <stat> [today\|week\|all] [page] [share]` | Leaderboard; `share` posts your line to the server (if allowed) |
| `.nyar stats hide` / `show` | Leave or rejoin the boards |
</details>

<details>
<summary><b>Events</b> <i>(admin)</i></summary>

| Command | What it does |
|---|---|
| `.nyar event list` / `info <id>` | Event definitions and their state |
| `.nyar event start <id>` / `stop <id>` | Start now / end early |
| `.nyar event enable <id>` / `disable <id>` | Switch an event on or off |
| `.nyar event set <id> <field> <value>` | Change one setting |
| `.nyar event reload` | Re-read `events.json` |
| `.nyar spawn <unit> [count] [level] [hp×] [power×]` | One-off test spawn |
</details>

<details>
<summary><b>Zones, stats and announcements</b> <i>(admin)</i></summary>

| Command | What it does |
|---|---|
| `.nyar zone add <name> <radius>` / `remove <name>` / `list` | Defended zones at your position |
| `.nyar stats reset <player\|all>` then `… confirm` | Clear stats |
| `.nyar announce <text>` / `digest` | Broadcast a message or today's highlights now |
| `.nyar debug here` | Faction, territory, zone and nearest boss where you stand |
</details>

<details>
<summary><b>Kill switch</b> <i>(admin)</i></summary>

`.nyar purge`, then `.nyar purge confirm`, ends every event, removes every unit the mod spawned and pauses
scheduled events for a minute. To take the mod out entirely, stop the server and delete `Nyarlathotep.dll`.
The mod's units expire on their own, and its buffs wear off.
</details>

## Configuration

`BepInEx/config/kdpen.Nyarlathotep.cfg`. Event definitions and zones are JSON files in
`BepInEx/config/Nyarlathotep/`.

| Section | Key | Default | Effect |
|---|---|---|---|
| General | Enabled | `true` | Master switch |
| General | AnnounceEvents | `true` | Chat announcements for events |
| Pillars | FactionEmpowerment · SiegeWaves · DefendedZones · BossReinforcements · EventSpawns | `false` | One switch per pillar |
| Limits | MaxTrackedUnits | `150` | Most units the mod keeps alive at once (hard ceiling 500) |
| Limits | MaxUnitsPerWave | `20` | Most units in one wave (hard ceiling 50) |
| Limits | MaxConcurrentEvents | `3` | Most events running at once (hard ceiling 10) |

The stats and announcement switches (`[Stats]`, `[Announcements]`) arrive with their features, also off by default.

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
