# Session handoff

Where work stands between sessions. Overwrite this file at the end of a session; git history keeps the old ones.
The plans (`docs/dod/`), audits (`docs/audits/`) and feature docs stay the source of truth. This file only points
into them.

**Written 2026-10-13**, during apocalypse step 3. The repo is `C:\dev\projects\Games\V Rising\Nyarlathotep Lord of Chaos`.

## Where we are

- **0.11.0 (hunting-grounds) is pushed** with its GitHub pre-release.
- **0.12.0 (revenge + final-wave bosses) is committed and tagged locally, NOT pushed.** Release commit 8ee59c5, tag v0.12.0, zip `Nyarlathotep/Nyarlathotep/build/kdpen-Nyarlathotep-0.12.0.zip` (sha256 in `docs/audits/revenge.md` › Step 3). The push waits on the owner's OK. Every commit after it (apocalypse steps 1-3) is local too.
- **Apocalypse 0.13.0** (`docs/dod/apocalypse.md`, in progress, Reviews 4-8 READY):
  - Step 1 done: spike "march verdict: go — hop" (with the unit's path cleared), "pathing verdict: walk not usable" (pathing is a 1.0 study); faction-mix table in `docs/GAME_ASSETS.md` › Faction compatibility. Evidence in `docs/features/APOCALYPSE.md` › Spike.
  - Step 2 done: Logic merged (038c8c4), code-review fixes 1841fee, tests 3337. 18/38 items verified.
  - Step 3 (services, zombie-apocalypse template, FactionGroups, scenarios) is being built by a builder subagent in a worktree; then the orchestrator merges and runs Sessions 1-3 by vrclient.
- **Owner acceptance still open (rubric 3, never Claude):** revenge D18-D21 and D32 (`docs/features/REVENGE.md` › Session 1), D22 (a second player); apocalypse D24 and D37 (`docs/features/APOCALYPSE.md` › Spike).
- **Dev world:** snapshot apo0 restored; Chaos is DEAD in that snapshot — after connecting, respawn by clicking the Wooden Coffin on the map. The dev server is stopped with the 0.12.0 DLL deployed.

## Next, in order

1. Merge step 3, review it, run Sessions 1-3 (`tools/vrclient/scenarios/apocalypse*.vrs`), step 3 post-audit (plant the roam-move fault alone).
2. On the owner's push OK: privacy greps (`git grep -n -E "7656119|kdpenland"`, `git log -p origin/main..main`), push main and v0.12.0, verify HEAD = ls-remote, `gh release view v0.12.0` then the pre-release with the zip, `pwsh tools/release-verify.ps1 -Tag v0.12.0 -Asset kdpen-Nyarlathotep-0.12.0.zip`, manifest remote-tag/remote-release, `dod close revenge` (partial).
3. Step 4: the owner's test weekend (~10-24/25). Step 5: release 0.13.0 (Codex once, rollback gate from v0.12.0, six surfaces; Rollout must read 0.12.0 per Review 8 F1). Release by 10-31.

## After Halloween (1.0), owner decisions D52, D53

- Scale unit counts and levels to the players in the region (their number and average level). Admins can already set the start level and HP/damage and use per-wave `growth`.
- Replace a unit that dies before it has hit a player, within a per-event budget; border play is left to the honor system.
- Bosses for other wave types (undead and others), after the faction-compatibility checks.
- An ambush at a hemmed-in spot comes in close ("shortened" by walk safety). A wider search before shortening is a 1.0 candidate.
