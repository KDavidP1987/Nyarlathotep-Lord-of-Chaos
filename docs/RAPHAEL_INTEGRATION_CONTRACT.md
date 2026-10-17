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
> **Current api:** 8
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
> `wave` push. api 6 adds no key; an api 5 client may show an unknown `trigger` value wrongly.
>
> api 7 ships with the `revenge` release (0.12.0): a new value of `trigger` on `[NYAR:def]` and `[NYAR:tpl]` rows,
> `revenge` (§3). It adds no tag, key, push kind or command, and no heat, player or position reaches the wire; an api 6
> client may show the unknown `trigger` value wrongly.
>
> api 8 ships with the 0.13.0 release (`unit-watch`): the twin `event unstick` (§5a) and the reason `watch_off`. It
> adds no tag, key or push kind. api 9 and later are PLANNED in §10, the rows each later child adds. Nothing in §10 is
> sent yet.

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
| `field` | tag | PLANNED (live-ops) | — |
| `run` | tag | PLANNED (live-ops) | — |
| `spot` | tag | PLANNED (live-ops) | — |
| `unit` | tag | PLANNED (live-ops) | — |
| `usum` | tag | PLANNED (live-ops) | — |
| `next` | tag | PLANNED (live-ops) | — |
| `health` | tag | PLANNED (live-ops) | — |
| `mine` | tag | PLANNED (live-ops) | — |
| `ann` | tag | PLANNED (communications) | — |
| `msg` | tag | PLANNED (communications) | — |
| `note` | tag | PLANNED (communications) | — |
| `fields` | command | PLANNED (live-ops) | — |
| `run` | command | PLANNED (live-ops) | — |
| `units` | command | PLANNED (live-ops) | — |
| `unit` | command | PLANNED (live-ops) | — |
| `schedule` | command | PLANNED (live-ops) | — |
| `health` | command | PLANNED (live-ops) | — |
| `mine` | command | PLANNED (live-ops) | — |
| `announce` | command | PLANNED (communications) | — |
| `ann` | command | PLANNED (communications) | — |
| `messages` | command | PLANNED (communications) | — |
| `message` | command | PLANNED (communications) | — |
| `notices` | command | PLANNED (communications) | — |

The live-ops and communications rows were proposed on 2026-10-15 (§10.5-§10.8), and the owner settled their decisions
the same day (§10.9 L1-L8, design §9 D59). Neither child has a dod plan yet.

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
- `faction` is `-` for waves events; `wave` is `<spawned>/<total>`. A wave-set event's (0.9.0, `action.waveList`) total is the number of waves in its list.
- A wave set's scoreboard (0.9.0) is a server chat line only: player names never reach the wire, and an event that ends with all its waves defeated sends the usual `event-end`.
- A swarm (0.10.0, `action.arena`, `action.winner`, `action.growth`) adds nothing to the wire: an event's arena and winner never reach the wire, and names never do. A growth event's `wave` total is its `action.waves`.
- Hunting grounds (0.11.0, a Region location, `action.regionLock`, the RegionHunt behaviour) add nothing to the wire: a Region location, the region lock and the region hunt never reach the wire, and a hunt target is never named or located.
- An apocalypse (0.13.0, `"type": "Apocalypse"`) adds no tag, key, push kind or command of its own (0.13.0's api 8 is unit watch's `event unstick` twin, §5a), and hotspots, roamers, fronts and the goal never reach the wire. Its status row is `kind=waves` with `wave=<hordes risen>/<hordes>`, and every outcome (won, lost, survived) sends the usual `event-end`.
- Wave variety (0.13.0, `action.pools`, pool entries, boss rolls in `action.bosses` and `action.waveList.<n>.bosses`, `action.pursuit`) adds no tag, key, push kind or command: the new fields are settable through the admin-only `event set` api twin like every settable field (the stored value cut to 120 bytes, as today), and no row, push or read carries pools, rolls or pursuit state in 0.13.0.
- The login notice (0.13.1, `[Announcements] LoginEventNotice`) adds nothing to the wire: LoginEventNotice is a server cfg switch Raphael neither reads nor sets in api 8, and `[NYAR:version]` keeps its keys.
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
- `trigger` ∈ `schedule | ingame | vbloodkilled | interval | regionentered | factionkills | revenge | bossengaged |
  bosshealth | zoneactivity | manual`; `interval`, `regionentered` and `factionkills` from api 6, `revenge` from api 7.
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
| Unstick a running event's units | `.nyar event unstick <id>` | admin |
| Switch a pillar (saved to the cfg) | `.nyar pillar list` / `.nyar pillar <name> on\|off` | admin |
| Test spawn | `.nyar spawn <unit> [count] [level] [hp×] [power×]` | admin |
| Kill switch | `.nyar purge` then `.nyar purge confirm` (two presses, with a confirmation dialog) | admin |
| Broadcast now | `.nyar announce <text>` (text 1-200 characters, no `<`, `>` or control characters; quote text of more than 16 words). No wire twin yet (§10.6). `announce digest` is not built: the word `digest` is broadcast as text until the stats child adds the digest | admin |
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
| `.nyar api event unstick <id>` | `[NYAR:ok] cmd=event verb=unstick id=<id> marked=<n> fighting=<f>` (api 8) |
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
  arg=location`: the admin stands outside the action regions); from api 8, `watch_off` (`code=invalid`: unit watch is off).
- **Unstick** (api 8, 0.13.0, unit-watch): `event unstick` marks every live unit of the event's running instance that
  has not fought in the last StuckSeconds as stuck; `marked` counts them and `fighting` the units left alone. It refuses
  with `code=notfound arg=id` for an unknown id, `code=state arg=id reason=not_active` when the event is not running,
  `code=state arg=id` when it has no live unit, and `code=invalid reason=watch_off` when the server's `[UnitWatch]
  Enabled` is false. The watch's lines never reach the wire, and no line names or locates a player.
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
- **From api 8 (§5a):** an Unstick button on each running event in Admin › Events.
- **When live-ops and communications ship (§10.5-§10.8, PLANNED):** Admin › Live (one event in detail), Admin › Units
  (global unit lookup), Admin › Schedule, Admin › Health, Admin › Messages, a field editor that shows stored values,
  My events (`mine`), and toasts from `notice` pushes. `docs/RAPHAEL_HANDOFF_API8.md` describes each panel.

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
| R1 | 2026-10-15 | When an ending event's units are gone, say so: a push, or `grace=` on `event-end` (Raphael's list refresh guessed) | planned, §10.7 `units-gone` |
| R2 | 2026-10-15 | `[NYAR:def]` names an apocalypse `action=apocalypse`, not `action=waves` | planned, §10.5 |
| R3 | 2026-10-15 | `code=disabled reason=pillar_off` names the pillar (`arg=pillar` today; `pillar=<name>` wanted) | planned, §10.5 |

## 9. Change log

| Api | Release | Change |
|---|---|---|
| 1 | 0.2.0 (foundation) | The handshake: `.nyar api version`, tag `version`. |
| 2 | 0.3.0 (raphael-api-core) | `status`, `events` and `sub`; tags `event`, `def`, `end`, `err`, `ok`, `ev`; paging and errors (§4). |
| 3 | 0.4.0 (faction-empowerment) | `status` rows of `kind=empower`: `faction=<names joined by ','>`, `wave=-`, admin `units` = NPCs holding the event's empowerment. No new tag or key. |
| 4 | 0.5.2 (raphael-api-admin) | Admin action twins `event`, `template`, `pillar`, `purge` (§5a); reads `templates`, `template info`, `pillar list`, `killswitch`; tags `tpl`, `pillar`, `ks`; `verb=` on twin `ok` and `err` lines; `reason=` on `err` lines; error codes `exists`, `state`, `invalid`, `full`, `io`, `confirm`, `limit` (§4); `config-changed` after a pillar switch. |
| 5 | 0.6.0 (regions) | The `regions` read and tag `region` (§3); `region=` last on `[NYAR:def]` and `[NYAR:event]` rows and after `secs` on `event-start` and `event-end` pushes; reasons `no_player_in_region` and `out_of_region` (§5a). |
| 6 | 0.8.0 (automation) | `trigger` values `interval`, `regionentered` and `factionkills` on `[NYAR:def]` rows (§3). No new tag, key, push kind or command. |
| 7 | 0.12.0 (revenge) | `trigger` value `revenge` on `[NYAR:def]` and `[NYAR:tpl]` rows (§3). No new tag, key, push kind or command. |
| 8 | 0.13.0 (unit-watch) | The twin `event unstick` (§5a) and the reason `watch_off`; no new tag, key or push kind. |

---

## 10. api 9 and later — PLANNED

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
| live-ops (proposed 2026-10-15, no plan yet) | planned next, beside the 0.13.0 test weekend (L7) | the reads `fields`, `run`, `units`, `unit`, `schedule`, `health`, `mine`; the twins `unit despawn\|unstick\|goto`; `next=` on status rows; the pushes `progress`, `wave-cleared`, `health`; `what=` on `config-changed` (§10.5-§10.7) |
| communications (proposed 2026-10-15, no plan yet) | after the 0.13.0 release (L7) | the login event notice, start warnings, editable announce text, scheduled and recurring messages; the reads `ann`, `messages`, `notices`; the twins `announce`, `message`; the pushes `notice` and `event-soon` (§10.6-§10.8) |

The release numbers of the first four rows are the Epic's old order; those children have not shipped and keep their
rows until they are planned.

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

### 10.5 Live operations reads — PLANNED (live-ops, proposed 2026-10-15)

The reads an admin console needs to see what the mod is doing right now: a definition's stored fields (so the field
editor shows current values), one running event in detail, every unit the mod spawned or empowered, what starts next,
and the server's health. All are admin-only except `mine` and the player form of `schedule`. None changes anything, and
none counts against §5a's twin rate. Rows are built from the server's own ledgers; a read touches the game only for the
rows of the page it sends.

**`.nyar api fields event <id> [page]`** and **`.nyar api fields template <tpl> [page]`** — every field the
definition or template holds, one row per field of design §6's settable-field table that is set, then
`[NYAR:end] cmd=fields page= count=`:
```
[NYAR:field] id=ashfall key=durationSeconds value=900 part=1/1
[NYAR:field] id=ashfall key=action.units value=CHAR_Undead_SkeletonSoldier_Base*6,CHAR_Undead_Priest*2 part=1/1
[NYAR:field] id=ashfall key=announce.start value=The_dead_walk_on_Ashfall part=1/1
```
- `value` is the stored value in the form `event set` takes, wire-safe (§1; `announce.*` text uses §10.8's escape). A
  value longer than 300 bytes is split over rows `part=<k>/<n>` with the same `key`; the client joins them in order.
- An unset optional field sends no row. An unknown id or template answers `code=notfound arg=id` / `arg=template`.

**`.nyar api run <id>`** — one running event in detail, then `[NYAR:end] cmd=run count=<n>`:
```
[NYAR:run] id=ashfall kind=waves state=active trigger=schedule age=488 left=412 wave=2/3 next=95 clear=0 alive=14 queued=4 killed=22 expired=0 stuck=1 recovered=3 removed=0 region=-
[NYAR:run] id=legion-surge kind=empower state=active trigger=vbloodkilled age=300 left=1500 holding=42 pending=6 region=CursedForest
[NYAR:run] id=night-of-the-dead kind=apocalypse state=active trigger=manual age=1200 left=2400 hordes=2/4 next=600 roamers=37 hotspots=3 alive=88 killed=140 region=-
[NYAR:spot] id=night-of-the-dead kind=hotspot n=1 x=-1520.0 y=0.0 z=-460.0 region=FarbaneWoods pct=-
[NYAR:spot] id=night-of-the-dead kind=front n=2 x=-1310.4 y=2.5 z=-505.1 region=FarbaneWoods pct=40
[NYAR:spot] id=night-of-the-dead kind=goal n=1 x=-912.9 y=0.0 z=-828.8 region=DunleyFarmlands pct=-
```
- `age` is seconds since the start. `next` is seconds until the next wave or horde, or `-` when the next waits for a
  clear (`clear=1`) or none is left. `alive` counts live tracked units, `queued` spawn orders waiting, `killed` units
  killed, `expired` units that timed out; `stuck`, `recovered` and `removed` are unit watch's counts (`-` when unit
  watch is off).
- `kind=empower` sends `holding` (NPCs holding the carrier) and `pending` (carriers still to apply); `kind=apocalypse`
  sends `hordes=<risen>/<total>`, `roamers` and `hotspots`.
- `trigger` names how the instance started (a trigger type or `manual`). It never names the player whose action
  started it.
- `[NYAR:spot]` rows follow: our event's map spots (hotspots, horde fronts, the goal, a Point location), with exact
  coordinates (L1). They never carry a player's position or a hunt target; an Admin or AroundPlayer location sends no
  spot row, because it is a player's position.
- An event that is not running answers `code=state arg=id reason=not_active`.

**`.nyar api units [filter …] [page]`** — every unit the mod tracks or empowers, then
`[NYAR:end] cmd=units page= count=`:
```
[NYAR:unit] key=4294967361 src=spawn prefab=CHAR_Undead_SkeletonSoldier_Base event=ashfall wave=2 lvl=42 hp=610 maxhp=900 left=380 watch=fighting x=-1500.2 y=0.0 z=-470.9 region=FarbaneWoods
[NYAR:unit] key=8589934722 src=empower prefab=CHAR_Militia_Guard event=legion-surge wave=- lvl=38 hp=1200 maxhp=1200 left=1500 watch=- x=-901.0 y=5.0 z=-812.4 region=DunleyFarmlands
```
- `src` ∈ `spawn` (a unit SpawnTracker registered: waves, bosses, roamers, `.nyar spawn`) | `empower` (a native NPC
  holding an empowerment carrier). `event=-` marks a test spawn.
- `key` is stable while the unit lives and is the handle for `unit` and the unit twins (§10.6).
- `watch` ∈ `fighting | moving | stuck | waiting | -` (unit watch's view; `-` when it is off, and for `src=empower`).
- `x`, `y` and `z` are the unit's exact position, read when the row is built, one decimal (L1). Positions of our units,
  never of a player. `api units` with no `event=` filter is the bulk view across every event and test spawn;
  `api units event=<id>` is one event's units.
- Filters are `key=value` words before the page, in any order, all optional: `event=<id>`, `src=spawn|empower`,
  `prefab=<text>` (case-insensitive substring), `region=<id>`, `watch=<state>`, `near=<metres>` (around the requesting
  admin, 1-500). An unknown filter answers `code=badarg arg=filter`.
- Rows are ordered by event, then wave, then key.

**`.nyar api units summary [page]`** — one row per event, source and prefab, then `[NYAR:end] cmd=units page= count=`:
```
[NYAR:usum] event=ashfall src=spawn prefab=CHAR_Undead_SkeletonSoldier_Base alive=10 stuck=1
```

**`.nyar api unit <key>`** — the one `[NYAR:unit]` row, then `[NYAR:end] cmd=unit count=1`; `code=notfound arg=key`
once the unit is gone.

**`.nyar api schedule [page]`** — what can start next, one row per enabled definition, soonest first, then
`[NYAR:end] cmd=schedule page= count=`:
```
[NYAR:next] id=ashfall trigger=schedule in=3540 at=1792101600 cooldown=0 why=scheduled
[NYAR:next] id=undead-nightfall trigger=ingame in=- at=- cooldown=0 why=night
[NYAR:next] id=revenge-legion trigger=revenge in=- at=- cooldown=1200 why=armed
```
- `in` is seconds until the start and `at` its Unix time (UTC), for `schedule` and `interval` triggers; both are `-`
  for triggers that wait on the world. `cooldown` is the seconds left of `conditions.cooldownMinutes`.
- `why` ∈ `scheduled | interval | night | day | armed | running | cooldown | disabled | pillar_off | killswitch |
  max_concurrent`.
- A player who sends it gets only the `trigger=schedule` rows due today, which the daily banner already tells everyone
  (§3's fairness rule), and only while `DailyBanner` is on; otherwise `code=disabled`.

**`.nyar api health`** — one row, then `[NYAR:end] cmd=health count=1`:
```
[NYAR:health] tracked=212 max=600 spawnq=0 despawnq=12 empowered=42 carrierq=0 events=2 maxevents=3 announceq=1 pushq=0 subs=4 ks=0 degraded=-
```
- `degraded` lists HealthMonitor's degraded parts joined by `,`, or `-`.

**`.nyar api mine`** (anyone) — what concerns the requester alone, then `[NYAR:end] cmd=mine count=<n>`. Two row types:
```
[NYAR:mine] type=standing id=ashfall kills=12 deaths=1 rank=2 of=6
[NYAR:mine] type=alert id=revenge-legion kind=revenge state=warned faction=Legion left=- wave=-
[NYAR:mine] type=alert id=revenge-legion kind=revenge state=active faction=Legion left=540 wave=1/3
[NYAR:mine] type=alert id=bandit-ambush kind=hunted state=active faction=Bandits left=300 wave=2/2
```
- `type=standing`: one row per running event that keeps a scoreboard and counts the requester. `rank` is their place as
  it stands now, `of` the players on it (L4).
- `type=alert` (L8): an event that is about the requester personally.
  - `kind=revenge`: `state=warned` once a revenge definition's heat on them has passed a `warnAt` step (the private
    chat warning they already got), until that heat cools or the event starts; `state=active` while a revenge event
    runs with them as its focus.
  - `kind=hunted`: `state=active` while a running event's wave was aimed at them (an AroundPlayer wave that picked
    them, or its fan-out) or its hunting units hold them as their target.
  - `faction` names the faction behind it (without `Faction_`); `left` and `wave` are the event's, as on its status row,
    or `-` before it starts.
  - An alert never carries a heat number, a unit count, a unit's or player's position, a direction or a distance, and
    never names another player. It reaches only the player it is about.
- A player with nothing gets `[NYAR:end] cmd=mine count=0`.

**Status rows gain `next`.** From live-ops, `[NYAR:event]` rows end with `next=<seconds until the next wave or horde,
or ->`: for admins always; for players only when the event's `announce.warnings` is true and `WaveWarnings` is on,
  since the chat warnings already tell them, and `-` otherwise (L2). A new last key, so an api 8 parser skips it.

**Two corrections to existing lines (requests R2, R3).** From live-ops, `[NYAR:def]` says `action=apocalypse` for an
apocalypse definition (api 8 says `action=waves`); a client that does not know the value shows it as text. The
pillar-off refusal gains a last key naming the pillar: `[NYAR:err] cmd=event verb=start code=disabled arg=pillar
reason=pillar_off pillar=spawns`, so the client can say which `[Pillars]` switch is off.

### 10.6 Twins — PLANNED (live-ops, communications)

§5a's rules hold for every new twin: one `ok` or `err` line, the same flow and log line as the human command, and the
5-per-second rate.

| Twin | Child | Success line |
|---|---|---|
| `.nyar api unit despawn <key>` | live-ops | `[NYAR:ok] cmd=unit verb=despawn id=<key> queued=1`. The despawn goes through SpawnTracker's per-tick budget like any other. A `src=empower` unit answers `code=state reason=not_ours`: empowerment ends by stopping the event, never by killing a native NPC |
| `.nyar api unit unstick <key>` | live-ops | `[NYAR:ok] cmd=unit verb=unstick id=<key> marked=<0\|1>`; `code=invalid reason=watch_off` as `event unstick` |
| `.nyar api unit goto <key>` | live-ops | `[NYAR:ok] cmd=unit verb=goto id=<key>`; teleports only the requesting admin beside the unit (L3); `code=notfound arg=key` once the unit is gone |
| `.nyar api announce "<text>"` | communications | `[NYAR:ok] cmd=announce verb=send id=- queued=<lines ahead>`; §5's text rules |
| `.nyar api message add <id> <when> <target> "<text>"` | communications | `[NYAR:ok] cmd=message verb=add id=<id>`; created disabled |
| `.nyar api message set <id> <field> <value>` | communications | `[NYAR:ok] cmd=message verb=set id=<id> field=<field> value=<stored>`; fields `when`, `target`, `text` |
| `.nyar api message enable\|disable <id>` | communications | `[NYAR:ok] cmd=message verb=enable id=<id> changed=<0\|1>` |
| `.nyar api message delete <id>`, then `… confirm` | communications | as `event delete` |
| `.nyar api message test <id>` | communications | `[NYAR:ok] cmd=message verb=test id=<id>`; sends the message to the requesting admin only |

Communications also makes four event fields settable through the existing `event set` twin: `announce.start` and
`announce.end` (1-5 lines joined by `|`, each with §5's text rules), `announce.warnings` (`true|false`) and
`announce.soon` (`true|false`, §10.8). They add no tag.

### 10.7 Push events — PLANNED (live-ops, communications)

§3's fairness rule holds: a push never tells a subscriber more than chat or `.nyar status` tells every player. A key
marked admin-only is sent to players as `-`.

| Type or key | Child | Payload |
|---|---|---|
| `progress` | live-ops | `id=<event> secs=<left> wave=<spawned>/<total> next=<secs\|-> alive=<n\|->`. Sent when a wave or horde spawns, on every clear, and at most once per 15 s per event while `alive` changes. `alive` is admin-only; `next` follows the status rows' rule (L2) |
| `wave-cleared` | live-ops | `id=<event> secs=0 wave=<n>`; the push twin of wave-sets' "wave n cleared" chat line, sent to whoever that line reaches |
| `health` | live-ops | `id=- secs=0 degraded=<list\|->`; admins only, when the degraded set changes |
| `personal` | live-ops | `id=<event> secs=0 kind=revenge\|hunted state=warned\|active\|ended`; only to the player the alert is about, when one of their `mine` alerts appears, changes state or ends (L8). The client then re-reads `api mine` |
| `what=` on `config-changed` | live-ops | `events \| pillars \| messages \| announce`, after `secs`; the client re-reads only that part. An api 8 client ignores it and re-reads everything |
| `notice` | communications | `id=<event\|-> secs=0 kind=<kind> to=<all\|you\|clan\|admins> text=<escaped>`; the push twin of every chat line the server sends to that player, public or private (§10.8). The chat line is still sent: the push is a copy (L5) |
| `units-gone` | live-ops | `id=<event> secs=0`; after `event-end`, once the event's last tracked unit has despawned or died (request R1). The client drops the event's unit rows then, without a refresh |
| `event-soon` | communications | `id=<event> secs=<until start>`, at the start-warning offsets before a `schedule` trigger's start when `announce.soon` is on |

`notice` `kind` ∈ `start | end | warn | cleared | horde | march | outcome | scoreboard | revenge | daily | admin |
message | login | soon | degraded`. `to=you` marks a private line (a revenge warning, a login notice, the admin
degraded notice). A scoreboard notice carries the player names its chat line already shows everyone (wave-sets D12).
No notice carries a position.

```
[NYAR:ev] type=progress id=ashfall secs=412 wave=2/3 next=95 alive=14
[NYAR:ev] type=notice id=ashfall secs=0 kind=warn to=all text=Wave_2_of_Ashfall_Raid_in_60_seconds!
[NYAR:ev] type=event-soon id=ashfall secs=300
[NYAR:ev] type=config-changed id=- secs=0 what=messages
```

### 10.8 Communications server features — PLANNED (communications, proposed 2026-10-15)

These are server behaviour, not only wire. Each has its own switch, off by default, and the twins above.

- **Login event notice** (`[Announcements] LoginEvents`): a player who connects while events are active gets one
  private line per event they may see (at most 3), 10 s after connecting, and not again on a reconnect within 60 s
  (the existing LoginGate): "Ashfall Raid is under way: 7 min left, wave 2 of 3". A siege goes only to the target clan.
  Pushed as `notice kind=login to=you`. A Raphael client can already build its own login toast from `api status` after
  the handshake, on api 8 today; this feature reaches players without Raphael.
- **Start warnings** (`[Announcements] StartWarnings`, per event `announce.soon`): for `schedule` triggers only (no
  other trigger can be predicted), a server-wide line at `StartWarningOffsets` (default `600,60`) before the start:
  "Ashfall Raid begins in 10 minutes". Pushed as `event-soon` and `notice kind=soon`.
- **Editable announce text**: `announce.start`, `announce.end`, `announce.warnings` and `announce.soon` become
  settable (§10.6).
- **Scheduled and recurring messages** (`messages.json`, written only by Persistence; at most 50 messages, each 1-200
  characters with §5's text rules), sent through the announcement queue (one line a second):
  - `when` ∈ `every:<minutes>` (5-1440) | `daily:<HH:mm>` | `days:<Mon,Tue,…>@<HH:mm>` | `login` (each player once per
    connect, after the login notice) | `event-start:<id>` | `event-end:<id>` | `while:<id>:<minutes>` (repeats while
    that event runs).
  - `target` ∈ `all` (connected players) | `admins`.
  - Tokens in the text: `{events}` (the active events with time left), `{next}` (the next `schedule` start and its
    time), `{event}` and `{left}` (for the event-bound `when` values). A token expands to player-safe facts only.
- **Text escape** (`notice` text, `announce.*` values, `msg` text and `when`): a space becomes `_`; `%`, `_`, `=`, `;`,
  `:`, `,` and `|` become `%25`, `%5F`, `%3D`, `%3B`, `%3A`, `%2C` and `%7C`. The client turns `_` back into a space,
  then decodes the `%XX` codes. Text is cut on a character boundary before it is escaped, so the line still fits 480
  bytes.

Reads:

**`.nyar api ann`** (admin) — the announcement settings, one row, then `[NYAR:end] cmd=ann count=1`:
```
[NYAR:ann] warnings=300,60,10 daily=20:00 tzmin=-240 soon=600,60 loginevents=0 startwarnings=0 queue=0
```
- `tzmin` is the server's UTC offset in minutes, so the client can show `daily` and message times in the player's
  own time.

**`.nyar api messages [page]`** (admin) — then `[NYAR:end] cmd=messages page= count=`:
```
[NYAR:msg] id=welcome enabled=1 when=login target=all next=- text=Welcome%2C_events_tonight%3A_{next}
[NYAR:msg] id=raid-reminder enabled=1 when=while%3Aashfall%3A5 target=all next=180 text=Ashfall_Raid%3A_{left}_left
```
- `next` is seconds until the message is next due, or `-` for an event-bound or `login` message that is not due.

**`.nyar api notices`** (anyone) — the public notices of the events still active, oldest first, at most 10, as
`[NYAR:note]` rows with `notice`'s keys, then `[NYAR:end] cmd=notices count=<n>`. A client that has just logged in
reads it to show what it missed.

### 10.9 Decisions — Resolved (owner, 2026-10-15; design §9 D59)

L8 came from the owner's follow-up message the same day; the others were asked as options.


| # | Decision | Answer |
|---|---|---|
| L1 | Map positions for admins | Admins get exact coordinates (x, y, z) of every unit the mod spawned, per event (`api units event=<id>`) or across all events (`api units`), of the event's map spots (`[NYAR:spot]`), and of native NPCs holding an empowerment carrier (`src=empower`). Never a player's position or a hunt target; players never get coordinates (§1). For admins only, this amends the apocalypse (0.13.0) and hunting-grounds (0.11.0) rule that hotspots, fronts, the goal and hunts never reach the wire. |
| L2 | Next-wave countdown | Admins always; players only for an event whose chat wave warnings are on. |
| L3 | `unit goto` | Built: it teleports only the requesting admin. |
| L4 | `mine` | Kills, deaths and rank, the requester's own row only. |
| L5 | `notice` push | A copy: the chat line is still sent. A quiet mode can follow if players ask. |
| L6 | Scheduled messages | In Nyarlathotep, with event placeholders; off by default, at most 50. |
| L7 | Order | Live-ops is planned next, beside the 0.13.0 test weekend, on its own branch; communications after the 0.13.0 release. Scaling and replacements keep their places after live-ops. |
| L8 | The player's own view | Every player can read the global board (status rows with time left, wave count and, per L2, the next-wave countdown; today's scheduled starts; the public notices) and their own `mine` rows: their standing and personal alerts, a revenge that warned them or runs against them, an event whose units are after them. No unit count, position, direction or distance, and never another player. For the requester only, this amends revenge's rule (0.12.0) that the hunted player never reaches the wire and automation D13's rule that the focus player stays in memory. Raphael may poll these reads for an overlay (handoff §5). |
