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
- baseline boot: the v0.13.0-rc.2 DLL; the session before this one read clean (6 baseline warnings: HarmonyX x3, Il2CppInterop, Beelzebub TUNE, the dev example-empowerment), logs copied to %TEMP%
yar-wv2-logs; snapshot wvmix saved (34 files); [Debug] VerboseLogging set true for the session

## Post-audit
### Step 1 · 2026-10-16 · step 1 commit (builder subagent, on a896846)
- compile: `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__` 0 Warning(s), 0 Error(s)
- tests: Passed! 3780/3780 (3586 before, 194 new)
- preflight: PREFLIGHT OK, "secrets: none (2115 files scanned, 2050 index blobs)"
- built: Logic/Pools.cs (the roll, the "rolled:" line), Logic/Pursuit.cs (the rule and PursuitPhase), Model/Validation (pools, pool entries, wave and action boss rolls, pursuit), Arena, UnitWatch, HuntingGrounds and Engine (a pursuing run), FactionGroups.Problem over an action's pool members, CommandArgs/DefinitionEditor/EventAdmin/Messages (chat fields and info lines), design §6 rows, the contract sentence; Services/WaveAction already takes the roll (Pools.Resolve replaces BossWave.Units/Lead), SpawnTracker.RegionWorld implements IPursuitWorld and FocusKey
- amendment: A1 (discovered, ~D4, layer 5.1): no authoring warning exists in `.nyar event set`; the clause is dropped
- /code-review: 10 findings, all fixed: (1) Pools.Resolve had no production caller, pool entries would queue prefab "" (WaveAction wired, BossWave.Units/Lead removed); (2) IRegionWorld.FocusKey defaulted to null (default removed, SpawnTracker implements it); (3) between waves an AroundPlayer/Region run had no centres (last centres kept); (4) chat units lists skipped the 50-unit wave size; (5) players read once per run (now once per tick); (6) the api twin carried the shortened "<n> units" text; (7) a malformed boss pool token answered "no pool"; (8) the centre rule duplicated (Arena.LocationCentre shared); (9) as 1; (10) a dead FactionGroups overload removed
- Codex: once per child before the release (Halloween track, design §9 D34), in step 4
- planted faults: 17 lines in docs/dod/wave-variety.md › Log, each failed with its fault and passed after the copy back
- left for step 3: PursuitPhase.Tick in the scheduler, EventEnded/Clear on every end path, the health entry, the dependency suite, HuntAction's map-wide seeds for a pursuing run
- in-game: none in this step (Logic); pursuit and rolls in game are step 3's sessions (D17, D18)
### Step 2 · 2026-10-16 · step 2 commit
- in-game: Session 1 (faction-mix-wide.vrs 284/337: six groups clean, the undead and militia passes lost to the 300 s manual-spawn lifetime) and Session 2 (their rerun at 1800 s, 118/119: only CHAR_Undead_ZealousCultist_Ghost, which dies on its own), recorded in docs/features/WAVE_VARIETY.md › Test results and docs/GAME_ASSETS.md › Faction compatibility
- session 1 log check: 0 unhandled, 1287 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons (`-LogCheck -ServerPath` over the saved copy in %TEMP%
yar-wv2-logs); every [Warning read: the six baseline lines and one slow tick (551 ms) at the first CHAR_Militia_Hound_VBlood spawn; no [Error
- session 2 log check: 0 unhandled, 377 nyar lines, 0 orphan errors, 0 unity errors, regions 10 polygons (live logs); every [Warning read: the six baseline lines only; no [Error
- built: Logic/FactionGroups.cs grows every group to the recorded allied units and adds blackfangs (Faction_Blackfangs); FactionGroupsTests Recorded reads docs/GAME_ASSETS.md (copied into the test output); the apocalypse theory case for CHAR_Legion_Shadowkin_Lesser now expects "mixes the undead and legion groups" (it joined the legion group)
- compile: 0 Warning(s), 0 Error(s); tests Passed! 3784/3784; planted: a ShadowSoldier added to the undead group fails all four Recorded tests, passes after the copy back
- /code-review: in step 3's review of the child's diff (this step adds data and one test class)

