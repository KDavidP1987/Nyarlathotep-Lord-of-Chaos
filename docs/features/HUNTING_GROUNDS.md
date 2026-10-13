# Hunting grounds — wave events bound to map regions

**Status:** released in 0.11.0 (docs/dod/hunting-grounds.md, audit docs/audits/hunting-grounds.md). The mover spike
passed with hidden jumps, and the logic and services passed their Session 2 scenarios (Test results). The owner's
live checks (D26, D27) are recorded under Test results when done.

## Goal

A spawn event can be bound to named map regions (the game's own world regions, as `scope` names them). Three optional
pieces on a SpawnWaves action (design §9 D48, D50):

- a **Region location** (`"location": { "type": "Region" }`): each wave's groups spawn at spots spread over the scope
  regions, on walkable ground.
- a **region lock** (`"regionLock": true`): a unit that leaves its regions is brought back just inside the border near
  where it left, and forgets the players outside, as swarm's arena does for its circle.
- a **region hunt** (behaviour `{ "type": "RegionHunt" }`, with the lock): each group goes after the nearest eligible
  player inside the regions. A unit farther than about 60 m from its player is moved toward them in hidden jumps,
  only while no player is within 100 m to see it, the last jump landing 50-60 m away; a unit within 100 m of an eligible
  player inside the regions is set on them (A4), and the game's own pathing and aggro take over.

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
| D24 hunt | `hunting-grounds-hunt.vrs` | First run 15/17: two arrivals were killed by bandits at the old hg-b, so the line moved. **PASS 17/17** on the new line (`-113113`): jumps 267 → 239 → … → 55 m, "no tracked units within 49 m" right after the final jump, the 6 listed within 60 m, and no jump after Chaos crossed into Dunley. Rerun on the A1 build: **PASS 17/17** (`-121504`), on the A3 build: **PASS 17/17** (`-144703`), and after the review fix: **PASS 17/17** (`-151056`). |
| D25 ends | `hunting-grounds-ends.vrs` | **PASS 18/18** (`-113622`). Stop and pillar-off each left 0 tracked, with no region line after the end. Restart: "event hg-point cancelled by restart …" and "boot marker sweep: 6 found, 6 queued for despawn". Purge: at the end of the timing run, "purge: 1 events ended, 20 units queued", then 0 tracked and no region line. |
| D28 tick | `hunting-grounds-timing.vrs` | First run 11/12: the scenario expected 12 units in wave 2, but one of its three groups found no spot and was skipped, as D4 says. The expectation was fixed. On the A1 build: **PASS 12/12** (`-124725`); `preflight -TimingSpan … -Windows 10 -MinTracked 10`: "timing span: 10/10 windows under 5 ms, tracked >= 10, targets >= 1, 0 slow ticks" (windows averaged about 0.4 ms). On the A3 build: **PASS 12/12** (`-144040`), and after the review fix: **PASS 12/12** (`-154144`), each with the same timing-span line. All 24 units reached Chaos, with no unit stranded. |

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
- **A walk-check cap and rejoining (A3).** The A2 build's full timing run left 8 of 24 units at a south-east corner
  (near x -934, z -1958 to -2000) that neither a step nor a 55 m crossing clears. Re-planning them every check cost
  "region walk: 736 checks" and slow ticks of 262-290 ms in the region phase, because a walk check costs about 0.4 ms in
  game. The hunt now spends at most 200 walk checks per check across all events. A unit that found no landing tries,
  at its next check, to land 3 m beside the unit of its own event nearest its target. The first cap build starved the
  units: "1 jumped, 13 waiting", "13 out of budget", "region walk: 100 checks". It counted the two budget calls a walk
  check takes, so it allowed only 100 checks, and units it cut off never got their turn. The cap now counts walk
  checks. Units plan fewest failed checks first, a unit cut off keeps its count, and the events take turns planning
  first, so the cap rotates. In the 150 s rerun every check moved 11-12 units, with 0 waiting, at most 158 walk checks
  per check, and no slow tick.
- **One slow region tick.** The final timing run logged one "slow tick: 273 ms (region 273 ms …)" before its ten
  windows. That check made 45 walk checks (about 18 ms), so the walk checks did not cause it. The build now logs
  "region parts: <t> ms (open, walk in n checks, players, claimed, moves in m, outside parts; gc …)" for a region phase
  of 100 ms or more, at most once a minute. The reruns on that build and on the review fix (hunt, a 150 s timing run and
  the full timing run) had no slow tick and no region parts line. If the stall comes back, the line names it.
- **The review fix.** A fresh review of A1-A3 found that one event's stuck units could still spend the shared cap before
  another event's movable units planned. Every event's units with no failed check now plan first, then the stuck ones.
- **The hand-over.** After the final jump the units stood about 53 m from Chaos. They were seeded within one hunt
  interval ("hunt targets: 1"), went into Combat, then into Return. Chaos is invulnerable, so the real fight with a
  vulnerable player is D26's.
- **Log checks.** Every boot: 0 unhandled, 0 orphan errors, 0 unity errors, regions 10 polygons. The only warnings were
  the known three, plus the stop and purge lines. NyarDev.log had only the 226 missing-prefab lines.

### Session 3 · 2026-10-13 · the owner's D26 run and the final-ring fixes

Same dev server and dev events.json, still wrapped by `dev-snapshot -Save hg2`. The owner played by hand on the
b136d7f build (the Codex F1/F2 fixes); Claude reran with vrclient after each fix.

| Run | Build | Result |
|---|---|---|
| Owner, D26 (hg-point, standing still at hg-b) | b136d7f | **FAIL, as the owner reported it:** all 6 units jumped from 300 m to the final ring ("landed 55 m") and then idled 55-64 m away for the rest of the event, with "0 seeds kept" in all 85 hunt ticks. When the owner walked about 15 m toward them, they attacked. Two causes, each amended before its code: the seed range was 60 m, the same as the ring's outer edge (A4), and the spider's leash (`MaxDistanceFromPreCombatPosition` 45 m) dropped any seed, because every jump set its return point 50-60 m from the player (A5). |
| vrclient `hunting-grounds-hunt.vrs` | A4 (df4d9ae) | 16/20: Chaos was targeted from the ring ("1 targeted"), but the units stayed about 58 m away (`.nyar debug here 15`: "no tracked units within 15 m"). This confirmed the leash cause. The other failed steps were chat inputs that didn't open. |
| vrclient `hunting-grounds-hunt.vrs` | A5 | 18/19, then **PASS 19/19** (`-20261013-002835.json`) after the scenario's "tracked units: 6" check was relaxed, because one spider died in the fight. Seeds now hold ("5 seeds kept", "6 seeds kept"), and `debug here 15` lists the spiders at -1580 -1641, on Chaos, in both runs (screenshots `hg-point-closed-002138` and `-002733`). |

| vrclient `hunting-grounds-timing.vrs` | A5 | **PASS 12/12** (`-20261013-010258.json`): "timing span: 10/10 windows under 5 ms, tracked >= 10, targets >= 1, 0 slow ticks". |
| vrclient `hunting-grounds-lock.vrs` | A6 (eb1c0a2) | **PASS 19/19** (`-20261013-011200.json`). A6, the owner's castle rule, is unit-tested; the dev world has no castle at a region border. |
| vrclient `hunting-grounds-lock.vrs` and `hunting-grounds-hunt.vrs` | 772762a (review 2's F5) | lock **PASS 19/19** (`-012437`). Hunt was 18/19: its "none within 49 m right after the last jump" step became a race, because the units now run at Chaos as soon as they land (one stood at 46 m). The step was removed; "landed 5x m" remains the no-pop-in proof. Rerun: **PASS 18/18** (`-012958`). |

| Owner, D26 (hg-point) | 772762a | **PASS** (owner). The spiders arrived and attacked while the owner stood still. Running north, two followed to the Dunley border and stopped there; the rest had turned on NPCs and dropped the chase. "Everything seemed to work perfectly or nearly perfectly." |
| Owner, D27 roam (hg-roam with RegionHunt) | 772762a | **PASS** (owner). Spots 550-900 m away; the spiders reached the owner about 4.5 min after the start and attacked. (The first sheet's hg-roam had no RegionHunt, so its units stood where they spawned: a test-sheet error.) |
| Owner, D27 ambush (hg-ambush) | 772762a | **PASS** (owner). 4 spiders at the pond; from -1680 -1080 in Dunley "wave 1 of hg-ambush skipped: no eligible player". |
| Owner, D27 arena (spider-swarm) | 772762a | **PASS** (owner). Wave 1 cleared, wave 2 caught the owner in sunlight. The scoreboard read "no player scored" because IncludeAdmins is false. The run showed "trigger: VBloodKilled CHAR_VampireMale" on the owner's death, a defect since faction-empowerment, fixed in 62ce215 (a player's death raises nothing). |

| vrclient `vblood-player-death.vrs` | 62ce215 | **PASS 8/8** (`-20261013-114727.json`): Chaos killed by a tracked spider, no VBloodKilled; a tracked CHAR_Spider_Queen_VBlood killed, "trigger: VBloodKilled CHAR_Spider_Queen_VBlood". |

D27 **PASS** (owner): "the modes feel right overall. It feels like we're moving in a good direction." spider-hunting-grounds (optional) was not run.
