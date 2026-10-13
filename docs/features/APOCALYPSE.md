# Apocalypse: the outbreak event

> **Status:** step 1 in progress (spikes); step 2 (Logic: schema, rules, record, chat) built on its branch. Plan `docs/dod/apocalypse.md` (0.13.0, the Halloween release). The
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

## Action contract

The keys of `"type": "Apocalypse"` (D1), each refused with its path when out of range; an Apocalypse runs 600-604800 s
(every other kind keeps 30-7200 s).

| Key | Values |
|---|---|
| `faction` | a known, not deny-listed `Faction_` name |
| `units[]{prefab,count,chance,modifiers}` | 1-10 entries, as SpawnWaves' units |
| `hotspots{count,minNatives,minSpacing}` | 1-5; 3-50 natives in one 100 m cell; 100-3000 m apart |
| `roamers{perHotspot,perKill,max,perMinute,huntRange}` | 1-10; 0-2; 1-100; 1-30; 20-60 |
| `hordes{count,firstAfterMinutes,everyMinutes,size,growth}` | 1-10; 0-1440; 5-1440; 5-50; 0-20; the last horde rises before the end |
| `goal{location,radius,name}` | `{ "type": "Point", x, z, optional y }` or `{ "type": "Admin" }` (Manual trigger only); 10-100 m; 1-32 characters |
| `marchSpeed` | 0.5-5.0 m/s |
| `bosses[]{prefab,count,modifiers}` | optional, revenge D31's form; joins the last horde |
| `scope` | Global or region names; it bounds where hotspots are found, never where roamers hunt |
| `scoreboard` | true (default) or false |

`roamers.huntRange` is the Hunt behaviour's range once the mover has brought a roamer within reach of its player and
seeded aggro (event-spawns' Hunt); the map-wide search itself has no range.

## Resume and crash loss

The record in state.json is written at once at a horde's rise or defeat, every 30 s of a march and every 60 s
otherwise. A crash therefore loses at most the last record-write interval (60 s, 30 s during a march) of scoreboard
credits, spread window and front progress, and a horde defeated inside that interval is rebuilt at the resume. A clean
restart loses nothing. The unit tally (D15) is per boot: it restarts at the resume, the units from before the restart
are accounted by the boot sweep's line, and the rebuilt units are queued into the new tally.

## Open questions

- March: hidden hops of the horde (go) or a relay that respawns it at the front (no-go)? The step 1 spike decides
  it (D24).
- Pathing (design §9 D50): can the game's own pathing walk a horde 300 m? If so, the owner chooses whether hordes
  keep walking while players are near (a D6 amendment).
- Faction mixes: which undead, ghost and shadow units stay allied when spawned together (D37)?

## Test results
