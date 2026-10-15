# Session handoff

Where work stands between sessions. Overwrite this file at the end of a session; git history keeps the old ones.
The plans (`docs/dod/`), audits (`docs/audits/`) and feature docs stay the source of truth. This file only points
into them.

**Written 2026-10-14 (late night)**: the coming 0.13.0 passed Codex's pre-release review after four fixes (b1e3cdc);
the replacements plan (D58) is approved; main is merged into `scaling` again. Push waits on the owner's OK: main has
b1e3cdc, b51d520, f7dbe74 and this handoff unpushed; `scaling` has the merges on top. The repo is
`C:\dev\projects\Games\V Rising\Nyarlathotep Lord of Chaos`.

## Where we are

- **0.12.0 (revenge + final-wave bosses)** pushed with its GitHub pre-release. `dod close revenge` (partial) waits on the
  owner's sign-off.
- **Apocalypse + unit watch → 0.13.0** (`docs/dod/apocalypse.md`, `docs/dod/unit-watch.md`): steps 1-3 of both done;
  test weekend sheets in `docs/features/APOCALYPSE.md` and `docs/features/UNIT_WATCH.md`. Codex's once-per-child
  pre-release review (`git diff v0.12.0..main`) said REVISE with four findings, all fixed in b1e3cdc and re-checked
  APPROVED:
  - unit-watch's off-ground level scan costs walk-budget per probe and stops 10 m above the unit (D10's ceiling);
  - a failed apocalypse Begin rolls the start back (logged "begin failed; the start is rolled back", refused);
  - a purge counts the run's waiting orders in the final tally;
  - the fight read follows three owner links.
  The deployed dev DLL (md5 6345be3f…) predates 6f9f75c and b1e3cdc; the weekend build must be rebuilt from main.
- **Scaling** (`docs/dod/scaling.md`, 18/27, branch `scaling`, release 0.14.0): steps 1-4 done; step 5 sheet for real
  players in `docs/features/SCALING.md` › Owner test sheet. After merging b1e3cdc, e26f2bb updated one source check for
  `EventRuntime.End`'s new `announce` parameter (3700/3700).
- **Replacements** (`docs/dod/replacements.md`, ready 0/29, design §9 D58, Epic A40, release 0.15.0): review 1 REVISE
  (world death causes never exercised) → D29 Session 3 per cause (water, fall, other NPCs, sunlight) and S-16 (a cause
  the game delivers as a removal is not replaced, told to the owner); review 2 READY. Build on branch `replacements`
  from the `scaling` tip after `dod start replacements`; step 1 is the `.devperish` tool and the spike.
- **Tester package** `tools/tester-package` (round 1, 20 production-setup rows); built output is gitignored.
- **Owner acceptance still open (rubric 3, never Claude):** revenge D18-D21, D32, D22; apocalypse D24, D37, D25-D28,
  D30, D31; unit-watch D19, D24-D27 (and D23, D29 at the weekend); scaling D19, D20, D21 (and D23 with real players).
- **dod store:** the closed plan `spikes` reports one `--check` problem (an old plan, not touched this session).
- **Dev server:** stopped; snapshot sc4 restored.

## Next, in order

1. The owner's OK to push main and `scaling`; privacy greps were clean on 2026-10-14; verify
   `git rev-parse HEAD` = `git ls-remote origin <branch>` after.
2. Test weekend (~10-24/25) on a Release build of main: apocalypse and unit-watch sheets; scaling's step 5 sheet on a
   build of `scaling`.
3. Joint 0.13.0 release (apocalypse + unit watch): six surfaces, tag, rollback gate `-From v0.12.0 -To v0.13.0`, by
   10-31. Then merge main into `scaling`, scaling step 6 → 0.14.0.
4. Replacements: `dod start replacements`, branch, step 1 (pre-audit, `.devperish`, spike Session 0).
5. Then (D55): other boss types + faction compatibility, ambush spacing, the pathing study (D50), the Epic children.
