using UnityEngine;

[CreateAssetMenu(fileName = "WorldGenSettings", menuName = "Map/World Generation Settings")]
public class WorldGenSettings : ScriptableObject
{
    [Header("Base Settings")]
    public int seed = 42;
    public int width = 5120;
    public int height = 1536;

    [Header("Base Terrain")]
    public int waterHeight = 800;

    [Header("Dirt Settings")]
    public float dirtBaseHeight = 900f;
    public float dirtRange = 100f;
    [Space(4)]
    public float dirtNoiseBaseFrequency = 0.005f;
    public int   dirtNoiseOctaves       = 4;
    [Range(0f,1f)] public float dirtNoisePersistence = 0.5f;
    public float dirtNoiseLacunarity    = 2f;

    [Header("Rock Settings")]
    public float rockBaseHeight = 1200f;
    public float rockRange = 100f;
    [Space(4)]
    public float rockNoiseBaseFrequency = 0.005f;
    public int   rockNoiseOctaves       = 4;
    [Range(0f,1f)] public float rockNoisePersistence = 0.5f;
    public float rockNoiseLacunarity    = 2f;

    [Header("Granite Settings")]
    public float graniteBaseHeight      = 1100f;
    public float graniteRange           = 80f;
    [Space(4)]
    public float graniteNoiseBaseFrequency = 0.006f;
    public int   graniteNoiseOctaves       = 3;
    [Range(0f,1f)] public float graniteNoisePersistence = 0.5f;
    public float graniteNoiseLacunarity    = 2.2f;

    [Header("Amphibolite Settings")]
    public float amphibBaseHeight       = 1000f;
    public float amphibRange            = 60f;
    [Space(4)]
    public float amphibNoiseBaseFrequency = 0.007f;
    public int   amphibNoiseOctaves       = 3;
    [Range(0f,1f)] public float amphibNoisePersistence = 0.5f;
    public float amphibNoiseLacunarity    = 2.4f;

    [Header("Ore: Coal Cluster Settings")]
    public int coalMinHeight = 200;
    public int coalMaxHeight = 600;
    public float coalClusterSizeMean = 80f;
    public float coalClusterSizeStdDev = 20f;
    public float coalSeedDensity = 0.002f;
    public float coalExpansionProb = 0.5f;
    public float coalMaxGrowthFactor = 1.5f;

    [Header("Ore: Copper Cluster Settings")]
    public int copperMinHeight = 300;
    public int copperMaxHeight = 700;
    public float copperClusterSizeMean = 60f;
    public float copperClusterSizeStdDev = 15f;
    public float copperSeedDensity = 0.0015f;
    public float copperExpansionProb = 0.4f;
    public float copperMaxGrowthFactor = 1.4f;

    [Header("Ore: Iron Cluster Settings")]
    public int ironMinHeight = 400;
    public int ironMaxHeight = 800;
    public float ironClusterSizeMean = 50f;
    public float ironClusterSizeStdDev = 12f;
    public float ironSeedDensity = 0.001f;
    public float ironExpansionProb = 0.3f;
    public float ironMaxGrowthFactor = 1.3f;

    [Header("Cluster Behavior")]
    public int minInterClusterDist = 10;
    public int clusterJitter = 5;
    public enum NeighborMode { FourDir, EightDir }
    public NeighborMode neighborMode = NeighborMode.EightDir;
    public enum FrontierMode { FIFO, Random }
    public FrontierMode frontierMode = FrontierMode.Random;

    [Header("Cave Settings (Cellular Automata)")]
    [Range(0,100)] public int caveInitialFillPercent = 45;
    public int caveBirthLimit        = 5;
    public int caveSurvivalLimit     = 4;
    public int caveIterations        = 5;

    [Header("Cave Settings (Drunkard's Walk)")]
    public int   caveWalkerCount   = 10;
    public int   caveWalkLength    = 500;
    [Range(0f,1f)] public float caveDirectionBias = 0.5f;

    [Header("Tree Settings")]
    [Range(0f,1f)] public float treeDensity    = 0.02f;    // 한 x열당 나무 심을 확률
    public int   treeMinHeight  = 4;       // 줄기 최소 높이
    public int   treeModeHeight = 6;       // 줄기 최빈 높이
    public int   treeMaxHeight  = 10;      // 줄기 최대 높이

    [Header("Block Colors")]
    public Color airColor    = Color.white;
    public Color waterColor  = Color.blue;
    public Color dirtColor   = new Color(0.545f, 0.271f, 0.075f);
    public Color grassColor  = new Color(0.4f, 0.8f, 0.2f);
    public Color rockColor   = Color.grey;
    public Color graniteColor= new Color(0.4f,0.4f,0.4f);
    public Color amphibColor = new Color(0.25f,0.25f,0.25f);
    public Color coalColor   = new Color(0.1f, 0.1f, 0.1f);
    public Color copperColor = new Color(0.8f, 0.5f, 0.2f);
    public Color ironColor   = new Color(0.6f, 0.6f, 0.6f);
    public Color trunkColor    = new Color(0.4f, 0.2f, 0.05f);
    public Color leafColor     = new Color(0.2f, 0.8f, 0.2f);
}