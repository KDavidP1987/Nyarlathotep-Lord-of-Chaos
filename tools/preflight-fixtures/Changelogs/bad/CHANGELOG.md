# Changelog — Nyarlathotep, Lord of Chaos (full / GitHub)

The complete technical history. The concise, player-facing changelog that ships to Thunderstore lives at
`Nyarlathotep/Nyarlathotep/CHANGELOG.md`. Pre-1.0: every feature is experimental until it is validated on
a live dedicated server.

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
