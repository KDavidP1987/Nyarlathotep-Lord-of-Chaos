# Process suggestions for Beelzebub, from Nyarlathotep

Written 2026-09-29 after reading Beelzebub's procedure docs only (read-only: CLAUDE.md, the hooks, PLAN.md and
PLAN-REVIEW-LOG.md, docs/INGAME_TEST_CHECKLIST.md, the test project, the memory index and the git log). Paths without a
prefix are in the Nyarlathotep workspace. Beelzebub paths start with `Beelzebub/`.

## What Beelzebub already does well

- Decisions go to the owner in plan mode, each with options, a recommendation and its status (CLAUDE.md).
- The plan is reviewed by Codex until APPROVED. After each build, a fresh read-only Codex pass reads the plan, the diff
  and a note of how the build deviated from the plan. Each finding is quoted and marked ACCEPTED or REJECTED with a
  reason (PLAN-REVIEW-LOG.md).
- In-game checklists give exact `.beelz` commands and expected output (docs/INGAME_TEST_CHECKLIST.md).
- Reference paths are guarded, and the release surfaces have a reminder hook.

## Differences that paid off in Nyarlathotep

Ordered by value.

### 1. One tracked plan per feature, with typed amendments

**Nyarlathotep:**
- Each feature is a plan in `docs/dod/<slug>.md` (the `dod` skill). The Definition of Done items (D1…Dn) each name
  their evidence: a test, a command, a file or a manual check.
- The baseline is frozen at approval.
- Anything unforeseen is written down as an amendment before it is built: A1…A16 in `docs/dod/automation.md`. Each has
  a kind (discovered, defect, requested…), the layer it missed, and the in-game evidence behind it.
- At close, a prediction rate says how much of the design the plan foresaw.

**Beelzebub:** PLAN.md holds rev deltas and is untracked. The owner's decision sheet sits outside the repo. A rev
delta records the change but not why the plan missed it.

**Suggestion:**
- Move PLAN.md and PLAN-REVIEW-LOG.md into the repo (e.g. `docs/plans/`).
- Give each rev delta a kind and a one-line cause.
- Or adopt the `dod` store (`/dod setup`) for the next feature and let its `status` check the evidence.

### 2. A test fails when its control is removed

**Nyarlathotep:**
- There are 2435 test cases (1027 test methods) over 40 pure `Logic/*.cs` files. `Services/` stays a thin shell around IL2CPP.
- A test named `X_fails_when_<defect>` must fail when that defect is planted. Each new control is checked by copying
  the file, planting the fault, running the tests and restoring the file.
- `ControlCases.cs` maps every plan item to its tests, and a test fails when an item has none.
- Game calls sit behind interfaces (`IWalkProbe`) with fake probes, so placement, budgets and the log text are all
  unit-tested.

**Beelzebub:** 44 tests over 5 pure files. `Beelzebub.Tests` is not in `Beelzebub.sln`, so a normal build never runs it.

**Suggestion:**
- Add the test project to the solution.
- Move decision logic out of the large services (`AdminCommands.cs` 2345 lines, `AbilityRules.cs` 2002,
  `SummonAllyService.cs` 1497) into pure `Logic/` classes with an interface for each game call.
- Plant each new check's fault once before trusting it.

### 3. A preflight that blocks, not only reminds

**Nyarlathotep:** `pwsh tools/preflight.ps1` checks:
- the six release surfaces are in sync;
- the tests pass;
- every closed plan has an audit record;
- the path manifest, and more.

`-AuthSuite` and `-LogCheck` add their own gates, and the release commit waits for PREFLIGHT OK.

**Beelzebub:** its CLAUDE.md lists `tools/preflight.ps1` as "(TBD)". Every rule is a reminder hook, and the hooks are
local only (`.claude/` is gitignored; Nyarlathotep's hooks are too, and its preflight is what enforces the rules).

**Suggestion:** port `tools/preflight.ps1` with the release-surface and test checks first, then run it before every
`chore(release)` commit.

### 4. Plan, pre-audit, build, post-audit: one commit and one audit entry per step

**Nyarlathotep:**
- Every step gets a pre-audit (clean tree, compile, preflight, plan status) and a post-audit:
  - compile Release and Debug;
  - tests;
  - preflight;
  - a fresh read-only review subagent;
  - a fresh read-only Codex pass on the diff.
- Both reviewers run until READY, at most 3 rounds. Each review round is its own commit.
- The verdict lines go into `docs/audits/<slug>.md` (template in `docs/audits/README.md`).

**Beelzebub:** v0.132–v0.135 never got their own commits and were folded into one release commit (56 files, +9396/-3062).
The last Codex inspection round was not run.

**Suggestion:**
- Commit each build step and each review round.
- Keep a short audit entry per step.
- Finish or explicitly waive every inspection round.

### 5. Put a diagnostic line in before a fix

**Nyarlathotep:** when an in-game test failed, the next build added a verbose line that names the reason before any fix
was guessed:
- A11 gave each unchecked spawn a reason.
- A12 added a walk survey in eight directions.

Two sessions of those lines found the root cause, the wrong height level at the player's own spot (A13, A16). This is
Beelzebub's own lesson from `docs/CHAIN_AUDIT.md`: eight versions went into an untested theory.

**Suggestion:** a failed in-game check gets a diagnostic build first, then a fix, recorded as two amendments.

### 6. Test requests, server handling and logs

**Nyarlathotep:**
- The owner only connects. Claude starts and stops the dev server.
- Each test request has numbered steps, the server address (127.0.0.1:9876), quoted commands and a pass line.
- After each session Claude reads every [Error]/[Warning] in both logs: BepInEx's LogOutput.log and the game's own
  `-logFile`, since Unity's errors are not in BepInEx's log.
- Before any restart, both logs are copied and the server is stopped only after an autosave.
- `tools/dev-snapshot.ps1` saves and restores the dev world around test sessions.

**Suggestion:** adopt the numbered-steps-with-address format and the two-log read. A dev-world snapshot also lets
ability tests start from the same state every time.

### 7. Handoff state lives in the repo

**Nyarlathotep:** where to resume is a dated log line in the plan, plus one handoff memory kept current.

**Beelzebub:** `MEMORY.md` still says v0.135 is "BUILT (uncommitted)" next to v0.136 "COMMITTED+PUSHED", and
`project_current_state.md` (marked read-first) stops at v0.100.0.

**Suggestion:**
- Keep a single current-state memory and delete superseded session checkpoints.
- Put the resume point in a tracked file.

## Clean-up found while reading

- **Stale CLAUDE.md lines:**
  - "Repo: not yet initialized" (there are 86 commits).
  - `tools/` and `docs/` marked "(TBD)".
  - "Things to watch out for… Empty for now".
- **A second changelog:** CLAUDE.md says "no second changelog", but a tracked root `CHANGELOG.md` (154 KB, last edited
  May 23) exists.
- **Oversized files:**
  - The player `CHANGELOG.md` is 198 KB, which Thunderstore may reject. Keep it concise and player-facing, with the
    full history on GitHub, as Nyarlathotep's `docs/DOC_STYLE.md` does.
  - `BCH_INTEGRATION_HANDOFF.md` is 180 KB and `ABILITY_CONDITIONS_AUDIT.md` 210 KB. Split them by topic, or index them.
- **Layout:**
  - There are two docs folders (`Beelzebub/docs` and `Beelzebub/Beelzebub/docs`).
  - A stray file is named `Beelzebub/docs/c:tempcurated_vbloods.txt`.
- **A contradiction in PLAN.md:** assumption 8 says "No automated test harness", but the later D7 section adds one.
