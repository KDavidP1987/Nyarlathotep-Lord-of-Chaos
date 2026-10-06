# Session handoff

Where work stands between sessions. Overwrite this file at the end of a session; git history keeps the old ones.
The plans (`docs/dod/`), audits (`docs/audits/`) and feature docs stay the source of truth. This file only points
into them.

**Written 2026-10-06** before the owner moved Claude Code to a second subscription
(Kristopher.Penland@SkillEra.IO) for the rest of the week. It's the same PC, Windows user, repo and server.

## Where we are

- **Git:** `main` at `1c25ab0`, clean. Nothing has been pushed since the v0.8.0 tag.
- **Current child:** wave-sets (0.9.0), `docs/dod/wave-sets.md`, in progress with 14 of 27 items checked. Step 2 of 3.
- **Session 1 in game** (`docs/features/WAVE_SETS.md` › Test results › Session 1): D20, D21 and D22 pass. D23 (tick
  budget) fails: one hunt-phase tick took 176.8 ms, so window 10 averaged 8.524 ms against the 5 ms limit. This is
  amendment A7. HuntAction comes from automation (0.8.0) and the cause isn't known yet.
- **A6 is still open:** event units sometimes leave the ledger in batches without any kill credit.
  `Services/SpawnTracker.cs` now names the killer on its verbose died line, but no run has caught it yet.
- **Dev server** is stopped. Its plugins are the 0.8.0 snapshot (`dev-snapshot -Restore` ws1). Do a Release build
  with the real VRisingServerPath before the next in-game run.
- **World note:** it was rolled back to AutoSave_3527 after a `.devkill` killed 232 world entities. Chaos lost the
  items in hotbar slots 5-6 at the pond.

## Next, in order

1. **A7 diagnostic:** on a slow tick, log the timing of each part of the hunt phase as one `[nyar …]` line. Build,
   redeploy, then rerun `tools/vrclient/scenarios/wave-sets-timing.vrs`:
   - ws-three set to 1800 s, every entry `CHAR_Undead_SkeletonSoldier_Armored_Farbane` at level 30, at the pond
     (`.devgo pond`).
   - About 25 minutes.
   - Tell the owner first, because the run takes over the mouse, keyboard and Steam.
   - Pass condition: `pwsh tools/preflight.ps1 -TimingSpan <log> -Windows 10 -MinTracked 10`.
2. **Step 2 post-audit:**
   - `/code-review` of `c5c8848..HEAD` by a fresh subagent.
   - Planted faults for the new controls, including NewlyCleared.
   - Evidence lines for D3 D5 D7 D9 D11 D13 D16 D18 D23, and a rerun of D8.
   - Privacy grep, then commit.
3. **Step 3, release 0.9.0:**
   - Rollback drill (D24).
   - The one Codex cross-inspection.
   - Six surfaces, `tcli`, tag, push and a GitHub pre-release.
   - Before `dod close`: a fresh-subagent re-review of A2 and A4-A7, generated with
     `dod-wbs.mjs --html wave-sets --review` and `--review-prompt wave-sets --scope …`.
4. **Then swarm, revenge and apocalypse.** None of them has a dod plan yet. Each gets one plan with one review round.
   Before release, a test weekend with the owner's players. Release by **2026-10-31**.

## What lives outside this repo

The account switch doesn't touch any of these, because they're local or have their own logins:

| Thing | Where | Account switch |
|---|---|---|
| Claude Code memory (procedures, vrclient lessons, owner preferences) | `%USERPROFILE%\.claude\projects\C--Users-KDPen-OneDrive-…-Nyarlathotep-Lord-of-Chaos\memory\` | Kept (local disk) |
| Global instructions | `%USERPROFILE%\.claude\CLAUDE.md` | Kept |
| dod skill | `%USERPROFILE%\.claude\skills\dod` → junction to `…\SkillEra Skills and MCPs\skillera-skills\skills\dod` | Kept |
| Plugins (typesafe, code-review and others) | `%USERPROFILE%\.claude\plugins` | Kept (re-enable if the new login doesn't show them) |
| Codex CLI (code cross-inspection) | its own OpenAI login | Unaffected; still needs `-c features.experimental_windows_sandbox=true` |
| GitHub | `gh`, authenticated as `KDavidP1987` | Unaffected |
| Dev server, Steam, character `Chaos` | local, 127.0.0.1:9876 | Unaffected |
| claude.ai artifacts and connectors (Docs, Drive, Gmail) | the old account | **Lost, but this project uses none of them.** The plan, review and dashboard pages are local files in `docs/dod/*.html`. No claude.ai link appears anywhere in the repo. |

The artifacts on the old account (dod 0.3.x, SkillEra, Jev and others) belong to other projects, not to
Nyarlathotep.

## Starting the next session

Say "pick up from the handoff". Then:

1. Work `docs/PREFLIGHT.md`.
2. Read this file, the Amendments and Log tail of `docs/dod/wave-sets.md`, and the end of `docs/audits/wave-sets.md`.
3. Start at step 1 above.
