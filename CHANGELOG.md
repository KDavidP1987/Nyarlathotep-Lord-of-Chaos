# Changelog — Nyarlathotep, Lord of Chaos (full / GitHub)

The complete technical history. The concise, player-facing changelog that ships to Thunderstore lives at
`Nyarlathotep/Nyarlathotep/CHANGELOG.md`. Public beta from 0.2.0; features stay experimental until validated on live servers.

## [0.5.2] - 2026-09-28

The `raphael-api-admin` child of the DoD Epic (`docs/dod/raphael-api-admin.md`): api 4, the admin actions as
machine-readable twins for the Raphael client. Design and session: `docs/features/RAPHAEL_API_ADMIN.md`; audit:
`docs/audits/raphael-api-admin.md`; contract: `docs/RAPHAEL_INTEGRATION_CONTRACT.md` §5a; client handoff:
`docs/RAPHAEL_HANDOFF_API4.md`.

- **Typed outcomes.** Every admin path (`Logic/AdminFlows.cs`, one flow per verb, shared by the human command and its
  twin) returns an `Outcome`: ok with keys, or a refusal with a code, an argument and a reason
  (`Logic/Outcome.cs`, contract §4). The human commands reply `outcome.Human`, pinned to 0.5.1's texts by
  `HumanReplyTests` (85 captured replies covering all 89 of 0.5.1's reply literals), so no human reply changes.
- **Twins.** `.nyar api event <verb> …` (start, stop, enable, disable, set, reload, new, copy, delete),
  `.nyar api template use <template> [as <id>]`, `.nyar api pillar <name> on|off` and `.nyar api purge [confirm]`
  answer exactly one `[NYAR:ok]` or `[NYAR:err]` line (admin-only). Order: rate gate, verb, argument count, argument
  checks, the admin log line (prefixed `api `), the action gateway, the operation. A malformed argument answers
  `code=badarg` before the log line.
- **Reads.** `.nyar api templates [pillar] [page]` (`tpl` rows), `.nyar api template info <template>`,
  `.nyar api pillar list` (five `pillar` rows) and `.nyar api killswitch` (`ks`).
- **Rate limit.** Five twins per admin per second (`Logic/RateGate.cs`, at most 64 admins tracked); the sixth
  answers `code=ratelimit secs=1`. Reads and human commands are not counted.
- **Failures.** A twin whose operation throws answers `code=io reason=internal` and logs the exception once per
  streak. `internal` promises no rollback: the action may be partly or fully applied, and the client re-reads state
  (A16).
- **Wire.** New codes `exists`, `state`, `invalid`, `full`, `io`, `confirm`, `limit`; `verb=` and `reason=` keys; a
  location value is written with one decimal, culture-invariant. `config-changed` is pushed after a pillar switch,
  as after the human command.
- **Static checks.** `Logic/ApiCommandTable.cs` copies every `.nyar api` signature; preflight `WireContract` holds
  it equal to the commands both ways, fails a parameter without a default, and `ApiOverloadTests` proves VCF can
  route every (word, argument count). The command walks reject attribute syntax they cannot read (qualified,
  aliased, escaped, listed or targeted `Command` attributes).
- **Upgrading / rollback.** No new cfg keys or files; `events.json` and `state.json` are unchanged. Raphael clients
  on api 1–3 keep using the human commands. 0.5.1 loads 0.5.2's files unchanged (rollback gate).

## [0.5.1] - 2026-09-28

The `walkable-spawns` child of the DoD Epic (`docs/dod/walkable-spawns.md`), split from event-spawns (Epic A26): wave
units spawn on walkable ground. Design and sessions: `docs/features/WALKABLE_SPAWNS.md`; audit:
`docs/audits/walkable-spawns.md`.

- **Walk check.** `Services/WalkCheck.cs` reads the game's static tile collision (read-only): per wave it builds map
  data over the live `TileWorldSingleton` (a system entity: `ServerScriptMapper.GetSingletonEntity`, then an
  `IncludeSystems` query, A11) at the height level `TileLayerUtility.GetHeightLevel(centre y)` (A13), and a point is
  walkable when `TileMapCollisionMath.CheckStaticCircle` (0.5 m circle, world metres, `CollideNormalMovement`) is free
  and `GetIsGrounded` holds. Session 1's walk probe chose the world-metres source: dry free and grounded, pond, second
  water body, cliff and building wall blocked; the tile-grid source read free everywhere (A8, A12). A default or
  uncreated TileWorld and a negative tile index never reach native code (A10).
- **Placement.** `Logic/Spawning.cs`: `SpawnPoints.Choose` keeps a free ring point, else tries the ring's 11 other
  angles, 12 angles at half the radius, then the centre (at most 25 checks); `WavePoints.Plan` keeps one point per
  unit in ring order under `WalkBudget` (2,500 game calls per tick, reset each tick; a point past it keeps its ring
  point, unchecked). A Point saved without a height is not checked. The wave line gains
  `(<m> moved, <u> unchecked)` and `, walk h <level>`.
- **Fail open.** No tile world, a height outside 0–1000 m, a throw or a failed dispose leaves the rest of that wave on
  its ring points, logs `walk check unavailable: <reason>` once per streak and adds `spawns: walk check unavailable`
  to the degraded list (health line, `.nyar status`, admin login notice) until a wave's check returns; the next wave
  tries again. `Dependency.WalkCheck` has its policy row and fault case.
- **Session 2.** bandit-ambush at a pond shore, radius 10, five waves: none in water, moved 2–3 of 6 per wave,
  tick timing avg ≤ 0.93 ms; the wave's height level equalled the admin's.
- **Removed.** The temporary `.nyar debug walk` probe of step 1. Preflight `DebugCommands` fails while any `debug`
  command's usage or parameter comparison names a verb other than `here`; `Changelogs` forbids the 0.5.0
  known-issue note in both READMEs from 0.5.1 on; ControlCases rows are keyed by plan slug and D-id.
- **Upgrading / rollback.** No new cfg keys, files or keys; events.json, state.json and api 3 unchanged. 0.5.0
  loads 0.5.1's files unchanged (rollback gate).

## [0.5.0] - 2026-09-28

The `event-library` child of the DoD Epic (`docs/dod/event-library.md`): built-in templates and in-game authoring.
Design: `docs/features/EVENT_LIBRARY.md`; sessions 1–7 there; audit: `docs/audits/event-library.md`.

- **Template catalogue.** `Resources/templates.json`, embedded and read-only, six templates (all `enabled: false`):
  legion-weekend-surge (Schedule Sat 20:00, Legion ×1.5 physical power and max health, 30 min), bandit-vengeance
  (8 bandit V Bloods, ×1.3 physical power and attack speed, 10 min, 30 min cooldown), undead-nightfall (GameTime
  night, ×1.25 physical and spell power, 20 min), militia-crackdown (15 Militia/Church V Bloods, ×1.3 max health,
  15 min, 30 min cooldown), bandit-ambush (manual, 3 waves of 4 CHAR_Bandit_Thug + 2 CHAR_Bandit_Hunter, radius 10)
  and undead-rising (manual, 2 waves of 5 armoured skeletons + 2 skeleton crossbows, radius 12). Validated at boot
  ("templates: 6/6 valid"); a bad catalogue disables the template commands, never the mod.
- **Commands.** `.nyar template list [pillar] [page] | info <id> | use <id> [as <newId>]`; `.nyar event new | copy
  | delete [confirm]` (30 s arming); `.nyar event set` gains trigger, faction, unit and `location here` fields
  (Point with x, y, z); `.nyar pillar list | <name> on|off` (written to the cfg, a running event of a pillar
  switched off ends). Every chat write is a whole-file edit through `.tmp` + replace with one `.bak`, refused when
  the file changed on disk since load or holds a newer schema, then reloaded, so a chat write equals a hand edit.
- **Readiness.** `.nyar event list` shows `ready`, `off (purge)`, `off (mod)`, `off (pillar)`, `full (cap)`,
  `invalid: <reason>` or `off (event)`; long V Blood triggers print as `vbloodkilled <n> bosses` with the bosses
  on `bosses:` lines of `info`.
- **Wave placement.** A Point keeps its height (`y`); a wave unit the game snaps onto another terrain level than
  the centre (more than 2 m off) is moved once to within 1 m of the centre, with its AI home
  (`AggroConsumer.PreCombatPosition`) (A23).
- **Slow-tick warning.** `[nyar] slow tick: <t> ms (<phase> <ms> ms, …; outside phases <r> ms)` for a tick of
  250 ms or more, at most one a minute with a held-back count, independent of `Debug.TimingLog` (A25).
- **Known issue.** A ring point in deep water at the centre's level is not detected; the unit is stuck until its
  wave ends (A27). A walkability check is planned for 0.5.1.
- **Soak.** Session 7: three boots with the six templates, a restart mid-event and a short boot after the final
  stop; `tools/soak-report.ps1` → "soak: 565 timing minutes, 27 starts, 25 ends, 2 cancelled by restart, 0 unpaired,
  0 unhandled, tick avg max 3.528 ms, templates 6/6", pass; no slow-tick line.
- **Tooling.** `tools/soak-report.ps1`, `preflight -Paths -DeclaredOf`, `-SessionsOf`, `-AuditOf`, `-RollbackOf`,
  `-DependencySuite`, `rollback-gate.ps1 -Plan`, `tools/ingame/session-events.py`.
- **Upgrading / rollback.** No new cfg keys; events.json stays SchemaVersion 1. 0.4.0 loads 0.5.0's files with the
  same valid and disabled counts (rollback gate, D29).

## [0.4.0] - 2026-09-26

The `faction-empowerment` child of the DoD Epic (`docs/dod/faction-empowerment.md`): **Pillar A, faction
empowerment**. Design: `docs/features/FACTION_EMPOWERMENT.md`; sessions 1–3 there; audit:
`docs/audits/faction-empowerment.md`.

- **`Empower` action** (pillar `empowerment`, which it must pair with): `factions` (1–5 `Faction_` names; players,
  servants, traders, critters, prisoners and `Faction_Ignored` refused), optional `includeUnits` / `excludeUnits`
  and `includeVBloods` (default false), and `stats`: `physicalPower`, `spellPower`, `maxHealth`, `attackSpeed`,
  `moveSpeed`, each a multiplier 1.0–3.0, at least one above 1.0. The seeded `example-empowerment` (disabled) rallies
  the bandits for 10 minutes after any V Blood kill.
- **Carriers, not stat writes.** Each eligible NPC gets one carrier buff (`BuffType.Replace`, marked
  `SpellLevel = 1313952069`, `LifeTime` = the event's seconds left, `EndAction Destroy`) holding the stat modifiers;
  the NPC's own stats are never written, so a carrier ends on its own even without the mod. Eligibility skips
  prefabs, the dead, V Bloods (unless included), our own spawned units and familiars' `Faction_Ignored`.
- **Sweeps and budget.** An event sweeps its factions at start and again every 15 s while active, so respawned and
  newly loaded NPCs are caught; applies and removals share `Limits.EmpowerBatchPerTick` (new cfg key, default 200,
  50–1000), removals first. Stop, purge and restart remove carriers; a carrier still present 5 s after its event's
  natural end is removed. The boot sweep splits our markers: units are despawned, carriers removed, their NPCs kept.
- **One empowerment per faction.** A start that overlaps an active Empower event's factions or units is refused
  ("faction <name> already empowered by <id>").
- **Messages and admin.** `{faction}` in announcements names the factions; `.nyar event set <id>
  action.stats.<stat> <1.0–3.0>`; `.nyar debug here` also lists up to 10 native NPCs with their carrier and stats;
  a manual start refused by the purge cooldown says how many seconds are left. The V Blood trigger now needs
  `VBloodConsumeSource`, so gate bosses no longer fire it.
- **Raphael api 3.** `.nyar api status` rows for an Empower event carry `kind=empower`, `faction=<names>`,
  `wave=-`, and for admins `units` = NPCs holding its carrier. api 3 only adds values; an api 2 client keeps working
  (`docs/RAPHAEL_HANDOFF.md` › api 3).
- **Release tooling.** `tools/dev-snapshot.ps1` (wraps every dev-server session), `tools/repo-rollback-drill.ps1`,
  `tools/rollback-gate.ps1` (the repository revert, the N-1 boot drill and the snapshot selftest as one gate),
  `tools/release-verify.ps1` (the pre-release zip's hash against the audit); `tools/rollback-drill.ps1` accepts that
  N-1 disables a definition of an action type it does not know. `preflight -SelfTest` runs every registered external
  selftest and the unit tests.
- **Upgrading.** events.json stays SchemaVersion 1 and existing definitions are unchanged. An `example-empowerment`
  written by an earlier release (a SpawnWaves action under pillar empowerment) now loads disabled with "pillar
  empowerment takes an Empower action": set its pillar to `spawns`, or replace it with the Empower template in the
  README (Faction empowerment). **Rolling back to 0.3.0:** it loads Empower definitions disabled ("unknown action type Empower") and
  does not remove carriers, which then expire within their event's remaining time (at most 2 h); run `.nyar purge
  confirm` on 0.4.0 first to remove them at once.
- 871 tests.

## [0.3.0] - 2026-09-26

The `raphael-api-core` child of the DoD Epic (`docs/dod/raphael-api-core.md`): the Raphael wire moves from api 1
(handshake only) to **api 2**. Contract: `docs/RAPHAEL_INTEGRATION_CONTRACT.md` §1–§4; client handoff:
`docs/RAPHAEL_HANDOFF.md`; sessions: `docs/features/RAPHAEL_API.md`.

- **`.nyar api status`** (anyone): one `[NYAR:event]` row per active event (`state=active`, or `ending` while its
  units wait out `GraceSeconds`, with `wave=-`), then `[NYAR:end] cmd=status count=<n>`. `units` goes to admins
  only; no row carries a position.
- **`.nyar api events [page]`** (admin): `[NYAR:def]` rows, 10 per page, then `[NYAR:end] cmd=events
  page=<cur>/<total> count=<n>`. The action is named by pillar even for a definition validation disabled; `name`
  is cut to 64 UTF-8 bytes and `reason` to 120 on a character boundary so every line stays under 480 bytes. A bad
  page answers `[NYAR:err] cmd=events code=badarg arg=page`. Both reads answer normally when `General.Enabled=false`.
- **`.nyar api sub on|off`** (anyone) and six push types, `[NYAR:ev] type=event-start|event-end|wave-warn|wave|
  killswitch|config-changed`. Subscriptions are in memory only (cap 128, `ratelimit` past it), pruned on
  disconnect through a `ServerBootstrapSystem.OnUserDisconnected` hook; the push queue holds 50 lines (oldest
  dropped) and sends 5 per tick. Fairness: `wave-warn` pushes only when the chat warning fires. An event's end drops
  its queued wave-warn lines; a waiting config-changed absorbs a new one. `.nyar event reload|set|enable|disable`
  push config-changed only when a load was applied (`Logic/DefinitionEditor`).
- **Failure isolation.** A throwing disconnect hook, user source, recipient or push entry point is caught, logged
  once per streak, and never stops the event tick or the other subscribers.
- **Preflight.** `Test-CheckWireContract` fails when a wire tag or `api` command in the code is missing from the
  contract, still PLANNED there, or the api numbers differ; `-SessionsOf` reads the preflight-checks `childDocs`
  mapping and fails on a session numbered twice; the data inventory covers each listed plan's Design › Data rows.
- **Rollback drill.** `tools/rollback-drill.ps1 -From v0.3.0 -To v0.2.1` (with `-SelfTest`) boots N on a fresh
  config, lets it write `events.json` and `state.json`, then boots N-1 on them and checks every load line. 0.3.0
  writes no new file and no new schema.
- 703 tests.

## [0.2.1] - 2026-09-25

Fixes from the foundation child's closing review (Codex whole-child cross-inspection, `docs/audits/foundation.md`
› Post-audit › Step 9). Foundation closes at 40/40 items verified.

- **Every unit despawns through the budget (A21).** 0.2.0 pushed a unit's `LifeTime` past the despawn queue's drain
  only when the event end decided its due time (A16), so a unit whose own `unitLifetimeSeconds` or
  `ManualSpawnLifetimeSeconds` decided it was removed by the game's lifetime system, one spawn batch per frame,
  outside `MaxDespawnsPerTick`. Every tracked unit now carries a due time (`SpawnLedger.Lifetime`: min(own lifetime,
  event end + `GraceSeconds`), or the manual lifetime); `SpawnLedger.QueueDue` moves due units into the budgeted
  queue each tick, earliest first; `LifeTime` is due time + ceil(`MaxTrackedUnits` / `MaxDespawnsPerTick`) s + 60 s
  for every unit, only a backstop. Session 20: 10 units with a 30 s lifetime left 1 a tick at `MaxDespawnsPerTick`
  1. The wave log line now reads "due in <n>s, lifetime <m>s".
- **Marker first (A22).** `SpawnTracker.Prepare` sets the marker buff (its `SpellLevel` record first) before any
  other fallible step and attempts `LifeTime` and `Age` even when marking fails, so a unit whose setup and destroy
  both fail keeps the boot sweep's record or its lifetime. Session 21: a kill after an autosave with 5 units alive,
  then "boot marker sweep: 5 found, 5 queued".
- 539 tests.

## [0.2.0] - 2026-09-25

First public beta: the foundation child of the DoD Epic (`docs/dod/foundation.md`, 34/40 items verified at
release, the rest close with it). The engine runs `SpawnWaves` events under every pillar switch; each pillar's
own action arrives with its child plan.

- **Pure logic core (`Logic/`, xUnit, 531 tests).** Event definitions v1 with per-field validation (a bad entry
  is disabled with its event id and the reason logged; a file that does not parse keeps the last valid set), control
  precedence (purge > `General.Enabled` > pillar switch > `[Limits]` caps > definition), server-local schedules
  keyed by occurrence so a restart never fires one twice, `ActionGateway` authorization for every mutating
  operation, the `[NYAR:*]` wire format, text sinks, message builders that cannot carry positions, the spawn
  ledger and the announcer rules.
- **Persistence.** `events.json`, `state.json` (running instances, tracked units, last fire and start per event,
  purge cooldown, daily banner key) under `BepInEx/config/Nyarlathotep/`, written through `.tmp` + `File.Replace`
  with one `.bak`; a corrupt file is kept as `.corrupt`. The first boot seeds five disabled example events.
- **Spawner.** `SpawnTracker` spawns through a per-tick budget (`MaxSpawnsPerTick`) with every unit registered,
  marked (a stat-carrying marker buff) and given a `LifeTime` that outlasts the budgeted despawn drain (A16);
  despawns go through `MaxDespawnsPerTick`; a boot sweep removes marked survivors of a crash or kill; a failed
  despawn is requeued with its slot.
- **Engine.** `TriggerBus` (Schedule, GameTime day and night, VBloodKilled via `DeathEventListenerSystem`,
  Manual), `EventScheduler` phases (spawn queues → triggers → events → announcements → health → state flush,
  each phase guarded), `EventRuntime` (waves, grace, expiry, fault limit, purge with `PurgeCooldownSeconds`),
  conditions (minPlayers, cooldownMinutes, chancePercent, window). A restart cancels every event.
- **Messages.** `Announcer` queue (20 lines, one a second, oldest informational line dropped first; a warning
  still queued at its wave time is dropped), wave warnings at `WarningOffsets`, start and end banners, the daily
  banner at `DailyBannerTime`, `.nyar announce`, a 10-minute health line, and a private degraded notice to an
  admin 10 s after login (not again on a reconnect within 60 s). `[Announcements]` switches all default off.
- **Commands.** `.nyar` (lists the caller's commands), `.nyar status`, `.nyar api version` (contract §2, api 1),
  and admin-only `.nyar event`, `.nyar spawn`, `.nyar purge`, `.nyar debug here`, `.nyar announce`. Every command
  starts with the `Core.IsReady` guard.
- **Config.** `General.AnnounceEvents` retired (S-8): the key is no longer bound and is ignored. New sections
  `[Limits]` (eight caps, clamped at load), `[Announcements]`, `[Debug]` (`VerboseLogging`, `TimingLog`;
  `FaultInjection` works in Debug builds only).
- **Fixes found in game.** A refusal line showing "< >" lost words to the chat's rich-text parser; it now says
  "angle brackets" (A20). Units spawned in an event's last tick are now included in its cleanup (A17).
- **Tooling.** `tools/preflight.ps1` gained 26 self-tested checks (structural-edit and file-write fences, patch
  guards, pillar and announcement defaults, ready guard, gateway-only mutation, fault injection debug-only,
  secrets), `-LogCheck` over the BepInEx and server logs, `-SessionsOf`, `-AuditOf`, `-Paths`, `-ServerWrites`,
  `-ListCommands`. `tools/ingame/` session helpers for the development world `save-data-nyardev`.
- **Spikes child (tooling only, not shipped).** A throwaway harness answered the engine's open questions
  (marching, marking, save behaviour of spawned units, sweep); its code was removed before foundation.
- Verified on a local dedicated server in 18 recorded sessions (`docs/features/FOUNDATION.md` › Test results).

## [0.1.0] - 2026-09-23

- **Project initialized.** BepInEx IL2CPP server-side scaffold modeled on Faust/Uriel: `Plugin` (server-only
  guard), `Core` (deferred init via `SpawnTeamSystem_OnPersistenceLoad`, coroutine host),
  `EntityExtensions`, `Config/Settings` (master switch, five pillar switches all default **off**, safety
  caps), and a bare `.nyar` root command.
- **Design + research docs.** `docs/NYARLATHOTEP_DESIGN.md`, one design doc per pillar under
  `docs/features/`, `docs/RESEARCH_NOTES.md` (sibling-mod, learning-mod, and Thunderstore findings), and
  `docs/GAME_ASSETS.md` (how to use the prefab dump; faction and key buff GUIDs).
- **Tooling.** `tools/preflight.ps1` release-surface check; local Claude Code hooks (session preflight,
  reference-path guard, release-sync reminder, spawn-safety reminder).
- **Reference data copied in (gitignored).** Prefab dump (23,535 files) and `prefab_names.tsv` from
  Beelzebub; `script_edges.tsv` (boss phase buffs); generated `unit_index.tsv` (533 units with faction,
  level, V Blood flag). Under `Learning Mods/`: Bloodcraft 1.13.24, KindredCommands, VampireCommandFramework,
  DyWorld Rising (README/DLL), and source clones of 11 public Thunderstore mods (BloodyBoss, BloodyCore,
  BloodyEncounters, XPRising, ScarletCore, TideOfWar, SanguineArchives, KindredArenas, RaidForge, HookDOTS,
  NPCs).
