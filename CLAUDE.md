# CLAUDE.md — Nyarlathotep, Lord of Chaos

Guidance for Claude Code when working in this workspace.

## What this workspace is

**Nyarlathotep, Lord of Chaos** is a **server-side** BepInEx IL2CPP plugin for V Rising's dedicated
server. It adds admin-configured, event-driven NPC behaviour the base game doesn't have. Four pillars:

1. **Faction empowerment** — for N minutes, on a schedule or after a trigger (e.g. a V Blood is killed),
   every NPC of a faction is buffed. A Blood Moon for the NPCs.
2. **Sieges & defended zones** — NPC waves that march on player castles; admin-defined zones that summon
   reinforcements when vampire activity inside them crosses a threshold.
3. **Boss reinforcements** — adds that join V Blood fights.
4. **Event spawns** — waves of chosen unit types into chosen areas, at base stats or with level/HP/damage
   modifiers.

Everything ships **disabled**; admins opt in per pillar and per event.

Same family as the author's other mods — **Beelzebub** (server), **Uriel** (server), **Faust** (server),
and **Raphael** (client, formerly BloodCraftHub). Nyarlathotep mirrors the Faust/Uriel architecture.

> **Status:** freshly scaffolded (v0.1.0). The project builds and loads; no pillar is implemented. The
> first build target is the **Foundation** (SpawnTracker, EventScheduler, TriggerBus, JSON event store,
> admin commands) — see `docs/NYARLATHOTEP_DESIGN.md` §"Build order". Open design decisions are listed in
> that doc's §"Open decisions" and must be settled with the user before the code that depends on them.

The only buildable project lives at `Nyarlathotep/` (`Nyarlathotep.sln`). When the user says "the mod",
"this codebase", or "our mod" they mean `Nyarlathotep/`.

## Reference material (read, never edit)

A `PreToolUse` hook (`.claude/hooks/guard-reference-paths.ps1`) warns on edits to any of these.

**Copied into this workspace (gitignored):**

- `Reference Data/Prefabs/` — V Rising prefab dump, 23,535 files, `<PrefabName> PrefabGuid(<int>).txt`.
- `Reference Data/prefab_names.tsv` — `<guid>\t<name>` for fast lookup.
- `Reference Data/unit_index.tsv` — every `CHAR_*` unit with faction, level, V Blood flag (generated here).
- `Reference Data/script_edges.tsv` — boss health-phase buff edges (boss reinforcement triggers).
- `Learning Mods/Bloodcraft-main/` (v1.13.24) — spawning (`Systems/PrimalWarEventSystem.cs`), familiar
  AI/aggro (`Utilities/Familiars.cs`, `Systems/Familiars/`), buffs, death/V Blood hooks, custom ECS systems.
- `Learning Mods/KindredCommands-main/` (v2.5.8) — `Services/UnitSpawnerService.cs` (spawn-with-callback),
  `Commands/SpawnNpcCommands.cs` (level override), `Buffs.cs`, castle territory/region services.
- `Learning Mods/VampireCommandFramework-main/` — VCF source.
- `Learning Mods/DyWorld-DyWorld_Rising-0.1.0/` — **closest existing design** (faction heat → hunting
  squads → castle sieges). DLL + README only, no source.
- Public Thunderstore mods (source clones, 2026-09-23): `BloodyBoss` + `BloodyCore` (boss stat scaling,
  summon adds, spawn callbacks), `BloodyEncounters` (random encounters), `XPRising` (ambush squads,
  `DestroyWhenDisabled`), `ScarletCore` (immediate spawn), `TideOfWar` (NPC armies, leash tuning),
  `SanguineArchives` (V Blood fight start/reset detection), `KindredArenas` (circle zones), `RaidForge`
  (castle damage windows, structure damage), `HookDOTS` (hooking unmanaged systems), `NPCs` (scheduled
  spawns — includes an anti-pattern). Licenses vary: learn from them, re-implement, don't paste.

How to use the asset data: **`docs/GAME_ASSETS.md`**. What we learned from every source: **`docs/RESEARCH_NOTES.md`**.

**Sibling workspaces (never edit from here):** `..\Beelzebub Lord of Gluttony\`, `..\Uriel Lord of Oaths\`,
`..\Faust Lord of Investigation\`, `..\Raphael Lord of Wisdom\`, `..\BloodCraftUI 2\`. The canonical
prefab dump lives in Beelzebub's `Reference Data\`; refresh the copy here from it after a game update.

## Project layout

```
Nyarlathotep/
├── Nyarlathotep.sln
└── Nyarlathotep/                  ← C# project root
    ├── Nyarlathotep.csproj        ← single version source (auto-generates MyPluginInfo)
    ├── Plugin.cs                  ← entry point (server-only guard, Harmony, VCF)
    ├── Core.cs                    ← deferred init hub (IsReady gate) + coroutine host
    ├── EntityExtensions.cs        ← Exists-guarded entity helpers
    ├── Patches/                   ← Harmony patches (one file per patched system)
    ├── Services/                  ← SpawnTracker, EventScheduler, TriggerBus, pillar services
    ├── Commands/                  ← VCF commands (.nyar …)
    ├── Config/Settings.cs         ← BepInEx scalars: master/pillar switches + safety caps
    ├── Resources/                 ← (planned) embedded default event templates (JSON)
    ├── README.md / CHANGELOG.md   ← THUNDERSTORE page + concise changelog (to be written)
    └── thunderstore.toml          ← versionNumber synced to csproj
docs/
├── NYARLATHOTEP_DESIGN.md         ← vision, architecture, config/persistence, build order, OPEN DECISIONS
├── features/                      ← one design doc per pillar; keep current as features evolve
├── GAME_ASSETS.md · RESEARCH_NOTES.md · DEV_REMINDERS.md · PREFLIGHT.md · DOC_STYLE.md
tools/preflight.ps1                ← release-surface sync check
Reference Data/ · Learning Mods/   ← reference-only (gitignored)
```

- Plugin GUID `kdpen.Nyarlathotep`; chat root `.nyar` (see Decision D1).
- `net6.0`, `BepInEx.Unity.IL2CPP` 6.0.0-be.733, `VampireReferenceAssemblies` 1.1.12-r99041-b2,
  `VRising.VampireCommandFramework` 0.10.* — versions match the siblings so they coexist on one server.
- Intended GitHub repo: `KDavidP1987/Nyarlathotep-Lord-of-Chaos` (not created yet).

## Build & local deploy

```powershell
cd Nyarlathotep
dotnet build Nyarlathotep.sln -c Release                                        # builds + deploys
dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__  # compile check only
```

- `BuildToServer` copies the DLL to
  `C:\Program Files (x86)\Steam\steamapps\common\VRisingDedicatedServer\BepInEx\plugins` when that folder
  exists. **Stop the dedicated server first** — it file-locks the DLL.
- `BuildToDist` stages the DLL + CHANGELOG under `Nyarlathotep/Nyarlathotep/dist/` for `tcli`.
- No unit tests, no lint. Verification is **in-game on the dedicated server**: build, launch, watch the
  BepInEx console, exercise `.nyar` commands. Record results in the feature doc.

## Release & changelog discipline — SIX surfaces move together

In one `chore(release): vX.Y.Z` commit:

1. `Nyarlathotep/Nyarlathotep/Nyarlathotep.csproj <Version>` **and** 2. `thunderstore.toml versionNumber` (identical).
3. `CHANGELOG.md` (root) — full GitHub changelog, every version, technical detail welcome.
4. `Nyarlathotep/Nyarlathotep/CHANGELOG.md` — concise, player-facing Thunderstore changelog.
5. `README.md` (root) — GitHub landing page. 6. `Nyarlathotep/Nyarlathotep/README.md` — Thunderstore page.

Plus: any `docs/features/*.md` whose behaviour changed. Run `pwsh tools/preflight.ps1` first. Follow
`docs/DOC_STYLE.md` (say cross-cutting facts once; no per-entry boilerplate). The
`release-sync-reminder.ps1` hook is a backstop; this rule is authoritative.

## Spawn & buff safety — the rule specific to this mod

Nyarlathotep creates entities and alters live NPCs. The failure modes are severe (save pollution,
permanently buffed NPCs, crashed server ticks), so:

- **Every spawned unit is registered with `SpawnTracker`** and is findable by the boot-time orphan sweep.
- **Every change is reversible.** Empowerment rides on timed carrier buffs that expire on their own. Direct
  stat writes are allowed only on units we spawned (and will despawn).
- **No structural prefab edits, ever.** Prefab value edits only via capture/restore.
- **Stage despawns** through a per-tick budget; never mass-destroy in one frame.
- **Respect the caps** in `Config/Settings.cs` and keep a one-command kill switch working at all times.

`docs/DEV_REMINDERS.md` has the full list with sources. The `spawn-safety-reminder.ps1` hook fires on
edits to spawn/buff/AI services and patches.

## Decision presentation — options + a recommendation

When work reaches a point where the user must decide (design direction, scope, trade-offs), present the
decisions together — each with: the decision and why it matters, the realistic options with trade-offs,
a recommendation, and *Resolved* status for anything already settled. Use plan mode for this when
mid-implementation. Record settled answers in `docs/NYARLATHOTEP_DESIGN.md` §"Open decisions".

## Git workflow

- Conventional Commits: `feat|fix|chore|docs|refactor|test|build|ci|perf|style|revert(scope)?: subject`;
  release commits `chore(release): vX.Y.Z`.
- `gh` CLI is authenticated as `KDavidP1987`. GitHub is the public home until Thunderstore publication.

## Shell notes (this Windows / OneDrive environment)

- `robocopy` exit codes 1–7 are success; check `$LASTEXITCODE -lt 8`.
- OneDrive can transiently lock files during move/delete — re-inspect and retry, don't assume data loss.
- Run `Remove-Item` as its own minimal command (the sandbox false-positives when it's combined with regex
  or other path operations).

## Session start

Work `docs/PREFLIGHT.md` (a `SessionStart` hook reminds you). Short form: git status → reference paths are
read-only → open the feature doc → spawn/buff safety → stop the server before deploying.
