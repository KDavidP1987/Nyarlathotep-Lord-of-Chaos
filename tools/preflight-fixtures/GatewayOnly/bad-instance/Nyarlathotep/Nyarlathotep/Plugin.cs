namespace Nyarlathotep;

/// <summary>Synthetic fixture (event-spawns D22): the plugin entry point calls no [Mutating] method.</summary>
public class Plugin
{
    public void Load() => Core.TryInitialize("Load");
}
