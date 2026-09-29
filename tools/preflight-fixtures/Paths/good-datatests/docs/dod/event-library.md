# Event library (fixture: Rollout › Paths walked of docs/dod/event-library.md only, and a plan naming its `dataTests` entry, event-spawns D27)

## Rollout
### Paths walked
Walking the Build plan:
- **Step 1:**
  - docs/audits/event-library.md, docs/features/EVENT_LIBRARY.md.
  - tools/preflight-checks.json (childDocs).
  - Nyarlathotep/Nyarlathotep/Resources/templates.json, Nyarlathotep/Nyarlathotep/Nyarlathotep.csproj, Nyarlathotep/Nyarlathotep.Tests/Nyarlathotep.Tests.csproj.
  - Nyarlathotep/Nyarlathotep/Logic/{Templates,Authoring,Pillars,EventAdmin,CommandArgs,DefinitionEditor,DataStore,ActionGateway,Idempotency,Schedule,Validation}.cs (Schedule and Validation by A5 and A6).
  - Nyarlathotep/Nyarlathotep.Tests/{TemplateLibraryTests,TemplateCommandTests,AuthoringTests,AuthoringCapacityTests,PillarSwitchTests,ReadinessTests,DependencyFailureTests.Library,ControlCaseTests,ControlCases,CommandArgTests,ConfigChangedTests,AuthorizationTests,ContractDocTests,ControlPrecedenceTests,FakeStores,CommandArgTests.Library,ConfigChangedTests.Library,EngineTests}.cs (the last three by A8).
  - Nyarlathotep/Nyarlathotep/Commands/EventCommands.cs and docs/NYARLATHOTEP_DESIGN.md §6 (by A8).
- **Step 2:**
  - Nyarlathotep/Nyarlathotep/Services/{TemplateLibrary,PillarSwitches,EventStore,EventRuntime,HealthMonitor,TriggerBus}.cs (TriggerBus by A5), Nyarlathotep/Nyarlathotep/Commands/{TemplateCommands,PillarCommands,EventCommands,RootCommands}.cs.
  - Nyarlathotep/Nyarlathotep/Plugin.cs, and Nyarlathotep/Nyarlathotep/Core.cs (by A12).
  - tools/preflight.ps1, tools/preflight-checks.json, tools/preflight-fixtures/CfgWrites/**, tools/preflight-fixtures/VcfDependency/**, tools/preflight-fixtures/TemplatesJson/bad-3/**, tools/preflight-fixtures/TemplatesJson/good/** (by A12), tools/preflight-fixtures/DependencySuite/**, tools/preflight-fixtures/TestRuns/**.
  - docs/NYARLATHOTEP_DESIGN.md.
- **Step 3:** tools/preflight-fixtures/Paths/bad-base/**, tools/preflight-fixtures/Paths/bad-transient/** (by A14), tools/rollback-gate.ps1, tools/soak-report.ps1, tools/soak-report-fixtures/**, tools/preflight.ps1, tools/preflight-checks.json (dataTables), tools/preflight-fixtures/DataInventory/bad-el/**, tools/preflight-fixtures/Paths/bad-soak/**, tools/preflight-fixtures/Paths/bad-undeclared/**, tools/preflight-fixtures/RollbackRoutes/**, tools/preflight-fixtures/DataInventory/bad-tmp/**, tools/preflight-fixtures/DataInventory/bad-tmp-2/**, tools/paths-manifest.txt, tools/data-inventory.json, tools/ingame/session-events.py.
- **Steps 3–5, on the server:** BepInEx/plugins/Nyarlathotep.dll, BepInEx/config/kdpen.Nyarlathotep.cfg, BepInEx/config/Nyarlathotep/{events,state}.json{,.bak,.tmp} (by A13), save-data-nyardev/**, logs/NyarDev.log and BepInEx/LogOutput.log; outside the repository %TEMP%\nyar-snap-* (the snapshot), %TEMP%\nyar-soak-* (the soak archive) and %TEMP%\nyar-session (session-events.py's copies of the files it replaces, by A13), each a `temp:` line of the manifest and a Design › Data row; and the self-test scratch folders every step's checks create and remove, %TEMP%\nyar-{selftest,depsuite,snaptest,drilltest}-* (preflight -SelfTest, preflight -DependencySuite, dev-snapshot -SelfTest and rollback-drill -SelfTest; by A14).
- **Step 6:**
  - The six release surfaces: Nyarlathotep/Nyarlathotep/Nyarlathotep.csproj, Nyarlathotep/Nyarlathotep/thunderstore.toml, CHANGELOG.md, Nyarlathotep/Nyarlathotep/CHANGELOG.md, README.md, Nyarlathotep/Nyarlathotep/README.md.
  - Remote writes: the tag v0.5.0 and the GitHub release v0.5.0, declared as `remote-tag:` and `remote-release:` lines of tools/paths-manifest.txt.
  - Nyarlathotep/Nyarlathotep/dist/** and build/*.zip (ignored); %TEMP%\nyar-rel-*, %TEMP%\nyar-rollback-* and %TEMP%\nyar-drill-* from the release tools.
  - docs/dod/event-library.md, docs/dod/nyarlathotep.md, docs/dod/README.md.
- **Build outputs of every step** (`dotnet build`, `dotnet test`, tcli build): Nyarlathotep/**/bin/**, Nyarlathotep/**/obj/**, *.binlog, Nyarlathotep/Nyarlathotep/dist/** and Nyarlathotep/Nyarlathotep/build/**, covered by the existing ignored globs of tools/paths-manifest.txt (`ignored: **/bin/**`, `ignored: **/obj/**`, `ignored: Nyarlathotep/Nyarlathotep/dist/**`, `ignored: Nyarlathotep/Nyarlathotep/build/**`, `ignored: **/*.binlog`); the server logs are its `external: BepInEx/LogOutput*.log` and `server: logs/NyarDev.log` lines; the one glob missing, `temp: nyar-soak-*`, is added in step 3 (D30).
- **Review process:** docs/dod/event-library.reviews.md and docs/dod/event-library.review.html.

## Out of scope
