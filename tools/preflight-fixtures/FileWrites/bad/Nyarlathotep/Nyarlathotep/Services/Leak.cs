using System.IO;

namespace Nyarlathotep.Services;

internal static class Leak
{
    public static void Run(string p) => File.WriteAllText(p, "planted");
}
