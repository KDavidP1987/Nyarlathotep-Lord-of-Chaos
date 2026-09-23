# Nyarlathotep, Lord of Chaos

A server-side event layer for V Rising. Admins set up NPC events the base game doesn't have: a faction
that grows stronger for twenty minutes after its V Blood falls, war parties that march on castles, zones
that call for reinforcements when vampires move in, adds that join boss fights, and scheduled waves of
enemies at chosen places and strengths.

Pre-1.0 and in active development. Everything ships disabled; nothing changes on your server until an
admin turns an event on.

## What it does

| Pillar | Summary | Status |
|---|---|---|
| Faction empowerment | Timed buffs on every NPC of a faction, on a schedule or after a trigger | Planned |
| Sieges & defended zones | NPC waves that assault castles; zones that summon reinforcements | Planned |
| Boss reinforcements | Adds that join V Blood fights | Planned |
| Event spawns | Waves of chosen units into chosen areas, with optional level/HP/damage modifiers | Planned |

## Install

Server-side only. Requires BepInExPack V Rising and VampireCommandFramework. Drop `Nyarlathotep.dll` into
`BepInEx/plugins` on the dedicated server. Players need nothing.

## Commands

| Command | What it does |
|---|---|
| `.nyar` | Overview and status |

## Configuration

`BepInEx/config/kdpen.Nyarlathotep.cfg` holds the master switch, one switch per pillar (all off), and
server-wide caps (`MaxTrackedUnits`, `MaxUnitsPerWave`, `MaxConcurrentEvents`). Event definitions will live
in `BepInEx/config/Nyarlathotep/`.

## License

AGPL-3.0-or-later.
