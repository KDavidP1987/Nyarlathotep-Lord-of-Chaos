# Apocalypse: the outbreak event

> **Status:** step 1 in progress (spikes). Plan `docs/dod/apocalypse.md` (0.13.0, the Halloween release). The
> owner's brief and the settled questions are in docs/features/OUTBREAK.md.

## Design summary

- **One action kind** `"type": "Apocalypse"` beside SpawnWaves and Empower, under the Spawns pillar switch.
- **Hotspots** are found once per run from where the faction's native NPCs stand (cells of 100 m), read only.
- **Roamers** are topped up at each hotspot at the start and at each horde rise. They hunt map-wide (design §9
  D49): each group goes after the eligible player nearest to it, moved by hunting-grounds' mover (hidden jumps only
  while no player is within 100 m, never landing in a claimed castle).
- **Spread:** a kill by a roamer or a horde unit queues one or two roamers where the victim fell, under the caps.
- **Hordes** rise one after another at the hotspots and march on a goal (a point or the admin's start position).
  The last horde brings the action's bosses (design §9 D51).
- **Outcomes:** lost when a horde reaches the goal, won when every horde is dead, survived when the time runs
  out. A scoreboard ranks the players.
- **Resume:** the only event that survives a restart, rebuilt from its record in state.json.

## Open questions

- March: hidden hops of the horde (go) or a relay that respawns it at the front (no-go)? The step 1 spike decides
  it (D24).
- Pathing (design §9 D50): can the game's own pathing walk a horde 300 m? If so, the owner chooses whether hordes
  keep walking while players are near (a D6 amendment).
- Faction mixes: which undead, ghost and shadow units stay allied when spawned together (D37)?

## Test results
