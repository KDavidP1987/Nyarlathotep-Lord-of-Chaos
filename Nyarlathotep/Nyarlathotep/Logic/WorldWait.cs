#nullable enable
using System.Collections.Generic;

namespace Nyarlathotep.Logic;

/// <summary>What Core.TryInitialize does with the world data it found (Epic A42): a brand-new world fires ServerStartupPatch
/// before its DayNightCycle singleton and region polygons exist, and a service built then stays degraded for the whole
/// session. So init waits for both, retrying each second, and after <see cref="LimitSeconds"/> starts without them.</summary>
public enum WorldWaitStep { Init, Wait, InitWithout }

public static class WorldWait
{
    public const int LimitSeconds = 120;

    /// <summary>Both present → Init; one missing → Wait until <see cref="LimitSeconds"/> have passed since the first attempt
    /// that found it missing, then InitWithout.</summary>
    public static WorldWaitStep Decide(bool dayNight, bool regions, double waitedSeconds) =>
        dayNight && regions ? WorldWaitStep.Init
        : waitedSeconds >= LimitSeconds ? WorldWaitStep.InitWithout
        : WorldWaitStep.Wait;

    /// <summary>"the day/night cycle, the region polygons": what is missing, for the log line; empty when nothing is.</summary>
    public static string Missing(bool dayNight, bool regions)
    {
        var parts = new List<string>(2);
        if (!dayNight) parts.Add("the day/night cycle");
        if (!regions) parts.Add("the region polygons");
        return string.Join(", ", parts);
    }
}
