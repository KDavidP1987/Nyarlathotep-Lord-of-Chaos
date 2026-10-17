# Reviews — login-notice

## Review 1 · 2026-10-17 · subagent · plan uncommitted · plan 32293 B · 12 items · files 0 · e3b0c44298fc · prompt ef4bcad2f268
F1 advisory · 4.1/14.4 · Epic docs/dod/nyarlathotep.md D41 and S-24 say every [Announcements] default is false with the evidence "announcement defaults: all off (5 switches)"; after D3 that evidence no longer prints its text and no Build-plan step amends it — add an Epic amendment in step 1 rewriting D41 to "all off but LoginEventNotice (6 switches)" and pointing S-24 at D63
F2 advisory · 5.3 · D1's part template "<name> (<n> min left)" contradicts its fails-when "a part differs from the status line of the same instance" (Messages.Status prints "<name>: <n> min left", the apocalypse line its own text) — state one rule and make the Text test assert it
F3 advisory · sizing · Epic A45 records login-notice as S, the plan header says M — align them
F4 advisory · 7.3 · the 60 s gate is used up at connect even when nothing runs, so a reconnect within 60 s after an event started gets nothing — say so in Business rules 3, or consult the gate only when a notice is sent
F5 advisory · 11.2 · on a slow-loading remote server a line sent 10 s after connect may reach a client still loading — record in the feature doc's Open questions that the G-Portal weekend confirms S-3
F6 advisory · 12.4 · the later-step dry runs must land as planted notes in the Log before their items are checked — carry out steps 1 and 2 as written
missing components: none
undeclared families: none
15/15 layers · 50/50 probes
VERDICT: READY
### Dispositions
- F1 · accepted · deferred to the build: an Epic amendment of D41 in step 1 (Log note deferred)
- F2 · accepted · deferred to the build: each part is the Messages.Status element of its instance; D1's wording is amended at the build if the test shows the template conflicts (Log note deferred)
- F3 · accepted · the Epic Log notes the size M (Log note deferred)
- F4 · accepted · deferred: the build consults the gate only when a notice is due to be sent, keeping D2's promise (Log note deferred)
- F5 · accepted · deferred: an Open question in docs/features/LOGIN_NOTICE.md (Log note deferred)
- F6 · accepted · no change; steps 1 and 2 log the planted runs
