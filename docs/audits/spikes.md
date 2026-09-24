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

### Step 3 · 2026-09-23 · (in progress)
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
