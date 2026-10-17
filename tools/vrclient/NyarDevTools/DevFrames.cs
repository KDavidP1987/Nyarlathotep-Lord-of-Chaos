using System.Collections;
using System.Diagnostics;
using System.Globalization;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using ProjectM.Physics;
using UnityEngine;
using VampireCommandFramework;

namespace NyarDevTools;

// DEV-ONLY (tools/vrclient), undead-hordes D2: the server's frame meter for the load ladder. `.devframes on` starts a
// coroutine that steps once per server frame and times the interval between frames (ServerFps 30 paces an idle server
// at about 33.3 ms); once a minute it logs
//   [DEV FRAME] avg=<ms> p95=<ms> max=<ms> frames=<n> mem=<MB>
// with mem the process working set, and "[DEV FRAME] long=<ms> ms" right after any frame of 1 s or more (Session 0: one
// frame of 10 s at nightfall with 250 units, cause unknown). `.devframes off` stops it and logs the partial window. `.devnote` marks a
// scenario's phases in the same log. Never in the mod.
static class FrameMeter
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    public const double LongFrameMs = 1000;

    static MonoBehaviour _host;
    static Coroutine _run;
    static readonly List<double> _intervals = new(4096);
    static DateTime _since;

    static MonoBehaviour Host
    {
        get
        {
            if (_host == null)
            {
                _host = new GameObject("NyarDevFrames").AddComponent<IgnorePhysicsDebugSystem>();
                UnityEngine.Object.DontDestroyOnLoad(_host.gameObject);
            }
            return _host;
        }
    }

    public static bool On => _run != null;

    public static void Start()
    {
        if (_run != null) return;
        _intervals.Clear();
        _since = DateTime.UtcNow;
        _run = Host.StartCoroutine(Run().WrapToIl2Cpp());
        Plugin.Logger.LogInfo("[DEV FRAME] on");
    }

    public static void Stop()
    {
        if (_run == null) return;
        if (_host != null) _host.StopCoroutine(_run);
        _run = null;
        Flush();
        Plugin.Logger.LogInfo("[DEV FRAME] off");
    }

    static IEnumerator Run()
    {
        var last = Stopwatch.GetTimestamp();
        while (true)
        {
            yield return null;
            var now = Stopwatch.GetTimestamp();
            var ms = (now - last) * 1000.0 / Stopwatch.Frequency;
            _intervals.Add(ms);
            last = now;
            // a frame of a second or more is logged when it ends, so the lines just before it show what ran in it
            if (ms >= LongFrameMs)
                Plugin.Logger.LogInfo(string.Create(CultureInfo.InvariantCulture, $"[DEV FRAME] long={ms:0} ms"));
            if (DateTime.UtcNow - _since >= Window) Flush();
        }
    }

    static void Flush()
    {
        if (_intervals.Count > 0)
        {
            var sorted = _intervals.OrderBy(x => x).ToList();
            var p95 = sorted[Math.Min(sorted.Count - 1, (int)Math.Ceiling(sorted.Count * 0.95) - 1)];
            var mem = Process.GetCurrentProcess().WorkingSet64 / (1024.0 * 1024.0);
            Plugin.Logger.LogInfo(string.Create(CultureInfo.InvariantCulture,
                $"[DEV FRAME] avg={sorted.Average():0.0} p95={p95:0.0} max={sorted[^1]:0.0} frames={sorted.Count} mem={mem:0}"));
        }
        _intervals.Clear();
        _since = DateTime.UtcNow;
    }
}

static class FrameCommands
{
    [Command("devframes", adminOnly: true)]
    public static void DevFrames(ChatCommandContext ctx, string state)
    {
        switch (state.ToLowerInvariant())
        {
            case "on": FrameMeter.Start(); break;
            case "off": FrameMeter.Stop(); break;
            default: ctx.Reply("devframes: on or off"); return;
        }
        ctx.Reply($"devframes: {(FrameMeter.On ? "on" : "off")}");
    }

    // `.devnote <word> [word…]` logs "[DEV NOTE] <words>": the logs carry no timestamps, so a scenario marks its phases
    // (load-ladder.py reads "step=<n> phase=<p> [s=<seconds>]") and its results in the log the report reads.
    [Command("devnote", adminOnly: true)]
    public static void DevNote(ChatCommandContext ctx, string a, string b = "", string c = "", string d = "", string e = "")
    {
        var text = string.Join(" ", new[] { a, b, c, d, e }.Where(w => w.Length > 0));
        Plugin.Logger.LogInfo($"[DEV NOTE] {text}");
        ctx.Reply($"devnote: {text}");
    }
}
