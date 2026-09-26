using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>faction-empowerment D13: the VBloodKilled trigger reaches an Empower definition, and one kill delivered twice
/// within 5 s starts it once (foundation TriggerDedupe).</summary>
public partial class TriggerActivationTests
{
    [Fact]
    public void A_V_Blood_kill_seen_twice_starts_the_Empower_event_once()
    {
        var catalog = new EventCatalog();
        Assert.Null(catalog.Reload(EventValidator.Parse(Json.File(
            Json.Empower("surge", Cases[TriggerType.VBloodKilled].Trigger)), FakeUnits.Default()), FileStamp.Of(Now, [1])));
        var engine = new EventEngine(catalog);
        var dedupe = new TriggerDedupe();
        var controls = new ControlState(false, true, new HashSet<Pillar> { Pillar.Empowerment }, 0, 3);
        var starts = 0;
        foreach (var seen in new[] { Now, Now.AddSeconds(4.9) })
        {
            if (!DeathRule.IsVBloodKill(hasConsumeSource: true, hasVBloodUnit: true) || !dedupe.ShouldFire("boss-entity-42", seen)) continue;
            foreach (var d in TriggerRouter.VBloodKilled(catalog.Current, "CHAR_Bandit_Tourok_VBlood"))
                if (engine.Start(d.Id, "vbloodkilled", seen, controls) is null) starts++;
        }
        Assert.Equal(1, starts);
        Assert.Single(engine.Active);
    }
}
