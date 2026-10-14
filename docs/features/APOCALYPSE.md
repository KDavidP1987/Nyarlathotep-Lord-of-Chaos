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

### Spike · 2026-10-13 · step 1 (0.12.0 DLL + NyarDevTools .devhop clearPath, .devwalk) · vrclient as Chaos on the local dev server

Snapshot `tools/dev-snapshot.ps1 -Save apo0` before the first boot; logs copied to %TEMP%
yar-apo0-logs before each
stop. Dev cfg for the session: VerboseLogging true (died lines), ManualSpawnLifetimeSeconds 1800 (boot 2; at 300 the
marched units fell due mid-march in run 1). Chaos was found dead in the restored world and was respawned at his coffin.

**March (D24)**: `tools/vrclient/scenarios/apocalypse-spike.vrs`, run 3 SCENARIO PASS 83/83
(`apocalypse-spike-20261013-195425.json`). 20 CHAR_Undead_SkeletonSoldier_Armored_Farbane hopped 10 m east every 5 s,
30 times, from the pond (-1880, -1640) to (-1580, -1640), anchored on the moving mark apo-s0, home moved and the path
cleared on each hop, Chaos 150 m north of the line's middle (no player within 100 m of a hop).

- [x] units alive: 20/20 at the end of the march (run 3; run 2 kept 19/20, one fell out of the hop radius)
- [x] units within 30 m of the moved point: 20/20 at the last read before the approach; 18/20 when Chaos arrived (a native CHAR_Forest_Bear_Standard killed 2 at the end point, "died (killer CHAR_Forest_Bear_Standard)")
- [x] height within 2 m of the walk level: 20/20 at the end (y 0.0, Chaos's [DEV WHERE] y 0.0); mid-march, over the pond's water, some units read y -50 for a hop or two and came back to the ground by the end; step 3's hop writes the walk check's height, as the plan says
- [x] units that engage Chaos on approach: 18/18 living (state Combat, target=1, within 10 s)
- [x] errors: none from the hops (the log check below)
- Run 1 without the path clear: units fell behind the moving anchor (moved 20, 18, 15, 13, 9 … 0 within the 25 m hop radius), the hunting-grounds spike's variant 2 finding; clearing the path and velocity, as hunting-grounds' mover does, kept every unit with the anchor.

march verdict: go — hop

**Pathing probe (D24, design §9 D50)**: the same scenario's second half. 10 fresh units at the pond got their return
point (SpawnTransform.Position and PreCombatPosition) moved 40 m ahead toward apo-s0 every 5 s for 120 s; run 3 also put
each unit not in combat into the Return state (GenericEnemyState.Return). Chaos stood 150 m north of the line.

- [ ] units within 30 m of the walk target after 120 s: 0/10 (runs 1 and 3)
- longest stall: 238 s in run 3 (no unit got 2 m closer at any call; "farthest" stayed 302.9 m from the first call to the last)
- errors: none
- Reading: an idle unit does not walk to a moved return point, and writing the Return state does not start a walk either; the game's own pathing was not reached with these levers.

pathing verdict: walk not usable

D6 stays as written (hordes hop, hold while a player is near); hordes do not keep walking among players. D24 waits on the
owner's acceptance of this record (rubric 3).
