// WorldDataGenerator.cs (?ÑÏ≤¥ ÍµêÏ≤¥Î≥?
// - ??Volcano strata Ï∂îÍ?
//   1) Tuff(id=48): ?????¨Ïù¥??"?àÎ? ?íÏù¥Îß??ºÎ°ú ?ΩÏûÖ (?îÏÇ∞ Íµ¨Í∞ÑÎß? ?ÑÏ∂©ÏßÄ?Ä ?ïÎ•† ?ÅÏö©)
//   2) Andesite(id=49): ROCK ???ÅÎã®?êÏÑú "??ëêÍª?Î°?ROCKÎß?ÏπòÌôò (?îÏÇ∞ Íµ¨Í∞ÑÎß?
//   3) Basalt(id=47): Andesite ?ÑÎûòÎ°?Ï∂îÍ? "??ëêÍª?Î°?ROCKÎß?ÏπòÌôò (?îÏÇ∞ Íµ¨Í∞ÑÎß?
// - ??Volcano uplift ?ÅÏö© ?úÏÑú ?òÏ†ï
//   * uplift "?¥Ï†Ñ"??strata ?íÏù¥/?êÍªòÎ•?Î®ºÏ? ÎßåÎì† ?? upliftÎ•?strata?êÎèÑ ?ôÏùº(??100%)Î°?Î∞òÏòÅ
// - ??MagmaPass(B) ?ÅÏö©
//   * Ï£??©ÏïîÎ∞??åÏ¶à) + Ï£??©ÏïîÍ∏∞Îë•(Î≤†Ï???Ï§ëÏã¨??+ ?êÍªò ?úÎ∏å) + ?îÍ?ÏßÄ(?∏Î†Å??Ï§ëÏã¨?†Ïóê???ïÎ•† ?§Ìè∞, 45???ÄÍ∞ÅÏÑ†?ºÎ°ú TuffÍπåÏ?)
//   * Î™®Îì† ?©Ïïî Ï§ÑÍ∏∞/Î∞?Í∞ÄÏßÄ: Tuff(id=48) Í¥Ä??Î∂àÍ?(ÎßåÎÇòÎ©?Ï¢ÖÎ£å)
//   * Lava fluid id=2
// - ??Lava FloodFill
//   * SeaSurface Í∏∞Î∞ò???ÑÎãà??"ÎßµÏóê Ï°¥Ïû¨?òÎäî Î™®Îì† Lava"Î•?seedÎ°?Î©Ä?∞ÏÜå???åÎü¨?úÌïÑ
//   * Î∞©Ìñ•: Ï¢????ÑÎûò 3Î∞©Ìñ•(?ÅÎ∞© ?ÑÌåå Í∏àÏ?)
// - ??Branch curve
//   * ?îÍ?ÏßÄ ?ùÏ†ê?Ä Í∏∞Ï°¥ 45???àÏù¥Î°?Í≤∞Ï†ï (Tuff ÏßÅÏ†Ñ)
//   * Í≤ΩÎ°ú??Ï£ºÏö©?îÏ≤ò??xÎß?cubic bezier ?òÌîåÎß?
//   * Íµ¨Î∂àÍ±∞Î¶º?Ä Ï£ºÏö©?îÎ≥¥??Í∞ïÌïòÍ≤?Ïª®Ìä∏Î°??§ÌîÑ?ãÏùÑ steps Í∏∞Î∞ò?ºÎ°ú ?¨Í≤å)

using System;
using System.Collections.Generic;
using UnityEngine;

using Game.Data;

namespace Game.World
{
    public static partial class WorldDataGenerator
    {
        // ?Ä?Ä Solid IDs (ATT_Solid.jsonÍ≥??ºÏπò) ?Ä?Ä
        private const ushort ID_AIR  = 0;
        private const ushort ID_ROCK = 1;
        private const ushort ID_DIRT = 2;
    
        // ??Grass Î∂ÑÎ¶¨: id=3~9, meta=0 Í≥†Ï†ï
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
    
        // ??Desert decor
        private const ushort ID_DEAD_BUSH = 2006;
    
        // ??Agave 3x2 tiles
        private const ushort ID_AGAVE_0 = 2007;
        private const ushort ID_AGAVE_1 = 2008;
        private const ushort ID_AGAVE_2 = 2009;
        private const ushort ID_AGAVE_3 = 2010;
        private const ushort ID_AGAVE_4 = 2011;
        private const ushort ID_AGAVE_5 = 2012;
    
        private const ushort ID_CACTUS = 2013;
    
        // ??Snow biome decor
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
    
        // ??Sandstone + Pyramid brick
        private const ushort ID_SANDSTONE       = 35; // "SandStone"
        private const ushort ID_SANDSTONE_BRICK = 36; // "SandStone Brick Cell"
    
        // ??Snow biome solids
        // NOTE: Frozen Dirt ?§Ï†ú ID??ÎßûÍ≤å ?òÏ†ï ?ÑÏöî (?¨Í∏∞??46 Í∞Ä??
        private const ushort ID_FROZEN_DIRT = 46;
    
        // Frozen Grass Î∂ÑÎ¶¨: id=37~43
        private const ushort ID_FROZEN_GRASS_TOP          = 37;
        private const ushort ID_FROZEN_GRASS_LEFT         = 38;
        private const ushort ID_FROZEN_GRASS_RIGHT        = 39;
        private const ushort ID_FROZEN_GRASS_TOPLEFT      = 40;
        private const ushort ID_FROZEN_GRASS_TOPRIGHT     = 41;
        private const ushort ID_FROZEN_GRASS_LEFTRIGHT    = 42;
        private const ushort ID_FROZEN_GRASS_TOPLEFTRIGHT = 43;
    
        private const ushort ID_ICE_CELL  = 44;
        private const ushort ID_SNOW_CELL = 45;
    
        // ??Volcano solids (new ids)
        private const ushort ID_BASALT   = 47; // "Basalt"
        private const ushort ID_TUFF     = 48; // "Tuff"
        private const ushort ID_ANDESITE = 49; // "Andesite"
    
        // ?Ä?Ä Fluid IDs (ATT_Fluid.jsonÍ≥??ºÏπò) ?Ä?Ä
        private const ushort FLUID_NONE  = 0;
        private const ushort FLUID_WATER = 1;
        private const ushort FLUID_LAVA  = 2;
    
        // ?Ä?Ä Light ?Ä?Ä
        private const byte NATURAL_MAX = 15;
    
        // ??Seed salt (hex literal?Ä "?´Ïûê"Îß?Í∞Ä??
        private const int SALT_DESERT_START = unchecked((int)0x0D35E12);
        private const int SALT_DESERT_PASS  = unchecked((int)0x0D35E12A);
        private const int SALT_SAND_BFS     = unchecked((int)0x0A11CE);
        private const int SALT_DECOR        = unchecked((int)0x00DEC0);
    
        private const int SALT_SNOW_END     = unchecked((int)0x0510001);  // ?ÑÏùò
        private const int SALT_SNOW_PASS    = unchecked((int)0x0510005A); // ?ÑÏùò
    
        // ??Magma
        private const int SALT_MAGMA = unchecked((int)0x0BADC0DE);
    
        // Î°úÍ∑∏ ?†Ìã∏
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
    
            // Step 1) Noise heights (1D) + Volcano strata maps -> Í∑??§Ïùå upliftÎ•??àÏù¥?¥Î≥ÑÎ°?Î∞òÏòÅ
            float[] dirtH = new float[w];
            float[] rockH = new float[w];
            float[] granH = new float[w];
            float[] amphH = new float[w];
    
            // ??Volcano strata
            float[] tuffH = new float[w]; // ?àÎ? ?íÏù¥Îß?(Dirt/Rock ?¨Ïù¥ ?ΩÏûÖ)
            float[] andT  = new float[w]; // ROCK ?ÅÎã®?êÏÑú ??ëêÍª?
            float[] basT  = new float[w]; // Andesite ?ÑÎûò ??ëêÍª?
    
            for (int x = 0; x < w; x++)
            {
                float sx = x + seed;
    
                // (A) uplift ?ÜÎäî Í∏∞Î≥∏ ?àÏù¥??
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
    
                // (B) uplift ?ÜÎäî Volcano strata (?îÏÇ∞ Íµ¨Í∞ÑÎß?
                if (IsVolcanoColumnEnabled(s, seed, x, w))
                {
                    // Tuff: ?àÎ? ?íÏù¥Îß?
                    tuffH[x] = ProceduralUtil.FractalPerlin1D(
                        sx + 40000, s.tuffNoiseBaseFrequency, s.tuffNoiseOctaves,
                        s.tuffNoisePersistence, s.tuffNoiseLacunarity,
                        s.tuffBaseHeight, s.tuffRange);
    
                    // Andesite/Basalt: "?êÍªò"Î°??¨Ïö© (??ëêÍª?Ïπ®Î≤î)
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
    
                // (C) uplift Í≥ÑÏÇ∞ ?? ?àÏù¥?¥Î≥ÑÎ°??ÅÏö© (strata??"?ôÍ≥º ?ôÏùº ÎπÑÏú®" = 1.0)
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
    
            // Step 2) Layer fill & BG ?ïÏ†ï (+ Volcano strata ?ÅÏö©)
            for (int x = 0; x < w; x++)
            {
                bool volcanoOn = (tuffH[x] != 0f) || (andT[x] != 0f) || (basT[x] != 0f);
    
                float dirtTop = dirtH[x];
                float rockTop = rockH[x];
                float granTop = granH[x];
                float amphTop = amphH[x];
    
                // ??Tuff band: "?????¨Ïù¥" (?????àÏóê??tuffTopÍπåÏ?) + rockTopÎ≥¥Îã§ ?ÑÏ™ΩÎß?
                // - ?òÎèÑ: Dirt -> Tuff -> Rock
                // - Ï°∞Í±¥: y < tuffTop AND y >= rockTop (Ï¶?????Ï§??òÎã® ?ºÎ?Î•?tuffÎ°?Î∞îÍøà)
                float tuffTop = tuffH[x];
    
                // ??Andesite/Basalt invasion only within ROCK band: [granTop .. rockTop)
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
    
                    // ??Volcano strata ?ÅÏö©
                    if (volcanoOn)
                    {
                        // (1) Tuff: ?????¨Ïù¥?êÏÑúÎß? ?àÎ? ?íÏù¥ÎßµÏúºÎ°?DIRT ÏπòÌôò
                        //     - yÍ∞Ä rockTopÎ≥¥Îã§ "?????àÏúºÎ©¥ÏÑú
                        //     - yÍ∞Ä tuffTopÎ≥¥Îã§ "?ÑÎûò"Î©?(Ï¶?[rockTop .. tuffTop) )
                        if (id == ID_DIRT && tuffTop > 0f && y >= rockTop && y < tuffTop)
                        {
                            id = ID_TUFF;
                        }
    
                        // (2) Andesite/Basalt: ROCK ?†Ïóê?úÎßå, ?ÅÎã® ??ëêÍªòÎ°ú ROCKÎß?ÏπòÌôò
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
    
            // ??Step 2.5) Magma (SeaColumnFill ?¥Ï†Ñ)
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
    
                // ??lavaÍ∞Ä ?àÎäî ?Ä?Ä ?ôÍµ¥Î°??´Ï? ?äÏùå
                if (commonFluid[x, y] == FLUID_LAVA) continue;
    
                commonSolid[x, y] = ID_AIR;
                commonMeta[x, y]  = 0;
                commonFluid[x, y] = FLUID_NONE;
            }
    
            StepLog("Step 6 - Caves carve (noise)", t0, totalStart);
            t0 = Time.realtimeSinceStartup;
    
            // Step 7) Fluid infiltration flood fill (sea?Ä ?∞Í≤∞??Í≥µÍ∞ÑÎß? seaLevel ?ÑÎ°ú Í∏àÏ?)
            FloodFillFluidFromSeaSurface(commonSolid, commonFluid, w, h, seaLevel, FLUID_WATER);
            StepLog("Step 7 - Water flood fill (no upward)", t0, totalStart);
            t0 = Time.realtimeSinceStartup;
    
            // ??Step 7.1) Lava flood fill (Î™®Îì† lava seed, Ï¢????ÑÎûòÎß?
            FloodFillFluidFromAllExistingCells_3Dir(commonSolid, commonFluid, w, h, FLUID_LAVA);
            StepLog("Step 7.1 - Lava flood fill (3-dir, all seeds)", t0, totalStart);
            t0 = Time.realtimeSinceStartup;
    
            // Step 7.5) PyramidPass (??desertStart ~ volcanoStart Ï§ëÏïô)
            ApplyPyramidPass(desertStartX, volcanoCoreStartX, seaLevel, commonSolid, commonMeta, w, h);
            StepLog("Step 7.5 - PyramidPass (between desert & volcano)", t0, totalStart);
            t0 = Time.realtimeSinceStartup;
    
            // ??FloodFill ?¥ÌõÑ??Crevasse: (1) ?¨Î†àÎ∞îÏä§ Íµ¨Í∞Ñ Î¨º‚Üí?ºÏùå (2) Íµ¨Îç©???´Í∏∞
            ApplyCrevasseFreezeAndCarvePass(s, seed, commonSolid, commonMeta, commonFluid);
            StepLog("Step 7.6 - CrevasseFreeze+Carve (after floodfill)", t0, totalStart);
            t0 = Time.realtimeSinceStartup;
    
            // Step 7.7) VolcanoPass (?ÑÏû¨ ?©Ïïî ÎØ∏Íµ¨?? ÏßÄÏ∏??µÍ∏∞??Step1?êÏÑú Ï≤òÎ¶¨??
            StepLog("Step 7.7 - VolcanoPass (reserved)", t0, totalStart);
            t0 = Time.realtimeSinceStartup;
    
            // Step 7.8) DesertPass (??Volcano Íµ¨Í∞Ñ?Ä ?ÅÏö© ?úÏô∏)
            ApplyDesertPass(s, seed, desertStartX, commonSolid, commonMeta, commonFluid);
            StepLog("Step 7.8 - DesertPass (skip volcano)", t0, totalStart);
            t0 = Time.realtimeSinceStartup;
    
            // Step 7.9) SnowPass (??DesertPass Î∞îÎ°ú ?§Ïùå)
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
    
    }
}
