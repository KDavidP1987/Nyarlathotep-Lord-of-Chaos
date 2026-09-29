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
