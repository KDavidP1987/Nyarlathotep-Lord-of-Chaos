# PREFLIGHT — session-start checklist

Work this list at the top of every working session, before making changes.

## 1. Workspace state

- [ ] `git status` — working tree clean? If not, understand what's pending before adding to it.
- [ ] `git log --oneline -5` — re-orient on where the last session left off.
- [ ] Open the `docs/features/*.md` for whatever you're about to touch. Its **Status** and **Open
      questions** sections are the hand-off from the last session.

## 2. Boundaries

- [ ] `Reference Data/` and `Learning Mods/` are **read-only** (copied reference material; gitignored).
- [ ] Sibling workspaces (Beelzebub, Uriel, Faust, Raphael, BloodCraftUI 2) are **never edited** from
      here. Read them for patterns. A `PreToolUse` hook warns on both.

## 3. Build & deploy safety

- [ ] Is the local V Rising dedicated server **running**? It file-locks `Nyarlathotep.dll` — stop it
      before any build that deploys.
- [ ] Compile-check only (no deploy):
      `dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__`

## 4. Spawn / buff safety (this mod's specific risk)

- [ ] Every unit the mod creates is registered with `SpawnTracker` and is findable by the orphan sweep
      after a restart. Nothing is spawned "fire and forget".
- [ ] Every stat, level, or buff change is **reversible**: either it lives on a timed buff that expires on
      its own, or the original value is captured so `end`/restart restores it.
- [ ] No edits to **prefab** entities without `CaptureOriginal` (a prefab edit hits every unit of that type,
      bosses included).
- [ ] Caps respected: `MaxTrackedUnits`, `MaxUnitsPerWave`, `MaxConcurrentEvents`.
- [ ] Testing on a live server? Have `.nyar purge` (or the current kill-switch) ready before you start.

## 5. Release intent

- [ ] If this session ends in a version bump: re-read CLAUDE.md → "Release & changelog discipline" (six
      surfaces move together) and run `pwsh tools/preflight.ps1` before the `chore(release)` commit.
- [ ] Touching a changelog or README? Follow `docs/DOC_STYLE.md`.

## 6. Live-server data

- [ ] If a change alters a persisted JSON schema under `BepInEx/config/Nyarlathotep/`, bump its
      `SchemaVersion` and add a migration path. Never silently drop an admin's event definitions.
