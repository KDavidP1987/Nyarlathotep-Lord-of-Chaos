# Research notes — prior art for Nyarlathotep

Compiled 2026-09-23 from four sources: the author's sibling mods, the reference mods under
`Learning Mods/`, the public Thunderstore ecosystem, and the prefab dump. Each section ends with **→ use**:
what we take from it. File references are to `Learning Mods/<mod>/…` unless another root is given.
Gotchas are consolidated in `DEV_REMINDERS.md`; this file is about *techniques and where to find them*.

## Headline findings

1. **Nobody has shipped NPCs that path to and damage player castles.** The Thunderstore index (300
   packages) has no working siege/invasion mod (VampireEmpire's "Church crusades" page is 404; the idea
   exists only as requests on ideas.vrisingmods.com). DyWorld Rising describes castle sieges, but ships as a
   closed DLL. The siege pillar is new ground → prototype it early and scope an MVP.
2. **Pillars A, C, D have solid prior art** — spawn-with-callback, stat scaling, marker buffs, V Blood
   fight detection, leash/aggro overrides are all proven in public code we now have locally.
3. **Vanilla Blood Moon does not empower NPCs.** `Buff_BloodMoon` is a player buff (+move speed, +blood
   efficiency). A "reverse Blood Moon" must be our own buff. The *timing* is readable though:
   `DayNightCycle` exposes `NextBloodMoonDay` / `IsBloodMoonDay()` — so "empower during blood moons" is a
   feasible trigger.
4. **Two game constraints shape sieges:** NPC hits deal 0 to stone structures unless
   `DealDamageEvent.MaterialModifiers.StoneStructure > 0` (RaidForge), and `CastleDamageMode`/raid windows
   gate structure damage server-wide. Walls and closed doors also block NPC pathing.

## Sibling mods (author's own)

| Source | Technique | → use |
|---|---|---|
| Beelzebub `Services/SummonAllyService.cs` `ApplyPlayerAllySetup` (70-275) | Full recipe to make a spawned NPC engage: aggro on, `CanPreventDisableWhenNoPlayersInRange=false`, clear drops, strip charm, `BehaviourTreeState` | Template for `UnitSetup` (drop the owner/follower-to-player parts) |
| Beelzebub `InjectAggroTarget` (1045-1070), `HandleHordeEnteringCombat` (838-923) | `AggroBuffer.Add{DamageValue,Entity,Weight}` + `Combat` state; needs a fresh `PreCombatPosition` | How to point a unit at a target |
| Beelzebub `Patches/StatChangeSystemPatch.cs` (45-106) + `ReactToDamageOnSummon` | `DamageTakenEvent` → alert the whole group | Defended zones: "hit one, the squad responds" |
| Beelzebub `GrantPowerScalingService.cs` (103-147) | Immediate carrier buff + `LifeTime` + `ModifyUnitStatBuff_DOTS` | **Empowerment core mechanism** |
| Beelzebub `DrainDespawnQueues` (757-808) | Staged despawns (budget per tick) | SpawnTracker despawn queue |
| Beelzebub `BroadcastService.cs`, `ChatNotifier.cs` | Every-N-minutes tick; message pools with placeholders; 510-byte truncation | Announcer |
| Beelzebub `PersistenceService.cs` | Dirty flag + ≤1 save/s + atomic `File.Replace` | Persistence |
| Beelzebub `AbilityCastStartedSystemPatch.cs` `SummonTargets` (56-133) | Boss summon ability → unit GUID + count | Default boss adds per boss |
| Beelzebub `Resources/script_edges.tsv` (copied to `Reference Data/`) | Boss → health-threshold phase buff | Boss reinforcement "phase" trigger |
| Uriel `Services/ObjectSpawnService.cs` (1867-1935, 268-327, 2299-2313) | Instantiate + registry-is-truth + re-resolve handles; chain-controller-aware destroy | SpawnTracker model |
| Uriel `Services/Tick.cs` | Frame-deferral driver | "wait N frames" sequencing |
| Uriel `Services/ObjectConditionsService.cs` | Global defaults + per-GUID overrides with nullable fields | Per-unit wave modifiers |
| Faust `Core.cs` coroutine host, `HeatmapSampler.cs` | `WaitForSeconds` loop re-reading config each pass | EventScheduler tick |
| Faust `Services/CastleService.cs` (78-119, 273-294, 562-574) | Territory block map, nearest heart, heart state (sealed/decaying) | Siege target selection |
| Faust `Services/PlayerInfoService.cs`, `RegionStats.cs`, `HeatmapStore.cs` | Online player positions; per-region population; historical heat | Defended-zone activity sampling |
| Faust `Services/BossService.cs` (51-255) | Live V Blood state; prefer `Translation`; off-map sentinel; roaming-boss fallbacks | Boss reinforcements: where is the boss |
| Faust `Services/FeatureControlService.cs` (75-103) | Minute-of-day windows incl. wrap past midnight | Schedule windows |
| Faust `Config/ConfigEditor.cs` | Live config edits via `k=v,k=v` (VCF 0.10 splits on spaces) | Admin event editing |
| Faust `Services/MapMarkerService.cs` | `AttachMapIconsToEntity` (experimental) | Optional event map markers — validate first |
| Raphael `WORLDSCAN_COMPLIANCE_NOTICE.md` | Community-admin pre-clearance of a sensitive feature | Clear sieges with the admin before release |

## Reference mods (in `Learning Mods/`)

### Spawning
- **KindredCommands `Services/UnitSpawnerService.cs`** — `UnitSpawnerUpdateSystem.SpawnUnit` with a random
  "duration key" + `UnitSpawnerReactSystem` prefix that matches the key, restores the real `LifeTime`, runs
  a callback. **Bug:** `return` instead of `continue` (~line 101). `Commands/SpawnNpcCommands.cs` shows the
  level override via a borrowed buff + `ModifyUnitLevelBuff` and re-applies levels after restart.
- **BloodyCore** `SpawnSystem.SpawnUnitWithCallback` — same trick, same bug, plus a global enabled flag that
  can strand callbacks. **ScarletCore** `SpawnerService` — key collides above 2^24 (float precision);
  `ImmediateSpawn` = `InstantiateEntityImmediate` + `Age` + `LifeTime` + teleport (**synchronous, no race**).
- **XPRising** — encodes faction+level into the lifetime value (stateless tagging); after spawn sets
  faction/level, **adds `DestroyWhenDisabled`** ("if the user runs far away… destroyed"), removes `Minion`
  (counters Bloodcraft's `IsMinion` stamp).
- **Bloodcraft `Systems/PrimalWarEventSystem.cs`** — starts a vanilla **War Event** (`WarEvent_StartEvent`,
  `NetworkEventType{EventId_WarEvent_StartEvent, IsAdminEvent}`), rewrites `UnitCompositionGroupEntry`
  buffers on `UC_WarEvent_*` prefabs, scales every `WarEvent_ActiveUnit`. The game's own wave engine,
  reusable.
- **Bloodcraft `Systems/Familiars/FamiliarBindingSystem.cs:157`** — `InstantiateEntityImmediate`, then the
  war-event spawn visual 0.25 s later.

→ **use:** `InstantiateEntityImmediate` as the primary spawn path (synchronous, tag same frame, spawn
visual deferred). Keep a *fixed* duration-key path (int keys < 2^24, `continue`, `count>1`, key expiry) only
if we need the spawner's ground snapping / spread. Evaluate the War Event pipeline as an alternative engine
for event spawns (Decision D6).

### Tagging & restart safety
- **Bloodcraft** — inert **marker buff** that persists with the unit (`SpellLevel.Level = 731002` magic
  number, `LifeTime{0, None}`, remove `PersistenceV2.DontSaveEntity`, strip all effects) so scaling isn't
  re-applied after restart. Also compares live stats to the prefab baseline ("AlreadyNonStandard").
- **BloodyBoss** — tags bosses via `NameableInteractable.Name += "bb"`, re-finds by suffix on startup.
- **Untested idea:** add `PersistenceV2.DontSaveEntity` to transient wave units to keep them out of the save.

→ **use:** our own marker buff with a distinct magic value (Decision D4), plus finite `LifeTime` and
`DestroyWhenDisabled` on every wave unit so orphans clean themselves up even if the mod is removed.
Spike `DontSaveEntity`. **Result (foundation A9):** it keeps the unit out of the save but not its child entities,
which come back as orphans, so spawned units save normally and the boot marker sweep removes them.

### Stats & buffs
- **KindredCommands `Services/BoostedPlayerService.cs:433-509`** + `Patches/BuffSystem_Spawn_ServerPatch.cs`
  — fill `ModifyUnitStatBuff_DOTS` as the buff spawns; stat list incl. `SiegePower`.
- **KindredCommands `Buffs.cs:10-100`** — `AddBuff(user, target, prefab, duration, immortal)`: strip event
  components, `Buff_Persists_Through_Death`, `LifeTime` handling, removal.
- **Bloodcraft `Utilities/Buffs.cs:264-324`** — turn any effect buff into a **pure visual aura** by
  stripping every gameplay component. `PrimalWarEventSystem.ModifyPrimalUnit` (522-583) — the "elite unit"
  recipe (level, HP×3, power×2, attack speed, move speed, resistances). `Core.cs:433-496` Nightmare Mode —
  global NPC buff (edits prefabs too — **not** our approach).
- **BloodyBoss `ModifyBoss`** — level + HP + `FillStats`; zero `PassiveHealthRegen`/`HealthRecovery` so the
  boss doesn't heal to full. Issue #10: high `UnitLevel` feels like immortality (level-gap scaling).

→ **use:** empowerment = carrier buff (`BuffType.Replace`, `LifeTime` = remaining event time,
`ModifyUnitStatBuff_DOTS`) + optional stripped visual aura. Spawned units = direct writes scaled from the
**prefab** baseline. Keep level deltas small; prefer HP/power multipliers.

### AI, aggro, leashing
- **Bloodcraft `Utilities/Familiars.cs:826-859`** — `AggroBuffer` injection (400 base for bosses, 100
  otherwise); `504-530` `PreCombatPosition` as the home point; detection-range tuning
  (`AggroModifiers`/`AlertModifiers` radius factors, `GainAggroByVicinity`).
- **Bloodcraft / Beelzebub `BehaviourStateChangedSystemPatch.cs`** — rewrite `GenericEnemyState.Return`.
- **TideOfWar `SpawnForWar/`** — `MaxDistanceFromPreCombatPosition` **and** `ProximityRadius` = 350 for
  long chases ("need both"); `LifeTime{120, Kill}` to cap unit counts. Side effect: huge aggro ranges pull
  in native mobs. No patrol routes.
- **Nothing in any source drives an NPC to a coordinate.** `PatrolState`/`PathWaypointNode` exist only as
  registered types.

→ **use:** "march" = invisible/immobile **anchor entity** + `Follower.Followed = anchor` (moved in steps
toward the target), with `PreCombatPosition` updated as they advance and `AggroBuffer` seeded at the
destination. **Unverified — this is Spike S1.**

### Triggers
- **Death:** `DeathEventListenerSystem` postfix (Bloodcraft `Patches/DeathEventSystemPatch.cs`; Beelzebub,
  Uriel, Faust). V Blood = `VBloodConsumeSource`; gate boss = `VBloodUnit` without it.
- **V Blood fed:** `VBloodSystem.EventList` prefix — fires per participating player, **dedupe**.
- **V Blood fight start:** **SanguineArchives** — prefix on `PlayerCombatBuffSystem_OnAggro`, find
  `InverseAggroEvents.Added` with `Producer` player and `Consumer` `VBloodUnit`. (System name differs across
  builds — Bloodcraft's commented code uses `…_InitialApplication_Aggro`; verify against current interop.)
- **V Blood fight reset:** behaviour state leaving `AnyCombat` for a V Blood, then full health.
- **Zones:** **KindredArenas** — circles `{Location, Radius}`, periodic player loop, alert cooldowns.
- **Schedules:** coroutine tick (Faust), `ServerGameManager.ServerTime` (Bloodcraft), hourly "next at
  mm:ss" (NPCs mod). **cheesasaurus/EventScheduler** runs chat commands on a cron — an alternative if we
  expose good commands.
- **Day/night:** `ServerGameManager.DayNightCycle.TimeOfDay` (KindredLogistics `BonfirePatch.cs`).
  CrimsonMoon triggers a blood moon with `DebugEventsSystem.JumpToNextBloodMoon()`.
- **Spawns of native units mid-event:** Bloodcraft `SpawnTransformSystem_OnSpawn` prefix (EliteShardBearers).

### Castles & sieges
- **RaidForge `Patches/`** — flips `CastleDamageMode` at runtime (settings + GameBalanceSettings singleton);
  raises `DealDamageEvent.MaterialModifiers.StoneStructure` in a `DealDamageSystem` prefix **via HookDOTS**
  (Harmony can't hook `ISystem`s in IL2CPP). Checks `CastleHeart.IsSieged()`. Siege Golem is a *player*
  shapeshift, not an NPC.
- **HookDOTS** (`Learning Mods/HookDOTS/`) — how to hook unmanaged systems.
- **DyWorld Rising** (README only) — faction heat → squads 3/5/8 → V Blood hunter → castle siege of 3 waves
  120 s apart, ×1/×1.5/×2, spawned 30 units from castle centre; players inside their castle radius are safe;
  spawn queue drained N per tick; 300 s unit lifetime. Likely spawns next to the castle and relies on
  normal aggro.
- **XPRising** "wanted" system — kills raise faction heat → ambush squads scaled to nearby players, never
  near a V Blood, never while in combat.

→ **use:** siege MVP = **harassment raid**: waves spawn outside the castle perimeter and engage defenders,
servants, and exposed structures under vanilla rules. Structure damage is a later, opt-in phase that must
respect `CastleDamageMode`/raid windows and **defer to RaidForge** if installed (Decision D3).

## Compatibility watch-list (mods commonly on the same servers)

| Mod | Interaction |
|---|---|
| Bloodcraft | Prefixes `UnitSpawnerReactSystem` and stamps **every** spawner unit `IsMinion` (reduced XP). Treats `BlockFeedBuff` as "is familiar". Nightmare Mode edits prefabs globally. |
| KindredCommands | Also prefixes `UnitSpawnerReactSystem`; `ModifyUnitLevelBuff` re-application on boot. |
| BloodyBoss | Spawns boss adds with `EntityOwner = boss`; world bosses renamed with `bb` suffix — exclude them from our boss triggers or treat deliberately. |
| RaidForge | Owns `CastleDamageMode` and structure damage modifiers — detect and defer. |
| XPRising | Its own ambush squads; removes `Minion` from its units. |

## Tools

Regenerate `Reference Data/unit_index.tsv` (PowerShell, from the workspace root):

```powershell
$ref = "Reference Data"; $fac = @{}
Get-ChildItem "$ref\Prefabs" -Filter 'Faction_*' | % { if ($_.Name -match '^(\S+) PrefabGuid\((-?\d+)\)') { $fac[$Matches[2]] = $Matches[1] } }
$rows = [Collections.Generic.List[string]]::new(); $rows.Add("guid`tname`tfaction`tlevel`tvblood`tminion`thealth_settings")
Get-ChildItem "$ref\Prefabs" -Filter 'CHAR_*' | Sort-Object Name | % {
  if ($_.Name -notmatch '^(\S+) PrefabGuid\((-?\d+)\)') { return }; $n=$Matches[1]; $g=$Matches[2]; $t=[IO.File]::ReadAllText($_.FullName)
  $f = if ($t -match 'FactionReference\s+FactionGuid: ModifiablePrefabGUID PrefabGuid\((-?\d+)\)') { $fac[$Matches[1]] ?? $Matches[1] } else { '' }
  $l = if ($t -match 'ProjectM\.UnitLevel\s+Level: (\d+)') { $Matches[1] } else { '' }
  $v = if ($t -match 'ProjectM\.VBloodUnit\b') { 'Y' } else { '' }; $m = if ($t -match 'ProjectM\.Minion\b') { 'Y' } else { '' }
  $h = if ($t -match 'HealthSettingsPrefabGuid: (\S+)') { $Matches[1] } else { '' }
  $rows.Add("$g`t$n`t$f`t$l`t$v`t$m`t$h") }
[IO.File]::WriteAllLines("$ref\unit_index.tsv", $rows)
```

## Spike contracts (VampireReferenceAssemblies 1.1.12)

One row per component and system the spike harness read or wrote (spikes D10, 2026-09-24; game 1.1.15.101082). "Documented" means the recipe's source; "observed" is what the spike sessions saw.

| Component / system | Fields touched | Prefab GUIDs | Source (path:line) | Observed vs documented |
|---|---|---|---|---|
| ServerGameManager.InstantiateEntityImmediate | spawns a unit from a prefab GUID | CHAR_Bandit_Thug -301730941, CHAR_Critter_Rat -2072914343 | Spikes/SpikeUnits.cs:42 | observed ≠ documented: the unit has no Age, so a LifeTime written on it never expires (A10). Every reference mod spawns through UnitSpawnerUpdateSystem.SpawnUnit (Learning Mods/KindredCommands-main/Services/UnitSpawnerService.cs:29) |
| ServerGameManager.TryInstantiateBuffEntityImmediate | applies a buff prefab to a unit and returns the buff entity | marker AB_Consumable_PhysicalPowerPotion_T01_Buff -1954355403; carrier AB_Consumable_PhysicalPowerPotion_T02_Buff -1591883586 | Spikes/SpikeUnits.cs:67, Spikes/SpikeCarrier.cs:47 | observed = documented: the buff exists on return and can be edited in the same frame |
| Translation, LastTranslation | Value (spawn position; variant 2 anchor steps) | — | Spikes/SpikeUnits.cs:45, Spikes/SpikeMarch.cs:211 | observed = documented for spawning. Writing Translation on a live unit moves it without walking (variant 2 anchor) |
| LifeTime | Duration, EndAction (Destroy on units and carriers) | — | Learning Mods/KindredCommands-main/Services/UnitSpawnerService.cs:115 | observed ≠ documented: counts only when the entity has Age; with Age it is saved and keeps counting across a restart (S2) |
| Age | Value (read; set to 0 on spawned units, A10) | — | Spikes/SpikeUnits.cs (A10); Learning Mods/RaidForge/Services/RaidInterferenceService.cs:458 | observed: absent on InstantiateEntityImmediate units, present on buffs; LifeTime remaining = Duration − Age |
| DestroyWhenDisabled | added (no fields) | — | Learning Mods/XPRising (RESEARCH_NOTES › XPRising) | observed = documented, and wider: it deletes units at boot, when players leave, and within 5 s of spawning 100 m from any player |
| CanPreventDisableWhenNoPlayersInRange | CanDisable = ModifiableBool(false) | — | Learning Mods/Bloodcraft-main/Systems/Familiars/FamiliarBindingSystem.cs:602 | observed = documented: keeps a unit enabled far from players and through a restart, so DestroyWhenDisabled does not fire |
| PersistenceV2.DontSaveEntity | added (no fields) | — | Learning Mods/Bloodcraft-main/Utilities/EntityQueries.cs:3259 | observed = documented: the unit is not in the next save and does not come back after a restart; its child entities are still saved, as orphans (foundation A9), so the foundation does not use it |
| DropTableBuffer | cleared on spawned units | — | Spikes/SpikeUnits.cs:57 | observed = documented: killed spike units dropped nothing |
| Buff, SpellLevel | Buff.Target read; SpellLevel.Level = 1314472274 on the marker | marker -1954355403 | Spikes/SpikeUnits.cs:67-90 | observed = documented: an IncludeDisabled | IncludeSpawnTag query on Buff + SpellLevel finds every marked unit, including after a restart |
| CreateGameplayEventsOnSpawn, GameplayEventListeners, RemoveBuffOnGameplayEvent, RemoveBuffOnGameplayEventEntry, DestroyOnGameplayEvent | removed from the marker and carrier buffs | -1954355403, -1591883586 | Learning Mods/KindredCommands-main/Buffs.cs:54 (buff edit pattern) | observed = documented: the buffs stay inert and are not removed by combat or feeding |
| ModifyUnitStatBuff_DOTS | buffer cleared, then PhysicalPower +0.5 and MaxHealth +1.0, MultiplyBaseAdd | -1591883586 | Spikes/SpikeCarrier.cs:58-75 | observed = documented: stats apply at once and revert on expiry, keeping the Health ratio |
| UnitStats, Health | PhysicalPower, MaxHealth, Value (read only) | — | Spikes/SpikeCarrier.cs:94-95 | observed = documented |
| FactionReference, VBloodUnit, PlayerCharacter, DestroyTag | read to pick native NPCs | — | Spikes/SpikeCarrier.cs:139-150 | observed ≠ intended: the filter admitted CHAR_Bandit_Prisoner_Villager_Female; the pillar must exclude prisoners and non-combatants (Epic A15) |
| DropInInventoryOnSpawn | read on carrier prefabs (do-not-spawn check) | — | Spikes/SpikeCarrier.cs:33 | observed = documented |
| AggroConsumer | ProximityRadius, MaxDistanceFromPreCombatPosition, Active; PreCombatPosition (variant 3) | — | Learning Mods/TideOfWar/SpawnForWar/Core.cs:395-398 | observed ≠ documented: the ranges hold, but the game drops a target beyond about 86 m or out of line of sight (S1). PreCombatPosition plus a Return override leaves the unit Idle |
| AggroModifiers | CircleRadiusFactor, ConeRadiusFactor | — | Learning Mods/TideOfWar/SpawnForWar/Core.cs:400-403 | observed: the values hold; they do not extend the prune distance |
| AggroBuffer | entry {Entity = admin, DamageValue 500, Weight 1} | — | Learning Mods/Bloodcraft-main/Utilities/Familiars.cs:826-859 | observed = documented within about 60 m with line of sight: the unit enters Combat and walks there at about 3 m/s. Beyond that the entry is removed every update |
| BehaviourTreeState | Value read; set to Return (variant 3) | — | Spikes/SpikeMarch.cs:140 | observed: the forced Return did not move the unit; a unit that loses its target stays in Combat |
| Follower | Followed, ModeModifiable = 0 | anchor -2072914343 | Learning Mods/Bloodcraft-main/Systems/Familiars/FamiliarBindingSystem.cs:453-457 | observed ≠ documented: the follower teleports and does not walk; with a non-player anchor two sessions aborted the server (entity does not exist). Only the player prefab has FollowerBuffer |
| AiMoveSpeeds | Walk, Run, Circle, Return = 0 (anchor held still) | -2072914343 | Spikes/SpikeMarch.cs:114 | observed = documented: the anchor stood still |
| PrefabGUID | read | all of the above | Spikes/SpikeCarrier.cs:112 | observed = documented |
| DestroyUtility | Destroy (through EntityExtensions.DestroySafe, 5 per batch) | — | Nyarlathotep/Nyarlathotep/EntityExtensions.cs:69 | observed = documented: staged clears of 30 units in 6 batches, no errors |

## Sources

Public repos (copies in `Learning Mods/`): BloodyBoss (github.com/oscarpedrero/BloodyBoss, 2.1.5),
Bloody.Core (oscarpedrero/BloodyCore, 2.0.2), BloodyEncounters (oscarpedrero/BloodyEncounters, 3.0.0 —
pre-1.1), Bloodcraft (mfoltz/Bloodcraft, 1.13.24), KindredCommands (Odjit/KindredCommands, 2.5.8),
KindredArenas (odjit), ScarletCore (markvaaz/ScarletCore), XPRising (aontas/XPRising, 0.5.2), RaidForge
(Darreans/RaidForge, 3.2.4), TideOfWar (adayoegi/TideOfWar, 0.1.1), SanguineArchives
(dokebi21/SanguineArchives, 0.1.1), NPCs (delta663/NPCs, 1.0.0), HookDOTS (cheesasaurus/HookDOTS, 1.1.1).
Also: mfoltz prefab/component dumps (github.com/mfoltz/mfoltz.github.io), cheesasaurus/v-rising-modding-notes.
Closed-source, read but not kept: CrimsonMoon 1.0.3, VAMP 1.3.3 (skytech6). DyWorld Rising 0.1.0 is DLL +
README only. Licenses vary — these are reference material, not code to paste; re-implement patterns.
