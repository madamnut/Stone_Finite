// WorldDataGenerator.cs (?꾩껜 援먯껜蹂?
// - ??Volcano strata 異붽?
//   1) Tuff(id=48): ?????ъ씠??"?덈? ?믪씠留??쇰줈 ?쎌엯 (?붿궛 援ш컙留? ?꾩땐吏? ?뺣쪧 ?곸슜)
//   2) Andesite(id=49): ROCK ???곷떒?먯꽌 "??몢猿?濡?ROCK留?移섑솚 (?붿궛 援ш컙留?
//   3) Basalt(id=47): Andesite ?꾨옒濡?異붽? "??몢猿?濡?ROCK留?移섑솚 (?붿궛 援ш컙留?
// - ??Volcano uplift ?곸슜 ?쒖꽌 ?섏젙
//   * uplift "?댁쟾"??strata ?믪씠/?먭퍡瑜?癒쇱? 留뚮뱺 ?? uplift瑜?strata?먮룄 ?숈씪(??100%)濡?諛섏쁺
// - ??MagmaPass(B) ?곸슜
//   * 二??⑹븫諛??뚯쫰) + 二??⑹븫湲곕뫁(踰좎???以묒떖??+ ?먭퍡 ?쒕툕) + ?붽?吏(?몃쟻??以묒떖?좎뿉???뺣쪧 ?ㅽ룿, 45???媛곸꽑?쇰줈 Tuff源뚯?)
//   * 紐⑤뱺 ?⑹븫 以꾧린/諛?媛吏: Tuff(id=48) 愿??遺덇?(留뚮굹硫?醫낅즺)
//   * Lava fluid id=2
// - ??Lava FloodFill
//   * SeaSurface 湲곕컲???꾨땲??"留듭뿉 議댁옱?섎뒗 紐⑤뱺 Lava"瑜?seed濡?硫?곗냼???뚮윭?쒗븘
//   * 諛⑺뼢: 醫????꾨옒 3諛⑺뼢(?곷갑 ?꾪뙆 湲덉?)
// - ??Branch curve
//   * ?붽?吏 ?앹젏? 湲곗〈 45???덉씠濡?寃곗젙 (Tuff 吏곸쟾)
//   * 寃쎈줈??二쇱슜?붿쿂??x留?cubic bezier ?섑뵆留?
//   * 援щ텋嫄곕┝? 二쇱슜?붾낫??媛뺥븯寃?而⑦듃濡??ㅽ봽?뗭쓣 steps 湲곕컲?쇰줈 ?ш쾶)

using System;
using System.Collections.Generic;
using UnityEngine;

using Game.Data;
using Game.Core;

namespace Game.World
{
    public static partial class WorldDataGenerator
    {
        // ?? Solid IDs (ATT_Solid.json怨??쇱튂) ??
        private const ushort ID_AIR  = 0;
        private const ushort ID_ROCK = 1;
        private const ushort ID_DIRT = 2;
    
        // ??Grass 遺꾨━: id=3~9, meta=0 怨좎젙
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
        // NOTE: Frozen Dirt ?ㅼ젣 ID??留욊쾶 ?섏젙 ?꾩슂 (?ш린??46 媛??
        private const ushort ID_FROZEN_DIRT = 46;
    
        // Frozen Grass 遺꾨━: id=37~43
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
    
        // ?? Fluid IDs (ATT_Fluid.json怨??쇱튂) ??
        private const ushort FLUID_NONE  = 0;
        private const ushort FLUID_WATER = 1;
        private const ushort FLUID_LAVA  = 2;
    
        // ?? Light ??
        private const byte NATURAL_MAX = 15;
    
        // ??Seed salt (hex literal? "?レ옄"留?媛??
        private const int SALT_DESERT_START = unchecked((int)0x0D35E12);
        private const int SALT_DESERT_PASS  = unchecked((int)0x0D35E12A);
        private const int SALT_SAND_BFS     = unchecked((int)0x0A11CE);
        private const int SALT_DECOR        = unchecked((int)0x00DEC0);
    
        private const int SALT_SNOW_END     = unchecked((int)0x0510001);  // ?꾩쓽
        private const int SALT_SNOW_PASS    = unchecked((int)0x0510005A); // ?꾩쓽
    
        // ??Magma
        private const int SALT_MAGMA = unchecked((int)0x0BADC0DE);
    
        // 濡쒓렇 ?좏떥
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
    
            // Step 1) Noise heights (1D) + Volcano strata maps -> 洹??ㅼ쓬 uplift瑜??덉씠?대퀎濡?諛섏쁺
            float[] dirtH = new float[w];
            float[] rockH = new float[w];
            float[] granH = new float[w];
            float[] amphH = new float[w];
    
            // ??Volcano strata
            float[] tuffH = new float[w]; // ?덈? ?믪씠留?(Dirt/Rock ?ъ씠 ?쎌엯)
            float[] andT  = new float[w]; // ROCK ?곷떒?먯꽌 ??몢猿?
            float[] basT  = new float[w]; // Andesite ?꾨옒 ??몢猿?
    
            for (int x = 0; x < w; x++)
            {
                float sx = x + seed;
    
                // (A) uplift ?녿뒗 湲곕낯 ?덉씠??
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
    
                // (B) uplift ?녿뒗 Volcano strata (?붿궛 援ш컙留?
                if (IsVolcanoColumnEnabled(s, seed, x, w))
                {
                    // Tuff: ?덈? ?믪씠留?
                    tuffH[x] = ProceduralUtil.FractalPerlin1D(
                        sx + 40000, s.tuffNoiseBaseFrequency, s.tuffNoiseOctaves,
                        s.tuffNoisePersistence, s.tuffNoiseLacunarity,
                        s.tuffBaseHeight, s.tuffRange);
    
                    // Andesite/Basalt: "?먭퍡"濡??ъ슜 (??몢猿?移⑤쾾)
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
    
                // (C) uplift 怨꾩궛 ?? ?덉씠?대퀎濡??곸슜 (strata??"?숆낵 ?숈씪 鍮꾩쑉" = 1.0)
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
    
            // Step 2) Layer fill & BG ?뺤젙 (+ Volcano strata ?곸슜)
            for (int x = 0; x < w; x++)
            {
                bool volcanoOn = (tuffH[x] != 0f) || (andT[x] != 0f) || (basT[x] != 0f);
    
                float dirtTop = dirtH[x];
                float rockTop = rockH[x];
                float granTop = granH[x];
                float amphTop = amphH[x];
    
                // ??Tuff band: "?????ъ씠" (?????덉뿉??tuffTop源뚯?) + rockTop蹂대떎 ?꾩そ留?
                // - ?섎룄: Dirt -> Tuff -> Rock
                // - 議곌굔: y < tuffTop AND y >= rockTop (利?????以??섎떒 ?쇰?瑜?tuff濡?諛붽퓞)
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
    
                    // ??Volcano strata ?곸슜
                    if (volcanoOn)
                    {
                        // (1) Tuff: ?????ъ씠?먯꽌留? ?덈? ?믪씠留듭쑝濡?DIRT 移섑솚
                        //     - y媛 rockTop蹂대떎 "?????덉쑝硫댁꽌
                        //     - y媛 tuffTop蹂대떎 "?꾨옒"硫?(利?[rockTop .. tuffTop) )
                        if (id == ID_DIRT && tuffTop > 0f && y >= rockTop && y < tuffTop)
                        {
                            id = ID_TUFF;
                        }
    
                        // (2) Andesite/Basalt: ROCK ?좎뿉?쒕쭔, ?곷떒 ??몢猿섎줈 ROCK留?移섑솚
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
    
            // ??Step 2.5) Magma (SeaColumnFill ?댁쟾)
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
    
                // ??lava媛 ?덈뒗 ?? ?숆뎬濡??レ? ?딆쓬
                if (commonFluid[x, y] == FLUID_LAVA) continue;
    
                commonSolid[x, y] = ID_AIR;
                commonMeta[x, y]  = 0;
                commonFluid[x, y] = FLUID_NONE;
            }
    
            StepLog("Step 6 - Caves carve (noise)", t0, totalStart);
            t0 = Time.realtimeSinceStartup;
    
            // Step 7) Fluid infiltration flood fill (sea? ?곌껐??怨듦컙留? seaLevel ?꾨줈 湲덉?)
            FloodFillFluidFromSeaSurface(commonSolid, commonFluid, w, h, seaLevel, FLUID_WATER);
            StepLog("Step 7 - Water flood fill (no upward)", t0, totalStart);
            t0 = Time.realtimeSinceStartup;
    
            // ??Step 7.1) Lava flood fill (紐⑤뱺 lava seed, 醫????꾨옒留?
            FloodFillFluidFromAllExistingCells_3Dir(commonSolid, commonFluid, w, h, FLUID_LAVA);
            StepLog("Step 7.1 - Lava flood fill (3-dir, all seeds)", t0, totalStart);
            t0 = Time.realtimeSinceStartup;
    
            // Step 7.5) PyramidPass (??desertStart ~ volcanoStart 以묒븰)
            ApplyPyramidPass(desertStartX, volcanoCoreStartX, seaLevel, commonSolid, commonMeta, w, h);
            StepLog("Step 7.5 - PyramidPass (between desert & volcano)", t0, totalStart);
            t0 = Time.realtimeSinceStartup;
    
            // ??FloodFill ?댄썑??Crevasse: (1) ?щ젅諛붿뒪 援ш컙 臾쇄넂?쇱쓬 (2) 援щ뜦???リ린
            ApplyCrevasseFreezeAndCarvePass(s, seed, commonSolid, commonMeta, commonFluid);
            StepLog("Step 7.6 - CrevasseFreeze+Carve (after floodfill)", t0, totalStart);
            t0 = Time.realtimeSinceStartup;
    
            // Step 7.7) VolcanoPass (?꾩옱 ?⑹븫 誘멸뎄?? 吏痢??듦린??Step1?먯꽌 泥섎━??
            StepLog("Step 7.7 - VolcanoPass (reserved)", t0, totalStart);
            t0 = Time.realtimeSinceStartup;
    
            // Step 7.8) DesertPass (??Volcano 援ш컙? ?곸슜 ?쒖쇅)
            ApplyDesertPass(s, seed, desertStartX, commonSolid, commonMeta, commonFluid);
            StepLog("Step 7.8 - DesertPass (skip volcano)", t0, totalStart);
            t0 = Time.realtimeSinceStartup;
    
            // Step 7.9) SnowPass (??DesertPass 諛붾줈 ?ㅼ쓬)
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
