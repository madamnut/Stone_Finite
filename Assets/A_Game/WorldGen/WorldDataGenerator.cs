using System;
using System.Collections.Generic;
using UnityEngine;

public static class WorldDataGenerator
{
    // ── Cell IDs (ATT_Cell.json과 일치) ──
    private const ushort ID_AIR                 = 0;
    private const ushort ID_ROCK                = 1;

    private const ushort ID_DIRT                = 2;
    private const ushort ID_GRASS_LEFT          = 3;
    private const ushort ID_GRASS_TOP           = 4;
    private const ushort ID_GRASS_RIGHT         = 5;
    private const ushort ID_GRASS_TOPLEFT       = 6;
    private const ushort ID_GRASS_TOPRIGHT      = 7;
    private const ushort ID_GRASS_LEFTRIGHT     = 8;
    private const ushort ID_GRASS_TOPLEFTRIGHT  = 9;

    private const ushort ID_SAND                = 1000;
    private const ushort ID_GRAVEL              = 1001;

    private const ushort ID_TRUNK               = 2000;
    private const ushort ID_LEAF                = 2001;
    private const ushort ID_PLANT               = 2002;
    private const ushort ID_BUSH                = 2003;
    private const ushort ID_STONE_PILE          = 2004;
    private const ushort ID_SMALL_STONE_PILE    = 2005;

    private const ushort ID_ORE_COAL            = 3000;
    private const ushort ID_ORE_COPPER          = 3001;
    private const ushort ID_ORE_IRON            = 3002;
    private const ushort ID_ORE_TIN             = 3003;

    private const ushort ID_GRANITE             = 4000;
    private const ushort ID_AMPHIBOLITE         = 4001;

    private const ushort ID_WATER               = 60000;

    // ── 라이트 파라미터 ──
    private const byte NATURAL_MAX = 20; // 자연광 최대

    public static WorldData Generate(WorldGenSettings s)
    {
        int w = s.width, h = s.height;

        // 공통 ID 맵과 BG 맵
        var common = new ushort[w, h]; // 0=Air
        var bg     = new ushort[w, h]; // 지층 단계에서만 기록

        // 1) 해수면 시드(전역)
        for (int x = 0; x < w; x++)
        for (int y = 0; y < Math.Min(h, s.waterHeight); y++)
            common[x, y] = ID_WATER;

        // 2) 노이즈 기반 지층 높이들
        float[,] dirtH = new float[w, h], rockH = new float[w, h], granH = new float[w, h], amphH = new float[w, h];
        for (int x = 0; x < w; x++)
        {
            float sx = x + s.seed;
            for (int y = 0; y < h; y++)
            {
                dirtH[x, y] = ProceduralUtil.FractalPerlin1D(
                    sx, s.dirtNoiseBaseFrequency, s.dirtNoiseOctaves,
                    s.dirtNoisePersistence, s.dirtNoiseLacunarity,
                    s.dirtBaseHeight, s.dirtRange);
                rockH[x, y] = ProceduralUtil.FractalPerlin1D(
                    sx + 10000, s.rockNoiseBaseFrequency, s.rockNoiseOctaves,
                    s.rockNoisePersistence, s.rockNoiseLacunarity,
                    s.rockBaseHeight, s.rockRange);
                granH[x, y] = ProceduralUtil.FractalPerlin1D(
                    sx + 20000, s.graniteNoiseBaseFrequency, s.graniteNoiseOctaves,
                    s.graniteNoisePersistence, s.graniteNoiseLacunarity,
                    s.graniteBaseHeight, s.graniteRange);
                amphH[x, y] = ProceduralUtil.FractalPerlin1D(
                    sx + 30000, s.amphibNoiseBaseFrequency, s.amphibNoiseOctaves,
                    s.amphibNoisePersistence, s.amphibNoiseLacunarity,
                    s.amphibBaseHeight, s.amphibRange);
            }
        }

        // 3) 지층 덮어쓰기 + BG 확정 (Dirt → Rock → Granite → Amphibolite)
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            ushort id = 0;
            if (y < dirtH[x, y]) id = ID_DIRT;
            if (y < rockH[x, y]) id = ID_ROCK;
            if (y < granH[x, y]) id = ID_GRANITE;
            if (y < amphH[x, y]) id = ID_AMPHIBOLITE;

            if (id != 0)
            {
                common[x, y] = id; // 해수면 시드 위에 솔리드가 덮일 수 있음
                bg[x, y]     = id; // BG는 이 시점에만 기록
            }
        }

        // 4) 광물 클러스터 (ROCK에만)
        ApplyOreClusters(s, common);

        // 5) 동굴 캐브아웃 (common만 0으로, BG 유지)
        bool[,] cave = ProceduralUtil.GenerateMixedCave(
            w, h,
            s.caveInitialFillPercent, s.caveBirthLimit,
            s.caveSurvivalLimit, s.caveIterations,
            s.caveWalkerCount, s.caveWalkLength, s.caveDirectionBias);

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            if (cave[x, y]) common[x, y] = ID_AIR;

        // 6) 물 플러드필(해수면 시드에서만 확장)
        FloodFillWater(common, w, h, ID_WATER, ID_AIR);

        // 7) 잔디 변형 (DIRT → GRASS_*)
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            if (common[x, y] == ID_DIRT)
                common[x, y] = GetGrassVariant(x, y, common);

        // 8) 나무 배치
        PlaceTrees(s, common);

        // 9) 데코 배치
        PlaceDecorAfterTrees(s, common);

        // 10) 레이어 주입 → WorldData
        var world = new WorldData(w, h);

        // BG 주입
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            world.bg[x, y] = bg[x, y];

        // FG/Liquid/Deco 주입
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            ushort id = common[x, y];
            switch (CellLibrary.TypeOf(id))
            {
                case CellType.Solid:
                    world.fg[x, y]     = new SolidCell  { id = id, hasGravity = CellLibrary.HasGravity(id) };
                    world.liquid[x, y] = new LiquidCell { id = 0, amount = 0 };
                    world.deco[x, y]   = new DecoCell   { id = 0, depend = DepFlags.None };
                    break;

                case CellType.Liquid:
                    world.fg[x, y]     = new SolidCell  { id = 0, hasGravity = false };
                    world.deco[x, y]   = new DecoCell   { id = 0, depend = DepFlags.None };
                    world.liquid[x, y] = new LiquidCell { id = id, amount = 100 };
                    break;

                case CellType.Deco:
                    world.fg[x, y]     = new SolidCell  { id = 0, hasGravity = false };
                    world.liquid[x, y] = new LiquidCell { id = 0, amount = 0 };
                    world.deco[x, y]   = new DecoCell   { id = id, depend = CellLibrary.DependFlagsOf(id) };
                    break;

                default: // None/Air
                    world.fg[x, y]     = new SolidCell  { id = 0, hasGravity = false };
                    world.liquid[x, y] = new LiquidCell { id = 0, amount = 0 };
                    world.deco[x, y]   = new DecoCell   { id = 0, depend = DepFlags.None };
                    break;
            }
        }

        // 11) 자연광 전파(NATURAL_MAX=20, 감쇄 FG=2 + BG=1)
        PropagateNaturalLight(world);

        return world;
    }

    // ─────────────────────────────────────────────────────────
    // 내부: 광물 클러스터를 common에 적용 (ROCK 셀에만)
    // ─────────────────────────────────────────────────────────
    private static void ApplyOreClusters(WorldGenSettings s, ushort[,] common)
    {
        int w = common.GetLength(0), h = common.GetLength(1);

        void apply(int minH, int maxH, float mean, float std, float den, float exp, float maxf, ushort oreId)
        {
            var seeds    = ProceduralUtil.SampleSeedPositions(w, minH, maxH, den);
            var offsets  = ProceduralUtil.GetNeighborOffsets(s.neighborMode == WorldGenSettings.NeighborMode.EightDir);
            var clusters = ProceduralUtil.GenerateClusters(seeds, mean, std, maxf, exp, offsets, s.frontierMode == WorldGenSettings.FrontierMode.Random);

            foreach (var cl in clusters)
            foreach (var p in cl)
                if ((uint)p.x < w && (uint)p.y < h && common[p.x, p.y] == ID_ROCK)
                    common[p.x, p.y] = oreId;
        }

        apply(s.coalMinHeight,   s.coalMaxHeight,   s.coalClusterSizeMean,   s.coalClusterSizeStdDev,
              s.coalSeedDensity, s.coalExpansionProb, s.coalMaxGrowthFactor, ID_ORE_COAL);

        apply(s.copperMinHeight, s.copperMaxHeight, s.copperClusterSizeMean, s.copperClusterSizeStdDev,
              s.copperSeedDensity, s.copperExpansionProb, s.copperMaxGrowthFactor, ID_ORE_COPPER);

        apply(s.ironMinHeight,   s.ironMaxHeight,   s.ironClusterSizeMean,   s.ironClusterSizeStdDev,
              s.ironSeedDensity, s.ironExpansionProb, s.ironMaxGrowthFactor, ID_ORE_IRON);

        // 필요 시 주석 해제
        // apply(s.tinMinHeight, s.tinMaxHeight, s.tinClusterSizeMean, s.tinClusterSizeStdDev,
        //       s.tinSeedDensity, s.tinExpansionProb, s.tinMaxGrowthFactor, ID_ORE_TIN);
    }

    // ─────────────────────────────────────────────────────────
    // 내부: 물 플러드필 (Right, Left, Down 확장)
    // ─────────────────────────────────────────────────────────
    private static void FloodFillWater(ushort[,] common, int w, int h, ushort waterId, ushort airId)
    {
        var dirs = new (int dx, int dy)[] { (1, 0), (-1, 0), (0, -1) }; // 우, 좌, 하향
        var visited = new bool[w, h];
        var q = new Queue<(int x, int y)>();

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            if (common[x, y] == waterId)
            {
                visited[x, y] = true;
                q.Enqueue((x, y));
            }

        while (q.Count > 0)
        {
            var (cx, cy) = q.Dequeue();
            foreach (var (dx, dy) in dirs)
            {
                int nx = cx + dx, ny = cy + dy;
                if ((uint)nx >= w || (uint)ny >= h || visited[nx, ny]) continue;
                if (common[nx, ny] == airId)
                {
                    common[nx, ny] = waterId;
                    q.Enqueue((nx, ny));
                }
                visited[nx, ny] = true;
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    // 내부: 잔디 변형
    // ─────────────────────────────────────────────────────────
    private static ushort GetGrassVariant(int x, int y, ushort[,] common)
    {
        int w = common.GetLength(0), h = common.GetLength(1);

        // 머리 위 끝까지 공기/물인지 체크. 아니면 흙 유지.
        for (int yy = y + 1; yy < h; yy++)
            if (common[x, yy] != ID_AIR)
                return ID_DIRT;

        bool up    = (y + 1 < h && (common[x, y + 1] == ID_AIR));
        bool left  = (x - 1 >= 0 && (common[x - 1, y] == ID_AIR));
        bool right = (x + 1 < w && (common[x + 1, y] == ID_AIR));

        int mask = (up ? 1 : 0) | (left ? 2 : 0) | (right ? 4 : 0);
        switch (mask)
        {
            case 1: return ID_GRASS_TOP;
            case 2: return ID_GRASS_LEFT;
            case 3: return ID_GRASS_TOPLEFT;
            case 4: return ID_GRASS_RIGHT;
            case 5: return ID_GRASS_TOPRIGHT;
            case 6: return ID_GRASS_LEFTRIGHT;
            case 7: return ID_GRASS_TOPLEFTRIGHT;
            default: return ID_DIRT;
        }
    }

    // ─────────────────────────────────────────────────────────
    // 내부: 트리 배치 (줄기+잎)
    // ─────────────────────────────────────────────────────────
    private static void PlaceTrees(WorldGenSettings s, ushort[,] common)
    {
        int w = common.GetLength(0), h = common.GetLength(1);
        var rand = new System.Random(s.seed);

        for (int x = 0; x < w; x++)
        {
            if (rand.NextDouble() > s.treeDensity) continue;

            // 지면 찾기: 위에서 아래로 첫 비공기(또는 비물) 위치
            int y = h - 1;
            while (y > 0 && (common[x, y] == ID_AIR)) y--;

            // 해당 지면이 잔디 변형과 일치해야 트리 스폰
            if (common[x, y] != GetGrassVariant(x, y, common)) continue;

            int seedY = y + 1;
            int H = SampleTri(rand, s.treeMinHeight, s.treeModeHeight, s.treeMaxHeight);

            // 줄기
            for (int i = 0; i < H && seedY + i < h; i++)
                if (common[x, seedY + i] == ID_AIR) common[x, seedY + i] = ID_TRUNK;

            // 잎
            ProceduralUtil.DrawLeafBlobOnIDMap(
                x, seedY + H - 1, H,
                w, h,
                ID_LEAF,
                common);
        }
    }

    // ─────────────────────────────────────────────────────────
    // 내부: 트리 이후 데코
    // ─────────────────────────────────────────────────────────
    private static void PlaceDecorAfterTrees(WorldGenSettings s, ushort[,] common)
    {
        int w = common.GetLength(0), h = common.GetLength(1);
        var rand = new System.Random(s.seed ^ 0xDEC0);

        for (int x = 1; x < w - 1; x++)
        for (int y = 1; y < h - 1; y++)
        {
            ushort here = common[x, y];
            int ya = y + 1; // 위 셀
            if (common[x, ya] != ID_AIR) continue;

            bool isGrass =
                here == ID_GRASS_LEFT || here == ID_GRASS_TOP || here == ID_GRASS_RIGHT ||
                here == ID_GRASS_TOPLEFT || here == ID_GRASS_TOPRIGHT ||
                here == ID_GRASS_LEFTRIGHT || here == ID_GRASS_TOPLEFTRIGHT;

            if (isGrass)
            {
                double r = rand.NextDouble();
                if      (r < 0.60) common[x, ya] = ID_PLANT;
                else if (r < 0.75) common[x, ya] = ID_BUSH;
                else if (r < 0.85) common[x, ya] = ID_SMALL_STONE_PILE;
                continue;
            }

            if (here == ID_ROCK)
            {
                if (rand.NextDouble() < 0.20)
                    common[x, ya] = ID_STONE_PILE;
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    // 내부: 자연광 전파(NATURAL_MAX=20). 감쇄=BG(1)+FG(2)
    // ─────────────────────────────────────────────────────────
    private static void PropagateNaturalLight(WorldData world)
    {
        int w = world.bg.GetLength(0), h = world.bg.GetLength(1);

        // 초기화
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            world.light[x, y] = new LightCell { natural = 0, artificial = 0 };

        var q = new Queue<(int x, int y)>();
        int yTop = h - 1;
        for (int x = 0; x < w; x++)
        {
            world.light[x, yTop].natural = NATURAL_MAX;
            q.Enqueue((x, yTop));
        }

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        while (q.Count > 0)
        {
            var (cx, cy) = q.Dequeue();
            byte curr = world.light[cx, cy].natural;
            if (curr == 0) continue;

            for (int i = 0; i < 4; i++)
            {
                int nx = cx + dx[i], ny = cy + dy[i];
                if ((uint)nx >= w || (uint)ny >= h) continue;

                int atten = 0;
                if (world.bg[nx, ny] != ID_AIR) atten += 1;          // BG 감쇄
                if (world.fg[nx, ny].id != 0)   atten += 2;          // FG 감쇄

                int next = curr - atten;
                if (next <= 0) continue;

                if (next > world.light[nx, ny].natural)
                {
                    world.light[nx, ny].natural = (byte)Math.Min(next, NATURAL_MAX);
                    q.Enqueue((nx, ny));
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    // 내부: 삼각분포 표본
    // ─────────────────────────────────────────────────────────
    private static int SampleTri(System.Random r, int min, int mode, int max)
    {
        double u = r.NextDouble(), c = (double)(mode - min) / (max - min);
        return u < c
            ? min + (int)Math.Sqrt(u * (mode - min) * (max - min))
            : max - (int)Math.Sqrt((1 - u) * (max - mode) * (max - min));
    }
}
