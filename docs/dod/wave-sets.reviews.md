## Review 1 · 2026-10-04 · subagent · plan commit 2638ead · plan 62867 B · 27 items · files 0 · e3b0c44298fc · prompt 9de9c24ce6d8
Reviewer: fresh-context Claude subagent (general-purpose), read-only
F1 advisory · 4.3 / 9.1 — a skipped wave counts as cleared immediately and WaveGate.Decide returns Skip for "no eligible player", "player query failed", "centre claimed" and "territory unknown" (Spawning.cs:1037-1041), so once wave 1 has spawned, an AroundPlayer wave set whose players all log off or wander out of range skips wave 2, then wave 3 on the next tick, and ends "all waves defeated" plus a scoreboard though nobody fought those waves — say in Business rules 4 whether a skipped trailing wave counts toward "defeated" or victory needs every wave spawned; add the cascade case to D4 and D7's tests.
F2 advisory · 4.2 — with no free MaxTrackedUnits slots Precedence.WaveSize clamps the wave to 0 yet WaveGate still returns WaveOutcome.Spawn with an empty list (Spawning.cs:1046, Precedence.cs:51-55), which WaveDecided counts as spawned, so D7's "at least one of its waves spawned" can hold with no unit ever existing — define "spawned" for victory as "queued at least one unit", or treat a zero-unit Spawn as ZeroRolled; add the case to D7's test.
F3 advisory · 7.3 vs 5.1 — Interfaces 5.1 says Scoreboard.Wants reads EventStore.Catalog.Current while States 7.3 says the running instance keeps its definition, so an edit mid-run would stop or start kill reads while the end path uses the instance's definition — Scoreboard.Wants reads the active instances' definitions (Engine.Active); add the mid-run edit case to D11 or D13.
F4 advisory · 3.1 / 5.3 — D14's units-to-waveList conversion is unreachable from chat: `.nyar event new <id> spawns` always writes units, waves and intervalSeconds (Authoring.cs:58-63), units is required, and "validation beats chat" refuses `action.units none`, so every chat-created spawn event is refused with keys it can never clear — choose: the conversion drops those keys (or turns units into wave 1) and names them, or conversion is reachable only from JSON or the template; D14 tests it.
F5 advisory · 12.4 — D20's "wave 2 within one tick of wave 1's last unit dying" cannot be checked: LogOutput.log lines carry no timestamps and the cleared line appears only with VerboseLogging — name a timestamped source, or reword as an ordering check (the "wave 1 cleared" line right before "wave 2/3", wave 2 well before 120 s).
F6 advisory · 13.1 / 12.4 — D23 needs ten windows with at least 10 tracked units but ws-three's counts are not given, and Session 1 has Chaos killing every wave, so ten such windows may not happen; -MinTracked 10 cites no source — give the counts, run D23 in its own window with waves left alive, and cite the 10.
F7 advisory · 4.5 — a plain-text IntervalSeconds scan for D6 also matches HuntPlan.IntervalSeconds (Spawning.cs:881, HuntAction.cs:80/86), and the plan does not say what the scan prints with no files — match only reads off a SpawnWavesAction (or an allowlist) and fail on zero scanned files.
F8 advisory · 6.1 — kill credit resolves the killer through its owner but death credit takes "the killer is a tracked unit of that event" literally, so a player killed by an event unit's summon or a projectile entity goes uncounted — resolve the death-side killer through EntityOwner too; D21 confirms which entity the game reports.
F9 advisory · 12.4 — D25's and D27's dry runs show only the passing case (no planted token or platform id), and S-9 is reversible with the fallback "none needed" — record one planted-fault run for D27's grep and secrets check; re-tag S-9 validated or give it a real fallback.
F10 advisory · 2.1 / 10.1 — admin-only access for the new fields is proven only by the general preflight in D25 — add to D14's evidence that AuthSuite in PREFLIGHT OK covers `event set`.
Scenarios: skip cascade to a false victory (F1, 4.3 / 9.1); zero-slot "spawned" wave ending the event at once (F2, 4.2); a mid-run scoreboard edit (F3, 7.2 / 7.3).
Gating evidence: 2.1 and 10.1 D25 (AuthSuite); 3.3 D12, D26; 4.4 D3, D14; 6.2 D13, D18; 10.3 D27; 12.4 fails-when lines, D19, the dry runs; 14.3 D24; 14.4 D26.
15/15 layers · 49/49 probes
VERDICT: READY
### Dispositions
- F1 · accepted · Business rules 4 and D7: victory needs every wave to have queued at least one unit; a skipped or zero-rolled wave still releases its successor (D4) but rules out the early end, so such an event runs to its duration (natural end, scoreboard shown); the cascade case joins D7's fails-when (within D41: "defeated" means fought)
- F2 · accepted · D4 and D7: a Spawn decision with zero queued units (the tracked cap full) is treated as zero-rolled; D7's fails-when gains the zero-slot case
- F3 · accepted · Interfaces 5.1 and D13: Scoreboard.Wants reads the running instances' own definitions (Engine.Active), never the catalog; D13's fails-when gains the mid-run edit
- F4 · accepted · D14: on a units-form event `action.waveList.1.units …` converts it — units become wave 1 with the action modifiers copied to each entry, waves, intervalSeconds and modifiers are dropped and named in the reply; n other than 1 on a units-form event is refused; the "keys to clear" rule is dropped
- F5 · accepted · D20 is an ordering check: "event ws-three wave 1 cleared" (logged always, not only with VerboseLogging; 12.2 amended) comes before "wave 2/3", and wave 2's line comes before the 120 s mark by the vrclient results JSON's step times
- F6 · accepted · D23 runs in its own window with waves left alive; ws-three's wave 1 is 6 + 6 units, so 12 are tracked; MinTracked 10 is automation D21's threshold
- F7 · accepted · D6: the scan matches `.IntervalSeconds` reads on an expression typed SpawnWavesAction by an allowlist of the files that may (Logic/Engine.cs) and fails with "readers scan: 0 files" on an empty tree
- F8 · accepted · D8: the death-side killer is resolved through EntityOwner (as KillReader resolves the kill side) before the tracked-unit lookup; D21 confirms the entity in game
- F9 · accepted · S-9 re-tagged validated (the 100 ms scheduler cadence is the existing EventRuntime tick); D27's planted-fault run (a planted platform-id-shaped line in an untracked scratch file found by `git grep --untracked`, then the file removed) is recorded in step 3's post-audit
- F10 · accepted · D14's evidence adds `pwsh tools/preflight.ps1 -AuthSuite` → "auth suite: pass (tests, commands, …)", whose command inventory holds `event set`

## Review 2 · 2026-10-11 · subagent · plan commit 8af73f6 · plan 84401 B · 27 items · files 0 · e3b0c44298fc · prompt 1c5a50497f7d · scope A2,A4,A5,A6,A7,A8,A9,A10,A11

The rest of the plan is frozen context, so findings about it are advisory. I checked the amendments' claims against the repository at HEAD 8af73f6. These hold:
- `tools/_pf.ps1` is gone (A2).
- `pair-otherkey` still says "unknown field action.spawnVisual", and `pair-colourkey` says "unknown field action.colour". The drill lists `pair-colourkey` with `Pass = $false`, and `preflight-checks.json` expects "drill selftest: 20/20" (A9).
- `SpawnTracker.KillerText` prints only prefab names and " player", never a name (A6).
- `HuntTimingLog` is in `Logic/Spawning.cs` and is wired from `Services/EventScheduler.cs`. `HuntAction.cs` was changed in e8c35d3 and 603f958 (A7, A8).
- The three child drafts and their `.reviews.md` files exist (A8, A11).
- All five `wave-sets*.vrs` scenarios exist (A5).
- The step 3 audit has the Session 2 hand-restore line with both hash pairs equal (A10).

**Re-scored coverage:** all 15 layers Considered, as the frozen review scored them. The amendments answer 14.4 (A2, A4, A5, A8, A9, A11), 6.2 (A6) and 9.1 (A7) with stated decisions. No N/A layers.

F1 · blocking · 14.4 / D26 (an item checked whose evidence command fails), within A10's scope. A10 says D26's "snapshot restored; hashes equal" holds for Session 2 through the hand restore. But D26's own evidence command, `pwsh tools/preflight.ps1 -SessionsOf wave-sets`, cannot pass on the tree as it is, for two reasons:
- `snapshotSessions` lists `"wave-sets": 1`, so `Test-CheckSessionLogs` needs a `dev-snapshot.ps1 -Save <label>` line and a "snapshot restored; hashes equal (<label>" line in every session block from 1 on. The `### Session 2` block in `docs/features/WAVE_SETS.md` has neither.
- The audit has no line shaped `- session 2 log check: …`. Session 2's counts sit inside "- session 2 in-game: … -LogCheck 0 unhandled …", which the check's pattern does not match.

So a stranger running D26's cmd gets session 2 reported as missing and unwrapped. The Log records pass lines for `-AuditOf` and `-Paths -DeclaredOf` but none for `-SessionsOf`, yet D26 is `[x]`.
Fix: amend D26/A10 to say how `-SessionsOf` accepts a hand restore, for example a named exemption in `preflight-checks.json` like `sessionsUncounted`, or `snapshotSessions` narrowed to session 1 with A10 cited. Write a proper `- session 2 log check: 0 unhandled, <s> nyar lines, 0 orphan errors, 0 unity errors` line in the audit. Run `-SessionsOf wave-sets` and record its pass line before D26 stays checked.

F2 · advisory · A10, 3.3/7.3. The hand restore put back only the cfg and `events.json`. A4 says every boot also writes `BepInEx/config/Nyarlathotep/state.json` and `save-data-nyardev/**`, and those kept Session 2's writes. A10 should say whether that drift is accepted (dev world only) or restored.
Fix: one clause in A10 naming the files left unrestored and why that is acceptable.

F3 · advisory · A5, 14.4/D26. Paths walked declares the dev server's `BepInEx/plugins/NyarDevTools.dll`, but `tools/paths-manifest.txt` has no `server:` line for it. Only `BepInEx/plugins/Nyarlathotep.dll` is listed. D26 says every written path is in the manifest; the 116/116 reading passes because `-Paths` does not walk the server's plugins folder.
Fix: add `server: BepInEx/plugins/NyarDevTools.dll`, or state that dev-only tools are outside D26's manifest rule.

F4 · advisory · A5 wording. The amendment says "Session 1 runs in four scenario files" and then names five (wave-sets, -timing, -death, -prep-restart, -boot2).
Fix: change "four" to "five".

F5 · advisory · A5, 9.2. The first `.devkill` killed 232 world entities before it was narrowed to one prefab within a radius. The narrowing is the only safeguard, and no test or scenario step proves `.devkill` with a non-matching prefab kills nothing. It is a dev-only, unshipped tool.
Fix: a negative step in a scenario (`.devkill` of an absent prefab → `[DEV KILL] n=0`) when NyarDevTools is next touched.

F6 · advisory · A7. The fails-when promises a breakdown for any hunt-phase tick over the threshold. The build logs at 20 ms or more but at most once a minute, so a second slow tick inside a minute logs no breakdown, which contradicts the fails-when as written. A Log note records the change instead of an amendment.
Fix: restate A7's fails-when with the 20 ms threshold and the once-a-minute limit.

F7 · advisory · A6. The diagnostic is built, but its fails-when ("a ws-three unit's verbose died line names no killer while the DeathEvent carries one") has no recorded run, because no unit died in Session 2. The cause of the batch deaths is still unproven and is carried as an open question.
Fix: keep it in `docs/features/WAVE_SETS.md` Open questions (already done) and name the child or session that will exercise it.

F8 · advisory · A9 against the live text. D24's text and Build plan step 3 still name `pair-otherkey` for the "action.colour" negative. A reader checking D24's fails-when ("pair-otherkey passes") without reading A9 would test the wrong fixture.
Fix: the Report or close notes should point D24's negative to `pair-colourkey` (A9).

Hunt (scenarios not handled):
- (a) Concurrent use, 7.2: a mid-session restart with no snapshot (Session 2's pattern) leaves `state.json` and the save drifted for the next child's baseline boot (F2).
- (b) Misuse, 9.2: `.devkill` with a broad radius near the castle still kills native units of the named prefab with Chaos as the killer. On a run with a scoreboard event this would credit nothing (native units count nothing under D8), but it would change the world (F5).
- (c) Volume, 9.1: repeated hunt spikes within a minute go unlogged (F6).

15/15 layers · 49/49 probes
VERDICT: REVISE

### Dispositions
- F1 · accepted · amendment A12: Test-CheckSessionLogs gains snapshotHandRestored (fixtures good-handrestore, bad-handrestore; planted fault caught), wave-sets lists session 2, the audit gains canonical session 1 and 2 log-check lines; `-SessionsOf wave-sets` → "session logs: wave-sets 2/2 checked; snapshots 2/2 from session 1 (hand restore: session 2)", its D26 pass line recorded
- F2 · accepted · A12 names the drift: state.json and save-data-nyardev/** keep Session 2's writes, accepted as the dev world's
- F3 · accepted · tools/paths-manifest.txt gains `server: BepInEx/plugins/NyarDevTools.dll` (A12)
- F4 · accepted · a Log note corrects A5's count (five scenario files); A5 is not rewritten
- F5 · accepted · deferred to the Log: a negative `.devkill` step (an absent prefab → n=0) when NyarDevTools is next touched (swarm's or revenge's first session)
- F6 · accepted · a Log note restates A7's fails-when as built: a hunt phase of 20 ms or more logs one breakdown, at most once a minute
- F7 · accepted · deferred to the Log: A6's died-line diagnostic stays an open question in docs/features/WAVE_SETS.md; the swarm child's first session, where units die to players, exercises it
- F8 · accepted · a Log note and the close report point D24's negative to pair-colourkey (A9)
