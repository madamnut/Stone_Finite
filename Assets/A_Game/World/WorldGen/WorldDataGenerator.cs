// WorldDataGenerator.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public static class WorldDataGenerator
{
    // ── Solid Cell IDs (ATT_Solid.json과 일치) ──
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

    // ── Liquid IDs (ATT_Liquid.json과 일치) ──
    private const ushort LIQ_NONE  = 0;
    private const ushort LIQ_WATER = 1; // 물 id = 1 (확정)

    // ── 라이트 파라미터 ──
    private const byte NATURAL_MAX = 15;

    // 로그 유틸 (단계별 ms + 누적 ms)
    private static void StepLog(string label, float stepStart, float totalStart)
    {
        float now = Time.realtimeSinceStartup;
        Debug.Log($"[WorldGen] {label}: {(now - stepStart) * 1000f:F1} ms (total {(now - totalStart) * 1000f:F1} ms)");
    }

    /// <summary>
    /// 월드 전체 생성. 시드는 별도 인자.
    /// </summary>
    public static WorldData Generate(WorldGenSettings s, int seed, CellLibrary cellLibrary)
    {
        int w = s.width, h = s.height;

        float totalStart = Time.realtimeSinceStartup;
        float t0 = totalStart;

        Debug.Log($"[WorldGen] START Generate w={w} h={h} seed={seed} waterHeight={s.waterHeight}");

        // 공통 파이프라인 1회 실행 → commonSolid, bg, commonLiquid 획득
        BuildCommonAndBg(s, seed, out var commonSolid, out var bg, out var commonLiquid);
        StepLog("BuildCommonAndBg", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // WorldData 생성
        var world = new WorldData(w, h);
        StepLog("Create WorldData arrays", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // BG 주입
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            ushort bgId = bg[x, y];
            if (bgId != ID_AIR)
                world.ForceBG(x, y, bgId);
        }
        StepLog("Inject BG", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Solid 주입
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            ushort id = commonSolid[x, y];
            if (id == ID_AIR) continue;

            // 인스펙터 신뢰 전제: cellLibrary는 정상 주입되어 있어야 함
            var cell = cellLibrary.MakeSolidCell(id);
            world.ForceSolid(x, y, in cell);
        }
        StepLog("Inject Solid", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Liquid 주입 (맵 생성 시 유체는 기본 MaxFluid로 가득)
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            ushort lid = commonLiquid[x, y];
            if (lid == LIQ_NONE) continue;

            var lc = cellLibrary.MakeLiquidCell(lid); // default = 128
            world.ForceLiquid(x, y, in lc);
        }
        StepLog("Inject Liquid", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // 자연광
        PropagateNaturalLight(world);
        StepLog("PropagateNaturalLight", t0, totalStart);

        float totalEnd = Time.realtimeSinceStartup;
        Debug.Log($"[WorldGen] END Generate TOTAL: {(totalEnd - totalStart) * 1000f:F1} ms");

        return world;
    }

    /// <summary>
    /// 프리뷰용: commonSolid만. 시드 별도 인자.
    /// </summary>
    public static ushort[,] GenerateCommonSolid(WorldGenSettings s, int seed, out ushort[,] bg, out ushort[,] commonLiquid)
    {
        BuildCommonAndBg(s, seed, out var commonSolid, out bg, out commonLiquid);
        return commonSolid;
    }

    /// <summary>
    /// 내부 파이프라인: commonSolid/bg/commonLiquid 구성 단계.
    /// </summary>
    private static void BuildCommonAndBg(
        WorldGenSettings s, int seed,
        out ushort[,] commonSolid,
        out ushort[,] bg,
        out ushort[,] commonLiquid
    )
    {
        int w = s.width, h = s.height;

        float totalStart = Time.realtimeSinceStartup;
        float t0 = totalStart;

        commonSolid  = new ushort[w, h];
        bg           = new ushort[w, h];
        commonLiquid = new ushort[w, h];

        int seaLevel = s.waterHeight; // 요구사항: 세팅 그대로 사용

        Debug.Log($"[WorldGen] BuildCommonAndBg START w={w} h={h} seed={seed} seaLevel(waterHeight)={seaLevel}");

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
                bg[x, y]          = id;

                // solid 확정된 칸은 유체 없음
                commonLiquid[x, y] = LIQ_NONE;
            }
        }

        StepLog("Step 2 - Layer fill & BG", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 3) SeaColumnFill (seaLevel부터 아래로, AIR인 동안만 채우다가, Air 아닌거 만나면 해당 x는 끝)
        SeaColumnFill(commonSolid, commonLiquid, w, h, seaLevel, LIQ_WATER);

        StepLog($"Step 3 - SeaColumnFill (seaLevel={seaLevel})", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 4) Ores
        ApplyOreClusters(s, seed, commonSolid);
        StepLog("Step 4 - Ore clusters", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 5) Clay clusters
        ApplyClayClusters(s, seed, commonSolid);
        StepLog("Step 5 - Clay clusters", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 6) Caves carve-out (noise)
        bool[,] cave = ProceduralUtil.GenerateNoiseCaveMask(w, h, seed, s);

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            if (!cave[x, y]) continue;

            // solid 공기화 + 유체 초기화(침투는 다음 단계에서)
            commonSolid[x, y]  = ID_AIR;
            commonLiquid[x, y] = LIQ_NONE;
        }

        StepLog("Step 6 - Caves carve (noise)", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 7) Liquid infiltration flood fill (sea와 연결된 공간만)
        // ★ 수정: seaLevel 위로는 절대 확장하지 않음
        FloodFillLiquidFromSeaSurface(commonSolid, commonLiquid, w, h, seaLevel, LIQ_WATER);

        StepLog("Step 7 - Liquid flood fill (no upward)", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 8) Sand/Gravel/Clay conversion (water dist-map)
        ApplySandAndGravelAndClay(s, seed, commonSolid, commonLiquid);
        StepLog("Step 8 - Sand/Gravel/Clay", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 9) Grass variants
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            if (commonSolid[x, y] == ID_DIRT)
                commonSolid[x, y] = GetGrassVariant(x, y, commonSolid);

        StepLog("Step 9 - Grass variants", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 10) Trees
        PlaceTrees(s, seed, commonSolid);
        StepLog("Step 10 - Trees", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 11) Decor
        PlaceDecorAfterTrees(s, seed, commonSolid);
        StepLog("Step 11 - Decor", t0, totalStart);

        float end = Time.realtimeSinceStartup;
        Debug.Log($"[WorldGen] BuildCommonAndBg END TOTAL: {(end - totalStart) * 1000f:F1} ms");
    }

    // ─────────────────────────────────────────────────────────
    // Step 3: 해수면 컬럼 채우기
    // seaLevel에서 아래(0)로 내려가며 AIR인 동안만 liquidId 채움.
    // AIR가 아닌 걸 만나면 해당 x는 종료 (다음 x로).
    // seaLevel은 waterHeight "그대로" 사용.
    // ─────────────────────────────────────────────────────────
    private static void SeaColumnFill(ushort[,] commonSolid, ushort[,] commonLiquid, int w, int h, int seaLevel, ushort liquidId)
    {
        int y0 = seaLevel;

        for (int x = 0; x < w; x++)
        {
            for (int y = y0; y >= 0; y--)
            {
                if (commonSolid[x, y] != ID_AIR)
                    break;

                commonLiquid[x, y] = liquidId;
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    // Step 7: 바닷물 침투 FloodFill
    // seaLevel 라인에서 시작해서, solid가 AIR인 공간만 확장하며 liquid를 채움.
    // ★ seaLevel 위(y > seaLevel)로는 절대 확장하지 않음.
    // seaLevel은 waterHeight "그대로" 사용.
    // ─────────────────────────────────────────────────────────
    private static void FloodFillLiquidFromSeaSurface(ushort[,] commonSolid, ushort[,] commonLiquid, int w, int h, int seaLevel, ushort liquidId)
    {
        int ySeed = seaLevel;

        // 4방
        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        var visited = new bool[w, h];
        var q = new Queue<(int x, int y)>();

        // seaLevel 라인에서 "이미 물인 칸"만 시드로
        for (int x = 0; x < w; x++)
        {
            if (commonLiquid[x, ySeed] == liquidId && commonSolid[x, ySeed] == ID_AIR)
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

                // ★ 핵심: 위로는 절대 안 감 (seaLevel 초과 금지)
                if (ny > seaLevel) continue;

                if (visited[nx, ny]) continue;
                visited[nx, ny] = true;

                if (commonSolid[nx, ny] != ID_AIR) continue;

                if (commonLiquid[nx, ny] == LIQ_NONE)
                    commonLiquid[nx, ny] = liquidId;

                if (commonLiquid[nx, ny] == liquidId)
                    q.Enqueue((nx, ny));
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    // 모래/자갈/점토 변환 (BFS 기반 거리맵) - 물 소스가 commonLiquid 기준
    // ─────────────────────────────────────────────────────────
    private static void ApplySandAndGravelAndClay(WorldGenSettings s, int seed, ushort[,] commonSolid, ushort[,] commonLiquid)
    {
        float tStart = Time.realtimeSinceStartup;

        int w = commonSolid.GetLength(0), h = commonSolid.GetLength(1);
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
            if (commonLiquid[x, y] == LIQ_WATER)
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
            if (commonSolid[x, y] == ID_DIRT)
            {
                int d = distWater[x, y];
                if (d > 0 && d <= maxWaterR)
                    commonSolid[x, y] = ID_SAND;
            }
        }

        // 2단계 A: Rock → Gravel (반경3 내 Water)
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            if (commonSolid[x, y] == ID_ROCK)
            {
                int d = distWater[x, y];
                if (d > 0 && d <= maxWaterR)
                    commonSolid[x, y] = ID_GRAVEL;
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

        // 2단계 B: Rock → Gravel (반경2 내 Dirt, 30%)
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            if (commonSolid[x, y] == ID_ROCK)
            {
                int d = distDirt[x, y];
                if (d > 0 && d <= maxDirtR)
                {
                    if (rand.NextDouble() < 0.30)
                        commonSolid[x, y] = ID_GRAVEL;
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

        // 3단계: Dirt → Gravel/Clay (반경3 내 Sand → 40%/40%)
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            if (commonSolid[x, y] == ID_DIRT)
            {
                int d = distSand[x, y];
                if (d > 0 && d <= maxSandR)
                {
                    double r = rand.NextDouble();
                    if      (r < 0.40) commonSolid[x, y] = ID_GRAVEL;
                    else if (r < 0.80) commonSolid[x, y] = ID_CLAY;
                }
            }
        }

        float tEnd = Time.realtimeSinceStartup;
        Debug.Log($"[WorldGen] ApplySandAndGravelAndClay (BFS) TOTAL: {(tEnd - tStart) * 1000f:F1} ms");
    }

    // ─────────────────────────────────────────────────────────
    // 내부: 점토 클러스터 (Dirt에만 부여)
    // ─────────────────────────────────────────────────────────
    private static void ApplyClayClusters(WorldGenSettings s, int seed, ushort[,] commonSolid)
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
            if ((uint)p.x < w && (uint)p.y < h && commonSolid[p.x, p.y] == ID_DIRT)
                commonSolid[p.x, p.y] = ID_CLAY;
    }

    // ─────────────────────────────────────────────────────────
    // 내부: 광물 클러스터
    // ─────────────────────────────────────────────────────────
    private static void ApplyOreClusters(WorldGenSettings s, int seed, ushort[,] commonSolid)
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
                if ((uint)p.x < w && (uint)p.y < h && commonSolid[p.x, p.y] == ID_ROCK)
                    commonSolid[p.x, p.y] = oreId;
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
    // 내부: 잔디 변형
    // ─────────────────────────────────────────────────────────
    private static ushort GetGrassVariant(int x, int y, ushort[,] commonSolid)
    {
        int w = commonSolid.GetLength(0), h = commonSolid.GetLength(1);

        for (int yy = y + 1; yy < h; yy++)
            if (commonSolid[x, yy] != ID_AIR)
                return ID_DIRT;

        bool up    = (y + 1 < h && (commonSolid[x, y + 1] == ID_AIR));
        bool left  = (x - 1 >= 0 && (commonSolid[x - 1, y] == ID_AIR));
        bool right = (x + 1 < w && (commonSolid[x + 1, y] == ID_AIR));

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
    private static void PlaceTrees(WorldGenSettings s, int seed, ushort[,] commonSolid)
    {
        int w = commonSolid.GetLength(0), h = commonSolid.GetLength(1);
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
            while (y > 0 && commonSolid[x, y] == ID_AIR) y--;
            if (commonSolid[x, y] != GetGrassVariant(x, y, commonSolid)) continue;

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

                    int curr = commonSolid[wx, wy];
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

                    if (canWrite) commonSolid[wx, wy] = (ushort)id;
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    // 내부: 트리 이후 데코
    // ─────────────────────────────────────────────────────────
    private static void PlaceDecorAfterTrees(WorldGenSettings s, int seed, ushort[,] commonSolid)
    {
        int w = commonSolid.GetLength(0), h = commonSolid.GetLength(1);
        var rand = new System.Random(seed ^ 0xDEC0);

        for (int x = 1; x < w - 1; x++)
        for (int y = 1; y < h - 1; y++)
        {
            ushort here = commonSolid[x, y];
            int ya = y + 1;
            if (commonSolid[x, ya] != ID_AIR) continue;

            bool isGrass =
                here == ID_GRASS_LEFT || here == ID_GRASS_TOP || here == ID_GRASS_RIGHT ||
                here == ID_GRASS_TOPLEFT || here == ID_GRASS_TOPRIGHT ||
                here == ID_GRASS_LEFTRIGHT || here == ID_GRASS_TOPLEFTRIGHT;

            if (isGrass)
            {
                double r = rand.NextDouble();
                if      (r < 0.60) commonSolid[x, ya] = ID_PLANT;
                else if (r < 0.75) commonSolid[x, ya] = ID_BUSH;
                else if (r < 0.85) commonSolid[x, ya] = ID_SMALL_STONE_PILE;
                continue;
            }

            if (here == ID_ROCK)
            {
                if (rand.NextDouble() < 0.20)
                    commonSolid[x, ya] = ID_STONE_PILE;
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    // 내부: 자연광 전파 (버킷 기반 Wavefront BFS)
    // ─────────────────────────────────────────────────────────
    private static void PropagateNaturalLight(WorldData world)
    {
        int w = world.bg.GetLength(0);
        int h = world.bg.GetLength(1);

        // 1) 라이트 초기화
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            world.light[x, y] = new LightCell { natural = 0, artificial = 0 };

        // 2) 감쇠량 캐싱 (bg / collidable 기반)
        byte[,] atten = new byte[w, h];
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            byte a = 0;
            if (world.bg[x, y] != ID_AIR) a += 1;
            if (world.IsCollidable(x, y)) a += 2;
            atten[x, y] = a;
        }

        // 3) 거리 맵 (dist = 누적 감쇠량)
        const byte INF = 255;
        byte[,] dist = new byte[w, h];
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            dist[x, y] = INF;

        // 4) 버킷 dist 0~NATURAL_MAX
        var buckets = new List<(int x, int y)>[NATURAL_MAX + 1];
        for (int i = 0; i <= NATURAL_MAX; i++)
            buckets[i] = new List<(int x, int y)>();

        // 5) 최상단 라인(yTop)을 자연광 소스로
        int yTop = h - 1;
        for (int x = 0; x < w; x++)
        {
            dist[x, yTop] = 0;
            buckets[0].Add((x, yTop));
        }

        // 4방향
        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        // 6) Dial-style Dijkstra
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

        // 7) dist -> natural
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            byte d = dist[x, y];
            if (d <= NATURAL_MAX)
            {
                byte val = (byte)(NATURAL_MAX - d);
                var lc = world.light[x, y];
                lc.natural = val;
                world.light[x, y] = lc;
            }
        }
    }
}
