# Regions — global or regional scope for triggers and actions

**Status:** in build (docs/dod/regions.md, audit docs/audits/regions.md). Step 1 (region index, scope model,
regions unavailable) built at e512382; step 2 (scope enforced in triggers, empowerment and waves; `.nyar region`;
scope in `event info|list|set`; `{region}`; api 5) built and post-audited at a85852e (Codex READY at round 4). Its baseline boot read the dev
world's index: 10 polygons, one per region. Step 3 (Session 1 on 0.6.0, then the release) is next. 0.5.2 is the current published release.

## Goal

An event can be limited to named regions of the map, the game's own world regions (Farbane Woods, Dunley Farmlands,
the Cursed Forest …). A V Blood trigger fires only for a kill in its regions; empowerment reaches only NPCs standing
there; waves spawn only there. Without a `scope` key everything stays global, exactly as in 0.5.x.

## What ships

- **Step 1:** Logic/Regions.cs (RegionIndex: box test, then even-odd point-in-polygon; RegionNames; Scope; display
  names); the optional `scope` key on `trigger` and `action` in events.json and templates; Services/RegionMap.cs
  reading the game's WorldRegionPolygon entities once per boot (read-only), with the boot line
  "regions: <p> polygons, <r> regions (<names>)"; regional definitions disabled with "regions unavailable" when the
  map cannot be read, and the health entry "regions: unavailable".
- **Step 2:** scope in triggers (kill position; a player in the regions for Schedule, GameTime and Manual),
  empowerment (the "region" skip), waves (Point and Admin locations, ring points), `.nyar region list|here`,
  `event info|list|set` scope lines, `{region}`, and api 5 (`api regions`, `region=` keys).
- **Step 3:** Session 1 in game, then release 0.6.0.

## Test plan

- **Unit tests:** RegionTests, EventValidationTests Scope, DependencyFailureTests Regions (step 1); trigger,
  eligibility, spawning, admin, announcer, wire, privacy and control-case tests, and RegionTests Cost over the
  boot's polygon count (step 2, A31).
- **Preflight:** LogCheck's regions line (A2, A14), Commands/bad-13 (A30), the `api regions` allow-list entry.
- **Session 1 (owner, step 3):** `.nyar region here` in three regions, a regional empowerment, a regional wave start
  refused and accepted, a regional V Blood trigger, and the tick timing (D12).

## Open questions

- None yet.

## Test results

### Owner steps for Session 1 · regions step 3

Setup (Claude): 0.6.0 deployed (version pair set first, A33), snapshot rg1, dev world booted; the boot line reads
"regions: 10 polygons, 10 regions (…); 0 untagged, 0 dropped", equal to the fixture (A31). Debug.TimingLog is on;
every pillar is on. Every reply below also lands in the BepInEx log, so note only what differs from the expectation.
The route is Dunley Farmlands → Farbane Woods → Cursed Forest; travel by waygate or on foot.

1. Connect to **127.0.0.1:9876** (Nyar Dev) with your admin character. Open the console (the ~ key), enter
   `adminauth`, and close it.
2. Create the four test events from templates (you can do this anywhere):
   - `.nyar template use undead-nightfall as rg-undead`
   - `.nyar event set rg-undead trigger.type Manual`
   - `.nyar event set rg-undead action.scope CursedForest`
   - `.nyar template use bandit-ambush as rg-ambush`
   - `.nyar event set rg-ambush action.scope FarbaneWoods`
   - `.nyar template use bandit-vengeance as rg-vengeance`
   - `.nyar event set rg-vengeance trigger.scope FarbaneWoods`
   - `.nyar event copy rg-vengeance rg-vengeance-dunley`
   - `.nyar event set rg-vengeance-dunley trigger.scope DunleyFarmlands`
   - `.nyar event enable rg-undead`, `.nyar event enable rg-ambush`, `.nyar event enable rg-vengeance`,
     `.nyar event enable rg-vengeance-dunley`
   Expect each command to be accepted.
3. Run `.nyar event info rg-undead`. Expect the lines "trigger scope: Global" and "action scope: CursedForest".
   Run `.nyar event info rg-vengeance`. Expect "trigger scope: FarbaneWoods" and "action scope: Global".
4. Run `.nyar region list`. Expect one line per region, including "FarbaneWoods (Farbane Woods): 2 events",
   "DunleyFarmlands (Dunley Farmlands): 1 events" and "CursedForest (Cursed Forest): 1 events", then a "global:" line.
5. Go to **Dunley Farmlands**. Run `.nyar region here`. Expect "you are in DunleyFarmlands (Dunley Farmlands)".
6. Still in Dunley, run `.nyar event start rg-ambush`. Expect the refusal "your position is outside the event's
   regions", and no bandits appear.
7. Go to **Farbane Woods**. Run `.nyar region here`. Expect "you are in FarbaneWoods (Farbane Woods)".
8. Stand in an open spot and run `.nyar event start rg-ambush`. Expect the start line, and bandits spawn near you
   (step back; they attack). Then run `.nyar event stop rg-ambush`. Expect the stop line, and the bandits vanish
   within a few seconds.
9. Go to Rufus the Foreman's lumber camp in Farbane and kill Rufus. Right after, run `.nyar status`.
   Expect **rg-vengeance** running and **rg-vengeance-dunley** not running. Then run
   `.nyar event stop rg-vengeance`.
10. Go to a graveyard in Farbane with Undead in view (a Rotting Ghoul or Skeleton). Run
    `.nyar event start rg-undead`. Expect the start line. Wait 10 seconds, then run `.nyar debug here 40`.
    Expect the Undead rows to show **"carrier none"**, because they are outside the Cursed Forest.
11. Go to the **Cursed Forest** within about 15 minutes (rg-undead lasts 20). Run `.nyar region here`. Expect
    "you are in CursedForest (Cursed Forest)".
12. With Undead in view (the edge of the forest is enough), wait 20 seconds, then run `.nyar debug here 40`.
    Expect the Undead rows to show **"carrier rg-undead"**.
13. Run `.nyar event stop rg-undead`. Wait 5 seconds, then run `.nyar debug here 40`. Expect "carrier none" on
    every row.
14. Stay connected for about 2 minutes, so the server autosaves after these steps. Then tell me you're done, and
    paste anything that differed from the expectation.

Afterwards (Claude): stop after an autosave that follows the last step; copy both logs; `-LogCheck` (the line must
carry ", regions 10 polygons", A33, A36); read every [Error] and [Warning] line; record every observed line below;
the tick-timing averages during the rg-undead sweeps (under 5 ms, Epic D24); `dev-snapshot.ps1 -Restore`, then
redeploy.
