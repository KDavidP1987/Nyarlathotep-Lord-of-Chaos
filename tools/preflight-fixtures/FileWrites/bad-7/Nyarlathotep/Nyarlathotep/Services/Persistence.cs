using System.IO;
using BepInEx;

namespace Nyarlathotep.Services;

internal static class Persistence
{
    const string EventsFile = "events.json";
    const string ZonesFile = "zones.json";
    const string StateFile = "state.json";

    static string Folder => Path.Combine(Paths.ConfigPath, "Nyarlathotep");

    public static void SaveEvents(string json) => WriteAtomic(EventsFile, json);
    public static void SaveZones(string json) => WriteAtomic(ZonesFile, json);
    public static void SaveState(string json) => WriteAtomic(StateFile, json);

    static void WriteAtomic(string fileName, string json)
    {
        Directory.CreateDirectory(Folder);
        var target = Path.Combine(Folder, fileName);
        var tmp = target + ".tmp";
        File.WriteAllText(fileName + ".tmp", json);
        if (File.Exists(target)) File.Replace(tmp, target, target + ".bak");
        else File.Move(tmp, target);
    }
}
