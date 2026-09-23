# Nyarlathotep, Lord of Chaos

A server-side BepInEx IL2CPP plugin for V Rising that adds admin-configured, event-driven NPC behaviour:
timed faction empowerment, castle sieges and reactive defended zones, boss-fight reinforcements, and
scheduled spawn waves with optional stat modifiers.

## Status

v0.1.0 — scaffold only. The plugin loads on a dedicated server; no pillar is implemented yet. See
[`CHANGELOG.md`](CHANGELOG.md) and the build order in
[`docs/NYARLATHOTEP_DESIGN.md`](docs/NYARLATHOTEP_DESIGN.md).

## How it works

Every feature is an **event**: a *trigger* (schedule, V Blood kill, boss health phase, zone activity,
admin command) fires an *action* (empower a faction, spawn waves with a behaviour) for a *duration*, after
which everything the event created is reverted or despawned. Four services carry the load: `TriggerBus`,
`EventScheduler`, `SpawnTracker`, and the per-pillar action services.

## Features

| Pillar | Design doc |
|---|---|
| Faction empowerment | [`docs/features/FACTION_EMPOWERMENT.md`](docs/features/FACTION_EMPOWERMENT.md) |
| Sieges | [`docs/features/SIEGES.md`](docs/features/SIEGES.md) |
| Defended zones | [`docs/features/DEFENDED_ZONES.md`](docs/features/DEFENDED_ZONES.md) |
| Boss reinforcements | [`docs/features/BOSS_REINFORCEMENTS.md`](docs/features/BOSS_REINFORCEMENTS.md) |
| Event spawns | [`docs/features/EVENT_SPAWNS.md`](docs/features/EVENT_SPAWNS.md) |

## Layout

```
Nyarlathotep/Nyarlathotep/   C# project (Plugin, Core, Patches, Services, Commands, Config)
docs/                        design, research, asset guide, dev reminders, preflight, doc style
tools/preflight.ps1          release-surface sync check
```

## Building

```powershell
cd Nyarlathotep
dotnet build Nyarlathotep.sln -c Release                                        # build + deploy to local server
dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__  # compile check only
```

Targets `net6.0` with `BepInEx.Unity.IL2CPP` 6.0.0-be.733, `VampireReferenceAssemblies` 1.1.12, and
VampireCommandFramework 0.10.

## Release discipline

Six surfaces move together in one `chore(release): vX.Y.Z` commit (csproj + toml versions, both
changelogs, both READMEs). Run `pwsh tools/preflight.ps1` first.

## Docs

- [`docs/NYARLATHOTEP_DESIGN.md`](docs/NYARLATHOTEP_DESIGN.md) — architecture, config, build order, open decisions
- [`docs/RESEARCH_NOTES.md`](docs/RESEARCH_NOTES.md) — what we learned from sibling mods, reference mods, and Thunderstore
- [`docs/GAME_ASSETS.md`](docs/GAME_ASSETS.md) — using the prefab dump; factions, buffs, units
- [`docs/DEV_REMINDERS.md`](docs/DEV_REMINDERS.md) — IL2CPP/ECS gotchas with sources

## License

AGPL-3.0-or-later.
