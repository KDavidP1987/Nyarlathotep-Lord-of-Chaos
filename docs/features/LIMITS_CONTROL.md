# Limits control (backlog)

> **Status:** backlog, researched 2026-10-18. The owner asked for it on 2026-10-18, after a question from the Raphael
> session. It has no dod plan yet. **Nothing here is decided:** the open decisions go to the owner before any code.

## Goal

A server admin sets every safety cap themselves, sees it from Raphael, and changes it without a restart where that is
safe. Two caps prompted this:

1. **Units per wave.** 0.14.0 lets a definition ask for up to 500 units per entry and per pool wave (was 50). The cfg's
   `MaxUnitsPerWave` (default 20) still trims a start, and it is read only when the server starts. Raphael's editor
   cannot see that cap, so an admin can type 300 and get 20.
2. **Events at once.** The owner wants admins to choose how many events can run at the same time (3, 10, 20 …), so
   they can stagger or overlap events by their weight.

## What exists today (recon, 2026-10-18)

| Cap | Where | Default, range | Counts | Applies |
|---|---|---|---|---|
| `[Limits] MaxConcurrentEvents` | `Nyarlathotep/Nyarlathotep/Logic/Limits.cs:27`, bound at `Config/Settings.cs:82` | 3, 1-10 | events **started** (active), of every pillar. Enabled events have no cap. | at server start |
| `[Limits] MaxUnitsPerWave` | `Limits.cs:25` | 20, 1-500 | units in one wave; a start it cuts says "capped: <n> of <m> by MaxUnitsPerWave <v>; raise it in the cfg" | at server start |
| `[Limits] MaxTrackedUnits` | `Limits.cs:24` | 150, 1-500 | units of ours alive at once | at server start |
| The other `[Limits]` keys | `Limits.cs:28-49` | per key | spawn and despawn budgets, grace, purge cooldown … | at server start |

- **Already a cfg option, not hard-coded:** the number of concurrent events. It is only capped low (10).
- A start past it is refused: a manual start answers "skipped by MaxConcurrentEvents" (`Logic/Precedence.cs:47`), and
  the api answers `reason=max_concurrent` (contract §4 `limit`). A scheduled start past it is skipped, not queued.
- One apocalypse-type event (Apocalypse, Infestation, HordeMarch) runs at a time, whatever the cap.
- **Load is not only units.** `MaxTrackedUnits` and the per-tick budgets cap the units, but some work grows with the
  event count:
  - `EventRuntime.Tick` walks every active event each second;
  - empowerment work is per event;
  - hunt passes group by event.
  So a higher event cap needs a load test before it ships, like the undead-hordes unit ladder: 10, 25 and 50 light
  events plus empowerments, watching the mod's tick time and the server's fps.
- Raphael has no read of the caps. The wire carries the "capped" text only.

## The answer to Raphael now (0.14.0, api 8, no change needed)

The editor accepts 1-500 per entry and per pool wave, as the server's validator does. Beside a count above 20 it
shows a note:

> The server's `MaxUnitsPerWave` cfg cap (default 20) trims each wave at start, and a cut start says so. Raising it
> needs a server restart.

The editor does not block the value. The same note can say that `MaxConcurrentEvents` (default 3, range 1-10 today)
caps how many events run at once. The cfg cap belongs to the server owner, and the definition stays valid if the
cap is raised later.

## How it could work (proposed, to be decided)

1. **Raise the event cap's ceiling** from 10 to the highest step that passes the event-count load test (target 50).
   The default stays 3. One `Limits.cs` line, the test results and the release notes. Admins then set 3, 10 or 20 in
   the cfg themselves.
2. **Change caps without a restart:**
   - `.nyar limit list` shows every `[Limits]` key with its value, range and default.
   - `.nyar limit set <key> <value>` writes the cfg file through BepInEx and applies the value at once.
   - Lowering a cap never despawns anything. It only stops new starts or spawns past it: running events keep their
     units, and a lower `MaxConcurrentEvents` refuses new starts until the count drops.
   - Keys that can't change safely mid-run stay restart-only and say so.
   - Admin-only, like every `.nyar` admin command (the mod has no owner rank). Logged with the admin's name.
3. **Raphael (api 9):**
   - a read `api limits` with one row per key: `key value min max default live=0|1`;
   - an admin twin `api limit set`.
   The editor can then show "this server trims waves to 20" beside the field, and its cfg page can change the value.
4. **Scheduled starts past the cap** (decision 3): skipped as today, or held until a slot frees within a few minutes.

## Open decisions (each with a recommendation)

| # | Decision | Options | Recommendation |
|---|---|---|---|
| 1 | Ceiling of `MaxConcurrentEvents` | 20 / 50 / no ceiling | The highest step of 10, 25 and 50 that passes the load test (target 50). It stays a bound a typo can't blow through. |
| 2 | Changing caps live | cfg file and restart only (today) / `.nyar limit set` live for admins / live, plus a cfg switch that turns live changes off | Live for admins, with restart-only keys marked, and a `[Limits] LiveChanges` switch (on) so a server owner can lock the cfg. |
| 3 | A scheduled start past the event cap | Skipped (today) / held up to N minutes for a free slot | Held up to 5 minutes, then skipped with the same log line. |
| 4 | Per-pillar event caps | One cap for all (today) / an extra cap per pillar | One cap for now. The owner's staggering works with it; add per-pillar caps only if admins ask. |
| 5 | Raphael | Text note only (above) / api 9 `limits` read and set | The note now; api 9 with the live set. |

## Size and order

About S for decision 1 alone (the load test and one line); M with the live set and api 9. It is independent of the other children and can go into
any 0.14.x or 0.15.0 release.
