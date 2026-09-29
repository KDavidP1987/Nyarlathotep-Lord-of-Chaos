namespace Nyarlathotep;

/// <summary>Planted (event-spawns D22, A2): the plugin entry point calls a [Mutating] method outside Gateway.Run.</summary>
public class Plugin
{
    public void Load()
    {
        Core.TryInitialize("Load");
        Services.EventStore.Reload();
    }
}
