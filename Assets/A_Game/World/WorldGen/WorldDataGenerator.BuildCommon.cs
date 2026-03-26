using UnityEngine;

namespace Game.World
{
    public static partial class WorldDataGenerator
    {
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
            commonMeta = new ushort[w, h];
            bg = new ushort[w, h];
            commonFluid = new ushort[w, h];

            int seaLevel = s.waterHeight;

            int desertStartX = ComputeStartX(seed ^ SALT_DESERT_START, s.desertStartMinX, s.desertStartMaxX, w);
            int snowEndX = ComputeStartX(seed ^ SALT_SNOW_END, s.snowEndMinX, s.snowEndMaxX, w);
            int volcanoCoreStartX = GetVolcanoCoreStartX(s, w);

            Debug.Log($"[WorldGen] BuildCommonAndBg START w={w} h={h} seed={seed} seaLevel={seaLevel} desertStartX={desertStartX} snowEndX={snowEndX} volcanoCoreStartX={volcanoCoreStartX}");

            float[] dirtH = new float[w];
            float[] rockH = new float[w];
            float[] granH = new float[w];
            float[] amphH = new float[w];
            float[] tuffH = new float[w];
            float[] andT = new float[w];
            float[] basT = new float[w];

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

                if (IsVolcanoColumnEnabled(s, seed, x, w))
                {
                    tuffH[x] = ProceduralUtil.FractalPerlin1D(
                        sx + 40000, s.tuffNoiseBaseFrequency, s.tuffNoiseOctaves,
                        s.tuffNoisePersistence, s.tuffNoiseLacunarity,
                        s.tuffBaseHeight, s.tuffRange);

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
                    andT[x] = 0f;
                    basT[x] = 0f;
                }

                float uplift = VolcanoUpliftAtX(s, seed, x, w);
                if (uplift != 0f)
                {
                    dirtH[x] += uplift * 1.00f;
                    rockH[x] += uplift * 0.80f;
                    granH[x] += uplift * 0.60f;
                    amphH[x] += uplift * 0.40f;

                    if (tuffH[x] != 0f) tuffH[x] += uplift * 1.00f;
                    if (andT[x] != 0f) andT[x] = Mathf.Max(0f, andT[x] + uplift * 1.00f);
                    if (basT[x] != 0f) basT[x] = Mathf.Max(0f, basT[x] + uplift * 1.00f);
                }
            }

            StepLog("Step 1 - Noise heights (+Volcano strata -> then uplift)", t0, totalStart);
            t0 = Time.realtimeSinceStartup;

            for (int x = 0; x < w; x++)
            {
                bool volcanoOn = (tuffH[x] != 0f) || (andT[x] != 0f) || (basT[x] != 0f);

                float dirtTop = dirtH[x];
                float rockTop = rockH[x];
                float granTop = granH[x];
                float amphTop = amphH[x];
                float tuffTop = tuffH[x];
                float rockBandBottom = granTop;
                float rockBandTop = rockTop;
                float aT = andT[x];
                float bT = basT[x];

                for (int y = 0; y < h; y++)
                {
                    ushort id = 0;
                    if (y < dirtTop) id = ID_DIRT;
                    if (y < rockTop) id = ID_ROCK;
                    if (y < granTop) id = ID_GRANITE;
                    if (y < amphTop) id = ID_AMPHIBOLITE;

                    if (volcanoOn)
                    {
                        if (id == ID_DIRT && tuffTop > 0f && y >= rockTop && y < tuffTop)
                            id = ID_TUFF;

                        if (id == ID_ROCK && y >= rockBandBottom && y < rockBandTop)
                        {
                            if (aT > 0f && y >= rockBandTop - aT)
                                id = ID_ANDESITE;
                            else if (bT > 0f && y >= rockBandTop - aT - bT)
                                id = ID_BASALT;
                        }
                    }

                    if (id != 0)
                    {
                        commonSolid[x, y] = id;
                        commonMeta[x, y] = 0;
                        bg[x, y] = id;
                        commonFluid[x, y] = FLUID_NONE;
                    }
                }
            }

            StepLog("Step 2 - Layer fill & BG (+Volcano strata)", t0, totalStart);
            t0 = Time.realtimeSinceStartup;

            ApplyVolcanoMagmaPass(s, seed, volcanoCoreStartX, seaLevel, commonSolid, commonMeta, commonFluid);
            StepLog("Step 2.5 - MagmaPass (before SeaColumnFill)", t0, totalStart);
            t0 = Time.realtimeSinceStartup;

            SeaColumnFill(commonSolid, commonFluid, w, h, seaLevel, FLUID_WATER);
            StepLog($"Step 3 - SeaColumnFill (seaLevel={seaLevel})", t0, totalStart);
            t0 = Time.realtimeSinceStartup;

            ApplyOreClusters(s, seed, commonSolid, commonMeta);
            StepLog("Step 4 - Ore clusters", t0, totalStart);
            t0 = Time.realtimeSinceStartup;

            ApplyClayClusters(s, seed, commonSolid, commonMeta);
            StepLog("Step 5 - Clay clusters", t0, totalStart);
            t0 = Time.realtimeSinceStartup;

            bool[,] cave = ProceduralUtil.GenerateNoiseCaveMask(w, h, seed, s);
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                if (!cave[x, y]) continue;
                if (commonFluid[x, y] == FLUID_LAVA) continue;

                commonSolid[x, y] = ID_AIR;
                commonMeta[x, y] = 0;
                commonFluid[x, y] = FLUID_NONE;
            }

            StepLog("Step 6 - Caves carve (noise)", t0, totalStart);
            t0 = Time.realtimeSinceStartup;

            FloodFillFluidFromSeaSurface(commonSolid, commonFluid, w, h, seaLevel, FLUID_WATER);
            StepLog("Step 7 - Water flood fill (no upward)", t0, totalStart);
            t0 = Time.realtimeSinceStartup;

            FloodFillFluidFromAllExistingCells_3Dir(commonSolid, commonFluid, w, h, FLUID_LAVA);
            StepLog("Step 7.1 - Lava flood fill (3-dir, all seeds)", t0, totalStart);
            t0 = Time.realtimeSinceStartup;

            ApplyPyramidPass(desertStartX, volcanoCoreStartX, seaLevel, commonSolid, commonMeta, w, h);
            StepLog("Step 7.5 - PyramidPass (between desert & volcano)", t0, totalStart);
            t0 = Time.realtimeSinceStartup;

            ApplyCrevasseFreezeAndCarvePass(s, seed, commonSolid, commonMeta, commonFluid);
            StepLog("Step 7.6 - CrevasseFreeze+Carve (after floodfill)", t0, totalStart);
            t0 = Time.realtimeSinceStartup;

            StepLog("Step 7.7 - VolcanoPass (reserved)", t0, totalStart);
            t0 = Time.realtimeSinceStartup;

            ApplyDesertPass(s, seed, desertStartX, commonSolid, commonMeta, commonFluid);
            StepLog("Step 7.8 - DesertPass (skip volcano)", t0, totalStart);
            t0 = Time.realtimeSinceStartup;

            ApplySnowPass(s, seed, snowEndX, commonSolid, commonMeta, commonFluid);
            StepLog("Step 7.9 - SnowPass", t0, totalStart);
            t0 = Time.realtimeSinceStartup;

            ApplySandAndGravelAndClay(s, seed, desertStartX, commonSolid, commonMeta, commonFluid);
            StepLog("Step 8 - Sand/Gravel/Clay", t0, totalStart);
            t0 = Time.realtimeSinceStartup;

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
                        commonMeta[x, y] = 0;
                    }
                }
                else
                {
                    if (commonSolid[x, y] != ID_DIRT) continue;
                    if (TryComputeGrassId(x, y, commonSolid, out ushort grassId))
                    {
                        commonSolid[x, y] = grassId;
                        commonMeta[x, y] = 0;
                    }
                }
            }

            StepLog("Step 9 - Grass/FrozenGrass variants", t0, totalStart);
            t0 = Time.realtimeSinceStartup;

            PlaceTrees(s, seed, desertStartX, snowEndX, commonSolid, commonMeta);
            StepLog("Step 10 - Trees/DesertPlants/SnowTrunks", t0, totalStart);
            t0 = Time.realtimeSinceStartup;

            PlaceDecorAfterTrees(s, seed, desertStartX, snowEndX, commonSolid, commonMeta);
            StepLog("Step 11 - Decor", t0, totalStart);

            float end = Time.realtimeSinceStartup;
            Debug.Log($"[WorldGen] BuildCommonAndBg END TOTAL: {(end - totalStart) * 1000f:F1} ms");
        }
    }
}
