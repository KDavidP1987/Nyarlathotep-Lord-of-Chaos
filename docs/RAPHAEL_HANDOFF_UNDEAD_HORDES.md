# Raphael handoff — undead hordes (Nyarlathotep 0.14.0, still api 8)

This page is for a session working **in the Raphael workspace**. It covers what Nyarlathotep's **undead-hordes** child
(release 0.14.0) changes for Raphael. Read it after `docs/RAPHAEL_HANDOFF_API8.md` and
`docs/RAPHAEL_HANDOFF_WAVE_VARIETY.md`, with `docs/RAPHAEL_INTEGRATION_CONTRACT.md` as the source of truth for every
line shape. Feature doc: `docs/features/UNDEAD_HORDES.md`.

> **Short version.** The wire does not change: still **api 8**, with no new tag, key, push kind, command or reason code
> (contract §2, the undead-hordes sentence). Raphael works with 0.14.0 as it is. Everything below adds to the admin
> **field editor** and the **templates** panel, and none of it is required.

---

## 1. What undead hordes adds on the server

| Feature | Definition | What it does |
|---|---|---|
| **Wave destination** | `action.destination { "location": Point \| Admin, "radius": 10-100, "name" }` and `action.marchSpeed` 0.5-5.0 (default 2.0) on a SpawnWaves action | Each wave marches as one front from where it spawned to the destination, then takes its behaviour there. Its location must be a Point or Admin; refused with `fanOut`, `regionLock`, `arena` and `pursuit`. A wave a player has watched for 30 s in a row is *met* and fights where it stands. |
| **Infestation** | `"type": "Infestation"` | The faction's dead rise at `origins.count` (1-10) hotspots of its natives, each with at least `minNatives` (1-20), `minSpacing` (100-2000) m apart, `perOrigin` (1-100) units each. `hunters` (0-100) % of the living hunt the nearest player; the rest drift up to `driftRadius` (0-2000) m. Each kill adds a draw from its `spread` block. |
| **Horde march** | `"type": "HordeMarch"` | One horde (10-500 units, plus `bosses`, fixed or rolled) rises at `start` (a Point, `Admin`, or `Hotspot` with `minDistance` 100-3000) and marches to its `destination`. A kill on the way adds a `growth` draw. On arrival, after 120 s stuck, or once players have watched it for 30 s, it becomes an infestation there by its `infest` block. |
| **Spread blocks** | `spread`, `growth`, `infest.spread`: `{ "perKillMin": 0-3, "perKillMax": 0-3, "pool", "max": 1-500, "perMinute" }` | A uniform draw of min..max units from the named pool per kill, within `max` alive and `perMinute`. The apocalypse's old `"perKill": n` still reads as n..n. |
| **Raised ranges** | `roamers.max` 1-500, `hordes.size` 5-500, `growth` 0-100 | The cfg's `MaxTrackedUnits` stays capped at 500 (the load test: 250 units ran clean, 500 dropped to about 19 fps with a player among them). |

**Caps.** `[Limits] MaxUnitsPerWave` now goes to 500, like `MaxTrackedUnits` (defaults stay 20 and 150). A start that
a cap cuts says so in the human reply and in `.nyar event info`: "capped: <n> of <m> by <key> <value>; raise it in the
cfg". It is text, not a wire field; the api reply is unchanged. The `[Limits]` values are read when the server starts,
so a cfg editor in Raphael should tell the admin to restart after changing them.

One apocalypse-type event runs at a time (Apocalypse, Infestation or HordeMarch). A second start is refused with
"an apocalypse-type event is running: <name>". Destination waves can run alongside them.

**Two new templates** (off, `pillar=spawns`, Manual; 36 templates in all): `undead-infestation` and
`undead-horde-march` (200 units, 160 troops at level 20 with 32 elite at 40, 8 strong at 60 and the Bishop of Death, marching to the
starting admin). `zombie-apocalypse` is kept and made bigger.

---

## 2. What reaches the wire (nothing new)

- **Rows:** an infestation or a horde march has an `[NYAR:event]` row `kind=waves`, `state=active`, like the
  apocalypse. Its origins, fronts and destination reach no row, push or read beyond what api 8 already sends admins.
- **Pushes:** the usual `event-start` / `event-end`. The new banners ("The dead rise in <n> places: <event>.", "The
  horde has reached <name>!") are ordinary announcement lines, not push kinds.
- **Templates:** `api templates` now pages through 36 rows (was 34). Page until `[NYAR:end]`.
- **Player names and positions** never reach the wire or a log line. No new log line carries a position.

---

## 3. What Raphael can add (optional, admin only)

### 3a. Field editor

Set through the existing admin twin `.nyar api event set <id> <field> <value>` (contract §5a). A bad value comes back
as `[NYAR:err] cmd=event verb=set …` and nothing is saved. A good value saves, and if the definition is not yet valid
the event is disabled with the reason: re-read `api events` and show the row's `reason`, as for wave variety.

**SpawnWaves (destination waves):**

| Field | Value | Notes for the editor |
|---|---|---|
| `action.destination` | `here` (the admin's position, as a Point) or `none` | `none` also removes `marchSpeed`. Set the destination before its radius or name: they are refused without one. |
| `action.destination.radius` | `10`-`100` | Metres. |
| `action.destination.name` | plain text, up to 40 characters | Shown in the arrival lines. |
| `action.marchSpeed` | `0.5`-`5.0` | Metres a second. |

**Infestation:**

| Field | Value |
|---|---|
| `action.origins.count` | `1`-`10` |
| `action.origins.minNatives` | `1`-`20` |
| `action.origins.minSpacing` | `100`-`2000` |
| `action.origins.perOrigin` | `1`-`100` |
| `action.hunters` | `0`-`100` (percent) |
| `action.driftRadius` | `0`-`2000` |
| `action.spread.perKillMin` / `.perKillMax` | `0`-`3` (max not below min; the validator checks) |
| `action.spread.max` | `1`-`500` |
| `action.spread.perMinute` | `1`-`30` |
| `action.spread.pool` | a pool name, or `none` |

**HordeMarch:**

| Field | Value |
|---|---|
| `action.start` | `here` (the admin's position) |
| `action.start.minDistance` | `100`-`3000` (a Hotspot start) |
| `action.growth.*` | the same five keys and ranges as `action.spread.*` |
| `action.infest.hunters` / `action.infest.driftRadius` | as `action.hunters` / `action.driftRadius` |
| `action.infest.spread.*` | the same five keys as `action.spread.*` |
| `action.destination`, `.radius`, `.name`, `action.marchSpeed` | as for destination waves |
| `action.bosses` | fixed or rolled entries, as in wave variety |

Pools and pool entries work as in wave variety (`action.pools.<name>`, `pool:<name>:<count>`).

**Hints worth showing beside the fields** (found in game, 0.14.0):

- A horde march's `growth.max` caps the horde's living count, so a max at or below `size` means it never grows. The
  shipped template has size 200 and growth max 400.
- Kills, spread and growth happen only near players: the game stops its own NPCs 80 m or more from every player.
- Hunters jump in straight lines, so a player behind a river or a cliff band is reached by fewer of them. A castle
  territory is a refuge.

### 3b. Seeing the current values

`.nyar event info <id>` shows each new block on one line (origins, hunters and drift, spread, start, growth, infest).
A running infestation's `.nyar status` line reads "<event>: <n> undead, <k> places". A horde march's reads
"<event>: horde <n> units, <d> m to <destination>", "… arrived", or "the horde is gathering" before it rises. Show them as text; they are not wire codes.

### 3c. Commands

No new command. Every new field goes through `event set`, the existing gateway.
