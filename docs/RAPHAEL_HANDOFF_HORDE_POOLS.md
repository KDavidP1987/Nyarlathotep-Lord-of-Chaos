# Raphael handoff — horde pools (Nyarlathotep 0.15.0, still api 8)

This page is for a session working **in the Raphael workspace**. It covers what Nyarlathotep's **horde-pools** child
(release 0.15.0) changes for Raphael. Read it after `docs/RAPHAEL_HANDOFF_UNDEAD_HORDES.md`, with
`docs/RAPHAEL_INTEGRATION_CONTRACT.md` as the source of truth for every line shape. Feature doc:
`docs/features/HORDE_POOLS.md`.

> **Short version.** The wire does not change: still **api 8**, with no new tag, key, push kind, command or reason code
> (contract §3, the horde-pools sentence). Raphael works with 0.15.0 as it is. The one thing Raphael **must** do is keep
> keys it does not know when it saves an event back; everything else below is optional.

---

## 1. What horde pools adds on the server

| Feature | Definition | What it does |
|---|---|---|
| **Full-size hordes** | none (the cfg) | A HordeMarch, Infestation or Apocalypse horde is held by `[Limits] MaxTrackedUnits` alone, never by `MaxUnitsPerWave`. MaxTrackedUnits' default is now 500; a cfg that already holds a value keeps it. |
| **The castle switch** | `"allowTerritory": true \| false` on an Apocalypse, Infestation or HordeMarch action (absent = false) | Off: the horde never targets a player on claimed castle ground, never moves or spawns there, and a march stops at the castle's edge. On: it may do all of these. |
| **The pool library** | `BepInEx/config/Nyarlathotep/pools.json`: `{ "SchemaVersion": 1, "pools": { "<name>": [CHAR_ names] } }`, 1-64 pools | Pools kept apart from the events, like templates. Seeded once with 17 pools, `<group>-units` and `<group>-bosses` (no `bandits-bosses`), for the nine allied groups; never written again by the mod. An event's pool entry, boss roll or spread pool may name a library pool; the event's own pool of the same name wins. |
| **Random counts** | a unit pool entry `{ "pool": "<name>", "min": 1-500, "max": min-500 }` in a SpawnWaves or HordeMarch action | Each draw rolls a count in min..max, then draws that many members. Refused in an Apocalypse or Infestation, where `count` is a draw weight. A horde's maxes stay within 500 and its mins reach 10. |
| **`.nyar pool`** | admin chat command | `list [page]`, `info <pool> [page]` (members, the events that name it, a "mixes groups" warning), `reload`. A human command only, with no api twin. |

## 2. What Raphael's editor must accept

1. **Keep unknown keys.** When the editor reads an event, changes some fields and saves it back (through `event set` or
   by writing the file), every key it does not show must survive unchanged. 0.15.0 adds `allowTerritory` on the three
   horde types and `min`/`max` on pool entries; a later release may add more.
2. **A pool name that is not in the event's own `pools`.** It names a library pool; the server checks it on reload
   ("action.units.pool: no pool <name>" when neither holds it). The editor must not refuse it or drop the entry.
3. **A pool entry without `count`.** `{ "pool": "undead-units", "min": 5, "max": 20 }` is valid on SpawnWaves and
   HordeMarch. The chat form is `pool:<name>:<min>-<max>` in a units list (`pool:<name>:<count>` still works).
4. **`allowTerritory` on the three horde types**, a bool. The chat form is `.nyar event set <id> action.allowTerritory
   true|false`; `none` is refused, as on SpawnWaves.

## 3. What Raphael can add (optional, admin only)

- A min..max editor for pool entries on SpawnWaves and HordeMarch, and a castle switch on the horde types.
- A pool picker that offers the library's names. There is no read for them in api 8: the admin runs `.nyar pool list`
  in chat, or the editor accepts a typed name. A `pools` read is a candidate for a later api (contract §10).

## 4. How to check it

On a 0.15.0 server: `.nyar pool list` shows 17 pools; save an event that has `"allowTerritory": true` through the
editor and confirm `.nyar event info <id>` still says "claimed territory allowed".
