# Raphael handoff — api 3 today, api 4 next (Nyarlathotep 0.5.x)

This page is for a session working **in the Raphael workspace**. It says what Raphael can build now against the
wire Nyarlathotep ships today (**api 3**), and what to build when **api 4** and the later children land. The wire
is specified in `docs/RAPHAEL_INTEGRATION_CONTRACT.md`: §1–§5 for what ships, §10 for what is planned. The first
handoff, `docs/RAPHAEL_HANDOFF.md`, covers the parser, the handshake and the api 2 panels; this page doesn't repeat
it.

Nyarlathotep never edits the Raphael workspace, so this page is not copied there. Open it from this repository.

> **Everything under "When api 4 lands" is PLANNED.** A PLANNED row in the contract can change until the child
> that owns it ships and marks it IMPLEMENTED. Build it behind an `api>=4` gate (or the api the change log gives
> that child), and re-read contract §10 and §9 before starting each part.

## What exists today (api 3, Nyarlathotep 0.5.0)

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

Every admin action gets a wire twin: `.nyar api` plus the human command. Each twin answers exactly one
`[NYAR:ok] cmd= verb= id= …` or `[NYAR:err] cmd= verb= code= [secs=] [arg=] [reason=]` line (contract §10.1). Switch
the send path to the twins when the handshake reports `api>=4`, and keep the human path for older servers.

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
- The new error codes are `exists`, `state`, `invalid`, `full`, `io` and `confirm` (contract §10.3). The api 2 codes
  still apply. Show `secs` as a countdown on `cooldown` and `ratelimit`.
- `changed=0` on `enable`, `disable` or `pillar` means that state was already set. Nothing is wrong.

## When the later children land

| Child (release) | Raphael adds |
|---|---|
| regions (0.6.0) | **Region picker:** `api regions` for the list, and the `region=` key on `[NYAR:def]` rows. The event manager sets an event's regions with the `event set` twin. The Events board shows `region=` from `event-start`, or "global" for `-`. |
| event-spawns (0.7.0) | new fields in the field editor (hunt, modifiers, AroundPlayer, loot), set through the `event set` twin. No new tag. |
| boss-reinforcements (0.8.0) | a `boss-adds` line on the Events board: the boss, the phase, and how many adds joined |
| anti-farming (0.9.0) | `farm-tier` on the live readout: the event, the tier, and the region only. Never show or guess a player or a position. Admin › Zones comes back with `api zones`. |
| outbreak (0.10.0) | **Live horde readout:** `horde-wave` (wave, units, region), `horde-spread` (hotspots, region), and `horde-boss-down`, where `empowered=0` means the horde lost its boss empowerment |

Every new push type still follows the fairness rule: it never says more than chat tells every player. Show only
what the line carries.

## Requests (contract §8)

If a panel needs a shape, key or command that isn't in the contract, add a row to contract §8 in this repository.
Give it the date, the request and the status `open`. The next child's plan picks the row up. A request made before
raphael-api-admin is planned can still change api 4's shapes.
- Never work around a missing key by parsing a human reply.
- To report a wire bug, attach the client log line (with `NyarDiagnostics` on) and the server's `LogOutput.log`
  lines from the same minute.
