# Session handoff

Where work stands between sessions. Overwrite this file at the end of a session; git history keeps the old ones.
The plans (`docs/dod/`), audits (`docs/audits/`) and feature docs stay the source of truth. This file only points
into them.

**Written 2026-10-13**, after the hunting-grounds release was prepared. The repo is `C:\dev\projects\Games\V Rising\Nyarlathotep Lord of Chaos`.

## Where we are

- **0.11.0 (hunting-grounds) is committed and tagged locally, NOT pushed.**
  - The release commit is 91fe093 `chore(release): v0.11.0`, with the annotated tag v0.11.0. The post-audit records come after it.
  - The zip is `Nyarlathotep/Nyarlathotep/build/kdpen-Nyarlathotep-0.11.0.zip`, with sha256 BBE9C330…C59EF (the full hash is in `docs/audits/hunting-grounds.md`).
  - The rollback gate gave 4/4. Tests: 2935/2935. Every Codex verdict is PASS after the first FAIL was fixed.
  - The push waits on the owner's OK.
- **It also carries a defect fix.** Before it, a player's death raised VBloodKilled, because a player character carries VBloodConsumeSource. Commits 62ce215 and e17c17e fix it; `vblood-player-death.vrs` gave PASS 8/8.
- **Owner verdicts (who = kd), Session 3:**
  - D26 and D27 are PASS: "the modes feel right overall".
  - D20-D25 still need the owner to accept the vrclient evidence (rubric 3, never Claude).
- **Dev world:** the hg2 snapshot is restored ("hashes equal"). The dev server is stopped, with the 0.11.0 DLL deployed.

## Next, in order

1. **On the owner's push OK:**
   1. Run the privacy grep and `git log -p origin/main..main` (both were clean on 2026-10-13).
   2. Run `git push origin main` and `git push origin v0.11.0`.
   3. Check that HEAD equals `git ls-remote origin main`.
   4. Run `gh release view v0.11.0` first, then create the pre-release with the zip.
   5. Run `pwsh tools/release-verify.ps1 -Tag v0.11.0 -Asset kdpen-Nyarlathotep-0.11.0.zip`.
   6. Set the paths-manifest `remote-tag` and `remote-release` to v0.11.0.
   7. Run `dod close hunting-grounds`. It closes partial while D20-D25 wait on the owner.
2. **Revenge (0.12.0), plan `docs/dod/revenge.md` (ready).** Amend it before building:
   - the version, now 0.12.0;
   - the lessons from this child: the spider leash is 45 m, so re-home seeded units; no unseen move lands in a claimed castle;
   - whatever the owner decides about a boss in the last wave and replacement units.
3. **Apocalypse (0.13.0), plan `docs/dod/apocalypse.md` (ready).** Amend it for:
   - D49 map-wide roaming and the pathing study;
   - a faction-compatibility spike (undead, ghosts and other shadow units spawned together must not fight each other).
4. Test weekend about 10-24/25. Release by 10-31.

## Owner requests recorded 2026-10-13 (in the hunting-grounds Log)

- Scale unit counts and levels to the players in the region (their number and average level). Today admins can already set the start level and HP/damage and use per-wave `growth`.
- A boss in the last wave: Ungoro (CHAR_Spider_Queen_VBlood) for spiders, undead bosses for undead. A tracked Ungoro spawns and dies cleanly (vblood-player-death.vrs).
- Faction compatibility for templates, so mixed units don't fight each other.
- Replace a unit that dies before it has hit a player, within a replacement limit. Border cheese is left to the honor system.
- An ambush at a hemmed-in spot comes in close ("shortened" by walk safety). A wider search before shortening is a 1.0 candidate.
