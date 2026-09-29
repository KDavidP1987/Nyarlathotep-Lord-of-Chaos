# Nyarlathotep ↔ Raphael — integration contract

> **Direction:** this is the seam between **Nyarlathotep** (server) and **Raphael, Lord of Wisdom** (client
> UI). It is a living contract. When Nyarlathotep changes anything Raphael-facing, this file changes in the
> same commit (§7) and a copy goes to the Raphael workspace. Raphael already speaks the same dialect with
> Beelzebub, Uriel and Faust (`[MOD:*]` wire lines, a version handshake, paging, `err` lines), so it needs a
> `Nyar` service set and a tab group, not new machinery (§6).
>
> **Status of every section:** each command and line shape is marked **PLANNED (api N)** or
> **IMPLEMENTED (api N)**. Build against IMPLEMENTED only. A PLANNED shape can still change before it ships;
> once it is IMPLEMENTED it only grows (§7).
>
> **Current api:** 6
>
> api 1 shipped with the `foundation` release (0.2.0): the handshake. api 2 ships with the `raphael-api-core`
> release (0.3.0): `status`, `events` and the push subscription. `me`, `top` and `zones` stay PLANNED until the
> child that fills them (stats, defended-zones) ships; each bumps the api (the Epic plan, `docs/dod/nyarlathotep.md`, A20).
> api 3 ships with the `faction-empowerment` release (0.4.0): `status` rows of `kind=empower` (§3). It adds no tag or
> key; the change log (§9) lists every api.
>
> api 4 ships with the `raphael-api-admin` release (0.5.2): a wire twin for every admin action (§5a), the
> `templates`, `template info`, `pillar list` and `killswitch` reads (§3) and seven error codes (§4).
>
> api 5 ships with the `regions` release (0.6.0): the `regions` read and a `region=` key on `[NYAR:def]` and
> `[NYAR:event]` rows and on `event-start` and `event-end` pushes (§3).
>
> api 6 ships with the `automation` release (0.8.0): three new values of `trigger` on `[NYAR:def]` rows, `interval`,
> `regionentered` and `factionkills` (§3). It adds no tag, key, push kind or command; a fanned-out wave still sends one
> `wave` push. An api 5 client ignores no key here but may show such an event's trigger wrongly. api 7 and later are
> PLANNED in §10, the rows each later child adds. Nothing in §10 is sent yet.

### Tags and commands

The code is checked against this table (§7): every tag the server builds and every `.nyar api` command it answers
is listed here as IMPLEMENTED with the api that added it.

| Tag or command | Kind | Status | Api |
|---|---|---|---|
| `version` | tag | IMPLEMENTED | 1 |
| `event` | tag | IMPLEMENTED | 2 |
| `def` | tag | IMPLEMENTED | 2 |
| `end` | tag | IMPLEMENTED | 2 |
| `err` | tag | IMPLEMENTED | 2 |
| `ok` | tag | IMPLEMENTED | 2 |
| `ev` | tag | IMPLEMENTED | 2 |
| `me` | tag | PLANNED (stats) | — |
| `top` | tag | PLANNED (stats) | — |
| `zone` | tag | PLANNED (defended-zones) | — |
| `tpl` | tag | IMPLEMENTED | 4 |
| `pillar` | tag | IMPLEMENTED | 4 |
| `ks` | tag | IMPLEMENTED | 4 |
| `region` | tag | IMPLEMENTED | 5 |
| `version` | command | IMPLEMENTED | 1 |
| `status` | command | IMPLEMENTED | 2 |
| `events` | command | IMPLEMENTED | 2 |
| `sub` | command | IMPLEMENTED | 2 |
| `me` | command | PLANNED (stats) | — |
| `top` | command | PLANNED (stats) | — |
| `zones` | command | PLANNED (defended-zones) | — |
| `event` | command | IMPLEMENTED | 4 |
| `template` | command | IMPLEMENTED | 4 |
| `templates` | command | IMPLEMENTED | 4 |
| `pillar` | command | IMPLEMENTED | 4 |
| `purge` | command | IMPLEMENTED | 4 |
| `killswitch` | command | IMPLEMENTED | 4 |
| `regions` | command | IMPLEMENTED | 5 |

`zone` and `zones` keep their defended-zones label: the anti-farming child absorbs defended-zones (Epic, 2026-09-28)
and relabels both rows when it is planned (§10.4).

---

## 1. Transport

- Raphael sends `.nyar …` commands by silently injecting a chat message (`MessageService.EnqueueMessageSilent`),
  the path it already uses for `.faust` / `.uriel` / `.beelz`.
- Nyarlathotep answers with VCF `ctx.Reply` System messages. Wire lines start with `[NYAR:`, so Raphael
  routes them in `ClientChatPatch` to `NyarProtocolService.HandleLine` and removes them from the visible chat.
- **One `ctx.Reply` per wire line**, never joined with `\n`. A paged answer is N row replies followed by a
  `[NYAR:end]` reply (the #1 Uriel integration bug).
- **Line grammar.** `[NYAR:<tag>]`, then space-separated `key=value` tokens. Split on spaces, then on the first `=`.
  - Values never contain a space, `=`, `;` or `:`. Spaces become `_`, and Raphael turns `_` back into a space
    for display.
  - Lists are comma-separated without spaces.
  - `-` means unknown or none; `0`/`1` are booleans.
  - Numbers are bare: seconds, not `5m`.
  - Unknown keys are ignored, so a line may grow without breaking an older Raphael.
  - A value outside the set §3 lists for a key (a `kind` or `state` the client does not render) never breaks
    parsing: the client shows the row with its generic fields or skips it. Every value of an enumerated key is
    listed in §3 before the server sends it; `kind=empower` has been listed since api 2 and is first sent in api 3.
- **At most 480 bytes per line** (under the 510-byte `FixedString512Bytes` limit). Wire lines carry **no colour
  tags**.
- The human replies (`.nyar status`, `.nyar top`, …) are **not** a wire. They are for people, may change
  wording, and Raphael should not parse them. Every piece of data Raphael needs has an `api` twin.
- **Privacy is enforced on the server, not the client:**
  - No wire line sent to a non-admin carries coordinates, zone radii or another player's position.
  - Player names appear only on `top` rows and the requester's own `me` rows.
  - Hidden players (`.nyar stats hide`) and ignored players never appear on `top`.

---

## 2. Handshake — `.nyar api version` — IMPLEMENTED (api 1, Nyarlathotep 0.2.0)

Raphael probes it on login with its usual back-off (every 4 s, up to 12 attempts) and shows the NYAR tab group
only after an answer with `ready=1`. Before the server world has loaded, the command replies the plain line
`still loading` like every `.nyar` command; treat it as no answer and probe again. The answer is one line (wrapped here for reading):

```
[NYAR:version] api=2 plugin=0.3.0 ready=1 admin=0 enabled=1 killswitch=0
  empower=0 waves=0 boss=0 zones=0 sieges=0 stats=0 annwarn=0 annbanner=0 anndaily=0 annlogin=0 annshare=0
```

| Key | Meaning |
|---|---|
| `api` | Integer contract version. Raphael gates newer UI on `api >= N`. |
| `plugin` | Nyarlathotep's version (semver). |
| `ready` | `1` once the server world has loaded (`Core.IsReady`). `0` → keep the group greyed and re-probe. |
| `admin` | `1` when the **server** considers the requester an admin. Use it for UI gating only; the server still checks every command. |
| `enabled` | `General.Enabled` in the cfg. `0` means the whole mod is off. |
| `killswitch` | `1` while the post-purge cooldown suppresses scheduled starts. |
| `empower waves boss zones sieges` | Each pillar switch (`0`/`1`). Always off in a fresh install. |
| `stats` | `1` when stats are collected, which enables `me`/`top`. |
| `annwarn annbanner anndaily annlogin annshare` | The five announcement switches: wave warnings, event banners, daily banner, login stats and player share. `annshare=1` means the Share button works. |

The switches live in `BepInEx/config/kdpen.Nyarlathotep.cfg`. The five pillar switches change with `.nyar pillar`
and its api 4 twin (§5a); the others only by editing the cfg.

---

## 3. Reads

All live under `.nyar api …`. Paged commands take an optional 1-based `[page]` (§4).

### `status` — active events (anyone) — IMPLEMENTED (api 3)
`.nyar api status` sends one row per active event, then `[NYAR:end] cmd=status count=<n>` (unpaged):
```
[NYAR:event] id=ashfall kind=waves name=Ashfall_Raid state=active faction=Undead left=412 wave=2/3 units=18 region=-
```
- `kind` ∈ `empower | waves | boss | zone | siege`.
- `state` ∈ `scheduled | active | ending`. api 2 sends `active`, and `ending` for an event that has ended while its
  units wait out the grace (`left` = seconds until they despawn, `wave=-`).
- `faction` is `-` for waves events; `wave` is `<spawned>/<total>`.
- An empower row (api 3) carries `faction=<names joined by ','>`: each faction of the event without its `Faction_`
  prefix, e.g. `faction=Legion,Bandits`; its `wave` is `-`, and its admin `units` is the number of NPCs holding the
  event's empowerment. An empower event has no `ending` row.
```
[NYAR:event] id=legion-surge kind=empower name=Legion_Surge state=active faction=Legion,Bandits left=1500 wave=- units=42 region=CursedForest,FarbaneWoods
```
- `left` is the number of seconds left.
- `units` is sent to admins only; players get `units=-`.
- `region` (api 5) is the event's action scope, the region names joined by `,` as `api regions` prints them, or `-`
  for a global event or an ending row whose definition is gone. It names the event's configured regions, never where
  a player is.
- A siege row goes only to members of the target clan and to admins.
- No row ever carries a position.

### `me` — the requester's own stats (anyone, when `stats=1`) — PLANNED (stats)
`.nyar api me` sends three rows (windows `today`, `week`, `all`), then `[NYAR:end] cmd=me count=<n>` (unpaged):
```
[NYAR:me] window=week name=Vlad kills=57 events=6 waves=11 defences=1 bossadds=4 deaths=2 hidden=0
```
- `hidden=1` means the player ran `.nyar stats hide`. They still see their own numbers.
- A player with no counted activity gets one row: `[NYAR:me] window=all none=1`.

### `top` — leaderboards (anyone, when `stats=1`) — PLANNED (stats)
`.nyar api top <stat> <window> [page]` sends up to 10 rows, then
`[NYAR:end] cmd=top stat=<stat> window=<window> page=<cur>/<total> count=<n>`:
```
[NYAR:top] stat=kills window=week rank=1 name=Vlad value=57 self=0
```
- `stat` ∈ `kills | events | waves | defences | bossadds | deaths`.
- `window` ∈ `today | week | all`.
- `self=1` marks the requester's own row.
- Ties are ordered by name, and the ranks follow that order.
- Admins are left out unless `Stats.IncludeAdmins` is set; hidden and ignored players are always left out.
- Sharing uses the human command `.nyar top <stat> <window> share` (§5), whose limits answer
  `[NYAR:err] code=ratelimit`.

### `events` — definitions (admin) — IMPLEMENTED (api 2)
`.nyar api events [page]` sends up to 10 rows, then `[NYAR:end] cmd=events page= count=`:
```
[NYAR:def] id=ashfall name=Ashfall_Raid enabled=1 trigger=schedule action=waves duration=900 state=idle reason=- region=-
```
- `trigger` ∈ `schedule | ingame | vbloodkilled | interval | regionentered | factionkills | bossengaged | bosshealth |
  zoneactivity | manual`; `interval`, `regionentered` and `factionkills` from api 6.
- `action` ∈ `empower | waves | boss | siege`: the definition's pillar action, sent even when validation disabled a
  definition for a missing action.
- `state` ∈ `idle | scheduled | active | disabled`.
- `reason` is the wire-safe validation error, or `disabled` for a definition switched off, when `state=disabled`;
  `-` otherwise.
- `name` is cut to 64 UTF-8 bytes and `reason` to 120, on a character boundary, so every key fits the line.
- `region` (api 5) is the definition's action scope, as on `status` rows; a new last key, so an api 4 parser skips it (§7).

### `regions` — the map's regions (anyone) — IMPLEMENTED (api 5)
`.nyar api regions [page]` sends one row per region, in the game's order, then `[NYAR:end] cmd=regions page= count=`:
```
[NYAR:region] id=CursedForest events=1
```
- `id` is the game's region name, as `.nyar region list` prints it: `StartCave`, `FarbaneWoods`, `DunleyFarmlands`,
  `CursedForest`, `HallowedMountains`, `SilverlightHills`, `Gloomrot_South`, `Gloomrot_North`, `RuinsOfMortium`,
  `Strongblade` at game 1.1.12.
- `events` counts the active events whose action scope names the region. The row names no player and no position.
- It changes nothing and is not rate-limited.

### `zones` — defended zones (admin) — PLANNED (defended-zones)
`.nyar api zones [page]` sends up to 10 rows, then `[NYAR:end] cmd=zones page= count=`:
```
[NYAR:zone] name=Dunley_Gate x=-1520.5 z=-460.0 r=40 threshold=3 cooldown=600 state=idle
```
- `state` ∈ `idle | alert | cooldown`. Coordinates are sent to admins only.

### `templates` and `template info` — the template catalogue (admin) — IMPLEMENTED (api 4)
`.nyar api templates [pillar] [page]` sends up to 10 rows, then `[NYAR:end] cmd=templates page= count=`; one word is
a page when it is all digits, else a pillar name. `.nyar api template info <template>` sends the one row, then
`[NYAR:end] cmd=template count=1`:
```
[NYAR:tpl] id=undead-nightfall pillar=spawns trigger=ingame duration=1800 summary=Undead_rise_at_night
```
- `pillar` ∈ `empowerment | spawns | boss | zones | sieges`; `trigger` takes the values of `events`' `trigger`.
- `summary` is the template's name, cut to 120 UTF-8 bytes on a character boundary.
- An unknown pillar answers `[NYAR:err] cmd=templates code=notfound arg=pillar`, an unknown template
  `[NYAR:err] cmd=template code=notfound arg=template`, and a bad page `code=badarg arg=page`.
- While the template catalogue is unavailable (it failed to load at boot), both answer `code=io reason=read`.
```
[NYAR:err] cmd=templates code=io reason=read
```

### `pillar list` — the pillar switches (admin) — IMPLEMENTED (api 4)
`.nyar api pillar list` sends five rows, in this order, then `[NYAR:end] cmd=pillar count=5`:
```
[NYAR:pillar] id=empowerment on=1
```
- `id` ∈ `empowerment | spawns | boss | zones | sieges`; `on` is the switch as the cfg holds it.

### `killswitch` — the purge cooldown (admin) — IMPLEMENTED (api 4)
`.nyar api killswitch` sends one row, then `[NYAR:end] cmd=killswitch count=1`:
```
[NYAR:ks] on=1 secs=240 events=0 units=12
```
- `on=1` while the purge cooldown runs, and `secs` is its time left, rounded up; otherwise `on=0 secs=0`.
- `events` is the active events and `units` the tracked units.

These four reads change nothing and are not rate-limited (§5a's rate counts twins only).

### Push events — `.nyar api sub on|off` (anyone) — IMPLEMENTED (api 2)
After `sub on`, the server pushes lines to that player until `sub off`, a disconnect or a server restart. Subscriptions live only in memory: a reconnect starts unsubscribed, so Raphael sends `sub on` again after every successful handshake. Lines go only to connected subscribers:
```
[NYAR:ev] type=wave-warn id=ashfall secs=60 wave=2
```
- `type` ∈ `event-start | event-end | wave-warn | wave | killswitch | config-changed`.
- `id` names the event; it is `-` for `killswitch` and `config-changed`.
- `secs` is the event's length for `event-start`, the time until the wave for `wave-warn`, the purge cooldown for
  `killswitch`, and 0 for `event-end`, `wave` and `config-changed`. `wave=<n>` follows it on `wave` and `wave-warn`.
- From api 5, `region=<id,…>` follows `secs` on `event-start` and `event-end`: the event's action scope as on
  `status` rows, or `-` for a global event.
```
[NYAR:ev] type=event-start id=undead-nightfall secs=1800 region=CursedForest
```
- `config-changed` asks Raphael to re-read `version` and `events`; it has no other payload. It follows every applied
  events.json write and, from api 4, every pillar switch that changed the cfg, from a human command or a twin alike.
- Siege events go only to subscribers who are members of the target clan, and to admins.
- The subscribe command answers `[NYAR:ok] cmd=sub on=1` (`on=0` for `sub off`); any other argument answers
  `[NYAR:err] cmd=sub code=badarg arg=state`. At most 128 players are subscribed at once; a new `sub on` past that
  answers `[NYAR:err] cmd=sub code=ratelimit`.
- **Fairness rule:** a push never tells a subscriber more than chat or `.nyar status` tells every player. `wave-warn`
  is pushed only when the chat warning would fire: `WaveWarnings` on, the event's `announce.warnings` true, at the
  `WarningOffsets`. event-start, event-end, wave, killswitch and config-changed always push.
- Push lines are queued (50 at most, oldest dropped) and sent 5 per server tick; a dropped line is recovered by
  re-reading `api status` on an `[NYAR:ev]` naming an unknown event, and after every event-end and killswitch.

---

## 4. Paging, acknowledgements and errors — IMPLEMENTED (api 2)

- **Paging:** 1-based, 10 rows per page.
  - Every paged answer ends with `[NYAR:end] cmd=<cmd> page=<cur>/<total> count=<rows in all pages>`.
  - An empty result still sends `[NYAR:end] cmd=<cmd> page=1/1 count=0`.
  - No page given means page 1. A page below 1 or not a number answers `[NYAR:err] cmd=<cmd> code=badarg arg=page`.
  - A page past the last sends no rows, then `[NYAR:end] cmd=<cmd> page=<asked>/<total> count=<n>`.
- **Unpaged reads** (`status`, `me`) end with `[NYAR:end] cmd=<cmd> count=<rows sent>` and take no page.
  - Raphael asks for `cur+1` until `cur == total`.
- **Acknowledgement:** `[NYAR:ok] cmd=<cmd> …` for `api` commands that change nothing but a subscription.
- **Errors:** `[NYAR:err] cmd=<cmd> code=<code> [secs=<n>] [arg=<name>] [reason=<word>]`; a twin's error carries
  `verb=<verb>` after `cmd` (§5a). `reason` (api 4) is one wire-safe word from §5a's list. The codes:

| Code | Meaning |
|---|---|
| `notready` | Reserved, never sent (api 2 to 4): before the world is ready every command replies the plain line `still loading`, and no player can connect before then. |
| `noaccess` | Reserved. Admin-only commands are refused by VCF before the mod runs, with VCF's own human-readable line, so Raphael shows admin panels only when `version` says `admin=1`. |
| `disabled` | The pillar, the stats or the mod is switched off. |
| `notfound` | Unknown event id, zone or stat. |
| `badarg` | Bad argument; `arg` names it. |
| `ratelimit` | A share limit was hit, or an admin sent more than 5 twins in a second (§5a); `secs` is the time until the next is allowed. |
| `cooldown` | The kill-switch cooldown is active; `secs` is the time left. |
| `exists` | (api 4) `new`, `copy` or `template use` named an id that is taken; `arg` is `id` or `newId`. |
| `state` | (api 4) The event or the kill switch is in the wrong state: `start` on an active event (`reason=already_active`), `stop` on one that is not active (`not_active`), `delete` of a running event (`running`), a start on a disabled event (`disabled`), a clashing empowerment (`empower_clash`), or a purge with nothing to purge (`nothing_to_purge`). |
| `invalid` | (api 4) The change would fail validation, or the template is invalid; `arg` names the field or `template` and `reason` the validator's word. |
| `full` | (api 4) The definitions file holds its 200 definitions (`reason=count`) or would pass 1 MB (`reason=size`). |
| `io` | (api 4) The definitions file, the cfg or the template catalogue could not be read, parsed or saved, or the twin failed on the server; `reason` ∈ `read`, `parse`, `save`, `stale`, `read_only`, `write_uncertain`, `internal`. A refusal changes nothing on the server except what a file now holds after a failed save and the events a pillar `off` ends (§5a). `internal` may leave the action partly or fully applied; re-read state (§5a). |
| `confirm` | (api 4) A confirm arrived with no ask from the same admin in the last 30 s. |
| `limit` | (api 4) A manual start was skipped because `MaxConcurrentEvents` events are running (`reason=max_concurrent`). |

Examples — the end of page 1 of 3 of a 24-row read, the end of an unpaged read, a bad page, an error with a wait
(`secs` comes before `arg` when both are sent) and a subscription:
```
[NYAR:end] cmd=events page=1/3 count=24
[NYAR:end] cmd=status count=2
[NYAR:err] cmd=events code=badarg arg=page
[NYAR:err] cmd=top code=ratelimit secs=45
[NYAR:ok] cmd=sub on=1
```

---

## 5. Admin actions and player actions (human commands, not wire)

When the handshake reports `api>=4`, Raphael sends the admin actions as their §5a wire twins and reads the one
`[NYAR:ok]` or `[NYAR:err]` line each answers. Against an api 1–3 server, and for the actions that have no twin yet,
Raphael sends the **human** commands below and then re-reads state (`api events`, `api status`, or waiting for
`[NYAR:ev] type=config-changed`); it does not parse the human replies. Both paths run the same flow, which
`docs/NYARLATHOTEP_DESIGN.md` §6 lists in full. The human commands a panel needs:

| Panel action | Command | Who |
|---|---|---|
| Start / stop an event | `.nyar event start <id>` / `.nyar event stop <id>` | admin |
| Enable / disable an event | `.nyar event enable <id>` / `.nyar event disable <id>` | admin |
| Edit one field | `.nyar event set <id> <field> <value>` | admin |
| Reload definitions from disk | `.nyar event reload` | admin |
| New / copy / delete an event | `.nyar event new <id> <pillar>` / `.nyar event copy <id> <newId>` / `.nyar event delete <id>` then `… confirm` | admin |
| Set an event's location to the admin's position | `.nyar event set <id> location here` | admin |
| Create an event from a template | `.nyar template list [pillar] [page]` / `.nyar template info <template>` / `.nyar template use <template> [as <id>]` | admin |
| Switch a pillar (saved to the cfg) | `.nyar pillar list` / `.nyar pillar <name> on\|off` | admin |
| Test spawn | `.nyar spawn <unit> [count] [level] [hp×] [power×]` | admin |
| Kill switch | `.nyar purge` then `.nyar purge confirm` (two presses, with a confirmation dialog) | admin |
| Broadcast now | `.nyar announce <text>` / `.nyar announce digest` (text 1-200 characters, no `<`, `>` or control characters; otherwise `badarg`) | admin |
| Add / remove a zone at the admin's position | `.nyar zone add <name> <radius>` / `.nyar zone remove <name>` | admin |
| Reset stats | `.nyar stats reset <player\|all>` then `… confirm` | admin |
| Hide / show me on boards | `.nyar stats hide` / `.nyar stats show` | anyone |
| Share my board line | `.nyar top <stat> <window> share` | anyone, when `annshare=1` |

Every admin action is logged on the server with the admin's name. A non-admin gets VCF's standard refusal.
Raphael should grey these controls out rather than hide them. api 4 (§5a) gives each admin action a wire twin that
answers `[NYAR:ok]` or `[NYAR:err]`; until the handshake reports `api>=4`, Raphael keeps sending the human commands.

---

## 5a. Admin action twins — IMPLEMENTED (api 4)

Each twin is `.nyar api` followed by the human command of §5, with the same arguments. It runs the same flow as the
human command: the same validation, the same gateway row, the same admin log line (prefixed `api `), the same file
writes and the same pushes (`config-changed`, `event-start`, `event-end`, `killswitch`). It answers exactly one line:
- on success, `[NYAR:ok] cmd=<cmd> verb=<verb> id=<id> …`;
- on refusal, `[NYAR:err] cmd=<cmd> verb=<verb> code=<code> [secs=<n>] [arg=<name>] [reason=<word>]`.

`id` names the object: the event, the pillar, or `-` for purge and reload. The twins are admin-only; as in §4, VCF
refuses a non-admin with its own human line, and before the world is ready a twin replies `still loading`.

| Twin | Success line |
|---|---|
| `.nyar api event start <id>` / `… stop <id>` | `[NYAR:ok] cmd=event verb=start id=<id>` (`verb=stop`) |
| `.nyar api event enable <id>` / `… disable <id>` | `[NYAR:ok] cmd=event verb=enable id=<id> changed=<0\|1>` |
| `.nyar api event set <id> <field> <value>` | `[NYAR:ok] cmd=event verb=set id=<id> field=<field> value=<stored value>` |
| `.nyar api event set <id> location here` | `… field=location value=<x>,<z>`, from the admin's position, each rounded to 0.1 and written with one decimal (`value=-912.9,-828.8`) |
| `.nyar api event reload` | `[NYAR:ok] cmd=event verb=reload id=- count=<definitions loaded>` |
| `.nyar api event new <id> <pillar>` | `[NYAR:ok] cmd=event verb=new id=<id> pillar=<pillar>`; the event is created disabled |
| `.nyar api event copy <id> <newId>` | `[NYAR:ok] cmd=event verb=copy id=<newId> from=<id>`; the copy is disabled |
| `.nyar api event delete <id>`, then `… delete <id> confirm` | `… verb=delete id=<id> confirm=30`, then `… verb=delete id=<id> done=1` |
| `.nyar api template use <template> [as <id>]` | `[NYAR:ok] cmd=template verb=use id=<id> tpl=<template>` |
| `.nyar api pillar <name> on\|off` | `[NYAR:ok] cmd=pillar verb=set id=<name> on=<0\|1> changed=<0\|1>`, plus `ended=<n>` when `off` ended running events; saved to the cfg |
| `.nyar api purge`, then `.nyar api purge confirm` | `[NYAR:ok] cmd=purge verb=ask id=- confirm=30`, then `[NYAR:ok] cmd=purge verb=confirm id=- events=<ended> units=<to despawn> secs=<cooldown>` |

Rules:
- **Idempotency:**
  - `enable` and `disable` on the state the event already holds answer `ok` with `changed=0` and write nothing.
  - `pillar` on the state it already holds answers `ok` with `changed=0` and writes nothing; `off` still ends that
    pillar's running events (an operator may have switched it off in the cfg), with `ended=<n>` and their `event-end`
    pushes.
  - `start` on an active event and `stop` on one that is not active answer `code=state`: the engine's one-instance rule.
  - `set` to the value a field already holds writes the file and answers `ok`, as the human command does.
- **Two-step confirm:** the confirm must come from the same admin within 30 s of the ask, otherwise it answers
  `code=confirm`. `confirm=<secs>` on the ask is that window. A human ask and a twin confirm pair, since they share one
  arming. A purge confirm with nothing left to purge answers `code=state reason=nothing_to_purge`, armed or not.
  Raphael shows its confirmation dialog between the two presses.
- **After an `ok`:** Raphael may update its state on the `ok` line and treat the push that follows as confirmation.
- **A refusal** changes nothing on the server except what a file now holds after a failed save (the server then
  follows the file) and the events a pillar `off` ends. A twin that answers `code=io reason=internal` is the
  exception: its server operation threw, possibly after it changed state, so the action may be partly or fully
  applied; Raphael re-reads state (`api status`, `api events`, `api pillar list`, `api killswitch`) before acting on
  it.
- **Arguments:** an unknown verb (`list` and `info` of `event` included) answers `code=badarg arg=verb`; a missing or
  malformed argument answers `code=badarg` with `arg` ∈ `id`, `field`, `value`, `pillar`, `state`, `newId`, `template`,
  `confirm`, `location`; one surplus word answers `code=badarg arg=extra`. Two or more surplus words are refused by
  VCF with its own human line before the mod runs, so Raphael never sends them.
- **Rate:** at most 5 twins per admin per second, sliding. The sixth answers `code=ratelimit secs=1` before anything
  else runs; every twin counts, whatever it answers. The reads of §3 are not counted.
- **Failure:** a twin that fails on the server answers `code=io reason=internal`; the server log names the exception.
  The failure may come after the action changed state, and nothing is rolled back (see **A refusal** above).
  No push is promised after `internal`; re-read, then re-send only if the state shows the action did not apply (a repeated start answers `code=state`, a repeated purge confirm `code=confirm` or `state reason=nothing_to_purge`).
- **Reasons** (the `reason` word): `general`, `pillar_off`, `max_concurrent`, `disabled`, `already_active`,
  `empower_clash`, `admin_location`, `no_position`, `condition`, `not_active`, `field`, `value`, `trigger`, `stats`,
  `read`, `parse`, `stale`, `read_only`, `size`, `write_uncertain`, `count`, `running`, `save`, `nothing_to_purge`,
  `internal`, `template`; from api 5, `no_player_in_region` (`code=state arg=scope`: no online player is in the event's
  trigger regions) and `out_of_region` (`code=state arg=scope`: the kill is outside the trigger regions; `code=badarg
  arg=location`: the admin stands outside the action regions).
- **Line length:** every line fits 480 bytes. `value`, `verb` and `reason` are cut to 120 bytes on a character
  boundary; an empty echoed word is `-`.

```
[NYAR:ok] cmd=event verb=set id=undead-nightfall field=duration value=1800
[NYAR:ok] cmd=event verb=enable id=undead-nightfall changed=1
[NYAR:ok] cmd=event verb=copy id=raid-2 from=raid
[NYAR:ok] cmd=template verb=use id=undead-nightfall tpl=undead-nightfall
[NYAR:ok] cmd=pillar verb=set id=spawns on=0 changed=1 ended=2
[NYAR:ok] cmd=purge verb=ask id=- confirm=30
[NYAR:ok] cmd=purge verb=confirm id=- events=1 units=12 secs=300
[NYAR:err] cmd=event verb=start code=state arg=id reason=already_active
[NYAR:err] cmd=event verb=start code=cooldown secs=240
[NYAR:err] cmd=event verb=set code=io reason=stale
[NYAR:err] cmd=template verb=use code=exists arg=id
[NYAR:err] cmd=event verb=launch code=ratelimit secs=1
```

---

## 6. What Raphael builds (lives in the Raphael workspace)

Mirror the Faust integration:

| Area | What to add |
|---|---|
| `Services/Nyar/` | `NyarWireParser.cs` (the grammar in §1), `NyarClient.cs` (send helpers), `NyarState.cs` (handshake map, events, zones, boards), `NyarProtocolService.cs` (probe, `Tick`, `HandleLine`, `Reset`), `NyarDiag.cs` |
| `Patches/ClientChatPatch.cs` | A `[NYAR:` branch next to `[FAUST:` that calls `NyarProtocolService.HandleLine` and destroys the chat entity |
| `Plugin.cs` / `Patches/InitializationPatch.cs` | Register `NyarProtocolService.Tick`; call `Reset()` on login |
| `Config/Settings.cs` | `NyarAvailability` (Auto/On/Off) and `NyarDiagnostics` |
| `UI/ModContent/MainPanel.cs` + `MainPanel.Nyar.cs` | A NYAR tab group, shown on `ready=1`, with these panels |

The panels:
- **Events:** live status board from `api status` plus push events, with a countdown per wave.
- **Leaderboards:** stat and window toggles, paging, the requester's own row highlighted, and a Share button when
  `annshare=1`.
- **My stats:** `api me`, plus the hide/show toggle.
- **Admin › Events:** the `api events` list with start/stop/enable/disable/reload and a field editor.
- **Admin › Zones:** the `api zones` list and map pins, add-here and remove.
- **Admin › Announcements:** free-text and digest broadcast, and the five switches read-only.
- **Admin › Kill switch:** purge with confirmation, and the `killswitch` countdown.
- **From api 4 (§3, §5a):** Admin › Templates and Admin › Pillars.
- **From api 5 (§3):** a region picker fed by `api regions`, and the region of each event on the status board and in
  Admin › Events (`region=`); later (§10) a live horde and anti-farming readout.

Gate the panels on the handshake:
- A pillar tab shows only when its switch is 1.
- `stats=0` hides Leaderboards and My stats.
- `admin=0` greys out the Admin tabs.

---

## 7. Change discipline

- Any change to a `.nyar api` command, a `[NYAR:*]` shape or a handshake key lands in the same commit as an edit
  to this file. A shape that ships moves from PLANNED to IMPLEMENTED with its api number.
- **`api` is bumped whenever the wire grows.** New keys and tags are additive; an existing key is never renamed or
  repurposed. Removing one takes a RETIRED note here and one api version of overlap.
- A preflight check (Epic D38, `Test-CheckWireContract`, built in the `raphael-api-core` child) fails when a tag or
  `api` command in the code is missing from the Tags and commands table, is still marked PLANNED there, or when the
  api numbers differ.
- The human command table lives in `docs/NYARLATHOTEP_DESIGN.md` §6; §5 above quotes only the commands a panel sends.

## 8. Request log (Raphael → Nyarlathotep)

| # | Date | Request | Status |
|---|---|---|---|
| — | — | none yet | — |

## 9. Change log

| Api | Release | Change |
|---|---|---|
| 1 | 0.2.0 (foundation) | The handshake: `.nyar api version`, tag `version`. |
| 2 | 0.3.0 (raphael-api-core) | `status`, `events` and `sub`; tags `event`, `def`, `end`, `err`, `ok`, `ev`; paging and errors (§4). |
| 3 | 0.4.0 (faction-empowerment) | `status` rows of `kind=empower`: `faction=<names joined by ','>`, `wave=-`, admin `units` = NPCs holding the event's empowerment. No new tag or key. |
| 4 | 0.5.2 (raphael-api-admin) | Admin action twins `event`, `template`, `pillar`, `purge` (§5a); reads `templates`, `template info`, `pillar list`, `killswitch`; tags `tpl`, `pillar`, `ks`; `verb=` on twin `ok` and `err` lines; `reason=` on `err` lines; error codes `exists`, `state`, `invalid`, `full`, `io`, `confirm`, `limit` (§4); `config-changed` after a pillar switch. |
| 5 | 0.6.0 (regions) | The `regions` read and tag `region` (§3); `region=` last on `[NYAR:def]` and `[NYAR:event]` rows and after `secs` on `event-start` and `event-end` pushes; reasons `no_player_in_region` and `out_of_region` (§5a). |
| 6 | 0.8.0 (automation) | `trigger` values `interval`, `regionentered` and `factionkills` on `[NYAR:def]` rows (§3). No new tag, key, push kind or command. |

---

## 10. api 7 and later — PLANNED

Nothing in this section is sent yet. Every shape is PLANNED under the child named with it, and can change until that
child ships. The Tags and commands table carries one row per new tag and command. A child that grows the wire bumps
`api` by one when it ships, so the number is assigned then. This section names the child and its release, in the
Epic's order, not the api. §10.1 and §10.3 moved into §5a and §4 when api 4 shipped (raphael-api-admin, 0.5.2), and
the regions rows into §3 when api 5 shipped (regions, 0.6.0); the numbers of §10.2 and §10.4 are kept so references to
them stay valid.

| Child | Release | Adds |
|---|---|---|
| event-spawns | 0.7.0 | new settable fields (hunt, modifiers, AroundPlayer, loot), set through the `event set` twin (§5a); no new tag |
| boss-reinforcements | 0.8.0 | the push `boss-adds` |
| anti-farming | 0.9.0 | the push `farm-tier`; `zones` and `zone`, relabelled from defended-zones, which it absorbs |
| outbreak | 0.10.0 | the pushes `horde-wave`, `horde-boss-down` and `horde-spread` |

The sieges and castle-takeover child adds its rows when it is planned. Siege pushes keep §3's rule: they go only to
members of the target clan and to admins.

### 10.2 New reads — PLANNED

| Read | Child | Rows | Who |
|---|---|---|---|
| `.nyar api zones [page]` | anti-farming | §3's shape; the row is relabelled when anti-farming is planned | admin |


### 10.4 Push events — PLANNED

New `[NYAR:ev]` types and keys. §3's fairness rule holds for all of them: a push never says more than chat or
`.nyar status` tells every player. The new keys follow `secs`, which is 0 for every new type.

| Type or key | Child | Payload |
|---|---|---|
| `boss-adds` | boss-reinforcements (0.8.0) | `id=<event> boss=<V Blood name> phase=<n> count=<adds that joined>` |
| `farm-tier` | anti-farming (0.9.0) | `id=<event> tier=<n> region=<id>` |
| `horde-wave` | outbreak (0.10.0) | `id=<event> wave=<n> count=<units> region=<id>` |
| `horde-boss-down` | outbreak (0.10.0) | `id=<event> boss=<name> empowered=<0\|1>`; `0` once the last leading boss is down and the horde has lost its empowerment |
| `horde-spread` | outbreak (0.10.0) | `id=<event> hotspots=<n> region=<id>` |

Privacy:
- `farm-tier` never names the targeted players and never carries a position. It carries only the region, and it
  sends the same line to the targeted group, to everyone else and to admins. Admins find positions with
  `.nyar debug here`, not on the wire.
- No push names a player.

```
[NYAR:ev] type=horde-wave id=undead-nightfall secs=0 wave=3 count=60 region=-
[NYAR:ev] type=farm-tier id=anti-farm secs=0 tier=2 region=FarbaneWoods
```
