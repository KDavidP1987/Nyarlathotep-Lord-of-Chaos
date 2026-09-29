# Regions — global or regional scope for triggers and actions

**Status:** released in 0.6.0 (docs/dod/regions.md done, audit docs/audits/regions.md). Step 1 (region index, scope model,
regions unavailable) built at e512382; step 2 (scope enforced in triggers, empowerment and waves; `.nyar region`;
scope in `event info|list|set`; `{region}`; api 5) built and post-audited at a85852e (Codex READY at round 4). Its baseline boot read the dev
world's index: 10 polygons, one per region. Step 3: Session 1 passed (A39), 0.6.0 tagged at 85f6080 and pre-released on GitHub.

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

### Session 1 · 2026-09-28 · regions step 3 (eb57d80 plus the uncommitted 0.6.0 version pair, dev world nyardev, with the owner)

Setup as in the steps above (`pwsh tools/dev-snapshot.ps1 -Save rg1` before the deploy). Owner connected about 22:10–23:23. `.nyar region`, `event info` and
`event list` log nothing by design (RegionCommands D14), so steps 3, 4, 5, 7 and 11 are read from the owner's chat only.

- [x] boot: "regions: 10 polygons, 10 regions (StartCave, FarbaneWoods, DunleyFarmlands, CursedForest, HallowedMountains,
  SilverlightHills, Gloomrot_South, Gloomrot_North, RuinsOfMortium, Strongblade); 0 untagged, 0 dropped", equal to the fixture
- [x] step 2: every command accepted ("template undead-nightfall added as rg-undead (disabled)", "event rg-undead
  action.scope = CursedForest", "event rg-vengeance copied to rg-vengeance-dunley (disabled)", "event … enabled"); the
  action.scope set was sent seven times, each accepted
- [x] steps 3, 4, 5, 7, 11 (`event info`, `region list`, `region here`): not in the log; the owner confirmed on
  2026-09-28 that each showed its expected text ("trigger scope: Global" / "action scope: CursedForest" and the reverse
  for rg-vengeance; "FarbaneWoods (Farbane Woods): 2 events", "DunleyFarmlands (Dunley Farmlands): 1 events",
  "CursedForest (Cursed Forest): 1 events", then "global:"; "you are in DunleyFarmlands (Dunley Farmlands)", "you are in
  FarbaneWoods (Farbane Woods)", "you are in CursedForest (Cursed Forest)"). The api rows the owner pasted from the retest carry "region=FarbaneWoods" on rg-ambush and rg-bandit-fw, "region=CursedForest"
  on rg-undead and "region=-" on the trigger-scoped rg-vengeance pair (the row reports the action scope)
- [x] step 6, wave refused outside its regions: after step 5's "you are in DunleyFarmlands", "event rg-ambush not
  started by manual: your position is outside the event's regions"; a second refusal follows before step 7 (where the
  owner stood for it is unreported); the accepted start follows step 7's "you are in FarbaneWoods"
- [x] step 8, wave in its region: "event rg-ambush started by manual", "wave 1/3: 6 units queued (0 moved, 0 unchecked)",
  "spawn batch: 6 of 6 spawned"; stop → "despawn batch: 5 of 5 destroyed … 1 left", "1 of 1 destroyed … 0 left"
- [x] step 9, regional V Blood trigger: "trigger: VBloodKilled CHAR_Bandit_Foreman_VBlood", "event rg-vengeance started by
  VBloodKilled CHAR_Bandit_Foreman_VBlood"; rg-vengeance-dunley did not start; "sweep 328 applied, 11 skipped (dead 6
  vblood 5)", stop → "328 removed, 0 left to expire"
- [x] step 10, empowerment outside its regions: rg-undead (CursedForest) "query 193 of 193 faction entities", "sweep 0
  applied, 193 skipped (region 193)"; the Farbane skeleton rows "CHAR_Undead_SkeletonSoldier_Armored_Farbane native Undead
  … carrier none"
- [ ] steps 12–13 as written could not pass on the dev world: no Undead stood within 40 m of the owner at the Cursed
  Forest edge (the row showed only CHAR_Cursed_Bear_Standard and CHAR_Spider_Baneling), rg-undead's sweep skipped all
  193 queried Undead as "region" (outside the scope or unreadable), and Reference Data/unit_index.tsv names no ordinary
  Undead variant for the Cursed Forest (its only Cursed-named Undead are the Cursed Smith's floating weapons; the index
  has no placement field, so this is a naming read, not a placement proof). rg-undead had already been stopped before the
  owner reached the forest. Replaced by the retest below
- [x] retest, empowerment inside its regions: rg-bandit-fw (a copy of rg-vengeance, Manual, action.scope FarbaneWoods)
  started in Farbane → "query 338 of 338 faction entities", "sweep 200 applied, 138 skipped (region 136 vblood 2)";
  rows "CHAR_Bandit_Rascal native Bandits d 1m carrier rg-bandit-fw left 585s … mods PhysicalPower:MultiplyBaseAdd:0.3,
  PrimaryAttackSpeed:MultiplyBaseAdd:0.3,AbilityAttackSpeed:MultiplyBaseAdd:0.3 … pp 14.943 sp 11.494 aspd 1.3", Scouts
  likewise, Deer/Bear/Wolf "carrier none"; stop → "200 carriers queued for removal (stopped)", "200 removed, 0 left to
  expire", every row "carrier none" at base (pp 11.494, aspd 1). The status row right after the start read "units=123",
  the carriers applied so far by the paced sweep
- [x] tick timing (D12, Epic D24): every average under 5 ms (idle 0.04–0.09 ms); the sweep minutes avg 3.678 ms max 77.0 ms
  (the wave and Rufus kill), avg 2.540 ms max 89.9 ms (rg-undead's 193 region skips), avg 1.390 ms max 54.3 ms (rg-bandit-fw)
- [x] stopped after AutoSave_1765 (23:24:46), which follows the last step; both logs copied; -LogCheck "0 unhandled, 296 nyar
  lines, 0 orphan errors, 0 unity errors, regions 10 polygons"; BepInEx warnings: the known Il2CppInterop and two Beelzebub
  lines, example-empowerment's "pillar empowerment takes an Empower action", and our stop warnings; no [Error]; Unity log
  0 exceptions
- [x] `pwsh tools/dev-snapshot.ps1 -Restore` → "snapshot restored; hashes equal (rg1, … deleted)"; redeployed (Release
  build, 0 warnings)
