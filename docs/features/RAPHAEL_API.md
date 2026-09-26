# Raphael api — the machine interface

**Status:** in development (docs/dod/raphael-api-core.md, step 1 of 6). Ships in 0.3.0 as api 2.

## What it provides

The hidden `.nyar api …` commands the Raphael client mod sends silently, and whose `[NYAR:*]` replies it hides and
parses. The wire grammar, every tag and every key are specified in docs/RAPHAEL_INTEGRATION_CONTRACT.md; this doc
says what Nyarlathotep implements and how it was tested.

- **`.nyar api status`** (anyone): one `[NYAR:event]` row per active event and per ended event whose units still
  wait out the grace, then `[NYAR:end] cmd=status count=`. The unit count goes to admins only; no row carries a
  position.
- **`.nyar api events [page]`** (admin): one `[NYAR:def]` row per definition, 10 per page, then
  `[NYAR:end] cmd=events page=<cur>/<total> count=`.
- **`.nyar api sub on|off`** (anyone): push lines `[NYAR:ev]` for event start and end, waves, wave warnings, the
  kill switch and config changes. Subscriptions live in memory only.

## Code map

| File | Role |
|---|---|
| `Logic/Wire.cs` | Line builders: grammar, 480-byte cap, the tag builders |
| `Logic/Paging.cs` | Contract §4 paging: page parsing, the end line, badarg |
| `Logic/ApiLines.cs` | The `status` and `events` rows from the engine's state |

## Test results

### 2026-09-25 · step 1 · unit tests
- `dotnet test Nyarlathotep/Nyarlathotep.Tests`: 600 passed, 0 failed (61 new, after the post-audit fixes).
- ApiLinesTests (D1, D2): status rows in contract order, units for admins only, ending rows for events waiting out
  the grace (latest cleanup, one row per id, none for an id active again), the four definition states, reasons
  mapped to the wire grammar and sent only with state=disabled, a duplicate id never active, an action named
  for a definition without one, no ending row for a cleanup already due.
- PagingTests (D3): "0", "-1", "+1", " 1", "x", "1.5", "99999999999", "2147483648" and a non-ASCII digit are badarg;
  empty is page=1/1 count=0; page 2 of 10 rows is the end line alone; page 2 of 11 rows is the eleventh row.
- WireFormatTests (D4): every contract example line of event, def, end, err, ok and ev equals what the builder
  sends for the same values; a 32-character id with a 200-character name and reason of 4-byte characters keeps
  every key, the name cut to 64 bytes and the reason to 120. Examples are read from the section that documents each tag, and
  the optional forms (paged end, err with secs and with arg, ev with wave) must each have one. api=2 is asserted in step 2, when Wire.Api moves.

## Open questions

None.
