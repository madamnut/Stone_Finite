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

    private const ushort ID_CLAY                = 10;

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
    private const byte NATURAL_MAX = 20;

    /// <summary>
    /// 월드 전체 생성. 시드는 별도 인자.
    /// </summary>
    public static WorldData Generate(WorldGenSettings s, int seed)
    {
        int w = s.width, h = s.height;

        float genStart = Time.realtimeSinceStartup;

        // 공통 파이프라인 1회 실행 → common, bg 획득
        BuildCommonAndBg(s, seed, out var common, out var bg);

        float afterBuild = Time.realtimeSinceStartup;
        Debug.Log($"[WorldGen] BuildCommonAndBg total: {(afterBuild - genStart) * 1000f:F1} ms");

        // WorldData 생성
        var world = new WorldData(w, h);

        // BG 주입
        float bgStart = Time.realtimeSinceStartup;
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            ushort bgId = bg[x, y];
            if (bgId != ID_AIR)
                world.ForceBG(x, y, bgId);
        }
        float bgEnd = Time.realtimeSinceStartup;
        Debug.Log($"[WorldGen] Inject BG: {(bgEnd - bgStart) * 1000f:F1} ms");

        // FG / Fluid 주입
        float fgStart = Time.realtimeSinceStartup;
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            ushort id = common[x, y];
            if (id == ID_AIR)
                continue;

            if (id == ID_WATER)
            {
                // 초기 물은 가득 찬 유체로 강제 배치
                world.ForceFluid(x, y, ID_WATER, 128); // fluidAmount: 1~128, 가득 참 = 128
                continue;
            }

            // 나머지는 전부 본체(FG)로 강제 배치
            var cell = CellLibrary.MakeFgCell(id);
            world.ForceFG(x, y, in cell);
        }
        float fgEnd = Time.realtimeSinceStartup;
        Debug.Log($"[WorldGen] Inject FG/Fluid: {(fgEnd - fgStart) * 1000f:F1} ms");

        // 자연광
        float lightStart = Time.realtimeSinceStartup;
        PropagateNaturalLight(world);
        float lightEnd = Time.realtimeSinceStartup;
        Debug.Log($"[WorldGen] PropagateNaturalLight: {(lightEnd - lightStart) * 1000f:F1} ms");

        float genEnd = Time.realtimeSinceStartup;
        Debug.Log($"[WorldGen] TOTAL Generate: {(genEnd - genStart) * 1000f:F1} ms");

        return world;
    }

    /// <summary>프리뷰용: common만. 시드 별도 인자.</summary>
    public static ushort[,] GenerateCommon(WorldGenSettings s, int seed, out ushort[,] bg)
    {
        BuildCommonAndBg(s, seed, out var common, out bg);
        return common;
    }

    /// <summary>내부 파이프라인: common/bg 구성 단계.</summary>
    private static void BuildCommonAndBg(WorldGenSettings s, int seed, out ushort[,] common, out ushort[,] bg)
    {
        int w = s.width, h = s.height;

        float tStartAll = Time.realtimeSinceStartup;
        float t0 = tStartAll;

        common = new ushort[w, h];
        bg     = new ushort[w, h];

        // 1) 해수면 시드
        int waterH = Mathf.Min(h, s.waterHeight);
        for (int x = 0; x < w; x++)
        for (int y = 0; y < waterH; y++)
            common[x, y] = ID_WATER;

        float t1 = Time.realtimeSinceStartup;
        Debug.Log($"[WorldGen] Step 1 - Water seed: {(t1 - t0) * 1000f:F1} ms");
        t0 = t1;

        // 2) 노이즈 높이 (1D로 변경)
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

        t1 = Time.realtimeSinceStartup;
        Debug.Log($"[WorldGen] Step 2 - Noise heights: {(t1 - t0) * 1000f:F1} ms");
        t0 = t1;

        // 3) 지층 덮어쓰기 + BG 확정 (1D 높이 사용)
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
                common[x, y] = id;
                bg[x, y]     = id;
            }
        }

        t1 = Time.realtimeSinceStartup;
        Debug.Log($"[WorldGen] Step 3 - Layer fill & BG: {(t1 - t0) * 1000f:F1} ms");
        t0 = t1;

        // 4) 광물
        ApplyOreClusters(s, seed, common);
        t1 = Time.realtimeSinceStartup;
        Debug.Log($"[WorldGen] Step 4 - Ore clusters: {(t1 - t0) * 1000f:F1} ms");
        t0 = t1;

        // 4.5) 점토 클러스터 (Dirt에만 생성)
        ApplyClayClusters(s, seed, common);
        t1 = Time.realtimeSinceStartup;
        Debug.Log($"[WorldGen] Step 4.5 - Clay clusters: {(t1 - t0) * 1000f:F1} ms");
        t0 = t1;

        // 5) 동굴 캐브아웃 (노이즈 기반 A ∪ B)
        float caveStart = Time.realtimeSinceStartup;
        bool[,] cave = ProceduralUtil.GenerateNoiseCaveMask(w, h, seed, s);

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            if (cave[x, y])
                common[x, y] = ID_AIR;

        float caveEnd = Time.realtimeSinceStartup;
        Debug.Log($"[WorldGen] Step 5 - Caves carve (noise): {(caveEnd - caveStart) * 1000f:F1} ms");
        t0 = caveEnd;

        // 6) 물 플러드필
        FloodFillWater(common, w, h, ID_WATER, ID_AIR);
        t1 = Time.realtimeSinceStartup;
        Debug.Log($"[WorldGen] Step 6 - Water flood fill: {(t1 - t0) * 1000f:F1} ms");
        t0 = t1;

        // 6.5) 지형 변환: 모래/자갈/점토
        ApplySandAndGravelAndClay(s, seed, common);
        t1 = Time.realtimeSinceStartup;
        Debug.Log($"[WorldGen] Step 6.5 - Sand/Gravel/Clay: {(t1 - t0) * 1000f:F1} ms");
        t0 = t1;

        // 7) 잔디 변형
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            if (common[x, y] == ID_DIRT)
                common[x, y] = GetGrassVariant(x, y, common);

        t1 = Time.realtimeSinceStartup;
        Debug.Log($"[WorldGen] Step 7 - Grass variants: {(t1 - t0) * 1000f:F1} ms");
        t0 = t1;

        // 8) 나무
        PlaceTrees(s, seed, common);
        t1 = Time.realtimeSinceStartup;
        Debug.Log($"[WorldGen] Step 8 - Trees: {(t1 - t0) * 1000f:F1} ms");
        t0 = t1;

        // 9) 데코
        PlaceDecorAfterTrees(s, seed, common);
        t1 = Time.realtimeSinceStartup;
        Debug.Log($"[WorldGen] Step 9 - Decor: {(t1 - t0) * 1000f:F1} ms");

        float tEnd = Time.realtimeSinceStartup;
        Debug.Log($"[WorldGen] BuildCommonAndBg TOTAL (Steps 1-9): {(tEnd - tStartAll) * 1000f:F1} ms");
    }

    // ─────────────────────────────────────────────────────────
    // 모래/자갈/점토 변환 (BFS 기반 거리맵)
    // ─────────────────────────────────────────────────────────
    private static void ApplySandAndGravelAndClay(WorldGenSettings s, int seed, ushort[,] common)
    {
        float tStart = Time.realtimeSinceStartup;

        int w = common.GetLength(0), h = common.GetLength(1);
        var rand = new System.Random(seed ^ 0xA11CE);

        const int INF = 1_000_000;

        // Chebyshev 거리용 8방향 (대각 포함)
        int[] dx8 = { 1,  1,  0, -1, -1, -1,  0,  1 };
        int[] dy8 = { 0,  1,  1,  1,  0, -1, -1, -1 };

        var q = new Queue<(int x, int y)>();

        // ───────────────── 1) Water 거리 맵 (반경 3) ─────────────────
        int[,] distWater = new int[w, h];
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            distWater[x, y] = INF;

        q.Clear();
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            if (common[x, y] == ID_WATER)
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

        // 1단계: Dirt → Sand (반경3 내 Water)
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            if (common[x, y] == ID_DIRT)
            {
                int d = distWater[x, y];
                if (d > 0 && d <= maxWaterR)
                    common[x, y] = ID_SAND;
            }
        }

        // 2단계 A: Rock → Gravel (반경3 내 Water)
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            if (common[x, y] == ID_ROCK)
            {
                int d = distWater[x, y];
                if (d > 0 && d <= maxWaterR)
                    common[x, y] = ID_GRAVEL;
            }
        }

        // ───────────────── 2) Dirt 거리 맵 (반경 2) ─────────────────
        int[,] distDirt = new int[w, h];
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            distDirt[x, y] = INF;

        q.Clear();
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            if (common[x, y] == ID_DIRT)
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

        // 2단계 B: Rock → Gravel (반경2 내 Dirt, 30%)
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            if (common[x, y] == ID_ROCK)
            {
                int d = distDirt[x, y];
                if (d > 0 && d <= maxDirtR)
                {
                    if (rand.NextDouble() < 0.30)
                        common[x, y] = ID_GRAVEL;
                }
            }
        }

        // ───────────────── 3) Sand 거리 맵 (반경 3) ─────────────────
        int[,] distSand = new int[w, h];
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            distSand[x, y] = INF;

        q.Clear();
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            if (common[x, y] == ID_SAND)
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

        // 3단계: Dirt → Gravel/Clay (반경3 내 Sand → 40%/40%)
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            if (common[x, y] == ID_DIRT)
            {
                int d = distSand[x, y];
                if (d > 0 && d <= maxSandR)
                {
                    double r = rand.NextDouble();
                    if      (r < 0.40) common[x, y] = ID_GRAVEL;
                    else if (r < 0.80) common[x, y] = ID_CLAY;
                }
            }
        }

        float tEnd = Time.realtimeSinceStartup;
        Debug.Log($"[WorldGen] ApplySandAndGravelAndClay (BFS) TOTAL: {(tEnd - tStart) * 1000f:F1} ms");
    }

    // ─────────────────────────────────────────────────────────
    // 내부: 점토 클러스터 (Dirt에만 부여)
    // ─────────────────────────────────────────────────────────
    private static void ApplyClayClusters(WorldGenSettings s, int seed, ushort[,] common)
    {
        int w = common.GetLength(0), h = common.GetLength(1);

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
            if ((uint)p.x < w && (uint)p.y < h && common[p.x, p.y] == ID_DIRT)
                common[p.x, p.y] = ID_CLAY;
    }

    // ─────────────────────────────────────────────────────────
    // 내부: 광물 클러스터
    // ─────────────────────────────────────────────────────────
    private static void ApplyOreClusters(WorldGenSettings s, int seed, ushort[,] common)
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

        // apply(s.tinMinHeight, s.tinMaxHeight, s.tinClusterSizeMean, s.tinClusterSizeStdDev,
        //       s.tinSeedDensity, s.tinExpansionProb, s.tinMaxGrowthFactor, ID_ORE_TIN);
    }

    // ─────────────────────────────────────────────────────────
    // 내부: 물 플러드필
    // ─────────────────────────────────────────────────────────
    private static void FloodFillWater(ushort[,] common, int w, int h, ushort waterId, ushort airId)
    {
        var dirs = new (int dx, int dy)[] { (1, 0), (-1, 0), (0, -1) };
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
                if ((uint)nx >= w || ((uint)ny >= h) || visited[nx, ny]) continue;
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
    // 내부: 트리 배치
    // ─────────────────────────────────────────────────────────
    private static void PlaceTrees(WorldGenSettings s, int seed, ushort[,] common)
    {
        int w = common.GetLength(0), h = common.GetLength(1);
        var rand = new System.Random(seed);

        var tpl = StructureLoader.Load("Tree");
        if (tpl == null || tpl.layers == null || tpl.layers.deco == null) return;
        var deco = tpl.layers.deco;
        int tplH = deco.Length; if (tplH == 0) return;
        int tplW = deco[0].Length;
        int ax = tpl.anchor.x, ay = tpl.anchor.y;

        for (int x = 0; x < w; x++)
        {
            if (rand.NextDouble() > s.treeDensity) continue;

            // 지면 찾기
            int y = h - 1;
            while (y > 0 && common[x, y] == ID_AIR) y--;
            if (common[x, y] != GetGrassVariant(x, y, common)) continue;

            int seedY = y + 1;
            int worldOx = x - ax;
            int worldOy = seedY - ay;

            // 템플릿 페인트
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

                    int curr = common[wx, wy];
                    bool canWrite = false;

                    if (tpl.writeRules != null && tpl.writeRules.TryGetValue(id, out var rule) && rule?.targets != null)
                    {
                        for (int k = 0; k < rule.targets.Length; k++)
                            if (curr == rule.targets[k]) { canWrite = true; break; }
                    }
                    else
                    {
                        canWrite = (curr == ID_AIR);
                    }

                    if (canWrite) common[wx, wy] = (ushort)id;
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    // 내부: 트리 이후 데코
    // ─────────────────────────────────────────────────────────
    private static void PlaceDecorAfterTrees(WorldGenSettings s, int seed, ushort[,] common)
    {
        int w = common.GetLength(0), h = common.GetLength(1);
        var rand = new System.Random(seed ^ 0xDEC0);

        for (int x = 1; x < w - 1; x++)
        for (int y = 1; y < h - 1; y++)
        {
            ushort here = common[x, y];
            int ya = y + 1;
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
    // 내부: 자연광 전파
    // ─────────────────────────────────────────────────────────
    private static void PropagateNaturalLight(WorldData world)
    {
        int w = world.bg.GetLength(0), h = world.bg.GetLength(1);

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
                if (world.bg[nx, ny] != ID_AIR) atten += 1;
                if (world.IsCollidable(nx, ny)) atten += 2;

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
}
