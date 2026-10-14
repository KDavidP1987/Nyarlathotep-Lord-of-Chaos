# Session handoff

Where work stands between sessions. Overwrite this file at the end of a session; git history keeps the old ones.
The plans (`docs/dod/`), audits (`docs/audits/`) and feature docs stay the source of truth. This file only points
into them.

**Written 2026-10-14 (evening)**: unit-watch step 3 done (code, A2/A3, in-game Session 1); the next step is the test
weekend sheet. Every commit after 246fe9e (v0.12.0) is local: push waits on the owner's OK. The repo is
`C:\dev\projects\Games\V Rising\Nyarlathotep Lord of Chaos`.

## Where we are

- **0.12.0 (revenge + final-wave bosses)** pushed with its GitHub pre-release. `dod close revenge` (partial) waits on the
  owner's sign-off.
- **Unit watch** (`docs/dod/unit-watch.md`, 34 items, 4 verified; ships WITH apocalypse as 0.13.0):
  - Steps 1-3 done. A2 (design §9 D56): the standing and still checks mark only a unit holding a player as its aggro
    target. A3: NPCs drop a forced target on invulnerable Chaos and the game's height correction lifts a sunk unit, so the
    in-game sessions use `.nyar event unstick` marks; positive standing/still cases and D34's first-move measurement
    (which sets SpawnStillSeconds) move to the test weekend.
  - Session 1 results: `docs/features/UNIT_WATCH.md` › Test results › Session 1 (watch 23/23, remove 23/23, stall 25/25,
    ends 28/33 + 7/7 + 10/10, apocalypse 15/16 + resume read, timing run 3 27/27 with D28 10/10 windows). Post-audit:
    `docs/audits/unit-watch.md` › Post-audit › Step 3.
  - Verified: D18, D20, D22, D28. Ready for the owner's acceptance (manual, rubric 3): D19, D24-D27.
  - Dev-only NyarDevTools additions: `.devaggro`/`.devaggroread` (DevAggro.cs), the strand hold (DevHold.cs), `.devbuff`,
    `.devperch`. The DevHold type scan logs HarmonyX type-load warnings at boot (dev server only).
- **Apocalypse 0.13.0** (`docs/dod/apocalypse.md`, 26/38): steps 1-3 done; step 4 sheet in `docs/features/APOCALYPSE.md`
  › Test weekend; step 5's drill part built.
- **Owner acceptance still open (rubric 3, never Claude):** revenge D18-D21, D32, D22; apocalypse D24, D37, D25-D28,
  D30, D31; unit-watch D19, D24-D27 (and D23, D29 at the weekend).
- **Dev server:** stopped; snapshot uw1 restored, current DLL deployed (hash equal to the build).

## Next, in order

1. Unit watch step 4: add the test weekend sheet to `docs/features/UNIT_WATCH.md` (a vulnerable player near a stuck
   unit that then moves 150 m (D23); D34's first-move measurement → SpawnStillSeconds; a horde unit landing beside its
   horde (D26); D19 acceptance), joined to apocalypse's weekend (~10-24/25).
2. On the owner's sign-off of revenge D18-D22, D32: `dod close revenge` (partial).
3. The owner runs the test weekend; changes asked for become amendments first.
4. Joint step 5 (apocalypse + unit watch, 0.13.0): Codex once for unit watch (and apocalypse if its code changed), the six
   surfaces, tag v0.13.0, `pwsh tools/rollback-gate.ps1 -From v0.12.0 -To v0.13.0 -Plan apocalypse` (and unit-watch),
   release by 10-31.

## After Halloween (1.0), owner decisions D52, D53, D55 (order: scaling first)

- Scale unit counts and levels to the players in the region (their number and average level).
- Replace a unit that dies before it has hit a player, within a per-event budget; border play is left to the honor system.
- Bosses for other wave types, after the faction-compatibility checks.
- An ambush at a hemmed-in spot comes in close; a wider search before shortening.
- The pathing study (units walking instead of hidden hops in busy areas).
- Stragglers left disabled far from every player can keep a horde alive, so a win needs players to find them.
