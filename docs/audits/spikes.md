# Audit — spikes

Build plan steps 1–9 of docs/dod/spikes.md. Each step has one "### Step <n>" entry under "## Pre-audit"
and one under "## Post-audit"; every post-audit entry carries a "Codex verdict:" line (D17).

## Pre-audit
### Step 1 · 2026-09-23 · 3ba32a0
- git status: clean
- compile: 0 errors, 0 warnings
- preflight: exit 0
- dod status: spikes 0/20 verified (just started); Epic 11/36
- feature doc read: none (tooling step); docs/dod/spikes.md Build plan step 1 and D4, D13, D14, D17
- server: not running; not touched by this step

### Step 2 · 2026-09-23 · 50e196a
- git status: clean
- compile: 0 errors, 0 warnings
- preflight: exit 0 ("spike code: none", selftest 19/19)
- dod status: spikes 1/20 verified; Epic 11/46 (review pending on v2.1, Codex round 2 running; it does not gate this step)
- feature doc read: docs/dod/spikes.md Build plan step 2, Business rules 1–5, Design › States and UX, D1–D6, D12, D16, D19; DEV_REMINDERS #4, #8, #9, #13, #14, #16–#19, #22, #23
- step-2 parent (D19): 50e196a. Compile items captured to docs/audits/spikes-compile-base.txt (6 items, none under obj/) before any harness file was added
- scope note: step 3's D5 and D6 sessions already run `march` and `empower`, so this step builds all six subcommands, including Spikes/SpikeMarch.cs and Spikes/SpikeCarrier.cs. Steps 4 and 6 are the sessions and any variant tuning
- marker recipe: AB_Consumable_PhysicalPowerPotion_T01_Buff (-1954355403), chosen because it has Buff and LifeTime (DEV_REMINDERS #17) and differs from the default carrier (T02, -1591883586). Its stat buffer is cleared, its gameplay-event components are stripped (#18), LifeTime is set to {0, None} so it lives with the unit, and SpellLevel.Level is set to 1314472274
- risk: DEV_REMINDERS #13 warns that some buffs applied on the spawn frame crashed the server. The plan applies the marker on the spawn frame, and the throwaway save absorbs a crash. If one happens, the marker moves to the next frame under a `discovered` amendment
- known limit: Test-CheckStructuralEdits matches `.DestroyEntity(`, not `DestroyUtility.Destroy(`. The harness calls DestroyUtility only inside EntityExtensions.DestroySafe, and the post-audit code review confirms this
- server: not running; not touched by this step

### Step 3 · 2026-09-23 · bd6bc0d
- git status: clean except this step's A4 change (tools/preflight.ps1 -LocalServerPath, ServerWrites bad-8, spikes D4 and S-2), committed with this entry
- compile: 0 errors, 0 warnings
- preflight: exit 0; `-SelfTest` → "selftest: 19/19 checks, 3 fixtures each, 38 extra bad fixtures" (bad-8 fails with "owner data touched"); `-Paths` → "paths: 347 walked, all in manifest"
- dod status: spikes 5/20 verified (D1 D2 D3 D14 D16); A3 and A4 recorded
- feature doc read: docs/dod/spikes.md Build plan step 3, D4, D5, D6, D12; docs/features/EVENT_SPAWNS.md Status
- owner input: the owner's test world lives at C:\VRising-LocalServer (world1, start_server_local.bat, same server executable). The throwaway save stays at <server>\save-data-nyarspikes with default host settings; A4 extends the D4 snapshot to C:\VRising-LocalServer so any change to world1 fails
- baseline boot: BepInEx/LogOutput.log of the owner's last boot (2026-09-23 19:32) with the deployed v0.1.0 scaffold: "Nyarlathotep initialized via GameDataInitializedPatch (attempt #1)", loaded beside VCF 0.10.4, Beelzebub 0.136.0, Faust 0.16.5 and Uriel 0.20.0; 0 lines matching error or exception
- server: not running

### Step 4 · 2026-09-24 · (with step 3)
- recorded retrospectively: the S1 march sessions (1–3, 5–8) ran inside step 3's session series with the same server, build pipeline and checks. Each session began from the state recorded under Post-audit › Step 3: tree committed, compile 0/0, preflight OK, the server boot log checked for "Nyarlathotep initialized" and 0 exceptions

### Step 5 · 2026-09-24 · (with step 3)
- recorded retrospectively: the S2 restart sessions (9–12) ran inside step 3's series; A8 (stop after autosave) and A9 (tag keep) were recorded before the code they changed was built

### Step 6 · 2026-09-24 · (with step 3)
- recorded retrospectively: the S3 carrier session (4) and its mid-buff restart ran inside step 3's series on build 35dfbe9, whose compile and preflight are recorded under Post-audit › Step 2 and Step 3

### Step 7 · 2026-09-24 · aed615e
- git status: clean; compile 0/0; preflight OK; dod status spikes 10/20 before the contracts table
- read: docs/RESEARCH_NOTES.md sections, docs/dod/nyarlathotep.md › Amendments (A1–A13) and Child constraints, the three feature docs' Status lines

### Step 8 · 2026-09-24 · e51b808
- git status: clean (0 entries), HEAD e51b808
- compile: `dotnet build … -p:VRisingServerPath=C:\__nodeploy__` → 0 Warning(s), 0 Error(s)
- preflight: first run failed with "data inventory: 'config/config.vdf' has no entry" (the A12 manifest path) → inventory entry added; rerun "data inventory: 43/43 complete", PREFLIGHT OK, "spike code: present, allowed while spikes is in-progress"; `-SelfTest` 19/19
- dod status: spikes 12/20 verified, 0 problems
- server: not running; the owner's game client closed
- plan read: Build plan step 8 (D13, D15, D18, D19), Rollout › Rollback; step-2 parent for D19 is 50e196a

## Post-audit
### Step 1 · 2026-09-23 · (this commit)
- compile / preflight: 0 errors, 0 warnings (no C# change); `pwsh tools/preflight.ps1` exit 0 with "spike code: none"; `-SelfTest -Verbose` → "selftest: 19/19 checks, 3 fixtures each, 36 extra bad fixtures", every bad fixture failing for its planted reason; `-Paths` → "paths: 296 walked, all in manifest"
- real modes:
  - `-ServerWrites -Snapshot` (scratch file) recorded 1546 files and folders. An unchanged `-Compare` → "0 created, 0 changed, 0 deleted, all in manifest, no other save"
  - a planted nyar-probe.txt in the server root → "unmanifested: nyar-probe.txt"
  - an empty save-data-probe/Saves/ → "another Saves folder exists: save-data-probe/Saves"
  - both probes deleted; a re-compare came back clean
  - `-AuditOf spikes` → "1/9 pre, 0/9 post, 0/9 Codex verdicts" and a failing exit, as expected this early
- /code-review: inline review of the PowerShell diff. `-ServerWrites` without `-Compare` fails with a clear message. Snapshots can hold LocalLow paths that contain a SteamID: they stay in %TEMP% or the scratchpad, printed LocalLow paths are cut to two segments (Format-SafePath), and LocalLow/VRising/** is declared external. No further findings
- the Audits and FeatureResults bad fixtures briefly passed with 0/0, because their plant replaced `status: draft` in a plan that is now in-progress. The self-test caught it, and the plant now rewrites whatever status line is there
- Codex round 1: REVISE with 6 blocking findings.

  | Finding | Disposition |
  |---|---|
  | F1 (empty folders unseen) | accepted: directory rows; ServerWrites bad-5 and bad-6 |
  | F2 (locked files compare equal) | accepted: three hash retries, then fail; walk errors fail; any hashed row without a SHA-256 fails; bad-7 |
  | F3 (comment stripper cuts at "//" inside strings) | accepted: a literal-aware lexer, shared with the Epic checks, recorded as Epic amendment A2 (defect); SpikeCode bad-4, StructuralEdits bad-8 |
  | F4 (qualified attribute names) | accepted: bad-5 |
  | F5 (the plan defines its own step set) | accepted in part: steps must be numbered 1..N with no gap (AuditSteps bad-4). Removing steps is a plan edit that the dod amendments record |
  | F6 (no check for D19) | rejected: D19's evidence is its own command, run at Build step 8 |
- Codex round 2: REVISE with 1 blocking finding and 1 advisory:
  - F7 (a command name built from a constant or concatenation): accepted. A command name that is not one string literal counts as spike code; SpikeCode bad-6 and bad-7
  - F8 (advisory: a "Spike…" string literal fails): rejected, because failing is the safe direction after removal
- Codex round 3 (the cap): REVISE with 2 blocking findings, both applied:
  - F9: a using-alias for a command attribute counts as spike code (bad-8)
  - F10: \u escapes are decoded before the scan (bad-9)
- Codex verdict: REVISE at the 3-round cap. Every finding from the final round was applied and is proven by a failing fixture. There is no further Codex round; the owner reviews this record
- in-game: not applicable (tooling only; no DLL change)
- dod status: D14 verified (see Log); spikes 1/20

### Step 2 · 2026-09-23 · dec14e1
- compile / preflight (on dec14e1): `--no-incremental` build 0 errors, 0 warnings; `pwsh tools/preflight.ps1` → "PREFLIGHT OK" with "commands: 6 admin-only, 1 public (allow-listed)", "structural edits: fenced (3 Prefab-guarded calls in EntityExtensions.cs)", "secrets: none (331 files scanned)", "spike code: present, allowed while spikes is in-progress"; `-SelfTest` → "selftest: 19/19 checks, 3 fixtures each, 37 extra bad fixtures"; `-Paths` → "paths: 343 walked, all in manifest"
- known-limit check: `git grep DestroyUtility -- *.cs` finds only EntityExtensions.cs:69, inside DestroySafe
- /code-review (inline): refusal order matches Business rules 4; clear bypasses the cooldown and the Enabled gate; the drain is at most 5 destroys per batch, batches 0.25 s apart, so a second `clear` can land mid-drain (D5); `empower` has no per-command NPC cap beyond the 30 m radius, accepted for a spike because every carrier expires by LifeTime; no blocking findings
- Codex runs from the scratchpad with the diff, DEV_REMINDERS and the plan pasted in. A first attempt run from the repo could not read files (sandbox policy) and is not counted
- Codex round 1: REVISE.

  | Finding | Disposition |
  |---|---|
  | F1 (structural helpers lack ECS generic constraints; would not compile) | rejected: the IL2CPP interop EntityManager generics carry no such constraint; the D1 build is 0 errors, 0 warnings |
  | F2 (rich-text tags not stripped from replies) | accepted: Truncate strips `<...>` and stray angle brackets |
- Codex round 2: REVISE.

  | Finding | Disposition |
  |---|---|
  | F3 (a march anchor makes 11 units from one command) | accepted: march count 1–9 for variants 1 and 2; spikes amendment A3 (discovered) |
  | F4 (marker and carrier queries lack IncludeSpawnTag) | accepted: both use IncludeDisabled and IncludeSpawnTag |
  | F5 (raw exception message in the command failure log) | accepted: Fail sanitizes once and logs one line |
- Codex round 3 (the cap): REVISE with 1 finding, applied:
  - F6: the march mover and clear drain logged raw exception text. Both now use SpikeUnits.OneLine
- Codex verdict: REVISE at the 3-round cap. Every finding from the final round was applied and rebuilt clean. There is no further Codex round; the owner reviews this record
- in-game: not yet. The harness runs first in step 3, on the throwaway save
- dod status: D1, D2, D3, D16 verified on dec14e1 (see the plan Log)

### Step 3 · 2026-09-23 to 2026-09-24 · f4687a1
- snapshot: `-ServerWrites -Snapshot $env:TEMP
yarspikes-before.tsv` at 2026-09-23 19:46:28, server stopped, 1633 files and folders (85 under LocalServer/)
- setup: save-data-nyarspikes\Settingsdminlist.txt written (one line, the owner's SteamID; not quoted here); step-2 build deployed (Nyarlathotep.dll 45056 bytes); default host settings (port 9876)
- launch: the server started from this session has no console, so Ctrl-C cannot stop it cleanly and BepInEx's buffered disk log was lost on the first two stops. BepInEx/config/BepInEx.cfg `[Logging.Disk] InstantFlushing` set false → true for the spike sessions; restored to false in step 8
- boot (20:43): "Nyarlathotep initialized via GameDataInitializedPatch (attempt #1)" beside Beelzebub, Faust and Uriel; no error lines
- session 1 (owner in game, 20:45–21:00), D5 sequence:
  - `tag 10` ×3 → "10/10 spawned … 10 alive", "… 20 alive", "… 30 alive". The owner saw only about 20 at once: the engine log shows the owner's client sending dozens of ChangeHealthOfClosestToPositionDebugEvent admin events (the admin kill tool), so units were being killed as they came. Kills lower the alive count, so two more `tag 10` were admitted ("30 alive", "28 alive") before "spike limit 30 (22 alive)" ×3
  - `sweep` → "marked 14, listed 14, faults 0"; `clear` → batch 1 ran in the command frame ("destroyed 5, 1 left"), reply "clear: 6 queued, 5 per batch", batch 2 finished 0.25 s later; the second `clear` → "nothing to clear". The drain was too fast for the mid-drain checks
  - `tag 1` ×2, `clear` → "clear: 1 queued", done
  - `march 2` → the anchor (CHAR_Critter_Rat) and the first CHAR_Bandit_Thug spawned and were logged; the server then aborted before the second unit's spawn line: "System.ArgumentException: The entity does not exist … EntityComponentStore::AppendDestroyedEntityRecordError … thrown from a job compiled with Burst … burst will now abort the Application". No Nyarlathotep stack frame (Burst abort)
- findings and changes (fix build, not yet re-run):
  - the abort happened inside `march 2`, between setting the first unit's Follower.Followed to the anchor and spawning the second unit. The march now spawns the whole group first and then applies the lever, sets Follower.ModeModifiable to 0 as Bloodcraft does for a set Followed, and logs each step ("anchor … held still", "<n> spawned, applying variant", "variant <v> set on <id>") so a repeat pinpoints the failing step. Only the player character prefab carries FollowerBuffer, so native NPCs follow without one and no buffer is added
  - clear batches now run 1 s apart (still at most 5 destroys per frame), so a person can type a second `clear` and a `tag 1` mid-drain
  - session 2 re-runs D5 without the admin kill tool, then isolates S1: `march 3` (no Follower) first, then `march 1 1`, then `march 2`
- session 2 (2026-09-24 07:45, build b27e252, no admin kill tool):
  - D5: `tag 10` ×3 → 10, 20, 30 alive; `tag 10` ×2 → "spike limit 30 (30 alive)"; `sweep` → "marked 30, listed 30, faults 0"; `clear` → "clear: 30 queued, 5 per batch", batches 1–6 of 5, 1 s apart, "clear done"; `sweep` → "marked 0, listed 0, faults 0". A second run of 20 drained the same way. The owner could not paste `tag 1` inside the drain window and waived that one check; the refusal path is the same "clear in progress" branch that `tag` and `march` share
  - `march 3` ×3 (no Follower): all 5 units spawned 100 m north and stayed Idle (the BehaviourTreeState override to Return did not stick), then all 5 vanished at t=5 s ("alive 0"); `clear` → "nothing to clear". Cause: units spawned beyond every player's range are disabled, and the DestroyWhenDisabled we add removes them within seconds (DEV_REMINDERS #14 names the counter, CanPreventDisableWhenNoPlayersInRange.CanDisable = false; the step-2 march did not apply it). Finding for the Epic: a siege or distant wave spawned away from players is deleted before it arrives unless it is kept enabled
  - `march 1 1` ×2: the rat anchor spawned at the admin and held still; the unit went from 100 m to 3.9 m ("Follow") within 1 s, i.e. the follow behaviour teleports a far follower to its leader, then it stood by the still anchor. No crash with one follower set after the spawns
  - change (not yet run): march units and the anchor get CanPreventDisableWhenNoPlayersInRange.CanDisable = false (added to the rat, which lacks it); LifeTime still bounds them
- session 3 (2026-09-24 08:20–08:43, build 35dfbe9):
  - `march 3` at 100, 40 and 20 m: all units now survive (CanDisable = false works) but stay "Idle" for 60 s and more; the owner saw the 20 m group mill in place. Variant 3 does not move units
  - `march 2 1` ×2: the rat anchor stepped 10 m per second from 100 m out to the admin (seen in game as a rat appearing in the distance, then beside the owner); the follower stayed at the spawn point ("Follow", d=100.4), then jumped to about 4 m after 11 s and 51 s. Variant 2 does not walk units either
  - server abort #2 (08:43): same message as #1 ("The entity does not exist … AppendDestroyedEntityRecordError", Burst). Just before it, Beelzebub logged the owner devouring a CHAR_Bandit_Thug, and the owner reports picking up one of the spike rats (rats are pick-up critters) when the server went down. Both aborts involved follow-linked units and the rat anchor; the pick-up path destroying a rat that carries our marker, LifeTime and DestroyWhenDisabled, with a bandit following it, is the leading suspect. The rat anchor is retired; variant 4 uses no anchor and no Follower
  - research: none of the reference mods walks a unit to an empty point; the proven long walk is an aggro chase (TideOfWar widens AggroConsumer and AggroModifiers ranges to 350; Bloodcraft adds an AggroBuffer entry). Variant 4 (A5) does both, aimed at the admin
- variant 4 review (1d14f66): compile 0 warnings 0 errors; preflight OK; `dod-index --check spikes` 0 problems. Codex read-only, round 1: one finding, rejected. The finding was that the admin entity held in AggroBuffer could go stale on disconnect or death. It was rejected because the game's own aggro system writes the same entries for every target it notices, targets are destroyed routinely, a disconnected vampire keeps its character entity, and Bloodcraft (Utilities/Familiars.cs:826-859) ships the same pattern. The in-game test watches for it anyway.
  - Codex verdict: REVISE (finding rejected with reason; no code change)
- session 4 (2026-09-24, build 35dfbe9): S3 empower. Stats, expiry, stream-out and a hard stop right after an autosave all passed; S3 verdict go (docs/features/FACTION_EMPOWERMENT.md › Test results). No exception in the log
- session 5 (2026-09-24, build 4debb17 deployed at the S3 restart): `march 4 5 100` ×5 stayed Idle at about 100 m, alive, for up to 123 s (the owner saw no units, and a brief in-combat flag each time). `march 4 5 30` walked 5/5 units to the admin in 13 s. The limit refusal fired once ("spike limit 30 (28 alive)"); `clear` destroyed 28 in 6 batches. A6 adds the variant 4 probe
- session 6 (build 6e1a94b): the probe shows the ranges persist and the admin is pruned from AggroBuffer at about 86–94 m; 60 m arrives. A7 re-adds the entry
- session 7 (build 98132c0): re-adding does not hold beyond about 80 m; at 80 m, 6/10 units froze in Combat. No exception in any session since the rat anchor was retired
- session 8 (build 98132c0): wall run; the target is pruned whenever the wall breaks line of sight; S1 verdict go (D7). No exception
- session 8 crash check: fed on a marked `tag 1` thug, no crash and no exception; marker-only units are safe to feed on. Session 3 cause (owner): a picked-up rat becomes an inventory item, so the rat entity is destroyed under live references
- S2 support (A8): `tag` adds PersistenceV2.DontSaveEntity to even-numbered units; `sweep` appends the saved and DontSaveEntity groups with each unit's remaining LifeTime
- sessions 9–12: S2 go (docs/features/EVENT_SPAWNS.md › Test results). A9 (tag keep) and A10 (Age beside LifeTime) were found and fixed on the way. D12 log check after session 12: 0 unhandled
- session 13 (D6, build c4473bf), replies from BepInEx/LogOutput.log:
  - ranges: `tag 0`/`tag 11` "count must be 1-10"; `tag 1 29`/`tag 1 601` "lifetime must be 30-600"; `tag 1 30 2` "keep must be 0-1"; `march 0`/`march 5` "variant must be 1-4"; `march 4 0`/`march 4 11` "count must be 1-10"; `march 1 10` "count must be 1-9"; `march 4 5 19`/`march 4 5 201` "distance must be 20-200"; `empower 9`/`empower 601` "seconds must be 10-600"; `empower 30 0`/`empower 30 31` "radius must be 1-30"; `inspect 0`/`inspect 31` "radius must be 1-30"
  - carriers: `empower 30 10 12345` "carrier 12345: unknown prefab"; `empower 30 10 1227555070` "prefab Buff_InCombat_Npc_Elite lacks LifeTime"
  - precedence and limits: after `tag 10` ×3 ("30 alive"), `tag 0` "count must be 1-10" (the argument wins over the limit) and `tag 1` "spike limit 30 (30 alive)"
  - cooldown: `inspect` twice quickly, second "spike cooldown (234 ms)" (and again "267 ms")
  - clear has no cooldown: `clear` "clear: 30 queued, 5 per batch", then "clear in progress (25 left)", "(15 left)", "(10 left)"
  - General.Enabled = false, restart: `tag 0` "General.Enabled is false" (Enabled wins over the argument); `clear` "nothing to clear", `sweep` "marked 0, listed 0, faults 0" and `inspect` "no native NPC within 10 m" all ran; `march 4` "General.Enabled is false". Enabled restored to true afterwards
  - D12 log check: "log check: 0 unhandled, 5 spike lines"; no exception in any boot since session 3
- in-game coverage: steps 3–6 of the Build plan ran together in sessions 1–13 (S1 march, S2 restart, S3 carrier, D5 and D6). Verdicts: S1 go, S2 go, S3 go
- close-out checks (2026-09-24, f4687a1):
  - compile: `dotnet build Nyarlathotep/Nyarlathotep.sln -c Release --no-incremental -p:VRisingServerPath=C:\__nodeploy__` → 0 Warning(s), 0 Error(s)
  - preflight: PREFLIGHT OK ("secrets: none", "spike code: present, allowed while spikes is in-progress"); `-Paths` "348 walked, all in manifest"
  - D4 `-ServerWrites -Compare` (client closed): first run flagged config/config.vdf (Steam API connection cache, rewritten at every server launch) → manifest entry, A12; rerun "server writes: 19 created, 38 changed, 0 deleted, all in manifest, no other save"; C:\VRising-LocalServer untouched
  - /code-review (inline, on bd6bc0d..f4687a1): the variant 4 re-add is idempotent (one entry per target); every new structural edit goes through AddComponentSafe (Age, DontSaveEntity, CanPreventDisableWhenNoPlayersInRange); sweep's save groups only read; the tag `keep` argument is range-checked. No finding beyond Codex's
  - Codex read-only cross-inspection of bd6bc0d..HEAD (Nyarlathotep/), 3 rounds:
    - round 1: variants 1–2 still executable after both follow-link aborts → fixed, A11 (a91db69)
    - round 2: a destroyed admin entity left in AggroBuffer → the mover drops the entry and stops (f4687a1)
    - round 3: no remaining defect
  - Codex verdict: READY
  - dod status: verified 12/20 (D1 D2 D3 D5 D6 D7 D8 D9 D10 D11 D14 D15 D16 pass lines; D12 fail recorded for the session 1 and 3 aborts)
