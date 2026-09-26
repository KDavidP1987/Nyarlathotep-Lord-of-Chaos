# Audit — raphael-api-core

Build plan steps 1–6 of docs/dod/raphael-api-core.md. Each step has one "### Step <n>" entry under "## Pre-audit"
and one under "## Post-audit"; every post-audit entry carries a "Codex verdict:" line. Session log checks are lines
"- session <n> log check: …". Rollback base: v0.2.1 = 8b405a0.

## Pre-audit
### Step 1 · 2026-09-25 · 0453740
- pre-child: 0453740 — the plan's approve-and-start commit; the build's rollback base is the tag v0.2.1 (8b405a0)
- git status: clean
- compile: 0 errors, 0 warnings
- tests: 539 passed
- preflight: exit 0 ("PREFLIGHT OK")
- dod status: raphael-api-core 0/22 verified (just started); review human (Review 4 READY after Codex rounds 1–3)
- feature doc read: none yet — docs/features/RAPHAEL_API.md is created by this step; read the plan's D1–D4, Business rules 5, docs/RAPHAEL_INTEGRATION_CONTRACT.md §3 and §4, Logic/Wire.cs, Logic/Model.cs, Logic/Engine.cs
- server: not running; step 1 is pure logic with no in-game test

## Post-audit
### Step 1 · 2026-09-25 · 6437b90
- compile: 0 errors, 0 warnings (plugin and Nyarlathotep.Tests)
- tests: `dotnet test Nyarlathotep/Nyarlathotep.Tests` → Passed 600, Failed 0 (61 new: ApiLinesTests, PagingTests, WireFormatTests cases)
- mutation check: sending units to players failed 2 cases; accepting page "0" failed 1; letting a duplicate id read active failed 1; restored code passes
- preflight: exit 0 ("PREFLIGHT OK", tree clean)
- /code-review (eca9522): 2 low findings, both fixed in 6437b90 — a running event no longer startable sent a reason (now reason only with state=disabled); a duplicate id row read active (now only the definition Find returns can be active)
- Codex cross-inspection round 1 (eca9522): REVISE, 5 findings — (1) action=- for a definition without an action: accepted, the pillar's action (amendment A2 changes D2); (2) an overdue cleanup showed an ending row: accepted, filtered; (3) Pages(int.MaxValue) overflowed: accepted; (4) examples were read from the whole contract: accepted, from the section documenting each tag; (5) err with secs never exercised: accepted, contract example and order test added
- Codex cross-inspection round 2 (6437b90): F2–F5 resolved, no new finding; F1 open against the unamended D2
- Codex cross-inspection round 3 (A2 at 76c55c3): F1 resolved, no new finding
- Codex verdict: READY (round 3)
- amendments: A1 (discovered, 5.1: `sub` moves to step 3 with the code it calls), A2 (discovered, 3.1: action named by pillar)
- in-game: none (pure logic step)
- dod status: D1, D2, D3 pass lines added; D4 waits for Wire.Api = 2 in step 2
