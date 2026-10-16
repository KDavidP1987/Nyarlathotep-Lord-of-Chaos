# Raphael handoff — wave variety (Nyarlathotep 0.13.0, still api 8)

This page is for a session working **in the Raphael workspace**. It covers what Nyarlathotep's **wave-variety** child
(test build `v0.13.0-rc.3`, release 0.13.0) changes for Raphael. Read it after `docs/RAPHAEL_HANDOFF_API8.md`, which
stays the main api 8 handoff, and with `docs/RAPHAEL_INTEGRATION_CONTRACT.md` (the source of truth for every line
shape). Feature doc: `docs/features/WAVE_VARIETY.md`.

> **Short version.** The wire does not change: still **api 8**, no new tag, key, push kind, command or reason code
> (contract §2, the wave-variety sentence). Raphael works with rc.3 as it is. Everything below adds things to the admin
> **field editor** and the **templates** panel. None of it is required for Raphael to keep working.

---

## 1. What wave variety adds on the server

| Feature | Definition key | What it does |
|---|---|---|
| **Pursuit** | `action.pursuit { "afterSeconds": 15-600 }` | Once no eligible player has been in the event's area (its arena, else its Hunt range) for that long, the waves go after the nearest eligible player anywhere on the map until the event ends. Claimed castle territory and PvP combat still protect a player. Needs `behaviour` Hunt; not with `fanOut` or `regionLock`. |
| **Unit pools** | `action.pools { "<name>": [1-30 CHAR_ prefabs] }`, 1-8 pools, names `a-z 0-9 -` up to 24 characters | Named groups of units. Every pool must be used by an entry or a roll. |
| **Pool entries** | `{ "pool": "<name>", "count": 1-50, "chance"?, "modifiers"? }` in `action.units` or `action.waveList.<n>.units` | Draws `count` units at random (with repeats) from the pool when the wave is decided. |
| **Boss rolls** | `{ "pool": "<name>", "min": 1-5, "max": min-5, "modifiers"? }` in `action.bosses` or `action.waveList.<n>.bosses` | Draws `min`-`max` distinct bosses from the pool. A wave's own `bosses` list (fixed or rolled) puts bosses mid-run, not only on the last wave. At most 5 bosses per wave. |

Each pool keeps to one allied group of units, so units of one wave never fight each other. The server refuses a
template that mixes groups.

**Fourteen new templates** (all off, `pillar=spawns`, Manual, started at the admin; 34 templates in all now):
- Timed (`<base>-timed`: a new wave every 75 s whether or not the last is cleared): `spider-swarm-timed`,
  `graveyard-waves-timed`, `militia-muster-timed`, `silverlight-crusade-timed`, `legion-incursion-timed`,
  `gloomrot-assault-timed`, `cursed-night-timed`.
- Onslaught (rolled waves, a boss mid-run and one or two at the end): `spider-onslaught`, `undead-onslaught`,
  `militia-onslaught`, `silverlight-onslaught`, `legion-onslaught`, `gloomrot-onslaught`, `cursed-onslaught`.
- All fourteen pursue after 60 s.

---

## 2. What reaches the wire (nothing new)

- **Rows and pushes:** pools, rolls and pursuit state never appear on `[NYAR:event]`, `[NYAR:def]`, `[NYAR:tpl]` or any
  push. A pursuing event is still `kind=waves`, `state=active`; its `wave=<n>/<N>` and admin `units=` count as before.
  Ending (all waves defeated, time up, stop, purge, pillar off) sends the usual `event-end`.
- **Templates:** `api templates` now pages through 34 rows (was 20). Page until `[NYAR:end]`, as today; never assume a
  page count.
- **Player names and positions** never reach the wire. The player that pursuers chase is never named or located on
  any row, push or line.
- **`plugin=0.13.0-rc.3`** in `api version`: still semver with a suffix (see the api 8 handoff §1). Gate on `api` only.
- **The degraded notice:** if the pursuit check fails repeatedly, the server's health set gains the entry
  `pursuit: check failing`. Admins see it in the existing chat notice (`nyar: degraded: …`). Show it as text; it is not
  a wire code.

---

## 3. What Raphael can add (optional, admin only)

### 3a. Field editor: four new fields

All are set with the existing admin twin `.nyar api event set <id> <field> <value>` (contract §5a). Two outcomes
matter for the editor:
- A value that does not parse (out of range, a bad token), or removing a pool an entry still names, is refused with an
  `[NYAR:err] cmd=event verb=set …` line (contract §5a codes). Nothing is saved.
- A value that parses is saved: `[NYAR:ok] cmd=event verb=set id=<id> field=<field> value=<stored value>` (cut to 120
  bytes). If the definition as a whole is not valid yet (a pool no entry uses, an entry naming a pool not made yet,
  pursuit without Hunt), the event is **disabled** with that reason. Re-read `api events` after a set and show the row's
  `reason` beside the editor, so the admin sees what is still missing; it clears once the definition is valid (then
  enable it).

Add the fields to the field picker. They are all action fields of a `spawns` (SpawnWaves) event,
and they come from design §6 (`docs/NYARLATHOTEP_DESIGN.md`).

| Field | Value | Notes for the editor |
|---|---|---|
| `action.pursuit` | `15`-`600` (seconds) or `none` | A number box with a "none" option. Without behaviour Hunt the value is saved but the event is disabled with "action.pursuit needs behaviour Hunt". |
| `action.pools.<name>` | `prefab,prefab,…`, or `+<prefab>` to add one, `-<prefab>` to remove one, or `none` to delete the pool | A pool of 30 names does not fit one 512-byte chat line, so **build big pools by appends**: send the first few names, then one `+<prefab>` per twin (queue them, one every 250 ms, as the api 8 budget says). A new pool and the entry that uses it take two sets, in either order; the event is disabled in between. Removing (`none`) a pool that an entry still names is refused; remove the entry first. |
| `action.bosses` and `action.waveList.<n>.bosses` | `entry,entry,…` or `none`; an entry is `<prefab>:<count>` (fixed) or `pool:<name>:<min>-<max>` (rolled) | Two kinds of entry in one list. `action.bosses` and the last wave's own list cannot both be set. |
| `pool:<name>:<count>` tokens | inside the existing `action.units` and `action.waveList.<n>.units` lists, beside `<prefab>:<count>` tokens | Offer "add from pool" next to "add unit" in the units list editor. |

The unit picker requested in the 2026-10-15 feedback (a list for unit-id fields) fits the pool members and fixed bosses
too. The unit names are the `CHAR_` prefab names the server already validates; an unknown or deny-listed one comes back
as `invalid` with its reason.

### 3b. Seeing the current values

api 8 still cannot read a definition's stored fields. The pools and pursuit appear in the human `.nyar event info <id>`
and `.nyar template info <id>` replies, so Raphael's existing "Show in chat" button covers them:
```
pools: troops (11 units): CHAR_Undead_ArmoredSkeletonCrossbow_Farbane, CHAR_Undead_SkeletonSoldier_Armored_Farbane, …
pursuit: after 60 s with no player within 40 m
```
Do not parse these lines (contract §5: human replies are not wire). Live-ops' planned `fields` read (contract §10.5)
is where a structured read will come.

### 3c. Templates panel

- Group the new templates in the browser: *Timed* (`-timed`) and *Onslaught* (`-onslaught`) next to the existing wave
  templates. The id suffix is a naming convention, not a wire field, so fall back to the plain list if it changes.
- "Create from template" works unchanged (`template use <tpl> as <id>`). Remind the admin that spawn templates need
  the Spawns pillar on; with it off, start fails with `code=disabled reason=pillar_off` (contract §5a, Raphael's
  pillar_off hint from the 2026-10-15 feedback).
- The templates start at the admin who starts them (`location Admin`), so "Start" must be pressed in game where the
  fight should happen.

### 3d. Player-facing

Nothing new to build. Players see the waves and the usual `event-start`, `wave-warn`, `wave` and `event-end` toasts.
Pursuit is deliberately silent on the wire; the units simply arrive. A toast such as "The waves are coming for you" is
**not** possible on api 8 (the server never says who is chased), and is not planned.

---

## 4. Testing against the rc.3 server

1. Install `v0.13.0-rc.3` (GitHub pre-release) on a test server; check `api version` → `api=8 plugin=0.13.0-rc.3`.
2. `api templates` → 34 rows over the pages; the 14 new ids are among them with `pillar=spawns`.
3. Field editor: on a copy of `undead-onslaught` (`template use undead-onslaught as wv-test`), set `action.pursuit 90`
   → `ok … value=90`; set `action.pursuit 5` → an `err` line, nothing saved. Set `action.pools.test
   CHAR_Undead_Priest` → `ok`, and `api events` shows `wv-test` disabled with "action.pools.test is not used"; set
   `action.pools.test +CHAR_Undead_Assassin` → `ok`; set `action.waveList.1.units pool:test:4` → the reason clears;
   `event enable wv-test`.
4. Start the copy in game and walk 60 m away: the status row stays `kind=waves state=active`, no new push kind
   arrives, and `event-end` follows the stop.

---

## 5. Open items for Raphael (from earlier feedback, unchanged here)

The requests R1-R3 of the 2026-10-15 feedback (a units-gone push, `action=apocalypse`, the pillar in `pillar_off`) stay
in contract §8 and §10.5/§10.7 for live-ops. Wave variety does not change them.
