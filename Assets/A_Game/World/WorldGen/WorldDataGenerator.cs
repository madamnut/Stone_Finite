// WorldDataGenerator.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public static class WorldDataGenerator
{
    // ── Solid IDs (ATT_Solid.json과 일치) ──
    private const ushort ID_AIR  = 0;
    private const ushort ID_ROCK = 1;
    private const ushort ID_DIRT = 2;

    // ✅ Grass 분리: id=3~9, meta=0 고정
    private const ushort ID_GRASS_TOP          = 3;
    private const ushort ID_GRASS_LEFT         = 4;
    private const ushort ID_GRASS_RIGHT        = 5;
    private const ushort ID_GRASS_TOPLEFT      = 6;
    private const ushort ID_GRASS_TOPRIGHT     = 7;
    private const ushort ID_GRASS_LEFTRIGHT    = 8;
    private const ushort ID_GRASS_TOPLEFTRIGHT = 9;

    private const ushort ID_CLAY = 10;
    private const ushort ID_MUD  = 11;

    private const ushort ID_SAND   = 1000;
    private const ushort ID_GRAVEL = 1001;

    private const ushort ID_TRUNK = 2000;
    private const ushort ID_LEAF  = 2001;
    private const ushort ID_PLANT = 2002;
    private const ushort ID_BUSH  = 2003;
    private const ushort ID_STONE_PILE       = 2004;
    private const ushort ID_SMALL_STONE_PILE = 2005;

    // ✅ Desert decor
    private const ushort ID_DEAD_BUSH = 2006;

    // ✅ Agave 3x2 tiles
    private const ushort ID_AGAVE_0 = 2007;
    private const ushort ID_AGAVE_1 = 2008;
    private const ushort ID_AGAVE_2 = 2009;
    private const ushort ID_AGAVE_3 = 2010;
    private const ushort ID_AGAVE_4 = 2011;
    private const ushort ID_AGAVE_5 = 2012;

    private const ushort ID_CACTUS = 2013;

    // ✅ Snow biome decor
    private const ushort ID_SNOW         = 2014;
    private const ushort ID_FROZEN_BUSH  = 2015;
    private const ushort ID_FROZEN_PLANT = 2016;
    private const ushort ID_FROZEN_TRUNK = 2017;

    private const ushort ID_ORE_COAL   = 3000;
    private const ushort ID_ORE_COPPER = 3001;
    private const ushort ID_ORE_IRON   = 3002;
    private const ushort ID_ORE_TIN    = 3003;

    private const ushort ID_GRANITE     = 4000;
    private const ushort ID_AMPHIBOLITE = 4001;

    // ✅ Sandstone + Pyramid brick
    private const ushort ID_SANDSTONE       = 35; // "SandStone"
    private const ushort ID_SANDSTONE_BRICK = 36; // "SandStone Brick Cell"

    // ✅ Snow biome solids
    // NOTE: Frozen Dirt 실제 ID에 맞게 수정 필요 (여기선 46 가정)
    private const ushort ID_FROZEN_DIRT = 46;

    // Frozen Grass 분리: id=37~43
    private const ushort ID_FROZEN_GRASS_TOP          = 37;
    private const ushort ID_FROZEN_GRASS_LEFT         = 38;
    private const ushort ID_FROZEN_GRASS_RIGHT        = 39;
    private const ushort ID_FROZEN_GRASS_TOPLEFT      = 40;
    private const ushort ID_FROZEN_GRASS_TOPRIGHT     = 41;
    private const ushort ID_FROZEN_GRASS_LEFTRIGHT    = 42;
    private const ushort ID_FROZEN_GRASS_TOPLEFTRIGHT = 43;

    private const ushort ID_ICE_CELL  = 44;
    private const ushort ID_SNOW_CELL = 45;

    // ── Fluid IDs (ATT_Fluid.json과 일치) ──
    private const ushort FLUID_NONE  = 0;
    private const ushort FLUID_WATER = 1;

    // ── Light ──
    private const byte NATURAL_MAX = 15;

    // ✅ Seed salt (hex literal은 "숫자"만 가능)
    private const int SALT_DESERT_START = unchecked((int)0x0D35E12);
    private const int SALT_DESERT_PASS  = unchecked((int)0x0D35E12A);
    private const int SALT_SAND_BFS     = unchecked((int)0x0A11CE);
    private const int SALT_DECOR        = unchecked((int)0x00DEC0);

    private const int SALT_SNOW_END     = unchecked((int)0x0510001);  // 임의
    private const int SALT_SNOW_PASS    = unchecked((int)0x0510005A); // 임의

    // 로그 유틸
    private static void StepLog(string label, float stepStart, float totalStart)
    {
        float now = Time.realtimeSinceStartup;
        Debug.Log($"[WorldGen] {label}: {(now - stepStart) * 1000f:F1} ms (total {(now - totalStart) * 1000f:F1} ms)");
    }

    public static WorldData Generate(WorldGenSettings s, int seed, CellLibrary cellLibrary)
    {
        int w = s.width, h = s.height;

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

    private static void BuildCommonAndBg(
        WorldGenSettings s, int seed,
        out ushort[,] commonSolid,
        out ushort[,] commonMeta,
        out ushort[,] bg,
        out ushort[,] commonFluid
    )
    {
        int w = s.width, h = s.height;

        float totalStart = Time.realtimeSinceStartup;
        float t0 = totalStart;

        commonSolid = new ushort[w, h];
        commonMeta  = new ushort[w, h];
        bg          = new ushort[w, h];
        commonFluid = new ushort[w, h];

        int seaLevel = s.waterHeight;

        int desertStartX = ComputeStartX(seed ^ SALT_DESERT_START, s.desertStartMinX, s.desertStartMaxX, w);
        int snowEndX     = ComputeStartX(seed ^ SALT_SNOW_END,     s.snowEndMinX,    s.snowEndMaxX,    w);

        Debug.Log($"[WorldGen] BuildCommonAndBg START w={w} h={h} seed={seed} seaLevel={seaLevel} desertStartX={desertStartX} snowEndX={snowEndX}");

        // Step 1) Noise heights (1D)
        float[] dirtH = new float[w];
        float[] rockH = new float[w];
        float[] granH = new float[w];
        float[] amphH = new float[w];

        for (int x = 0; x < w; x++)
        {
            float sx = x + seed;

            dirtH[x] = ProceduralUtil.FractalPerlin1D(
                sx, s.dirtNoiseBaseFrequency, s.dirtNoiseOctaves,
                s.dirtNoisePersistence, s.dirtNoiseLacunarity,
                s.dirtBaseHeight, s.dirtRange);

            rockH[x] = ProceduralUtil.FractalPerlin1D(
                sx + 10000, s.rockNoiseBaseFrequency, s.rockNoiseOctaves,
                s.rockNoisePersistence, s.rockNoiseLacunarity,
                s.rockBaseHeight, s.rockRange);

            granH[x] = ProceduralUtil.FractalPerlin1D(
                sx + 20000, s.graniteNoiseBaseFrequency, s.graniteNoiseOctaves,
                s.graniteNoisePersistence, s.graniteNoiseLacunarity,
                s.graniteBaseHeight, s.graniteRange);

            amphH[x] = ProceduralUtil.FractalPerlin1D(
                sx + 30000, s.amphibNoiseBaseFrequency, s.amphibNoiseOctaves,
                s.amphibNoisePersistence, s.amphibNoiseLacunarity,
                s.amphibBaseHeight, s.amphibRange);
        }

        StepLog("Step 1 - Noise heights", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 2) Layer fill & BG 확정
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            ushort id = 0;
            if (y < dirtH[x]) id = ID_DIRT;
            if (y < rockH[x]) id = ID_ROCK;
            if (y < granH[x]) id = ID_GRANITE;
            if (y < amphH[x]) id = ID_AMPHIBOLITE;

            if (id != 0)
            {
                commonSolid[x, y] = id;
                commonMeta[x, y]  = 0;
                bg[x, y]          = id;
                commonFluid[x, y] = FLUID_NONE;
            }
        }

        StepLog("Step 2 - Layer fill & BG", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 3) SeaColumnFill
        SeaColumnFill(commonSolid, commonFluid, w, h, seaLevel, FLUID_WATER);
        StepLog($"Step 3 - SeaColumnFill (seaLevel={seaLevel})", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 4) Ores
        ApplyOreClusters(s, seed, commonSolid, commonMeta);
        StepLog("Step 4 - Ore clusters", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 5) Clay clusters
        ApplyClayClusters(s, seed, commonSolid, commonMeta);
        StepLog("Step 5 - Clay clusters", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 6) Caves carve-out (noise)
        bool[,] cave = ProceduralUtil.GenerateNoiseCaveMask(w, h, seed, s);

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            if (!cave[x, y]) continue;

            commonSolid[x, y] = ID_AIR;
            commonMeta[x, y]  = 0;
            commonFluid[x, y] = FLUID_NONE;
        }

        StepLog("Step 6 - Caves carve (noise)", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 7) Fluid infiltration flood fill (sea와 연결된 공간만, seaLevel 위로 금지)
        FloodFillFluidFromSeaSurface(commonSolid, commonFluid, w, h, seaLevel, FLUID_WATER);
        StepLog("Step 7 - Fluid flood fill (no upward)", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 7.5) PyramidPass (DesertPass 전에)
        ApplyPyramidPass(desertStartX, seaLevel, commonSolid, commonMeta, w, h);
        StepLog("Step 7.5 - PyramidPass", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 7.6) DesertPass
        ApplyDesertPass(s, seed, desertStartX, commonSolid, commonMeta, commonFluid);
        StepLog("Step 7.6 - DesertPass", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 8) Sand/Gravel/Clay
        ApplySandAndGravelAndClay(s, seed, desertStartX, commonSolid, commonMeta, commonFluid);
        StepLog("Step 8 - Sand/Gravel/Clay", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 8.5) SnowPass
        ApplySnowPass(s, seed, snowEndX, commonSolid, commonMeta, commonFluid);
        StepLog("Step 8.5 - SnowPass", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 9) Grass variants (Grass + FrozenGrass)
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            ushort id = commonSolid[x, y];
            if (id != ID_DIRT && id != ID_FROZEN_DIRT) continue;

            if (IsInSnowBiome(s, x, w, snowEndX))
            {
                if (commonSolid[x, y] != ID_FROZEN_DIRT) continue;

                if (TryComputeFrozenGrassId(x, y, commonSolid, out ushort fgId))
                {
                    commonSolid[x, y] = fgId;
                    commonMeta[x, y]  = 0;
                }
            }
            else
            {
                if (commonSolid[x, y] != ID_DIRT) continue;

                if (TryComputeGrassId(x, y, commonSolid, out ushort grassId))
                {
                    commonSolid[x, y] = grassId;
                    commonMeta[x, y]  = 0;
                }
            }
        }

        StepLog("Step 9 - Grass/FrozenGrass variants", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 10) Trees + Desert plants + Snow trunks
        PlaceTrees(s, seed, desertStartX, snowEndX, commonSolid, commonMeta);
        StepLog("Step 10 - Trees/DesertPlants/SnowTrunks", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 11) Decor (Desert + Snow)
        PlaceDecorAfterTrees(s, seed, desertStartX, snowEndX, commonSolid, commonMeta);
        StepLog("Step 11 - Decor", t0, totalStart);

        float end = Time.realtimeSinceStartup;
        Debug.Log($"[WorldGen] BuildCommonAndBg END TOTAL: {(end - totalStart) * 1000f:F1} ms");
    }

    private static int ComputeStartX(int seed, int minX, int maxX, int width)
    {
        int lo = Mathf.Clamp(minX, 0, width - 1);
        int hi = Mathf.Clamp(maxX, 0, width - 1);
        if (hi < lo) { int t = lo; lo = hi; hi = t; }
        if (lo == hi) return lo;

        var r = new System.Random(seed);
        return r.Next(lo, hi + 1);
    }

    private static void ApplyPyramidPass(int desertStartX, int seaLevel, ushort[,] commonSolid, ushort[,] commonMeta, int w, int h)
    {
        const int baseWidth = 101;     // 홀수
        int halfBase = baseWidth / 2;  // 50
        int height = halfBase + 1;     // 51

        int x0 = desertStartX;
        int x1 = w - 1;
        if (x1 < x0) return;

        int centerX = (x0 + x1) / 2;
        int baseY = seaLevel;

        for (int i = 0; i < height; i++)
        {
            int y = baseY + i;
            if ((uint)y >= (uint)h) break;

            int width_i = baseWidth - 2 * i;
            if (width_i <= 0) break;

            int half = width_i / 2;
            int lx = centerX - half;
            int rx = centerX + half;

            if (rx < 0 || lx >= w) continue;
            if (lx < 0) lx = 0;
            if (rx >= w) rx = w - 1;

            for (int x = lx; x <= rx; x++)
            {
                commonSolid[x, y] = ID_SANDSTONE_BRICK;
                commonMeta[x, y]  = 0;
            }
        }
    }

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

                if (commonFluid[nx, ny] == FLUID_NONE)
                    commonFluid[nx, ny] = fluidId;

                if (commonFluid[nx, ny] == fluidId)
                    q.Enqueue((nx, ny));
            }
        }
    }

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

            // Snow: FrozenGrass 위 FrozenTrunk
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

            // Desert: cactus/agave
            if (x >= desertStartX)
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

            if (x >= desertStartX)
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
                double r = rand.NextDouble();
                if (r < 0.60)
                {
                    commonSolid[x, ya] = ID_PLANT;
                    commonMeta[x, ya]  = 0;
                }
                else if (r < 0.75)
                {
                    commonSolid[x, ya] = ID_BUSH;
                    commonMeta[x, ya]  = 0;
                }
                else if (r < 0.85)
                {
                    commonSolid[x, ya] = ID_SMALL_STONE_PILE;
                    commonMeta[x, ya]  = 0;
                }
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

    private static void PropagateNaturalLight(WorldData world, CellLibrary cellLibrary)
    {
        int w = world.bg.GetLength(0);
        int h = world.bg.GetLength(1);

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            world.SetNaturalLight(x, y, 0);
            world.SetArtificialLight(x, y, 0);
        }

        byte[,] atten = new byte[w, h];
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            byte a = 0;

            if (world.GetBG(x, y) != ID_AIR) a += 1;

            ushort sid = world.GetSolid(x, y).id;
            if (sid != 0)
            {
                var flags = cellLibrary.GetSolidFlags(sid);
                if ((flags & CellLibrary.SolidFlags.Collidable) != 0)
                    a += 2;
            }

            atten[x, y] = a;
        }

        const byte INF = 255;
        byte[,] dist = new byte[w, h];
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            dist[x, y] = INF;

        var buckets = new List<(int x, int y)>[NATURAL_MAX + 1];
        for (int i = 0; i <= NATURAL_MAX; i++)
            buckets[i] = new List<(int x, int y)>();

        int yTop = h - 1;
        for (int x = 0; x < w; x++)
        {
            dist[x, yTop] = 0;
            buckets[0].Add((x, yTop));
        }

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        for (byte d = 0; d <= NATURAL_MAX; d++)
        {
            var bucket = buckets[d];
            for (int idx = 0; idx < bucket.Count; idx++)
            {
                var (cx, cy) = bucket[idx];
                if (dist[cx, cy] != d) continue;

                for (int dir = 0; dir < 4; dir++)
                {
                    int nx = cx + dx[dir];
                    int ny = cy + dy[dir];
                    if ((uint)nx >= (uint)w || (uint)ny >= (uint)h) continue;

                    byte a = atten[nx, ny];
                    int ndInt = d + a;
                    if (ndInt > NATURAL_MAX) continue;

                    byte nd = (byte)ndInt;
                    if (nd >= dist[nx, ny]) continue;

                    dist[nx, ny] = nd;
                    buckets[nd].Add((nx, ny));
                }
            }

            bucket.Clear();
        }

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            byte d = dist[x, y];
            if (d <= NATURAL_MAX)
            {
                ushort val = (ushort)(NATURAL_MAX - d);
                world.SetNaturalLight(x, y, val);
            }
        }
    }
}
