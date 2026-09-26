#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>The game hooks this child depends on (Interfaces › External).</summary>
public enum Hook { DeathEvent, DayNight, UserConnect, UserDisconnect }

/// <summary>Attaches one hook to the game. The service side checks that the patched system or singleton exists;
/// the tests use a fake that throws. A throw means the hook is unavailable.</summary>
public interface IHookRegistry
{
    void Register(Hook hook);
}

/// <summary>Registers every hook independently: an unavailable one disables only what depends on it, logged once
/// as "hook &lt;name&gt; unavailable, pillar disabled" (D9, D31).</summary>
public sealed class HookSet(IHookRegistry registry, Action<string> log)
{
    readonly HashSet<Hook> _unavailable = [];

    public IReadOnlyCollection<Hook> Unavailable => _unavailable;

    public void RegisterAll()
    {
        foreach (var hook in Enum.GetValues<Hook>())
        {
            try { registry.Register(hook); }
            catch (Exception ex)
            {
                if (_unavailable.Add(hook)) log($"hook {hook} unavailable, pillar disabled ({ex.Message})");
            }
        }
    }

    public bool IsAvailable(Hook hook) => !_unavailable.Contains(hook);

    /// <summary>The hook a trigger type needs, or null when it needs none.</summary>
    public static Hook? HookFor(TriggerType type) => type switch
    {
        TriggerType.VBloodKilled => Hook.DeathEvent,
        TriggerType.GameTime => Hook.DayNight,
        _ => null,
    };

    public bool AllowsTrigger(TriggerType type) => HookFor(type) is not { } h || IsAvailable(h);
}

/// <summary>The connected players, read per send. The service side reads the game's User entities.</summary>
public interface IUserSource
{
    IReadOnlyList<ulong> Connected();
    void Send(ulong platformId, string text);
}

/// <summary>Sends one line to every connected user. A source that throws skips that send; a recipient that throws
/// is skipped; each logs once per failure streak (D9).</summary>
public sealed class Broadcaster(IUserSource users, Action<string> log)
{
    readonly FailureStreak _source = new();
    readonly FailureStreak _recipient = new();

    /// <summary>The number of users the line reached.</summary>
    public int SendToAll(string text)
    {
        IReadOnlyList<ulong> ids;
        try { ids = users.Connected(); _source.Ok(); }
        catch (Exception ex)
        {
            if (_source.Fail()) log($"announce: user list unavailable ({ex.Message}); send skipped");
            return 0;
        }

        var sent = 0;
        var failed = false;
        foreach (var id in ids)
        {
            try { users.Send(id, text); sent++; }
            catch (Exception ex)
            {
                failed = true;
                if (_recipient.Fail()) log($"announce: a recipient could not be reached ({ex.Message}); skipped");
            }
        }
        if (!failed) _recipient.Ok();
        return sent;
    }
}

/// <summary>Registers command groups one by one, so a group that fails to register is logged and the rest still
/// register (Dependency.CommandRegistration, D9).</summary>
public static class CommandGroups
{
    /// <summary>The number of groups registered.</summary>
    public static int RegisterEach(IEnumerable<(string Name, Action Register)> groups, Action<string> log)
    {
        var ok = 0;
        IEnumerator<(string Name, Action Register)> each;
        try { each = groups.GetEnumerator(); }
        catch (Exception ex) { log($"command discovery failed ({ex.Message}); no command group registered"); return 0; }
        using (each)
        {
            while (true)
            {
                // Discovery is lazy (reflection over the assembly), so a failure can surface at any step.
                try { if (!each.MoveNext()) break; }
                catch (Exception ex) { log($"command discovery failed ({ex.Message}); {ok} group(s) registered before it"); break; }
                var (name, register) = each.Current;
                try { register(); ok++; }
                catch (Exception ex) { log($"command group {name} failed to register ({ex.Message})"); }
            }
        }
        return ok;
    }
}
