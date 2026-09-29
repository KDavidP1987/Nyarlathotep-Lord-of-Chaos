# Raphael handoff — api 4 (Nyarlathotep 0.5.2) and what comes next

This page is for a session working **in the Raphael workspace**. It says what Raphael can build against the
wire Nyarlathotep ships (**api 4**, Nyarlathotep 0.5.2; api 3 servers are 0.5.0 and 0.5.1), and what to build when the
later children land. The wire
is specified in `docs/RAPHAEL_INTEGRATION_CONTRACT.md`: §1–§5a for what ships, §10 for what is planned. The first
handoff, `docs/RAPHAEL_HANDOFF.md`, covers the parser, the handshake and the api 2 panels; this page doesn't repeat
it.

Nyarlathotep never edits the Raphael workspace, so this page is not copied there. Open it from this repository.

> **api 4 is IMPLEMENTED in the contract** (§3, §4, §5a) and ships in Nyarlathotep 0.5.2. Everything under "When the
> later children land" is PLANNED: a PLANNED row can change until the child that owns it ships and marks it
> IMPLEMENTED. Build each part behind its `api>=N` gate, and re-read contract §9 and §10 before starting it.

## What api 3 servers have (Nyarlathotep 0.5.0 and 0.5.1; api 4 has all of it)

| Command | Answer | Who |
|---|---|---|
| `.nyar api version` | `[NYAR:version] api=3 …`, the switches of contract §2 | anyone |
| `.nyar api status` | `[NYAR:event]` rows, then `[NYAR:end] cmd=status count=<n>` | anyone |
| `.nyar api events [page]` | `[NYAR:def]` rows, 10 per page, then `[NYAR:end] cmd=events page=<cur>/<total> count=<n>` | admin |
| `.nyar api sub on\|off` | `[NYAR:ok] cmd=sub on=1\|0`, then `[NYAR:ev]` pushes | anyone |

- api 3 added `kind=empower` status rows (faction empowerment) and no new tag or key.
- The admin actions are human commands only (contract §5), including the 0.5.0 ones: `event new`, `event copy`,
  `event delete … confirm`, `event set <id> location here`, `template list|info|use`, `pillar list|<name> on|off`.
  They answer in human lines, which Raphael must never parse.

## Build now, against api 3

The read-only panels need nothing new from the server:
- **Events board** (everyone): `api status` plus the push stream, with a countdown per wave, the `ending` grace
  shown as its own state, and `faction` on empower rows.
- **Admin › Events list:** `api events`, paged, with each definition's `state` and `reason`.
- **Subscription:** `sub on` after every handshake with `ready=1`, and the re-read rules of `docs/RAPHAEL_HANDOFF.md`.
- **Admin actions, for now:** buttons that send the human commands of contract §5, then wait for the push
  (`config-changed`, `event-start`, `event-end`, `killswitch`) and re-read. This is the path to replace when api 4
  lands, so keep the send in one place (for example `NyarClient.Admin(string humanCommand)`).

## When api 4 lands (raphael-api-admin, 0.5.2)

api 4 ships in Nyarlathotep 0.5.2. Every admin action gets a wire twin (contract §5a): `.nyar api event <verb> …`,
`.nyar api template use <template> [as <id>]`, `.nyar api pillar <name> on|off` and `.nyar api purge [confirm]`, the
human command with `.nyar api` in front. Each twin answers exactly one `[NYAR:ok] cmd= verb= id= …` or
`[NYAR:err] cmd= verb= code= [secs=] [arg=] [reason=]` line. Switch the send path to the twins when the handshake
reports `api>=4`, and keep the human path for older servers. The twins are limited to 5 a second per admin; past
that they answer `code=ratelimit secs=1`, so a bulk edit is spread out.

| Panel | Built from |
|---|---|
| **Event manager** | the twins `event start`, `stop`, `enable`, `disable`, `set` (including `set <id> location here`), `new`, `copy`, `delete` with its confirm, `reload`; a field editor that shows `code=invalid`'s `arg` and `reason` next to the field |
| **Template picker** | `api templates [pillar] [page]` for the list, `api template info <template>` for one row, then the twin `template use <template> [as <id>]`; `code=exists` asks for another id |
| **Pillar switches** | `api pillar list`, and the twin `pillar <name> on\|off` |
| **Purge** | the twin `purge` answers `confirm=30`. Show the confirmation dialog, then send `purge confirm` inside the window. `code=confirm` means the window passed. |
| **Kill switch status** | `api killswitch`: `on`, the cooldown `secs`, and the active events and tracked units |

Handling the answers:
- Match an `ok` or `err` line to the button on `cmd` and `verb`. Update the panel from the `ok` line, and treat
  the push that follows as confirmation.
- The new error codes are `exists`, `state`, `invalid`, `full`, `io`, `confirm` and `limit` (contract §4), each
  with its `reason` word (§5a). The api 2 codes still apply. Show `secs` as a countdown on `cooldown` and `ratelimit`.
- `code=io reason=internal` means the action failed on the server, possibly after it changed something; its log
  names the exception. Nothing is rolled back, so re-read state (`api status`, `api events`, `api pillar list`,
  `api killswitch`) before showing the result. No push is promised after `internal`; re-read, then re-send only if the state shows the action did not apply (a repeated start answers `code=state`, a repeated purge confirm `code=confirm` or `state reason=nothing_to_purge`).
- `changed=0` on `enable`, `disable` or `pillar` means that state was already set. Nothing is wrong.

## When api 5 lands (regions, 0.6.0)

api 5 ships in Nyarlathotep 0.6.0 (contract §3, §9). Gate on `api>=5` from the handshake.
- **Region picker:** `.nyar api regions` lists the ten region ids with the active events scoped to each
  (`[NYAR:region] id=CursedForest events=1`). Show them by their display name: the id split at capitals, `_` as a
  space (`Gloomrot_South` is "Gloomrot South").
- **Scope in Admin › Events:** `[NYAR:def]` rows end with `region=<id,…>`, or `-` for a global event. The field editor
  sets `trigger.scope` or `action.scope` with the `event set` twin, the value `Global` or ids joined by `,`.
- **Events board:** `[NYAR:event]` rows and the `event-start` and `event-end` pushes carry the same `region=`; show
  "global" for `-`. The key names the event's configured regions, never where a player is.
- Two new refusal reasons: `no_player_in_region` (a regional start with no player in the regions) and `out_of_region`
  (a kill or the admin outside them). Show the human text; re-read `api status` as for any refusal.

## When the later children land

| Child (release) | Raphael adds |
|---|---|
| regions (0.6.0) | shipped as api 5: see "When api 5 lands" above |
| event-spawns (0.7.0) | new fields in the field editor (hunt, modifiers, AroundPlayer, loot), set through the `event set` twin. No new tag. |
| boss-reinforcements (0.8.0) | a `boss-adds` line on the Events board: the boss, the phase, and how many adds joined |
| anti-farming (0.9.0) | `farm-tier` on the live readout: the event, the tier, and the region only. Never show or guess a player or a position. Admin › Zones comes back with `api zones`. |
| outbreak (0.10.0) | **Live horde readout:** `horde-wave` (wave, units, region), `horde-spread` (hotspots, region), and `horde-boss-down`, where `empowered=0` means the horde lost its boss empowerment |

Every new push type still follows the fairness rule: it never says more than chat tells every player. Show only
what the line carries.

## Requests (contract §8)

If a panel needs a shape, key or command that isn't in the contract, add a row to contract §8 in this repository.
Give it the date, the request and the status `open`. The next child's plan picks the row up. A request made before
a child is planned can still change that child's shapes; api 4's are IMPLEMENTED and only grow (contract §7).
- Never work around a missing key by parsing a human reply.
- To report a wire bug, attach the client log line (with `NyarDiagnostics` on) and the server's `LogOutput.log`
  lines from the same minute.
