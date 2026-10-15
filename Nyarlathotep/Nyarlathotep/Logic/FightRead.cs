#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>
/// The fight read's pass over one batch of the game's stat changes (unit-watch D4, D18, the spike's verdict: the apply
/// signal, StatChangeSystem.ApplyStatChanges with the DealDamageSystem reason). Patches/FightPatch hands it the batch's row
/// count and a reader that names, for row i, the tracked unit that hit a player or was hit by one (null for any other row:
/// a hit between NPCs, on a structure, or with no tracked unit in it); each such unit's last fight time goes to the
/// <see cref="FightBook"/>. Guarded per row: a throwing row is skipped and the rest run, the first throw of a streak is
/// logged "unit-watch: read failed: &lt;message&gt;" and "unit watch: read failing" is held until a watch check passes with no
/// faulting batch since the last one (a clean batch alone does not end the streak, so a fault on every other frame logs once), the
/// events end or the server restarts. Never throws, so the host system's own work and the death pass never miss a beat.
/// Memory only: a unit key and a time, never a player.
/// </summary>
public sealed class FightRead
{
    readonly FailureStreak _streak = new();
    bool _faultSinceCheck;

    /// <summary>The read's health entry while failing (D18): the watch's own "read failing".</summary>
    public IReadOnlyList<string> Health => _streak.Count > 0 ? [UnitWatchPhase.ReadFailing] : [];

    /// <summary>Rows reported to the book since the server started (a diagnostic count, no names).</summary>
    public long Reported { get; private set; }

    /// <summary>One batch of <paramref name="count"/> rows at <paramref name="at"/>; returns the rows kept. Never throws.</summary>
    public int Pass(int count, Func<int, long?> read, FightBook book, Func<long, bool> tracked, DateTime at, Action<string> error)
    {
        var kept = 0;
        string? fault = null;
        for (var i = 0; i < count; i++)
        {
            try
            {
                if (read(i) is not { } key) continue;
                if (book.Report(key, true, at, tracked)) kept++;
            }
            catch (Exception e)
            {
                fault ??= e.Message;                                     // that row only (D18); the rest still run
            }
        }
        Reported += kept;
        if (fault is null) return kept;
        _faultSinceCheck = true;
        if (_streak.Fail()) Phases.Safe(error, UnitWatchLines.ReadFailed($"fight: {fault}"));
        return kept;
    }

    /// <summary>The events ended, the watch was turned off or purged, or the server restarted: the streak ends (D18).</summary>
    public void Ok()
    {
        _streak.Ok();
        _faultSinceCheck = false;
    }

    /// <summary>One watch check (every UnitWatch.CheckSeconds): no faulting batch since the last check ends the streak.</summary>
    public void Checked()
    {
        if (!_faultSinceCheck) _streak.Ok();
        _faultSinceCheck = false;
    }
}

/// <summary>The fight read's owner walk (unit-watch D4): from a hit row's entity, the entity itself and then up to
/// <see cref="Steps"/> owner links (a projectile, an area, a summon), as the spike read them. Pure over the patch's reads.</summary>
public static class OwnerChain
{
    /// <summary>Owner links followed past the starting entity (Codex pre-release L1: the start plus three owners).</summary>
    public const int Steps = 3;

    /// <summary>The entity's owner, when it has one.</summary>
    public delegate bool TryOwner<T>(T entity, out T owner);

    /// <summary>The first entity on the chain from <paramref name="start"/> for which <paramref name="verdict"/> answers
    /// true; null when it answers false, an entity does not exist, has no owner, or <see cref="Steps"/> links were followed.
    /// A verdict of null follows the owner.</summary>
    public static T? Find<T>(T start, Func<T, bool> exists, Func<T, bool?> verdict, TryOwner<T> owner) where T : struct
    {
        var entity = start;
        for (var link = 0; link <= Steps && exists(entity); link++)
        {
            switch (verdict(entity))
            {
                case true: return entity;
                case false: return null;
            }
            if (link == Steps || !owner(entity, out entity)) return null;
        }
        return null;
    }
}
