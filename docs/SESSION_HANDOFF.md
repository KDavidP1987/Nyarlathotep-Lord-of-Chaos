# Session handoff

Where work stands between sessions. Overwrite this file at the end of a session; git history keeps the old ones.
The plans (`docs/dod/`), audits (`docs/audits/`) and feature docs stay the source of truth. This file only points
into them.

**Written 2026-10-13**, after the revenge release was prepared. The repo is `C:\dev\projects\Games\V Rising\Nyarlathotep Lord of Chaos`.

## Where we are

- **0.11.0 (hunting-grounds) is pushed** with its GitHub pre-release (origin/main was 0aacdba).
- **0.12.0 (revenge + final-wave bosses) is committed and tagged locally, NOT pushed.**
  - The release commit is 8ee59c5 `chore(release): v0.12.0`, with the annotated tag v0.12.0. The post-audit and tooling commits come after it.
  - The zip is `Nyarlathotep/Nyarlathotep/build/kdpen-Nyarlathotep-0.12.0.zip`, sha256 F793056A…E30C91 (full hash in `docs/audits/revenge.md` › Step 3).
  - Tests 3134/3134. Codex: CHANGES REQUESTED (2) → both fixed → PASS on the re-check. Diff review: 4 low doc findings, 3 fixed, 1 kept.
  - Rollback gate: see `docs/audits/revenge.md` › Step 3 (the repo drill now reverts the first-parent line, because the range holds the step 1 merge 7031eb2).
  - The push waits on the owner's OK.
- **Owner acceptance still open (rubric 3, never Claude):** D18-D21 and D32 (vrclient evidence in `docs/features/REVENGE.md` › Test results › Session 1), D22 (a second player's look-and-feel session).
- **Dev world:** snapshot rv1 restored ("hashes equal"). The dev server is stopped, with the 0.12.0 DLL deployed. Config is back to VerboseLogging false, IncludeAdmins false, TimingLog true, EventBanners false. The marks camp, camp2 and vbstalker were lost with the restore: camp (-1515,0,-1545), camp2 (-1560,0,-1410), vbstalker (-1670,5,-1400).

## Next, in order

1. **On the owner's push OK:**
   1. Run `git grep -n -E "7656119|kdpenland"` and `git log -p origin/main..main` (both showed only pattern-quoting lines on 2026-10-13).
   2. Run `git push origin main` and `git push origin v0.12.0`, then check that `git rev-parse HEAD` equals `git ls-remote origin main`.
   3. Run `gh release view v0.12.0` first, then create the pre-release with the zip.
   4. Run `pwsh tools/release-verify.ps1 -Tag v0.12.0 -Asset kdpen-Nyarlathotep-0.12.0.zip`.
   5. Set the paths-manifest `remote-tag` and `remote-release` to v0.12.0; check D24, D25, D27 with their pass lines.
   6. Run `dod close revenge`. It closes partial while D18-D22 and D32 wait on the owner.
2. **Apocalypse (0.13.0), plan `docs/dod/apocalypse.md` (ready).** Amend before building:
   - the version, now 0.13.0 (it was planned before hunting-grounds and revenge took 0.11.0 and 0.12.0);
   - D49 map-wide roaming (not region-locked) and a pathing study at the start (the owner asked about walking instead of hidden jumps in crowded areas);
   - a faction-compatibility spike: undead, ghosts and other shadow units spawned together must not fight each other;
   - the lessons from hunting-grounds and revenge: the spider leash is 45 m, no unseen move lands in a claimed castle, fight squads where they spawn.
3. Test weekend about 10-24/25. Release by 10-31.

## After Halloween (1.0), owner decisions D52, D53

- Scale unit counts and levels to the players in the region (their number and average level). Admins can already set the start level and HP/damage and use per-wave `growth`.
- Replace a unit that dies before it has hit a player, within a per-event budget; border play is left to the honor system.
- Bosses for other wave types (undead and others), after the faction-compatibility checks.
- An ambush at a hemmed-in spot comes in close ("shortened" by walk safety). A wider search before shortening is a 1.0 candidate.
