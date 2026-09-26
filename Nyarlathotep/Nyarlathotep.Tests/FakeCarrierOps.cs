using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>An in-memory game for CarrierLedger (faction-empowerment D5, D20): units with facts, carriers as buff keys, and
/// a failure switch per stage. <see cref="Operations"/> counts what the budget covers: units visited and removal
/// operations.</summary>
sealed class FakeCarrierOps : ICarrierOps
{
    public Dictionary<long, UnitFacts> Units { get; } = [];
    public HashSet<long> Buffs { get; } = [];
    public Dictionary<long, CarrierRecipe> Recipes { get; } = [];
    public Dictionary<long, long> BuffUnit { get; } = [];
    public List<string> Calls { get; } = [];
    public int Operations { get; set; }
    public int Queries { get; private set; }
    public int ExtraFactionEntities { get; set; } = 3;

    /// <summary>"create" (before anything exists), "mark" (inside create-and-mark, after the buff was made; the fake
    /// destroys it as the service does), "lifetime", "strip" or "modifiers": that stage throws, for <see cref="ThrowFor"/> units
    /// (all units when empty).</summary>
    public string? ThrowAt { get; set; }
    public HashSet<long> ThrowFor { get; } = [];
    public int RemoveThrows { get; set; }
    public bool ExpireThrows { get; set; }
    public bool QueryThrows { get; set; }
    long _next = 1000;

    public static FakeCarrierOps WithBandits(int count, long first = 1)
    {
        var ops = new FakeCarrierOps();
        for (var i = 0; i < count; i++) ops.Units[first + i] = Bandit();
        return ops;
    }

    public static UnitFacts Bandit(string prefab = "CHAR_Bandit_Thug") =>
        new(prefab, "Faction_Bandits", false, false, false, false, false);

    public SweepQuery Query(EmpowerAction action)
    {
        Queries++;
        if (QueryThrows) throw new InvalidOperationException("query failed");
        return new SweepQuery(Units.Keys.ToList(), Units.Count + ExtraFactionEntities);
    }

    public UnitFacts? Facts(long unit)
    {
        Operations++;
        return Units.TryGetValue(unit, out var f) ? f : null;
    }

    public bool Exists(long buff) => Buffs.Contains(buff);

    void Stage(string stage, long unit)
    {
        if (ThrowAt == stage && (ThrowFor.Count == 0 || ThrowFor.Contains(unit))) throw new InvalidOperationException($"{stage} failed");
    }

    public long Create(long unit, CarrierRecipe recipe)
    {
        Stage("create", unit);
        var buff = _next++;
        Buffs.Add(buff);
        BuffUnit[buff] = unit;
        Calls.Add($"create {unit}");
        try { Stage("mark", unit); }
        catch
        {
            Buffs.Remove(buff);                                     // destroyed before the throw leaves Create (A2)
            throw;
        }
        Recipes[buff] = recipe;
        return buff;
    }

    public void Lifetime(long buff, CarrierRecipe recipe) => Stage("lifetime", BuffUnit[buff]);
    public void Strip(long buff, CarrierRecipe recipe) => Stage("strip", BuffUnit[buff]);
    public void Modifiers(long buff, CarrierRecipe recipe) => Stage("modifiers", BuffUnit[buff]);

    public void Remove(long buff)
    {
        Operations++;
        Calls.Add($"remove {buff}");
        if (RemoveThrows > 0)
        {
            RemoveThrows--;
            throw new InvalidOperationException("remove failed");
        }
        Buffs.Remove(buff);
    }

    public void Expire(long buff)
    {
        Operations++;
        Calls.Add($"expire {buff}");
        if (ExpireThrows) throw new InvalidOperationException("expire failed");
        Buffs.Remove(buff);
    }

    public int CallCount(string prefix) => Calls.Count(c => c.StartsWith(prefix, StringComparison.Ordinal));
}
