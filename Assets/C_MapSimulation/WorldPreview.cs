using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

[ExecuteAlways]
public class WorldPreview : MonoBehaviour
{
    public WorldGenSettings settings;
    public RawImage previewImage;
    private Texture2D _tex;

    void OnValidate()
    {
        if (settings != null) GeneratePreview();
    }

    [ContextMenu("Generate Preview")]
    public void GeneratePreview()
    {
        if (settings == null || previewImage == null)
            return;

        int width  = settings.width;
        int height = settings.height;

        // 텍스처 초기화
        if (_tex == null || _tex.width != width || _tex.height != height)
        {
            _tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode   = TextureWrapMode.Clamp
            };
        }

        // 1) 초기 waterMap
        bool[,] waterMap = new bool[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y <= settings.waterHeight && y < height; y++)
                waterMap[x, y] = true;

        // 2) 공기 초기화
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = settings.airColor;

        // 3) 레이어 높이 계산
        int dirtSeed    = settings.seed;
        int rockSeed    = settings.seed + 10000;
        int graniteSeed = settings.seed + 20000;
        int amphibSeed  = settings.seed + 30000;

        float[,] dirtH    = new float[width, height];
        float[,] rockH    = new float[width, height];
        float[,] graniteH = new float[width, height];
        float[,] amphibH  = new float[width, height];

        for (int x = 0; x < width; x++)
        {
            float sxD = x + dirtSeed;
            float sxR = x + rockSeed;
            float sxG = x + graniteSeed;
            float sxA = x + amphibSeed;
            for (int y = 0; y < height; y++)
            {
                dirtH[x, y]    = ProceduralUtil.FractalPerlin1D(sxD, settings.dirtNoiseBaseFrequency, settings.dirtNoiseOctaves, settings.dirtNoisePersistence, settings.dirtNoiseLacunarity, settings.dirtBaseHeight, settings.dirtRange);
                rockH[x, y]    = ProceduralUtil.FractalPerlin1D(sxR, settings.rockNoiseBaseFrequency, settings.rockNoiseOctaves, settings.rockNoisePersistence, settings.rockNoiseLacunarity, settings.rockBaseHeight, settings.rockRange);
                graniteH[x, y] = ProceduralUtil.FractalPerlin1D(sxG, settings.graniteNoiseBaseFrequency, settings.graniteNoiseOctaves, settings.graniteNoisePersistence, settings.graniteNoiseLacunarity, settings.graniteBaseHeight, settings.graniteRange);
                amphibH[x, y]  = ProceduralUtil.FractalPerlin1D(sxA, settings.amphibNoiseBaseFrequency, settings.amphibNoiseOctaves, settings.amphibNoisePersistence, settings.amphibNoiseLacunarity, settings.amphibBaseHeight, settings.amphibRange);
            }
        }

        // 4) 지층 색상 및 물맵
        var offsets = ProceduralUtil.GetNeighborOffsets(settings.neighborMode == WorldGenSettings.NeighborMode.EightDir);
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                int idx = x + y * width;
                if (y <= dirtH[x, y])    { pixels[idx] = settings.dirtColor;    waterMap[x, y] = false; }
                if (y <= rockH[x, y])    { pixels[idx] = settings.rockColor;    waterMap[x, y] = false; }
                if (y <= graniteH[x, y]) { pixels[idx] = settings.graniteColor; waterMap[x, y] = false; }
                if (y <= amphibH[x, y])  { pixels[idx] = settings.amphibColor;  waterMap[x, y] = false; }
            }

        // 5) 광물 배치: Coal, Copper, Iron
        PlaceOreClusters(width, height, offsets, rockH, graniteH, amphibH, pixels, waterMap,
            settings.coalMinHeight,   settings.coalMaxHeight,   settings.coalSeedDensity,   settings.coalClusterSizeMean,   settings.coalClusterSizeStdDev,   settings.coalMaxGrowthFactor,   settings.coalExpansionProb,   settings.coalColor);
        PlaceOreClusters(width, height, offsets, rockH, graniteH, amphibH, pixels, waterMap,
            settings.copperMinHeight, settings.copperMaxHeight, settings.copperSeedDensity, settings.copperClusterSizeMean, settings.copperClusterSizeStdDev, settings.copperMaxGrowthFactor, settings.copperExpansionProb, settings.copperColor);
        PlaceOreClusters(width, height, offsets, rockH, graniteH, amphibH, pixels, waterMap,
            settings.ironMinHeight,   settings.ironMaxHeight,   settings.ironSeedDensity,   settings.ironClusterSizeMean,   settings.ironClusterSizeStdDev,   settings.ironMaxGrowthFactor,   settings.ironExpansionProb,   settings.ironColor);

        // 6) 동굴 적용
        bool[,] cave = ProceduralUtil.GenerateMixedCave(width, height,
            settings.caveInitialFillPercent, settings.caveBirthLimit, settings.caveSurvivalLimit, settings.caveIterations,
            settings.caveWalkerCount, settings.caveWalkLength, settings.caveDirectionBias);
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (cave[x, y]) pixels[x + y * width] = settings.airColor;

        // 7) 물 Flood-fill
        FloodFillWater(width, height, pixels, waterMap, cave);

        // 8) 잔디 배치
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                int idx = x + y * width;
                if (pixels[idx] == settings.dirtColor)
                {
                    bool skyOpen = true;
                    for (int yy = y + 1; yy < height; yy++)
                        if (pixels[x + yy * width] != settings.airColor)
                        {
                            skyOpen = false;
                            break;
                        }
                    if (skyOpen) pixels[idx] = settings.grassColor;
                }
            }

        // 9) 나무 배치: 줄기와 잎
        PlaceTrees(width, height, pixels);

        // 최종 텍스처 적용
        _tex.SetPixels(pixels);
        _tex.Apply();
        previewImage.texture = _tex;
    }

    private void PlaceOreClusters(int width, int height, Vector2Int[] offsets, float[,] rockH, float[,] graniteH, float[,] amphibH,
                                  Color[] pixels, bool[,] waterMap,
                                  int minH, int maxH, float seedDensity, float meanSize, float stdDev, float maxGrowth, float expandProb, Color oreColor)
    {
        var seeds   = ProceduralUtil.SampleSeedPositions(width, minH, maxH, seedDensity);
        var clusters= ProceduralUtil.GenerateClusters(seeds, meanSize, stdDev, maxGrowth, expandProb, offsets, settings.frontierMode == WorldGenSettings.FrontierMode.Random);
        foreach (var cl in clusters)
            foreach (var p in cl)
            {
                int x = p.x, y = p.y;
                if (x < 0 || y < 0 || x >= width || y >= height) continue;
                if (y <= rockH[x, y] && y > graniteH[x, y] && y > amphibH[x, y])
                {
                    pixels[x + y * width] = oreColor;
                    waterMap[x, y] = false;
                }
            }
    }

    private void FloodFillWater(int width, int height, Color[] pixels, bool[,] waterMap, bool[,] cave)
    {
        var q = new Queue<Vector2Int>();
        Vector2Int[] dirs = { new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(1, 0) };
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (waterMap[x, y]) q.Enqueue(new Vector2Int(x, y));

        while (q.Count > 0)
        {
            var p = q.Dequeue();
            foreach (var d in dirs)
            {
                int nx = p.x + d.x;
                int ny = p.y + d.y;
                if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                if (waterMap[nx, ny]) continue;
                if (cave[nx, ny])
                {
                    waterMap[nx, ny] = true;
                    q.Enqueue(new Vector2Int(nx, ny));
                }
            }
        }
    }

    private void PlaceTrees(int width, int height, Color[] pixels)
    {
        var rand = new System.Random(settings.seed);
        for (int x = 0; x < width; x++)
        {
            if (rand.NextDouble() > settings.treeDensity) continue;
            int y = height - 1;
            while (y > 0 && pixels[x + y * width] == settings.airColor) y--;
            if (pixels[x + y * width] != settings.grassColor) continue;

            int seedY = y + 1;
            int h = SampleTriangular(rand, settings.treeMinHeight, settings.treeModeHeight, settings.treeMaxHeight);

            // 줄기 배치 (공기만 덮기)
            for (int i = 0; i < h && seedY + i < height; i++)
                if (pixels[x + (seedY + i) * width] == settings.airColor)
                    pixels[x + (seedY + i) * width] = settings.trunkColor;

            // 잎 배치 (공기만 덮기)
            ProceduralUtil.DrawLeafBlob(
                x,
                seedY + h - 1,
                h,
                width,
                height,
                settings.leafColor,
                settings.airColor,
                pixels
            );
        }
    }

    private int SampleTriangular(System.Random rand, int min, int mode, int max)
    {
        double u = rand.NextDouble();
        double c = (mode - min) / (double)(max - min);
        if (u < c)
            return min + (int)Math.Sqrt(u * (mode - min) * (max - min));
        else
            return max - (int)Math.Sqrt((1 - u) * (max - mode) * (max - min));
    }
}
