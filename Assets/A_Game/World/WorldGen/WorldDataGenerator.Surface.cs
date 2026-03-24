using System;
using System.Collections.Generic;
using UnityEngine;


namespace Game.World
{
    public static partial class WorldDataGenerator
    {
        private static void ApplyDesertPass(WorldGenSettings s, int seed, int desertStartX, ushort[,] commonSolid, ushort[,] commonMeta, ushort[,] commonFluid)
        {
            int w = commonSolid.GetLength(0);
            int h = commonSolid.GetLength(1);
    
            int transLen = Mathf.Max(0, s.desertTransitionLen);
            float transChance = Mathf.Clamp01(s.desertTransitionChance);
    
            int transStartX = desertStartX - transLen;
    
            var rand = new System.Random(seed ^ SALT_DESERT_PASS);
    
            const int R = 2;
    
            for (int x = 0; x < w; x++)
            {
                // ??Volcano Íµ¨Í∞Ñ?êÏÑú???¨Îßâ ?®Ïä§ Í∏àÏ?
                if (IsInVolcanoBiome(s, x, w)) continue;
    
                bool inCore = (x >= desertStartX);
                bool inTrans = (!inCore && x >= transStartX && x < desertStartX);
                if (!inCore && !inTrans) continue;
    
                float chance = inCore ? 1f : transChance;
    
                for (int y = 0; y < h; y++)
                {
                    ushort id = commonSolid[x, y];
    
                    if (id == ID_CLAY) continue;
                    if (id == ID_SANDSTONE_BRICK) continue;
    
                    if (id == ID_DIRT)
                    {
                        if (rand.NextDouble() > chance) continue;
    
                        bool nearRock = HasNeighborWithinR(commonSolid, w, h, x, y, R, ID_ROCK);
                        if (nearRock)
                        {
                            commonSolid[x, y] = ID_SANDSTONE;
                            commonMeta[x, y]  = 0;
                        }
                        else
                        {
                            commonSolid[x, y] = ID_SAND;
                            commonMeta[x, y]  = 0;
                        }
                        continue;
                    }
    
                    if (id == ID_ROCK)
                    {
                        if (rand.NextDouble() > chance) continue;
    
                        bool nearSand = HasNeighborWithinR(commonSolid, w, h, x, y, R, ID_SAND);
                        bool nearClay = HasNeighborWithinR(commonSolid, w, h, x, y, R, ID_CLAY);
                        if (nearSand || nearClay)
                        {
                            commonSolid[x, y] = ID_SANDSTONE;
                            commonMeta[x, y]  = 0;
                        }
                        continue;
                    }
                }
            }
        }
    
        private static bool HasNeighborWithinR(ushort[,] solid, int w, int h, int cx, int cy, int r, ushort target)
        {
            int x0 = cx - r; if (x0 < 0) x0 = 0;
            int x1 = cx + r; if (x1 >= w) x1 = w - 1;
            int y0 = cy - r; if (y0 < 0) y0 = 0;
            int y1 = cy + r; if (y1 >= h) y1 = h - 1;
    
            for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
            {
                if (x == cx && y == cy) continue;
                if (solid[x, y] == target) return true;
            }
            return false;
        }
    
        private static void SeaColumnFill(ushort[,] commonSolid, ushort[,] commonFluid, int w, int h, int seaLevel, ushort fluidId)
        {
            int y0 = seaLevel;
    
            for (int x = 0; x < w; x++)
            {
                for (int y = y0; y >= 0; y--)
                {
                    if (commonSolid[x, y] != ID_AIR)
                        break;
    
                    // ???¥Î? ?§Î•∏ ?†Ï≤¥(?? lava)Í∞Ä ?àÏúºÎ©?Î∞îÎã§ Î¨ºÏù¥ Í¥Ä????ñ¥?∞Ï? ?äÏùå
                    if (commonFluid[x, y] != FLUID_NONE)
                        break;
    
                    commonFluid[x, y] = fluidId;
                }
            }
        }
    
        private static void FloodFillFluidFromSeaSurface(ushort[,] commonSolid, ushort[,] commonFluid, int w, int h, int seaLevel, ushort fluidId)
        {
            int ySeed = seaLevel;
    
            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };
    
            var visited = new bool[w, h];
            var q = new Queue<(int x, int y)>();
    
            for (int x = 0; x < w; x++)
            {
                if (commonFluid[x, ySeed] == fluidId && commonSolid[x, ySeed] == ID_AIR)
                {
                    visited[x, ySeed] = true;
                    q.Enqueue((x, ySeed));
                }
            }
    
            while (q.Count > 0)
            {
                var (cx, cy) = q.Dequeue();
    
                for (int dir = 0; dir < 4; dir++)
                {
                    int nx = cx + dx[dir];
                    int ny = cy + dy[dir];
    
                    if ((uint)nx >= (uint)w || (uint)ny >= (uint)h) continue;
                    if (ny > seaLevel) continue;
    
                    if (visited[nx, ny]) continue;
                    visited[nx, ny] = true;
    
                    if (commonSolid[nx, ny] != ID_AIR) continue;
    
                    // ??lava???àÎ? Î¨ºÎ°ú Î∞îÍæ∏ÏßÄ ?äÏùå + Î¨??ÑÌåå??Ï∞®Îã®
                    if (commonFluid[nx, ny] == FLUID_LAVA) continue;
    
                    if (commonFluid[nx, ny] == FLUID_NONE)
                        commonFluid[nx, ny] = fluidId;
    
                    if (commonFluid[nx, ny] == fluidId)
                        q.Enqueue((nx, ny));
                }
            }
        }
    
        // ??Lava FloodFill: "?ÑÏû¨ Ï°¥Ïû¨?òÎäî Î™®Îì† lava"Î•?seedÎ°? Ï¢????ÑÎûòÎß??ïÏÇ∞
        private static void FloodFillFluidFromAllExistingCells_3Dir(
            ushort[,] commonSolid, ushort[,] commonFluid,
            int w, int h,
            ushort fluidId
        )
        {
            // Ï¢????ÑÎûò (?ÅÎ∞© ?ÑÌåå Í∏àÏ?)
            int[] dx = { -1, 1, 0 };
            int[] dy = {  0, 0,-1 };
    
            var visited = new bool[w, h];
            var q = new Queue<(int x, int y)>();
    
            // seed: Îß??ÑÏ≤¥?êÏÑú Í∏∞Ï°¥ fluidIdÎ•??ÑÎ? ?êÏóê ?£Ïùå
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                if (commonSolid[x, y] != ID_AIR) continue;
                if (commonFluid[x, y] != fluidId) continue;
    
                visited[x, y] = true;
                q.Enqueue((x, y));
            }
    
            while (q.Count > 0)
            {
                var (cx, cy) = q.Dequeue();
    
                for (int dir = 0; dir < 3; dir++)
                {
                    int nx = cx + dx[dir];
                    int ny = cy + dy[dir];
    
                    if ((uint)nx >= (uint)w || (uint)ny >= (uint)h) continue;
                    if (visited[nx, ny]) continue;
                    visited[nx, ny] = true;
    
                    if (commonSolid[nx, ny] != ID_AIR) continue;
    
                    // ?§Î•∏ ?†Ï≤¥(?? Î¨?Î°úÎäî ?àÎ? Ïπ®Î≤î/??ñ¥?∞Í∏∞ Í∏àÏ?
                    if (commonFluid[nx, ny] != FLUID_NONE && commonFluid[nx, ny] != fluidId) continue;
    
                    if (commonFluid[nx, ny] == FLUID_NONE)
                        commonFluid[nx, ny] = fluidId;
    
                    if (commonFluid[nx, ny] == fluidId)
                        q.Enqueue((nx, ny));
                }
            }
        }
    
        // ?¥Ìïò: ?àÍ? Î∂ôÏó¨Ï§Ä Í∏∞Ï°¥ ÏΩîÎìú Í∑∏Î?Î°?(ApplySandAndGravelAndClay ~ PropagateNaturalLight)
        // -----------------------------------------------------------------------
    
        private static void ApplySandAndGravelAndClay(WorldGenSettings s, int seed, int desertStartX, ushort[,] commonSolid, ushort[,] commonMeta, ushort[,] commonFluid)
        {
            float tStart = Time.realtimeSinceStartup;
    
            int w = commonSolid.GetLength(0), h = commonSolid.GetLength(1);
            var rand = new System.Random(seed ^ SALT_SAND_BFS);
    
            int transLen = Mathf.Max(0, s.desertTransitionLen);
            float transChance = Mathf.Clamp01(s.desertTransitionChance);
            int transStartX = desertStartX - transLen;
    
            const int INF = 1_000_000;
    
            int[] dx8 = { 1,  1,  0, -1, -1, -1,  0,  1 };
            int[] dy8 = { 0,  1,  1,  1,  0, -1, -1, -1 };
    
            var q = new Queue<(int x, int y)>();
    
            int[,] distWater = new int[w, h];
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                distWater[x, y] = INF;
    
            q.Clear();
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                if (commonFluid[x, y] == FLUID_WATER)
                {
                    distWater[x, y] = 0;
                    q.Enqueue((x, y));
                }
            }
    
            int maxWaterR = 3;
            while (q.Count > 0)
            {
                var (cx, cy) = q.Dequeue();
                int cd = distWater[cx, cy];
                if (cd >= maxWaterR) continue;
    
                for (int i = 0; i < 8; i++)
                {
                    int nx = cx + dx8[i];
                    int ny = cy + dy8[i];
                    if ((uint)nx >= (uint)w || (uint)ny >= (uint)h) continue;
    
                    int nd = cd + 1;
                    if (nd < distWater[nx, ny])
                    {
                        distWater[nx, ny] = nd;
                        q.Enqueue((nx, ny));
                    }
                }
            }
    
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                if (commonSolid[x, y] == ID_DIRT)
                {
                    int d = distWater[x, y];
                    if (d > 0 && d <= maxWaterR)
                    {
                        commonSolid[x, y] = ID_SAND;
                        commonMeta[x, y]  = 0;
                    }
                }
            }
    
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                if (commonSolid[x, y] == ID_ROCK)
                {
                    int d = distWater[x, y];
                    if (d > 0 && d <= maxWaterR)
                    {
                        commonSolid[x, y] = ID_GRAVEL;
                        commonMeta[x, y]  = 0;
                    }
                }
            }
    
            int[,] distDirt = new int[w, h];
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                distDirt[x, y] = INF;
    
            q.Clear();
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                if (commonSolid[x, y] == ID_DIRT)
                {
                    distDirt[x, y] = 0;
                    q.Enqueue((x, y));
                }
            }
    
            int maxDirtR = 2;
            while (q.Count > 0)
            {
                var (cx, cy) = q.Dequeue();
                int cd = distDirt[cx, cy];
                if (cd >= maxDirtR) continue;
    
                for (int i = 0; i < 8; i++)
                {
                    int nx = cx + dx8[i];
                    int ny = cy + dy8[i];
                    if ((uint)nx >= (uint)w || (uint)ny >= (uint)h) continue;
    
                    int nd = cd + 1;
                    if (nd < distDirt[nx, ny])
                    {
                        distDirt[nx, ny] = nd;
                        q.Enqueue((nx, ny));
                    }
                }
            }
    
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                if (commonSolid[x, y] == ID_ROCK)
                {
                    int d = distDirt[x, y];
                    if (d > 0 && d <= maxDirtR)
                    {
                        if (rand.NextDouble() < 0.30)
                        {
                            commonSolid[x, y] = ID_GRAVEL;
                            commonMeta[x, y]  = 0;
                        }
                    }
                }
            }
    
            int[,] distSand = new int[w, h];
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                distSand[x, y] = INF;
    
            q.Clear();
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                if (commonSolid[x, y] == ID_SAND)
                {
                    distSand[x, y] = 0;
                    q.Enqueue((x, y));
                }
            }
    
            int maxSandR = 3;
            while (q.Count > 0)
            {
                var (cx, cy) = q.Dequeue();
                int cd = distSand[cx, cy];
                if (cd >= maxSandR) continue;
    
                for (int i = 0; i < 8; i++)
                {
                    int nx = cx + dx8[i];
                    int ny = cy + dy8[i];
                    if ((uint)nx >= (uint)w || (uint)ny >= (uint)h) continue;
    
                    int nd = cd + 1;
                    if (nd < distSand[nx, ny])
                    {
                        distSand[nx, ny] = nd;
                        q.Enqueue((nx, ny));
                    }
                }
            }
    
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                if (commonSolid[x, y] == ID_DIRT)
                {
                    int d = distSand[x, y];
                    if (d > 0 && d <= maxSandR)
                    {
                        double r = rand.NextDouble();
                        if (r < 0.40)
                        {
                            commonSolid[x, y] = ID_GRAVEL;
                            commonMeta[x, y]  = 0;
                        }
                        else if (r < 0.80)
                        {
                            commonSolid[x, y] = ID_CLAY;
                            commonMeta[x, y]  = 0;
                        }
                    }
                }
            }
    
            const int waterNearR = 2;
            for (int x = 0; x < w; x++)
            {
                // ??Volcano Íµ¨Í∞Ñ?Ä ?¨Ïïî???úÏô∏
                if (IsInVolcanoBiome(s, x, w)) continue;
    
                bool inCore = (x >= desertStartX);
                bool inTrans = (!inCore && x >= transStartX && x < desertStartX);
                if (!inCore && !inTrans) continue;
    
                float chance = inCore ? 1f : transChance;
    
                for (int y = 0; y < h; y++)
                {
                    if (commonSolid[x, y] != ID_SAND) continue;
    
                    int dw = distWater[x, y];
                    if (dw > 0 && dw <= waterNearR)
                    {
                        if (rand.NextDouble() <= chance)
                        {
                            commonSolid[x, y] = ID_SANDSTONE;
                            commonMeta[x, y]  = 0;
                        }
                    }
                }
            }
    
            float tEnd = Time.realtimeSinceStartup;
            Debug.Log($"[WorldGen] ApplySandAndGravelAndClay (BFS) TOTAL: {(tEnd - tStart) * 1000f:F1} ms");
        }
    
        private static void ApplyClayClusters(WorldGenSettings s, int seed, ushort[,] commonSolid, ushort[,] commonMeta)
        {
            int w = commonSolid.GetLength(0), h = commonSolid.GetLength(1);
    
            var seeds    = ProceduralUtil.SampleSeedPositions(w, s.clayMinHeight, s.clayMaxHeight, s.claySeedDensity);
            var offsets  = ProceduralUtil.GetNeighborOffsets(s.neighborMode == WorldGenSettings.NeighborMode.EightDir);
            var clusters = ProceduralUtil.GenerateClusters(
                seeds,
                s.clayClusterSizeMean, s.clayClusterSizeStdDev,
                s.clayMaxGrowthFactor, s.clayExpansionProb,
                offsets,
                s.frontierMode == WorldGenSettings.FrontierMode.Random
            );
    
            foreach (var cl in clusters)
            foreach (var p in cl)
            {
                if ((uint)p.x < w && (uint)p.y < h && commonSolid[p.x, p.y] == ID_DIRT)
                {
                    commonSolid[p.x, p.y] = ID_CLAY;
                    commonMeta[p.x, p.y]  = 0;
                }
            }
        }
    
        private static void ApplyOreClusters(WorldGenSettings s, int seed, ushort[,] commonSolid, ushort[,] commonMeta)
        {
            int w = commonSolid.GetLength(0), h = commonSolid.GetLength(1);
    
            void apply(int minH, int maxH, float mean, float std, float den, float exp, float maxf, ushort oreId)
            {
                var seeds    = ProceduralUtil.SampleSeedPositions(w, minH, maxH, den);
                var offsets  = ProceduralUtil.GetNeighborOffsets(s.neighborMode == WorldGenSettings.NeighborMode.EightDir);
                var clusters = ProceduralUtil.GenerateClusters(
                    seeds, mean, std, maxf, exp,
                    offsets,
                    s.frontierMode == WorldGenSettings.FrontierMode.Random
                );
    
                foreach (var cl in clusters)
                foreach (var p in cl)
                {
                    if ((uint)p.x < w && (uint)p.y < h && commonSolid[p.x, p.y] == ID_ROCK)
                    {
                        commonSolid[p.x, p.y] = oreId;
                        commonMeta[p.x, p.y]  = 0;
                    }
                }
            }
    
            apply(s.coalMinHeight,   s.coalMaxHeight,   s.coalClusterSizeMean,   s.coalClusterSizeStdDev,
                  s.coalSeedDensity, s.coalExpansionProb, s.coalMaxGrowthFactor, ID_ORE_COAL);
    
            apply(s.tinMinHeight, s.tinMaxHeight, s.tinClusterSizeMean, s.tinClusterSizeStdDev,
                  s.tinSeedDensity, s.tinExpansionProb, s.tinMaxGrowthFactor, ID_ORE_TIN);
    
            apply(s.copperMinHeight, s.copperMaxHeight, s.copperClusterSizeMean, s.copperClusterSizeStdDev,
                  s.copperSeedDensity, s.copperExpansionProb, s.copperMaxGrowthFactor, ID_ORE_COPPER);
    
            apply(s.ironMinHeight,   s.ironMaxHeight,   s.ironClusterSizeMean,   s.ironClusterSizeStdDev,
                  s.ironSeedDensity, s.ironExpansionProb, s.ironMaxGrowthFactor, ID_ORE_IRON);
        }
    
        private static bool TryComputeGrassId(int x, int y, ushort[,] commonSolid, out ushort grassId)
        {
            int w = commonSolid.GetLength(0);
            int h = commonSolid.GetLength(1);
    
            for (int yy = y + 1; yy < h; yy++)
            {
                if (commonSolid[x, yy] != ID_AIR)
                {
                    grassId = 0;
                    return false;
                }
            }
    
            bool up    = (y + 1 < h && commonSolid[x, y + 1] == ID_AIR);
            bool left  = (x - 1 >= 0 && commonSolid[x - 1, y] == ID_AIR);
            bool right = (x + 1 < w && commonSolid[x + 1, y] == ID_AIR);
    
            int mask = (up ? 1 : 0) | (left ? 2 : 0) | (right ? 4 : 0);
    
            switch (mask)
            {
                case 1: grassId = ID_GRASS_TOP;          return true;
                case 2: grassId = ID_GRASS_LEFT;         return true;
                case 3: grassId = ID_GRASS_TOPLEFT;      return true;
                case 4: grassId = ID_GRASS_RIGHT;        return true;
                case 5: grassId = ID_GRASS_TOPRIGHT;     return true;
                case 6: grassId = ID_GRASS_LEFTRIGHT;    return true;
                case 7: grassId = ID_GRASS_TOPLEFTRIGHT; return true;
                default:
                    grassId = 0;
                    return false;
            }
        }
    
        private static bool TryComputeFrozenGrassId(int x, int y, ushort[,] commonSolid, out ushort grassId)
        {
            int w = commonSolid.GetLength(0);
            int h = commonSolid.GetLength(1);
    
            for (int yy = y + 1; yy < h; yy++)
            {
                if (commonSolid[x, yy] != ID_AIR)
                {
                    grassId = 0;
                    return false;
                }
            }
    
            bool up    = (y + 1 < h && commonSolid[x, y + 1] == ID_AIR);
            bool left  = (x - 1 >= 0 && commonSolid[x - 1, y] == ID_AIR);
            bool right = (x + 1 < w && commonSolid[x + 1, y] == ID_AIR);
    
            int mask = (up ? 1 : 0) | (left ? 2 : 0) | (right ? 4 : 0);
    
            switch (mask)
            {
                case 1: grassId = ID_FROZEN_GRASS_TOP;          return true;
                case 2: grassId = ID_FROZEN_GRASS_LEFT;         return true;
                case 3: grassId = ID_FROZEN_GRASS_TOPLEFT;      return true;
                case 4: grassId = ID_FROZEN_GRASS_RIGHT;        return true;
                case 5: grassId = ID_FROZEN_GRASS_TOPRIGHT;     return true;
                case 6: grassId = ID_FROZEN_GRASS_LEFTRIGHT;    return true;
                case 7: grassId = ID_FROZEN_GRASS_TOPLEFTRIGHT; return true;
                default:
                    grassId = 0;
                    return false;
            }
        }
    
        private static bool IsOpenSky(int x, int y, ushort[,] solid)
        {
            int h = solid.GetLength(1);
            for (int yy = y + 1; yy < h; yy++)
            {
                if (solid[x, yy] != ID_AIR) return false;
            }
            return true;
        }
    
        private static bool IsInSnowBiome(WorldGenSettings s, int x, int w, int snowEndX)
        {
            int transLen = Mathf.Max(0, s.snowTransitionLen);
            int transEndX = snowEndX + transLen;
            return (x >= 0 && x <= snowEndX) || (x > snowEndX && x <= transEndX);
        }
    
        private static void ApplySnowPass(WorldGenSettings s, int seed, int snowEndX, ushort[,] solid, ushort[,] meta, ushort[,] fluid)
        {
            int w = solid.GetLength(0);
            int h = solid.GetLength(1);
    
            int transLen = Mathf.Max(0, s.snowTransitionLen);
            float transChance = Mathf.Clamp01(s.snowTransitionChance);
            int transEndX = snowEndX + transLen;
    
            var rand = new System.Random(seed ^ SALT_SNOW_PASS);
    
            // (1) Dirt -> Frozen Dirt
            for (int x = 0; x < w; x++)
            {
                bool inCore = (x >= 0 && x <= snowEndX);
                bool inTrans = (!inCore && x > snowEndX && x <= transEndX);
                if (!inCore && !inTrans) continue;
    
                float chance = inCore ? 1f : transChance;
    
                for (int y = 0; y < h; y++)
                {
                    if (solid[x, y] != ID_DIRT) continue;
                    if (rand.NextDouble() > chance) continue;
    
                    solid[x, y] = ID_FROZEN_DIRT;
                    meta[x, y]  = 0;
                }
            }
    
            // (2) Rock -> Ice (near Frozen Dirt or Dirt within R=2)
            const int R = 2;
            for (int x = 0; x < w; x++)
            {
                bool inCore = (x >= 0 && x <= snowEndX);
                bool inTrans = (!inCore && x > snowEndX && x <= transEndX);
                if (!inCore && !inTrans) continue;
    
                float chance = inCore ? 1f : transChance;
    
                for (int y = 0; y < h; y++)
                {
                    if (solid[x, y] != ID_ROCK) continue;
                    if (rand.NextDouble() > chance) continue;
    
                    bool nearFrozen = HasNeighborWithinR(solid, w, h, x, y, R, ID_FROZEN_DIRT);
                    bool nearDirt   = HasNeighborWithinR(solid, w, h, x, y, R, ID_DIRT);
    
                    if (nearFrozen || nearDirt)
                    {
                        solid[x, y] = ID_ICE_CELL;
                        meta[x, y]  = 0;
                    }
                }
            }
    
            // (3) Freeze water surface: air+water where above is not water
            for (int x = 0; x < w; x++)
            {
                bool inCore = (x >= 0 && x <= snowEndX);
                bool inTrans = (!inCore && x > snowEndX && x <= transEndX);
                if (!inCore && !inTrans) continue;
    
                float chance = inCore ? 1f : transChance;
    
                for (int y = 0; y < h; y++)
                {
                    if (solid[x, y] != ID_AIR) continue;
                    if (fluid[x, y] != FLUID_WATER) continue;
    
                    int ya = y + 1;
                    bool aboveIsWater = (ya < h && solid[x, ya] == ID_AIR && fluid[x, ya] == FLUID_WATER);
                    if (aboveIsWater) continue;
    
                    if (rand.NextDouble() > chance) continue;
    
                    solid[x, y] = ID_ICE_CELL;
                    meta[x, y]  = 0;
                    fluid[x, y] = FLUID_NONE;
                }
            }
        }
    
    }
}
