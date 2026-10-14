# Session handoff

Where work stands between sessions. Overwrite this file at the end of a session; git history keeps the old ones.
The plans (`docs/dod/`), audits (`docs/audits/`) and feature docs stay the source of truth. This file only points
into them.

**Written 2026-10-14**, v0.12.0 pushed and pre-released; apocalypse step 3 done (soak, early Codex); unit-watch (stuck
units, design D54) planned, approved and started: step 1 spike done, step 2 (Logic) under way. The repo is
`C:\dev\projects\Games\V Rising\Nyarlathotep Lord of Chaos`.

## Where we are

- **0.11.0 (hunting-grounds) is pushed** with its GitHub pre-release.
- **0.12.0 (revenge + final-wave bosses) is pushed** with its GitHub pre-release (release-verify hashes equal). Every
  commit after 246fe9e is local: push waits on the owner's OK. `dod close revenge` (partial) waits on the owner's sign-off.
- **Unit watch** (`docs/dod/unit-watch.md`, 33 items, Review 2 READY, in progress; ships WITH apocalypse as 0.13.0):
  - Step 1 spike done (`docs/features/UNIT_WATCH.md` › Spike): fight verdict = the apply signal (StatChangeSystem
    ApplyStatChanges) plus S-9's player-target clause; off-ground verdict not usable, so S-12's fallback. D19 waits on the
    owner.
  - Step 2 (Logic/UnitWatch.cs, unstick, wire api 8, tests) next or under way; then step 3 (services, vrclient Sessions 1-2).
- **Apocalypse 0.13.0** (`docs/dod/apocalypse.md`, in progress, Reviews 4-10 READY):
  - Steps 1-3 done. Step 3 post-audit in `docs/audits/apocalypse.md` (two code reviews, all findings fixed with tests and
    planted faults in `docs/audits/apocalypse-step3-planted.txt`). Tests 3395.
  - In game (`docs/features/APOCALYPSE.md` › Test results): Session 1 (outbreak, won/lost/survived, stop/purge/pillar off),
    Session 2 (resume across a restart; D29 tick budget 10/10 windows, worst 0.645 ms), Session 3 (3 h soak, D30: renewals,
    record writes, rises on the 30 min schedule, tracked 65 of 150, no errors; one unexplained 2.3 s slow tick 13 min in,
    none after; the d88dbbc DLL names the slow part if it recurs).
  - Step 4 sheet written (`docs/features/APOCALYPSE.md` › Test weekend). Step 5's drill part is built (A13: 0.12.0
    refuses an Apocalypse over 7200 s with "durationSeconds must be 30-7200"; drill selftest 32/32).
  - Codex ran early on `v0.12.0..HEAD` (code only): ISSUES (3), two fixed in 6e0b901 (a resume record past the unit cap is
    refused; no walk check fails the read), one rejected (`docs/audits/apocalypse.md`). It runs again before 0.13.0 only
    if code changes after the test weekend.
- **Owner acceptance still open (rubric 3, never Claude):** revenge D18-D21 and D32, D22 (a second player); apocalypse
  D24, D37 (Spike), D25-D27 (Session 1), D28 (Session 2), D30 (Session 3), D31 (the test weekend).
- **Known, left alone:** `docs/dod/spikes.reviews.md` (closed plan, cp1252) trips a newer dod check ("a VERDICT is glued
  to other text on line 21").

## Next, in order

1. On the owner's sign-off of revenge D18-D22, D32: `dod close revenge` (partial).
2. Unit watch steps 2-3, then its step 4 sheet joins apocalypse's test weekend.
3. Apocalypse step 4: the owner runs the test weekend (~10-24/25) from the sheet; changes asked for become amendments first.
4. Joint step 5 (apocalypse + unit watch, 0.13.0): Codex once for unit watch, again for apocalypse only if code changed after the weekend, the six surfaces (Rollout reads 0.12.0), name pair-apocalypse-short in Paths
   walked (Review 10 F1), tag v0.13.0, `pwsh tools/rollback-gate.ps1 -From v0.12.0 -To v0.13.0 -Plan apocalypse`,
   release by 10-31.

## After Halloween (1.0), owner decisions D52, D53, D55 (order: scaling first)

- Scale unit counts and levels to the players in the region (their number and average level).
- Replace a unit that dies before it has hit a player, within a per-event budget; border play is left to the honor system.
- Bosses for other wave types, after the faction-compatibility checks.
- An ambush at a hemmed-in spot comes in close; a wider search before shortening.
- The pathing study (units walking instead of hidden hops in busy areas).
- Stragglers left disabled far from every player can keep a horde alive, so a win needs players to find them.
