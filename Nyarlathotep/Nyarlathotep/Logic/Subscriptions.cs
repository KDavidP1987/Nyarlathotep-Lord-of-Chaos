#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>The push subscriptions (raphael-api-core D5; contract § Push events): an in-memory set of SteamIDs, each
/// added and removed only by its own player through `.nyar api sub on|off`, removed on a disconnect or when found
/// offline at a send, and gone on a restart. At most <see cref="Capacity"/> entries. Every change logs
/// "push: &lt;n&gt; subscribed (&lt;reason&gt;)"; no log line carries an id or a name, so a failure logs the exception's
/// type and never its message, which can hold either.</summary>
public sealed class Subscriptions(Action<string> log)
{
    public const int Capacity = 128;

    readonly HashSet<ulong> _ids = [];
    readonly FailureStreak _source = new();
    readonly FailureStreak _recipient = new();

    public int Count => _ids.Count;

    public bool Contains(ulong id) => _ids.Contains(id);

    /// <summary>The `sub` argument: true for "on", false for "off", null for anything else (badarg).</summary>
    public static bool? ParseState(string? state) => state switch
    {
        "on" => true,
        "off" => false,
        _ => null,
    };

    /// <summary>`sub on`: `[NYAR:ok] cmd=sub on=1`, or `[NYAR:err] cmd=sub code=ratelimit` when a new id finds the set
    /// full of connected players: at the cap the offline entries are pruned first (<paramref name="users"/>), so players
    /// who left without the disconnect hook never hold the slots. A second `on` changes nothing and logs nothing.</summary>
    public string On(ulong id, IUserSource? users = null)
    {
        if (!_ids.Contains(id))
        {
            if (_ids.Count >= Capacity && users is not null) Prune(users);
            if (_ids.Count >= Capacity) return Wire.Error("sub", WireError.RateLimit);
            _ids.Add(id);
            Changed("on");
        }
        return Wire.Ok("sub", ("on", "1"));
    }

    /// <summary>`sub off`: `[NYAR:ok] cmd=sub on=0`, whether or not the id was subscribed.</summary>
    public string Off(ulong id)
    {
        if (_ids.Remove(id)) Changed("off");
        return Wire.Ok("sub", ("on", "0"));
    }

    /// <summary>The disconnect hook: the player's entry goes.</summary>
    public void Disconnected(ulong id)
    {
        if (_ids.Remove(id)) Changed("disconnect");
    }

    /// <summary>Sends <paramref name="text"/> to every subscriber connected now. A subscriber found offline is removed
    /// without a send. A user list that cannot be read skips the send (the line is not kept), and a recipient that throws
    /// is skipped; each logs once per failure streak. Returns the number of subscribers reached.</summary>
    public int Deliver(IUserSource users, string text)
    {
        if (_ids.Count == 0 || !Prune(users)) return 0;
        var sent = 0;
        var failed = false;
        foreach (var id in _ids)
        {
            try { users.Send(id, text); sent++; }
            catch (Exception ex)
            {
                failed = true;
                if (_recipient.Fail()) log($"push: a subscriber could not be reached ({ex.GetType().Name}); skipped");
            }
        }
        if (!failed) _recipient.Ok();
        return sent;
    }

    /// <summary>Removes the entries not connected now. False when the user list cannot be read (logged once per
    /// streak); nothing is removed then.</summary>
    bool Prune(IUserSource users)
    {
        HashSet<ulong> connected;
        try { connected = [.. users.Connected()]; _source.Ok(); }
        catch (Exception ex)
        {
            if (_source.Fail()) log($"push: user list unavailable ({ex.GetType().Name}); nothing sent or pruned");
            return false;
        }
        if (_ids.RemoveWhere(id => !connected.Contains(id)) > 0) Changed("offline");
        return true;
    }

    void Changed(string reason) => log($"push: {_ids.Count} subscribed ({reason})");
}
