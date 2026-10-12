# Session handoff

Where work stands between sessions. Overwrite this file at the end of a session; git history keeps the old ones.
The plans (`docs/dod/`), audits (`docs/audits/`) and feature docs stay the source of truth. This file only points
into them.

**Written 2026-10-11**, late, at the end of the swarm build. The repo is `C:\dev\projects\Games\V Rising\Nyarlathotep Lord of Chaos`.

## Where we are

- **Releases:** 0.9.0 (wave-sets) is tagged, pushed and on GitHub as a pre-release. The owner publishes it to Thunderstore.
- **Current child:** swarm (0.10.0), `docs/dod/swarm.md`, 19 of 25 items checked; all three Build plan steps are built
  and audited (`docs/audits/swarm.md`: 3/3 pre, 3/3 post, the Codex verdict recorded).
- **Local only, not pushed:** commits 083d191..2aac320 and the annotated tag **v0.10.0 on c06a928** (the release
  commit). Zip `Nyarlathotep/Nyarlathotep/build/kdpen-Nyarlathotep-0.10.0.zip`, sha256 B1BBCC41…B04FE1 (in the audit).
  The push waits for the owner's OK. After it: verify `git rev-parse HEAD` = `git ls-remote origin main`, check that
  the tag is on origin, run `gh release view v0.10.0` (expect none), create the pre-release with the zip, run
  `pwsh tools/release-verify.ps1 -Tag v0.10.0 -Asset kdpen-Nyarlathotep-0.10.0.zip`, then evidence D21 and D22 and
  write `remote-tag:` / `remote-release:` lines in tools/paths-manifest.txt.
- **Waiting on the owner:**
  - D15: the chase-and-return part. vrclient can't walk the character, and the spiders don't attack an
    invulnerable one.
  - D16 and D17: acceptance. The evidence is in the Log notes; on a rubric-3 plan the builder never accepts a manual
    item.
  - D18: look and feel.
  - One spider-swarm run by the owner covers all of them; the steps are in the session's last message, and
    `docs/features/SWARM.md` has the open question.
- **Dev server:** running with the 0.10.0 DLL for the owner's run (Session 3), wrapped by `dev-snapshot -Save sw3`.
  The cfg has IncludeAdmins = true, and sw-arena is in events.json. After the owner's run:
  1. Close any client, copy both logs, run `-LogCheck`, and read every [Error]/[Warning].
  2. Stop the server with taskkill, no /F.
  3. Run `pwsh tools/dev-snapshot.ps1 -Restore`.
  4. Record "### Session 3" in SWARM.md, and the session 3 log check and snapshot lines in the audit.
- **World note:** Chaos died twice in Session 1 (one `.devdie`, then the sun at the coffin). A death container
  stands at the pond (-1880, -1640), and gear may be broken.
- **Known:** the dod index reports one problem in the old `spikes` plan; it predates this work.

## Next, in order

1. Owner's Session 3 → record it; the owner writes the D15 to D18 pass lines (or tells Claude the verdict to record
   as theirs).
2. With the owner's OK: push main and v0.10.0, then do the release steps above. Then `dod close swarm`
   (D18 may close partial).
3. revenge (0.11.0, `docs/dod/revenge.md`, approved): `dod start`, step 1 pre-audit, build.
4. apocalypse (0.12.0, `docs/dod/apocalypse.md`, approved). Then the test weekend (~10-24/25) and release by
   2026-10-31.

## Owner items still open

- Confirm whether wave-sets A6 and A7 were emergent.
- The dod feedback draft aaf0be8334b4 is not sent; the consent file ~/.dod/feedback-consent.json still has the old
  OneDrive path.
- Publish 0.9.0 (and 0.10.0 once released) on Thunderstore.
