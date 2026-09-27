# Event library (fixture: the D29 evidence command and Rollout › Rollback of docs/dod/event-library.md)

## Definition of Done
- [ ] D29 · **Rollback gate** · cmd: pwsh tools/rollback-gate.ps1 -From v0.4.0 -To v0.5.0 -Plan event-library → "rollback gate: 4/4"

## Rollout
### Rollback
- **In the repository:** `git revert --no-edit v0.4.0..v0.5.0`, drilled by the rollback gate (D29).
- **On the dev server during the build:** every session is wrapped by tools/dev-snapshot.ps1 (D30).
- **On a server:** install the 0.4.0 DLL. events.json holding chat-written definitions loads unchanged (the same v1 shape, D29), and the cfg's pillar lines are read as before. The template and authoring commands are gone; hand edits work as before. This remains possible after data is written, because 0.5.0 writes nothing 0.4.0 cannot read.
- **Published release:** as faction-empowerment's Rollout › Rollback: tags and releases are never deleted; a bad 0.5.0 is withdrawn by retitling its release, and versions move forward only (Epic S-19).
- **Commit range:** v0.4.0..v0.5.0.
## Out of scope
