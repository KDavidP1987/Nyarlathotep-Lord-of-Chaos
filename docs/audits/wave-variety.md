# Audit — wave-variety

## Pre-audit
### Step 1 · 2026-10-16 · 6a1e846
- git status: clean at 6a1e846 apart from the plan's start (status in-progress, docs/dod/wave-variety.build.md), committed with this entry
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s); tests 3586/3586
- preflight: PREFLIGHT OK, "secrets: none (2102 files scanned, 2047 index blobs)"
- dod status: 0/24 verified (just started)
- feature doc read: docs/features/WAVE_VARIETY.md created in this step (Status: in progress); docs/features/EVENT_LIBRARY.md (faction wave templates, D60) and docs/features/APOCALYPSE.md (RoamRule) read
- baseline boot: not an in-game step; the dev server runs the v0.13.0-rc.2 DLL (boot "v0.13.0-rc.2 loaded", 0 [Error)

### Step 2 · 2026-10-16 · a896846
- git status: clean at a896846 but for step 2's own tools/vrclient/scenarios/faction-mix-wide.vrs; step 1 is built in parallel by a builder subagent in Logic/ and tests only (the mix runs use only `.nyar spawn` and NyarDevTools, which the running DLL already has), so its uncommitted files are not part of this step
- compile: 0 Warning(s), 0 Error(s); tests 3586/3586 (step 1's pre-audit, same commit)
- preflight: PREFLIGHT OK, "secrets: none (2102 files scanned, 2047 index blobs)"
- dod status: 0/24 verified
- feature doc read: docs/features/WAVE_VARIETY.md (Status: in progress; open question: which units of each faction fight beside each other)
- baseline boot: the v0.13.0-rc.2 DLL; the session before this one read clean (6 baseline warnings: HarmonyX x3, Il2CppInterop, Beelzebub TUNE, the dev example-empowerment), logs copied to %TEMP%\nyar-wv2-logs; snapshot wvmix saved (34 files); [Debug] VerboseLogging set true for the session

### Step 3 · 2026-10-16 · e4141ab
- git status: clean at e4141ab (step 2 committed)
- compile: 0 Warning(s), 0 Error(s); tests 3784/3784
- preflight: PREFLIGHT OK, "secrets: none (2115 files scanned, 2060 index blobs)"; -SessionsOf wave-variety 2/2 checked
- dod status: 0/24 verified by status yet (step 1 and 2 controls pass; status runs at the end of the step)
- feature doc read: docs/features/WAVE_VARIETY.md (Status: steps 1-2 built; Session 2 recorded the groups)
- baseline boot: the dev server runs the rc.2 DLL from Session 2 (log check clean); step 3's sessions run on this step's Release build inside a fresh snapshot

### Step 4 · 2026-10-16 · f20d98e
- git status: clean at f20d98e but for this step's own rollback-drill fixtures and tools/rollback-drill.ps1 (built while Session 5 ran)
- compile: 0 Warning(s), 0 Error(s); tests 3825/3825 (step 3's post-audit, same tree)
- preflight: PREFLIGHT OK, "secrets: none (2130 files scanned, 2061 index blobs)"
- dod status: 17/24 verified (D13, D17, D18, D19 wait on the owner; D21, D22, D23 are this step's)
- feature doc read: docs/features/WAVE_VARIETY.md (Status: steps 1-3 built; Open questions: none)
- baseline boot: the dev server ran the step 3 build in Session 5, log check clean; this step's one boot pair (the step 3 build, then v0.12.0, on the same events.json) ran inside snapshot wv3, restored "hashes equal"
- session 6 log check: 0 unhandled, 220 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons (%TEMP%\nyar-wv3-logs\s6-srv; the fixed build after Codex's findings, pursuit.vrs 27/27); every [Warning read: the baseline kinds and the run's "stopped" line; no [Error

## Post-audit
### Step 1 · 2026-10-16 · step 1 commit (builder subagent, on a896846)
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s)
- tests: Passed! 3780/3780 (3586 before, 194 new)
- preflight: PREFLIGHT OK, "secrets: none (2115 files scanned, 2050 index blobs)"
- built: Logic/Pools.cs (the roll, the "rolled:" line), Logic/Pursuit.cs (the rule and PursuitPhase), Model/Validation (pools, pool entries, wave and action boss rolls, pursuit), Arena, UnitWatch, HuntingGrounds and Engine (a pursuing run), FactionGroups.Problem over an action's pool members, CommandArgs/DefinitionEditor/EventAdmin/Messages (chat fields and info lines), design §6 rows, the contract sentence; Services/WaveAction already takes the roll (Pools.Resolve replaces BossWave.Units/Lead), SpawnTracker.RegionWorld implements IPursuitWorld and FocusKey
- amendment: A1 (discovered, ~D4, layer 5.1): no authoring warning exists in `.nyar event set`; the clause is dropped
- /code-review: 10 findings, all fixed: (1) Pools.Resolve had no production caller, pool entries would queue prefab "" (WaveAction wired, BossWave.Units/Lead removed); (2) IRegionWorld.FocusKey defaulted to null (default removed, SpawnTracker implements it); (3) between waves an AroundPlayer/Region run had no centres (last centres kept); (4) chat units lists skipped the 50-unit wave size; (5) players read once per run (now once per tick); (6) the api twin carried the shortened "<n> units" text; (7) a malformed boss pool token answered "no pool"; (8) the centre rule duplicated (Arena.LocationCentre shared); (9) as 1; (10) a dead FactionGroups overload removed
- Codex verdict: deferred to step 4 (the child's single cross-inspection, design §9 D34)
- planted faults: 17 lines in docs/dod/wave-variety.md › Log, each failed with its fault and passed after the copy back
- left for step 3: PursuitPhase.Tick in the scheduler, EventEnded/Clear on every end path, the health entry, the dependency suite, HuntAction's map-wide seeds for a pursuing run
- in-game: none in this step (Logic); pursuit and rolls in game are step 3's sessions (D17, D18)
### Step 2 · 2026-10-16 · step 2 commit
- in-game: Session 1 (faction-mix-wide.vrs 284/337: six groups clean, the undead and militia passes lost to the 300 s manual-spawn lifetime) and Session 2 (their rerun at 1800 s, 118/119: only CHAR_Undead_ZealousCultist_Ghost, which dies on its own), recorded in docs/features/WAVE_VARIETY.md › Test results and docs/GAME_ASSETS.md › Faction compatibility
- session 1 log check: 0 unhandled, 1287 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons (`-LogCheck -ServerPath` over the saved copy in %TEMP%\nyar-wv2-logs); every [Warning read: the six baseline lines and one slow tick (551 ms) at the first CHAR_Militia_Hound_VBlood spawn; no [Error
- session 2 log check: 0 unhandled, 377 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons (live logs); every [Warning read: the six baseline lines only; no [Error
- built: Logic/FactionGroups.cs grows every group to the recorded allied units and adds blackfangs (Faction_Blackfangs); FactionGroupsTests Recorded reads docs/GAME_ASSETS.md (copied into the test output); the apocalypse theory case for CHAR_Legion_Shadowkin_Lesser now expects "mixes the undead and legion groups" (it joined the legion group)
- compile: 0 Warning(s), 0 Error(s); tests Passed! 3784/3784; planted: a ShadowSoldier added to the undead group fails all four Recorded tests, passes after the copy back
- /code-review: in step 3's review of the child's diff (this step adds data and one test class)
- Codex verdict: deferred to step 4 (the child's single cross-inspection, design §9 D34)
### Step 3 · 2026-10-16 · step 3 commit
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s)
- tests: Passed! 3825/3825 (3784 before)
- preflight: PREFLIGHT OK, "secrets: none (2130 files scanned, 2061 index blobs)"; `-DependencySuite wave-variety` "dependency suite: wave-variety 1/1 (pursuit-read)"
- built: pursuit in the region phase (PursuitPhase ticks with the hunt; a run turned pursuing is hunted the same tick; HuntAction seeds a pursuing run map-wide through Pursuit.SeedTargets), the "pursuit" health entry and its dependency suite, the verbose check line ("pursuit <id>: <n> players read, <k> eligible (…), <m> within <r> m of <c> centres, quiet <s> s"), the quiet clock that stops while a player is in the area and starts at the first check that sees nobody; the 14 templates in Resources/templates.json (seven timed, seven onslaughts) with TemplateLibraryTests.WaveVariety.cs; scenarios pursuit.vrs, wave-variety.vrs, pursuit-timing.vrs
- /code-review: 7 findings. Fixed: RegionPhase.Needed counted waiting runs; a run newly turned pursuing waited a hunt interval before its first hunt (now `due || started`); four scenario faults (the no-log window, the rolled line's "… and <n> more" cut, the home check not scoped to the event, the timing count). Left as advice: the eligibility rule (claimed territory, PvP combat) is written three times in HuntAction (targets, the verbose tally, the seeds); one helper is a refactor for after 0.13.0, recorded here and in docs/SESSION_HANDOFF.md
- planted faults: the D7, D8, D15, D16 and D17 lines in docs/dod/wave-variety.md › Log, each failed with its fault and passed after the copy back
- in-game: Sessions 3, 4 and 5 in docs/features/WAVE_VARIETY.md › Test results. D17 pursuit.vrs PASS 27/27 (pursuit-20261016-145616.json, "on" 20 s after the first empty check); D18 wave-variety.vrs PASS 26/26 (wave-variety-20261016-140858.json); D20 -TimingSpan "timing span: 10/10 windows under 5 ms, tracked >= 20, targets >= 1, 0 slow ticks" over Session 5's log copy (the scenario's own end count failed, 16 of 24 left after natives killed 8; the count is now recorded, not judged)
- session 3 log check: 0 unhandled, 583 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons (%TEMP%\nyar-wv3-logs\s3-srv)
- session 4 log check: 0 unhandled, 1494 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons (%TEMP%\nyar-wv3-logs\s4-srv)
- session 5 log check: 0 unhandled, 1061 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons (%TEMP%\nyar-wv3-logs\s5-srv); every [Warning read in all three: the baseline kinds and the runs' "stopped" lines; no [Error; NyarDev.log holds only the game's "Prefab … is in an unknown state" warnings
- snapshot: wv3 restored after Session 5, "snapshot restored; hashes equal"; VerboseLogging back to false
- owner: D19 (the feel of pursuit and the rolls) waits on the owner (rubric 3, never self-accepted)
- Codex verdict: deferred to step 4 (the child's single cross-inspection, design §9 D34)
- docs: %TEMP% paths that an earlier shell edit had mangled were repaired in docs/features/WAVE_VARIETY.md, docs/audits/wave-variety.md, docs/SESSION_HANDOFF.md, docs/dod/event-spawns.reviews.md, docs/dod/unit-watch.md (Log only), docs/features/APOCALYPSE.md and docs/features/UNIT_WATCH.md
