using System;
using System.Collections.Generic;
using UnityEngine;


namespace Game.World
{
    public static partial class WorldDataGenerator
    {
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
    
        // ??Volcano column enable (core=100%, transition=chance) : strata?êÎèÑ ?ôÏùº ?ÅÏö©
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
    
        // ??Volcano uplift U(x) (?ºÌÑ∞ ÏµúÎ?, ?ëÎÅù 0) + ?∏Ïù¥Ï¶?
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
    
        // ??Dormant volcano magma pass (B)
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
    
            // anchorY: ?¥Îãπ x?êÏÑú Í∞Ä???íÏ? Amphibolite
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
    
            // ?úÎ©¥ ?íÏù¥(?ÑÏû¨ solid Í∏∞Ï?)
            int surfaceY = h - 1;
            while (surfaceY > 0 && solid[centerX, surfaceY] == ID_AIR) surfaceY--;
    
            int stopMargin = 20;
            int stopY = Mathf.Clamp(surfaceY - stopMargin, 0, h - 1);
            if (stopY <= anchorY) stopY = Mathf.Min(anchorY + 1, h - 1);
    
            // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
            // 1) Main chamber (lens)
            // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
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
    
            // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
            // 2) Main trunk (Bezier centerline + thickness tube)
            // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
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
    
            // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
            // 3) Branches (spawn per trunk step, endpoint=45deg ray, path=cubic bezier stronger)
            // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
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
    
                // (A) ?ùÏ†ê Í≤∞Ï†ï: Í∏∞Ï°¥ Í∑úÏπô Í∑∏Î?Î°?"45???àÏù¥"Î°?Tuff ÏßÅÏ†ÑÍπåÏ?
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
    
                // (B) Í≤ΩÎ°ú ?ùÏÑ±: y??1Ïπ∏Ïî©, xÎß?cubic bezier (Ï£ºÏö©?îÎ≥¥??Í∞ïÌïòÍ≤??îÎì§Î¶¨Í≤å)
                int x0b = start.x;
                int x3b = endXb;
    
                float inv = 1f / nSteps;
    
                // ?úÏ£º?©ÏïîÎ≥¥Îã§ Í∞ïÌïòÍ≤å‚Ä? steps Í∏∞Î∞ò?ºÎ°ú Ïª®Ìä∏Î°?Î≤îÏúÑÎ•??¨Í≤å
                // (Í∞ÄÏßÄÍ∞Ä ?áÏïÑ??Íµ¨Î∂àÍ±∞Î¶º??Ï∂©Î∂Ñ???òÏò§Í≤?
                int maxCtrlB = Mathf.Clamp(Mathf.RoundToInt(nSteps * 1.25f), 12, 140);
    
                // ÏßÅÏÑ†(45?? Í∏∞Ï? x(t) = start.x + dir * round(nSteps * t)
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
    
                    // ?àÎ¨¥ ?úÍ∞Ñ?¥Îèô?òÎ©¥ Í∞ÄÏßÄÍ∞Ä ?äÍ≤® Î≥¥Ïù¥ÎØÄÎ°?"?§ÌÖù??Î≥Ä?????úÌïú.
                    // Íµ¨Î∂àÍ±∞Î¶º???¥Î¶¨Í∏??ÑÌï¥ ¬±2ÍπåÏ? ?àÏö©(Ï£ºÏö©?îÎ≥¥????Ï∂úÎ†Å??
                    bx = Mathf.Clamp(bx, prevX - 2, prevX + 2);
    
                    if (solid[bx, y] == ID_TUFF) break;
    
                    branchPts.Add((bx, y));
                    prevX = bx;
    
                    if (y >= h - 2) break;
                }
    
                if (branchPts.Count == 0) continue;
    
                // (C) ?êÍªò ?åÏù¥??carve (Í∏∞Ï°¥ ?†Ï?)
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
    
        // ??Pyramid: desertStartX ~ volcanoStartX ?¨Ïù¥??Ï§ëÏïô???ùÏÑ±
        private static void ApplyPyramidPass(int desertStartX, int volcanoCoreStartX, int seaLevel, ushort[,] commonSolid, ushort[,] commonMeta, int w, int h)
        {
            int x0 = Mathf.Clamp(desertStartX, 0, w - 1);
            int x1 = Mathf.Clamp(volcanoCoreStartX - 1, 0, w - 1);
    
            if (x1 <= x0) return;
    
            const int baseWidth = 301;     // ?Ä??
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
    
        // ??FloodFill ?¥ÌõÑ: ?¨Î†àÎ∞îÏä§ Íµ¨Í∞Ñ Î¨º‚Üí?ºÏùå + Íµ¨Îç©???´Í∏∞
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
    
            // (2) Carve: ?úÎ©¥Î∂Ä???ÑÎûòÎ°?depth(x) ÎßåÌÅº AIRÎ°??´Í∏∞ (Î¨??ºÏùå ?¨Ìï® ?úÍ±∞)
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
    
    }
}
