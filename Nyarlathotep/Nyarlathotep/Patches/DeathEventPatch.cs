using System;
using HarmonyLib;
using Nyarlathotep.Config;
using Nyarlathotep.Services;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;

namespace Nyarlathotep.Patches;

/// <summary>
/// Reads the frame's DeathEvents (DEV_REMINDERS #26): a dead unit of ours leaves the spawn ledger, and a V Blood kill
/// (Logic/DeathRule) raises VBloodKilled on the TriggerBus. The system runs only when something dies (DEV_REMINDERS #28), so nothing
/// ticks from here. While a FactionKills definition is startable (automation D11, D15), each death's ledger membership is
/// read first, then SpawnTracker.Died, then TriggerBus.Died inside its own try/catch, so a throw in the kill rule never
/// skips SpawnTracker.Died for this or a later death (Epic D8). While a running instance with `scoreboard: true` runs
/// (wave-sets D13), the victim's and the killer's ledger entries are read before SpawnTracker.Died and the scoreboard is
/// fed after it, each guarded by Logic/ScoreFeed; Logic/DeathPass holds the order.
/// </summary>
[HarmonyPatch(typeof(DeathEventListenerSystem), nameof(DeathEventListenerSystem.OnUpdate))]
internal static class DeathEventPatch
{
    static readonly Logic.FailureStreak Faults = new();
    static readonly Logic.FailureStreak KillFaults = new();
    static readonly Logic.FailureStreak VBloodFaults = new();

    static void ScoreLog(string line) => Core.Log.LogError($"[nyar] {line}");

    /// <summary>One death's ledger reads, taken before SpawnTracker.Died.</summary>
    sealed class DeathRead
    {
        public bool Ours;
        public Exception OursFault;
        public Logic.ScoreSides? Sides;
    }

    [HarmonyPostfix]
    public static void OnUpdate(DeathEventListenerSystem __instance)
    {
        if (!Core.IsReady) return;
        try
        {
            var deaths = __instance._DeathEventQuery.ToComponentDataArray<DeathEvent>(Allocator.Temp);
            try
            {
                bool wantsKills;
                try { wantsKills = TriggerBus.WantsKills; }
                catch (Exception ex)
                {
                    // kill-rule code: a throw here never skips SpawnTracker.Died (step 2 code review F3)
                    wantsKills = false;
                    if (KillFaults.Fail()) Core.Log.LogError($"[nyar] faction kills: death skipped: {ex.Message}");
                }
                var feed = EventRuntime.Feed;
                bool wantsScore;
                try { wantsScore = Logic.Scoreboard.Wants(EventRuntime.Engine.Active); }   // the running instances, never the catalog
                catch (Exception ex)
                {
                    wantsScore = false;
                    feed.Before(() => throw ex, ScoreLog);                                  // logged once per streak
                }
                if (!wantsScore) feed.Idle();
                var includeAdmins = wantsScore && Settings.ScoreboardIncludeAdmins.Value;
                Logic.DeathPass.Run(deaths.ToArray(), death =>
                {
                    // Both ledger reads come before SpawnTracker.Died forgets the victim; each is guarded.
                    var read = new DeathRead();
                    if (wantsKills)
                    {
                        try { read.Ours = SpawnTracker.IsOurs(death.Died); }
                        catch (Exception ex) { read.OursFault = ex; }                     // the kill feed counts it as a failed read
                    }
                    if (wantsScore) read.Sides = feed.Before(() => ScoreReader.Sides(death.Killer, death.Died), ScoreLog);
                    return read;
                }, death => SpawnTracker.Died(death.Died), (death, read) =>
                {
                    // faction-empowerment D13: a V Blood kill carries VBloodConsumeSource; a gate boss with VBloodUnit alone
                    // raises nothing (DEV_REMINDERS #26). Guarded per death, so a throw never skips this death's kill feed
                    // or a later death's SpawnTracker.Died (step 2 Codex round 2 F1).
                    try
                    {
                        if (Logic.DeathRule.IsVBloodKill(death.Died.Has<VBloodConsumeSource>(), death.Died.Has<VBloodUnit>()))
                        {
                            var died = death.Died;
                            TriggerBus.VBloodKilled(died.GetPrefabGuid().GetPrefabName(), () => KillPosition(died));   // read on demand (A38)
                        }
                        VBloodFaults.Ok();
                    }
                    catch (Exception ex)
                    {
                        if (VBloodFaults.Fail()) Core.Log.LogError($"[nyar] vblood kill: death skipped: {ex.Message}");
                    }
                    if (!wantsKills) return;
                    try
                    {
                        TriggerBus.Died(death.Killer, death.Died,
                            () => read.OursFault is null ? read.Ours : throw new InvalidOperationException($"victim read: {read.OursFault.Message}"));
                        KillFaults.Ok();
                    }
                    catch (Exception ex)
                    {
                        // The feed guards its own reads; this catches the rest, once per streak.
                        if (KillFaults.Fail()) Core.Log.LogError($"[nyar] faction kills: death skipped: {ex.Message}");
                    }
                }, (death, read) =>
                {
                    if (wantsScore)
                        feed.After(read.Sides, _ => ScoreReader.Players(death.Killer, death.Died), id => EventRuntime.Engine.Find(id),
                            EventRuntime.Board, includeAdmins, ScoreLog);
                });
            }
            finally
            {
                deaths.Dispose();
            }
            Faults.Ok();
        }
        catch (Exception ex)
        {
            // Once per failure streak: in a large fight this runs every frame.
            if (Faults.Fail()) Core.Log.LogError($"[nyar] death event read failed: {ex.Message}");
        }
    }

    /// <summary>The victim's x/z (regions D4), or null when it cannot be read; TriggerRouter.KillFor calls it only for a
    /// kill that reaches a scoped definition (A38).</summary>
    static (float X, float Z)? KillPosition(Unity.Entities.Entity died)
    {
        try
        {
            return died.TryGetComponent<Unity.Transforms.Translation>(out var t) ? (t.Value.x, t.Value.z) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>The scoreboard's reads of one death (wave-sets D8, D13), read-only, by KillReader's rule. A throw reaches
/// Logic/ScoreFeed's guard.</summary>
internal static class ScoreReader
{
    /// <summary>The victim, the killer and the killer's EntityOwner as our units (Logic ScoreSides.Killing picks). Ledger
    /// reads and one component read, before SpawnTracker.Died.</summary>
    internal static Logic.ScoreSides Sides(Entity killer, Entity died) =>
        new(SpawnTracker.EventOf(died), SpawnTracker.EventOf(killer),
            killer.TryGetComponent<EntityOwner>(out var owner) ? SpawnTracker.EventOf(owner.Owner) : null);

    /// <summary>The killer, its EntityOwner and the player that owner follows, each as a player when it is one (Logic
    /// ScorePlayers.Credited picks), the victim as a player, and whether the unit killed itself.</summary>
    internal static Logic.ScorePlayers Players(Entity killer, Entity died)
    {
        Logic.Scorer? owned = null, followed = null;
        if (killer.TryGetComponent<EntityOwner>(out var owner))
        {
            owned = Scorer(owner.Owner);
            if (owned is null && owner.Owner.TryGetComponent<Follower>(out var follows)) followed = Scorer(follows.Followed._Value);
        }
        return new Logic.ScorePlayers(Scorer(killer), Scorer(died), killer == died, owned, followed);
    }

    /// <summary>A player character's platform id (memory only, D12), character name and admin flag at the credit.</summary>
    static Logic.Scorer? Scorer(Entity e)
    {
        if (!e.TryGetComponent<PlayerCharacter>(out var pc) || !pc.UserEntity.TryGetComponent<User>(out var user) || user.PlatformId == 0) return null;
        return new Logic.Scorer(user.PlatformId.ToString(System.Globalization.CultureInfo.InvariantCulture), pc.Name.ToString(), user.IsAdmin);
    }
}

