# Documentation

Where everything about Nyarlathotep is written down. The project's front page is the [root README](../README.md);
the player and admin page is the [Thunderstore README](../Nyarlathotep/Nyarlathotep/README.md).

## Design

| Document | What it covers |
|---|---|
| [NYARLATHOTEP_DESIGN.md](NYARLATHOTEP_DESIGN.md) | Vision, architecture, configuration and persistence, the command reference (§6), the settable fields, every decision (§9) |
| [DEV_REMINDERS.md](DEV_REMINDERS.md) | IL2CPP and ECS pitfalls, with their sources, and the spawn and buff safety rules |
| [GAME_ASSETS.md](GAME_ASSETS.md) | Using the prefab dump: factions, units, buffs |
| [RESEARCH_NOTES.md](RESEARCH_NOTES.md) | What was learned from sibling mods, reference mods and Thunderstore |

## Features

One document per feature, kept current as it changes. Each ends with its in-game test results.

| Released | Planned |
|---|---|
| [FOUNDATION](features/FOUNDATION.md) · event engine, kill switch, announcements | [BOSS_REINFORCEMENTS](features/BOSS_REINFORCEMENTS.md) · adds that join V Blood fights |
| [FACTION_EMPOWERMENT](features/FACTION_EMPOWERMENT.md) · timed faction buffs | [BOSS_ENCOUNTERS](features/BOSS_ENCOUNTERS.md) · boss events (backlog) |
| [EVENT_LIBRARY](features/EVENT_LIBRARY.md) · templates and chat authoring | [DEFENDED_ZONES](features/DEFENDED_ZONES.md) · zones that call reinforcements |
| [WALKABLE_SPAWNS](features/WALKABLE_SPAWNS.md) · spawn points on walkable ground | [SIEGES](features/SIEGES.md) · waves that march on castles |
| [REGIONS](features/REGIONS.md) · region scope | [OUTBREAK](features/OUTBREAK.md) · spreading hordes, built as APOCALYPSE |
| [EVENT_SPAWNS](features/EVENT_SPAWNS.md) · modifiers, Hunt, waves around a player | |
| [AUTOMATION](features/AUTOMATION.md) · automatic triggers and fan-out | |
| [WAVE_SETS](features/WAVE_SETS.md) · per-wave units and the scoreboard | |
| [SWARM](features/SWARM.md) · arenas, growth, a winner | |
| [HUNTING_GROUNDS](features/HUNTING_GROUNDS.md) · region-bound hunts | |
| [REVENGE](features/REVENGE.md) · faction revenge and final-wave bosses | |
| [APOCALYPSE](features/APOCALYPSE.md) · the map-wide outbreak (0.13.0, in test) | |
| [UNIT_WATCH](features/UNIT_WATCH.md) · stuck-unit recovery (0.13.0, in test) | |
| [RAPHAEL_API](features/RAPHAEL_API.md) · [RAPHAEL_API_ADMIN](features/RAPHAEL_API_ADMIN.md) · the Raphael wire | |

## Raphael client integration

| Document | For |
|---|---|
| [RAPHAEL_INTEGRATION_CONTRACT.md](RAPHAEL_INTEGRATION_CONTRACT.md) | The `[NYAR:*]` wire: every command and line shape, its status and api number, and the planned live-operations and communications shapes (§10) |
| [RAPHAEL_HANDOFF_API8.md](RAPHAEL_HANDOFF_API8.md) | **Start here** when building Raphael's side: panels, refresh schedule, notifications, the player overlay, a test checklist |
| [RAPHAEL_HANDOFF_WAVE_VARIETY.md](RAPHAEL_HANDOFF_WAVE_VARIETY.md) | Wave variety (0.13.0, still api 8): new field-editor fields (pursuit, pools, boss rolls), the 14 timed and onslaught templates |
| [RAPHAEL_HANDOFF_API4.md](RAPHAEL_HANDOFF_API4.md) · [RAPHAEL_HANDOFF.md](RAPHAEL_HANDOFF.md) | The earlier handoffs: admin twins (api 4); parser, handshake and the first panels (api 2) |

## Plans, audits and process

| Folder or file | What it holds |
|---|---|
| [dod/](dod/) | The Definition-of-Done plans: the Epic [nyarlathotep.md](dod/nyarlathotep.md) and one child plan per feature, with reviews and evidence. The [index](dod/README.md) is generated |
| [audits/](audits/) | The pre- and post-audit record of every build step |
| [PREFLIGHT.md](PREFLIGHT.md) | The session-start checklist |
| [DOC_STYLE.md](DOC_STYLE.md) | How the changelogs and READMEs are written |
| [SESSION_HANDOFF.md](SESSION_HANDOFF.md) | Where the last work session stopped and what is next |

## Lessons for sibling mods

| Document | For |
|---|---|
| [lessons/SPAWNING_LESSONS_FOR_URIEL.md](lessons/SPAWNING_LESSONS_FOR_URIEL.md) | Uriel: spawning, controlling, following, respawning and testing units for castle events |
| [lessons/BEELZEBUB_PROCESS_SUGGESTIONS.md](lessons/BEELZEBUB_PROCESS_SUGGESTIONS.md) | Beelzebub: process suggestions from this project |

## Testing

The current tester workbook is in [`testing/`](../testing/). Automated in-game tests are driven by
[`tools/vrclient/`](../tools/vrclient/README.md).
