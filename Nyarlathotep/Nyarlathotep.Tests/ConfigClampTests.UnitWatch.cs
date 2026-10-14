using System.Text.RegularExpressions;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>unit-watch D1, D34: the [UnitWatch] section binds Enabled (true), StuckSeconds (90, 30-600), StallMinutes (10,
/// 0-60) and SpawnStillSeconds (10, 5-60) through Config/Settings.cs as the [Limits] keys are bound, clamped with one warning naming the key; the tries are 2
/// and not configurable; no definition, template or state.json property is added.</summary>
public partial class ConfigClampTests
{
    static string SettingsText => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", "Settings.cs.txt"));

    static readonly (string Key, int Default, int Min, int Max)[] WatchKeys = [("StuckSeconds", 90, 30, 600), ("StallMinutes", 10, 0, 60), ("SpawnStillSeconds", 10, 5, 60)];

    /// <summary>What keeps the cfg text and the limits from binding the three keys as planned; empty when they do.</summary>
    internal static List<string> UnitWatchProblems(string settings, IReadOnlyList<IntLimit> limits)
    {
        var p = new List<string>();
        if (!Regex.IsMatch(settings, @"Bind\(\s*""UnitWatch""\s*,\s*""Enabled""\s*,\s*true\b")) p.Add("Enabled is not bound with default true");
        foreach (var (key, def, min, max) in WatchKeys)
        {
            var l = limits.FirstOrDefault(x => x.Section == "UnitWatch" && x.Key == key);
            if (l is null) { p.Add($"{key} is not a limit"); continue; }
            if ((l.Default, l.Min, l.Max) != (def, min, max)) p.Add($"{key} is {l.Default} ({l.Min}-{l.Max})");
            if (!Regex.IsMatch(settings, $@"BindLimit\(\s*config\s*,\s*Limits\.{key}\b")) p.Add($"{key} is not bound through BindLimit");
        }
        if (limits.Any(l => l.Key.Contains("Tries", StringComparison.Ordinal)) || Regex.IsMatch(settings, @"""UnitWatch""\s*,\s*""\w*Tries""")) p.Add("the tries are configurable");
        return p;
    }

    static List<string> Names(Type t) => t.GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

    [Fact]
    public void UnitWatch_fails_when_a_default_differs()
    {
        Assert.Empty(UnitWatchProblems(SettingsText, Limits.All));
        Assert.Equal(new WatchSettings(true, 90, 10), WatchSettings.Default);
        Assert.Equal(2, UnitWatch.Tries);
        Assert.Equal(["Enabled is not bound with default true"],
            UnitWatchProblems(SettingsText.Replace("\"Enabled\", true", "\"Enabled\", false", StringComparison.Ordinal), Limits.All));
        var off = Limits.All.Select(l => l == Limits.StuckSeconds ? l with { Default = 60 } : l).ToList();
        Assert.Equal(["StuckSeconds is 60 (30-600)"], UnitWatchProblems(SettingsText, off));
        Assert.Equal(["StallMinutes is not bound through BindLimit"],
            UnitWatchProblems(SettingsText.Replace("Limits.StallMinutes", "Limits.Stall", StringComparison.Ordinal), Limits.All));
    }

    [Fact]
    public void UnitWatch_fails_when_an_out_of_range_value_is_taken()
    {
        Assert.Equal((30, "UnitWatch.StuckSeconds=29 is below 30; using 30"), Limits.StuckSeconds.Clamp(29));
        Assert.Equal((600, "UnitWatch.StuckSeconds=601 is above its ceiling 600; using 600"), Limits.StuckSeconds.Clamp(601));
        Assert.Equal((60, "UnitWatch.StallMinutes=61 is above its ceiling 60; using 60"), Limits.StallMinutes.Clamp(61));
        Assert.Equal((0, "UnitWatch.StallMinutes=-1 is below 0; using 0"), Limits.StallMinutes.Clamp(-1));
    }

    [Fact]
    public void UnitWatch_fails_when_stall_zero_leaves_the_backstop_on()
    {
        Assert.Equal((0, (string?)null), Limits.StallMinutes.Clamp(0));
        var r = new WatchRig { S = WatchSettings.Default with { StallMinutes = 0 } };
        var a = Watch.Run(Watch.Waves(list: Watch.Two()), WatchRig.T0);
        a.Record(WatchRig.T0, fought: true);
        a.WavesSpawned = 1;
        r.Active.Add(a);
        r.W.Add("uw", WatchRig.T0.AddSeconds(1), 300, 500);
        r.For(3600);
        Assert.Empty(r.Log);
        r.S = WatchSettings.Default;                               // the default 10 min fires
        r.For(605);
        Assert.Contains("unit-watch uw stall: wave 1 quiet 10 min, 1 leftovers marked", r.Log);
    }

    [Fact]
    public void UnitWatch_fails_when_a_definition_or_state_gains_a_property()
    {
        Assert.Equal(["Action", "Announce", "Apocalypse", "Conditions", "DisabledReason", "DurationSeconds", "Empower", "Enabled", "Id", "Name", "Pillar",
            "Startable", "Trigger"], Names(typeof(EventDefinition)));
        Assert.Equal(["AllowTerritory", "Arena", "Behaviour", "Bosses", "FanOut", "Growth", "IntervalSeconds", "Location", "Loot", "Modifiers", "Radius",
            "RegionLock", "Scope", "Scoreboard", "UnitLifetimeSeconds", "Units", "WaveList", "Waves", "Winner"], Names(typeof(SpawnWavesAction)));
        Assert.Equal(["AfterSeconds", "Units", "WaitsForClear", "WhenCleared"], Names(typeof(WaveSpec)));
        Assert.Equal(["Bosses", "Faction", "Goal", "Hordes", "Hotspots", "MarchSpeed", "Roamers", "Scope", "Scoreboard", "Units"], Names(typeof(ApocalypseAction)));
        Assert.Equal(["Apocalypse", "DailyBanner", "Instances", "LastFired", "LastStart", "NextInterval", "PurgeUntilUtc", "SchemaVersion", "Units"],
            Names(typeof(StateDocument)));
    }

    [Fact]
    public void UnitWatch_passes_cfg_keys_as_planned()
    {
        Assert.Empty(UnitWatchProblems(SettingsText, Limits.All));
        Assert.Contains(Limits.StuckSeconds, Limits.All);
        Assert.Contains(Limits.StallMinutes, Limits.All);
    }

    [Fact]
    public void UnitWatch_empty_in_range_values_pass_silently()
    {
        foreach (var v in new[] { 30, 90, 600 }) Assert.Null(Limits.StuckSeconds.Clamp(v).Log);
        foreach (var v in new[] { 0, 10, 60 }) Assert.Null(Limits.StallMinutes.Clamp(v).Log);
        foreach (var v in new[] { 5, 10, 60 }) Assert.Null(Limits.SpawnStillSeconds.Clamp(v).Log);
        Assert.Equal(4, UnitWatchProblems("", Limits.All).Count);   // Enabled and the three BindLimit lines missing
    }
}
