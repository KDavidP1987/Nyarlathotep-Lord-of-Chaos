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
> **Current api:** 3
>
> api 1 shipped with the `foundation` release (0.2.0): the handshake. api 2 ships with the `raphael-api-core`
> release (0.3.0): `status`, `events` and the push subscription. `me`, `top` and `zones` stay PLANNED until the
> child that fills them (stats, defended-zones) ships; each bumps the api (the Epic plan, `docs/dod/nyarlathotep.md`, A20).
> api 3 ships with the `faction-empowerment` release (0.4.0): `status` rows of `kind=empower` (§3). It adds no tag or
> key; the change log (§9) lists every api.
>
> api 4 and later are PLANNED in §10: admin actions over the wire (raphael-api-admin, 0.5.2), then the rows each
> later child adds. Nothing in §10 is sent yet.

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
| `tpl` | tag | PLANNED (raphael-api-admin) | — |
| `pillar` | tag | PLANNED (raphael-api-admin) | — |
| `ks` | tag | PLANNED (raphael-api-admin) | — |
| `region` | tag | PLANNED (regions) | — |
| `version` | command | IMPLEMENTED | 1 |
| `status` | command | IMPLEMENTED | 2 |
| `events` | command | IMPLEMENTED | 2 |
| `sub` | command | IMPLEMENTED | 2 |
| `me` | command | PLANNED (stats) | — |
| `top` | command | PLANNED (stats) | — |
| `zones` | command | PLANNED (defended-zones) | — |
| `event` | command | PLANNED (raphael-api-admin) | — |
| `template` | command | PLANNED (raphael-api-admin) | — |
| `templates` | command | PLANNED (raphael-api-admin) | — |
| `pillar` | command | PLANNED (raphael-api-admin) | — |
| `purge` | command | PLANNED (raphael-api-admin) | — |
| `killswitch` | command | PLANNED (raphael-api-admin) | — |
| `regions` | command | PLANNED (regions) | — |

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

The switches live in `BepInEx/config/kdpen.Nyarlathotep.cfg`. They are shown read-only; v1 has no chat
command that changes them.

---

## 3. Reads

All live under `.nyar api …`. Paged commands take an optional 1-based `[page]` (§4).

### `status` — active events (anyone) — IMPLEMENTED (api 3)
`.nyar api status` sends one row per active event, then `[NYAR:end] cmd=status count=<n>` (unpaged):
```
[NYAR:event] id=ashfall kind=waves name=Ashfall_Raid state=active faction=Undead left=412 wave=2/3 units=18
```
- `kind` ∈ `empower | waves | boss | zone | siege`.
- `state` ∈ `scheduled | active | ending`. api 2 sends `active`, and `ending` for an event that has ended while its
  units wait out the grace (`left` = seconds until they despawn, `wave=-`).
- `faction` is `-` for waves events; `wave` is `<spawned>/<total>`.
- An empower row (api 3) carries `faction=<names joined by ','>`: each faction of the event without its `Faction_`
  prefix, e.g. `faction=Legion,Bandits`; its `wave` is `-`, and its admin `units` is the number of NPCs holding the
  event's empowerment. An empower event has no `ending` row.
```
[NYAR:event] id=legion-surge kind=empower name=Legion_Surge state=active faction=Legion,Bandits left=1500 wave=- units=42
```
- `left` is the number of seconds left.
- `units` is sent to admins only; players get `units=-`.
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
[NYAR:def] id=ashfall name=Ashfall_Raid enabled=1 trigger=schedule action=waves duration=900 state=idle reason=-
```
- `trigger` ∈ `schedule | ingame | vbloodkilled | bossengaged | bosshealth | zoneactivity | manual`.
- `action` ∈ `empower | waves | boss | siege`: the definition's pillar action, sent even when validation disabled a
  definition for a missing action.
- `state` ∈ `idle | scheduled | active | disabled`.
- `reason` is the wire-safe validation error, or `disabled` for a definition switched off, when `state=disabled`;
  `-` otherwise.
- `name` is cut to 64 UTF-8 bytes and `reason` to 120, on a character boundary, so every key fits the line.

### `zones` — defended zones (admin) — PLANNED (defended-zones)
`.nyar api zones [page]` sends up to 10 rows, then `[NYAR:end] cmd=zones page= count=`:
```
[NYAR:zone] name=Dunley_Gate x=-1520.5 z=-460.0 r=40 threshold=3 cooldown=600 state=idle
```
- `state` ∈ `idle | alert | cooldown`. Coordinates are sent to admins only.

### Push events — `.nyar api sub on|off` (anyone) — IMPLEMENTED (api 2)
After `sub on`, the server pushes lines to that player until `sub off`, a disconnect or a server restart. Subscriptions live only in memory: a reconnect starts unsubscribed, so Raphael sends `sub on` again after every successful handshake. Lines go only to connected subscribers:
```
[NYAR:ev] type=wave-warn id=ashfall secs=60 wave=2
```
- `type` ∈ `event-start | event-end | wave-warn | wave | killswitch | config-changed`.
- `id` names the event; it is `-` for `killswitch` and `config-changed`.
- `secs` is the event's length for `event-start`, the time until the wave for `wave-warn`, the purge cooldown for
  `killswitch`, and 0 for `event-end`, `wave` and `config-changed`. `wave=<n>` follows it on `wave` and `wave-warn`.
- `config-changed` asks Raphael to re-read `version` and `events`; it has no other payload.
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
- **Errors:** `[NYAR:err] cmd=<cmd> code=<code> [secs=<n>] [arg=<name>]`. The codes:

| Code | Meaning |
|---|---|
| `notready` | Reserved, never sent (api 2 and 3): before the world is ready every command replies the plain line `still loading`, and no player can connect before then. |
| `noaccess` | Reserved. Admin-only commands are refused by VCF before the mod runs, with VCF's own human-readable line, so Raphael shows admin panels only when `version` says `admin=1`. |
| `disabled` | The pillar, the stats or the mod is switched off. |
| `notfound` | Unknown event id, zone or stat. |
| `badarg` | Bad argument; `arg` names it. |
| `ratelimit` | A share limit was hit; `secs` is the time until the next share is allowed. |
| `cooldown` | The kill-switch cooldown is active; `secs` is the time left. |

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

Raphael changes things by sending the **human** commands and then re-reading state (`api events`,
`api status`, or waiting for `[NYAR:ev] type=config-changed`). It does not parse the human replies. This keeps a
single command surface, which `docs/NYARLATHOTEP_DESIGN.md` §6 lists in full. The ones a panel needs:

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
Raphael should grey these controls out rather than hide them. api 4 (§10.1) gives each admin action a wire twin that
answers `[NYAR:ok]` or `[NYAR:err]`; until the handshake reports `api>=4`, Raphael keeps sending the human commands.

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
- **From api 4 (§10):** Admin › Templates, Admin › Pillars, a region picker, and a live horde and anti-farming readout.

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
| 4 (planned) | 0.5.2 (raphael-api-admin) | 2026-09-28 api 4 planned: admin action twins, `templates`, `pillar`, `killswitch`, error codes `exists`, `state`, `invalid`, `full`, `io`, `confirm` (§10). Later children add rows with their own api number. |

---

## 10. api 4 and later — PLANNED

Nothing in this section is sent yet. Every shape is PLANNED under the child named with it, and can change until that
child ships. The Tags and commands table carries one row per new tag and command. A child that grows the wire bumps
`api` by one when it ships, so the number is assigned then. This section names the child and its release, in the
Epic's order, not the api. The first bump is api 4, with raphael-api-admin (0.5.2).

| Child | Release | Adds |
|---|---|---|
| raphael-api-admin | 0.5.2 | admin action twins (§10.1); the `templates`, `template info`, `pillar list` and `killswitch` reads (§10.2); six error codes (§10.3) |
| regions | 0.6.0 | the `regions` read; `region=` on `[NYAR:def]` and `[NYAR:event]` rows and on `event-start` and `event-end` pushes (§10.4) |
| event-spawns | 0.7.0 | new settable fields (hunt, modifiers, AroundPlayer, loot), set through the `event set` twin; no new tag |
| boss-reinforcements | 0.8.0 | the push `boss-adds` |
| anti-farming | 0.9.0 | the push `farm-tier`; `zones` and `zone`, relabelled from defended-zones, which it absorbs |
| outbreak | 0.10.0 | the pushes `horde-wave`, `horde-boss-down` and `horde-spread` |

The sieges and castle-takeover child adds its rows when it is planned. Siege pushes keep §3's rule: they go only to
members of the target clan and to admins.

### 10.1 Admin action twins — PLANNED (raphael-api-admin)

Each twin is `.nyar api` followed by the human command of §5, with the same arguments. It runs through the same
gateway as the human command: the same validation, the same admin log line, and the same pushes (`config-changed`,
`event-start`, `event-end`, `killswitch`). It answers exactly one line:
- on success, `[NYAR:ok] cmd=<cmd> verb=<verb> id=<id> …`;
- on refusal, `[NYAR:err] cmd=<cmd> verb=<verb> code=<code> [secs=<n>] [arg=<name>] [reason=<text>]`.

`id` names the object: the event, the pillar, or `-` for purge. The twins are admin-only. As in §4, VCF refuses a
non-admin with its own human line.

| Twin | Success line |
|---|---|
| `.nyar api event start <id>` / `… stop <id>` | `[NYAR:ok] cmd=event verb=start id=<id>` (`verb=stop`) |
| `.nyar api event enable <id>` / `… disable <id>` | `[NYAR:ok] cmd=event verb=enable id=<id> changed=<0\|1>` |
| `.nyar api event set <id> <field> <value>` | `[NYAR:ok] cmd=event verb=set id=<id> field=<field> value=<stored value>` |
| `.nyar api event set <id> location here` | `… field=location value=<x>,<z>`, from the admin's position |
| `.nyar api event reload` | `[NYAR:ok] cmd=event verb=reload id=- count=<definitions loaded>` |
| `.nyar api event new <id> <pillar>` | `[NYAR:ok] cmd=event verb=new id=<id> pillar=<pillar>`; the event is created disabled |
| `.nyar api event copy <id> <newId>` | `[NYAR:ok] cmd=event verb=copy id=<newId> from=<id>`; the copy is disabled |
| `.nyar api event delete <id>`, then `… delete <id> confirm` | `… verb=delete id=<id> confirm=30`, then `… verb=delete id=<id> done=1` |
| `.nyar api template use <template> [as <id>]` | `[NYAR:ok] cmd=template verb=use id=<id> tpl=<template>` |
| `.nyar api pillar <name> on\|off` | `[NYAR:ok] cmd=pillar verb=set id=<name> on=<0\|1> changed=<0\|1>`; saved to the cfg |
| `.nyar api purge`, then `.nyar api purge confirm` | `[NYAR:ok] cmd=purge verb=ask id=- confirm=30`, then `[NYAR:ok] cmd=purge verb=confirm id=- events=<ended> units=<to despawn> secs=<cooldown>` |

Rules:
- **Idempotency:**
  - `enable`, `disable` and `pillar` repeated on the state they already hold answer `ok` with `changed=0`.
  - `start` on an active event and `stop` on one that is not active answer `code=state`. This is the engine's
    one-instance rule.
- **Two-step confirm:** the confirm must come from the same admin within 30 s of the ask; otherwise it answers
  `code=confirm`. `confirm=<secs>` on the ask is that window. Raphael shows its confirmation dialog between the two
  presses.
- **After an `ok`:** Raphael may update its state on the `ok` line and treat the push that follows as confirmation.
  A refusal changes nothing on the server.
- **Rate:** at most 5 twins per admin per second. Past that the twin answers `code=ratelimit secs=1`. This number is
  planned; the child's plan settles it.
- **Line length:** every line fits 480 bytes. `value` and `reason` are cut to 120 bytes on a character boundary.

```
[NYAR:ok] cmd=event verb=set id=undead-nightfall field=duration value=1800
[NYAR:ok] cmd=event verb=enable id=undead-nightfall changed=1
[NYAR:ok] cmd=purge verb=ask id=- confirm=30
[NYAR:err] cmd=event verb=start code=state arg=id reason=already_active
[NYAR:err] cmd=event verb=start code=cooldown secs=240
[NYAR:err] cmd=template verb=use code=exists arg=id
```

### 10.2 New reads — PLANNED

| Read | Child | Rows | Who |
|---|---|---|---|
| `.nyar api templates [pillar] [page]` | raphael-api-admin | `[NYAR:tpl] id=<template> pillar=<pillar> trigger=<kind> duration=<secs> summary=<text>`, then `[NYAR:end] cmd=templates page= count=` | admin |
| `.nyar api template info <template>` | raphael-api-admin | the same single row, then `[NYAR:end] cmd=template count=1`; an unknown template answers `code=notfound arg=template` | admin |
| `.nyar api pillar list` | raphael-api-admin | `[NYAR:pillar] id=<name> on=<0\|1>` for `empowerment`, `spawns`, `boss`, `zones` and `sieges`, then `[NYAR:end] cmd=pillar count=5` | admin |
| `.nyar api killswitch` | raphael-api-admin | `[NYAR:ks] on=<0\|1> secs=<cooldown left> events=<active events> units=<tracked units>`, then `[NYAR:end] cmd=killswitch count=1` | admin |
| `.nyar api regions [page]` | regions | `[NYAR:region] id=<region> events=<active events scoped to it>`, then `[NYAR:end] cmd=regions page= count=` | anyone |
| `.nyar api zones [page]` | anti-farming | §3's shape; the row is relabelled when anti-farming is planned | admin |

- `summary` is cut to 120 bytes.
- The region ids are the game's region names, as `.nyar region list` prints them (the regions child).
- A `[NYAR:def]` row gains `region=<id,…>`, or `-` for a global event, from the regions release. A new key is
  additive (§7), so an api 3 parser skips it.

```
[NYAR:tpl] id=undead-nightfall pillar=spawns trigger=gametime duration=1800 summary=Undead_rise_at_night
[NYAR:pillar] id=empowerment on=1
[NYAR:ks] on=1 secs=240 events=0 units=12
[NYAR:region] id=CursedForest events=1
```

### 10.3 Error codes — PLANNED (raphael-api-admin)

The twins reuse §4's codes and add six. When the twins ship, the new codes move into §4's table and the `WireError`
enum together.

| Code | Meaning |
|---|---|
| `exists` | `new`, `copy` or `template use` named an id that is taken. |
| `state` | The event is in the wrong state: `start` on an active event, `stop` on one that is not active, or `delete` of a running event. |
| `invalid` | The change would fail validation. `arg` names the field and `reason` gives the validator's wire-safe message. |
| `full` | The definitions file already holds its 200 definitions. |
| `io` | The definitions file or the cfg could not be saved. Nothing changed. |
| `confirm` | A confirm arrived with no ask from the same admin in the last 30 s. |

The §4 codes the twins also send:
- `disabled`, where the human command refuses because the mod or the pillar is off;
- `notfound`, for an unknown event, template, pillar or region, with `arg` naming which;
- `badarg`, for a missing or malformed argument;
- `ratelimit`, past the rate;
- `cooldown`, for a manual start during the purge cooldown, with `secs` the time left.

### 10.4 Push events — PLANNED

New `[NYAR:ev]` types and keys. §3's fairness rule holds for all of them: a push never says more than chat or
`.nyar status` tells every player. The new keys follow `secs`, which is 0 for every new type.

| Type or key | Child | Payload |
|---|---|---|
| `region=<id,…>` on `event-start` and `event-end` | regions (0.6.0) | the event's regions, or `-` for a global event |
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
[NYAR:ev] type=event-start id=undead-nightfall secs=1800 region=CursedForest
[NYAR:ev] type=horde-wave id=undead-nightfall secs=0 wave=3 count=60 region=-
[NYAR:ev] type=farm-tier id=anti-farm secs=0 tier=2 region=FarbaneWoods
```
