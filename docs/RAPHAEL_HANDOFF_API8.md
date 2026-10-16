# Raphael handoff — api 8 (Nyarlathotep 0.13.0), live operations and notifications

This page is for a session working **in the Raphael workspace**. It says how to build a complete Nyarlathotep
console in Raphael against **api 8**, the wire Nyarlathotep 0.13.0 ships (test build `v0.13.0-rc.1` already speaks
it), and what to add when the two proposed children, **live-ops** and **communications**, ship.

Take two files to the Raphael session: this page and `docs/RAPHAEL_INTEGRATION_CONTRACT.md`. The contract is the
source of truth for every line shape; this page gives section numbers instead of repeating them. The earlier
handoffs still apply: `docs/RAPHAEL_HANDOFF.md` (parser, handshake, api 2 panels) and `docs/RAPHAEL_HANDOFF_API4.md`
(admin twins, templates, pillars, kill switch).

> **Status.** §1-§4 of this page are IMPLEMENTED on the server (api 8): build them now. §5-§6 are PLANNED (contract
> §10.5-§10.8). The owner settled their decisions on 2026-10-15 (contract §10.9): live-ops is planned next, and
> communications follows the 0.13.0 release. Neither has a dod plan yet, so a shape can still change. Build them behind
> `api >= 9` / `api >= 10` gates (the numbers are assigned when each child ships), and re-read the contract first.

---

## 1. What changed since the api 4 handoff

| Api | Nyarlathotep | What Raphael does with it |
|---|---|---|
| 5 | 0.6.0 regions | `api regions` read and `[NYAR:region]` rows; `region=` last on `[NYAR:def]` and `[NYAR:event]` rows and after `secs` on `event-start` / `event-end` pushes. Show the regions as chips; `-` means global. |
| 6 | 0.8.0 automation | `trigger` values `interval`, `regionentered`, `factionkills`. Label them; no new key. |
| 7 | 0.12.0 revenge | `trigger` value `revenge`. Label it; heat, the hunted player and positions never reach the wire. |
| 8 | 0.13.0 unit watch | Twin `.nyar api event unstick <id>` → `[NYAR:ok] cmd=event verb=unstick id= marked= fighting=`; reason `watch_off`. |

Features since 0.8.0 that add **nothing** to the wire, so Raphael needs no change for them: wave sets (`wave=` total is
the list length), swarm (arena, winner, growth), hunting grounds, apocalypse (`kind=waves`, `wave=<hordes risen>/<hordes>`,
outcome = the usual `event-end`), final-wave bosses. Their chat lines (scoreboard, horde rises, outcome) still reach
players as chat.

Compatibility points:
- **Pre-release versions.** `plugin=` can carry a suffix: `plugin=0.13.0-rc.1`. Parse semver with an optional
  `-suffix`, and gate features on `api` only, never on `plugin`. Nobody has checked yet whether Raphael accepts the
  suffix; that is the first test in §7.
- **Unknown enum values** (`trigger`, `kind`, `state`, push `type`): show the raw word or skip the row, never fail
  (contract §1).
- **`announce digest` is not built.** It broadcasts the word "digest" as text. Don't send it (contract §5).
- **Admin actions with no twin** on api 8: `announce`, `spawn`, `debug here`, `event info`. Send them as human commands
  and let the reply show in chat; never parse it (contract §5).

---

## 2. What to pull, and when (api 8)

### On login
1. Probe `.nyar api version` every 4 s, up to 12 tries. `still loading` counts as no answer. Show the NYAR tab group
   only after `ready=1`.
2. Then, in this order, one at a time (each finishes with its `[NYAR:end]` before the next is sent):
   `api sub on` → `api status` → `api regions`.
3. When `admin=1`: `api events` (all pages) → `api pillar list` → `api killswitch` → `api templates` (all pages).
4. **Login toast:** if `api status` returned rows, show one toast, for example "2 events under way: Ashfall Raid
   (6 min), Legion Surge (25 min)". This covers players who log in during an event, for Raphael users, on api 8. The
   server-side login notice for everyone is planned (§6).

Subscriptions live only in server memory, so repeat the whole sequence after every reconnect and after a server
restart (the handshake fails, then succeeds again).

### On each push (`[NYAR:ev]`, contract §3)

| `type` | Update | Notify |
|---|---|---|
| `event-start` | add the event; re-read `api status` for its row | toast "‹name› has begun" (+ region) |
| `wave-warn` | start a countdown from `secs` | toast "Wave ‹n› in ‹secs› s" |
| `wave` | re-read `api status` (the wave count and units) | optional small toast |
| `event-end` | remove the event; re-read `api status` (an `ending` row can follow, units leaving) | toast "‹name› is over" |
| `killswitch` | re-read `api killswitch` and `api status` | banner with the cooldown countdown |
| `config-changed` | re-read `api version` and `api events`; admins also `api pillar list` | none |

Re-read `api status` whenever a push names an event Raphael does not know: pushes queue 50 lines at most and drop the
oldest.

### Periodic refresh
- **Countdowns run locally**: decrement `left` and `secs` every second. Don't poll to count down.
- `api status` every 60 s while a NYAR panel or the event widget is visible, every 5 min otherwise. This only corrects
  drift; the pushes carry the changes.
- `api killswitch` every 5 s while `on=1`.
- Nothing else is polled on api 8.

### Budget
- Every row is one chat message to the requesting client. Send at most one read every 250 ms, and finish paging one
  read before starting the next.
- Twins: at most 5 per admin per second on the server (the sixth answers `code=ratelimit secs=1`). Queue them on the
  client and send one every 250 ms.
- When `admin` turns to 0 (a re-handshake), drop every cached admin row.

---

## 3. Panels to build now (api 8)

### Players
- **Events board** (`api status`): name, kind, faction(s), region chips, time left (countdown), `wave n/m`; `ending`
  rows read "units leaving". `units` is `-` for players. Siege rows only reach the target clan and admins.
- **Regions**: the ten regions with their active-event counts (`api regions`).
- **Notifications**: the toasts of §2 and §4, with per-category switches in Raphael's cfg (§4).

### Admins (`admin=1`; grey them out otherwise)
- **Events** (`api events`): state, trigger, action, duration, region, `reason` when disabled. Buttons, each a §5a twin:
  start, stop, enable, disable, **unstick** (running events only), copy, delete (two presses with a dialog), new
  (id + pillar), reload.
- **Field editor**: a field picker from the settable-field table in `docs/NYARLATHOTEP_DESIGN.md` §6 (60+ fields; a
  trigger field needs its trigger type and an action field its action type), a value box, and a "location here"
  button. On success the `ok` line echoes the stored value. **Gap:** api 8 cannot read a definition's current field
  values. Until live-ops' `fields` read (§5), add a "Show in chat" button that sends the human `.nyar event info <id>`
  so the server's reply appears in chat for the admin to read.
- **Live event view**: what the status row gives (time left, waves, units), plus "Show in chat" → `.nyar event info
  <id>`, whose reply has the run lines (waves, apocalypse hotspots and fronts, unit watch counts).
- **Units**: no wire read on api 8. A "Units near me" button sends the human `.nyar debug here [radius]`; the reply
  lists tracked units in chat. A "Test spawn" form sends the human `.nyar spawn <unit> [count] [level] [hp] [power]`.
- **Templates** (`api templates [pillar]`, `api template info`): browse by pillar, "Create from template" with an
  optional id (`template use <tpl> as <id>`). The new event is created disabled.
- **Pillars** (`api pillar list`): five switches. Turning one off ends its running events (`ended=` on the `ok` line).
- **Kill switch** (`api killswitch`): Purge → confirmation dialog → confirm within 30 s; then the cooldown countdown.
- **Announce**: a text box (1-200 characters, no `<`, `>` or control characters, checked on the client) that sends
  the human `.nyar announce "<text>"`, quoted.

### Errors
Show `[NYAR:err]` as a short message from `code` and `reason` (contract §4 and §5a's reason list), for example
`state` + `already_active` → "Already running"; `cooldown` + `secs` → "Kill-switch cooldown: 4 min left";
`invalid` + `arg=<field>` → "‹field› is not valid: ‹reason›"; `io` + `internal` → "Server error: re-reading state", then
re-read status, events, pillars and the kill switch.

---

## 4. Notifications and communications

### What the server says today, to whom, and what Raphael can do with it

| Message | Reaches | Server switch | Push on api 8 | Raphael on api 8 |
|---|---|---|---|---|
| Event start banner | every player online | `EventBanners` | `event-start` (always) | toast |
| Revenge faction alert at a revenge start | every player online | none (revenge D7) | `event-start` | toast |
| Wave warning | every player online | `WaveWarnings` + the event's `announce.warnings` | `wave-warn` | toast + countdown |
| Wave cleared (wave sets) | every player online | none | — | chat only |
| Apocalypse horde rises, march steps, outcome | every player online | none | `event-end` for the outcome | chat; toast on `event-end` |
| Scoreboard | every player online | the event's `action.scoreboard` | — | chat only |
| Event end banner | every player online | `EventBanners` | `event-end` (always) | toast |
| Daily banner (today's scheduled events) | every player online | `DailyBanner` at `DailyBannerTime` | — | chat only |
| Admin broadcast (`.nyar announce`) | every player online | admin | — | chat only |
| Revenge heat warnings | the one player | the revenge definition's `warnAt` | — | chat only (private) |
| Degraded notice at login | that admin only | none | — | chat only (private) |
| Kill switch | subscribers | none | `killswitch` | banner |

The switches are in `[Announcements]` of `BepInEx/config/kdpen.Nyarlathotep.cfg`; the handshake reports five of them
(`annwarn annbanner anndaily annlogin annshare`). `annlogin` and `annshare` belong to the unbuilt stats child and do
nothing yet.

### The three audiences
1. **One player (private):** revenge warnings and the admin degraded notice are chat only today. Planned: `notice
   to=you` pushes and the login event notice (§6).
2. **Everyone online:** start, end, wave warning and kill switch have pushes today, so Raphael can show toasts. The
   other public lines are chat only until the `notice` push (§6).
3. **Players who log in during an event:** on api 8, Raphael's own login toast from `api status` (§2). Planned: the
   server's private login notice for every player, Raphael or not, and the `notices` read so Raphael can show what was
   said before the player arrived (§6).

### Raphael's notification settings (client cfg)
`NyarToasts` (on/off), and one switch per category: event start/end, wave warnings, kill switch, login summary, and
later admin broadcasts, scoreboards, horde news and private lines. A toast duration. Admin-only categories (health,
config changes) show only when `admin=1`.

**Recurring and scheduled messages** are not on the server yet. Until communications ships, an admin repeats
`.nyar announce`. Raphael should **not** schedule broadcasts from the client: they would stop whenever that admin
logs out.

---

## 5. When live-ops ships (PLANNED, contract §10.5-§10.7)

The reads that make the admin console live. Gate on the api the child ships as.

| Panel | Read / push | Refresh |
|---|---|---|
| Field editor with current values | `api fields event <id>`, `api fields template <tpl>`; join `part=k/n` rows | on opening, and after every `set` ok |
| Live event | `api run <id>`: time left, waves `n/m`, next wave in, alive, queued, killed, expired, unit watch counts; empower `holding`/`pending`; apocalypse hordes, roamers, hotspots; `[NYAR:spot]` map pins with exact coordinates (hotspots, horde fronts, goal) | every 5 s while open; on each `progress` push |
| Units (global lookup) | A toggle between **one event** (`api units event=<id>`) and **all events** (`api units`, every spawned unit across every event and test spawn), plus filters `src=spawn\|empower prefab= region= watch= near=`; `api units summary`; `api unit <key>`. Columns: prefab, event, wave, level, HP, time left, watch state, region and exact `x y z` with a copy button; map pins. Actions: despawn (spawned units only), unstick, **go to** (teleports the admin beside the unit) | on demand; every 10 s while open |
| Schedule | `api schedule`: next start (`in`, `at` UTC), cooldowns, why not (`pillar_off`, `cooldown`, `night`…) | every 60 s while open; on `config-changed` |
| Health | `api health`: tracked/max, queues, subscribers, degraded parts | every 30 s while open; on `health` push |
| Events board | `next=` on status rows: admins always, players only for events with chat wave warnings on | as today |
| My events (players) | `api mine`: `type=standing` rows (own kills, deaths, rank) and `type=alert` rows (a revenge that warned them or runs against them, an event whose units are after them) | see the player overlay below |

`config-changed` gains `what=events|pillars|messages|announce`; re-read only that part. `wave-cleared` gives a toast.

### The player overlay (contract §10.5, decision L8)

A small always-on panel any player can turn on, in two parts. It shows what every player may know, and nothing about
units or places.

| Part | Rows | Shows | Source |
|---|---|---|---|
| **World** | one per running event | name, faction, region chips, time left (countdown), wave `n/m`, "next wave in" when `next` is sent (admins always, players for events with chat warnings on), "starting soon" countdowns for today's scheduled starts | `api status`, `api schedule` (player form), pushes |
| **You** | one per personal alert | "The Legion is hunting you" (`kind=revenge state=warned`: be watchful), "Revenge of the Legion: 9 min, wave 1/3" (`state=active`), "Bandits are coming for you: 5 min, wave 2/2" (`kind=hunted`); the player's own standing in scoreboard events | `api mine`, the `personal` push |

The overlay never shows a unit count, a direction, a distance or a map marker for players. Those are admin data.

Refresh while the overlay is visible:
- **Countdowns run locally** from `left`, `next` and `secs`.
- `api status` and `api mine` every 15 s, one after the other. Re-read them at once on any `event-start`, `event-end`,
  `wave`, `progress` or `personal` push.
- `api schedule` every 5 min, and on `config-changed`.
- With the overlay hidden, fall back to §2's refresh. Each player sends two small reads per 15 s. Live-ops' plan has to
  show the server copes with that at its 128-subscriber cap; until then, treat 15 s as the fastest rate.
- A personal alert that appears (`personal` push with `state=warned` or `active`) also raises a toast, styled as a
  private message.

---

## 6. When communications ships (PLANNED, contract §10.6-§10.8)

| Feature | Raphael |
|---|---|
| `notice` push: a copy of every chat line the server sends to that player, with `kind` and `to`; the chat line still shows | toasts by category, styled by `to` (`you` = private). Decode the text escape (contract §10.8): `_` → space, then `%XX` |
| `api notices` | on login, after `api status`: show the public notices of events still running |
| Login event notice (`LoginEvents`) | arrives as `notice kind=login to=you`; merge it with Raphael's own login toast so it doesn't show twice |
| Start warnings (`announce.soon`) | `event-soon` push → a countdown "begins in ‹n› min" on the Events board |
| Editable announce text | the field editor gets `announce.start`, `announce.end` (lines joined by `\|`), `announce.warnings`, `announce.soon` |
| Admin › Messages | `api messages`; add, edit (`when`, `target`, `text`), enable/disable, delete with confirm, "Send to me" (`message test`); help text for `{events}`, `{next}`, `{event}`, `{left}` |
| Admin › Announce | the `announce` twin replaces the human command; `api ann` shows the warning offsets, daily banner time (convert with `tzmin`) and the switches |

---

## 7. Testing against a server

Server: the local dev server `127.0.0.1:9876` running `v0.13.0-rc.1` (or a later rc) with the five pillars on. Turn on
`NyarDiagnostics` in Raphael and compare every `[NYAR:*]` line in the client log with the contract.

1. **Handshake with a suffix:** connect; the client log shows `[NYAR:version] api=8 plugin=0.13.0-rc.1 ready=1`. PASS: the
   NYAR tab group appears.
2. **Templates:** Admin › Templates → spawns → `graveyard-waves` → Create. PASS: `[NYAR:ok] cmd=template verb=use
   id=graveyard-waves tpl=graveyard-waves`, and the event appears disabled in Admin › Events.
3. **Field edit:** set `durationSeconds` to `600` on `graveyard-waves`. PASS: `ok … field=durationSeconds value=600`,
   then a `config-changed` push and a refreshed list.
4. **Run an event:** enable and start `graveyard-waves`. PASS: `event-start` toast with region; status row with a
   counting-down `left` and `wave 1/n`; `wave-warn` toasts when the cfg has `WaveWarnings=true`.
5. **Unstick:** while it runs, press Unstick. PASS: `ok … verb=unstick id=graveyard-waves marked=<n> fighting=<f>`, or
   `err … code=invalid reason=watch_off` when `[UnitWatch] Enabled` is false.
6. **Errors:** press Start again. PASS: "Already running" from `code=state reason=already_active`.
7. **Kill switch:** Purge, confirm. PASS: one `killswitch` push (a purge sends no `event-end` per event: the client
   clears every running row on `killswitch`), a cooldown banner counting down, and `api killswitch` back to `on=0` when
   it ends. Corrected 2026-10-16: this step said `event-end` pushes came too.
8. **Login toast:** start `graveyard-waves` again, disconnect, reconnect. PASS: one toast listing the running event.
9. **Empowerment:** create `legion-weekend-surge` from a template and start it. PASS: a `kind=empower` row with
   `faction=` and, for admins, `units=` > 0.

## 8. Asking Nyarlathotep for more

Ask through the owner. A request lands in the contract's §8 request log, and the shape is then specified in §10 before
it is built. Live-ops and communications are specified in contract §10.5-§10.8, with the owner's decisions in §10.9.
