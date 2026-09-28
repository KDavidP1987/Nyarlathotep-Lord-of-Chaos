# Raphael api 4 — admin action twins and admin reads

**Status:** in build (docs/dod/raphael-api-admin.md, audit docs/audits/raphael-api-admin.md); step 1 (typed outcomes,
no behaviour change) next. Nothing of it ships yet; 0.5.1 is the current release.

## Goal

Every admin action Nyarlathotep has in chat gets a machine-readable twin, so the Raphael client can press a button,
read one answer line and show success or the reason it failed. Admins without Raphael see no change: the human
commands reply exactly as in 0.5.1. The wire contract is docs/RAPHAEL_INTEGRATION_CONTRACT.md (§10.1–10.3 until
api 4 ships, then §3, §4 and §5a).

## What ships

- **Step 1 (no behaviour change):** every admin mutation runs through Logic/AdminFlows.cs, whose flows reach the game
  only through IAdminOps (Services/AdminOps.cs, one call per member). Every Logic path of the flows returns an
  `Outcome` (the human text plus a refusal code, argument and reason), and each human command replies its
  `Human` text. A capture of 0.5.1's replies (Nyarlathotep.Tests/Fixtures/human-replies-0.5.1.txt) pins them.
- **Step 2:** `.nyar api event|template|pillar|purge …` twins answering one `[NYAR:ok]` or `[NYAR:err]` line; the
  reads `templates`, `template info`, `pillar list` and `killswitch`; a rate gate of 5 twins per admin per second;
  contract api 4.
- **Step 3:** Session 1 in game, then release 0.5.2.

## Test plan

- **Unit tests:** HumanReplyTests and OutcomeCodeTests (step 1); ApiTwinTests, RateGateTests, ApiLinesTests
  AdminReads, WireFormatTests AdminTwins, the push, dependency-failure and privacy tests (step 2).
- **Preflight:** HumanReplies and OutcomeReturns (step 1), GatewayOnly's AdminOps rule, WireContract and Commands
  fixtures (steps 1–2).
- **Session 1 (owner, step 3):** each twin on bandit-ambush, its human effect in `.nyar event info` or
  `.nyar pillar list`, start twice, purge ask and confirm with the killswitch push, the four reads, and every line
  whole in chat (D13).

## Open questions

- None yet.

## Test results

No sessions yet.
