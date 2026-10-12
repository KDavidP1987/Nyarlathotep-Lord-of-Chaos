# Hunting grounds — wave events bound to map regions

**Status:** in progress for 0.11.0 (docs/dod/hunting-grounds.md, audit docs/audits/hunting-grounds.md). Steps 1-3 are
built: the mover spike passed with hidden jumps, and the logic and services passed their Session 2 scenarios (Test
results). The owner's live checks (D26, D27) and the release (step 4) are next.

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
  the straight line must be inside a scope region (Review 1 F5). Session 2 saw no group stuck at a border; the units
  that waited stood above or beside ground a step cannot reach (A1 hillsides, A2 a ravine), both now handled.
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

### Session 1 · 2026-10-12 · the border scan

The 0.10.0 DLL, wrapped by `dev-snapshot -Save hg1` (restored afterwards, hashes equal). `hunting-grounds-borders.vrs`
(51/53; the two misses were a doubled reply) and `hunting-grounds-borders2.vrs` (PASS 39/39) moved Chaos with `.devtp`
and asked `.nyar region here` at each point:

- The pond (-1880, -1640) is in **FarbaneWoods**. Going west, the map's regions end between x -2180 and -2280; going
  south, between z -1940 and -2040.
- To the north, Farbane Woods meets **DunleyFarmlands** between z -1140 and -1120 at x -1880 (a ravine, y -30), and
  between z -1200 and -1160 at x -1680 (y 0). At x -2080 it meets **SilverlightHills** between z -1160 and -1120.
- So the marks are on x -1680: hg-in (-1680, -1215), 15 m or more inside Farbane; hg-a (-1680, -1600) and hg-b (-1680,
  -1300), 300 m apart inside Farbane; hg-far (-1680, -1080) in Dunley.
- Log check: "0 unhandled, 68 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons". The warnings were only
  the known three; NyarDev.log had only the 226 missing-prefab lines.

### Session 2 · 2026-10-12 · the step 3 build

The plan's D-items call this run "Session 1"; this child's Session 1 was the border scan above. The step 3 Release DLL
ran with NyarDevTools and DevChatEcho, wrapped by `dev-snapshot -Save hg2`. The dev events.json held hg-roam, hg-lock
(Point -1680, -1215), hg-ambush, hg-point and hg-timing, all scoped to FarbaneWoods, and `[Debug] VerboseLogging = true`.
hg-point first stood at (-1680, -1600), with Chaos at (-1680, -1300). Native bandits there killed the arriving units, so
hg-point moved to the pond (-1880, -1640) and Chaos to hg-b (-1580, -1640), the spike's line.

| Item | Scenario | Result |
|---|---|---|
| D21 roam | `hunting-grounds.vrs` | **PASS 8/8** (`hunting-grounds-20261012-105140.json`). Wave 1 had 2 groups at (-1757.8, -1554.5) and (-1388.7, -1510.7), about 370 m apart. Wave 2 had 2 groups at (-1237.2, -1804.5) and (-998.1, -1853), about 244 m apart. All spots were at h 10-11. A visit run (session scratchpad, PASS 15/15) found each spot in FarbaneWoods with its group listed by `debug here 15`. |
| D22 lock | `hunting-grounds-lock.vrs` | **PASS 19/19** (`-105922`). "region lock hg-lock: returned 6, farthest 77 m out"; the 6 were back at hg-in, in FarbaneWoods. |
| D23 ambush | `hunting-grounds-ambush.vrs` | **PASS 12/12** (`-110124`). At the pond (inside): "around a player: 4 units queued". From DunleyFarmlands: "wave 1 of hg-ambush skipped: no eligible player". |
| D24 hunt | `hunting-grounds-hunt.vrs` | First run 15/17: two arrivals were killed by bandits at the old hg-b, so the line moved. **PASS 17/17** on the new line (`-113113`): jumps 267 → 239 → … → 55 m, "no tracked units within 49 m" right after the final jump, the 6 listed within 60 m, and no jump after Chaos crossed into Dunley. Rerun on the A1 build: **PASS 17/17** (`-121504`). |
| D25 ends | `hunting-grounds-ends.vrs` | **PASS 18/18** (`-113622`). Stop and pillar-off each left 0 tracked, with no region line after the end. Restart: "event hg-point cancelled by restart …" and "boot marker sweep: 6 found, 6 queued for despawn". Purge: at the end of the timing run, "purge: 1 events ended, 20 units queued", then 0 tracked and no region line. |
| D28 tick | `hunting-grounds-timing.vrs` | First run 11/12: the scenario expected 12 units in wave 2, but one of its three groups found no spot and was skipped, as D4 says. The expectation was fixed. On the A1 build: **PASS 12/12** (`-124725`); `preflight -TimingSpan … -Windows 10 -MinTracked 10`: "timing span: 10/10 windows under 5 ms, tracked >= 10, targets >= 1, 0 slow ticks" (windows averaged about 0.4 ms). |

What the runs found, each fixed and amended before release:

- **Hillsides (A1).** In the first timing run, 12 of 24 units waited the whole event. A verbose diagnostic listed every
  landing "not walkable", with the units at h 13, y 15, over ground two or three levels down. A jump now also tries two
  and three levels from the unit, and a unit that found no landing plans last, so it cannot spend the walk budget of
  units that can jump. Afterwards, 4 of 24 still waited.
- **A ravine (A2).** The units still waiting stood at the rim of a ravine east of the pond, about 35-45 m wide and 15 m
  deep: y 0 at x -1493, -14 at -1515 and 0 again at -1540 (z -1675), with cliff walls. "region walk: 683 checks, 647 not
  grounded, 22 not free". A jump now tries 35, 45 and 55 m crossings, but only after every landing of 25 m or less
  failed, and never ending within 100 m of the target. A 150 s rerun took the waiting units from 7-10 down to 0-4, and
  units in reach up to 17.
- **The hand-over.** After the final jump the units stood about 53 m from Chaos. They were seeded within one hunt
  interval ("hunt targets: 1"), went into Combat, then into Return. Chaos is invulnerable, so the real fight with a
  vulnerable player is D26's.
- **Log checks.** Every boot: 0 unhandled, 0 orphan errors, 0 unity errors, regions 10 polygons. The only warnings were
  the known three, plus the stop and purge lines. NyarDev.log had only the 226 missing-prefab lines.
