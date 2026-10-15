# Session handoff

Where work stands between sessions. Overwrite this file at the end of a session; git history keeps the old ones.
The plans (`docs/dod/`), audits (`docs/audits/`) and feature docs stay the source of truth. This file only points
into them.

**Written 2026-10-14 (night)**: scaling (the first 1.0 child) has steps 1-4 done on the git branch `scaling`; step 5
needs real players. main gained a fix to apocalypse's failed-resume cleanup (6f9f75c). Push waits on the owner's OK:
main has b8d21d1, 5b6ad38, 6f9f75c unpushed; the branch `scaling` is local only. The repo is
`C:\dev\projects\Games\V Rising\Nyarlathotep Lord of Chaos`.

## Where we are

- **0.12.0 (revenge + final-wave bosses)** pushed with its GitHub pre-release. `dod close revenge` (partial) waits on the
  owner's sign-off.
- **Apocalypse + unit watch → 0.13.0** (`docs/dod/apocalypse.md` 26/38, `docs/dod/unit-watch.md`): steps 1-3 of both
  done; their test weekend sheets are in `docs/features/APOCALYPSE.md` and `docs/features/UNIT_WATCH.md`. New on main:
  6f9f75c — a resume that fails after the engine resumed an apocalypse run now drops the run's hunt seeds, territory
  map, streaks and unit-watch rows (`IApocalypseGame.ResumeCancelled` → `EventRuntime.EndSpawnState`); Codex APPROVED;
  Log notes in both plans. The deployed dev DLL (md5 6345be3f…) predates it.
- **Scaling** (`docs/dod/scaling.md`, 18/27 verified, design §9 D52/D57; built on branch `scaling` so nothing reaches
  0.13.0; merge after v0.13.0 is tagged; release 0.14.0):
  - Step 1 spike (ff1a1b7): gear level = floor of the Equipment sum, equal to the character screen 7/7.
  - Step 2 Logic (faf64fb), step 3 Services (abe8020; reads only in `Services/ScalingReads.cs`; Debug-only phantom
    players `[Debug] FaultInjection = scaleplayers:<n>@<gear>`), merge of main's resume fix (0421b64).
  - Step 4 (642d376): vrclient Sessions 1-3 — scaling.vrs 19/19, scaling-admins-off.vrs 10/10, scaling-phantoms.vrs 22/22
    (Debug), scaling-timing.vrs 25/25 with D22 10/10 windows under 5 ms at 126 tracked. Results:
    `docs/features/SCALING.md` › Test results; audit `docs/audits/scaling.md`.
  - D1-D17 and D22 verified. Ready for the owner's acceptance: D19 (spike), D20 (Session 1), D21 (Session 2).
  - Step 5 sheet for real players: `docs/features/SCALING.md` › Owner test sheet (D23).
- **Tester package** `tools/tester-package` (round 1, 20 production-setup rows); built output is gitignored.
- **Owner acceptance still open (rubric 3, never Claude):** revenge D18-D21, D32, D22; apocalypse D24, D37, D25-D28,
  D30, D31; unit-watch D19, D24-D27 (and D23, D29 at the weekend); scaling D19, D20, D21 (and D23 with real players).
- **Dev server:** stopped; snapshot sc4 restored (hashes equal). Chaos wears two working Dracula's Grim pieces (gear 48)
  since the scaling spike; the snapshot holds BepInEx trees, not the world save.

## Next, in order

1. The owner's OK to push main (b8d21d1, 5b6ad38, 6f9f75c) and the branch `scaling`; privacy greps first, then
   `git rev-parse HEAD` = `git ls-remote origin <branch>`.
2. The test weekend (~10-24/25) on one Release build of main with 6f9f75c: apocalypse and unit-watch sheets; scaling's
   step 5 sheet can run the same weekend on a build of the branch.
3. Joint 0.13.0 release (apocalypse + unit watch): Codex once each, six surfaces, tag, rollback gate
   `-From v0.12.0 -To v0.13.0`, by 10-31. Then merge main into `scaling`, step 6 → 0.14.0.
4. After scaling (D55 order): replacements (D53), other boss types + faction compatibility, ambush spacing, the pathing
   study (D50).
