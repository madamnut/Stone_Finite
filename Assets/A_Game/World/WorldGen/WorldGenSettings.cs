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
    // Volcano strata (NEW)
    // - Tuff: absolute height-map band (like dirt/rock)
    // - Andesite/Basalt: "inverse height-map" thickness taken from rockTop downward
    //   (and only consumes cells that are ID_ROCK in generator logic)
    // ─────────────────────────────────────────────────────────
    [Header("Volcano Strata: Tuff (Absolute Height)")]
    public float tuffBaseHeight = 0f;
    public float tuffRange      = 0f;

    public float tuffNoiseBaseFrequency = 0.005f;
    public int   tuffNoiseOctaves       = 4;
    [Range(0f, 1f)] public float tuffNoisePersistence = 0.5f;
    public float tuffNoiseLacunarity    = 2f;

    [Header("Volcano Strata: Andesite (Inverse Thickness from rockTop)")]
    public float andesiteBaseHeight = 0f; // interpreted as thickness, not absolute y
    public float andesiteRange      = 0f; // interpreted as thickness range

    public float andesiteNoiseBaseFrequency = 0.005f;
    public int   andesiteNoiseOctaves       = 4;
    [Range(0f, 1f)] public float andesiteNoisePersistence = 0.5f;
    public float andesiteNoiseLacunarity    = 2f;

    [Header("Volcano Strata: Basalt (Inverse Thickness from rockTop, below Andesite)")]
    public float basaltBaseHeight = 0f; // interpreted as thickness, not absolute y
    public float basaltRange      = 0f; // interpreted as thickness range

    public float basaltNoiseBaseFrequency = 0.005f;
    public int   basaltNoiseOctaves       = 4;
    [Range(0f, 1f)] public float basaltNoisePersistence = 0.5f;
    public float basaltNoiseLacunarity    = 2f;

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
    // Biome Pass: Crevasse (NEW)
    // - 월드 최좌측 구간: [0 .. crevasseWidth]
    // - 완충지대(transition) 기본 10, 확률 50%
    // ─────────────────────────────────────────────────────────
    [Header("Biome Pass: Crevasse")]
    [Min(0)] public int crevasseWidth = 0;

    [Min(0)] public int crevasseTransitionLen = 10;
    [Range(0f, 1f)] public float crevasseTransitionChance = 0.5f;

    [Header("Crevasse Shape")]
    public int   crevasseMaxDepth = 300;
    public float crevasseLeftCurve  = 2.0f;
    public float crevasseRightCurve = 2.0f;

    public float crevasseWallSharpnessLeft  = 10.0f;
    public float crevasseWallSharpnessRight = 6.0f;

    [Header("Crevasse Noise")]
    public float crevasseRidgeJagAmp  = 8.0f;
    public float crevasseRidgeJagFreq = 0.02f;

    public float crevasseWallNoiseAmp  = 6.0f;
    public float crevasseWallNoiseFreq = 0.03f;

    // ─────────────────────────────────────────────────────────
    // Biome Pass: Volcano (NEW)
    // - 월드 최우측 구간: [width - volcanoWidth .. width]
    // - 완충지대(transition) 기본 10, 확률 50%
    // ─────────────────────────────────────────────────────────
    [Header("Biome Pass: Volcano")]
    [Min(0)] public int volcanoWidth = 0;

    [Min(0)] public int volcanoTransitionLen = 10;
    [Range(0f, 1f)] public float volcanoTransitionChance = 0.5f;

    [Header("Volcano Shape")]
    public int   volcanoPeakAddHeight = 250;
    public float volcanoShapeSharpness = 3.0f;

    [Header("Volcano Noise")]
    public float volcanoDetailAmp = 12.0f;
    public float volcanoDetailFreq = 0.02f;
    public float volcanoDetailCenterBoost = 0.6f;

    // ─────────────────────────────────────────────────────────
    // NEW: Magma Chamber (Dormant Volcano)
    // - Anchor x/y is computed in code (NOT a setting):
    //   x = volcano biome center, y = first Amphibolite encountered while going down at x.
    // - All lava conduits/branches MUST stop when encountering Tuff (solid id=48).
    // - Lava fluid id = 2.
    //
    // CHANGE (Bezier-based):
    // - Trunk centerline is a cubic bezier generated deterministically from seed (no wobble params).
    // - Branch end is determined by 45-degree raycast until Tuff (no length params).
    // - Branch spawns probabilistically per "vertical step" along trunk centerline.
    // ─────────────────────────────────────────────────────────
    [Header("Magma Chamber: Main (Lens)")]
    [Min(1)] public int magmaMainRadiusX = 40;   // 가로 반지름(타일)
    [Min(1)] public int magmaMainRadiusY = 22;   // 세로 반지름(타일)

    [Header("Magma Chamber: Main Distortion")]
    [Range(0f, 1f)] public float magmaTopSquash = 0.10f;  // 상단 눌림(0~1, 0이면 없음)
    [Min(0)] public int magmaEdgeJitter = 2;              // 경계 찌그러짐(타일)

    [Header("Magma Trunk: Thickness (Bezier Tube)")]
    [Min(1)] public int magmaTrunkWidthStart = 40; // 시작 두께(타일)
    [Min(1)] public int magmaTrunkWidthEnd   = 4;  // 끝 두께(타일)

    // ✅ (삭제/미사용) Drunk-walk wobble 파라미터들:
    // public int magmaTrunkMaxDx = 1;
    // public int magmaTrunkDriftMinSteps = 6;
    // public int magmaTrunkDriftMaxSteps = 14;

    [Header("Magma Branches (Spawn per Trunk Step)")]
    [Range(0f, 1f)] public float magmaBranchChancePerStep = 0.08f;

    [Header("Magma Branches: Thickness Range (Start/End)")]
    [Min(1)] public int magmaBranchWidthStartMin = 2;
    [Min(1)] public int magmaBranchWidthStartMax = 4;

    [Min(1)] public int magmaBranchWidthEndMin = 1;
    [Min(1)] public int magmaBranchWidthEndMax = 2;

    // ✅ (삭제/미사용) 기존 count/len/width/walk params:
    // public int magmaBranchCount = 5;
    // public int magmaBranchLenMin = 18;
    // public int magmaBranchLenMax = 45;
    // public int magmaBranchWidthMin = 2;
    // public int magmaBranchWidthMax = 4;
    // public float magmaBranchP_Up = 0.70f;
    // public float magmaBranchP_Left = 0.15f;
    // public float magmaBranchP_Right = 0.15f;
    // public float magmaBranchInertia = 0.20f;

    [Header("Magma Pockets (Small Chambers)")]
    [Range(0f, 1f)] public float magmaPocketChanceAtBranchEnd = 0.55f;
    [Range(0f, 1f)] public float magmaPocketChanceAlongBranch = 0.10f;

    [Min(0)] public int magmaPocketCountNearTrunkMin = 0;
    [Min(0)] public int magmaPocketCountNearTrunkMax = 2;

    [Min(1)] public int magmaPocketRadiusXMin = 4;
    [Min(1)] public int magmaPocketRadiusXMax = 10;
    [Min(1)] public int magmaPocketRadiusYMin = 3;
    [Min(1)] public int magmaPocketRadiusYMax = 7;

    [Min(0)] public int magmaPocketEdgeJitter = 1;

    // ─────────────────────────────────────────────────────────
    // Trees
    // ─────────────────────────────────────────────────────────
    [Header("Tree Settings")]
    [Range(0f, 1f)] public float treeDensity = 0.02f;
}
