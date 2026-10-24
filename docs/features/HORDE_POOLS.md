# Horde pools — full-size hordes, the castle switch, a library of faction pools

**Status:** in build (docs/dod/horde-pools.md, audit docs/audits/horde-pools.md; design §9 D65, Epic A49). Step 1
(caps and the castle switch) is built; step 2 (the pool library, random counts and `.nyar pool`) and the in-game
session follow.

## Goal

The owner asked for three things (2026-10-24):

1. A horde rises with every unit its event asks for, not 20 or 30 at a time.
2. A horde stays out of castles unless the admin allows it, per event, off by default.
3. Pools of units that live apart from the events, like templates: one for each faction's units and one for its
   bosses, which any event can name, with a random count drawn from a pool.

## Design summary

- **Caps.** The three horde types (HordeMarch, Infestation, Apocalypse) are held by `[Limits] MaxTrackedUnits` alone,
  never by `MaxUnitsPerWave`; a SpawnWaves wave is still held by both. MaxTrackedUnits' default is now 500 (range
  1-500; the undead-hordes load ladder's ceiling). An existing cfg keeps the value written in it, so a server upgraded
  from 0.14.0 still reads 150 until the admin raises it. A cut on a horde names MaxTrackedUnits ("capped: n of m by
  MaxTrackedUnits 500; raise it in the cfg"). The boot log prints the loaded caps: `[nyar] limits: MaxTrackedUnits …,
  MaxUnitsPerWave …, MaxSpawnsPerTick …, MaxDespawnsPerTick …`. One 500-unit horde takes every slot while it lives
  (S-11).
- **The castle switch.** `"allowTerritory": true | false` on a HordeMarch, Infestation or Apocalypse action, absent =
  false. Off:
  - no hunter, roamer or hunt seed targets a player on claimed territory;
  - no spawn, jump, hop or drift lands on a claimed block;
  - a march stops at the castle's edge (its own point is never claimed, and a crossing ends at claimed ground) and,
    after the 120 s stall, arrives and infests where it stands;
  - a Point or Admin march start on claimed ground is refused: "start is inside a castle's territory; set
    action.allowTerritory true to allow it";
  - a hotspot or infestation origin on claimed ground is skipped, logged by count ("apocalypse <id>: <n> hotspots
    skipped inside a castle's territory"); none left refuses the start with "no hotspot: every hotspot is inside a
    castle's territory; set action.allowTerritory true to allow it".

  On: castle players are targets of the roam and the hunt layer, units move, spawn and land (the unit watch's recovery
  included) on claimed ground, and a march walks into the castle to its destination. Chat: `.nyar event set <id>
  action.allowTerritory true|false` (none is refused, as on SpawnWaves; plan A1). `event info` adds "claimed territory
  allowed" when on. With the switch off, the game's own AI may still chase a unit into a castle after a fight starts
  (S-6). The claimed map is built at the start and at a resume, so a castle claimed later is not in it (S-12).
- **Pool library** (step 2): `pools.json` in the config folder, seeded once with 17 pools named `<group>-units` and
  `<group>-bosses` from the nine faction groups (no `bandits-bosses`), never overwritten, edited by hand and reloaded
  with `.nyar pool reload`. An event's own pool of the same name wins. `min`/`max` on a SpawnWaves or HordeMarch pool
  entry rolls the count.

## Open questions

- None open.

## Test results

- **Step 1 (Logic and Services, no in-game run):** `dotnet test` Passed! 4058/4058; HordePoolsTests Caps, TerritoryOff and
  TerritoryOn, LimitsTests HordeDefaults; planted faults red and restored (the plan's Log).
