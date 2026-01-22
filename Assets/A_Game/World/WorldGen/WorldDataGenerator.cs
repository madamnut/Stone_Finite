// WorldDataGenerator.cs (전체 교체본)
// - ✅ Volcano strata 추가
//   1) Tuff(id=48): 흙/돌 사이에 "절대 높이맵"으로 삽입 (화산 구간만, 완충지대 확률 적용)
//   2) Andesite(id=49): ROCK 띠 상단에서 "역두께"로 ROCK만 치환 (화산 구간만)
//   3) Basalt(id=47): Andesite 아래로 추가 "역두께"로 ROCK만 치환 (화산 구간만)
// - ✅ Volcano uplift 적용 순서 수정
//   * uplift "이전"에 strata 높이/두께를 먼저 만든 뒤, uplift를 strata에도 동일(흙 100%)로 반영
// - ✅ MagmaPass(B) 적용
//   * 주 용암방(렌즈) + 주 용암기둥(베지어 중심선 + 두께 튜브) + 잔가지(트렁크 중심선에서 확률 스폰, 45도 대각선으로 Tuff까지)
//   * 모든 용암 줄기/방/가지: Tuff(id=48) 관통 불가(만나면 종료)
//   * Lava fluid id=2
// - ✅ Lava FloodFill
//   * SeaSurface 기반이 아니라 "맵에 존재하는 모든 Lava"를 seed로 멀티소스 플러드필
//   * 방향: 좌/우/아래 3방향(상방 전파 금지)
// - ✅ Branch curve
//   * 잔가지 끝점은 기존 45도 레이로 결정 (Tuff 직전)
//   * 경로는 주용암처럼 x만 cubic bezier 샘플링
//   * 구불거림은 주용암보다 강하게(컨트롤 오프셋을 steps 기반으로 크게)

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
    private const ushort ID_FLAX_TOP    = 2020;
    private const ushort ID_FLAX_BOTTOM = 2021;

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

    // ✅ Volcano solids (new ids)
    private const ushort ID_BASALT   = 47; // "Basalt"
    private const ushort ID_TUFF     = 48; // "Tuff"
    private const ushort ID_ANDESITE = 49; // "Andesite"

    // ── Fluid IDs (ATT_Fluid.json과 일치) ──
    private const ushort FLUID_NONE  = 0;
    private const ushort FLUID_WATER = 1;
    private const ushort FLUID_LAVA  = 2;

    // ── Light ──
    private const byte NATURAL_MAX = 15;

    // ✅ Seed salt (hex literal은 "숫자"만 가능)
    private const int SALT_DESERT_START = unchecked((int)0x0D35E12);
    private const int SALT_DESERT_PASS  = unchecked((int)0x0D35E12A);
    private const int SALT_SAND_BFS     = unchecked((int)0x0A11CE);
    private const int SALT_DECOR        = unchecked((int)0x00DEC0);

    private const int SALT_SNOW_END     = unchecked((int)0x0510001);  // 임의
    private const int SALT_SNOW_PASS    = unchecked((int)0x0510005A); // 임의

    // ✅ Magma
    private const int SALT_MAGMA = unchecked((int)0x0BADC0DE);

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

        int volcanoCoreStartX = GetVolcanoCoreStartX(s, w);

        Debug.Log($"[WorldGen] BuildCommonAndBg START w={w} h={h} seed={seed} seaLevel={seaLevel} desertStartX={desertStartX} snowEndX={snowEndX} volcanoCoreStartX={volcanoCoreStartX}");

        // Step 1) Noise heights (1D) + Volcano strata maps -> 그 다음 uplift를 레이어별로 반영
        float[] dirtH = new float[w];
        float[] rockH = new float[w];
        float[] granH = new float[w];
        float[] amphH = new float[w];

        // ✅ Volcano strata
        float[] tuffH = new float[w]; // 절대 높이맵 (Dirt/Rock 사이 삽입)
        float[] andT  = new float[w]; // ROCK 상단에서 역두께
        float[] basT  = new float[w]; // Andesite 아래 역두께

        for (int x = 0; x < w; x++)
        {
            float sx = x + seed;

            // (A) uplift 없는 기본 레이어
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

            // (B) uplift 없는 Volcano strata (화산 구간만)
            if (IsVolcanoColumnEnabled(s, seed, x, w))
            {
                // Tuff: 절대 높이맵
                tuffH[x] = ProceduralUtil.FractalPerlin1D(
                    sx + 40000, s.tuffNoiseBaseFrequency, s.tuffNoiseOctaves,
                    s.tuffNoisePersistence, s.tuffNoiseLacunarity,
                    s.tuffBaseHeight, s.tuffRange);

                // Andesite/Basalt: "두께"로 사용 (역두께 침범)
                float a = ProceduralUtil.FractalPerlin1D(
                    sx + 50000, s.andesiteNoiseBaseFrequency, s.andesiteNoiseOctaves,
                    s.andesiteNoisePersistence, s.andesiteNoiseLacunarity,
                    s.andesiteBaseHeight, s.andesiteRange);

                float b = ProceduralUtil.FractalPerlin1D(
                    sx + 60000, s.basaltNoiseBaseFrequency, s.basaltNoiseOctaves,
                    s.basaltNoisePersistence, s.basaltNoiseLacunarity,
                    s.basaltBaseHeight, s.basaltRange);

                andT[x] = Mathf.Max(0f, a);
                basT[x] = Mathf.Max(0f, b);
            }
            else
            {
                tuffH[x] = 0f;
                andT[x]  = 0f;
                basT[x]  = 0f;
            }

            // (C) uplift 계산 후, 레이어별로 적용 (strata는 "흙과 동일 비율" = 1.0)
            float uplift = VolcanoUpliftAtX(s, seed, x, w);
            if (uplift != 0f)
            {
                dirtH[x] += uplift * 1.00f;
                rockH[x] += uplift * 0.80f;
                granH[x] += uplift * 0.60f;
                amphH[x] += uplift * 0.40f;

                if (tuffH[x] != 0f) tuffH[x] += uplift * 1.00f;
                if (andT[x]  != 0f) andT[x]  = Mathf.Max(0f, andT[x] + uplift * 1.00f);
                if (basT[x]  != 0f) basT[x]  = Mathf.Max(0f, basT[x] + uplift * 1.00f);
            }
        }

        StepLog("Step 1 - Noise heights (+Volcano strata -> then uplift)", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 2) Layer fill & BG 확정 (+ Volcano strata 적용)
        for (int x = 0; x < w; x++)
        {
            bool volcanoOn = (tuffH[x] != 0f) || (andT[x] != 0f) || (basT[x] != 0f);

            float dirtTop = dirtH[x];
            float rockTop = rockH[x];
            float granTop = granH[x];
            float amphTop = amphH[x];

            // ✅ Tuff band: "흙/돌 사이" (흙 띠 안에서 tuffTop까지) + rockTop보다 위쪽만
            // - 의도: Dirt -> Tuff -> Rock
            // - 조건: y < tuffTop AND y >= rockTop (즉 흙 띠 중 하단 일부를 tuff로 바꿈)
            float tuffTop = tuffH[x];

            // ✅ Andesite/Basalt invasion only within ROCK band: [granTop .. rockTop)
            float rockBandBottom = granTop;
            float rockBandTop    = rockTop;

            float aT = andT[x];
            float bT = basT[x];

            for (int y = 0; y < h; y++)
            {
                ushort id = 0;
                if (y < dirtTop) id = ID_DIRT;
                if (y < rockTop) id = ID_ROCK;
                if (y < granTop) id = ID_GRANITE;
                if (y < amphTop) id = ID_AMPHIBOLITE;

                // ✅ Volcano strata 적용
                if (volcanoOn)
                {
                    // (1) Tuff: 흙/돌 사이에서만, 절대 높이맵으로 DIRT 치환
                    //     - y가 rockTop보다 "위"에 있으면서
                    //     - y가 tuffTop보다 "아래"면 (즉 [rockTop .. tuffTop) )
                    if (id == ID_DIRT && tuffTop > 0f && y >= rockTop && y < tuffTop)
                    {
                        id = ID_TUFF;
                    }

                    // (2) Andesite/Basalt: ROCK 띠에서만, 상단 역두께로 ROCK만 치환
                    if (id == ID_ROCK && y >= rockBandBottom && y < rockBandTop)
                    {
                        // Andesite: [rockTop - aT .. rockTop)
                        // Basalt  : [rockTop - aT - bT .. rockTop - aT)
                        if (aT > 0f && y >= rockBandTop - aT)
                        {
                            id = ID_ANDESITE;
                        }
                        else if (bT > 0f && y >= rockBandTop - aT - bT)
                        {
                            id = ID_BASALT;
                        }
                    }
                }

                if (id != 0)
                {
                    commonSolid[x, y] = id;
                    commonMeta[x, y]  = 0;
                    bg[x, y]          = id;
                    commonFluid[x, y] = FLUID_NONE;
                }
            }
        }

        StepLog("Step 2 - Layer fill & BG (+Volcano strata)", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // ✅ Step 2.5) Magma (SeaColumnFill 이전)
        ApplyVolcanoMagmaPass(s, seed, volcanoCoreStartX, seaLevel, commonSolid, commonMeta, commonFluid);
        StepLog("Step 2.5 - MagmaPass (before SeaColumnFill)", t0, totalStart);
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

            // ✅ lava가 있는 셀은 동굴로 뚫지 않음
            if (commonFluid[x, y] == FLUID_LAVA) continue;

            commonSolid[x, y] = ID_AIR;
            commonMeta[x, y]  = 0;
            commonFluid[x, y] = FLUID_NONE;
        }

        StepLog("Step 6 - Caves carve (noise)", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 7) Fluid infiltration flood fill (sea와 연결된 공간만, seaLevel 위로 금지)
        FloodFillFluidFromSeaSurface(commonSolid, commonFluid, w, h, seaLevel, FLUID_WATER);
        StepLog("Step 7 - Water flood fill (no upward)", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // ✅ Step 7.1) Lava flood fill (모든 lava seed, 좌/우/아래만)
        FloodFillFluidFromAllExistingCells_3Dir(commonSolid, commonFluid, w, h, FLUID_LAVA);
        StepLog("Step 7.1 - Lava flood fill (3-dir, all seeds)", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 7.5) PyramidPass (✅ desertStart ~ volcanoStart 중앙)
        ApplyPyramidPass(desertStartX, volcanoCoreStartX, seaLevel, commonSolid, commonMeta, w, h);
        StepLog("Step 7.5 - PyramidPass (between desert & volcano)", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // ✅ FloodFill 이후에 Crevasse: (1) 크레바스 구간 물→얼음 (2) 구덩이 뚫기
        ApplyCrevasseFreezeAndCarvePass(s, seed, commonSolid, commonMeta, commonFluid);
        StepLog("Step 7.6 - CrevasseFreeze+Carve (after floodfill)", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 7.7) VolcanoPass (현재 용암 미구현: 지층 융기는 Step1에서 처리됨)
        StepLog("Step 7.7 - VolcanoPass (reserved)", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 7.8) DesertPass (✅ Volcano 구간은 적용 제외)
        ApplyDesertPass(s, seed, desertStartX, commonSolid, commonMeta, commonFluid);
        StepLog("Step 7.8 - DesertPass (skip volcano)", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 7.9) SnowPass (✅ DesertPass 바로 다음)
        ApplySnowPass(s, seed, snowEndX, commonSolid, commonMeta, commonFluid);
        StepLog("Step 7.9 - SnowPass", t0, totalStart);
        t0 = Time.realtimeSinceStartup;

        // Step 8) Sand/Gravel/Clay
        ApplySandAndGravelAndClay(s, seed, desertStartX, commonSolid, commonMeta, commonFluid);
        StepLog("Step 8 - Sand/Gravel/Clay", t0, totalStart);
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

    private static int GetVolcanoCoreStartX(WorldGenSettings s, int w)
    {
        int vw = Mathf.Clamp(s.volcanoWidth, 0, w);
        return Mathf.Clamp(w - vw, 0, w);
    }

    private static bool IsInVolcanoBiome(WorldGenSettings s, int x, int w)
    {
        int coreStart = GetVolcanoCoreStartX(s, w);
        int transLen = Mathf.Max(0, s.volcanoTransitionLen);
        int transStart = coreStart - transLen;
        return (x >= coreStart && x < w) || (x >= transStart && x < coreStart);
    }

    private static bool IsInCrevasseBiome(WorldGenSettings s, int x, int w)
    {
        int coreEnd = Mathf.Clamp(s.crevasseWidth, 0, w);
        int transLen = Mathf.Max(0, s.crevasseTransitionLen);
        int transEnd = coreEnd + transLen;
        return (x >= 0 && x < coreEnd) || (x >= coreEnd && x < transEnd);
    }

    // ✅ Volcano column enable (core=100%, transition=chance) : strata에도 동일 적용
    private static bool IsVolcanoColumnEnabled(WorldGenSettings s, int seed, int x, int w)
    {
        if (s.volcanoWidth <= 0) return false;

        int coreStart = GetVolcanoCoreStartX(s, w);
        int transLen = Mathf.Max(0, s.volcanoTransitionLen);
        int transStart = coreStart - transLen;

        bool inCore = (x >= coreStart && x < w);
        bool inTrans = (!inCore && x >= transStart && x < coreStart);
        if (!inCore && !inTrans) return false;

        if (inCore) return true;

        float chance = Mathf.Clamp01(s.volcanoTransitionChance);
        float r = Mathf.PerlinNoise((seed * 0.001f) + x * 0.0173f, (seed * 0.002f) + 13.37f);
        return (r <= chance);
    }

    // ✅ Volcano uplift U(x) (센터 최대, 양끝 0) + 노이즈
    private static float VolcanoUpliftAtX(WorldGenSettings s, int seed, int x, int w)
    {
        if (s.volcanoWidth <= 0) return 0f;

        int coreStart = GetVolcanoCoreStartX(s, w);
        int transLen = Mathf.Max(0, s.volcanoTransitionLen);
        int transStart = coreStart - transLen;

        bool inCore = (x >= coreStart && x < w);
        bool inTrans = (!inCore && x >= transStart && x < coreStart);
        if (!inCore && !inTrans) return 0f;

        float chance = inCore ? 1f : Mathf.Clamp01(s.volcanoTransitionChance);
        if (inTrans)
        {
            float r = Mathf.PerlinNoise((seed * 0.001f) + x * 0.0173f, (seed * 0.002f) + 13.37f);
            if (r > chance) return 0f;
        }

        int zoneStart = transStart;
        int zoneEnd = w;
        int zoneW = Mathf.Max(2, zoneEnd - zoneStart);

        float t = (x - zoneStart) / (float)(zoneW - 1); // 0..1
        float d = Mathf.Abs(t - 0.5f) * 2f;             // 0..1
        float shape = Mathf.Pow(Mathf.Max(0f, 1f - d), Mathf.Max(0.01f, s.volcanoShapeSharpness));

        float uplift = s.volcanoPeakAddHeight * shape;

        if (s.volcanoDetailAmp != 0f && s.volcanoDetailFreq > 0f)
        {
            float nx = (x + seed) * s.volcanoDetailFreq;
            float p = Mathf.PerlinNoise(nx, 0.1234f + seed * 0.0001f); // 0..1
            float n = (p * 2f - 1f); // -1..1

            float centerBoost = 1f + Mathf.Max(0f, s.volcanoDetailCenterBoost) * (1f - d);
            uplift += n * s.volcanoDetailAmp * centerBoost * shape;
        }

        return uplift;
    }

    // ✅ Dormant volcano magma pass (B)
    private static void ApplyVolcanoMagmaPass(
        WorldGenSettings s, int seed, int volcanoCoreStartX, int seaLevel,
        ushort[,] solid, ushort[,] meta, ushort[,] fluid)
    {
        int w = solid.GetLength(0);
        int h = solid.GetLength(1);

        if (s.volcanoWidth <= 0) return;

        int coreStart = Mathf.Clamp(volcanoCoreStartX, 0, w);
        if (coreStart >= w) return;

        int centerX = (coreStart + (w - 1)) / 2;

        // anchorY: 해당 x에서 가장 높은 Amphibolite
        int anchorY = -1;
        for (int y = h - 1; y >= 0; y--)
        {
            if (solid[centerX, y] == ID_AMPHIBOLITE)
            {
                anchorY = y;
                break;
            }
        }
        if (anchorY < 0) return;

        var rand = new System.Random(seed ^ SALT_MAGMA);

        // 표면 높이(현재 solid 기준)
        int surfaceY = h - 1;
        while (surfaceY > 0 && solid[centerX, surfaceY] == ID_AIR) surfaceY--;

        int stopMargin = 20;
        int stopY = Mathf.Clamp(surfaceY - stopMargin, 0, h - 1);
        if (stopY <= anchorY) stopY = Mathf.Min(anchorY + 1, h - 1);

        // ─────────────────────────────────────────────────────
        // 1) Main chamber (lens)
        // ─────────────────────────────────────────────────────
        int rx = Mathf.Max(1, s.magmaMainRadiusX);
        int ry = Mathf.Max(1, s.magmaMainRadiusY);
        float topSquash = Mathf.Clamp01(s.magmaTopSquash);
        int edgeJitter = Mathf.Max(0, s.magmaEdgeJitter);

        int x0 = Mathf.Clamp(centerX - rx - edgeJitter - 2, 0, w - 1);
        int x1 = Mathf.Clamp(centerX + rx + edgeJitter + 2, 0, w - 1);
        int y0 = Mathf.Clamp(anchorY - ry - edgeJitter - 2, 0, h - 1);
        int y1 = Mathf.Clamp(anchorY + ry + edgeJitter + 2, 0, h - 1);

        for (int x = x0; x <= x1; x++)
        for (int y = y0; y <= y1; y++)
        {
            if (solid[x, y] == ID_TUFF) continue;

            float fx = (x - centerX) / (float)rx;

            float effRy = ry;
            if (y > anchorY && topSquash > 0f)
                effRy = Mathf.Max(1f, ry * (1f - topSquash));

            float fy = (y - anchorY) / effRy;

            float v = fx * fx + fy * fy;

            if (edgeJitter > 0)
            {
                float p = Mathf.PerlinNoise((x + seed) * 0.11f, (y + seed) * 0.11f);
                float j = (p * 2f - 1f) * (edgeJitter / (float)Mathf.Max(rx, ry));
                if (v > 1f + j) continue;
            }
            else
            {
                if (v > 1f) continue;
            }

            solid[x, y] = ID_AIR;
            meta[x, y]  = 0;
            fluid[x, y] = FLUID_LAVA;
        }

        // ─────────────────────────────────────────────────────
        // 2) Main trunk (Bezier centerline + thickness tube)
        // ─────────────────────────────────────────────────────
        int widthStart = Mathf.Max(1, s.magmaTrunkWidthStart);
        int widthEnd   = Mathf.Max(1, s.magmaTrunkWidthEnd);

        int dy = Mathf.Max(1, stopY - anchorY);
        float invDy = 1f / dy;

        int xStart = centerX;

        int maxEndDrift = Mathf.Clamp(Mathf.Max(6, widthStart / 3), 6, 40);
        int xEnd = Mathf.Clamp(xStart + rand.Next(-maxEndDrift, maxEndDrift + 1), coreStart, w - 1);

        int maxCtrl = Mathf.Clamp(Mathf.Max(10, widthStart / 2), 10, 90);
        int xCtrl1 = Mathf.Clamp(xStart + rand.Next(-maxCtrl, maxCtrl + 1), coreStart, w - 1);
        int xCtrl2 = Mathf.Clamp(xEnd   + rand.Next(-maxCtrl, maxCtrl + 1), coreStart, w - 1);

        var trunkPts = new List<(int x, int y)>(dy + 1);

        for (int step = 0; step <= dy; step++)
        {
            int y = anchorY + step;
            if ((uint)y >= (uint)h) break;

            float t = step * invDy; // 0..1
            float omt = 1f - t;

            float xf =
                omt * omt * omt * xStart +
                3f * omt * omt * t * xCtrl1 +
                3f * omt * t * t * xCtrl2 +
                t * t * t * xEnd;

            int tx = Mathf.Clamp(Mathf.RoundToInt(xf), coreStart, w - 1);

            int width = Mathf.RoundToInt(Mathf.Lerp(widthStart, widthEnd, t));
            width = Mathf.Max(1, width);
            int r = Mathf.Max(1, width / 2);

            bool blocked = false;
            for (int xx = tx - r; xx <= tx + r; xx++)
            {
                if ((uint)xx >= (uint)w) continue;
                if (solid[xx, y] == ID_TUFF) { blocked = true; break; }
            }
            if (blocked) break;

            for (int xx = tx - r; xx <= tx + r; xx++)
            for (int yy = y - r; yy <= y + r; yy++)
            {
                if ((uint)xx >= (uint)w || (uint)yy >= (uint)h) continue;
                if (solid[xx, yy] == ID_TUFF) continue;

                int dx2 = xx - tx;
                int dy2 = yy - y;
                if (dx2 * dx2 + dy2 * dy2 > r * r) continue;

                solid[xx, yy] = ID_AIR;
                meta[xx, yy]  = 0;
                fluid[xx, yy] = FLUID_LAVA;
            }

            trunkPts.Add((tx, y));
        }

        if (trunkPts.Count == 0) return;

        // ─────────────────────────────────────────────────────
        // 3) Branches (spawn per trunk step, endpoint=45deg ray, path=cubic bezier stronger)
        // ─────────────────────────────────────────────────────
        float branchChance = Mathf.Clamp01(s.magmaBranchChancePerStep);

        int bStartMin = Mathf.Max(1, s.magmaBranchWidthStartMin);
        int bStartMax = Mathf.Max(bStartMin, s.magmaBranchWidthStartMax);

        int bEndMin = Mathf.Max(1, s.magmaBranchWidthEndMin);
        int bEndMax = Mathf.Max(bEndMin, s.magmaBranchWidthEndMax);

        int cooldown = 4;
        int cd = 0;

        for (int i = 0; i < trunkPts.Count; i++)
        {
            if (cd > 0) { cd--; continue; }
            if (rand.NextDouble() > branchChance) continue;

            var start = trunkPts[i];

            int dir = (rand.NextDouble() < 0.5) ? -1 : 1;

            int startW = (bStartMax > bStartMin) ? rand.Next(bStartMin, bStartMax + 1) : bStartMin;
            int endW   = (bEndMax   > bEndMin)   ? rand.Next(bEndMin,   bEndMax   + 1) : bEndMin;

            var branchPts = new List<(int x, int y)>(64);

            // (A) 끝점 결정: 기존 규칙 그대로 "45도 레이"로 Tuff 직전까지
            int rayX = start.x;
            int rayY = start.y;

            int endXb = start.x;
            int endYb = start.y;

            int nSteps = 0;
            while (true)
            {
                int nx = rayX + dir;
                int ny = rayY + 1;

                if ((uint)nx >= (uint)w || (uint)ny >= (uint)h) break;
                if (solid[nx, ny] == ID_TUFF) break;

                rayX = nx;
                rayY = ny;

                endXb = rayX;
                endYb = rayY;

                nSteps++;
                if (rayY >= h - 2) break;
            }

            if (nSteps <= 0) continue;

            // (B) 경로 생성: y는 1칸씩, x만 cubic bezier (주용암보다 강하게 흔들리게)
            int x0b = start.x;
            int x3b = endXb;

            float inv = 1f / nSteps;

            // “주용암보다 강하게”: steps 기반으로 컨트롤 범위를 크게
            // (가지가 얇아도 구불거림이 충분히 나오게)
            int maxCtrlB = Mathf.Clamp(Mathf.RoundToInt(nSteps * 1.25f), 12, 140);

            // 직선(45도) 기준 x(t) = start.x + dir * round(nSteps * t)
            int xLin1 = x0b + dir * Mathf.RoundToInt(nSteps * (1f / 3f));
            int xLin2 = x0b + dir * Mathf.RoundToInt(nSteps * (2f / 3f));

            int x1b = Mathf.Clamp(xLin1 + rand.Next(-maxCtrlB, maxCtrlB + 1), 0, w - 1);
            int x2b = Mathf.Clamp(xLin2 + rand.Next(-maxCtrlB, maxCtrlB + 1), 0, w - 1);

            int prevX = x0b;

            for (int step = 1; step <= nSteps; step++)
            {
                int y = start.y + step;
                if ((uint)y >= (uint)h) break;

                float t = step * inv;
                float omt = 1f - t;

                float xf =
                    omt * omt * omt * x0b +
                    3f * omt * omt * t * x1b +
                    3f * omt * t * t * x2b +
                    t * t * t * x3b;

                int bx = Mathf.Clamp(Mathf.RoundToInt(xf), 0, w - 1);

                // 너무 순간이동하면 가지가 끊겨 보이므로 "스텝당 변화"는 제한.
                // 구불거림을 살리기 위해 ±2까지 허용(주용암보다 더 출렁임)
                bx = Mathf.Clamp(bx, prevX - 2, prevX + 2);

                if (solid[bx, y] == ID_TUFF) break;

                branchPts.Add((bx, y));
                prevX = bx;

                if (y >= h - 2) break;
            }

            if (branchPts.Count == 0) continue;

            // (C) 두께 테이퍼 carve (기존 유지)
            int n = branchPts.Count;
            for (int k = 0; k < n; k++)
            {
                float t = (n <= 1) ? 1f : (k / (float)(n - 1));
                int width = Mathf.RoundToInt(Mathf.Lerp(startW, endW, t));
                width = Mathf.Max(1, width);
                int r = Mathf.Max(1, width / 2);

                var p = branchPts[k];

                bool blocked = false;
                for (int xx = p.x - r; xx <= p.x + r; xx++)
                {
                    if ((uint)xx >= (uint)w) continue;
                    if (solid[xx, p.y] == ID_TUFF) { blocked = true; break; }
                }
                if (blocked) break;

                for (int xx = p.x - r; xx <= p.x + r; xx++)
                for (int yy = p.y - r; yy <= p.y + r; yy++)
                {
                    if ((uint)xx >= (uint)w || (uint)yy >= (uint)h) continue;
                    if (solid[xx, yy] == ID_TUFF) continue;

                    int dx2 = xx - p.x;
                    int dy2 = yy - p.y;
                    if (dx2 * dx2 + dy2 * dy2 > r * r) continue;

                    solid[xx, yy] = ID_AIR;
                    meta[xx, yy]  = 0;
                    fluid[xx, yy] = FLUID_LAVA;
                }
            }

            cd = cooldown;
        }
    }

    // ✅ Pyramid: desertStartX ~ volcanoStartX 사이의 중앙에 생성
    private static void ApplyPyramidPass(int desertStartX, int volcanoCoreStartX, int seaLevel, ushort[,] commonSolid, ushort[,] commonMeta, int w, int h)
    {
        int x0 = Mathf.Clamp(desertStartX, 0, w - 1);
        int x1 = Mathf.Clamp(volcanoCoreStartX - 1, 0, w - 1);

        if (x1 <= x0) return;

        const int baseWidth = 301;     // 홀수
        int halfBase = baseWidth / 2;
        int height = halfBase + 1;

        int centerX = (x0 + x1) / 2;
        int baseY = seaLevel - 50;

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

    // ✅ FloodFill 이후: 크레바스 구간 물→얼음 + 구덩이 뚫기
    private static void ApplyCrevasseFreezeAndCarvePass(WorldGenSettings s, int seed, ushort[,] solid, ushort[,] meta, ushort[,] fluid)
    {
        int w = solid.GetLength(0);
        int h = solid.GetLength(1);

        if (s.crevasseWidth <= 0) return;

        int coreEnd = Mathf.Clamp(s.crevasseWidth, 0, w);
        int transLen = Mathf.Max(0, s.crevasseTransitionLen);
        float transChance = Mathf.Clamp01(s.crevasseTransitionChance);
        int transEnd = Mathf.Min(w, coreEnd + transLen);

        // (1) Freeze: air+water -> ice (core=100%, trans=chance)
        for (int x = 0; x < transEnd; x++)
        {
            bool inCore = (x < coreEnd);
            bool inTrans = (!inCore && x >= coreEnd && x < transEnd);
            if (!inCore && !inTrans) continue;

            bool doThisColumn = true;
            if (inTrans)
            {
                float r = Mathf.PerlinNoise((seed * 0.002f) + x * 0.0191f, 0.777f);
                doThisColumn = (r <= transChance);
            }
            if (!doThisColumn) continue;

            for (int y = 0; y < h; y++)
            {
                if (solid[x, y] != ID_AIR) continue;
                if (fluid[x, y] != FLUID_WATER) continue;

                solid[x, y] = ID_ICE_CELL;
                meta[x, y]  = 0;
                fluid[x, y] = FLUID_NONE;
            }
        }

        // (2) Carve: 표면부터 아래로 depth(x) 만큼 AIR로 뚫기 (물/얼음 포함 제거)
        for (int x = 0; x < transEnd; x++)
        {
            bool inCore = (x < coreEnd);
            bool inTrans = (!inCore && x >= coreEnd && x < transEnd);
            if (!inCore && !inTrans) continue;

            bool doThisColumn = true;
            if (inTrans)
            {
                float r = Mathf.PerlinNoise((seed * 0.003f) + x * 0.0217f, 0.313f);
                doThisColumn = (r <= transChance);
            }
            if (!doThisColumn) continue;

            int depth = ComputeCrevasseDepthAtX(s, seed, x, transEnd);
            if (depth <= 0) continue;

            int surfaceY = h - 1;
            while (surfaceY > 0 && solid[x, surfaceY] == ID_AIR) surfaceY--;

            if (surfaceY <= 0) continue;

            int yMin = Mathf.Max(0, surfaceY - depth);
            for (int y = surfaceY; y >= yMin; y--)
            {
                solid[x, y] = ID_AIR;
                meta[x, y]  = 0;
                fluid[x, y] = FLUID_NONE;
            }
        }
    }

    private static int ComputeCrevasseDepthAtX(WorldGenSettings s, int seed, int x, int zoneW)
    {
        if (zoneW <= 1) return 0;

        int maxDepth = Mathf.Max(0, s.crevasseMaxDepth);
        if (maxDepth == 0) return 0;

        float center = (zoneW - 1) * 0.5f;
        float half = Mathf.Max(1f, center);

        float dNorm = Mathf.Abs(x - center) / half; // 0..1
        float baseShape = Mathf.Max(0f, 1f - dNorm);

        float curve = (x <= center) ? Mathf.Max(0.01f, s.crevasseLeftCurve) : Mathf.Max(0.01f, s.crevasseRightCurve);
        float shaped = Mathf.Pow(baseShape, curve);

        float jag = 0f;
        if (s.crevasseRidgeJagAmp != 0f && s.crevasseRidgeJagFreq > 0f)
        {
            float p = Mathf.PerlinNoise((x + seed) * s.crevasseRidgeJagFreq, 0.9182f);
            jag = (p * 2f - 1f) * s.crevasseRidgeJagAmp;
        }

        float depthF = maxDepth * shaped + jag;
        int depth = Mathf.Clamp(Mathf.RoundToInt(depthF), 0, maxDepth);
        return depth;
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
            // ✅ Volcano 구간에서는 사막 패스 금지
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

                // ✅ 이미 다른 유체(예: lava)가 있으면 바다 물이 관통/덮어쓰지 않음
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

                // ✅ lava는 절대 물로 바꾸지 않음 + 물 전파도 차단
                if (commonFluid[nx, ny] == FLUID_LAVA) continue;

                if (commonFluid[nx, ny] == FLUID_NONE)
                    commonFluid[nx, ny] = fluidId;

                if (commonFluid[nx, ny] == fluidId)
                    q.Enqueue((nx, ny));
            }
        }
    }

    // ✅ Lava FloodFill: "현재 존재하는 모든 lava"를 seed로, 좌/우/아래만 확산
    private static void FloodFillFluidFromAllExistingCells_3Dir(
        ushort[,] commonSolid, ushort[,] commonFluid,
        int w, int h,
        ushort fluidId
    )
    {
        // 좌/우/아래 (상방 전파 금지)
        int[] dx = { -1, 1, 0 };
        int[] dy = {  0, 0,-1 };

        var visited = new bool[w, h];
        var q = new Queue<(int x, int y)>();

        // seed: 맵 전체에서 기존 fluidId를 전부 큐에 넣음
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

                // 다른 유체(예: 물)로는 절대 침범/덮어쓰기 금지
                if (commonFluid[nx, ny] != FLUID_NONE && commonFluid[nx, ny] != fluidId) continue;

                if (commonFluid[nx, ny] == FLUID_NONE)
                    commonFluid[nx, ny] = fluidId;

                if (commonFluid[nx, ny] == fluidId)
                    q.Enqueue((nx, ny));
            }
        }
    }

    // 이하: 너가 붙여준 기존 코드 그대로 (ApplySandAndGravelAndClay ~ PropagateNaturalLight)
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
            // ✅ Volcano 구간은 사암화 제외
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

            // Desert: cactus/agave (✅ Volcano 구간 제외)
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

            // Desert decor (✅ Volcano 구간 제외)
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
                // else: 45% 중 남은 45%? → 실제로는
                // flax 실패한 70% 중에서
                // 30 + 15 + 10 = 55%, 나머지 15%는 공백

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
