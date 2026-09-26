---
dod: 2
rubric: 2
id: dod-20260926-evl1
slug: event-library
title: Event library — built-in templates and in-game authoring
status: draft
size: L
parent: nyarlathotep
kind: feature
created: 2026-09-26
baselined: none
closed: none
commit: 23f2ba4
coverage_author: 15/15 layers · 49/49 probes
coverage_reviewer: pending
review: pending
---

# DoD: Event library — built-in templates and in-game authoring

**Size:** L. It touches several modules: Logic (a new Templates.cs with the catalogue and its lines, a new Authoring.cs with the create, copy, delete and field planners, a new Pillars.cs with the switch map and its planner, EventAdmin, CommandArgs, DefinitionEditor, ActionGateway, Idempotency), Services (a new TemplateLibrary and PillarSwitches, EventStore, EventRuntime), Commands (a new TemplateCommands and PillarCommands, EventCommands, RootCommands), Resources (a new embedded templates.json), the csproj files, tools (preflight checks and fixtures, a new soak-report.ps1, session-events.py) and docs (the design doc §6, a new feature doc). It adds a second writer of the BepInEx cfg (the `.nyar pillar` command) and new writes of events.json. It adds no dependency and no new data file.
**Planned:** interactively. This is a child of the approved Epic `nyarlathotep`; its `## Child constraints` › event-library entry and Epic D47 govern this plan. The owner settled the scope on 2026-09-26 (S-1); the assumptions marked reversible below are decisions taken inside that scope without asking, each with its fallback. The build starts only after faction-empowerment releases 0.4.0 (S-2).
**Request:** the owner's scope for event-library (2026-09-26): a built-in template catalogue compiled into the DLL with `.nyar template list|info|use`, in-game authoring with `.nyar event new|copy|delete|set` and `location here`, `.nyar pillar list|<name> on|off` saved to the cfg, a readiness column in `event list`, six starter templates shipped disabled, the live-use milestone of Epic D47 (a fresh install plus five chat commands runs an event with no file edits), a four-hour soak, and release 0.5.0.

## Definition of Done
- [ ] D1 · **Template catalogue file** Resources/templates.json is an EmbeddedResource (manifest name "Nyarlathotep.Resources.templates.json") in the events.json v1 shape (`{"schemaVersion": 1, "events": [...]}`), each entry a full definition with only the event keys and `"enabled": false`. Logic/Templates.cs `TemplateCatalog.Load(bytes, units, factions)` parses it through EventValidator.Parse and returns the templates with each one's disabled reason, if any. No code writes the catalogue: it is read from the assembly only. Template ids are distinct from each other and from the ids of Resources/events.default.json. The test project links templates.json as content · test: Nyarlathotep.Tests TemplateLibraryTests (fails when: a template ships enabled, a template has a key outside the event keys, two templates share an id, a template id equals an events.default.json id, the real file is disabled by the validator under the test catalog fakes, or Load accepts a file of another schemaVersion)
- [ ] D2 · **Six starter templates** the catalogue holds exactly legion-weekend-surge, bandit-vengeance, undead-nightfall, militia-crackdown, bandit-ambush and undead-rising, with the pillar, trigger, conditions, duration and action of Business rules 2, every unit and faction name taken from Reference Data/unit_index.tsv (S-4), and every bosses list at most 20 names · test: Nyarlathotep.Tests TemplateLibraryTests StarterTemplates (fails when: an id is missing or a seventh appears, or a template's pillar, trigger type, days, times, phase, bosses, factions, stats, units, counts, waves, interval, radius, location type, cooldown or duration differs from Business rules 2)
- [ ] D3 · **Templates valid in game** at boot, Services/TemplateLibrary loads the catalogue against the live unit and faction catalogs and logs "templates: <v>/<n> valid"; an invalid template adds "template <id> invalid: <reason>" and stays listed with its reason · manual: Session 1 boots the dev server with the new DLL; BepInEx/LogOutput.log shows "templates: 6/6 valid" and no "template … invalid" line
- [ ] D4 · **template list and info** `.nyar template list [pillar]` replies one line per template, "<id> <pillar> <trigger> \"<name>\"", with " (in events.json)" when events.json already holds that id and " invalid: <reason>" for an invalid one, ten per page with foundation's paging (Logic/Paging.cs); the pillar filter takes empowerment, spawns, boss, zones or sieges and an empty result replies "no templates for pillar <p>"; an unknown filter replies "unknown pillar <x>; use empowerment, spawns, boss, zones or sieges". `.nyar template info <id>` replies EventLines.Info for that template; an unknown id replies "no template <id>; .nyar template list shows them" · test: Nyarlathotep.Tests TemplateCommandTests (fails when: a line lacks the pillar, trigger or name, the in-events.json marker or invalid reason is missing or wrong, the filter returns another pillar's templates or treats an unknown pillar as all, an empty pillar replies nothing, a page holds more than ten lines, or info on an unknown id throws)
- [ ] D5 · **template use** `.nyar template use <t> [as <id>]` appends a copy of template t to events.json with `"enabled": false` and the id t (or the given id; its name is kept) and replies "template <t> added as <id> (disabled); .nyar event enable <id> to arm it". It writes nothing and replies with one line when t is unknown, t is invalid ("template <t> is invalid: <reason>"), the id exists ("event <id> already exists; use .nyar template use <t> as <newId>"), the id breaks the id rule ("id must be 1-32 of a-z 0-9 -"), events.json already holds 200 definitions ("events.json holds 200 definitions, the limit"), or the D12 file refusals apply · test: Nyarlathotep.Tests TemplateCommandTests Use (fails when: the copy is enabled, differs from the template in any field but id and enabled, a refused use changes a byte of events.json, `as` is ignored, or a second use of the same id adds a duplicate)
- [ ] D6 · **event new** `.nyar event new <id> <pillar>` appends a disabled skeleton named after its id: for empowerment a Manual trigger, durationSeconds 600 and an Empower action on Faction_Bandits with physicalPower 1.2; for any other pillar a Manual trigger, durationSeconds 600 and a SpawnWaves action of CHAR_Bandit_Thug × 3, 1 wave, intervalSeconds 60, radius 8, location Admin; the reply is "event <id> created (disabled, <pillar>); set its fields with .nyar event set". An unknown pillar, an existing or ill-formed id and the 200 limit each reply one line and write nothing · test: Nyarlathotep.Tests AuthoringTests New (fails when: a skeleton fails validation, is enabled, has another trigger or action than the above, or a refused new changes a byte of events.json)
- [ ] D7 · **event copy** `.nyar event copy <id> <newId>` appends a verbatim copy of definition id under newId with `"enabled": false` and replies "event <id> copied to <newId> (disabled)"; an unknown source, an existing or ill-formed newId and the 200 limit each reply one line and write nothing. A disabled (invalid) source is copied as it stands, reason and all · test: Nyarlathotep.Tests AuthoringTests Copy (fails when: the copy differs from the source in any field but id and enabled, is enabled, or a refused copy changes a byte of events.json)
- [ ] D8 · **event delete, confirmed** `.nyar event delete <id>` writes nothing and replies "delete <id>? run .nyar event delete <id> confirm within 30 s"; `.nyar event delete <id> confirm` from the same admin within 30 s removes the definition, clears its state.json cooldown row, and replies "event <id> deleted (events.json.bak keeps the previous file)". Logic/Idempotency.cs `DeleteArming` (the PurgeArming pattern) keys the pending delete by admin id and event id. The confirm first looks the event up: if it no longer exists (for example, admin A armed it, admin B then deleted it with B's own delete and confirm, and A confirms), it replies "unknown event <id>". Pending confirmations are per admin: a confirm without a pending delete of the same admin, after 30 s, or for another id replies "no delete pending for <id>". A delete of a running event replies "event <id> is running; stop it first" and arms nothing, and a confirm when the event started after arming refuses the same way · test: Nyarlathotep.Tests AuthoringTests Delete (fails when: a first call writes, a confirm deletes after 30 s or from another admin, a running event is deleted, the cooldown row stays, the confirm deletes a definition other than id, A's confirm after B's delete replies anything but "unknown event <id>", or a confirm after 30 s or without the same admin's arming replies anything but "no delete pending for <id>")
- [ ] D9 · **Trigger fields settable** `.nyar event set <id> <field> <value>` takes trigger.type (Manual, Schedule, GameTime or VBloodKilled; the trigger is replaced by that type's default: Schedule Sat 20:00, GameTime night, VBloodKilled any, Manual), trigger.days (a comma list of Sun..Sat, 1-7 distinct), trigger.times (a comma list of HH:mm, 1-12), trigger.phase (day or night) and trigger.bosses (any, or a comma list of 1-20 CHAR_ names); a field of another trigger type replies "trigger.<f> needs a <Type> trigger" and writes nothing; each value is checked for its character set and shape in Logic/CommandArgs.cs before any write, and name knowledge is left to the reload's validator, whose reason the reply appends ("; now disabled: <reason>") as the existing set does · test: Nyarlathotep.Tests AuthoringTests TriggerFields and CommandArgTests (fails when: a value with a space, quote, brace or control character reaches the file, an eighth day, a 13th time, 24:00, a 21st boss or "dusk" is written, a field of another trigger type is written, or trigger.type leaves keys of the old type behind)
- [ ] D10 · **Action fields settable** `.nyar event set` takes action.factions (a comma list of 1-5 Faction_ names) on an Empower action and action.units (a comma list of 1-10 `CHAR_<name>[:<count>]` entries, count 1-50, default 1) on a SpawnWaves action, beside the fields already settable (the stats, waves, interval and radius); a field of the other action type replies "<field> is not a <Type> field" and writes nothing; the "counts" of the scope are the `:<count>` suffixes (S-8) · test: Nyarlathotep.Tests AuthoringTests ActionFields and CommandArgTests (fails when: a sixth faction, an 11th unit, a count of 0 or 51, a name outside `[A-Za-z0-9_]`, or a field of the other action type is written, or a count-less entry is not written as count 1)
- [ ] D11 · **location here** `.nyar event set <id> location here` writes action.location as `{"type": "Point", "x": <x>, "z": <z>}` from the admin's position (Services reads the character's Translation and passes x and z to Logic `LocationArg.FromPosition`, which rounds to 0.1 and bounds to ±10000) and replies "event <id> action.location = Point <x>, <z>"; on an Empower action it replies "location is a SpawnWaves field" and writes nothing · test: Nyarlathotep.Tests AuthoringTests Location (fails when: the point is not rounded to 0.1, a position beyond ±10000 is written, the location keeps its old type, or an Empower definition gets a location)
- [ ] D12 · **Writes equal a file edit** every write of D5-D11 and D8's confirm goes through Logic/DefinitionEditor.cs over IFileStore: it reads events.json, edits it as JsonNode, writes it with `WriteEdit(bytes, LoadedStamp)` (the stale refusal "events.json changed on disk, run .nyar event reload first", the read-only newer-schema refusal, one .bak generation), reloads, and raises exactly one config-changed; a write that would make events.json larger than 1 MB (foundation MaxFileBytes) replies "events.json would exceed 1 MB; nothing written"; a write that the command refuses changes no byte of events.json or its .bak; a running instance keeps the end time it started with (Business rules 5a); the result after reload equals what a hand edit of the same JSON plus `.nyar event reload` gives · test: Nyarlathotep.Tests AuthoringTests Equivalence and ConfigChangedTests (fails when: a command writes past a stale stamp or a newer schema, skips the .bak or the reload, raises zero or two config-changed notices, a refused command touches events.json or its .bak, the reloaded set differs from the hand-edit-plus-reload set for any of the seven write kinds, the over-1 MB fixture gets another reply or events.json's SHA-256 differs before and after it, or a set of durationSeconds, a copy or a pillar switch changes the end time of a running instance)
- [ ] D13 · **Pillar name map** Logic/Pillars.cs maps the chat names to the [Pillars] keys: empowerment → FactionEmpowerment, spawns → EventSpawns, boss → BossReinforcements, zones → DefendedZones, sieges → SiegeWaves; the test project links Config/Settings.cs as text content and the test reads every `Bind("Pillars", …)` key from it · test: Nyarlathotep.Tests PillarSwitchTests Map (fails when: a Bind key of [Pillars] has no chat name, a chat name maps to a key Settings.cs does not bind, or two names share a key)
- [ ] D14 · **Pillar commands** `.nyar pillar list` replies one line per pillar, "<name> on|off (Pillars.<Key>)", and a first line "General.Enabled is off: nothing starts" while the master switch is off; `.nyar pillar <name> on|off` sets only that ConfigEntry's Value through Services/PillarSwitches over Logic's IPillarStore, and BepInEx saves the cfg (SaveOnConfigSet, S-11); it takes effect at once (EventRuntime reads the entries live), and replies "pillar <name> on (saved to cfg)"; setting a pillar to its current value replies "pillar <name> already on" and saves nothing; `off` ends that pillar's running events through the stop path ("event <id> ended (pillar off)", S-7); an unknown name or state replies with the five names or "use on or off". Ordering in one main-thread cycle (S-3): a due start dispatched before the off runs and is then ended by the off through the S-7 path; an off processed first means the start is not made (a System start silently, an admin start with "pillar <name> is off"), so `event list` shows `off (pillar)` · test: Nyarlathotep.Tests PillarSwitchTests Commands and Ordering (fails when: a no-op saves, `off` leaves a running event of that pillar or ends another pillar's, an unknown name writes, the master-switch line is missing while General.Enabled is off, a start dispatched before the off survives it, or a start processed after the off is made)
- [ ] D15 · **Pillar switch persists** the saved cfg differs from the one before in the one switched line; comments and every other key stay; the value holds after a restart · manual: Session 2 copies BepInEx/config/kdpen.Nyarlathotep.cfg, runs `.nyar pillar spawns on`, diffs the file (one line: EventSpawns = true), restarts the server, and `.nyar pillar list` shows "spawns on (Pillars.EventSpawns)"
- [ ] D16 · **Readiness column** readiness is computed from Logic/Precedence.cs's StartBlocker order, so the column and a start refusal name the same cause; `.nyar event list` lines become "<id> <readiness> <pillar> <trigger>[ RUNNING]", readiness being the first that applies of `invalid: <reason>` (then the line is "<id> invalid: <reason>"), `off (pillar)`, `off (event)` and `ready`; the list's first line is "General.Enabled is off: nothing starts" while the master switch is off · test: Nyarlathotep.Tests ReadinessTests and ControlPrecedenceTests (fails when: a definition that is invalid, disabled and whose pillar is off shows anything but `invalid: <reason>`, the column and EventEngine.Start's refusal name different causes for any combination of purge, General.Enabled, pillar, MaxConcurrentEvents and the definition's state, an invalid definition shows off or ready, a definition with its pillar off and itself disabled shows off (event), an enabled valid definition with its pillar on shows anything but ready, or the order changes)
- [ ] D17 · **Admin-only and gateway** every new command is adminOnly; ActionKind gains CreateEvent, DeleteEvent and SetPillar, granted to Admin only in the ActionTable; template use, event new and copy run as CreateEvent, delete confirm as DeleteEvent, the new set fields and location here as SetEventField, pillar on and off as SetPillar; every [Mutating] method of Services/TemplateLibrary, Services/PillarSwitches and the EventStore writers is called only inside Gateway.Run · cmd: pwsh tools/preflight.ps1 -AuthSuite → "auth suite: pass (tests, commands, admin list, gateway)" (fails when: a new command is not adminOnly, a new ActionKind is granted to Operator, System or Player, a [Mutating] call sits outside Gateway.Run, or the filter runs 0 tests)
- [ ] D18 · **Static checks and selftest** preflight gains Test-CheckCfgWrites: a write to a [Pillars] ConfigEntry (`.Value =` on EmpowermentEnabled, EventSpawnsEnabled, BossReinforcementsEnabled, DefendedZonesEnabled or SiegeWavesEnabled) anywhere but Services/PillarSwitches.cs, or a `ConfigFile.Save` call anywhere (the save is BepInEx's own, S-11), fails, printing "cfg writes: only PillarSwitches (<n> call sites)"; fixtures CfgWrites/bad (a Commands file setting EventSpawnsEnabled.Value), bad-2 (a Services file, PillarSwitches.cs included, calling Config.Save), good (copies of the real files) and empty are registered in tools/preflight-checks.json; TemplatesJson/bad-3 ships templates.json with one template enabled; the soak-report selftest (D24) joins `externalSelfTests`, beside the release-time selftests faction-empowerment registered, which this child re-runs at v0.5.0: tools/release-verify.ps1 -SelfTest (git, gh and GitHub: a missing asset and a differing hash each fail), tools/repo-rollback-drill.ps1 -SelfTest (git: a missing tag and a conflicting revert each fail), tools/rollback-gate.ps1 -SelfTest, tools/dev-snapshot.ps1 -SelfTest and tools/rollback-drill.ps1 -SelfTest; tools/preflight-checks.json maps event-library to docs/features/EVENT_LIBRARY.md in childDocs · cmd: pwsh tools/preflight.ps1 -SelfTest → "selftest: <n>/<n> checks, <k>/<k> external selftests"; then pwsh tools/preflight.ps1 → PREFLIGHT OK with "secrets: none", "pillar defaults: all off (5 switches, 2 templates)", "templates: 2 valid" and "cfg writes: only PillarSwitches (<n> call sites)" (fails when: a bad or empty fixture passes, the good one fails, a fixture or the soak selftest is unregistered, a registered release-time selftest prints its success line on its missing-asset, differing-hash, missing-tag or conflicting-revert case, a tracked file holds a token shape, or a Resources/*.json ships an enabled event)
- [ ] D19 · **Dependency failures** through Logic's IFileStore and IPillarStore fakes: a failed events.json write replies "could not write events.json: <reason>" and leaves the loaded set and the file unchanged; a cfg save that throws (the file possibly truncated or half written) is followed by ConfigFile.Reload, which re-reads the file on disk and sets the in-memory entry to what the file holds (a missing or unparsable key reads as its default, off); the reply reports the file, "pillar <name> could not be saved; the file says <on|off>", and the log adds the reason; a missing or unparsable catalogue logs "template catalogue unavailable: <reason>" once at boot, every template command replies "template catalogue unavailable", and every other command works; `location here` from a VCF context with no character or no position (the server console, or a character not yet in the world) replies "location here needs your character in the world" and writes nothing; a day and night phase source that throws faults only the GameTime definition read from it: its trigger does not fire, the failure is logged once per streak, and every other definition's trigger fires on the same tick · test: Nyarlathotep.Tests LibraryDependencyFailureTests and PillarSwitchTests SaveFailure (fails when: a failed write changes the loaded set, a failed save leaves an in-memory value that differs from the file, the reply after a fake store that truncates the file and then throws says anything but "the file says off", a fake that writes the new line and then throws is reported as anything but "the file says on", a catalogue failure throws, logs more than once, or disables a non-template command, a context without a character or position writes a location or throws, or a throwing phase source stops another definition's trigger, starts the GameTime event, or logs more than once per streak)
- [ ] D20 · **Design doc commands** docs/NYARLATHOTEP_DESIGN.md §6 lists `.nyar template list|info|use`, `.nyar event new|copy|delete`, `event set` and `.nyar pillar list|<name> on|off`, each as admin, and gains a field table of every settable field (field, family, permission). Logic/CommandArgs.cs exposes the settable fields as code data, `SettableFields` (name → family → permission; families: definition, trigger, empower action, spawn action, location), and SettableValue accepts exactly those names; ContractDocTests compares the §6 command table with the declared command forms and the §6 field table with SettableFields, both ways · test: Nyarlathotep.Tests ContractDocTests and CommandArgTests (fails when: a declared command form or a SettableFields entry is missing from §6, §6 lists a form or field the code does not have, a field's family or permission differs between §6 and SettableFields, a new form's permission is anything but admin, or SettableValue accepts a name outside SettableFields or refuses one inside it)
- [ ] D21 · **Authoring smoke in game** a fixed smoke list shows each new command's reply whole in the real chat (profile note 11.2); exhaustive behaviour (bounds, refusals, paging, stale writes, precedence) is the automated items' (D4-D16) · manual: Session 2 (owner, 127.0.0.1:9876) runs these cases in order and records each reply verbatim, one line per case, in the feature doc; a case passes when its reply is whole (no missing or eaten character) and matches the text in its cited item: (1) `.nyar template list` → six lines (D4); (2) `.nyar template list zones` → "no templates for pillar zones" (D4); (3) `.nyar template info bandit-ambush` (D4); (4) `.nyar template use bandit-ambush` → "added as bandit-ambush (disabled)" (D5); (5) the same again → "already exists" with the `as` hint (D5); (6) `.nyar template use bandit-ambush as ambush-2` (D5); (7) `.nyar event new my-surge empowerment` (D6); (8) `.nyar event copy ambush-2 ambush-3` (D7); (9) `.nyar event set my-surge trigger.type Schedule`, then `.nyar event set my-surge trigger.days Sat,Sun` (D9); (10) `.nyar event set my-surge action.factions Faction_Legion` (D10); (11) `.nyar event set ambush-2 action.units CHAR_Bandit_Thug:3` (D10); (12) at a marked spot `.nyar event set ambush-2 location here`, then `.nyar event enable ambush-2`, `.nyar pillar spawns on`, `.nyar event start ambush-2` → units appear at the marked spot (D11, D14); (13) `.nyar event delete ambush-2` while it runs → "is running; stop it first" (D8); (14) `.nyar event stop ambush-2`, `.nyar event delete ambush-3`, `.nyar event delete ambush-3 confirm` → deleted (D8); (15) `.nyar pillar list` → five lines (D14); (16) `.nyar pillar spawns off` → "pillar spawns off (saved to cfg)" (D14); (17) `.nyar event list` → bandit-ambush shows `off (pillar)` (D16)
- [ ] D22 · **Non-admins refused** a non-admin client gets VCF's refusal for every new command and nothing changes · manual: Session 2, a second client without admin runs template use, event new, copy, delete, delete confirm, set, location here and pillar on; the SHA-256 of events.json and kdpen.Nyarlathotep.cfg before and after are equal and the log shows no "admin ran" line for them
- [ ] D23 · **Live-use milestone** (Epic D47) on a fresh install, five chat commands run an event with no file edits · manual: Session 2 starts from `pwsh tools/dev-snapshot.ps1 -Save s2` followed by deleting BepInEx/config/kdpen.Nyarlathotep.cfg and BepInEx/config/Nyarlathotep/; the owner runs exactly `.nyar template list`, `.nyar template use undead-nightfall`, `.nyar pillar empowerment on`, `.nyar event enable undead-nightfall` and, after the log shows "event undead-nightfall started by gametime", `.nyar status`, which shows the event; no file is edited by hand between boot and the status reply
- [ ] D24 · **Soak report tool** tools/soak-report.ps1 `-Log <paths> -Templates <ids> -MinMinutes 240` reads the logs in order and prints "soak: <m> timing minutes, <s> starts, <e> ends, <c> cancelled by restart, <u> unpaired, <x> unhandled, tick avg max <a> ms, templates <k>/<n>", then "soak: pass" only when m ≥ MinMinutes (one "tick timing" line per minute), every start has an end or a restart cancel, x = 0, every timing line's average is under 5 ms, and each template id started at least once; its -SelfTest runs fixtures good, bad-unpaired, bad-tick, bad-unhandled, bad-short, bad-missing-template and empty → "soak selftest: 7/7" · cmd: pwsh tools/soak-report.ps1 -SelfTest → "soak selftest: 7/7" (fails when: bad-unpaired, bad-tick, bad-unhandled, bad-short or bad-missing-template prints "soak: pass", good does not, or the empty log prints anything but "soak: fail — no log")
- [ ] D25 · **Four-hour soak** with the four empowerment templates copied in by session-events.py mode `soak` and the two spawn templates by the owner's kick-off (S-13), all enabled, and the empowerment and spawns pillars on, the server runs at least four hours with Debug.TimingLog on · manual: Session 3's logs, archived before each restart, give `pwsh tools/soak-report.ps1 -Log <archived logs> -Templates <the six ids> -MinMinutes 240` → "soak: pass"; its summary line and each template's first start and end lines are copied into the feature doc
- [ ] D26 · **Restart mid-event in soak** a restart during an active library event cancels it and the next boot runs clean · manual: in Session 3, while legion-weekend-surge is active, the server is stopped and started again; the log shows "event legion-weekend-surge cancelled by restart", the boot carrier sweep line, and the event's next scheduled start and end; `.nyar status` after the boot shows no stale instance
- [ ] D27 · **Edit capacity** on an events.json of 199 definitions, template use, event new, copy, set and delete confirm each finish in under 200 ms in the test run (median of five, the parse and validation included); the 200th definition is written and the 201st refused with the D5 reply · test: Nyarlathotep.Tests AuthoringCapacityTests (fails when: an operation's median reaches 200 ms, the 200th definition is refused, or the 201st is written)
- [ ] D28 · **Release 0.5.0** csproj Version and thunderstore.toml versionNumber are 0.5.0; both changelogs and both READMEs describe the template library, the authoring commands and the pillar commands; both READMEs' Quick start is the five commands of D23; the annotated tag v0.5.0 is pushed and the GitHub pre-release carries the tcli zip; no tcli publish (the owner publishes) · cmd: pwsh tools/preflight.ps1 → PREFLIGHT OK and "release tags: <n>/<n>"; then pwsh tools/release-verify.ps1 -Tag v0.5.0 -Asset kdpen-Nyarlathotep-0.5.0.zip → "release verify: hashes equal" (fails when: a surface differs from 0.5.0, a README's Quick start is not the five commands, the tag is missing or unpushed, the release has no zip, or the hashes differ)
- [ ] D29 · **Rollback gate** 0.4.0 runs on 0.5.0's files: events.json holding template copies loads with the same valid and disabled counts, and the cfg has no new key · cmd: pwsh tools/rollback-gate.ps1 -From v0.4.0 -To v0.5.0 → "rollback gate: 3/3" (fails when: the revert of v0.4.0..v0.5.0 conflicts or its tree fails build, tests or preflight; 0.4.0 disables a definition 0.5.0 accepts or fails to start on 0.5.0's files; or the dev-snapshot selftest fails)
- [ ] D31 · **Control cases present** every control tested by the new test classes (TemplateLibraryTests, TemplateCommandTests, AuthoringTests, AuthoringCapacityTests, PillarSwitchTests, ReadinessTests and LibraryDependencyFailureTests) has a failing, an empty and a passing case, by name: each [Fact] or [Theory] method is named `<Control>_fails_when_<input>`, `<Control>_empty_<input>` or `<Control>_passes_<input>`; ControlCaseTests reflects over those seven classes, groups their methods by the `<Control>` prefix, and requires all three kinds for every prefix and at least one prefix per class; a [Theory] with no data rows already fails in xUnit · test: Nyarlathotep.Tests ControlCaseTests (fails when: a listed class is missing or has no test method, a method name follows none of the three forms, or a control prefix lacks its fails_when, empty or passes case)
- [ ] D30 · **Sessions, records, paths and data** every server session is "### Session <n> · <date>" under docs/features/EVENT_LIBRARY.md › Test results with a "- session <n> log check: 0 unhandled, <s> nyar lines, 0 orphan errors, <u> unity errors" line in docs/audits/event-library.md from -LogCheck before the next restart; the audit has a pre-audit, a post-audit and a "Codex verdict:" line per Build plan step; tools/data-inventory.json has entries for the pending-delete book, the soak log archive and the server logs row of Design › Data; tools/paths-manifest.txt gains the `temp:` glob %TEMP%\nyar-soak-* and the new tracked paths; every session is wrapped by tools/dev-snapshot.ps1 and its "snapshot restored" line is in the audit · cmd: pwsh tools/preflight.ps1 -AuditOf event-library → "audit steps: event-library 6/6 pre, 6/6 post, 6/6 Codex verdicts"; pwsh tools/preflight.ps1 -SessionsOf event-library → "session logs: event-library <n>/<n> checked"; pwsh tools/preflight.ps1 -Paths → "paths: <n> walked, all in manifest" (fails when: a step lacks an entry or verdict, a session lacks its log-check line, a Design › Data row (the server logs row included) has no inventory entry or one of its five fields is empty, a walked path matches no manifest glob, or a %TEMP%\nyar-soak-* folder is left over)

## Purpose & typical use
- **Who:** a server admin who wants events without learning the events.json schema. They'd say: "Give me a ready-made Legion surge I can switch on from chat", or "copy the bandit ambush and point it at my hill fort".
- **Job:** pick a built-in event, copy it into the server's events, arm it, and turn its pillar on, all from chat, with no file edit and no restart. Then adjust what they copied (when, where, who, how strong) with further chat commands.
- **Coexists with:** events.json hand-editing (every chat write is the same edit plus a reload, D12), foundation's controls (enable, disable, start, stop, purge), faction-empowerment and the SpawnWaves pillar (the templates use their actions), Raphael (config-changed tells it the definitions moved, D12), and the cfg file an operator may still edit by hand (D15).

## Use cases
### Typical
The admin types `.nyar template list` and sees six templates. `.nyar template use legion-weekend-surge` copies it into events.json disabled (D5). `.nyar pillar empowerment on` turns the pillar on and saves the cfg (D14). `.nyar event enable legion-weekend-surge` arms it, and `.nyar event list` shows "legion-weekend-surge ready empowerment schedule Sat 20:00" (D16). On Saturday at 20:00 the Legion surges for 30 minutes. Another admin wants a second surge on Sunday: `.nyar event copy legion-weekend-surge legion-sunday`, `.nyar event set legion-sunday trigger.days Sun`, `.nyar event enable legion-sunday` (D7, D9).
### Minimal stretch
- **Least use (8.1):** `.nyar template list` and nothing else. It writes nothing and changes nothing (D4). A skeleton from `.nyar event new` is the least definition that validates (D6).
- **Empty:** a pillar filter with no templates replies "no templates for pillar zones" (D4). An events.json with no definitions lists nothing, and `template use` writes the first one (D5).
- **Once and never again (8.2):** a template is copied once and then deleted with `.nyar event delete` and its confirm (D8). The .bak holds the previous file, and the cooldown row goes with the definition. The catalogue itself cannot change, so nothing of the library stays behind but the admin's own copies.
### Maximal stretch
- **Volume (9.1):** events.json at its 200-definition limit. Every write parses and validates the whole file, which D27 measures at 199 definitions; the 201st is turned away with a reply (D5, D27).
- **Abuse (9.2):**
  - An admin sets a trigger field with quotes, braces, spaces or a 400-character name: the character-set check replies before any write (D9, D10).
  - An admin sets location here while standing far outside the map: the point is bounded to ±10000 (D11).
  - A non-admin tries each command and gets VCF's refusal, with no change to either file (D22).
  - An admin tries to delete an event that is running and is told to stop it first (D8).
- **Repeated use (9.3):**
  - `template use` twice with one id: the second is turned away with the `as` hint (D5).
  - `pillar spawns on` twice: the second replies "already on" and saves nothing (D14).
  - `delete confirm` twice: the second replies "no delete pending" (D8).
  - `set` with the value already there: written again, one config-changed, harmless (D12).

## Business rules
1. **The catalogue is read-only (Epic child constraint):** templates live in the DLL. They reach events.json only through `.nyar template use`, which writes a disabled copy (D1, D5). A later pillar child adds its templates to Resources/templates.json in its own plan.
2. **The six starter templates (D2),** every name checked against Reference Data/unit_index.tsv (S-4), all `"enabled": false`:

| id | pillar | trigger | duration | action |
|---|---|---|---|---|
| legion-weekend-surge | empowerment | Schedule Sat 20:00 | 1800 s | Empower Faction_Legion: physicalPower 1.5, maxHealth 1.5 |
| bandit-vengeance | empowerment | VBloodKilled: the eight bandit V Bloods (CHAR_Bandit_Bomber_VBlood, Chaosarrow, Fisherman, Foreman, Frostarrow, Stalker, StoneBreaker, Tourok, each CHAR_Bandit_<name>_VBlood); cooldownMinutes 30 | 600 s | Empower Faction_Bandits: physicalPower 1.3, attackSpeed 1.3 |
| undead-nightfall | empowerment | GameTime night | 1200 s | Empower Faction_Undead: physicalPower 1.25, spellPower 1.25 |
| militia-crackdown | empowerment | VBloodKilled: the fifteen Militia and Church V Bloods of S-5; cooldownMinutes 30 | 900 s | Empower Faction_Militia and Faction_ChurchOfLum: maxHealth 1.3 |
| bandit-ambush | spawns | Manual | 600 s | SpawnWaves CHAR_Bandit_Thug × 4 and CHAR_Bandit_Hunter × 2 (S-6), 3 waves, intervalSeconds 60, radius 10, location Admin |
| undead-rising | spawns | Manual | 600 s | SpawnWaves CHAR_Undead_SkeletonSoldier_Armored_Farbane × 5 and CHAR_Undead_ArmoredSkeletonCrossbow_Farbane × 2, 2 waves, intervalSeconds 90, radius 12, location Admin |

   Each template has a start and an end announce line using {faction} or {event}, and no minPlayers condition (S-10).
3. **Every in-game write is a file edit plus reload (Epic child constraint, D12):** template use, event new, copy, delete confirm, set and location here all go through DefinitionEditor, the validator and Persistence. A value the validator does not accept is written and then disabled with its reason, as the existing `event set` does; a value that fails the character-set or shape check in CommandArgs is never written (D9, D10).
4. **New definitions are disabled (Epic D4):** template use, event new and copy always write `"enabled": false` (D5, D6, D7). Arming is a separate `.nyar event enable`.
5. **Delete takes two steps (D8):** a confirm within 30 s by the same admin for the same id. A running event cannot be deleted. Deleting removes the definition and its cooldown row; events.json.bak keeps the file as it was (S-9).
5a. **Durations are hard (Epic Business rules 3, D12):** a template-made event ends at its durationSeconds exactly like a file-made one, because both are the same events.json definition by the time they start. Nothing a template use, copy, set or pillar switch does extends, pauses, stacks or restarts a running instance: a set of durationSeconds on a running event changes the next start only (D12), and a pillar switched off ends its events early, never later (D14).
6. **Pillar switches (D13, D14):** saved through their BepInEx ConfigEntry and the ConfigFile's own Save (Epic child constraint). They take effect at once. Turning a pillar off ends its running events (S-7).
7. **Readiness (4.4, D16):** a definition's readiness is the first of: invalid, off (pillar), off (event), ready. General.Enabled off is shown once at the top of the list rather than per line. The start precedence of Epic Business rules 1 is unchanged: purge > General.Enabled > the pillar > MaxConcurrentEvents > the definition's own state, and the readiness column uses the same order (D16). Exceptions: none at run time. The owner decides exceptions by changing the cfg within its ranges (General.Enabled, the pillar switches, MaxConcurrentEvents); no command grants one, and the new commands only set those same switches (D14, D17).
8. **Temporal (4.3):**
   - A set or copy on a running event's definition changes the next start only; the running instance keeps its definition (foundation D6).
   - A pending delete expires after 30 s (D8).
   - A pillar switch applies at the next scheduler tick and survives restarts (D14, D15).
   - A template copied with the same id as a deleted event starts with no cooldown (the row went with the delete, D8).
9. **Every-X sets (4.5, profile note):**
   - **Every in-game write of events.json** (the "every write goes through the validator" rule): template use (D5), event new (D6), copy (D7), delete confirm (D8), set of a trigger field (D9), of an action field (D10) and of the existing fields (foundation, faction-empowerment D12), location here (D11), and enable and disable (foundation). Each goes through DefinitionEditor; D12 tests the seven new kinds for equivalence, and D17 checks each runs through the gateway.
   - **Every write of the cfg:** `.nyar pillar <name> on|off` only (D14), fenced by the cfg-writes check (D18). BepInEx itself writes the cfg at boot when a key is missing.
   - **Every template:** the six of D2, loaded at boot (D3), listed (D4), copied (D5) and started in the soak (D25).
   - **Every new command form:** in §6 of the design doc (D20), adminOnly (D17), tried by a non-admin (D22) and seen in the real chat (D21).
   - **Every pillar name:** the five of D13, read from Settings.cs itself.
   - **Every session:** -SessionsOf (D30). **Every path:** -Paths, run last in step 6 (D30).

## Interfaces
### Internal — reads / writes / changes (paths or symbols)
- **Reads:**
  - the embedded Resources/templates.json;
  - EventStore.Catalog.Current and events.json through IFileStore;
  - EventRuntime.Engine.Active (running events, for delete and pillar off);
  - the Settings [Pillars] entries and General.Enabled;
  - the admin character's Translation (location here);
  - PrefabUnitCatalog and the faction catalog (template validation).
- **Writes and changes:**
  - New Logic/Templates.cs: TemplateCatalog, TemplateLines.
  - New Logic/Authoring.cs: the new, copy, delete and field planners over JsonNode, LocationArg.
  - New Logic/Pillars.cs: the name map, IPillarStore, the pillar command planner.
  - Logic/EventAdmin.cs: EventsEditor gains the trigger and action fields; EventLines gains readiness.
  - Logic/CommandArgs.cs: the new settable fields and their shape checks.
  - Logic/DefinitionEditor.cs: Append, Copy, Delete beside Edit.
  - Logic/ActionGateway.cs: CreateEvent, DeleteEvent, SetPillar.
  - Logic/Idempotency.cs: DeleteArming.
  - New Services/TemplateLibrary.cs, Services/PillarSwitches.cs; Services/EventStore.cs (Create, Delete); Services/EventRuntime.cs (end a pillar's events).
  - New Commands/TemplateCommands.cs, Commands/PillarCommands.cs; Commands/EventCommands.cs (new, copy, delete, location here); Commands/RootCommands.cs (the command list).
  - New Resources/templates.json; the plugin csproj embeds it and the test csproj links it and Config/Settings.cs as content.
  - tools/preflight.ps1 and tools/preflight-checks.json (the cfg-writes check, fixtures, the soak selftest); new tools/soak-report.ps1 and its fixtures; tools/ingame/session-events.py (modes el1 and soak).
  - docs/NYARLATHOTEP_DESIGN.md §6, new docs/features/EVENT_LIBRARY.md, new docs/audits/event-library.md.
- **What breaks if this is wrong:**
  - A chat write that skipped the stamp would overwrite an operator's unsaved-to-memory hand edit; D12 covers it.
  - A pillar save that rewrote the cfg badly would lose the operator's other settings; D15 diffs it.
  - A template the live catalogs disagree with would ship broken; D3 checks it in game.
- **Shared contract (5.3):**
  - Resources/templates.json uses the events.json v1 shape exactly, so a copy needs no conversion (D1).
  - The settable field names are part of the admin's chat interface and of §6 (D10, D20).
  - Raphael's contract is unchanged: api stays 3, and config-changed already covers every definition write (D12).
### External — dependencies and their failure behaviour

| Dependency | Version and cost | Inputs sampled | Slow, down or garbage |
|---|---|---|---|
| V Rising server (ProjectM) | VampireReferenceAssemblies 1.1.12-r99041-b2; free | unit_index.tsv for every template name (S-4, S-5); the character's Translation for location here; the day and night phase for undead-nightfall (profile note 6.1: Sessions 1 and 3 confirm it fires) | a name the live catalog lacks disables that template at boot with its reason (D3); a phase read that throws faults only its GameTime definition (D19) |
| BepInEx ConfigFile | BepInEx 6.0.0-be.733; free | its own save after a ConfigEntry.Value change, other keys and comments kept (S-11, checked by D15) | a throwing save, even after truncating the file, is followed by ConfigFile.Reload; memory and the reply follow what the file holds (D19) |
| VampireCommandFramework (VCF, the chat command library) 0.10.* | pinned with the siblings; free | for example, `.nyar event set x trigger.days Sat,Sun` reaches the method as four string arguments; a quoted argument keeps its spaces | an unparseable argument gets VCF's usage reply; our own shape checks come after (D9, D10); a context with no character or position makes `location here` reply with its reason (D19) |
| xUnit (the unit-test runner) through Nyarlathotep.Tests | as foundation; free | for example, the new AuthoringTests run over FakeFileStore | a failing test fails the step's compile check |
| git, gh, GitHub | git 2.53.0, gh 2.92.0, GitHub free tier; a handful of API calls per release, no cost | tags, release assets | every release tool runs under $ErrorActionPreference='Stop', so a failed call aborts the step and a rerun finishes it; their own selftests, re-run by D18, plant the failures: release-verify.ps1's missing-asset and differing-hash cases, and repo-rollback-drill.ps1's missing-tag and conflicting-revert cases, none of which may print its success line (D28, D29) |
| Codex CLI | codex-cli 0.151.0 on the owner's plan; a few reviews per step, no per-call cost | the review prompt via stdin | a 15-minute timeout; no "VERDICT: READY" means no "Codex verdict:" line in the audit, and `pwsh tools/preflight.ps1 -AuditOf event-library` then fails on that step (D30; fixture AuditSteps/bad, a step without a verdict) |

6.3 (test mode): the dev server (save-data-nyardev) is the only place this child runs a server; the soak's retimed schedules are written by tools/ingame/session-events.py into the dev server's events.json, never into Resources/ (D18's pillar-defaults check).

## Design
### Data
No new data file and no schema change.
- events.json gains definitions written by chat: the same v1 shape (D12), the same .bak rule (one generation).
- The cfg's five [Pillars] lines can now be written by `.nyar pillar` (D14).
- state.json loses a cooldown row when its definition is deleted (D8).
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
| Soak and preflight fixtures | tools/soak-report-fixtures/**, tools/preflight-fixtures/CfgWrites/**, tools/preflight-fixtures/TemplatesJson/bad-3/** | the repo | forever in git | one |
| Session snapshots, release download, rollback worktrees, drill config | as faction-empowerment › Design › Data | its tools | as there | one |

tools/data-inventory.json gets the pending-delete and soak-archive entries (D30).
### States
- **Template:** shipped (in the DLL) → copied (a disabled definition in events.json) → the definition's own life (foundation). Nothing flows back to the catalogue.
- **Definition:** absent → created disabled (use, new, copy) → enabled → running → … → delete armed → deleted. Delete is armed only when not running (D8).
- **Pillar:** off ⇄ on, saved (D14).
- **7.1:**
  - Empty: no templates would mean an unavailable catalogue, which every template command reports (D19).
  - First run: events.json holds foundation's five disabled examples and no templates; `.nyar template list` is the way in (D4, D23).
  - Loading: before Core.IsReady, commands reply "still loading" (foundation).
  - Partial: a copied template whose pillar is off shows `off (pillar)` (D16).
  - Error: an invalid copy shows `invalid: <reason>` (D16).
- **7.2 Concurrency:**
  - Commands and the scheduler tick run on the server main thread (S-3), so two admins' writes are serial.
  - Two admins using the same template id: the second reply says the id exists (D5).
  - A due start and `.nyar pillar <name> off` in the same cycle: commands and ticks are serial, so a start dispatched first runs and the off then ends it through the S-7 path, while an off processed first means the start is not made and `event list` shows `off (pillar)` (D14).
  - An operator's hand edit between a load and a chat write: the stale-stamp refusal (D12).
  - Two admins arming deletes of one id: each arming is keyed by its admin (D8). If A arms, B deletes the event with B's own delete and confirm, and A then confirms, A gets "unknown event <id>"; a confirm with no arming of the same admin, or after 30 s, gets "no delete pending for <id>" (D8).
  - The actors are admins, the operator, System and me as builder; my sessions run only on the dev server.
- **7.3 Stale data, cancel, re-entry:**
  - A pending delete goes stale after 30 s or if the event starts meanwhile (D8).
  - Cancel: not confirming is the cancel. Undo of a delete is the .bak (S-9) or a new `template use`.
  - Re-entry after a restart: a running library event is cancelled by restart as any event (D26), and pending deletes are gone.
### Permissions
| Path | Who | Check |
|---|---|---|
| `.nyar template list`, `.nyar template info` | admin | adminOnly (D17) |
| `.nyar template use`, `.nyar event new`, `.nyar event copy` | admin | adminOnly; Gateway CreateEvent (D17) |
| `.nyar event delete [confirm]` | admin | adminOnly; Gateway DeleteEvent (D17, D8) |
| `.nyar event set` (new fields, location here) | admin | adminOnly; Gateway SetEventField (D17) |
| `.nyar pillar list` | admin | adminOnly (D17) |
| `.nyar pillar <name> on`, `off` | admin | adminOnly; Gateway SetPillar (D17) |
| events.json and cfg hand edits | Operator | file load through the validator; the stamp check of D12 |

Actor matrix (2.1):
- A **player** reaches none of the new paths (D22).
- An **admin** runs every path above.
- **System** creates, deletes and switches nothing; it only starts enabled definitions, as before.
- The **Operator** edits the files by hand, as before; the stamp keeps a chat write from overwriting a newer file (D12).
- **Raphael** acts as its player and gains no path (api stays 3).
- **Other mods** and an **unauthenticated** client reach nothing.

2.2: the unauthorized path is VCF's standard refusal; the mod's method never runs (D22).
2.3 ownership: definitions belong to the server, so any admin may copy, set or delete any of them. A pending delete belongs to the admin who armed it, and another admin's confirm does not count (D8). The catalogue belongs to the DLL and nobody edits it in game (D1).
### UX
- **Discovery (11.1):** `.nyar` lists the template and pillar commands (RootCommands); both READMEs' Quick start is the five milestone commands (D28); §6 documents every form (D20); each create reply names the next command to run (D5, D6).
- **Feedback (11.2, profile note):**
  - Every reply is one line, naming the id and what changed (D5-D14).
  - A set that the validator does not accept says "now disabled: <reason>" (D12).
  - The readiness column says why an event would not start (D16).
  - Every new line is seen in the real chat in Session 2 before release; chat eats "< >", so no reply uses angle brackets (D21).
- **Accessibility (11.3):** plain chat lines of at most 480 bytes, no meaning carried by colour; the chat window is the game's.
- **Activation (11.4):** a library event activates only when it is copied, enabled and its pillar is on, and then only on its trigger (D16, D23). Should-not-activate cases: a copy fresh from `template use` (disabled, D5); a pillar switched off mid-event (its events end, D14); a template in the catalogue that was never copied (the catalogue never runs, D1). The soak confirms each template's activation over four hours (D25).

## Security
- **10.1 Authorization on every path:** every new command is adminOnly and runs through the gateway under an Admin-only ActionKind (D17). No indirect path is added: System gains no create, delete or switch.
- **10.2 Injection:**
  - Every value passes a character-set and shape check before it reaches JsonNode (D9, D10), and JsonNode writes it as a JSON string or number, never as raw text.
  - Names are matched against the game's catalogs by the validator, never used in a query.
  - No input reaches a shell, a URL or a file name; ids pass the id rule (D5).
- **10.3 Secrets:** none are added; the preflight secrets check covers the new files (D18).
- **10.4 Personal data:** none new. location here writes a map point the admin chose; the file does not record who stood there (D11). The admin-command log line is foundation's. Non-admin attempts change nothing (D22).

## Failure & observability
- **12.1:**
  - Every refused write gives a one-line reason and leaves the file as it was (D5-D12).
  - A write or save failure names the file and leaves memory unchanged (D19).
  - A broken catalogue disables only the template commands (D19).
- **12.2:** log lines:
  - "templates: <v>/<n> valid" at boot, "template <id> invalid: <reason>" per invalid one (D3);
  - "template catalogue unavailable: <reason>" once (D19);
  - foundation's "admin ran" line for each new command, and "reloaded: <v> valid, <x> disabled" after each write (D12);
  - "pillar <name> on|off (saved to cfg)" and "event <id> ended (pillar off)" (D14).
- **12.3:** the operator polls: `.nyar event list` shows readiness (D16), `.nyar pillar list` the switches (D14), and the log after each restart is read with -LogCheck (D30). The soak report is the long-run signal (D25).
- **12.4:** every check has a failing input, a silent input and a non-passing empty input:
  - Cfg writes: bad, bad-2, good, empty (D18).
  - TemplatesJson and PillarDefaults: bad-3 ships a template enabled (D18).
  - Soak report: five bad fixtures, a good one and an empty log (D24).
  - The release-time selftests and the audit, session, path and inventory checks: named fixture by fixture in the matrix below (D18, D28-D30).
  - Test seams (profile note): every control sits in Logic/ first (the catalogue loader, the planners for new, copy, delete and fields, the location rounding, the pillar map and command planner, readiness, delete arming), so a unit test reaches it; the services keep only the file, cfg and ECS calls, covered by Sessions 1-3.

Selftest matrix (12.4):

| Check | Fails on | Silent on | Empty input |
|---|---|---|---|
| Test-CheckCfgWrites | bad: EventSpawnsEnabled.Value set in Commands/; bad-2: Config.Save in another Services file | good: copies of the real files | empty fixture, a failure |
| Test-CheckTemplatesJson / PillarDefaults | bad-3: templates.json with one template enabled | good: the real Resources/*.json | existing empty fixture |
| soak-report -SelfTest | bad-unpaired, bad-tick, bad-unhandled, bad-short, bad-missing-template | good | an empty log, "soak: fail — no log" |
| Unit tests (D1-D14, D16, D19, D20, D27), with ControlCaseTests (D31) over TemplateLibraryTests, TemplateCommandTests, AuthoringTests, AuthoringCapacityTests, PillarSwitchTests, ReadinessTests and LibraryDependencyFailureTests | each control's `_fails_when_` cases | its `_passes_` cases | its `_empty_` cases; a control missing any of the three fails ControlCaseTests, and a filter running 0 tests fails |
| preflight -AuthSuite (D17) | a new command without adminOnly, a planted [Mutating] call outside Gateway.Run | all parts pass | "auth suite: no tests ran", a failure |
| release-verify -SelfTest (D28, re-run) | a differing hash, a missing asset, an audit without its "zip sha256:" line | a matching hash | no audit line → a failure; the full pass prints "release verify selftest: 4/4" |
| repo-rollback-drill -SelfTest (D29, re-run) | a conflicting revert, a missing tag, in a scratch git repository | a clean range | a missing tag makes `git worktree add` fail under Stop, so "rollback: clean" is never printed |
| rollback-drill -SelfTest (D29, re-run) | bad-3: N-1 disabled a definition for a reason other than "unknown action type" | good-2 | an empty log → "fail — no log" |
| dev-snapshot -SelfTest (D29, D30, re-run) | a leftover manifest not refused, a corrupted copy restored as equal, an absent path restored as present | a faithful round trip | no scratch install → "snapshot selftest: 0/6", a failure |
| rollback-gate -SelfTest (D29, re-run) | a stub part failing, or exiting 0 without its success line | all three stub parts passing | fewer than two release tags → "rollback gate: 0/3, failed: needs two releases" |
| -AuditOf (D30) | AuditSteps/bad (a step without a Codex verdict), bad-2 to bad-4 | good | empty: a plan with no Build plan steps → "audit steps: … has no Build plan steps" |
| -SessionsOf (D30) | SessionLogs/bad to bad-16 (for example, a session without its log-check line) | good | empty: no sessions → "session logs: <slug> has no sessions under …", a failure |
| -Paths (D30) | Paths/bad (an unlisted path), bad-temp, bad-rel, bad-worktree, bad-remote, and new bad-soak (a leftover nyar-soak-* folder) | good | empty → "paths: tools/paths-manifest.txt missing or empty" |
| data inventory (D30) | DataInventory/bad to bad-3 (an entry missing a field), bad-temp (a `temp:` glob with no entry) | good | empty → "data inventory: no entries" |

Gating-control matrix (one evidence command per gating probe):

| Probe | Evidence | Fails on | Silent on | Empty input |
|---|---|---|---|---|
| 2.1 actors | D17 `pwsh tools/preflight.ps1 -AuthSuite` | a new ActionKind granted beyond Admin, or a mutation outside the gateway | the declared table | 0 tests → failure |
| 3.3 persistence | D30 `pwsh tools/preflight.ps1` data inventory line | a Design › Data row without a complete entry | a complete inventory | a missing data-inventory.json → failure |
| 4.4 precedence | D16 `dotnet test Nyarlathotep/Nyarlathotep.Tests --filter "FullyQualifiedName~ReadinessTests or FullyQualifiedName~ControlPrecedenceTests"` | a lower cause winning over a higher one, in the column or in the start refusal, or the two disagreeing | each single cause | 0 cases → failure |
| 6.2 dependencies | in game: D19 `dotnet test Nyarlathotep/Nyarlathotep.Tests --filter FullyQualifiedName~DependencyFailureTests`; release time: D18 `pwsh tools/preflight.ps1 -SelfTest`, which re-runs release-verify's and repo-rollback-drill's failure cases | a failure changing memory or escaping its command; a release-tool failure case printing its success line | healthy fakes; the good cases | 0 cases → failure; an empty registry → failure |
| 10.1 authorization | D17 (as 2.1) | as 2.1 | as 2.1 | as 2.1 |
| 10.3 secrets | D18 `pwsh tools/preflight.ps1 -SelfTest` | a planted token shape | ordinary text | no files → failure |
| 12.4 failing cases | D31 and D18 in one line: `dotnet test Nyarlathotep/Nyarlathotep.Tests --filter "FullyQualifiedName~ControlCaseTests or FullyQualifiedName~TemplateLibraryTests or FullyQualifiedName~TemplateCommandTests or FullyQualifiedName~AuthoringTests or FullyQualifiedName~AuthoringCapacityTests or FullyQualifiedName~PillarSwitchTests or FullyQualifiedName~ReadinessTests or FullyQualifiedName~LibraryDependencyFailureTests"; pwsh tools/preflight.ps1 -SelfTest` (the second runs every script check's fixtures and D24's soak selftest) | a control without its fails_when, empty or passes case; a bad or empty fixture passing | complete case sets; good fixtures | a class with no test methods, or a filter running 0 tests → failure; an empty registry → failure |
| 14.3 rollback | D29 `pwsh tools/rollback-gate.ps1 -From v0.4.0 -To v0.5.0` | a conflicting revert; 0.4.0 failing on 0.5.0's files; a mutated snapshot restore passing | a clean round trip | fewer than two release tags → "rollback gate: 0/3" |
| 14.4 paths | D30 `pwsh tools/preflight.ps1 -Paths` | a walked path matching no glob, or a leftover %TEMP%\nyar-soak-* folder | manifested paths | an empty manifest → failure |

## Performance
- **Budget (13.1):** the scheduler tick keeps Epic D24's under-5 ms average through the soak, library events included (D25). A chat write parses and validates the whole events.json once, off the tick; D27 bounds it under 200 ms at 199 definitions.
- **Bounds (13.2):**

| Bound | Source case | At the bound | Valid case excluded |
|---|---|---|---|
| 200 definitions (foundation MaxDefinitions) | foundation's validator | the 201st write is turned away (D27, D5) | a server with more than 200 events |
| 1 MB events.json (foundation MaxFileBytes) | foundation's validator | the reply is "events.json would exceed 1 MB; nothing written" and the file stays byte-identical, its SHA-256 equal before and after (D12) | very long announce pools across 200 events |
| 1-20 bosses per VBloodKilled trigger | foundation's validator | militia-crackdown uses 15 (D2) | a trigger on every V Blood by name (use "any") |
| 30 s delete window | the purge confirm pattern | the confirm expires (D8) | a confirm typed after a long pause |
| 10 lines per page | foundation paging | the next page is asked for (D4) | a one-reply list of many templates |

## Build plan
Every step runs inside the Epic's `## Rollout` › Procedure: a pre-audit and a post-audit recorded in docs/audits/event-library.md (template docs/audits/README.md), `/code-review`, and a Codex read-only cross-inspection of the step's diff (`codex exec -s read-only`, prompt via stdin) until "VERDICT: READY", with its line written as "Codex verdict:" in the audit. Anything the plan did not foresee is a dod amendment before it is built. The build starts only when faction-empowerment's `dod status` shows it closed and the tag v0.4.0 exists (S-2).
1. **Records and pure logic.**
   - Create docs/audits/event-library.md (rollback base v0.4.0) and docs/features/EVENT_LIBRARY.md with Status, Test plan and an empty Test results, and map event-library to that doc in tools/preflight-checks.json childDocs.
   - New Resources/templates.json with the six templates of Business rules 2; add `<EmbeddedResource Include="Resources\templates.json" />` to Nyarlathotep.csproj, and link templates.json and Config/Settings.cs as `None` content in Nyarlathotep.Tests.csproj.
   - New Logic/Templates.cs (TemplateCatalog.Load, TemplateLines), Logic/Authoring.cs (Append, Copy, Delete and field planners, LocationArg), Logic/Pillars.cs (the map, IPillarStore, the command planner).
   - Extend Logic/EventAdmin.cs (the trigger and action fields, readiness in EventLines), Logic/CommandArgs.cs (settable fields and shape checks), Logic/DefinitionEditor.cs (Append, Copy, Delete), Logic/ActionGateway.cs (CreateEvent, DeleteEvent, SetPillar granted to Admin), Logic/Idempotency.cs (DeleteArming).
   - Tests: TemplateLibraryTests, TemplateCommandTests, AuthoringTests, AuthoringCapacityTests, PillarSwitchTests, ReadinessTests, LibraryDependencyFailureTests (in DependencyFailureTests.Library.cs), ControlCaseTests, and additions to CommandArgTests, ConfigChangedTests and AuthorizationTests; FakeStores gains a fake IPillarStore.
   - Run `dotnet test Nyarlathotep/Nyarlathotep.Tests`.
   - Satisfies D1, D2, D4, D5, D6, D7, D8, D9, D10, D11, D12, D13, D14, D16, D19, D27, D31.
2. **Services, commands, checks and docs.**
   - New Services/TemplateLibrary.cs (boot load, "templates: <v>/<n> valid") and Services/PillarSwitches.cs (ConfigEntry.Value set, BepInEx saves, ConfigFile.Reload after a throw), both [Mutating] behind Gateway.Run.
   - Services/EventStore.cs: Create and Delete over DefinitionEditor; Services/EventRuntime.cs: end a pillar's running events on pillar off.
   - New Commands/TemplateCommands.cs (`[Command("template", adminOnly: true)]`) and Commands/PillarCommands.cs (`[Command("pillar", adminOnly: true)]`); Commands/EventCommands.cs gains new, copy, delete, delete confirm and location here; Commands/RootCommands.cs lists them.
   - tools/preflight.ps1: Test-CheckCfgWrites; fixtures CfgWrites/{bad,bad-2,good,empty} and TemplatesJson/bad-3 registered in tools/preflight-checks.json.
   - docs/NYARLATHOTEP_DESIGN.md §6: every new form (D20).
   - Run the compile check, the tests, `pwsh tools/preflight.ps1 -SelfTest`, `pwsh tools/preflight.ps1 -AuthSuite` and `pwsh tools/preflight.ps1`.
   - Satisfies D17, D18, D20, and the game halves of D5-D14.
3. **Unattended session (Session 1) and the soak tool.**
   - Write tools/soak-report.ps1 with its -SelfTest and fixtures under tools/soak-report-fixtures/ (D24), register it in `externalSelfTests`, add the `temp:` glob nyar-soak-* to tools/paths-manifest.txt with the fixture Paths/bad-soak, and add the soak-archive and server-logs entries to tools/data-inventory.json.
   - Stop the server, run `pwsh tools/preflight.ps1 -LogCheck` on the last logs, `pwsh tools/dev-snapshot.ps1 -Save s1`, and deploy with `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release`.
   - Add mode `el1` to tools/ingame/session-events.py: it writes the dev server's events.json with copies of the six templates, legion-weekend-surge retimed to today and 2 minutes after boot, undead-nightfall as shipped, both enabled; it turns Pillars.FactionEmpowerment and Pillars.EventSpawns on and Debug.TimingLog on in the cfg.
   - Boot the dev world (`$env:SteamAppId='1604030'`; VRisingServer.exe -persistentDataPath .\save-data-nyardev -serverName "Nyar Dev" -saveName nyardev -logFile .\logs\NyarDev.log).
   - Check the log for "templates: 6/6 valid" (D3), legion-weekend-surge's start and end, and undead-nightfall's start at the next night (S-12). Stop, run -LogCheck, and run `pwsh tools/soak-report.ps1` on the log with -MinMinutes 30 as a dry run of the tool on real lines.
   - `pwsh tools/dev-snapshot.ps1 -Restore`. Record Session 1.
   - Satisfies D3, D24, D30.
4. **In-game session with the owner (Session 2).**
   - Write the exact numbered steps (server 127.0.0.1:9876) into docs/features/EVENT_LIBRARY.md › Test results › Session 2: part A the milestone of D23 on a fresh install; part B the authoring walk of D21; part C the non-admin walk of D22; part D the pillar persistence of D15 with a restart.
   - Run `pwsh tools/dev-snapshot.ps1 -Save s2`, hash events.json and the cfg for D22, delete BepInEx/config/kdpen.Nyarlathotep.cfg and BepInEx/config/Nyarlathotep/ for the fresh install, deploy, boot, and hand the steps over.
   - Record each reply verbatim, stop, run -LogCheck, record it, and run `pwsh tools/dev-snapshot.ps1 -Restore`.
   - A reply that chat mangles gets a `discovered` amendment and a fix before step 6.
   - Satisfies D15, D21, D22, D23.
5. **Four-hour soak (Session 3).**
   - Add mode `soak` to tools/ingame/session-events.py: copies of the four empowerment templates, all enabled; legion-weekend-surge's copy retimed to every day at twelve times spread over the soak window; the empowerment and spawns pillars and Debug.TimingLog on.
   - Run `pwsh tools/dev-snapshot.ps1 -Save s3`, deploy, and boot.
   - Owner kick-off (S-13): the owner kills one bandit V Blood and one Militia or Church V Blood on the lists of Business rules 2, and for bandit-ambush and undead-rising runs `.nyar template use <id>`, `.nyar event set <id> location here` and `.nyar event start <id>`; then the owner may leave. (The `soak` mode therefore writes only the four empowerment templates; the two spawn templates come in by chat.) Without the kick-off the soak does not start.
   - Unattended: at about two hours, while legion-weekend-surge is active, copy both logs to %TEMP%\nyar-soak-<n>, run -LogCheck, restart the server (D26), and let it run until the timing lines total at least 240.
   - Stop, copy the logs again, run -LogCheck, then `pwsh tools/soak-report.ps1 -Log <archived logs in order> -Templates legion-weekend-surge,bandit-vengeance,undead-nightfall,militia-crackdown,bandit-ambush,undead-rising -MinMinutes 240`; copy its lines into the feature doc, delete the %TEMP%\nyar-soak-* folders, and run `pwsh tools/dev-snapshot.ps1 -Restore`.
   - Satisfies D25, D26, D30.
6. **Release.**
   - Move the six surfaces to 0.5.0 in one `chore(release): v0.5.0` commit, both READMEs' Quick start being the five commands of D23; tcli build; record the zip's SHA-256 in the audit.
   - Create the local annotated tag v0.5.0 and, with the server stopped, run `pwsh tools/rollback-gate.ps1 -From v0.4.0 -To v0.5.0` (D29) before anything is pushed. A failure means deleting the local tag, fixing, and re-tagging.
   - Search the release diff for the Steam-ID prefix and the owner's mail name that the release checklist of faction-empowerment names; then push the commit and tag, create the GitHub pre-release with the zip, and run `pwsh tools/release-verify.ps1 -Tag v0.5.0 -Asset kdpen-Nyarlathotep-0.5.0.zip` (D28).
   - Record the Epic D47 pass line, run `dod close event-library`, regenerate the dod index and commit.
   - Last, after every write of this child: run `pwsh tools/preflight.ps1 -Paths` and `pwsh tools/preflight.ps1`; an undeclared path is added to the manifest and the run repeated until clean.
   - Satisfies D28, D29, D30.

## Work breakdown
- W1 · **Catalogue**
- W1.1 · **Templates and lines** · items: D1 D2 D3 D4 · steps: 1, 2, 3
- W2 · **Authoring**
- W2.1 · **Create, copy, delete** · items: D5 D6 D7 D8 D12 D27 · steps: 1, 2
- W2.2 · **Set fields and location** · items: D9 D10 D11 · steps: 1, 2
- W3 · **Pillars and readiness**
- W3.1 · **Switches and readiness** · items: D13 D14 D15 D16 · steps: 1, 2, 4
- W4 · **Controls**
- W4.1 · **Auth, checks, failures, docs** · items: D17 D18 D19 D20 D31 · steps: 1, 2
- W5 · **Verification and release**
- W5.1 · **Owner session** · items: D21 D22 D23 · steps: 4
- W5.2 · **Soak** · items: D24 D25 D26 · steps: 3, 5
- W5.3 · **Release and records** · items: D28 D29 D30 · steps: 6

## Rollout
### Shipping
One release, 0.5.0, a GitHub pre-release at close; the owner publishes to Thunderstore. Everything still ships off: every template is disabled and every pillar defaults to off (Epic D4, D18). Who turns it off: any admin, with `.nyar pillar <name> off`, `.nyar event disable`, `.nyar event delete` or `.nyar purge confirm`; the operator, by editing the files or removing the DLL.
### Compatibility
- events.json stays SchemaVersion 1; no key is added. Existing definitions are untouched until an admin changes them.
- The cfg gains no key; `.nyar pillar` writes the existing [Pillars] lines.
- The event list line changes shape (the readiness column). It is a human reply, not part of Raphael's contract; api stays 3.
- The existing `event set` fields keep their names and ranges.
### Rollback
- **In the repository:** `git revert --no-edit v0.4.0..v0.5.0`, drilled by the rollback gate (D29).
- **On the dev server during the build:** every session is wrapped by tools/dev-snapshot.ps1 (D30).
- **On a server:** install the 0.4.0 DLL. events.json holding chat-written definitions loads unchanged (the same v1 shape, D29), and the cfg's pillar lines are read as before. The template and authoring commands are gone; hand edits work as before. This remains possible after data is written, because 0.5.0 writes nothing 0.4.0 cannot read.
- **Published release:** as faction-empowerment's Rollout › Rollback: tags and releases are never deleted; a bad 0.5.0 is withdrawn by retitling its release, and versions move forward only (Epic S-19).
- **Commit range:** v0.4.0..v0.5.0.
### Paths walked
Walking the Build plan:
- **Step 1:**
  - docs/audits/event-library.md, docs/features/EVENT_LIBRARY.md.
  - Nyarlathotep/Nyarlathotep/Resources/templates.json, Nyarlathotep/Nyarlathotep/Nyarlathotep.csproj, Nyarlathotep/Nyarlathotep.Tests/Nyarlathotep.Tests.csproj.
  - Nyarlathotep/Nyarlathotep/Logic/{Templates,Authoring,Pillars,EventAdmin,CommandArgs,DefinitionEditor,ActionGateway,Idempotency}.cs.
  - Nyarlathotep/Nyarlathotep.Tests/{TemplateLibraryTests,TemplateCommandTests,AuthoringTests,AuthoringCapacityTests,PillarSwitchTests,ReadinessTests,DependencyFailureTests.Library,ControlCaseTests,CommandArgTests,ConfigChangedTests,AuthorizationTests,FakeStores}.cs.
- **Step 2:**
  - Services/{TemplateLibrary,PillarSwitches,EventStore,EventRuntime}.cs, Commands/{TemplateCommands,PillarCommands,EventCommands,RootCommands}.cs.
  - tools/preflight.ps1, tools/preflight-checks.json, tools/preflight-fixtures/CfgWrites/**, tools/preflight-fixtures/TemplatesJson/bad-3/**.
  - docs/NYARLATHOTEP_DESIGN.md.
- **Step 3:** tools/soak-report.ps1, tools/soak-report-fixtures/**, tools/preflight-checks.json, tools/paths-manifest.txt, tools/data-inventory.json, tools/ingame/session-events.py.
- **Steps 3–5, on the server:** the plugins DLL, BepInEx/config/kdpen.Nyarlathotep.cfg, BepInEx/config/Nyarlathotep/{events,state}.json (+.bak/.tmp), save-data-nyardev/**, logs/NyarDev.log and BepInEx/LogOutput.log; outside the repository %TEMP%\nyar-snap-* (the snapshot) and %TEMP%\nyar-soak-* (the soak archive), each a `temp:` line of the manifest and a Design › Data row.
- **Step 6:**
  - The six release surfaces: Nyarlathotep/Nyarlathotep/Nyarlathotep.csproj, Nyarlathotep/Nyarlathotep/thunderstore.toml, CHANGELOG.md, Nyarlathotep/Nyarlathotep/CHANGELOG.md, README.md, Nyarlathotep/Nyarlathotep/README.md.
  - Remote writes: the tag v0.5.0 and the GitHub release v0.5.0, declared as `remote-tag:` and `remote-release:` lines of tools/paths-manifest.txt.
  - Nyarlathotep/Nyarlathotep/dist/** and build/*.zip (ignored); %TEMP%\nyar-rel-*, %TEMP%\nyar-rollback-* and %TEMP%\nyar-drill-* from the release tools.
  - docs/dod/event-library.md, docs/dod/nyarlathotep.md, docs/dod/README.md.
- **Build outputs of every step** (`dotnet build`, `dotnet test`, tcli build): Nyarlathotep/**/bin/**, Nyarlathotep/**/obj/**, *.binlog, Nyarlathotep/Nyarlathotep/dist/** and Nyarlathotep/Nyarlathotep/build/**, covered by the existing ignored globs of tools/paths-manifest.txt (`ignored: **/bin/**`, `ignored: **/obj/**`, `ignored: Nyarlathotep/Nyarlathotep/dist/**`, `ignored: Nyarlathotep/Nyarlathotep/build/**`, `ignored: **/*.binlog`); the server logs are its `external: BepInEx/LogOutput*.log` and `server: logs/NyarDev.log` lines; the one glob missing, `temp: nyar-soak-*`, is added in step 3 (D30).
- **Review process:** docs/dod/event-library.reviews.md and docs/dod/event-library.review.html.

## Out of scope
- **15.1, excluded here:**
  - Editing announce lines and conditions from chat beyond the fields listed (the existing MessageCommands and file edits cover them).
  - Admin-authored templates saved back into the catalogue (the catalogue is read-only by the Epic constraint).
  - Templates for boss reinforcements, defended zones and sieges (each pillar child adds its own).
  - A Raphael panel for templates or pillars (Raphael workspace; api stays 3).
  - Undo beyond the one .bak generation.
  - Thunderstore publication (the owner's).
- **15.2, deferred:**
  - boss, zones and sieges templates: the boss-reinforcements, defended-zones and sieges children;
  - Raphael rows for templates and pillars: a later api child;
  - chat editing of announce pools and conditions: a later authoring plan if admins ask.

## Also considered
- **Compliance and legal:** none; no personal data.
- **Localisation:** template names and replies are English, like every reply of the mod; names are admin-editable after copying.
- **Running cost:** none beyond tick time, measured by the soak (D25).
- **Operational ownership:** the server admin; the README's Quick start and command table.
- **Documentation and changelog:** the six surfaces, the feature doc and §6 (D20, D28).
- **Analytics:** the boot line counts valid templates; nothing else is counted.
- **Decommissioning:** foundation's five examples stay in events.default.json; the templates do not replace them in this release.
- **Support tooling:** the readiness column (D16) and `.nyar pillar list` (D14).

## Assumptions
- S-1 · validated · the scope of this plan: a compiled read-only catalogue with template list, info and use; event new, copy, delete (confirmed, not while running) and the widened set with location here; pillar list and on/off saved to the cfg; the readiness column; six starter templates shipped disabled; the D47 milestone; a four-hour soak; release 0.5.0 · source: owner decisions in plan mode 2026-09-26 (Epic A21 and its child constraint for event-library)
- S-2 · validated · the build starts only after faction-empowerment releases 0.4.0 · source: owner decision 2026-09-26, Epic Children order
- S-3 · validated · commands, the scheduler tick and Harmony postfixes all run on the server main thread, so authoring writes are serial · source: foundation Design › States, Services/EventScheduler.cs
- S-4 · validated · every unit and faction name in the templates exists: Faction_Legion (68 units), Faction_Bandits (40), Faction_Undead (62), Faction_Militia (54), Faction_ChurchOfLum (27); CHAR_Bandit_Thug (level 16), CHAR_Bandit_Hunter (16), CHAR_Undead_SkeletonSoldier_Armored_Farbane (20), CHAR_Undead_ArmoredSkeletonCrossbow_Farbane (18); the eight bandit V Bloods of Business rules 2 (CHAR_Bandit_Leader_VBlood_UNUSED left out) · source: Reference Data/unit_index.tsv
- S-5 · reversible · militia-crackdown's fifteen bosses are the Militia V Bloods (CHAR_ChurchOfLight_Sommelier_VBlood, CHAR_Militia_BishopOfDunley_VBlood, CHAR_Militia_Glassblower_VBlood, CHAR_Militia_Guard_VBlood, CHAR_Militia_Hound_VBlood, CHAR_Militia_HoundMaster_VBlood, CHAR_Militia_Leader_VBlood, CHAR_Militia_Longbowman_LightArrow_Vblood, CHAR_Militia_Nun_VBlood, CHAR_Militia_Scribe_VBlood), the Church V Bloods (CHAR_ChurchOfLight_Overseer_VBlood, CHAR_Militia_Fabian_VBlood, CHAR_Villager_Tailor_VBlood) and the two of the Church sub-faction (CHAR_ChurchOfLight_Cardinal_VBlood, CHAR_ChurchOfLight_Paladin_VBlood); the sub-faction Faction_ChurchOfLum_SpotShapeshiftVampire is not empowered · fallback: a boss the owner thinks wrong is removed from templates.json by a one-line edit before release, and the sub-faction can be added as a third faction the same way
- S-6 · reversible · CHAR_Bandit_Hunter is the bandit archer the scope asks for · fallback: if Session 3 shows it fighting in melee, it becomes CHAR_Bandit_Deadeye (level 26, unit_index.tsv) by a one-line edit before release
- S-7 · reversible · `.nyar pillar <name> off` ends that pillar's running events at once, like stop · fallback: leave them running to their end and only stop new starts; an amendment of D14 comes first, then Logic/Pillars.cs's planner and PillarSwitchTests, Services/PillarSwitches (no call to EventRuntime's end path), the reply text, §6 of the design doc, the feature doc and both READMEs change
- S-8 · reversible · the scope's "counts" are the `:<count>` suffixes of action.units, so one field sets units and counts together · fallback: add the indexed fields action.units.<n>.prefab and action.units.<n>.count beside action.units; an amendment of D10 comes first, then CommandArgs (rows and shape checks), EventsEditor's paths, AuthoringTests and CommandArgTests, §6 and ContractDocTests (D20), both READMEs' command tables and the feature doc change
- S-9 · reversible · the undo of a delete is the one .bak generation plus a fresh `template use`; nothing else keeps deleted definitions · fallback: keep a dated .deleted copy per delete, a Persistence path and a Design › Data row, if the owner wants longer retention
- S-10 · reversible · the templates carry no minPlayers condition, and the two V Blood templates carry cooldownMinutes 30 so a group killing several bosses does not chain them · fallback: a one-line edit per template before release
- S-11 · reversible · setting a [Pillars] ConfigEntry.Value makes BepInEx save the cfg itself (ConfigFile.SaveOnConfigSet, on by default), and that save keeps every other key's value and the comments BepInEx writes; after a save that throws, ConfigFile.Reload re-reads the file so memory matches it (D19) · fallback: if D15's diff shows more than one changed line, the release waits for an owner decision recorded as an amendment of D15; the cfg stays written only by BepInEx, never by a direct file write
- S-12 · reversible · the in-game clock advances with no player connected, so undead-nightfall can fire in unattended Sessions 1 and 3 · fallback: the owner leaves a client connected and idle during the unattended windows
- S-13 · reversible · the soak begins with an owner kick-off of a few minutes: one kill of a bandit V Blood and one of a Militia or Church V Blood from the lists of Business rules 2, and, for the two spawn templates, `.nyar template use`, `.nyar event set <id> location here` and `.nyar event start <id>`; it then runs unattended for four hours · fallback: if the owner cannot do the kick-off, the soak waits until they can; no template ships without its soak start and end (D25), and step 6 does not begin
- S-14 · reversible · readiness is shown in the order invalid, off (pillar), off (event), ready, and General.Enabled once at the top · fallback: a reorder in Logic and ReadinessTests

## Coverage
| # | Layer | Status | Probes | Pointer / reason |
|---|---|---|---|---|
| 1 | Purpose & typical use | Considered | 3/3 | Purpose & typical use |
| 2 | Actors & permissions | Considered | 3/3 | Design › Permissions › 2.1 D17 D22; 2.2 D22 D17; 2.3 D8 D12 |
| 3 | Inputs, outputs & data | Considered | 4/4 | Design › Data › 3.1 D4 D5 D9 D10 D11 D14; 3.2 D12 D14 D16; 3.3 D30 D12 D15; 3.4 D2 D29 |
| 4 | Business rules & invariants | Considered | 5/5 | Business rules › 4.1 D2 D8 D12 D16; 4.2 D1 D12 D5; 4.3 D8 D14; 4.4 D16 D14; 4.5 D1 D13 D17 D30 D20 |
| 5 | Internal interfaces | Considered | 3/3 | Interfaces › 5.1 D3 D12; 5.2 D12 D14; 5.3 D1 D10 D20 |
| 6 | External dependencies & contracts | Considered | 3/3 | Interfaces › 6.1 D3 D15 D25; 6.2 D19 D18; 6.3 D24 D30 |
| 7 | States & lifecycle | Considered | 3/3 | Design › States › 7.1 D3 D4 D16 D23; 7.2 D8 D12 D14; 7.3 D8 D12 D26 |
| 8 | Minimal stretch | Considered | 2/2 | Use cases › Minimal stretch › 8.1 D6 D4; 8.2 D5 D8 |
| 9 | Maximal stretch | Considered | 3/3 | Use cases › Maximal stretch › 9.1 D27 D5; 9.2 D9 D10 D11 D22; 9.3 D5 D8 D14 |
| 10 | Security & privacy | Considered | 4/4 | Security › 10.1 D17; 10.2 D9 D10 D12; 10.3 D18; 10.4 D11 D22 |
| 11 | Design & UX | Considered | 4/4 | Design › UX › 11.1 D4 D28 D20; 11.2 D21 D16; 11.3 prose: plain chat lines under 480 bytes with no meaning carried by colour; the chat window is the game's; 11.4 D23 D25 D16 |
| 12 | Failure handling & observability | Considered | 4/4 | Failure & observability › 12.1 D19 D21; 12.2 D3 D14 D25; 12.3 D16 D25 D30; 12.4 D31 D18 D24 |
| 13 | Performance & scale | Considered | 2/2 | Performance › 13.1 D27 D25; 13.2 D5 D27 |
| 14 | Rollout & compatibility | Considered | 4/4 | Rollout › 14.1 D28 D18; 14.2 D2 D29 D16; 14.3 D29; 14.4 D30 |
| 15 | Out of scope | Considered | 2/2 | Out of scope |
Gate — acceptance & testability: passed — every Considered layer 2–14 maps to ≥ 1 D-item

## Baseline

## Amendments

## Log
- 2026-09-26 · status → draft · plan
- 2026-09-26 · note · review: codex Review 1 REVISE (F1-F11, 7 blocking, 4 advisory); all accepted and applied in this revision
- 2026-09-26 · note · review: codex Review 2 REVISE (F1-F7, 4 blocking, 3 advisory); F1-F4 and F6 applied (+D31), F5 deferred to the owner's S-13 decision, F7 a review-tooling defect (no file access)
