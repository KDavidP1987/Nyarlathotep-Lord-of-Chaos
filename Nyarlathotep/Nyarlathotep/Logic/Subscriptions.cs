#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>The push subscriptions (raphael-api-core D5; contract § Push events): an in-memory set of SteamIDs, each
/// added and removed only by its own player through `.nyar api sub on|off`, removed on a disconnect or when found
/// offline at a send, and gone on a restart. At most <see cref="Capacity"/> entries. Every change logs
/// "push: &lt;n&gt; subscribed (&lt;reason&gt;)" and never an id or a name.</summary>
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
    /// full. A second `on` changes nothing and logs nothing.</summary>
    public string On(ulong id)
    {
        if (!_ids.Contains(id))
        {
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
        if (_ids.Count == 0) return 0;
        HashSet<ulong> connected;
        try { connected = [.. users.Connected()]; _source.Ok(); }
        catch (Exception ex)
        {
            if (_source.Fail()) log($"push: user list unavailable ({ex.Message}); line skipped");
            return 0;
        }

        var offline = _ids.RemoveWhere(id => !connected.Contains(id));
        if (offline > 0) Changed("offline");

        var sent = 0;
        var failed = false;
        foreach (var id in _ids)
        {
            try { users.Send(id, text); sent++; }
            catch (Exception ex)
            {
                failed = true;
                if (_recipient.Fail()) log($"push: a subscriber could not be reached ({ex.Message}); skipped");
            }
        }
        if (!failed) _recipient.Ok();
        return sent;
    }

    void Changed(string reason) => log($"push: {_ids.Count} subscribed ({reason})");
}
