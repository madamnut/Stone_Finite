using System;
using System.Collections.Generic;
using UnityEngine;

public static class ProceduralUtil
{
    // Fractal Perlin 1D
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

    // Cave generation (Cellular Automata + Drunkard's Walk)
    public static bool[,] GenerateMixedCave(
        int width, int height,
        int initialFillPercent, int birthLimit, int survivalLimit, int iterations,
        int walkerCount, int walkLength, float directionBias)
    {
        var cave  = GenerateCellularCave(width, height, initialFillPercent, birthLimit, survivalLimit, iterations);
        var drunk = GenerateDrunkardsWalk(width, height, walkerCount, walkLength, directionBias);

        bool[,] combined = new bool[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                combined[x, y] = cave[x, y] || drunk[x, y];
        return combined;
    }

    private static bool[,] GenerateCellularCave(int width, int height, int initialFillPercent, int birthLimit, int survivalLimit, int iterations)
    {
        bool[,] map = new bool[width, height];
        var rnd = new System.Random();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                map[x, y] = rnd.Next(0, 100) < initialFillPercent;

        for (int i = 0; i < iterations; i++)
        {
            bool[,] newMap = new bool[width, height];
            for (int xx = 0; xx < width; xx++)
                for (int yy = 0; yy < height; yy++)
                {
                    int walls = CountAdjacentWalls(map, xx, yy, width, height);
                    newMap[xx, yy] = map[xx, yy] ? (walls < birthLimit) : (walls < survivalLimit);
                }
            map = newMap;
        }
        return map;
    }

    private static bool[,] GenerateDrunkardsWalk(int width, int height, int walkerCount, int walkLength, float directionBias)
    {
        bool[,] map = new bool[width, height];
        var rnd = new System.Random();
        Vector2Int[] dirs = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

        for (int i = 0; i < walkerCount; i++)
        {
            int x = rnd.Next(width), y = rnd.Next(height);
            for (int step = 0; step < walkLength; step++)
            {
                map[x, y] = true;
                int idx = rnd.Next(dirs.Length);
                var d = dirs[idx];
                if (rnd.NextDouble() < directionBias) d = dirs[idx];
                x = Mathf.Clamp(x + d.x, 0, width - 1);
                y = Mathf.Clamp(y + d.y, 0, height - 1);
            }
        }
        return map;
    }

    private static int CountAdjacentWalls(bool[,] map, int x, int y, int width, int height)
    {
        int count = 0;
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= width || ny >= height) count++;
                else if (!map[nx, ny]) count++;
            }
        return count;
    }

    // Ore cluster sampling & clustering
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
            ? new[] { new Vector2Int(1,0), new Vector2Int(-1,0), new Vector2Int(0,1), new Vector2Int(0,-1),
                      new Vector2Int(1,1), new Vector2Int(1,-1), new Vector2Int(-1,1), new Vector2Int(-1,-1) }
            : new[] { new Vector2Int(1,0), new Vector2Int(-1,0), new Vector2Int(0,1), new Vector2Int(0,-1) };

    public static List<List<Vector2Int>> GenerateClusters(
        List<Vector2Int> seeds, float meanSize, float stdDevSize, float maxStepsFactor,
        float expansionProb, Vector2Int[] neighborOffsets, bool frontierRandom)
    {
        var clusters = new List<List<Vector2Int>>();
        foreach (var seed in seeds)
        {
            int target = SampleClusterSize(meanSize, stdDevSize);
            int maxSteps = Mathf.CeilToInt(target * maxStepsFactor);
            var cluster = new List<Vector2Int> { seed };
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

    // ───────── 잎(Deco) 채우기: common 맵에 ID만 기록 ─────────
    public static void DrawLeafBlobOnIDMap(
        int cx, int cy, int trunkHeight,
        int width, int height,
        ushort leafID,
        ushort[,] common)
    {
        int R0 = Mathf.RoundToInt(trunkHeight * 0.5f);
        int layers = 3;
        for (int layer = 0; layer < layers; layer++)
        {
            float R = R0 * (1f - layer / (float)layers);
            float A = R * 0.2f;
            int steps = 36;
            var outline = new List<Vector2>(steps);
            for (int i = 0; i < steps; i++)
            {
                float theta = 2 * Mathf.PI * i / steps;
                float n = Mathf.PerlinNoise(
                    (cx + Mathf.Cos(theta)) * 0.1f,
                    (cy + layer + Mathf.Sin(theta)) * 0.1f
                );
                float r = R + (n - 0.5f) * A;
                outline.Add(new Vector2(
                    cx + r * Mathf.Cos(theta),
                    cy + r * Mathf.Sin(theta) + layer
                ));
            }
            FillPolygonOnIDMap(outline, width, height, leafID, common);
        }
    }

    public static void FillPolygonOnIDMap(
        List<Vector2> poly,
        int width, int height,
        ushort fillID,
        ushort[,] common)
    {
        const ushort ID_AIR = 0;
        int n = poly.Count;
        int minY = height - 1, maxY = 0;
        foreach (var p in poly)
        {
            int py = Mathf.Clamp((int)p.y, 0, height - 1);
            minY = Mathf.Min(minY, py);
            maxY = Mathf.Max(maxY, py);
        }
        for (int y = minY; y <= maxY; y++)
        {
            var intersects = new List<float>();
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float yi = poly[i].y, yj = poly[j].y;
                if ((yi < y && yj >= y) || (yj < y && yi >= y))
                {
                    float xi = poly[i].x, xj = poly[j].x;
                    float x = xi + (y - yi) * (xj - xi) / (yj - yi);
                    intersects.Add(x);
                }
            }
            intersects.Sort();
            for (int k = 0; k + 1 < intersects.Count; k += 2)
            {
                int xStart = Mathf.Clamp((int)intersects[k], 0, width - 1);
                int xEnd   = Mathf.Clamp((int)intersects[k + 1], 0, width - 1);
                for (int x = xStart; x <= xEnd; x++)
                {
                    if (common[x, y] == ID_AIR)
                        common[x, y] = fillID; // ID만 기록. 타입 판정은 주입 단계에서.
                }
            }
        }
    }
}
