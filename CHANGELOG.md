# Changelog — Nyarlathotep, Lord of Chaos (full / GitHub)

The complete technical history. The concise, player-facing changelog that ships to Thunderstore lives at
`Nyarlathotep/Nyarlathotep/CHANGELOG.md`. Public beta from 0.2.0; features stay experimental until validated on live servers.

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
