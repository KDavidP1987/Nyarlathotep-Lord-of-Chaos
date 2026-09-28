# Reviews: raphael-api-admin

## Review 1 · 2026-09-28 · subagent · plan commit 733d411 · plan 51242 B · 16 items · files 0 · e3b0c44298fc · prompt 95c19dc93bf4
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, given the prompt file and read access to the repository (design §9 D26); the prompt held no Steam ID.

F1 · blocking · 12.4 (and the evidence for 6.2 and 4.4). The controls D1, D3, D4, D5, D6 (ordering), D9 and D12 are tested only by Nyarlathotep.Tests, but that project compiles only `..\Nyarlathotep\Logic\**` (Nyarlathotep.Tests.csproj, Epic S-21). EventRuntime.StartEvent, StopEvent and Purge, TemplateLibrary.UseTemplate, PillarSwitches, Gateway, the purge flow in SpawnCommands, and the new `…Flow` methods and Commands/ApiAdminCommands.cs are all outside Logic/. So HumanReplyTests cannot check that "a path in the list still returns string", ApiTwinTests cannot check that "a twin reaches a service outside Gateway.Run or with another ActionKind", and the one-line catch in D12 and the gate-before-log order in D6 cannot be tested. The capture "running every D1 path over FakeStores" cannot run the Services or Commands replies (Verbs, "argument must be confirm or nothing", "your position could not be read"). docs/dod/profile.md 12.4 says such a control "moves to Logic/ first".
Fix: state that each twin flow lives in Logic/ (e.g. Logic/AdminFlows.cs over interfaces, with fakes in FakeStores.cs), holding the rate gate, the log callback, the gateway call with its ActionKind, the throw catch and the Outcome → wire mapping; Commands/ApiAdminCommands.cs and the Services stay thin shims. For the rest, name a preflight static check with a planted fixture (the Services return types; each twin's ActionKind equals its human command's). Re-point the 4.4 and 6.2 evidence rows at those tests.

F2 · advisory · 10.1/2.1 evidence (D11). `tools/preflight-fixtures/AdminList/bad-3` already exists, and Test-CheckAdminList does not detect a missing adminOnly; Test-CheckCommands does, through `$script:PublicCommands`.
Fix: plant "a twin without adminOnly" as a new `Commands/bad-N` fixture.

F3 · advisory · 14.4. Step 1's Paths walked leaves out Services/TriggerBus.cs (its `Fire` passes a `Func<string>` wrapping EventRuntime.StartEvent), Services/EventStore.cs's wrappers and Logic/AdminLines.cs (NothingToPurge, NotArmed, PurgePrompt).
Fix: add them; add a Business rules 3 row for the System-only StartEvent refusals ("off", ConditionCheck blockers).

F4 · advisory · 4.2/4.4. PillarCommand.Switch's "already off" path ends running events, and its save-failure path ends events when the file says off; this clashes with D9 ("changed=0 queues nothing") and Business rules 2 ("a refusal changes nothing").
Fix: changed=0 with ended>0 answers `ok changed=0 ended=<n>` and still sends the event-end pushes; a failed save answers `io reason=save`; word Business rules 2 as "except what the file now holds (D19)".

F5 · advisory · 3.1. A surplus argument (`.nyar api event set x name a b`) reaches VCF's own human line, breaking "exactly one [NYAR] line".
Fix: give the arity per twin and add a trailing catch-all optional parameter answering badarg, or accept VCF's line and say so in contract §5a.

F6 · advisory · 12.1/3.1. "your position could not be read" for an Admin-location start has no Business rules 3 row.
Fix: add `badarg arg=location reason=no_position`.

F7 · advisory · D5 wording. PurgeArming.Confirm returns NothingToPurge before checking the arming, so an unarmed confirm with nothing to purge answers `state nothing_to_purge`, not `confirm`.
Fix: qualify D5.

F8 · advisory · parent constraint. The Epic's Children entry gives raphael-api-admin "the reads templates, regions and zones"; this plan defers regions and zones per contract §10.
Fix: record an Epic amendment aligning the entry with contract §10 and cite it in Out of scope.

F9 · advisory · D6. Whether a refused twin (badarg, notfound, a control) counts toward the 5/s window is unstated.
Fix: state it; counting every admitted twin is simplest.

Scenarios hunted: surplus-argument twin (3.1, F5); pillar "already off" with running events while a second admin's Raphael listens (4.2/7.2, F4); a cfg save that throws while events end (6.2/4.2, F4); an admin-location start with no readable position (12.1, F6).

14/15 layers · 48/49 probes
VERDICT: REVISE

### Dispositions
- F1 · accepted · Logic/AdminFlows.cs holds every flow over the Logic interface IAdminOps (fake in FakeStores.cs), with AdminFlows.Kinds as the single verb → ActionKind table; Services/AdminOps.cs is one-call shims; reply texts move into Logic/AdminLines.cs; new preflight checks HumanReplies (capture rows cited to v0.5.1 lines) and OutcomeReturns (the shims return Outcome), each with fixtures; the 4.4 and 6.2 evidence rows point at Logic tests (D1, D3, D6, D12)
- F2 · accepted · fixture Commands/bad-2 (D11, Failure & observability)
- F3 · accepted · Step 1 walks Services/{AdminOps,TriggerBus}.cs and Logic/AdminLines.cs; Business rules 3 gains the System-only StartEvent row
- F4 · accepted · D5 and D9 state `ok changed=0 ended=<n>` with the event-end pushes; Business rules 2 excepts the file's content after a failed save and the ended events
- F5 · accepted · each twin has a trailing optional `extra` answering badarg arg=extra; two or more surplus words are VCF's line, stated in contract §5a (D3)
- F6 · accepted · Business rules 3 row no_position
- F7 · accepted · D5 follows PurgeArming's order
- F8 · accepted · Epic A33 (discovered, 15.2) rewrites the Children entry; Out of scope cites it
- F9 · accepted · every admitted twin counts (D6)

## Review 2 · 2026-09-28 · subagent · plan commit 55d3337 · plan 57267 B · 16 items · files 0 · e3b0c44298fc · prompt e95ee05de2d5
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, given the prompt file and read access to the repository (design §9 D26); the prompt held no Steam ID.

F1 · blocking · 10.1. Test-CheckGatewayOnly (tools/preflight.ps1 1634-1678) scans only Commands/, Patches/ and Services/ and accepts a [Mutating] call only inside `Gateway.Run(` or in $script:DispatchedServices; the new Services/AdminOps.cs calls EventRuntime, EventStore and PillarSwitches [Mutating] methods outside Gateway.Run, so -AuthSuite fails in step 1, and adding AdminOps to the dispatched list empties the check (its members are not [Mutating], Logic/AdminFlows.cs is not scanned, and the regex does not match `gateway.Run<T>(`). "Every twin reaches IAdminOps only inside ActionGateway.Run<T>" is prose only.
Fix: state the new rule in D11 (AdminOps dispatched with [Mutating] members; AdminFlows scanned with a `Run<T>(` span as a gate; any AdminOps or IAdminOps member use outside AdminFlows fails), add fixture GatewayOnly/bad-6, and list the check and fixture in step 1's Paths walked.

F2 · blocking · 14.3, 6.2. The capture (`git show v0.5.1:`), the rollback range v0.5.1..v0.5.2 and the version 0.5.2 in D10, D13, D16 and the handoff assume a tag v0.5.1 that does not exist; walkable-spawns has a no-go branch where 0.5.1 is not released.
Fix: state a precondition (step 1 after v0.5.1 is tagged) and a fallback (base v0.5.0, release 0.5.1) as an assumption.

F3 · advisory. Step 1 adds seven WireError members but contract §4 gains them in step 2; WireFormatTests.Every_error_code_is_one_of_the_contract_codes (lines 70-82) then fails, and contract §7 wants code and contract in one commit.
Fix: move the members to step 2, or add the §4 rows in step 1.

F4 · advisory. Business rules 6 omits the ready guard. Fix: write the full order.

F5 · advisory. D13's "seven twins pasted within one second" is likely impossible by hand; D6 proves the rate. Fix: drop it or name the tool.

F6 · advisory (overload routing, 7.1/9.2). With the trailing `extra`, VCF's per-argument-count registration could route `pillar list <word>` to the switch overload. Fix: an assertion or unit case listing each twin's overload word counts.

F7 · advisory (9.1). Reads are exempt from the rate gate without a stated reason. Fix: one line in 13.2.

F8 · advisory. OutcomeReturns proves the return type but not that the shims are one call deep. Fix: fail on a body with more than one statement or a branch.

EARLIER: all resolved

14/15 layers · 47/49 probes
VERDICT: REVISE

### Dispositions
- F1 · accepted · D11 states the GatewayOnly rule (AdminOps dispatched, members [Mutating]; Logic/AdminFlows.cs scanned, `.Run<` spans are gates; member uses elsewhere fail) with fixtures GatewayOnly/bad-6 and bad-7; step 1 walks them
- F2 · accepted · Precondition line and S-9 (reversible: base v0.5.0 and release 0.5.1 by a corrected amendment before step 1 on no-go; the owner may renumber)
- F3 · accepted · step 1 uses a Logic RefusalCode enum; WireError's seven members and contract §4's rows land together in step 2
- F4 · accepted · Business rules 6 gives the full order
- F5 · accepted · the in-game rate step is dropped; D6 alone proves it
- F6 · accepted · Logic/ApiCommandTable.cs with ApiOverloadTests, matched to ApiAdminCommands by the WireContract check (D11)
- F7 · accepted · Performance 13.2 row for the unmetered reads
- F8 · accepted · OutcomeReturns forbids branches in AdminOps bodies, fixture OutcomeReturns/bad-2

## Review 3 · 2026-09-28 · subagent · plan commit ffe4712 · plan 60906 B · 16 items · files 0 · e3b0c44298fc · prompt 05719d9dff83
Reviewer: a fresh-context general-purpose Claude subagent, told read-only, given the prompt file and read access to the repository (design §9 D26); the prompt held no Steam ID.

F1 · blocking · 4.5. "Every control is ControlCases' raphael-api-admin rows" defines the set by the list itself, and nothing checks it: ControlCaseTests compares only Resources/event-library.md (Nyarlathotep.Tests.csproj line 28), so D11's "a control lacks its row" is run by none of D11's commands. By the project's definition (every D-item with a `(fails when:` clause) D4, D10, D11, D15 and D16 are controls too.
Fix: link this plan into the test Resources in step 2, extend ControlCaseTests to compare its controls with its rows, define the set as every fails-when item, and add the missing rows or reasons.

F2 · advisory. D1's OutcomeReturns rule contradicts IAdminOps' purge counts and admin position (a position read needs `?`). Fix: take the position from the shim's reader; return the counts as fields or exempt named read members.

F3 · advisory. D11's GatewayOnly clause drops the dispatched-service exemption and matches by name, so generic names (Reload, Edit, Purge, StartEvent) in EventRuntime, EventStore and PillarSwitches would fail. Fix: keep the exemption and give IAdminOps members distinct names (e.g. `Op*`).

F4 · advisory. The human `.nyar purge` ask writes no admin log line today (SpawnCommands.cs:45-51); a shared flow that always logs changes that, and nothing tests that argument refusals come before the log. Fix: state whether the ask logs; add ApiTwinTests cases.

F5 · advisory. Contract §10.1 ("A refusal changes nothing") and §10.3 io ("Nothing changed") contradict Business rules 2 and D12; D10 moves the text without correcting it. Fix: reword the moved rows and add a ContractDocTests case.

F6 · advisory. HumanReplies' fixture mode cannot run `git show v0.5.1:`; capture rows carry no reproducing input. Fix: a `v0.5.1/<path>` copy in the fixture; a scenario id per row.

F7 · advisory. A rate-limited or unknown-verb err line echoes the raw verb before any check. Fix: WireValue-map it, `-` when empty, cut to 120 bytes, in D8's grammar case.

F8 · advisory. Step 3's Paths walked omits tools/paths-manifest.txt (the remote-tag and remote-release lines). Fix: list it.

EARLIER: all resolved

14/15 layers · 48/49 probes
VERDICT: REVISE

### Dispositions
- F1 · accepted · controls are every D-item with a fails-when clause (D1-D12, D14-D16); step 2 links this plan into the test Resources and ControlCaseTests compares it, on walkable-spawns step 2's per-slug ControlCases (D11, Business rules 7)
- F2 · accepted · IAdminOps' mutating members return Outcome; OpPurgeCounts is a read outside the rule; the position comes from the shim (D1, Interfaces)
- F3 · accepted · the dispatched-service exemption stays and IAdminOps members are named Op* (D11)
- F4 · accepted · the purge ask logs nothing, as in 0.5.1; ApiTwinTests cases for badarg and the ask (D3)
- F5 · accepted · §5a and §4's io row carry Business rules 2's wording, with a ContractDocTests case (D10)
- F6 · accepted · fixture copies under `v0.5.1/<path>`; scenario ids per capture row (D1, Failure & observability)
- F7 · accepted · D8 covers the echoed verb
- F8 · accepted · step 3 lists tools/paths-manifest.txt
