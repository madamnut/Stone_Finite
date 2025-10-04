using UnityEngine;

[CreateAssetMenu(fileName = "WorldGenSettings", menuName = "Map/World Generation Settings")]
public class WorldGenSettings : ScriptableObject
{
    [Header("Base Settings")]
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

    // ─────────────────────────────────────────────────────────
    // Clay 추가 세팅
    // ─────────────────────────────────────────────────────────
    [Header("Clay Cluster Settings")]
    public int   clayMinHeight          = 200;     // Dirt 층 하부 포함
    public int   clayMaxHeight          = 900;     // 지표 근처까지 허용
    public float clayClusterSizeMean    = 70f;
    public float clayClusterSizeStdDev  = 18f;
    public float claySeedDensity        = 0.0018f; // Dirt 전용 시드 밀도
    public float clayExpansionProb      = 0.45f;   // 확장 확률
    public float clayMaxGrowthFactor    = 1.4f;    // 최대 성장 배수

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

}
