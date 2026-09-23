# Audit — spikes

Build plan steps 1–9 of docs/dod/spikes.md. Each step has one "### Step <n>" entry under "## Pre-audit"
and one under "## Post-audit"; every post-audit entry carries a "Codex verdict:" line (D17).

## Pre-audit
### Step 1 · 2026-09-23 · 3ba32a0
- git status: clean
- compile: 0 errors, 0 warnings
- preflight: exit 0
- dod status: spikes 0/20 verified (just started); Epic 11/36
- feature doc read: none (tooling step); docs/dod/spikes.md Build plan step 1 and D4, D13, D14, D17
- server: not running; not touched by this step

### Step 2 · 2026-09-23 · 50e196a
- git status: clean
- compile: 0 errors, 0 warnings
- preflight: exit 0 ("spike code: none", selftest 19/19)
- dod status: spikes 1/20 verified; Epic 11/46 (review pending on v2.1, Codex round 2 running; it does not gate this step)
- feature doc read: docs/dod/spikes.md Build plan step 2, Business rules 1–5, Design › States and UX, D1–D6, D12, D16, D19; DEV_REMINDERS #4, #8, #9, #13, #14, #16–#19, #22, #23
- step-2 parent (D19): 50e196a. Compile items captured to docs/audits/spikes-compile-base.txt (6 items, none under obj/) before any harness file was added
- scope note: step 3's D5 and D6 sessions already run `march` and `empower`, so this step builds all six subcommands, including Spikes/SpikeMarch.cs and Spikes/SpikeCarrier.cs. Steps 4 and 6 are the sessions and any variant tuning
- marker recipe: AB_Consumable_PhysicalPowerPotion_T01_Buff (-1954355403), chosen because it has Buff and LifeTime (DEV_REMINDERS #17) and differs from the default carrier (T02, -1591883586). Its stat buffer is cleared, its gameplay-event components are stripped (#18), LifeTime is set to {0, None} so it lives with the unit, and SpellLevel.Level is set to 1314472274
- risk: DEV_REMINDERS #13 warns that some buffs applied on the spawn frame crashed the server. The plan applies the marker on the spawn frame, and the throwaway save absorbs a crash. If one happens, the marker moves to the next frame under a `discovered` amendment
- known limit: Test-CheckStructuralEdits matches `.DestroyEntity(`, not `DestroyUtility.Destroy(`. The harness calls DestroyUtility only inside EntityExtensions.DestroySafe, and the post-audit code review confirms this
- server: not running; not touched by this step

## Post-audit
### Step 1 · 2026-09-23 · (this commit)
- compile / preflight: 0 errors, 0 warnings (no C# change); `pwsh tools/preflight.ps1` exit 0 with "spike code: none"; `-SelfTest -Verbose` → "selftest: 19/19 checks, 3 fixtures each, 36 extra bad fixtures", every bad fixture failing for its planted reason; `-Paths` → "paths: 296 walked, all in manifest"
- real modes:
  - `-ServerWrites -Snapshot` (scratch file) recorded 1546 files and folders. An unchanged `-Compare` → "0 created, 0 changed, 0 deleted, all in manifest, no other save"
  - a planted nyar-probe.txt in the server root → "unmanifested: nyar-probe.txt"
  - an empty save-data-probe/Saves/ → "another Saves folder exists: save-data-probe/Saves"
  - both probes deleted; a re-compare came back clean
  - `-AuditOf spikes` → "1/9 pre, 0/9 post, 0/9 Codex verdicts" and a failing exit, as expected this early
- /code-review: inline review of the PowerShell diff. `-ServerWrites` without `-Compare` fails with a clear message. Snapshots can hold LocalLow paths that contain a SteamID: they stay in %TEMP% or the scratchpad, printed LocalLow paths are cut to two segments (Format-SafePath), and LocalLow/VRising/** is declared external. No further findings
- the Audits and FeatureResults bad fixtures briefly passed with 0/0, because their plant replaced `status: draft` in a plan that is now in-progress. The self-test caught it, and the plant now rewrites whatever status line is there
- Codex round 1: REVISE with 6 blocking findings.

  | Finding | Disposition |
  |---|---|
  | F1 (empty folders unseen) | accepted: directory rows; ServerWrites bad-5 and bad-6 |
  | F2 (locked files compare equal) | accepted: three hash retries, then fail; walk errors fail; any hashed row without a SHA-256 fails; bad-7 |
  | F3 (comment stripper cuts at "//" inside strings) | accepted: a literal-aware lexer, shared with the Epic checks, recorded as Epic amendment A2 (defect); SpikeCode bad-4, StructuralEdits bad-8 |
  | F4 (qualified attribute names) | accepted: bad-5 |
  | F5 (the plan defines its own step set) | accepted in part: steps must be numbered 1..N with no gap (AuditSteps bad-4). Removing steps is a plan edit that the dod amendments record |
  | F6 (no check for D19) | rejected: D19's evidence is its own command, run at Build step 8 |
- Codex round 2: REVISE with 1 blocking finding and 1 advisory:
  - F7 (a command name built from a constant or concatenation): accepted. A command name that is not one string literal counts as spike code; SpikeCode bad-6 and bad-7
  - F8 (advisory: a "Spike…" string literal fails): rejected, because failing is the safe direction after removal
- Codex round 3 (the cap): REVISE with 2 blocking findings, both applied:
  - F9: a using-alias for a command attribute counts as spike code (bad-8)
  - F10: \u escapes are decoded before the scan (bad-9)
- Codex verdict: REVISE at the 3-round cap. Every finding from the final round was applied and is proven by a failing fixture. There is no further Codex round; the owner reviews this record
- in-game: not applicable (tooling only; no DLL change)
- dod status: D14 verified (see Log); spikes 1/20
