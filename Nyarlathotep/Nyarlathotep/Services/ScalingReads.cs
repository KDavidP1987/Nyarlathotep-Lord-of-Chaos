using System.Collections.Generic;
using System.Linq;
using Nyarlathotep.Config;
using Nyarlathotep.Logic;
using ProjectM;
using ProjectM.Network;
using Unity.Entities;

namespace Nyarlathotep.Services;

/// <summary>
/// Scaling's game side (docs/dod/scaling.md step 3): the players as Logic/Scaling reads them (PlayerQuery's rows, with each
/// row's admin flag and gear level read here, only when a scaled decision reads, D2, D6), the action scope's test (the region index, as the wave's own scope check), the V Blood
/// flag (D8) and the server's one <see cref="ScalingRuntime"/>: the latest decision per instance for `event info` (D15) and
/// the read guard with its health entry (D12). Reads only, never a write; every read runs inside a wave or apocalypse
/// decision, held for that decision and never logged or saved (D11). Every end path of EventRuntime calls
/// <see cref="EventEnded"/>; a restart and a purge call <see cref="Clear"/>.
/// </summary>
internal static class ScalingReads
{
    static readonly Reader _reader = new();

    internal static ScalingRuntime State { get; } = new();

    /// <summary>"scaling: player read failing" while a read fails (HealthMonitor, `.nyar status`; D12).</summary>
    internal static IReadOnlyList<string> Health => State.Health;

    /// <summary>One of D15's end paths (EventRuntime.EndSpawnState).</summary>
    internal static void EventEnded(string id) => State.EventEnded(id);

    /// <summary>A restart (EventRuntime.Initialize) or a purge: nothing is kept.</summary>
    internal static void Clear() => State.Clear();

    /// <summary>`event info`'s "scaling now: …" line of a running instance, or null.</summary>
    internal static string NowLine(ActiveEvent active) => State.NowLine(active);

    /// <summary>The fresh read of one SpawnWaves wave (D2): null without the block (nothing read) or when the read failed
    /// (the wave goes on unscaled, D12). Never throws.</summary>
    internal static ScalingRead Read(ActiveEvent active, SpawnWavesAction action) =>
        State.Read(active, action.Scaling, action.Scope, _reader, IncludeAdmins, Warn);

    /// <summary>The wave's scaled entries, recorded and logged (D3, D4, D10).</summary>
    internal static ScaledWave Wave(ActiveEvent active, SpawnWavesAction action, int wave, ScalingRead read, IReadOnlyList<(float X, float Z)> origins) =>
        State.Wave(active, action, wave, read, origins, line => Core.Log.LogInfo($"[nyar] {line}"));

    /// <summary>A running apocalypse's source (D5), or null without the block.</summary>
    internal static ScalingSource Source(ActiveEvent active) => State.Source(active, _reader, IncludeAdmins, Warn);

    /// <summary>True when <paramref name="unit"/> carries VBloodUnit (D8); Logic/Scaling.LevelAtSetup guards the read.</summary>
    internal static bool IsVBlood(Entity unit) => unit.Has<VBloodUnit>();

    /// <summary>[Scoreboard] IncludeAdmins, read at each decision (D2).</summary>
    static bool IncludeAdmins() => Settings.ScoreboardIncludeAdmins.Value;

    static void Warn(string line) => Core.Log.LogWarning($"[nyar] {line}");

    sealed class Reader : IScalingReader
    {
        /// <summary>PlayerQuery's rows, read afresh (D2), each with its admin flag and gear level. Throws when the user query
        /// or one admin flag cannot be read, so the whole decision goes unscaled (D12): an unread flag never counts an admin
        /// IncludeAdmins leaves out. An unreadable gear level is null for that player only (D6). In a Debug build the
        /// "scaleplayers:&lt;n&gt;@&lt;gear&gt;" setting adds its phantoms (D21).</summary>
        public IReadOnlyList<ScalingPlayer> Players()
        {
            var rows = PlayerQuery.Read().Select(p => new ScalingPlayer(p.X, p.Z, true, p.Alive, IsAdmin(p.Character), GearLevel(p.Character))).ToList();
#if DEBUG
            if (ScalePhantoms.Parse(Settings.FaultInjection?.Value) is { } phantoms)
            {
                var placed = ScalePhantoms.Place(rows, phantoms.N, phantoms.Gear);
                rows.AddRange(placed);
                Core.Log.LogInfo($"[nyar] {ScalePhantoms.PlacedLine(placed.Count, phantoms.N, phantoms.Gear)}");
            }
#endif
            return rows;
        }

        /// <summary>The wave's own scope check (WavePoints.ScopeCheck over the region index): outside every region when the
        /// index is unavailable.</summary>
        public bool InScope(Scope scope, float x, float z)
        {
            Func<float, float, string> regionOf = RegionMap.State.Available ? RegionMap.State.Index.RegionOf : null;
            return WavePoints.ScopeCheck(scope, regionOf) is not { } check || check(x, z);
        }

        /// <summary>The character's user's User.IsAdmin (D2); throws when it cannot be read (D12).</summary>
        static bool IsAdmin(Entity character)
        {
            var user = character.Read<PlayerCharacter>().UserEntity;
            return user.Read<User>().IsAdmin;
        }

        /// <summary>The character's gear level (D6, spike S-6): the sum of its Equipment's ArmorLevel, SpellLevel and
        /// WeaponLevel, which Logic/Scaling rounds down; null when it cannot be read or is not a number.</summary>
        static double? GearLevel(Entity character)
        {
            try
            {
                if (!character.TryGetComponent<Equipment>(out var eq)) return null;
                float armor = eq.ArmorLevel, spell = eq.SpellLevel, weapon = eq.WeaponLevel;
                var sum = (double)armor + spell + weapon;
                return double.IsFinite(sum) ? sum : null;
            }
            catch { return null; }
        }
    }
}
