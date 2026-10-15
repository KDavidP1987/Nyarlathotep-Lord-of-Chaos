# Session handoff

Where work stands between sessions. Overwrite this file at the end of a session; git history keeps the old ones.
The plans (`docs/dod/`), audits (`docs/audits/`) and feature docs stay the source of truth. This file only points
into them.

**Written 2026-10-14 (session close)**: the coming 0.13.0 passed Codex's pre-release review after four fixes (b1e3cdc);
the replacements plan (D58) is approved; the tester package is in GitHub (`testing/`) with install steps, and test
build **v0.13.0-rc.1** is a GitHub pre-release (mod zip + blank workbook + PDF, booted on the dev server first). main
and `scaling` are pushed and equal to origin. The repo is
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
- **Tester package** `tools/tester-package` (round 1, 20 production-setup rows). `python tools/tester-package/build.py`
  builds it and publishes the blank workbook + PDF to `testing/` (committed, with `testing/source.json`);
  `TesterPackageTests` fails when `rows.py`/`build.py` change without a rebuild. The "Start here" sheet, PDF and HTML
  carry the download link and nine install steps. Filled-in workbooks stay out of git (gitignored output folder).
- **Test build v0.13.0-rc.1**: annotated tag on aa7e4d3 and GitHub pre-release
  https://github.com/KDavidP1987/Nyarlathotep-Lord-of-Chaos/releases/tag/v0.13.0-rc.1 with
  `Nyarlathotep-0.13.0-rc.1.zip` (DLL + INSTALL.txt), the blank `.xlsx` and `.pdf`; downloaded back, md5 match. The rc DLL
  was built with `-p:Version=0.13.0-rc.1` (csproj stays 0.12.0) and booted on the dev server ("v0.13.0-rc.1 loaded",
  "push: ready", only the 6 baseline warnings). Untested: whether the optional Raphael client minds the "-rc.1" version.
- **Owner acceptance still open (rubric 3, never Claude):** revenge D18-D21, D32, D22; apocalypse D24, D37, D25-D28,
  D30, D31; unit-watch D19, D24-D27 (and D23, D29 at the weekend); scaling D19, D20, D21 (and D23 with real players).
- **dod store:** the closed plan `spikes` reports one `--check` problem (an old plan, not touched this session).
- **Dev server:** stopped; snapshot sc4 restored.

## Next, in order

1. A row edit in `tools/tester-package/rows.py` needs `python tools/tester-package/build.py` (TesterPackageTests fails
   otherwise); a new test build is a new `v0.13.0-rc.<n>` pre-release (DLL built with `-p:Version=0.13.0-rc.<n>`,
   tag and release declared in tools/paths-manifest.txt, `ROUND["download"]` updated).
2. Test weekend (~10-24/25) on v0.13.0-rc.1 (or a later rc): apocalypse and unit-watch sheets; scaling's step 5 sheet on a
   build of `scaling`.
3. Joint 0.13.0 release (apocalypse + unit watch): six surfaces, tag, rollback gate `-From v0.12.0 -To v0.13.0`, by
   10-31. Then merge main into `scaling`, scaling step 6 → 0.14.0.
4. Replacements: `dod start replacements`, branch, step 1 (pre-audit, `.devperish`, spike Session 0).
5. Then (D55): other boss types + faction compatibility, ambush spacing, the pathing study (D50), the Epic children.

## Backlog to 1.0 (as given to the owner 2026-10-14)

**Halloween 0.13.0 (apocalypse + unit watch), due 2026-10-31**

1. Test weekend (~10-24/25) on v0.13.0-rc.1: tester workbook round 1 (20 rows), apocalypse and unit-watch sheets; the
   scaling sheet can run the same weekend on a `scaling` build.
2. Owner sign-offs (rubric 3, never Claude): apocalypse D24, D25-D28, D30, D31, D37; unit-watch D19, D23-D27, D29;
   revenge D18-D22, D32 (lets revenge close `done · partial`).
3. Weekend fixes, then `v0.13.0-rc.2` if anything changed.
4. Release 0.13.0: six surfaces, tag, rollback gate `-From v0.12.0 -To v0.13.0`, GitHub pre-release; the owner publishes
   on Thunderstore.

**After Halloween, in the owner's order (D55)**

| # | Item | State | What's left |
|---|---|---|---|
| 1 | Scaling → 0.14.0 (`docs/dod/scaling.md`) | 18/27 | step 5 real-player session + owner sign-off (D19-D21, D23); step 6 release |
| 2 | Replacements → 0.15.0 (`docs/dod/replacements.md`, D58) | ready 0/29 | step 1 `.devperish` + spike; 2 Logic; 3 Services; 4 vrclient sessions incl. D29 world deaths (water, fall, other NPCs, sunlight); 5 owner session; 6 release |
| 3 | Other boss types + faction compatibility | no plan | final-wave bosses for undead and other factions; do ghosts, undead, shadow creatures fight each other when mixed |
| 4 | Ambush spacing | no plan | ambushes appear 30-40 m away, not on top of the player (owner, pond test) |
| 5 | Pathing study (D50) | no plan | walk to a map spot or castle heart on the game's roads/pathing instead of hidden jumps |
| 6 | Boss reinforcements (`docs/dod/boss-reinforcements.md`) | ready 0/20 | adds joining V Blood fights as the boss loses health |
| 7 | Anti-farming (Epic A29) | no plan | escalating reinforcements against a group farming one area, incl. V Blood copies |
| 8 | Outbreak (Epic A30) | no plan | faction hordes spreading from home territory; waves of 30-100 led by empowered boss copies |
| 9 | Sieges (Epic A31) | no plan | NPC waves on castles; optional castle destruction off by default, raid windows only, 5-minute clan warning |
| 10 | Spawn extras (Epic A32) | no plan | Ambush and Guard modes, spawn visual effect |
| 11 | Stats | no plan | kill/participation counters, leaderboards, daily digest, privacy controls |
| 12 | Epic close-out (`docs/dod/nyarlathotep.md`) | 38/47 | D16, D22 (closed plan `spikes` has one `--check` problem), D32 rollback drill, D36 permissions, D37 Raphael wire lines, D43 messages in game; D39, D40, D46 wait on Stats |
