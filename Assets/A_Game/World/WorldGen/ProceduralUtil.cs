using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public static class ProceduralUtil
{
    // ─────────────────────────────────────────────────────────
    // Fractal Perlin 1D (기존 지형 높이용)
    // ─────────────────────────────────────────────────────────
    public static float FractalPerlin1D(
        float x,
        float baseFrequency = 0.005f,
        int octaves = 4,
        float persistence = 0.5f,
        float lacunarity = 2f,
        float baseHeight = 0f,
        float range = 0.5f)
    {
        float value = 0f, amplitude = 1f, frequency = baseFrequency, max = 0f;
        for (int i = 0; i < octaves; i++)
        {
            value += Mathf.PerlinNoise(x * frequency, 0f) * amplitude;
            max += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }
        float normalized = (value / max) - 0.5f;
        return normalized * 2f * range + baseHeight;
    }

    // ─────────────────────────────────────────────────────────
    // 동굴 마스크 (멀티스케일 A ∪ 도메인 워핑 B)
    //
    //  - return[x, y] == true  → 동굴(뚫릴 곳, AIR)
    //  - return[x, y] == false → 땅(채워진 곳)
    //
    //  WorldGenSettings 의 Cave 파라미터들을 그대로 사용
    // ─────────────────────────────────────────────────────────
    public static bool[,] GenerateNoiseCaveMask(int width, int height, int seed, WorldGenSettings s)
    {
        var cave = new bool[width, height];

        // A: 멀티스케일 노이즈
        float freqL   = s.caveA_FreqLarge;
        int   octL    = Mathf.Clamp(s.caveA_OctLarge, 1, 8);
        float persL   = s.caveA_PersLarge;
        float lacL    = s.caveA_LacLarge;

        float freqD   = s.caveA_FreqDetail;
        int   octD    = Mathf.Clamp(s.caveA_OctDetail, 1, 8);
        float persD   = s.caveA_PersDetail;
        float lacD    = s.caveA_LacDetail;
        float detailW = s.caveA_DetailWeight;

        float thresholdA = s.caveA_Threshold;

        // B: 도메인 워핑 + 노이즈
        float warpFreq = s.caveB_WarpFreq;
        int   warpOct  = Mathf.Clamp(s.caveB_WarpOct, 1, 8);
        float warpPers = s.caveB_WarpPers;
        float warpLac  = s.caveB_WarpLac;
        float warpAmpX = s.caveB_WarpAmpX;
        float warpAmpY = s.caveB_WarpAmpY;

        float freqB      = s.caveB_FreqBase;
        int   octB       = Mathf.Clamp(s.caveB_OctBase, 1, 8);
        float persB      = s.caveB_PersBase;
        float lacB       = s.caveB_LacBase;
        float thresholdB = s.caveB_Threshold;

        // 깊이 마스크
        bool  useDepthMask = s.useCaveDepthMask;
        float depthStart   = Mathf.Clamp01(s.caveDepthStart);
        float depthEnd     = Mathf.Clamp01(s.caveDepthEnd);

        // 시드 → 각 노이즈 필드에 사용할 2D 오프셋
        Vector2 seedLarge  = new Vector2(seed * 11.13f, seed * 7.91f);
        Vector2 seedDetail = new Vector2(seed * 3.21f + 123.4f, seed * 5.67f + 567.8f);
        Vector2 seedWarp1  = new Vector2(seed * 9.99f + 101.1f, seed * 4.44f + 202.2f);
        Vector2 seedWarp2  = new Vector2(seed * 7.77f + 303.3f, seed * 2.22f + 404.4f);
        Vector2 seedB      = new Vector2(seed * 13.37f + 999.9f, seed * 6.28f + 555.5f);

        // 로컬 함수: 2D 프랙탈 Perlin [-1,1]
        float Frac(Vector2 p, float baseFreq, int octaves, float persistence, float lacunarity, Vector2 offset)
        {
            float value  = 0f;
            float amp    = 1f;
            float freq   = baseFreq;
            float maxAmp = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float nx = (p.x + offset.x) * freq;
                float ny = (p.y + offset.y) * freq;
                float n  = Mathf.PerlinNoise(nx, ny) * 2f - 1f; // [-1,1]

                value  += n * amp;
                maxAmp += amp;

                amp  *= persistence;
                freq *= lacunarity;
            }

            if (maxAmp <= 0f) return 0f;
            return value / maxAmp; // [-1,1]
        }

        int hMinus1 = Mathf.Max(1, height - 1);

        Parallel.For(0, width, x =>
        {
            for (int y = 0; y < height; y++)
            {
                // y 정규화 (0~1). 좌표계 방향은 프로젝트 기준으로 해석해서 튜닝.
                float normY = (float)y / hMinus1;

                // 깊이 마스크: 특정 구간 밖에서는 동굴 거의/전혀 없음
                float depthFactor = 1f;
                if (useDepthMask)
                {
                    if (normY < depthStart || normY > depthEnd)
                    {
                        depthFactor = 0f;
                    }
                    else
                    {
                        float t = (normY - depthStart) / Mathf.Max(0.0001f, depthEnd - depthStart);
                        depthFactor = t; // 구간 안에서 0→1로 증가
                    }
                }

                Vector2 world = new Vector2(x, y);

                // ── A: 멀티스케일 노이즈 ──
                float largeA  = Frac(world, freqL, octL, persL, lacL, seedLarge);
                float detailA = Frac(world, freqD, octD, persD, lacD, seedDetail);
                float noiseA  = largeA + detailW * detailA;
                noiseA = Mathf.Clamp(noiseA, -1f, 1f);

                bool caveA = false;
                if (depthFactor > 0f)
                {
                    // 위쪽에서는 좀 더 빡세게 컷되도록 보정 (원하면 계수 조정)
                    float thresholdAeff = thresholdA + (1f - depthFactor) * 0.3f;
                    caveA = (noiseA < thresholdAeff);
                }

                // ── B: 도메인 워핑 + 노이즈 ──
                float warpSrcX = Frac(world, warpFreq, warpOct, warpPers, warpLac, seedWarp1);
                float warpSrcY = Frac(world, warpFreq, warpOct, warpPers, warpLac, seedWarp2);
                float dx       = warpSrcX * warpAmpX;
                float dy       = warpSrcY * warpAmpY;
                Vector2 warped = new Vector2(world.x + dx, world.y + dy);

                float noiseB = Frac(warped, freqB, octB, persB, lacB, seedB);
                noiseB = Mathf.Clamp(noiseB, -1f, 1f);

                bool caveB = false;
                if (depthFactor > 0f)
                {
                    float thresholdBeff = thresholdB + (1f - depthFactor) * 0.3f;
                    caveB = (noiseB < thresholdBeff);
                }

                // 합집합: 둘 중 하나라도 동굴이면 동굴
                bool caveFinal = (caveA || caveB) && depthFactor > 0f;

                cave[x, y] = caveFinal;
            }
        });

        return cave;
    }

    // ─────────────────────────────────────────────────────────
    // Ore cluster sampling & clustering
    // ─────────────────────────────────────────────────────────
    public static List<Vector2Int> SampleSeedPositions(int width, int minHeight, int maxHeight, float seedDensity)
    {
        int valid = width * (maxHeight - minHeight + 1);
        int seedCount = Mathf.RoundToInt(valid * seedDensity);
        var seeds = new List<Vector2Int>();
        for (int i = 0; i < seedCount; i++)
        {
            int x = UnityEngine.Random.Range(0, width);
            int y = UnityEngine.Random.Range(minHeight, maxHeight + 1);
            seeds.Add(new Vector2Int(x, y));
        }
        return seeds;
    }

    public static int SampleClusterSize(float mean, float stdDev)
    {
        float u1 = UnityEngine.Random.value, u2 = UnityEngine.Random.value;
        float randStd = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Sin(2f * Mathf.PI * u2);
        int size = Mathf.RoundToInt(mean + stdDev * randStd);
        return Mathf.Max(1, size);
    }

    public static Vector2Int[] GetNeighborOffsets(bool eightDir)
        => eightDir
            ? new[]
              {
                  new Vector2Int(1, 0), new Vector2Int(-1, 0),
                  new Vector2Int(0, 1), new Vector2Int(0, -1),
                  new Vector2Int(1, 1), new Vector2Int(1, -1),
                  new Vector2Int(-1, 1), new Vector2Int(-1, -1)
              }
            : new[]
              {
                  new Vector2Int(1, 0), new Vector2Int(-1, 0),
                  new Vector2Int(0, 1), new Vector2Int(0, -1)
              };

    public static List<List<Vector2Int>> GenerateClusters(
        List<Vector2Int> seeds, float meanSize, float stdDevSize, float maxStepsFactor,
        float expansionProb, Vector2Int[] neighborOffsets, bool frontierRandom)
    {
        var clusters = new List<List<Vector2Int>>();
        foreach (var seed in seeds)
        {
            int target   = SampleClusterSize(meanSize, stdDevSize);
            int maxSteps = Mathf.CeilToInt(target * maxStepsFactor);
            var cluster  = new List<Vector2Int> { seed };
            var frontier = new List<Vector2Int> { seed };
            for (int step = 0; step < maxSteps && frontier.Count > 0 && cluster.Count < target; step++)
            {
                int idx = frontierRandom ? UnityEngine.Random.Range(0, frontier.Count) : 0;
                var pos = frontier[idx];
                frontier.RemoveAt(idx);
                foreach (var off in neighborOffsets)
                {
                    if (UnityEngine.Random.value > expansionProb) continue;
                    var np = pos + off;
                    if (!cluster.Contains(np))
                    {
                        cluster.Add(np);
                        frontier.Add(np);
                        if (cluster.Count >= target) break;
                    }
                }
            }
            clusters.Add(cluster);
        }
        return clusters;
    }

    public static List<List<Vector2Int>> FilterClusters(
        List<List<Vector2Int>> clusters, bool[,] caveMap, bool[,] waterMap)
    {
        var result = new List<List<Vector2Int>>();
        int w = caveMap.GetLength(0), h = caveMap.GetLength(1);
        foreach (var cluster in clusters)
        {
            var valid = new List<Vector2Int>();
            foreach (var pos in cluster)
            {
                int x = pos.x, y = pos.y;
                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                if (caveMap[x, y]) continue;
                valid.Add(pos);
                waterMap[x, y] = false;
            }
            if (valid.Count > 0) result.Add(valid);
        }
        return result;
    }
}
