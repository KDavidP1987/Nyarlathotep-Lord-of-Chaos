# Raphael handoff — api 2 (Nyarlathotep 0.3.0)

This page is for a session working **in the Raphael workspace**. It says what Raphael should build or check
against the wire that Nyarlathotep 0.3.0 ships as **api 2**. The wire itself is specified in
`docs/RAPHAEL_INTEGRATION_CONTRACT.md` (§1–§5), and the list of Raphael files is in its §6. This page doesn't
repeat them. It says what to do with them.

Nyarlathotep never edits the Raphael workspace, so this page is not copied there. Open it from this
repository, or from the GitHub release v0.3.0.

## Build against api 2

Raphael already has an api-1 client, written against the contract's shapes before they shipped:
- `Services/Nyar/`: `NyarWireParser.cs`, `NyarClient.cs`, `NyarState.cs`, `NyarProtocolService.cs` and `NyarDiag.cs`;
- the `[NYAR:` branch in `Patches/ClientChatPatch`;
- `UI/ModContent/MainPanel.Nyar.cs`.

api 2 makes the following shapes real. Each one is IMPLEMENTED in the contract's Tags and commands table.

| Command | Answer | Who |
|---|---|---|
| `.nyar api version` | `[NYAR:version] api=2 plugin=0.3.0 ready= admin= enabled= killswitch=` + the pillar, stats and announcement switches | anyone |
| `.nyar api status` | `[NYAR:event]` rows, then `[NYAR:end] cmd=status count=<n>` (unpaged) | anyone |
| `.nyar api events [page]` | `[NYAR:def]` rows, 10 per page, then `[NYAR:end] cmd=events page=<cur>/<total> count=<n>` | admin |
| `.nyar api sub on\|off` | `[NYAR:ok] cmd=sub on=1\|0`, then `[NYAR:ev]` pushes | anyone |

Every error is a `[NYAR:err] cmd= code= [secs=] [arg=]` line (contract §4). api 2 sends only two codes:
- `badarg`, for a bad `page` or `sub` argument;
- `ratelimit`, from `sub`.

The other codes are kept for later api versions and are never sent now. Before the world loads, every command
answers the plain line `still loading` instead of `notready`. VCF refuses admin commands with its own human line
instead of `noaccess`. The action commands (contract §5) are human commands and answer in human lines.

`api status` and `api events` answer normally when `enabled=0`: they show the definitions and an empty board.

To test the client against a real server, run Nyarlathotep 0.3.0 on a dedicated server, turn on
`NyarDiagnostics` in Raphael, and compare every `[NYAR:*]` line in the client log with the contract.

## Parser and state

`NyarWireParser.cs`, following contract §1:
- Split on spaces, then split each token on its first `=`.
- Ignore unknown keys. They are additive, and api 3 will add more.
- `-` means none. `_` becomes a space for display only.
- Never join two lines: each reply is its own chat message.

`NyarState.cs`: the shipped values to handle.
- `[NYAR:event] state=` is `active`, or `ending` for an event whose units wait out the grace. An `ending` row has
  `wave=-`, and its `left` counts down to the despawn. Show it as "ending", not as a live wave.
- `units=-` for non-admins.
- `faction=-` for waves events.
- `[NYAR:def] name=` is cut to 64 UTF-8 bytes and `reason=` to 120. `state=` is one of `idle`, `scheduled`,
  `active` or `disabled`. `reason` is the validation error, or `disabled` for a definition that is switched off.
- `action=` is sent even when validation disabled the definition.
- Paging: Raphael asks for `cur+1` until `cur == total`. A page past the last sends no rows and ends
  `page=<asked>/<total>`, so stop there too. Clear the accumulator on `[NYAR:err]` for the same `cmd`.

`NyarProtocolService.cs`: `HandleLine` routes on the tag. `Tick` runs the handshake back-off and the batched
re-reads. `Reset()` runs on login.

## Handshake and subscription

- Probe `.nyar api version` with the usual back-off: every 4 s, up to 12 attempts. The plain line
  `still loading` counts as no answer.
- Show the NYAR group only when `ready=1`. Enable the api 2 reads only when **`api>=2`**. With `api=1` (0.2.x),
  keep the group handshake-only.
- Send **`.nyar api sub on` after every handshake with ready=1**. Subscriptions live only in server memory:
  a disconnect or a server restart drops them, and a reconnect starts unsubscribed. `Reset()` on login clears
  `Present`, so the next handshake subscribes again. Keep that behaviour.
- At most 128 players can be subscribed. Past that, `sub on` answers `[NYAR:err] cmd=sub code=ratelimit`. Show
  the board without live pushes and re-read `api status` when the panel opens.

What a `[NYAR:ev]` line asks Raphael to do (contract §3, "Push events"):

| `type=` | Raphael does |
|---|---|
| `event-start` | re-read `api status` |
| `wave-warn` | start the countdown from `secs` for `wave=<n>` (only sent when the chat warning fires) |
| `wave` | clear the warning and re-read `api status` |
| `event-end` | clear the warning and re-read `api status` |
| `killswitch` | start the cooldown countdown from `secs`, then re-read `version` and `api status` |
| `config-changed` | **config-changed: re-read version and events** (`api events` for admins only) |

Recovery from a dropped line. The server queue holds 50 lines and drops the oldest.
- Re-read `api status` whenever an `[NYAR:ev]` names an event id that Raphael doesn't know.
- Always re-read `api status` after `event-end` and `killswitch`.

Raphael currently re-reads `status` for `wave` and `event-start`, but not for a `wave-warn` whose id is unknown.
Add that one.

## Panels

The contract §6 panels that api 2 can fill:
- **Events board** (everyone): the `api status` rows plus the push stream, with a countdown per wave and the
  `ending` grace shown as its own state.
- **Admin › Events**: the `api events` list with start, stop, enable, disable and reload, plus a field editor.
  Each action sends the human command from contract §5 (for example `.nyar event enable <id>`). Raphael then
  waits for `config-changed` (for enable, disable, set and reload) or for `event-start`/`event-end` (for start
  and stop). It never parses the human reply.
- **Kill switch** (admin): two presses with a confirmation dialog, sending `.nyar purge` and then
  `.nyar purge confirm`. A `purge confirm` without a `purge` in the last 30 s is refused with a human line.
  Then show the `killswitch` cooldown countdown.
- **Admin › Announcements**: the five announcement switches, read-only from `version`. Broadcasting uses
  `.nyar announce <text>`.

Gating, from the handshake:
- `admin=0` greys the Admin tabs out; it doesn't hide them.
- A pillar tab shows only when its switch is 1.
- `stats=0` hides Leaderboards and My stats. Stats aren't built yet, so api 2 always sends `stats=0`.

## Not yet (later api)

**me, top and zones stay PLANNED** in api 2. The server has no `.nyar api me`, `.nyar api top` or
`.nyar api zones`. Sending them to a 0.3.0 server gets VCF's human "command not found" line in the player's chat.
- Keep `RefreshMe`/`RefreshTop` gated on `stats=1` and `RefreshZones` on `zones=1`. Both switches are always 0 in
  api 2, so the calls never go out.
- The Leaderboards, My stats and Admin › Zones panels stay hidden until a later api marks these shapes IMPLEMENTED
  and bumps `api`.
- Also PLANNED: `state=scheduled` rows in `api status`, siege rows, and the share command.

## api 3 (faction empowerment)

IMPLEMENTED in Nyarlathotep 0.4.0 (docs/dod/faction-empowerment.md, D11; contract §3 and §9). No new tag or key: api 3 fills values that
api 2 already defines.
- A status row of `kind=empower` carries `faction=<names joined by ','>` (e.g. `faction=Legion,Bandits`, the
  `Faction_` prefix removed), `wave=-`, and for admins `units=<NPCs holding the event's empowerment>`.
- An empower event has no `ending` row: it goes straight from `active` to gone.
- A client gating on `api>=2` needs no change. Show `faction` on empower rows and hide the wave column when it is `-`.

## Requests (contract §8)

If Raphael needs a shape, key or command that api 2 doesn't have, add a row to contract §8 in the Nyarlathotep
repository. Give it the date, the request and the status `open`. Nyarlathotep's plan for the next child picks the
row up.
- Never work around a missing key by parsing a human reply.
- The §8 log is empty at api 2.

To report a wire bug, attach the client log line (with `NyarDiagnostics` on) and the server's
`LogOutput.log` lines from the same minute.
