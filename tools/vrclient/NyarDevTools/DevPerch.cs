using System.Globalization;
using ProjectM;
using ProjectM.Tiles;
using Unity.Mathematics;
using VampireCommandFramework;

namespace NyarDevTools;

// DEV-ONLY (tools/vrclient), unit-watch Session 1: finds a "rock top" for the standing rule's scenario without picking it by
// eye. `.devperch [min] [max] [rise]` scans a ring min-max metres (default 15-30) around the sender every metre for a
// spot that is free and grounded at a height level at least rise levels (default 3) above the sender's, with no free
// ground at the sender's level there (rock, not a slope above open ground); it logs the highest such spot:
//   [DEV PERCH] found=<0|1> x=<x> y=<y> z=<z> level=<n> rise=<levels> d=<m>
// A spider put there can usually not walk down to the sender; the scenario checks it by the standing mark itself.
static class PerchCommands
{
    static string F(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);

    [Command("devperch", adminOnly: true)]
    public static void DevPerch(ChatCommandContext ctx, float min = 15f, float max = 30f, int rise = 3)
    {
        var me = DevCommands.Where(ctx);
        if (me is null) { ctx.Reply("devperch: no position"); return; }
        var em = DevCommands.Em;
        if (Ground.Live(em) is not { } world) { ctx.Reply("devperch: no walk data"); return; }
        var baseLevel = TileLayerUtility.GetHeightLevel(me.Value.y);
        var polygons = TileCollisionHelper.CreateLinePolygon();
        float3 best = default;
        int bestLevel = -1, bestRise = 0;
        var bestD = 0f;
        int scanned = 0, open = 0, maxSeen = -1;
        try
        {
            var map = TileCollisionHelper.CreateMapData(polygons, world);
            bool Walk(float x, float z, byte l) =>
                !TileMapCollisionMath.CheckStaticCircle(ref map, new float2(x, z), l, Ground.UnitRadius, MapCollisionFlags.CollideNormalMovement)
                && world.GetIsGrounded(new int2(Ground.TileIndex(x), Ground.TileIndex(z)), l);
            for (var dx = -max; dx <= max; dx += 1f)
            for (var dz = -max; dz <= max; dz += 1f)
            {
                var d = math.sqrt(dx * dx + dz * dz);
                if (d < min || d > max) continue;
                var x = me.Value.x + dx;
                var z = me.Value.z + dz;
                scanned++;
                var openHere = Walk(x, z, baseLevel) || Walk(x, z, (byte)Math.Max(0, baseLevel - 1)) || Walk(x, z, (byte)Math.Min(255, baseLevel + 1));
                if (openHere) open++;
                for (var l = Math.Min(255, baseLevel + 40); l >= Math.Max(0, baseLevel - 10); l--)
                {
                    if (!Walk(x, z, (byte)l)) continue;
                    if (l > maxSeen) maxSeen = l;
                    if (openHere || l < baseLevel + rise) break;              // open ground at the sender's level: not a rock
                    if (l > bestLevel) { bestLevel = l; best = new float3(x, TileLayerUtility.GetHeightForLevel((byte)l), z); bestRise = l - baseLevel; bestD = d; }
                    break;
                }
            }
        }
        finally { polygons.Dispose(); }
        Plugin.Logger.LogInfo($"[DEV PERCH] scan base={baseLevel} y={F(me.Value.y)} scanned={scanned} open={open} maxlevel={maxSeen}");
        if (bestLevel < 0)
        {
            Plugin.Logger.LogInfo("[DEV PERCH] found=0");
            ctx.Reply("devperch found=0");
            return;
        }
        Plugin.Logger.LogInfo($"[DEV PERCH] found=1 x={F(best.x)} y={F(best.y)} z={F(best.z)} level={bestLevel} rise={bestRise} d={F(bestD)}");
        ctx.Reply($"devperch found=1 rise={bestRise} d={F(bestD)}");
    }
}
