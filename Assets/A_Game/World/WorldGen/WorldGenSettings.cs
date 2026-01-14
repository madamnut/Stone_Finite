using UnityEngine;

[CreateAssetMenu(fileName = "WorldGenSettings", menuName = "Map/World Generation Settings")]
public class WorldGenSettings : ScriptableObject
{
    [Header("Base Settings")]
    public int width = 5120;
    public int height = 1536;

    [Header("Base Terrain")]
    public int waterHeight = 800;

    // ─────────────────────────────────────────────────────────
    // Dirt band
    // ─────────────────────────────────────────────────────────
    [Header("Dirt Settings")]
    public float dirtBaseHeight = 900f;
    public float dirtRange = 100f;

    public float dirtNoiseBaseFrequency = 0.005f;
    public int   dirtNoiseOctaves       = 4;
    [Range(0f, 1f)] public float dirtNoisePersistence = 0.5f;
    public float dirtNoiseLacunarity    = 2f;

    // ─────────────────────────────────────────────────────────
    // Rock
    // ─────────────────────────────────────────────────────────
    [Header("Rock Settings")]
    public float rockBaseHeight = 1200f;
    public float rockRange = 100f;

    public float rockNoiseBaseFrequency = 0.005f;
    public int   rockNoiseOctaves       = 4;
    [Range(0f, 1f)] public float rockNoisePersistence = 0.5f;
    public float rockNoiseLacunarity    = 2f;

    // ─────────────────────────────────────────────────────────
    // Granite
    // ─────────────────────────────────────────────────────────
    [Header("Granite Settings")]
    public float graniteBaseHeight = 1100f;
    public float graniteRange      = 80f;

    public float graniteNoiseBaseFrequency = 0.006f;
    public int   graniteNoiseOctaves       = 3;
    [Range(0f, 1f)] public float graniteNoisePersistence = 0.5f;
    public float graniteNoiseLacunarity    = 2.2f;

    // ─────────────────────────────────────────────────────────
    // Amphibolite
    // ─────────────────────────────────────────────────────────
    [Header("Amphibolite Settings")]
    public float amphibBaseHeight = 1000f;
    public float amphibRange      = 60f;

    public float amphibNoiseBaseFrequency = 0.007f;
    public int   amphibNoiseOctaves       = 3;
    [Range(0f, 1f)] public float amphibNoisePersistence = 0.5f;
    public float amphibNoiseLacunarity    = 2.4f;

    // ─────────────────────────────────────────────────────────
    // Clay clusters
    // ─────────────────────────────────────────────────────────
    [Header("Clay Cluster Settings")]
    public int   clayMinHeight         = 200;
    public int   clayMaxHeight         = 900;
    public float clayClusterSizeMean   = 70f;
    public float clayClusterSizeStdDev = 18f;
    public float claySeedDensity       = 0.0018f;
    public float clayExpansionProb     = 0.45f;
    public float clayMaxGrowthFactor   = 1.4f;

    // ─────────────────────────────────────────────────────────
    // Ores
    // ─────────────────────────────────────────────────────────
    [Header("Ore: Coal Cluster Settings")]
    public int   coalMinHeight         = 200;
    public int   coalMaxHeight         = 600;
    public float coalClusterSizeMean   = 80f;
    public float coalClusterSizeStdDev = 20f;
    public float coalSeedDensity       = 0.002f;
    public float coalExpansionProb     = 0.5f;
    public float coalMaxGrowthFactor   = 1.5f;

    [Header("Ore: Tin Cluster Settings")]
    public int   tinMinHeight         = 300;
    public int   tinMaxHeight         = 700;
    public float tinClusterSizeMean   = 60f;
    public float tinClusterSizeStdDev = 15f;
    public float tinSeedDensity       = 0.0015f;
    public float tinExpansionProb     = 0.4f;
    public float tinMaxGrowthFactor   = 1.4f;

    [Header("Ore: Copper Cluster Settings")]
    public int   copperMinHeight         = 300;
    public int   copperMaxHeight         = 700;
    public float copperClusterSizeMean   = 60f;
    public float copperClusterSizeStdDev = 15f;
    public float copperSeedDensity       = 0.0015f;
    public float copperExpansionProb     = 0.4f;
    public float copperMaxGrowthFactor   = 1.4f;

    [Header("Ore: Iron Cluster Settings")]
    public int   ironMinHeight         = 400;
    public int   ironMaxHeight         = 800;
    public float ironClusterSizeMean   = 50f;
    public float ironClusterSizeStdDev = 12f;
    public float ironSeedDensity       = 0.001f;
    public float ironExpansionProb     = 0.3f;
    public float ironMaxGrowthFactor   = 1.3f;

    // 클러스터 공통
    [Header("Cluster Behavior")]
    public int minInterClusterDist = 10;
    public int clusterJitter       = 5;

    public enum NeighborMode { FourDir, EightDir }
    public NeighborMode neighborMode = NeighborMode.EightDir;

    public enum FrontierMode { FIFO, Random }
    public FrontierMode frontierMode = FrontierMode.Random;

    // ─────────────────────────────────────────────────────────
    // NEW: Noise-Based Cave Generation
    // (멀티스케일 + 도메인 워핑)
    // ─────────────────────────────────────────────────────────
    [Header("Cave A: Multiscale Noise")]
    public float caveA_FreqLarge = 0.008f;
    public int   caveA_OctLarge  = 4;
    public float caveA_PersLarge = 0.5f;
    public float caveA_LacLarge  = 2.0f;

    public float caveA_FreqDetail = 0.04f;
    public int   caveA_OctDetail  = 3;
    public float caveA_PersDetail = 0.5f;
    public float caveA_LacDetail  = 2.2f;

    [Range(0f, 1f)]
    public float caveA_DetailWeight = 0.5f;

    [Range(-1f, 1f)]
    public float caveA_Threshold = -0.10f;

    // ─────────────────────────────────────────────────────────
    // Cave B: Domain Warping
    // ─────────────────────────────────────────────────────────
    [Header("Cave B: Domain Warping (Noise Warp)")]
    public float caveB_WarpFreq = 0.010f;
    public int   caveB_WarpOct  = 3;
    public float caveB_WarpPers = 0.6f;
    public float caveB_WarpLac  = 2.2f;

    public float caveB_WarpAmpX = 40f;
    public float caveB_WarpAmpY = 30f;

    [Header("Cave B: Final Noise (before threshold)")]
    public float caveB_FreqBase = 0.020f;
    public int   caveB_OctBase  = 3;
    public float caveB_PersBase = 0.5f;
    public float caveB_LacBase  = 2.0f;

    [Range(-1f, 1f)]
    public float caveB_Threshold = -0.10f;

    // ─────────────────────────────────────────────────────────
    // Depth mask: top에서는 거의 동굴 없음 → 아래 갈수록 많아짐
    // ─────────────────────────────────────────────────────────
    [Header("Cave Depth Mask")]
    public bool useCaveDepthMask = true;

    [Range(0f, 1f)]
    public float caveDepthStart = 0.25f;

    [Range(0f, 1f)]
    public float caveDepthEnd = 0.90f;

    // ─────────────────────────────────────────────────────────
    // Biome Pass: Desert (NEW)
    // ─────────────────────────────────────────────────────────
    [Header("Biome Pass: Desert")]
    [Min(0)] public int desertStartMinX = 0;
    [Min(0)] public int desertStartMaxX = 0;

    [Min(0)] public int desertTransitionLen = 10;
    [Range(0f, 1f)] public float desertTransitionChance = 0.5f;

    // ─────────────────────────────────────────────────────────
    // Biome Pass: Snow (NEW)
    // ─────────────────────────────────────────────────────────
    [Header("Biome Pass: Snow")]
    [Min(0)] public int snowEndMinX = 0;
    [Min(0)] public int snowEndMaxX = 0;

    [Min(0)] public int snowTransitionLen = 10;
    [Range(0f, 1f)] public float snowTransitionChance = 0.5f;

    // ─────────────────────────────────────────────────────────
    // Trees
    // ─────────────────────────────────────────────────────────
    [Header("Tree Settings")]
    [Range(0f, 1f)] public float treeDensity = 0.02f;
}
