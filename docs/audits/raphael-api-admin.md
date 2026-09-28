# Audit — raphael-api-admin

Build plan steps 1–3 of docs/dod/raphael-api-admin.md. Each step has one "### Step <n>" entry under "## Pre-audit"
and one under "## Post-audit"; every post-audit entry carries a "Codex verdict:" line. Session log checks are lines
"- session <n> log check: …". Rollback base: v0.5.1.

## Pre-audit
### Step 1 · 2026-09-28 · d30f985
- pre-child: walkable-spawns released 0.5.1 (tag v0.5.1 at 437dbb4, GitHub pre-release, release-verify "hashes equal"); its D1-D12 pass and its close waits on the owner's confirmation of A11 (defect); the precondition of this plan holds and S-9's base v0.5.1 stands
- git status: clean at d30f985 (the start commit: A1-A12, the feature doc, childDocs, snapshotSessions, dataTables and the data-inventory rows)
- compile: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` → 0 Warning(s), 0 Error(s)
- tests: 1387 passed
- preflight: PREFLIGHT OK ("data inventory: 34 entries; 54/54 globs and files, 60/60 plan rows")
- dod status: raphael-api-admin 0/16 verified (just started); review subagent (A1-A12 are Review 4's own advisories and hunted scenarios)
- tooling versions: git 2.53.0, gh 2.92.0, pwsh 7.5.2, .NET SDK 10.0.302 (builds net6.0), codex-cli 0.151.0
- feature doc read: docs/features/RAPHAEL_API_ADMIN.md, created by the start commit (Status: in build; no open questions); plan D1, D2, D11, Business rules 3, Interfaces, Failure & observability, Build plan step 1, Rollout › Paths walked; Commands/{Event,Template,Pillar,Spawn}Commands.cs, Services/{EventRuntime,EventStore,PillarSwitches,TemplateLibrary,Gateway}.cs, Logic/{Precedence,EventCatalog,DefinitionEditor,ActionGateway,Idempotency,Engine,EventAdmin,Authoring,Pillars,AdminLines,Wire,DataStore}.cs; contract §4 and §10
- in-game baseline: not needed (step 1 has no in-game part; Session 1 is step 3)
- found on the way, recorded before the code that depends on it:
  - A13 (discovered, ~D2, layer 4.1): six refusals of the D1 paths had no row in Business rules 3.
  - A14 (discovered, layer 5.1): the start flow needs a `Definitions` read before the log line; OpPurgeCounts is read inside Run<T>.

## Post-audit
### Step 1 · 2026-09-28 · a6b2059
- built: Logic/Outcome.cs (Outcome, RefusalCode, Reasons, FileErrors); Logic/AdminFlows.cs (IAdminOps, one flow per admin verb, Kinds); Services/AdminOps.cs (11 one-expression members); every D1 Logic path returns Outcome; the human commands reply outcome.Human; tests HumanReplyTests (85 rows of Fixtures/human-replies-0.5.1.txt), OutcomeCodeTests (a case per Business rules 3 row, checked against the plan's code, arg and reason cells); preflight checks HumanReplies and OutcomeReturns, GatewayOnly extended (A1, A2)
- compile: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` → 0 Warning(s), 0 Error(s); tests: 1545 passed
- preflight: PREFLIGHT OK ("human replies: 89/89 at v0.5.1", "outcome returns: 11/11", "gateway: only ActionGateway mutates (9 call sites, 13 admin ops inside Run<T>)"), also on the committed tree, where the capture's adding commit is read; -SelfTest "36/36 checks, 7/7 external selftests (3 fixtures each, 158 extra bad fixtures …)"
- not foreseen by the plan, within its items: the flows Reload, Delete and UseTemplate are named ReloadEvents, DeleteEvent and TemplateUse (GatewayOnly matches [Mutating] methods by name; renaming beats loosening the rule); the plan is linked into the test output as Resources/raphael-api-admin.md in step 1 (step 2 lists it; D2's test needs it now); the HumanReplies members leave out the two read commands' members (list, info: no mutation); extra fixtures OutcomeReturns/bad-3 (an IAdminOps declaration returning string), GatewayOnly/bad-8 to bad-11, HumanReplies/bad-3 to bad-5
- /code-review (fresh subagent, read-only) on the uncommitted diff: replies, order (checks, log line, gateway), log level, purge reads inside Run<T>, caller overloads and Business rules 3 codes all match; F1 (low) the Logic gate took any `.Run<T>(` span, the denied argument included → fixed with Codex F2; F2 (low) a method group `ops.OpReload` taken outside the gate was missed → fixed (OpAccessRx matches `.Op<Name>` called or not; fixture GatewayOnly/bad-11)
- Codex verdict: REVISE (round 1) — F1 OutcomeCodeTests read only the Human text column → fixed (PlanTable parses code, arg and reason; each case must be admitted by its row; A_cell_allows_its_words_and_a_changed_cell_refuses); F2 any `.Run<T>(` counted as the gate → fixed (only the work argument of `<g>.Run<…>(`, <g> declared an ActionGateway; fixtures bad-9 denied argument, bad-10 Task.Run<T>); F3 placeholders and order ignored → disposition: placeholder names are the test's own and HumanReplyTests renders them by name, so a swapped capture fails the test; fixed what remained: literal parts in order (fixture HumanReplies/bad-3) and a committed capture equal to its adding commit (bad-4); F4 (advisory) new, copy, delete, template use and pillar log before their semantic checks → kept, v0.5.1's order (step 1 changes no behaviour); carried to step 2 for the twins
- Codex verdict: REVISE (round 2) — F1-F4 accepted; F5 a renamed committed capture skipped the no-edit check → fixed (git log --follow; a capture at HEAD with no adding commit fails closed; fixture bad-5); F6 (advisory) commas in a type argument list split the work span → fixed (GenericArgsRx with C#'s follow-token rule; GatewayOnly/good's Stop reads `Pick<Outcome, string>(…)`)
- Codex verdict: READY (round 3) — "No new findings. F5 and F6 are correctly addressed, including the identified edge cases."
- in-game: none in step 1 (Session 1 is step 3)
