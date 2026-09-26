using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using Nyarlathotep.Config;
using Nyarlathotep.Logic;
using ProjectM;
using ProjectM.Network;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Nyarlathotep.Services;

/// <summary>
/// The Empower action (docs/dod/faction-empowerment.md D3–D5, D7, D14, D16, D17, D20; Business rules 1, 9). Logic/CarrierLedger
/// plans every carrier operation within EmpowerBatchPerTick; this class is its game side (<see cref="Ops"/>): the faction
/// query, a unit's facts, and the carrier itself, an AB_Consumable_PhysicalPowerPotion_T02_Buff made inert and given
/// exactly the values of Logic/CarrierRecipe. A carrier is removed only through EntityExtensions.RemoveBuffSafe (D8).
/// Nothing here writes UnitStats or Health on a native NPC. EventRuntime calls <see cref="BeginTick"/> once per tick and
/// <see cref="TickEvent"/> inside each Empower event's try/catch; SpawnTracker.BootSweep hands it the carriers found at boot.
/// </summary>
internal static class EmpowerAction
{
    static readonly PrefabGUID CarrierBuff = new(Markers.CarrierBuffGuid);
    static readonly Ops _ops = new();
    static CarrierLedger _ledger = new(_ops, Log);
    static readonly FailureStreak _tickFaults = new();
    static readonly Dictionary<string, Sample> _samples = new(StringComparer.Ordinal);
    static readonly List<Sample> _reverts = new();

    /// <summary>The event whose sweep is running, for the sample and gap lines (the ops do not carry it).</summary>
    static string _current;

    internal static CarrierLedger Ledger => _ledger;

    static void Log(string line) => Core.Log.LogInfo($"[nyar] {line}");

    /// <summary>After SpawnTracker.Initialize and before its BootSweep, which queues the carriers it finds here.</summary>
    internal static void Initialize()
    {
        _ledger = new CarrierLedger(_ops, Log);
        _ops.Reset();
        _samples.Clear();
        _reverts.Clear();
    }

    /// <summary>An Empower event started: its first sweep is due this tick.</summary>
    [Mutating]
    internal static void StartCarriers(ActiveEvent active) =>
        _ledger.Start(active.Id, active.Definition.Empower!, active.Instance.EndsUtc, DateTime.UtcNow);

    /// <summary>The natural end: every carrier's LifeTime ends in this same second, so nothing is queued (D5).</summary>
    [Mutating]
    internal static void EndCarriers(string eventId)
    {
        _ledger.End(eventId);
        QueueRevert(eventId);
    }

    /// <summary>Stop and fault cancel: the event's carriers are queued for removal within the budget.</summary>
    [Mutating]
    internal static void StopCarriers(string eventId)
    {
        _ledger.Stop(eventId);
        QueueRevert(eventId);
    }

    /// <summary>Purge: every event's carriers are queued for removal.</summary>
    [Mutating]
    internal static void StopAllCarriers()
    {
        foreach (var id in _samples.Keys.ToList()) QueueRevert(id);
        _ledger.StopAll();
    }

    /// <summary>Carriers the boot marker sweep found (D7): each buff is queued for removal, never its unit. Returns how many
    /// are queued now.</summary>
    [Mutating]
    internal static int QueueBootCarriers(IReadOnlyList<long> buffs)
    {
        var before = _ledger.PendingRemovals;
        _ledger.QueueBootRemovals(buffs);
        return _ledger.PendingRemovals - before;
    }

    /// <summary>Once per scheduler tick, before the events: the removals first, within EmpowerBatchPerTick (D5). A throw is
    /// logged once per streak; the removal queue survives it.</summary>
    [Mutating]
    internal static void BeginCarrierTick()
    {
        try
        {
            _ledger.BeginTick(Settings.Limit(Limits.EmpowerBatchPerTick));
            _tickFaults.Ok();
        }
        catch (Exception ex)
        {
            if (_tickFaults.Fail()) Core.Log.LogError($"[nyar] empower removals failed: {ex.Message}");
        }
        if (Settings.VerboseLogging.Value) Samples();
    }

    /// <summary>The event's share of this tick. Throws on a failing query, which EventRuntime counts as the event's fault.</summary>
    [Mutating]
    internal static void TickCarriers(ActiveEvent active, DateTime now)
    {
        _current = active.Id;
        try { _ledger.TickEvent(active.Id, now); }
        finally { _current = null; }
    }

    /// <summary>Carriers of <paramref name="eventId"/> applied in full (the admin rows, D11).</summary>
    internal static int CarriersOf(string eventId) => _ledger.CountFor(eventId);

    static long KeyOf(Entity e) => ((long)e.Index << 32) | (uint)e.Version;
    static Entity EntityOf(long key) => new() { Index = (int)(key >> 32), Version = (int)(uint)key };

    // ---- D17: one sample NPC per event, read at apply and again after its carrier is gone (VerboseLogging). ----

    sealed class Sample(string eventId, Entity unit, string prefab, float pp, float hp)
    {
        public string EventId { get; } = eventId;
        public Entity Unit { get; } = unit;
        public string Prefab { get; } = prefab;
        public float Pp { get; set; } = pp;
        public float Hp { get; set; } = hp;
        public bool Logged { get; set; }
        public bool Ready { get; set; }
    }

    static void Took(Entity unit, string prefab)
    {
        StatChangeUtility.KillOrDestroyEntity(Core.EntityManager, unit, unit, unit, 0, StatChangeReason.Default, true);
        if (!Settings.VerboseLogging.Value || _current is null || _samples.ContainsKey(_current)) return;
        var (pp, hp) = Read(unit);
        _samples[_current] = new Sample(_current, unit, prefab, pp, hp);
    }

    static (float Pp, float Hp) Read(Entity unit) => (
        unit.TryGetComponent<UnitStats>(out var s) ? s.PhysicalPower._Value : 0f,
        unit.TryGetComponent<Health>(out var h) ? h.MaxHealth._Value : 0f);

    static void QueueRevert(string eventId)
    {
        if (_samples.Remove(eventId, out var s) && s.Logged) _reverts.Add(s);
    }

    /// <summary>The apply line one tick after the apply (the buff systems recompute the stats in between); the revert line
    /// one tick after the unit is seen without a carrier.</summary>
    static void Samples()
    {
        foreach (var s in _samples.Values.Where(s => !s.Logged))
        {
            s.Logged = true;
            if (!s.Unit.Exists()) continue;
            var (pp, hp) = Read(s.Unit);
            Log($"empower {s.EventId} sample {s.Prefab}: pp {s.Pp:0.##} -> {pp:0.##}, hp max {s.Hp:0.##} -> {hp:0.##}");
            s.Pp = pp;
            s.Hp = hp;
        }
        for (var i = _reverts.Count - 1; i >= 0; i--)
        {
            var s = _reverts[i];
            if (!s.Unit.Exists()) { _reverts.RemoveAt(i); continue; }
            if (!s.Ready)
            {
                s.Ready = _ledger.CarrierOf(KeyOf(s.Unit)) is null && CarrierOn(s.Unit) == Entity.Null;
                continue;
            }
            var (pp, hp) = Read(s.Unit);
            Log($"empower {s.EventId} sample {s.Prefab}: pp {s.Pp:0.##} -> {pp:0.##}, hp max {s.Hp:0.##} -> {hp:0.##}");
            _reverts.RemoveAt(i);
        }
    }

    /// <summary>The unit's carrier buff (the T02 potion prefab), or Entity.Null.</summary>
    static Entity CarrierOn(Entity unit)
    {
        if (!unit.Exists() || !Core.EntityManager.HasBuffer<BuffBuffer>(unit)) return Entity.Null;
        var buffs = Core.EntityManager.GetBuffer<BuffBuffer>(unit);
        for (var i = 0; i < buffs.Length; i++)
            if (buffs[i].PrefabGuid == CarrierBuff && buffs[i].Entity.Exists()) return buffs[i].Entity;
        return Entity.Null;
    }

    static bool HasMarker(Entity unit, int marker, out Entity buff)
    {
        buff = Entity.Null;
        if (!unit.Exists() || !Core.EntityManager.HasBuffer<BuffBuffer>(unit)) return false;
        var buffs = Core.EntityManager.GetBuffer<BuffBuffer>(unit);
        for (var i = 0; i < buffs.Length; i++)
        {
            var b = buffs[i].Entity;
            if (b.TryGetComponent<SpellLevel>(out var level) && level.Level == (float)marker)
            {
                buff = b;
                return true;
            }
        }
        return false;
    }

    // ---- D14: `.nyar debug here` natives. ----

    /// <summary>Native NPCs within <paramref name="radius"/> m of <paramref name="at"/>, each read back from the live
    /// entity and its carrier, as AdminLines.Natives renders them.</summary>
    internal static IReadOnlyList<string> DebugNatives(float3 at, int radius, int max)
    {
        var rows = new List<NativeRow>();
        var query = Core.EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[]
            {
                ComponentType.ReadOnly(Il2CppType.Of<PrefabGUID>()), ComponentType.ReadOnly(Il2CppType.Of<FactionReference>()),
                ComponentType.ReadOnly(Il2CppType.Of<Health>()), ComponentType.ReadOnly(Il2CppType.Of<UnitStats>()),
                ComponentType.ReadOnly(Il2CppType.Of<Translation>()),
            },
            None = new[] { ComponentType.ReadOnly(Il2CppType.Of<PlayerCharacter>()) },
            Options = EntityQueryOptions.IncludeDisabled
        });
        try
        {
            var units = query.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var unit in units)
                {
                    var distance = math.distance(unit.Read<Translation>().Value.xz, at.xz);
                    if (distance > radius || SpawnTracker.IsTracked(KeyOf(unit))) continue;
                    rows.Add(NativeOf(unit, distance));
                }
            }
            finally { units.Dispose(); }
        }
        finally { query.Dispose(); }
        return AdminLines.Natives(rows, max);
    }

    static NativeRow NativeOf(Entity unit, float distance)
    {
        var carrier = CarrierOn(unit);
        var others = 0;
        if (Core.EntityManager.HasBuffer<BuffBuffer>(unit))
        {
            var buffs = Core.EntityManager.GetBuffer<BuffBuffer>(unit);
            for (var i = 0; i < buffs.Length; i++)
                if (buffs[i].Entity != carrier && buffs[i].Entity.Has<ModifyUnitStatBuff_DOTS>()) others++;
        }
        var stats = unit.Read<UnitStats>();
        var health = unit.Read<Health>();
        var speed = unit.TryGetComponent<Movement>(out var move) ? move.Speed._Value : 0f;
        return new NativeRow(
            unit.GetPrefabGuid().GetPrefabName(),
            unit.Read<FactionReference>().FactionGuid._Value.GetPrefabName(),
            distance,
            KeyOf(unit),
            carrier == Entity.Null ? null : CarrierOf(unit, carrier),
            others,
            unit.TryGetComponent<UnitLevel>(out var level) ? level.Level._Value : 0,
            (int)MathF.Round(health.Value),
            (int)MathF.Round(health.MaxHealth._Value),
            stats.PhysicalPower._Value,
            stats.SpellPower._Value,
            unit.TryGetComponent<AbilityBar_Shared>(out var bar) ? bar.PrimaryAttackSpeed._Value : 0f,
            speed);
    }

    static NativeCarrier CarrierOf(Entity unit, Entity carrier)
    {
        var buff = carrier.Read<Buff>();
        var life = carrier.TryGetComponent<LifeTime>(out var lt) ? lt : default;
        var age = carrier.TryGetComponent<Age>(out var a) ? a.Value : 0f;
        var mark = carrier.TryGetComponent<SpellLevel>(out var sl) && Markers.KindOf(sl.Level) == MarkerKind.Carrier;
        var mods = new List<StatModifier>();
        if (Core.EntityManager.HasBuffer<ModifyUnitStatBuff_DOTS>(carrier))
        {
            var buffer = Core.EntityManager.GetBuffer<ModifyUnitStatBuff_DOTS>(carrier);
            for (var i = 0; i < buffer.Length; i++)
                mods.Add(new StatModifier(buffer[i].StatType.ToString(), buffer[i].ModificationType.ToString(), Math.Round(buffer[i].Value, 4)));
        }
        return new NativeCarrier(
            _ledger.CarrierOf(KeyOf(unit)) ?? "unknown",
            (int)(life.Duration - age),
            buff.BuffType.ToString(),
            buff.MaxStacks,
            buff.IncreaseStacks,
            life.EndAction.ToString(),
            mark,
            Ops.PresentStrip(carrier),
            mods);
    }

    /// <summary>The game side of the ledger (Logic ICarrierOps). Keys are entity Index:Version pairs.</summary>
    sealed class Ops : ICarrierOps
    {
        readonly Dictionary<int, string> _names = new();
        readonly HashSet<int> _playerTeams = new();
        readonly HashSet<string> _gapLogged = new(StringComparer.Ordinal);

        internal void Reset()
        {
            _names.Clear();
            _playerTeams.Clear();
            _gapLogged.Clear();
        }

        string Name(PrefabGUID guid)
        {
            if (!_names.TryGetValue(guid._Value, out var name)) _names[guid._Value] = name = guid.GetPrefabName();
            return name;
        }

        /// <summary>Business rules 9: PrefabGUID + FactionReference + Health + UnitStats, IncludeDisabled | IncludeSpawnTag,
        /// a unit of the factions or named in includeUnits; the faction's entities over PrefabGUID + FactionReference alone
        /// for the "query n of m" line, and on the first query with VerboseLogging the prefabs of the gap (D16).</summary>
        public SweepQuery Query(Logic.EmpowerAction action)
        {
            RefreshPlayerTeams();
            var units = new List<long>();
            var combatants = new HashSet<long>();
            ForEach(true, e =>
            {
                var prefab = Name(e.Read<PrefabGUID>());
                var faction = Name(e.Read<FactionReference>().FactionGuid._Value);
                if (!action.Factions.Contains(faction) && !action.IncludeUnits.Contains(prefab)) return;
                var key = KeyOf(e);
                units.Add(key);
                combatants.Add(key);
            });
            var gap = new Dictionary<string, int>(StringComparer.Ordinal);
            var factionEntities = 0;
            var verbose = _current is not null && Settings.VerboseLogging.Value && _gapLogged.Add(_current);
            ForEach(false, e =>
            {
                if (!action.Factions.Contains(Name(e.Read<FactionReference>().FactionGuid._Value))) return;
                factionEntities++;
                if (!verbose || combatants.Contains(KeyOf(e))) return;
                var prefab = Name(e.Read<PrefabGUID>());
                gap[prefab] = gap.GetValueOrDefault(prefab) + 1;
            });
            if (verbose && gap.Count > 0)
                Log($"empower {_current}: query gap " + string.Join(", ", gap.OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key} {g.Value}")));
            return new SweepQuery(units, factionEntities);
        }

        static void ForEach(bool combatants, Action<Entity> visit)
        {
            var all = combatants
                ? new[]
                {
                    ComponentType.ReadOnly(Il2CppType.Of<PrefabGUID>()), ComponentType.ReadOnly(Il2CppType.Of<FactionReference>()),
                    ComponentType.ReadOnly(Il2CppType.Of<Health>()), ComponentType.ReadOnly(Il2CppType.Of<UnitStats>()),
                }
                : new[] { ComponentType.ReadOnly(Il2CppType.Of<PrefabGUID>()), ComponentType.ReadOnly(Il2CppType.Of<FactionReference>()) };
            var query = Core.EntityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = all,
                Options = EntityQueryOptions.IncludeDisabled | EntityQueryOptions.IncludeSpawnTag
            });
            try
            {
                var entities = query.ToEntityArray(Allocator.Temp);
                try { foreach (var e in entities) visit(e); }
                finally { entities.Dispose(); }
            }
            finally { query.Dispose(); }
        }

        /// <summary>The Team values of player characters, read once per query (a player team, D3).</summary>
        void RefreshPlayerTeams()
        {
            _playerTeams.Clear();
            var query = Core.EntityManager.CreateEntityQuery(ComponentType.ReadOnly(Il2CppType.Of<PlayerCharacter>()),
                ComponentType.ReadOnly(Il2CppType.Of<Team>()));
            try
            {
                var teams = query.ToComponentDataArray<Team>(Allocator.Temp);
                try { foreach (var t in teams) _playerTeams.Add(t.Value); }
                finally { teams.Dispose(); }
            }
            finally { query.Dispose(); }
        }

        public UnitFacts Facts(long key)
        {
            var unit = EntityOf(key);
            if (!unit.Exists()) return null;
            var dead = unit.Has<Dead>() || (unit.TryGetComponent<Health>(out var h) && h.Value <= 0f);
            var ours = SpawnTracker.IsTracked(key) || HasMarker(unit, Markers.Unit, out _);
            string carrier = HasMarker(unit, Markers.Carrier, out _) ? "unknown" : null;
            return new UnitFacts(
                Name(unit.GetPrefabGuid()),
                unit.TryGetComponent<FactionReference>(out var f) ? Name(f.FactionGuid._Value) : "",
                unit.Has<Prefab>(),
                dead,
                unit.Has<VBloodUnit>(),
                ours,
                Ownership.Decide(new OwnershipFacts(FollowerLink(unit), EntityOwnerLink(unit), TeamLink(unit))),
                carrier);
        }

        // Each link: absent, leading to no player, leading to a player (character, user, or one hop of EntityOwner to a
        // player character), or present but unreadable, which counts as owned (D3, fail closed).
        Logic.OwnerLink FollowerLink(Entity unit)
        {
            try
            {
                if (!unit.TryGetComponent<Follower>(out var f)) return Logic.OwnerLink.Absent;
                var target = f.Followed._Value;
                return target == Entity.Null ? Logic.OwnerLink.Absent : Leads(target);
            }
            catch { return Logic.OwnerLink.Unresolvable; }
        }

        Logic.OwnerLink EntityOwnerLink(Entity unit)
        {
            try
            {
                if (!unit.TryGetComponent<EntityOwner>(out var o)) return Logic.OwnerLink.Absent;
                return o.Owner == Entity.Null || o.Owner == unit ? Logic.OwnerLink.Absent : Leads(o.Owner);
            }
            catch { return Logic.OwnerLink.Unresolvable; }
        }

        Logic.OwnerLink TeamLink(Entity unit)
        {
            try
            {
                if (!unit.TryGetComponent<Team>(out var t)) return Logic.OwnerLink.Absent;
                return _playerTeams.Contains(t.Value) ? Logic.OwnerLink.Player : Logic.OwnerLink.NotPlayer;
            }
            catch { return Logic.OwnerLink.Unresolvable; }
        }

        Logic.OwnerLink Leads(Entity target)
        {
            if (!target.Exists()) return Logic.OwnerLink.NotPlayer;
            if (target.Has<PlayerCharacter>() || target.Has<User>()) return Logic.OwnerLink.Player;
            if (target.TryGetComponent<EntityOwner>(out var o) && o.Owner != target && o.Owner.Exists()
                && (o.Owner.Has<PlayerCharacter>() || o.Owner.Has<User>())) return Logic.OwnerLink.Player;
            if (target.TryGetComponent<Team>(out var t) && _playerTeams.Contains(t.Value)) return Logic.OwnerLink.Player;
            return Logic.OwnerLink.NotPlayer;
        }

        public bool Exists(long buff) => EntityOf(buff).Exists();

        /// <summary>Create and mark (A2): the carrier exists only once SpellLevel = the carrier marker is written, so every
        /// carrier is found by the boot sweep; a throw destroys what was made through RemoveBuffSafe before it propagates.</summary>
        public long Create(long unitKey, CarrierRecipe recipe)
        {
            var unit = EntityOf(unitKey);
            if (!Core.ServerGameManager.TryInstantiateBuffEntityImmediate(unit, unit, CarrierBuff, out Entity buff) || !buff.Exists())
                throw new InvalidOperationException("carrier buff could not be applied");
            try
            {
                if (!buff.AddComponentSafe<SpellLevel>()) throw new InvalidOperationException("SpellLevel could not be added");
                buff.Write(new SpellLevel { Level = recipe.SpellLevel });
            }
            catch
            {
                try { buff.RemoveBuffSafe(); } catch { /* the boot sweep cannot see it; its prefab LifeTime ends it */ }
                throw;
            }
            return KeyOf(buff);
        }

        public void Lifetime(long key, CarrierRecipe recipe)
        {
            var buff = EntityOf(key);
            var b = buff.Read<Buff>();
            b.BuffType = Enum.Parse<BuffType>(recipe.BuffType);
            b.MaxStacks = (byte)recipe.MaxStacks;
            b.IncreaseStacks = recipe.IncreaseStacks;
            buff.Write(b);
            if (!buff.AddComponentSafe<LifeTime>()) throw new InvalidOperationException("LifeTime could not be added");
            buff.Write(new LifeTime { Duration = recipe.LifeTimeSeconds, EndAction = Enum.Parse<LifeTimeEndAction>(recipe.EndAction) });
            if (!buff.AddComponentSafe<Age>()) throw new InvalidOperationException("Age could not be added");
            buff.Write(new Age { Value = 0f });
        }

        public void Strip(long key, CarrierRecipe recipe)
        {
            var buff = EntityOf(key);
            foreach (var name in recipe.Strip)
            {
                var ok = name switch
                {
                    "CreateGameplayEventsOnSpawn" => buff.RemoveComponentSafe<CreateGameplayEventsOnSpawn>(),
                    "GameplayEventListeners" => buff.RemoveComponentSafe<GameplayEventListeners>(),
                    "RemoveBuffOnGameplayEvent" => buff.RemoveComponentSafe<RemoveBuffOnGameplayEvent>(),
                    "RemoveBuffOnGameplayEventEntry" => buff.RemoveComponentSafe<RemoveBuffOnGameplayEventEntry>(),
                    "DestroyOnGameplayEvent" => buff.RemoveComponentSafe<DestroyOnGameplayEvent>(),
                    _ => throw new InvalidOperationException($"unknown strip component {name}"),
                };
                if (!ok) throw new InvalidOperationException($"{name} could not be removed");
            }
        }

        /// <summary>The stripped gameplay-event components still on <paramref name="buff"/>, by recipe name (D14).</summary>
        internal static IReadOnlyList<string> PresentStrip(Entity buff)
        {
            var present = new List<string>();
            if (buff.Has<CreateGameplayEventsOnSpawn>()) present.Add("CreateGameplayEventsOnSpawn");
            if (buff.Has<GameplayEventListeners>()) present.Add("GameplayEventListeners");
            if (buff.Has<RemoveBuffOnGameplayEvent>()) present.Add("RemoveBuffOnGameplayEvent");
            if (buff.Has<RemoveBuffOnGameplayEventEntry>()) present.Add("RemoveBuffOnGameplayEventEntry");
            if (buff.Has<DestroyOnGameplayEvent>()) present.Add("DestroyOnGameplayEvent");
            return present;
        }

        public void Modifiers(long key, CarrierRecipe recipe)
        {
            var buff = EntityOf(key);
            if (!buff.Has<ModifyUnitStatBuff_DOTS>() && !buff.AddBufferSafe<ModifyUnitStatBuff_DOTS>())
                throw new InvalidOperationException("the stat buffer could not be added");
            var mods = Core.EntityManager.GetBuffer<ModifyUnitStatBuff_DOTS>(buff);
            mods.Clear();
            foreach (var m in recipe.Modifiers)
                mods.Add(new ModifyUnitStatBuff_DOTS
                {
                    StatType = Enum.Parse<UnitStatType>(m.Stat),
                    ModificationType = Enum.Parse<ModificationType>(m.Modification),
                    Value = (float)m.Value,
                    Modifier = 1,
                    IncreaseByStacks = false,
                    ValueByStacks = 0,
                    Priority = 0,
                    Id = ModificationIDs.Create().NewModificationId(),
                });
            // The last stage: the carrier is complete, and the unit's stats are still the base ones this frame.
            var unit = buff.Read<Buff>().Target;
            Took(unit, Name(unit.GetPrefabGuid()));
        }

        public void Remove(long key)
        {
            var buff = EntityOf(key);
            if (buff.Exists() && !buff.RemoveBuffSafe()) throw new InvalidOperationException("carrier could not be removed");
        }

        /// <summary>The fallback (S-7): LifeTime.Duration set to the carrier's age, so the game ends it next frame.</summary>
        public void Expire(long key)
        {
            var buff = EntityOf(key);
            if (!buff.Exists()) return;
            var age = buff.TryGetComponent<Age>(out var a) ? a.Value : 0f;
            if (!buff.AddComponentSafe<LifeTime>()) throw new InvalidOperationException("LifeTime could not be added");
            buff.Write(new LifeTime { Duration = age, EndAction = LifeTimeEndAction.Destroy });
        }
    }
}
