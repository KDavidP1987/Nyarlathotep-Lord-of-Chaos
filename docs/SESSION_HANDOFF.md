# Session handoff

Where work stands between sessions. Overwrite this file at the end of a session; git history keeps the old ones.
The plans (`docs/dod/`), audits (`docs/audits/`) and feature docs stay the source of truth. This file only points
into them.

**Written 2026-10-12**, during the hunting-grounds build. The repo is `C:\dev\projects\Games\V Rising\Nyarlathotep Lord of Chaos`.

## Where we are

- **Releases:** 0.9.0 (wave-sets) and 0.10.0 (swarm) are tagged, pushed and on GitHub as pre-releases; swarm is
  closed. The owner publishes them to Thunderstore.
- **Current child:** hunting-grounds (0.11.0), `docs/dod/hunting-grounds.md`, owner decisions D48-D50 (design §9).
  Steps 1-3 of 4 are built and post-audited; step 4 is under way (rollback drill 26/26, release surfaces drafted
  uncommitted at 0.11.0, Codex cross-inspection FAIL → F1 per-event territory and F2 per-group spots fixed in b136d7f,
  re-check "Codex verdict: PASS"; dispositions in the plan's Log). Amendments A1 (hillside levels), A2 (ravine crossings) and A3 (a cap of 200 walk checks
  per hunt check, rotation over stuck units, the rejoin) came out of Session 2, each amended before it was built.
- **Session 2 (vrclient, Release build) passed:** D21 roam, D22 lock, D23 ambush, D24 hunt, D25 end paths and D28
  tick budget. The results are in `docs/features/HUNTING_GROUNDS.md` › Test results › Session 2. D21-D25 are manual
  items. Each has a Log note "ready for the owner's acceptance". On a rubric-3 plan Claude never checks them.
- **Local only, not pushed:** the hunting-grounds commits since v0.10.0 (`git log origin/main..main`). The push waits
  for the owner's OK.
- **Dev server:** running the a902a20 DLL (before the b136d7f fix) with NyarDevTools and DevChatEcho, wrapped by `dev-snapshot -Save hg2`.
  Its events.json holds the dev events hg-roam, hg-lock, hg-ambush, hg-point, hg-timing and hg-east. **Don't restore
  hg2 until the owner's D26/D27 run is done.** After it:
  1. Close the client and copy both logs.
  2. Run `-LogCheck` and read every [Error]/[Warning].
  3. Stop the server with t## Next, in order

1. The owner's D26/D27 run → record it as "### Session 3" in HUNTING_GROUNDS.md; the owner's verdicts are written as
   theirs. Then close the client, copy both logs, `-LogCheck`, stop, `dev-snapshot -Restore`.
2. Redeploy b136d7f (or later) and rerun `hunting-grounds.vrs` (Region spots), `hunting-grounds-hunt.vrs` and
   `hunting-grounds-timing.vrs`, snapshot-wrapped, as Session 4.
3. Step 4 remainder:
   - `rollback-gate -From v0.10.0 -To v0.11.0 -Plan hunting-grounds` (server stopped);
   - `dotnet build -c Release --no-incremental`, tcli build with the token sentinel, the "zip sha256:" audit line;
   - step 4 post-audit with both Codex verdict lines (FAIL, then the re-check PASS);
   - `chore(release): v0.11.0` (six surfaces + CLAUDE.md + feature doc), annotated tag, privacy grep;
   - push only with the owner's OK, then verify; `gh release view` before the create; pre-release; release-verify;
   - `dod close hunting-grounds`.
4. revenge (0.12.0) and apocalypse (0.13.0): amend each plan at its start. Apocalypse gets D49's map-wide roaming and
   a study of pathing ("passing") for crowded areas. Then the test weekend (~10-24/25), and release by 2026-10-31.

se and release-verify;
   - `dod close hunting-grounds`.
4. revenge (0.12.0) and apocalypse (0.13.0): amend each plan at its start. Apocalypse gets D49's map-wide roaming and
   a study of pathing ("passing") for crowded areas. Then the test weekend (~10-24/25), and release by 2026-10-31.

## Owner items still open

- Confirm whether wave-sets A6 and A7 were emergent.
- The dod feedback draft aaf0be8334b4 is not sent; the consent file ~/.dod/feedback-consent.json still has the old
  OneDrive path.
- Publish 0.9.0 and 0.10.0 on Thunderstore.
