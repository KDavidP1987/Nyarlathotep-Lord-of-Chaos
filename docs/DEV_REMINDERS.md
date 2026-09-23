# DEV_REMINDERS — standing gotchas

Every item here cost a sibling mod real time or crashed a real server. The source is noted so you can
read the original post-mortem. `Beelz` = `..\Beelzebub Lord of Gluttony\Beelzebub\Beelzebub\`,
`Uriel` = `..\Uriel Lord of Oaths\`, `Faust` = `..\Faust Lord of Investigation\`,
`LM` = this workspace's `Learning Mods\`.

## IL2CPP / init

1. **No game-type statics at `Plugin.Load`.** `ComponentType.ReadOnly(Il2CppType.Of<T>())`, prefab lookups,
   `EntityQuery` construction — all NRE before `TypeManager` exists. Build them in `Core.TryInitialize` or
   lazily after `Core.IsReady`. *(all siblings)*
2. **Gate every patch body** with `if (!Core.IsReady) return;` and wrap it in try/catch. An exception that
   escapes a Harmony hook corrupts the server tick. *(Uriel DEV_REMINDERS)*
3. **Keep the server-only guard** in `Plugin.Load`. Nothing may need a client counterpart.
4. **Queries:** `ToEntityArray(Allocator.Temp)` inside try/finally → `Dispose`. Use
   `EntityQueryOptions.IncludeDisabled | IncludeSpawnTag` — distant NPCs carry
   `DisableWhenNoPlayersInRange` and default queries skip them. **`IncludeAll` also returns PREFAB
   entities** (Bloodcraft Nightmare Mode relies on that deliberately) — never let an empowerment pass
   touch prefabs by accident. *(Uriel #20, LM Bloodcraft `Core.cs`)*
5. **`Has<T>()` on an unregistered IL2CPP type throws.** Probe risky component types through a `SafeHas<T>`
   that disables that filter after the first throw. *(Uriel `ObjectSpawnService.cs:847-860`)*

## Spawning

6. **Every spawned unit goes through `SpawnTracker`.** In-memory `HashSet<Entity>` for the session **plus**
   a persistent marker so a boot sweep finds orphans after a crash/restart. V Rising's save drops
   mod-added *custom* components, so the marker must be a vanilla component or buff (candidate:
   `BlockFeedBuff`, the Bloodcraft/Beelzebub convention — but see Decision D4 in the design doc about
   collisions with Bloodcraft familiars). *(Uriel OBJECT_SPAWNING.md:530, Beelz `SummonAllyService.SweepOrphans`)*
7. **Never bulk-delete by a heuristic component match.** Uriel's purge-by-`CastleHeartConnection`
   destroyed ~2000 native plants; a `SpawnChainChild` sweep deleted 315 native resource nodes. Only
   entities your tracker created are safe to destroy. *(Uriel OBJECT_SPAWNING.md:493-514, 691-696)*
8. **Stage despawns.** 36 destroys in one frame crashed Beelzebub. Queue and drain a per-tick budget.
   *(Beelz CHANGELOG v0.22.1, `SummonAllyService.DrainDespawnQueues`)*
9. **Never destroy twice.** `DestroyUtility.Destroy` is deferred (stamps `DestroyTag`); a second destroy
   → Burst `AppendRemovedComponentRecordError`. Check `Has<DestroyTag>()` first. *(Beelz Morgana crash)*
10. **Deny-list crashers:** `CHAR_Mount_Horse_Vampire`, `CHAR_Mount_Horse_Gloomrot`, `CarriagePrisonerRelease*`,
    `MicroPOI*`, anything with `DropInInventoryOnSpawn`. Boss parts (`BehaviourTreeInstance.Immobile`) are
    inert. See `GAME_ASSETS.md` §Do-not-spawn.
11. **Health reads 0 on the spawn frame.** Don't use health for "alive" checks right after spawning; use
    `Exists()` + death-event pruning. *(Beelz `SummonAllyService.cs:1139`)*
12. **`UnitSpawnerUpdateSystem.SpawnUnit` is async and returns nothing.** To get the entity back, use the
    KindredCommands duration-key trick (`LM\KindredCommands-main\Services\UnitSpawnerService.cs`) — and
    **fix its bug**: line ~101 `return` must be `continue`, or one entity without `LifeTime` drops the rest
    of the batch. Bloodcraft and KindredCommands **both** prefix `UnitSpawnerReactSystem`; Bloodcraft marks
    *every* spawner unit `IsMinion` (reduced XP). Match only our own keys and expect that side effect.
13. **`InstantiateEntityImmediate(owner, guid)` is synchronous** — tag and edit the entity the same frame.
    Apply cosmetic spawn buffs ~0.25 s later, not the same frame ("blood buffs crash cause lifetime").
    *(LM Bloodcraft `FamiliarBindingSystem.cs:157`, `PrimalWarEventSystem.cs:283`)*
14. **Spawned units must be made to fight.** Some prefabs spawn with `Aggroable`/`AggroConsumer.Active` off.
    Set `CanPreventDisableWhenNoPlayersInRange.CanDisable = false` or waves freeze when no player is near.
    Clear `DropTableBuffer` (anti-farm), remove `ServantConvertable`/`CharmSource` unless you want them
    charmable. *(Beelz `ApplyPlayerAllySetup`, LM Bloodcraft `FamiliarBindingSystem.cs:600-730`)*
15. **Boss summon abilities don't work from a mod.** Many spawn nothing without the boss's NPC caster
    context. Spawn the add's unit prefab directly. *(Beelz TESTER_FEEDBACK_TRIAGE.md:263-271)*

## Buffs & stats

16. **Timed empowerment = a carrier buff with `LifeTime` + `ModifyUnitStatBuff_DOTS`.** It reverts on
    expiry by itself, which is the safety net if the server crashes mid-event.
    `ModificationType.MultiplyBaseAdd` takes a fraction (1.5× → `0.5`); `Add` takes an absolute; `Set`
    overrides. Use a fresh `ModificationIDs.Create().NewModificationId()` per entry. *(Beelz
    `GrantPowerScalingService.cs:118-147`, LM KindredCommands `BoostedPlayerService.cs:433-509`)*
17. **The carrier prefab must already have `LifeTime`.** `TryInstantiateBuffEntityImmediate` asserts it;
    the half-applied state then crashed Burst. Check the prefab dump first. *(Beelz
    `TransformBuffService.cs:189-207`)*
18. **`Clear()` the carrier's stat buffer** before adding entries, or the carrier's own bonus stacks on top.
    Strip `CreateGameplayEventsOnSpawn`, `GameplayEventListeners`, `RemoveBuffOnGameplayEvent`,
    `DestroyOnGameplayEvent` to make a buff inert. *(Beelz `NeuterCounterBuff`, LM KindredCommands `Buffs.cs`)*
19. **Prefer the Immediate buff API for NPCs.** `DebugEventsSystem.ApplyBuff` needs a `FromCharacter`
    (Bloodcraft passes the NPC as its own `User`) and lands a tick later, so a same-frame `TryGetBuff` misses.
20. **Direct stat writes are permanent** for that instance (Bloodcraft Primal/Nightmare). Fine for units we
    spawn and will despawn; **never** use them for faction empowerment of native NPCs. Scale from the
    **prefab's** base stats, not the live values, so a second pass doesn't compound. Verify the game doesn't
    recompute over direct writes (open question in Beelz PLAN-REVIEW-LOG #84).
21. **`DealDamageEvent` fields are readonly** in the interop wrapper — you cannot rescale damage per hit.
    Use stat buffs. *(Beelz `DealDamageSystemPatch.cs:12-35`)*

## Prefabs

22. **Never make structural edits to prefab entities** (Add/RemoveComponent, buffer Add/Clear). It can
    desync `LoadPersistenceSystemV2` and permanently brick saves — removing the mod doesn't fix it. Value
    edits on prefabs are safe only through `CaptureOriginal`/`RestorePrefab`, and remember a prefab edit
    changes **every** instance, bosses included. *(Beelz ABILITY_CHANGE_IMPACT.md §4b)*

## AI

23. **"Move to X" does not exist as an API in any source we have.** The levers are: `AggroBuffer` injection
    (`{DamageValue, Entity, Weight}`) + `BehaviourTreeState = Combat`; `AggroConsumer.PreCombatPosition`
    (the leash/home point — a stale one makes units refuse to chase); `Follower.Followed` to a (possibly
    invisible) leader entity; teleport via `Translation` **and** `LastTranslation`. Castle pathing is the
    project's biggest unknown — prototype it first. *(Beelz `InjectAggroTarget`, LM Bloodcraft `Familiars.cs:826-859`)*
24. **Units give up and walk home.** `BehaviourTreeStateChangedEvent` → `GenericEnemyState.Return` can be
    rewritten in a prefix on `CreateGameplayEventOnBehaviourStateChangedSystem` (both the state and the
    event's `NewState`). Only catches the transition, not already-stuck units. *(Beelz
    `BehaviourStateChangedSystemPatch.cs`, LM Bloodcraft same file)*
25. **Two writers on one component = Burst crash** (`AppendRemovedComponentRecordError`, Beelzebub's Mountup
    bug). Before writing AI/ability components, find out who else writes them (the game, Bloodcraft
    familiars, KindredCommands).

## Events & hooks

26. **Death:** postfix `DeathEventListenerSystem.OnUpdate`, read `_DeathEventQuery` → `DeathEvent{Died,Killer}`.
    Fires before the entity is destroyed. `Died.Has<VBloodConsumeSource>()` = V Blood (gate bosses have
    `VBloodUnit` without it). Resolve the killer through `EntityOwner` (familiars/summons).
27. **`VBloodSystem` fires on FEED, not kill.** A boss killed but not fed on never fires it. Use the death
    hook for "boss defeated" triggers; dedupe per boss within ~5 s (several events per kill).
28. **Sparse vs reliable hooks:** `DeathEventListenerSystem` only runs when deaths happen — don't tick from
    it. Use the coroutine host (`Core.StartCoroutine`) for scheduling. *(Beelz `Heartbeat.cs` header)*
29. **Entity handles are not stable** across stream-out, relog, or restart. Persist prefab GUID + position +
    event id, never an `Entity`.
30. **Chat:** `FixedString512Bytes` — truncate at ~480 bytes or it throws. One `ctx.Reply` per line.
    Broadcasts must check `User.IsConnected`. **Never broadcast player positions** (the Raphael ban).

## Compliance

31. **Castle raids touch other players' property.** Off by default, PvE/PvP availability as a separate
    axis, respect sealed/decaying/unclaimed hearts, and raise the design with the community admin before
    release (the World Scan pre-clearance path). *(Raphael `WORLDSCAN_COMPLIANCE_NOTICE.md`)*

## Process

32. One feature, one design doc under `docs/features/`, updated in the same commit as the code.
33. Test on the local dedicated server and log results in the feature doc. Unverified = "experimental".
34. Trace live before theorising — Beelzebub wasted eight versions on an untested theory
    (`Beelz docs/CHAIN_AUDIT.md`). Add a verbose-logging config flag early.
