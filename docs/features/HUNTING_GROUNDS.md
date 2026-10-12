# Hunting grounds — wave events bound to map regions

**Status:** in progress for 0.11.0 (docs/dod/hunting-grounds.md, audit docs/audits/hunting-grounds.md). Step 1, the
mover spike, passed with hidden jumps (Test results › Spike); step 2, the logic, is next.

## Goal

A spawn event can be bound to named map regions (the game's own world regions, as `scope` names them). Three optional
pieces on a SpawnWaves action (design §9 D48, D50):

- a **Region location** (`"location": { "type": "Region" }`): each wave's groups spawn at spots spread over the scope
  regions, on walkable ground.
- a **region lock** (`"regionLock": true`): a unit that leaves its regions is brought back just inside the border near
  where it left, and forgets the players outside, as swarm's arena does for its circle.
- a **region hunt** (behaviour `{ "type": "RegionHunt" }`, with the lock): each group goes after the nearest eligible
  player inside the regions. A unit farther than about 60 m from its player is moved toward them in hidden jumps,
  only while no player is within 100 m to see it; within 60 m the game's own pathing and aggro take over.

## Design summary

The owner's five modes:

1. **Region roam:** a Region location, the lock and RegionHunt (the `spider-hunting-grounds` template).
2. **Point arena:** swarm's `arena`, unchanged (never combined with the lock).
3. **Region ambush:** an AroundPlayer location with a regional scope picks only players standing inside the regions;
   with the lock the units never chase out.
4. **Point spread:** a Point location with the lock and RegionHunt.
5. **Map-wide spread:** apocalypse's map-wide roamers (design §9 D49), built on this child's mover. Not in this child.

Only units this event instance spawned are ever moved. The caps, the kill switch and claimed territory beat the lock
and the hunt. No log line names or locates a player.

## Open questions

- A target behind a concave border (or across two scope regions) can leave a group waiting, because every landing on
  the straight line must be inside a scope region (Review 1 F5). If a session sees a stuck group, a sidestep landing
  becomes an amendment.
- Real pathing for apocalypse hordes in busy areas, where hidden jumps cannot happen, is studied at apocalypse's start
  (design §9 D50).

## Test results

### Spike (D20) · Session 0 · 2026-10-12

The dev server ran the 0.10.0 DLL with the dev-only NyarDevTools (`.devhop`, `.devjump`, `.devunits`), wrapped by
`dev-snapshot -Save hg0` (restored afterwards, hashes equal). There were 12 `CHAR_Spider_Forest` from `.nyar spawn` at
the pond (-1880, -1640). Chaos stood 150 m or more from every move.

| Run | Scenario | Result |
|---|---|---|
| 1 | `hunting-grounds-spike.vrs --var MOVEHOME=false` (`.devhop`, Translation only) | 34/34 (`hunting-grounds-spike-20261012-091139.json`, the last of four tuning runs); after each hop the units walked back toward their spawn point (SpawnTransform) |
| 2 | `hunting-grounds-spike.vrs --var MOVEHOME=true` (SpawnTransform moved too) | no walk home, but units part-way along a path lost ground; one reached the end, engaged a native spider and died (purge queued 11) |
| 3 | `hunting-grounds-jump.vrs --var CLEARPATH=false` (`.devjump` 25 m toward mark hg-t) | 35/35 (`-092202.json`); uneven progress, the farthest unit went 283 → 195 m in 14 jumps, 4/12 arrived |
| 4 | `hunting-grounds-jump.vrs --var CLEARPATH=true` (the path buffer and velocity cleared at each jump) | **35/35** (`-093246.json`); every unit gained 25 m per jump in step (283 → 258 → … → 111.8 m), 12/12 arrived 52–60 m from the target by jump 12 |

Checklist (run 4):

- units alive: **12/12** (`[DEV UNITS] n=12` within 100 m of the target at the end)
- units within 15 m of the moved point: **12/12** (finals at x -1651.8 … -1659.7, z -1637.6 … -1643.3; the landing point
  was 55 m west of hg-t)
- heights within 2 m of Chaos's own height there (y 0.0 after `.devgo hg-t`): **11/12** (ten at 0.0–1.8; one at 5.0).
  Mid-run samples showed brief negative heights (-30 to -50) before units snapped back onto the ground, so the mover
  takes each landing's height from the walk check.
- units listed by `.nyar debug here 60` at Chaos's approach: **12/12** (all idle, no target, at about 55 m)
- errors and crashes: none. Every boot had 0 unhandled, 0 orphan errors, 0 unity errors and regions 10 polygons; the
  only warnings were the known ones plus the purge line; missing prefabs 226 (the baseline).
- **mover verdict: go — hidden jumps**

Method: a jump writes Translation, LastTranslation, PreCombatPosition and SpawnTransform.Position, and empties the path
buffer (ProjectM.Pathfinding.PathBuffer) and Velocity. Without the path clear a unit keeps walking its old path; without
SpawnTransform it walks home. Engagement with a vulnerable player is read from swarm Session 3 and confirmed by D26.
vrclient's first cold launch fails at "could not find 'Online Play'"; a second run works.
