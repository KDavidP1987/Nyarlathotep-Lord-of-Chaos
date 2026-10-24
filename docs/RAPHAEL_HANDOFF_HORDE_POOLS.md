# Raphael handoff — horde pools (Nyarlathotep 0.15.0, still api 8)

This page is for a session working **in the Raphael workspace**. It covers what Nyarlathotep 0.15.0 (the
**horde-pools** child) changes for Raphael, and what Raphael should change in its editor. Read it after
`docs/RAPHAEL_HANDOFF_UNDEAD_HORDES.md`. `docs/RAPHAEL_INTEGRATION_CONTRACT.md` is the source of truth for every line
shape. Feature doc: `docs/features/HORDE_POOLS.md`.

> **Short version.** The wire does not change: still **api 8**, with no new tag, key, push kind, command or reason code
> (contract §3, the horde-pools sentence). Raphael keeps working with 0.15.0 as it is. The one thing Raphael **must** do is
> keep keys it does not know when it saves an event back. Everything else below is optional and admin-only.

---

## 1. What 0.15.0 adds on the server

| Feature | Definition | What it does |
|---|---|---|
| **Full-size hordes** | none (the cfg) | A HordeMarch, Infestation or Apocalypse horde is held by `[Limits] MaxTrackedUnits` alone, never by `MaxUnitsPerWave`. `MaxTrackedUnits`' default is now 500 (range 1-500); a cfg that already holds a value keeps it. `MaxUnitsPerWave` (default 20) still holds SpawnWaves waves. |
| **The castle switch** | `"allowTerritory": true \| false` on an Apocalypse, Infestation or HordeMarch action (absent = false) | Off: the horde never targets a player on claimed castle ground, never moves or spawns there, a march stops at the castle's edge, and a start on claimed ground is refused. On: it may do all of these. SpawnWaves already had this key (0.7.0); it is unchanged there. |
| **The pool library** | `BepInEx/config/Nyarlathotep/pools.json` | Named unit pools kept apart from the events, like templates. Seeded once with 17 pools; never written again by the mod. An event may name a library pool anywhere it may name its own pool; the event's own pool of the same name wins. |
| **Random counts** | a unit pool entry `{ "pool": "<name>", "min": a, "max": b }` | On SpawnWaves (`units` and `waveList.<n>.units`) and HordeMarch `units`: each draw rolls a count in min..max, then draws that many members. Refused on Apocalypse and Infestation, where `count` is a draw weight. |
| **`.nyar pool`** | admin chat command | `list [page]`, `info <pool> [page]`, `reload`. A human command only: there is no api twin and no machine-readable pool read in api 8. |

### pools.json

```json
{
  "SchemaVersion": 1,
  "pools": {
    "undead-units":  [ "CHAR_Undead_SkeletonSoldier_Armored_Farbane", "CHAR_Undead_Ghoul", "…" ],
    "my-elite":      [ "CHAR_Bandit_Deadeye", "CHAR_Bandit_Thug" ]
  }
}
```

- 1-64 pools. A name is 1-24 characters of `a-z`, `0-9` and `-`. Each pool holds 1-30 distinct `CHAR_` prefabs, each
  known to the server and not deny-listed: the same rules as an event's own `pools`.
- The seed: `<group>-units` and `<group>-bosses` for the nine allied groups (`undead`, `spiders`, `bandits`, `militia`,
  `church`, `legion`, `gloomrot`, `cursed`, `blackfangs`, as in Logic/FactionGroups.cs), with no `bandits-bosses`: 17 pools.
- A pool whose members span two allied groups loads with the warning `pool <name> mixes groups: a, b`: those units
  would fight each other.
- An invalid file loads no library pool. Events that name a library pool then load disabled with
  `<at>.pool: no pool <name>`.

## 2. What Raphael's editor must accept (required)

1. **Keep unknown keys.** When the editor reads an event, changes some fields and saves it back (through `event set` or
   by writing the file), every key it does not show must survive unchanged. 0.15.0 adds `allowTerritory` on the three
   horde types and `min`/`max` on pool entries; a later release may add more.
2. **A pool name that is not in the event's own `pools`.** It names a library pool. The server checks it at load
   (`action.units.pool: no pool <name>` when neither holds it). The editor must not refuse such a name or drop the entry.
3. **A pool entry without `count`.** `{ "pool": "undead-units", "min": 5, "max": 20 }` is valid on SpawnWaves and HordeMarch.
4. **`allowTerritory` on the three horde types**, a bool.

## 3. What Raphael can add (optional, admin only)

### 3a. Field editor

Set through the existing admin twin `.nyar api event set <id> <field> <value>` (contract §5a). A bad value comes back as
`[NYAR:err] cmd=event verb=set …` and nothing is saved. A good value saves; if the definition is not yet valid, the
event is disabled with the reason: re-read `api events` and show the row's `reason`.

| Field | Types | Value | Notes for the editor |
|---|---|---|---|
| `action.allowTerritory` | Apocalypse, Infestation, HordeMarch (and SpawnWaves, as before) | `true` or `false` | `none` is refused, as on SpawnWaves. Show it as a checkbox "Castles may be entered", default off. |
| `action.units` | SpawnWaves, HordeMarch | a list of `CHAR_<name>[:<count>]`, `pool:<name>:<count>` or `pool:<name>:<min>-<max>`, comma separated | min 1-500, max min-500. Replaces the whole list. |
| `action.waveList.<n>.units` | SpawnWaves | the same tokens | One wave's list. |
| `action.units` | Apocalypse, Infestation | `pool:<name>:<count>` only | Here count is a weight: a range is refused. |

Load-time reasons the editor may show (from `api events`, row `reason`):

| Reason | When |
|---|---|
| `<at> takes count or min and max, not both` | an entry carries `count` and `min`/`max` |
| `<at>: min/max need a pool entry` | `min`/`max` on a prefab entry |
| `<at>.min must be 1-500` / `<at>.max must be min-500` | out of range, or max below min |
| `<at>: min/max apply to counted entries (SpawnWaves, HordeMarch); here count is a weight` | a range on Apocalypse or Infestation |
| `action.units must total 10-500 units for a HordeMarch` | the sum of the mins is under 10 or the sum of the maxes is over 500 |
| `action.allowTerritory must be true or false` | a non-bool value in the file |
| `<at>.pool: no pool <name>` | the name is in neither the event's pools nor the library |

Start refusals with the castle switch off (the reply to `api event start`): `start is inside a castle's territory; set
action.allowTerritory true to allow it` and `no hotspot: every hotspot is inside a castle's territory; set
action.allowTerritory true to allow it`.

### 3b. Pool picker

There is no machine-readable read of the library in api 8. Two options:

- Accept a typed pool name and let the server's load check answer (recommended for now).
- Run the human command `.nyar pool list` and show its reply lines as text. They are `<name>: <n> units, seeded|library`,
  10 a page, with a footer `page p/P; .nyar pool list p+1 for more`. Do not parse them as a contract: they may change.

A `pools` read is a candidate for a later api (contract §10).

### 3c. Caps shown in the editor

Raphael's note above 20 for `MaxUnitsPerWave` now applies to SpawnWaves only. For the three horde types, the cap that
matters is `MaxTrackedUnits` (default 500 on a new cfg).

## 4. How to check it

On a 0.15.0 server:
1. `.nyar pool list` shows 17 pools (on a server whose pools.json was seeded).
2. Save an event that has `"allowTerritory": true` through the editor; `.nyar event info <id>` still says
   "claimed territory allowed".
3. Save a SpawnWaves event with `pool:undead-units:5-20`; `.nyar event info <id>` shows `pool undead-units ×5-20`.
4. Edit any other field of an event that holds a key Raphael does not show; the key is still in events.json afterwards.
