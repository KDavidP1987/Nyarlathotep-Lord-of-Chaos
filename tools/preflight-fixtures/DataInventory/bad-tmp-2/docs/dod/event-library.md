# Event library (fixture: Design › Data of docs/dod/event-library.md only)

## Design
### Data
No new data file and no schema change.
- events.json gains definitions written by chat: the same v1 shape (D12), the same .bak rule (one generation).
- The cfg's five [Pillars] lines can now be written by `.nyar pillar` (D14).
- state.json loses a cooldown row when its definition is deleted (D8), or at the next successful save when that write failed (D19).
- The template catalogue is part of the DLL, never on disk.
- Pending deletes live in memory, 30 s at most.

| Artifact | Location | Owner | Retention and deletion | Copies |
|---|---|---|---|---|
| Template catalogue | the DLL (Resources/templates.json embedded) | the mod | replaced with the DLL | one per install |
| Chat-written definitions | BepInEx/config/Nyarlathotep/events.json (+ .bak) | the admin | until the admin deletes them (D8); the .bak holds the previous file until the next write | two (file and .bak) |
| Pillar switches | BepInEx/config/kdpen.Nyarlathotep.cfg | the admin | until changed again | one |
| Server logs (the lines this child adds: templates valid or invalid, catalogue unavailable, pillar on or off, the reload line after each write, and foundation's "admin ran" line for each new command, which names the admin but never a position) | BepInEx/LogOutput.log and logs/NyarDev.log (the server's -logFile); tools/paths-manifest.txt `external: BepInEx/LogOutput*.log` and `server: logs/NyarDev.log` | BepInEx (LogOutput.log) and the game server (NyarDev.log) | overwritten at each boot, after -LogCheck has read them (D30); deleted by the operator; the soak copies are the soak-archive row | one each |
| Pending deletes | server memory (Logic DeleteArming) | the mod | 30 s, or restart | one |
| Soak log archive | %TEMP%\nyar-soak-* (copies of LogOutput.log and NyarDev.log taken before each Session 3 restart) | Claude during step 5 | deleted once soak-report's lines are copied into the feature doc; a leftover fails -Paths (D30) | one |
| Feature doc, audit, plan, reviews | git | the owner | forever in git history | one per file |
| Soak and preflight fixtures | tools/soak-report-fixtures/**, tools/preflight-fixtures/CfgWrites/**, tools/preflight-fixtures/TemplatesJson/bad-3/**, tools/preflight-fixtures/VcfDependency/**, tools/preflight-fixtures/DataInventory/{bad-el,bad-tmp,bad-tmp-2}/**, tools/preflight-fixtures/DependencySuite/**, tools/preflight-fixtures/RollbackRoutes/** | the repo | forever in git | one |
| Session snapshots, release download, rollback worktrees, drill config | as faction-empowerment › Design › Data | its tools | as there | one |

tools/data-inventory.json gets the pending-delete, soak-archive and server-logs entries, and its existing "Event, zone and runtime state files" entry names the events.json.tmp row (D30).
## Build plan
