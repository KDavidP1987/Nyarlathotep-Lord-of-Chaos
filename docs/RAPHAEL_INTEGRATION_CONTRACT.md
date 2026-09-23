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
> **Current api:** none yet. The mod is v0.1.0 and answers no `api` command. api 1 ships with the
> `foundation` release (0.2.0) and api 2 with the `raphael-api` release (the Epic plan,
> `docs/dod/nyarlathotep.md`, Build plan steps 5 and 12).

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
- **At most 480 bytes per line** (under the 510-byte `FixedString512Bytes` limit). Wire lines carry **no colour
  tags**.
- The human replies (`.nyar status`, `.nyar top`, …) are **not** a wire. They are for people, may change
  wording, and Raphael should not parse them. Every piece of data Raphael needs has an `api` twin.
- **Privacy is enforced on the server, not the client:**
  - No wire line sent to a non-admin carries coordinates, zone radii or another player's position.
  - Player names appear only on `top` rows and the requester's own `me` rows.
  - Hidden players (`.nyar stats hide`) and ignored players never appear on `top`.

---

## 2. Handshake — `.nyar api version` — PLANNED (api 1)

Raphael probes it on login with its usual back-off (every 4 s, up to 12 attempts) and shows the NYAR tab group
only after an answer with `ready=1`. The answer is one line (wrapped here for reading):

```
[NYAR:version] api=1 plugin=0.2.0 ready=1 admin=0 enabled=1 killswitch=0
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

## 3. Reads — PLANNED (api 2 unless noted)

All live under `.nyar api …`. Paged commands take an optional 1-based `[page]` (§4).

### `status` — active events (anyone)
`.nyar api status` sends one row per active event, then `[NYAR:end] cmd=status`:
```
[NYAR:event] id=ashfall kind=waves name=Ashfall_Raid state=active faction=Undead left=412 wave=2/3 units=18
```
- `kind` ∈ `empower | waves | boss | zone | siege`.
- `state` ∈ `scheduled | active | ending`.
- `left` is the number of seconds left.
- `units` is sent to admins only; players get `units=-`.
- A siege row goes only to members of the target clan and to admins.
- No row ever carries a position.

### `me` — the requester's own stats (anyone, when `stats=1`)
`.nyar api me` sends three rows (windows `today`, `week`, `all`), then `[NYAR:end] cmd=me`:
```
[NYAR:me] window=week name=Vlad kills=57 events=6 waves=11 defences=1 bossadds=4 deaths=2 hidden=0
```
- `hidden=1` means the player ran `.nyar stats hide`. They still see their own numbers.
- A player with no counted activity gets one row: `[NYAR:me] window=all none=1`.

### `top` — leaderboards (anyone, when `stats=1`)
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

### `events` — definitions (admin)
`.nyar api events [page]` sends up to 10 rows, then `[NYAR:end] cmd=events page= count=`:
```
[NYAR:def] id=ashfall name=Ashfall_Raid enabled=1 trigger=schedule action=waves duration=900 state=idle reason=-
```
- `trigger` ∈ `schedule | ingame | vbloodkilled | bossengaged | bosshealth | zoneactivity | manual`.
- `action` ∈ `empower | waves | boss | siege`.
- `state` ∈ `idle | scheduled | active | disabled`.
- `reason` is the wire-safe validation error when `state=disabled`.

### `zones` — defended zones (admin; PLANNED api 2, filled by the defended-zones release)
`.nyar api zones [page]` sends up to 10 rows, then `[NYAR:end] cmd=zones page= count=`:
```
[NYAR:zone] name=Dunley_Gate x=-1520.5 z=-460.0 r=40 threshold=3 cooldown=600 state=idle
```
- `state` ∈ `idle | alert | cooldown`. Coordinates are sent to admins only.

### Push events — `.nyar api sub on|off` (anyone)
After `sub on`, the server pushes lines to that player until `sub off` or they disconnect:
```
[NYAR:ev] type=wave-warn id=ashfall secs=60 wave=2
```
- `type` ∈ `event-start | event-end | wave-warn | wave | killswitch | config-changed`.
- `id` names the event.
- `secs` is the time until the wave, or the event's length.
- `config-changed` asks Raphael to re-read `version` and `events`; it has no other payload.
- Siege events go only to subscribers who are members of the target clan, and to admins.
- The subscribe command answers `[NYAR:ok] cmd=sub on=1`.

---

## 4. Paging, acknowledgements and errors

- **Paging:** 1-based, 10 rows per page.
  - Every paged answer ends with `[NYAR:end] cmd=<cmd> page=<cur>/<total> count=<rows in all pages>`.
  - An empty result still sends `[NYAR:end] cmd=<cmd> page=1/1 count=0`.
  - Raphael asks for `cur+1` until `cur == total`.
- **Acknowledgement:** `[NYAR:ok] cmd=<cmd> …` for `api` commands that change nothing but a subscription.
- **Errors:** `[NYAR:err] cmd=<cmd> code=<code> [secs=<n>] [arg=<name>]`. The codes:

| Code | Meaning |
|---|---|
| `notready` | The world is still loading. Retry after `version` says `ready=1`. |
| `noaccess` | Admin-only command from a non-admin. |
| `disabled` | The pillar, the stats or the mod is switched off. |
| `notfound` | Unknown event id, zone or stat. |
| `badarg` | Bad argument; `arg` names it. |
| `ratelimit` | A share limit was hit; `secs` is the time until the next share is allowed. |
| `cooldown` | The kill-switch cooldown is active; `secs` is the time left. |

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
| Test spawn | `.nyar spawn <unit> [count] [level] [hp×] [power×]` | admin |
| Kill switch | `.nyar purge` then `.nyar purge confirm` (two presses, with a confirmation dialog) | admin |
| Broadcast now | `.nyar announce <text>` / `.nyar announce digest` | admin |
| Add / remove a zone at the admin's position | `.nyar zone add <name> <radius>` / `.nyar zone remove <name>` | admin |
| Reset stats | `.nyar stats reset <player\|all>` then `… confirm` | admin |
| Hide / show me on boards | `.nyar stats hide` / `.nyar stats show` | anyone |
| Share my board line | `.nyar top <stat> <window> share` | anyone, when `annshare=1` |

Every admin action is logged on the server with the admin's name. A non-admin gets VCF's standard refusal.
Raphael should grey these controls out rather than hide them.

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
- A preflight check (Epic D38, built in the `raphael-api` child) fails when a tag or `api` command in the code is
  missing here, is still marked PLANNED, or when the api numbers differ.
- The human command table lives in `docs/NYARLATHOTEP_DESIGN.md` §6; §5 above quotes only the commands a panel sends.

## 8. Request log (Raphael → Nyarlathotep)

| # | Date | Request | Status |
|---|---|---|---|
| — | — | none yet | — |
