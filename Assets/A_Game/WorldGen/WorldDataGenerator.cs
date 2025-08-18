using System;
using System.Collections.Generic;
using UnityEngine;

public static class WorldDataGenerator
{
    // 하드코딩된 블록 ID
    private const ushort ID_AIR           = 0;
    private const ushort ID_ROCK          = 1;
    private const ushort ID_DIRT          = 2;
    private const ushort ID_GRASS_LEFT      = 3;
    private const ushort ID_GRASS_TOP       = 4;
    private const ushort ID_GRASS_RIGHT     = 5;
    private const ushort ID_GRASS_TOPLEFT   = 6;
    private const ushort ID_GRASS_TOPRIGHT  = 7;
    private const ushort ID_GRASS_LEFTRIGHT = 8;
    private const ushort ID_GRASS_ALL       = 9;
    private const ushort ID_GRANITE       = 4000;
    private const ushort ID_AMPHIBOLITE   = 4001;
    private const ushort ID_WATER         = 60000;
    private const ushort ID_ORE_COAL      = 3000;
    private const ushort ID_ORE_COPPER    = 3001;
    private const ushort ID_ORE_IRON      = 3002;
    private const ushort ID_TRUNK         = 2000;
    private const ushort ID_LEAF          = 2001;

    public static WorldData Generate(WorldGenSettings settings)
    {
        int w = settings.width;
        int h = settings.height;
        var fgMap  = new CellData[w, h];
        var bgMap  = new ushort[w, h];
        var light  = new byte[w, h];

        // 1) 초기 공기/물 및 bg 초기화, light=0
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                ushort baseId = (y < settings.waterHeight) ? ID_WATER : ID_AIR;
                fgMap[x, y] = MakeCell(baseId);
                bgMap[x, y] = ID_AIR;
                light[x, y] = 0;
            }

        // 2) 노이즈 기반 지층 높이 계산
        float[,] dirtH = new float[w, h], rockH = new float[w, h], granH = new float[w, h], amphH = new float[w, h];
        for (int x = 0; x < w; x++)
        {
            float sx = x + settings.seed;
            for (int y = 0; y < h; y++)
            {
                dirtH[x, y] = ProceduralUtil.FractalPerlin1D(
                    sx, settings.dirtNoiseBaseFrequency, settings.dirtNoiseOctaves,
                    settings.dirtNoisePersistence, settings.dirtNoiseLacunarity,
                    settings.dirtBaseHeight, settings.dirtRange);
                rockH[x, y] = ProceduralUtil.FractalPerlin1D(
                    sx + 10000, settings.rockNoiseBaseFrequency, settings.rockNoiseOctaves,
                    settings.rockNoisePersistence, settings.rockNoiseLacunarity,
                    settings.rockBaseHeight, settings.rockRange);
                granH[x, y] = ProceduralUtil.FractalPerlin1D(
                    sx + 20000, settings.graniteNoiseBaseFrequency, settings.graniteNoiseOctaves,
                    settings.graniteNoisePersistence, settings.graniteNoiseLacunarity,
                    settings.graniteBaseHeight, settings.graniteRange);
                amphH[x, y] = ProceduralUtil.FractalPerlin1D(
                    sx + 30000, settings.amphibNoiseBaseFrequency, settings.amphibNoiseOctaves,
                    settings.amphibNoisePersistence, settings.amphibNoiseLacunarity,
                    settings.amphibBaseHeight, settings.amphibRange);
            }
        }

        // 3) 지층 덮어쓰기
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                if (y < dirtH[x, y])    fgMap[x, y] = MakeCell(ID_DIRT);
                if (y < rockH[x, y])    fgMap[x, y] = MakeCell(ID_ROCK);
                if (y < granH[x, y])    fgMap[x, y] = MakeCell(ID_GRANITE);
                if (y < amphH[x, y])    fgMap[x, y] = MakeCell(ID_AMPHIBOLITE);

                ushort fgId = fgMap[x, y].id;
                if (fgId == ID_DIRT || fgId == ID_ROCK || fgId == ID_GRANITE || fgId == ID_AMPHIBOLITE)
                    bgMap[x, y] = fgId;
            }

        // 4) 광물 클러스터 (FG만)
        ApplyOreClusters(settings, fgMap);

        // 5) 동굴 생성 (FG만)
        bool[,] cave = ProceduralUtil.GenerateMixedCave(
            w, h,
            settings.caveInitialFillPercent, settings.caveBirthLimit,
            settings.caveSurvivalLimit, settings.caveIterations,
            settings.caveWalkerCount, settings.caveWalkLength,
            settings.caveDirectionBias);
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (cave[x, y])
                    fgMap[x, y] = MakeCell(ID_AIR);

        // 6) 물 Flood-Fill (FG만)
        FloodFillWater(fgMap, w, h, ID_WATER, ID_AIR);

        // 7) 잔디 배치
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (fgMap[x, y].id == ID_DIRT)
                    fgMap[x, y] = MakeCell(GetGrassVariant(x, y, fgMap));

        // 8) 나무 배치 (FG 트렁크/잎)
        PlaceTrees(settings, fgMap);

        // 9) 스카이라이트 확산 (하늘을 광원으로 BFS)
        const byte skyLight = 20;
        var queue = new Queue<(int x, int y)>();
        int yTop = h - 1;
        for (int x = 0; x < w; x++)
        {
            light[x, yTop] = skyLight;
            queue.Enqueue((x, yTop));
        }

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };
        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            byte curr = light[cx, cy];
            if (curr == 0) continue;

            for (int i = 0; i < 4; i++)
            {
                int nx = cx + dx[i], ny = cy + dy[i];
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;

                int attenuation = 0;
                if (bgMap[nx, ny] != ID_AIR) attenuation += 1;
                if (fgMap[nx, ny].hasCollider) attenuation += 2;

                byte next = (byte)Math.Max(0, curr - attenuation);
                if (next > light[nx, ny])
                {
                    light[nx, ny] = next;
                    queue.Enqueue((nx, ny));
                }
            }
        }

        return new WorldData { fg = fgMap, bg = bgMap, light = light };
    }

    private static CellData MakeCell(ushort id)
    {
        return new CellData
        {
            id           = id,
            hasCollider  = BlockLibrary.HasCollider(id),
            isLiquid     = BlockLibrary.IsLiquid(id),
            hasGravity   = BlockLibrary.HasGravity(id),
            isDependent  = BlockLibrary.IsDependent(id)
        };
    }

    private static void ApplyOreClusters(WorldGenSettings s, CellData[,] fgMap)
    {
        int w = fgMap.GetLength(0), h = fgMap.GetLength(1);
        void apply(int minH, int maxH, float mean, float std, float den, float exp, float maxf, ushort oreId)
        {
            var seeds    = ProceduralUtil.SampleSeedPositions(w, minH, maxH, den);
            var offsets  = ProceduralUtil.GetNeighborOffsets(s.neighborMode == WorldGenSettings.NeighborMode.EightDir);
            var clusters = ProceduralUtil.GenerateClusters(seeds, mean, std, maxf, exp, offsets, s.frontierMode == WorldGenSettings.FrontierMode.Random);

            foreach (var cl in clusters)
                foreach (var p in cl)
                    if (p.x >= 0 && p.y >= 0 && p.x < w && p.y < h &&
                        fgMap[p.x, p.y].id == ID_ROCK)
                        fgMap[p.x, p.y] = MakeCell(oreId);
        }

        apply(s.coalMinHeight,   s.coalMaxHeight,   s.coalClusterSizeMean,   s.coalClusterSizeStdDev,
              s.coalSeedDensity, s.coalExpansionProb, s.coalMaxGrowthFactor, ID_ORE_COAL);
        apply(s.copperMinHeight, s.copperMaxHeight, s.copperClusterSizeMean, s.copperClusterSizeStdDev,
              s.copperSeedDensity, s.copperExpansionProb, s.copperMaxGrowthFactor, ID_ORE_COPPER);
        apply(s.ironMinHeight,   s.ironMaxHeight,   s.ironClusterSizeMean,   s.ironClusterSizeStdDev,
              s.ironSeedDensity, s.ironExpansionProb, s.ironMaxGrowthFactor, ID_ORE_IRON);
    }

    private static void FloodFillWater(CellData[,] fgMap, int w, int h, ushort waterId, ushort airId)
    {
        var dirs = new (int dx, int dy)[] { (1, 0), (-1, 0), (0, -1) };
        var visited = new bool[w, h];
        var queue = new Queue<(int x, int y)>();

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (fgMap[x, y].id == waterId)
                {
                    visited[x, y] = true;
                    queue.Enqueue((x, y));
                }

        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            foreach (var (dxOff, dyOff) in dirs)
            {
                int nx = cx + dxOff, ny = cy + dyOff;
                if (nx < 0 || ny < 0 || nx >= w || ny >= h || visited[nx, ny]) continue;
                if (fgMap[nx, ny].id == airId)
                {
                    fgMap[nx, ny] = MakeCell(waterId);
                    queue.Enqueue((nx, ny));
                }
                visited[nx, ny] = true;
            }
        }
    }

    private static ushort GetGrassVariant(int x, int y, CellData[,] fgMap)
    {
        int w = fgMap.GetLength(0), h = fgMap.GetLength(1);
        for (int yy = y + 1; yy < h; yy++)
            if (fgMap[x, yy].id != ID_AIR)
                return ID_DIRT;

        bool up    = (y + 1 < h && fgMap[x, y + 1].id == ID_AIR);
        bool left  = (x - 1 >= 0 && fgMap[x - 1, y].id == ID_AIR);
        bool right = (x + 1 < w && fgMap[x + 1, y].id == ID_AIR);

        int mask = (up ? 1 : 0) | (left ? 2 : 0) | (right ? 4 : 0);
        switch (mask)
        {
            case 1: return ID_GRASS_TOP;
            case 2: return ID_GRASS_LEFT;
            case 3: return ID_GRASS_TOPLEFT;
            case 4: return ID_GRASS_RIGHT;
            case 5: return ID_GRASS_TOPRIGHT;
            case 6: return ID_GRASS_LEFTRIGHT;
            case 7: return ID_GRASS_ALL;
            default: return ID_DIRT;
        }
    }

    private static void PlaceTrees(WorldGenSettings s, CellData[,] fgMap)
    {
        int w = fgMap.GetLength(0), h = fgMap.GetLength(1);
        var rand = new System.Random(s.seed);

        for (int x = 0; x < w; x++)
        {
            if (rand.NextDouble() > s.treeDensity) continue;

            int y = h - 1;
            while (y > 0 && fgMap[x, y].id == ID_AIR) y--;
            if (fgMap[x, y].id != GetGrassVariant(x, y, fgMap)) continue;

            int seedY = y + 1;
            int H = SampleTri(rand, s.treeMinHeight, s.treeModeHeight, s.treeMaxHeight);

            for (int i = 0; i < H && seedY + i < h; i++)
                if (fgMap[x, seedY + i].id == ID_AIR)
                    fgMap[x, seedY + i] = MakeCell(ID_TRUNK);

            ProceduralUtil.DrawLeafBlobOnIDMap(
                x, seedY + H - 1, H,
                w, h,
                ID_LEAF,
                fgMap);
        }
    }

    private static int SampleTri(System.Random r, int min, int mode, int max)
    {
        double u = r.NextDouble(), c = (mode - min) / (double)(max - min);
        return u < c
            ? min + (int)Math.Sqrt(u * (mode - min) * (max - min))
            : max - (int)Math.Sqrt((1 - u) * (max - mode) * (max - min));
    }
}
