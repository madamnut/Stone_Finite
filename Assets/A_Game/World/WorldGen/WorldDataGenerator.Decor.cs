using System;
using System.Collections.Generic;
using UnityEngine;


namespace Game.World
{
    public static partial class WorldDataGenerator
    {
        private static void PlaceTrees(WorldGenSettings s, int seed, int desertStartX, int snowEndX, ushort[,] commonSolid, ushort[,] commonMeta)
        {
            int w = commonSolid.GetLength(0), h = commonSolid.GetLength(1);
            var rand = new System.Random(seed);
    
            var tpl = StructureLoader.Load("Tree");
    
            for (int x = 0; x < w; x++)
            {
                bool treeRoll = (rand.NextDouble() <= s.treeDensity);
    
                int y = h - 1;
                while (y > 0 && commonSolid[x, y] == ID_AIR) y--;
    
                // Snow: FrozenGrass ??FrozenTrunk
                if (IsInSnowBiome(s, x, w, snowEndX))
                {
                    ushort ground = commonSolid[x, y];
                    if (ground >= ID_FROZEN_GRASS_TOP && ground <= ID_FROZEN_GRASS_TOPLEFTRIGHT)
                    {
                        if (!treeRoll) continue;
                        if (!IsOpenSky(x, y, commonSolid)) continue;
                        TryPlaceFrozenTrunk(rand, x, y, commonSolid, commonMeta);
                    }
                    continue;
                }
    
                // Desert: cactus/agave (??Volcano 구간 ?�외)
                if (x >= desertStartX && !IsInVolcanoBiome(s, x, w))
                {
                    if (!treeRoll) continue;
                    if (commonSolid[x, y] != ID_SAND) continue;
                    if (!IsOpenSky(x, y, commonSolid)) continue;
    
                    if (rand.NextDouble() < 0.5) TryPlaceCactus(rand, x, y, commonSolid, commonMeta);
                    else TryPlaceAgave(rand, x, y, commonSolid, commonMeta);
                    continue;
                }
    
                // Warm: tree template
                if (!treeRoll) continue;
    
                if (tpl == null || tpl.layers == null || tpl.layers.deco == null) continue;
                var deco = tpl.layers.deco;
                int tplH = deco.Length; if (tplH == 0) continue;
                int tplW = deco[0].Length;
                int ax = tpl.anchor.x, ay = tpl.anchor.y;
    
                ushort groundWarm = commonSolid[x, y];
                if (groundWarm < ID_GRASS_TOP || groundWarm > ID_GRASS_TOPLEFTRIGHT) continue;
    
                int seedY = y + 1;
                int worldOx = x - ax;
                int worldOy = seedY - ay;
    
                for (int ty = 0; ty < tplH; ty++)
                {
                    int ly = tplH - 1 - ty;
                    for (int tx = 0; tx < tplW; tx++)
                    {
                        int id = deco[ty][tx];
                        if (id < 0) continue;
    
                        int wx = worldOx + tx;
                        int wy = worldOy + ly;
                        if ((uint)wx >= (uint)w || (uint)wy >= (uint)h) continue;
    
                        int curr = commonSolid[wx, wy];
                        bool canWrite;
    
                        if (tpl.writeRules != null && tpl.writeRules.TryGetValue(id, out var rule) && rule?.targets != null)
                        {
                            canWrite = false;
                            for (int k = 0; k < rule.targets.Length; k++)
                            {
                                if (curr == rule.targets[k]) { canWrite = true; break; }
                            }
                        }
                        else
                        {
                            canWrite = (curr == ID_AIR);
                        }
    
                        if (canWrite)
                        {
                            commonSolid[wx, wy] = (ushort)id;
                            commonMeta[wx, wy]  = 0;
                        }
                    }
                }
            }
        }
    
        private static void TryPlaceFrozenTrunk(System.Random rand, int x, int groundY, ushort[,] solid, ushort[,] meta)
        {
            int h = solid.GetLength(1);
    
            int height = rand.Next(4, 8); // 4~7
            int startY = groundY + 1;
    
            for (int i = 0; i < height; i++)
            {
                int y = startY + i;
                if ((uint)y >= (uint)h) return;
                if (solid[x, y] != ID_AIR) return;
            }
    
            for (int i = 0; i < height; i++)
            {
                int y = startY + i;
                solid[x, y] = ID_FROZEN_TRUNK;
                meta[x, y]  = 0;
            }
    
            int topY = startY + (height - 1);
            int snowY = topY + 1;
            if ((uint)snowY < (uint)h && solid[x, snowY] == ID_AIR)
            {
                solid[x, snowY] = ID_SNOW;
                meta[x, snowY]  = 0;
            }
        }
    
        private static void TryPlaceCactus(System.Random rand, int x, int groundY, ushort[,] solid, ushort[,] meta)
        {
            int h = solid.GetLength(1);
    
            int height = rand.Next(3, 8);
            int startY = groundY + 1;
    
            for (int i = 0; i < height; i++)
            {
                int y = startY + i;
                if ((uint)y >= (uint)h) return;
                if (solid[x, y] != ID_AIR) return;
            }
    
            for (int i = 0; i < height; i++)
            {
                int y = startY + i;
                solid[x, y] = ID_CACTUS;
                meta[x, y]  = 0;
            }
        }
    
        private static void TryPlaceAgave(System.Random rand, int centerX, int groundY, ushort[,] solid, ushort[,] meta)
        {
            int w = solid.GetLength(0);
            int h = solid.GetLength(1);
    
            int leftX = centerX - 1;
            int bottomY = groundY + 1;
    
            if (leftX < 0 || leftX + 2 >= w) return;
            if (bottomY < 0 || bottomY + 1 >= h) return;
    
            for (int dy = 0; dy < 2; dy++)
            for (int dx = 0; dx < 3; dx++)
            {
                int x = leftX + dx;
                int y = bottomY + dy;
                if (solid[x, y] != ID_AIR) return;
            }
    
            ushort[,] tile = new ushort[2, 3]
            {
                { ID_AGAVE_0, ID_AGAVE_1, ID_AGAVE_2 },
                { ID_AGAVE_3, ID_AGAVE_4, ID_AGAVE_5 },
            };
    
            for (int dy = 0; dy < 2; dy++)
            for (int dx = 0; dx < 3; dx++)
            {
                int x = leftX + dx;
                int y = bottomY + dy;
                solid[x, y] = tile[dy, dx];
                meta[x, y]  = 0;
            }
        }
    
        private static void PlaceDecorAfterTrees(WorldGenSettings s, int seed, int desertStartX, int snowEndX, ushort[,] commonSolid, ushort[,] commonMeta)
        {
            int w = commonSolid.GetLength(0), h = commonSolid.GetLength(1);
            var rand = new System.Random(seed ^ SALT_DECOR);
    
            for (int x = 1; x < w - 1; x++)
            for (int y = 1; y < h - 1; y++)
            {
                ushort here = commonSolid[x, y];
                int ya = y + 1;
                if (commonSolid[x, ya] != ID_AIR) continue;
    
                if (IsInSnowBiome(s, x, w, snowEndX))
                {
                    if (!IsOpenSky(x, y, commonSolid)) continue;
    
                    if (here == ID_ROCK || here == ID_ICE_CELL)
                    {
                        commonSolid[x, ya] = ID_SNOW_CELL;
                        commonMeta[x, ya]  = 0;
                        continue;
                    }
    
                    if (here >= ID_FROZEN_GRASS_TOP && here <= ID_FROZEN_GRASS_TOPLEFTRIGHT)
                    {
                        bool placed = false;
    
                        if (rand.NextDouble() < 0.10)
                        {
                            commonSolid[x, ya] = ID_FROZEN_PLANT;
                            commonMeta[x, ya]  = 0;
                            placed = true;
                        }
                        else if (rand.NextDouble() < 0.10)
                        {
                            commonSolid[x, ya] = ID_FROZEN_BUSH;
                            commonMeta[x, ya]  = 0;
                            placed = true;
                        }
    
                        if (!placed)
                        {
                            commonSolid[x, ya] = ID_SNOW;
                            commonMeta[x, ya]  = 0;
                        }
                    }
    
                    continue;
                }
    
                // Desert decor (??Volcano 구간 ?�외)
                if (x >= desertStartX && !IsInVolcanoBiome(s, x, w))
                {
                    if (here != ID_SAND) continue;
                    if (!IsOpenSky(x, y, commonSolid)) continue;
    
                    if (rand.NextDouble() < 0.10)
                    {
                        commonSolid[x, ya] = ID_DEAD_BUSH;
                        commonMeta[x, ya]  = 0;
                    }
                    if (rand.NextDouble() < 0.10)
                    {
                        commonSolid[x, ya] = ID_STONE_PILE;
                        commonMeta[x, ya]  = 0;
                    }
                    if (rand.NextDouble() < 0.10)
                    {
                        commonSolid[x, ya] = ID_SMALL_STONE_PILE;
                        commonMeta[x, ya]  = 0;
                    }
                    continue;
                }
    
                if (here >= ID_GRASS_TOP && here <= ID_GRASS_TOPLEFTRIGHT)
                {
                    // ===== Flax (30%) =====
                    int yb = y + 2;
                    if (yb < h &&
                        commonSolid[x, ya] == ID_AIR &&
                        commonSolid[x, yb] == ID_AIR &&
                        rand.NextDouble() < 0.30)
                    {
                        commonSolid[x, ya] = ID_FLAX_BOTTOM; // 2021
                        commonMeta[x, ya]  = 0;
    
                        commonSolid[x, yb] = ID_FLAX_TOP;    // 2020
                        commonMeta[x, yb]  = 0;
                        continue;
                    }
    
                    // ===== Single-tile decor =====
                    double r = rand.NextDouble();
    
                    if (r < 0.30)
                    {
                        commonSolid[x, ya] = ID_PLANT;       // 30%
                        commonMeta[x, ya]  = 0;
                    }
                    else if (r < 0.45)
                    {
                        commonSolid[x, ya] = ID_BUSH;        // 15%
                        commonMeta[x, ya]  = 0;
                    }
                    else if (r < 0.55)
                    {
                        commonSolid[x, ya] = ID_SMALL_STONE_PILE; // 10%
                        commonMeta[x, ya]  = 0;
                    }
                    // else: 45% �??��? 45%? ???�제로는
                    // flax ?�패??70% 중에??
                    // 30 + 15 + 10 = 55%, ?�머지 15%??공백
    
                    continue;
                }
    
                if (here == ID_ROCK)
                {
                    if (rand.NextDouble() < 0.20)
                    {
                        commonSolid[x, ya] = ID_STONE_PILE;
                        commonMeta[x, ya]  = 0;
                    }
                }
            }
        }
    
    }
}
