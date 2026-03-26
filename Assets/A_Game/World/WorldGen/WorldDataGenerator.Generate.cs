


using UnityEngine;

using Game.Core;
using Game.Data;

namespace Game.World
{
    public static partial class WorldDataGenerator
    {
        
        public static WorldData Generate(WorldGenSettings s, int seed, CellLibrary cellLibrary)
        {

            int w = s.width;
            int h = s.height;

            float totalStart = Time.realtimeSinceStartup;
            float t0 = totalStart;

            Debug.Log($"[WorldGen] START Generate w={w} h={h} seed={seed} waterHeight={s.waterHeight}");

            BuildCommonAndBg(s, seed, out var commonSolid, out var commonMeta, out var bg, out var commonFluid);
            StepLog("BuildCommonAndBg", t0, totalStart);
            t0 = Time.realtimeSinceStartup;

            var world = new WorldData(w, h);
            StepLog("Create WorldData arrays", t0, totalStart);
            t0 = Time.realtimeSinceStartup;

            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                ushort bgId = bg[x, y];
                if (bgId != ID_AIR)
                    world.SetBG(x, y, bgId);
            }
            StepLog("Inject BG", t0, totalStart);
            t0 = Time.realtimeSinceStartup;

            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                ushort id = commonSolid[x, y];
                if (id == ID_AIR) continue;

                ushort meta = commonMeta[x, y];
                world.SetSolid(x, y, id, meta);
            }
            StepLog("Inject Solid", t0, totalStart);
            t0 = Time.realtimeSinceStartup;

            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                ushort fid = commonFluid[x, y];
                if (fid == FLUID_NONE) continue;

                world.SetFluid(x, y, fid, WorldData.MaxFluid);
            }
            StepLog("Inject Fluid", t0, totalStart);
            t0 = Time.realtimeSinceStartup;

            PropagateNaturalLight(world, cellLibrary);
            StepLog("PropagateNaturalLight", t0, totalStart);

            float totalEnd = Time.realtimeSinceStartup;
            Debug.Log($"[WorldGen] END Generate TOTAL: {(totalEnd - totalStart) * 1000f:F1} ms");

            return world;
        }

        
        public static ushort[,] GenerateCommonSolid(WorldGenSettings s, int seed, out ushort[,] bg, out ushort[,] commonFluid)
        {
            BuildCommonAndBg(s, seed, out var commonSolid, out _, out bg, out commonFluid);
            return commonSolid;
        }
    }
}
