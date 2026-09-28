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
