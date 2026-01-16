// ImageGenerator.cs
// ✅ 변경: Lava(FLUID_LAVA)가 AIR에 있을 때 빨간색으로 표시

using UnityEngine;
using UnityEngine.UI;

public class ImageGenerator : MonoBehaviour
{
    [Header("Generator")]
    public WorldGenSettings settings;
    [Tooltip("프리뷰에 사용할 시드")]
    public int seed = 0;

    [Header("Target")]
    public RawImage targetUI;
    public FilterMode filterMode = FilterMode.Point;
    public bool flipY = true;

    [Header("Unknown ID Color")]
    public Color unknownColor = new Color(1f, 0f, 1f, 1f); // 미지정 또는 예외

    [Header("Name → Color (인스펙터에서 직접 지정)")]
    public Color air  = new Color(0, 0, 0, 0);
    public Color rock = new Color(0.35f, 0.35f, 0.35f, 1);
    public Color dirt = new Color(0.47f, 0.28f, 0.19f, 1);

    // Grass(3~9) 대표색
    public Color grass = new Color(0.20f, 0.71f, 0.27f, 1);

    public Color clay = new Color(0.62f, 0.40f, 0.33f, 1);
    public Color mud  = new Color(0.35f, 0.25f, 0.20f, 1);

    public Color sand   = new Color(0.82f, 0.75f, 0.47f, 1);
    public Color gravel = new Color(0.55f, 0.51f, 0.47f, 1);

    public Color trunk            = new Color(0.43f, 0.27f, 0.12f, 1);
    public Color leaf             = new Color(0.16f, 0.63f, 0.24f, 1);
    public Color plant            = new Color(0.24f, 0.67f, 0.31f, 1);
    public Color bush             = new Color(0.27f, 0.59f, 0.27f, 1);
    public Color stone_Pile       = new Color(0.51f, 0.51f, 0.51f, 1);
    public Color small_Stone_Pile = new Color(0.59f, 0.59f, 0.59f, 1);

    // ✅ Desert additions
    public Color dead_Bush = new Color(0.45f, 0.36f, 0.22f, 1);

    // Agave(6타일) 대표색(프리뷰에서 한 색으로)
    public Color agave  = new Color(0.22f, 0.58f, 0.24f, 1);
    public Color cactus = new Color(0.15f, 0.55f, 0.18f, 1);

    // ✅ Sandstone / Pyramid
    public Color sandstone      = new Color(0.78f, 0.70f, 0.45f, 1);
    public Color sandstoneBrick = new Color(0.73f, 0.64f, 0.40f, 1);

    // ✅ Snow biome additions
    public Color frozenDirt   = new Color(0.40f, 0.33f, 0.30f, 1);
    public Color frozenGrass  = new Color(0.70f, 0.92f, 0.78f, 1);
    public Color iceCell      = new Color(0.70f, 0.85f, 0.95f, 0.95f);
    public Color snowCell     = new Color(0.92f, 0.95f, 0.98f, 1f);
    public Color snow         = new Color(0.95f, 0.97f, 1.00f, 1f);
    public Color frozenTrunk  = new Color(0.60f, 0.60f, 0.62f, 1f);
    public Color frozenPlant  = new Color(0.78f, 0.92f, 0.88f, 1f);
    public Color frozenBush   = new Color(0.74f, 0.88f, 0.84f, 1f);

    // ✅ Volcano biome additions
    public Color basalt   = new Color(0.18f, 0.18f, 0.20f, 1f);
    public Color tuff     = new Color(0.58f, 0.55f, 0.50f, 1f);
    public Color andesite = new Color(0.45f, 0.45f, 0.48f, 1f);

    public Color ore_Coal   = new Color(0.12f, 0.12f, 0.12f, 1);
    public Color ore_Copper = new Color(0.78f, 0.47f, 0.20f, 1);
    public Color ore_Iron   = new Color(0.71f, 0.71f, 0.78f, 1);
    public Color ore_Tin    = new Color(0.71f, 0.78f, 0.86f, 1);
    public Color granite    = new Color(0.59f, 0.59f, 0.67f, 1);
    public Color amphibolite= new Color(0.55f, 0.63f, 0.71f, 1);

    [Header("Fluid")]
    public Color water = new Color(0.20f, 0.43f, 0.82f, 0.78f);

    // ✅ Lava preview color
    public Color lava  = new Color(0.95f, 0.15f, 0.05f, 0.95f);

    Texture2D tex;

    // ── Solid IDs (ATT_Solid.json 기준) ──
    const ushort ID_AIR  = 0;
    const ushort ID_ROCK = 1;
    const ushort ID_DIRT = 2;

    // Grass 3~9
    const ushort ID_GRASS_TOP          = 3;
    const ushort ID_GRASS_LEFT         = 4;
    const ushort ID_GRASS_RIGHT        = 5;
    const ushort ID_GRASS_TOPLEFT      = 6;
    const ushort ID_GRASS_TOPRIGHT     = 7;
    const ushort ID_GRASS_LEFTRIGHT    = 8;
    const ushort ID_GRASS_TOPLEFTRIGHT = 9;

    const ushort ID_CLAY = 10;
    const ushort ID_MUD  = 11;

    const ushort ID_SAND   = 1000;
    const ushort ID_GRAVEL = 1001;

    const ushort ID_TRUNK = 2000;
    const ushort ID_LEAF  = 2001;
    const ushort ID_PLANT = 2002;
    const ushort ID_BUSH  = 2003;
    const ushort ID_STONE_PILE       = 2004;
    const ushort ID_SMALL_STONE_PILE = 2005;

    // ✅ Desert additions
    const ushort ID_DEAD_BUSH = 2006;

    // ✅ Agave 6 tiles
    const ushort ID_AGAVE_0 = 2007;
    const ushort ID_AGAVE_1 = 2008;
    const ushort ID_AGAVE_2 = 2009;
    const ushort ID_AGAVE_3 = 2010;
    const ushort ID_AGAVE_4 = 2011;
    const ushort ID_AGAVE_5 = 2012;
    const ushort ID_CACTUS  = 2013;

    // ✅ Snow additions (ATT_Solid.json 기준)
    // NOTE: Frozen Dirt 실제 ID로 맞춰야 함 (여기선 46 가정)
    const ushort ID_FROZEN_DIRT = 46;

    // Frozen Grass 37~43
    const ushort ID_FROZEN_GRASS_TOP          = 37;
    const ushort ID_FROZEN_GRASS_LEFT         = 38;
    const ushort ID_FROZEN_GRASS_RIGHT        = 39;
    const ushort ID_FROZEN_GRASS_TOPLEFT      = 40;
    const ushort ID_FROZEN_GRASS_TOPRIGHT     = 41;
    const ushort ID_FROZEN_GRASS_LEFTRIGHT    = 42;
    const ushort ID_FROZEN_GRASS_TOPLEFTRIGHT = 43;

    const ushort ID_ICE_CELL  = 44;
    const ushort ID_SNOW_CELL = 45;

    // ✅ Volcano solids
    const ushort ID_BASALT   = 47;
    const ushort ID_TUFF     = 48;
    const ushort ID_ANDESITE = 49;

    // Snow decor
    const ushort ID_SNOW         = 2014;
    const ushort ID_FROZEN_BUSH  = 2015;
    const ushort ID_FROZEN_PLANT = 2016;
    const ushort ID_FROZEN_TRUNK = 2017;

    // ✅ Sandstone + Pyramid brick
    const ushort ID_SANDSTONE       = 35;
    const ushort ID_SANDSTONE_BRICK = 36;

    const ushort ID_ORE_COAL   = 3000;
    const ushort ID_ORE_COPPER = 3001;
    const ushort ID_ORE_IRON   = 3002;
    const ushort ID_ORE_TIN    = 3003;

    const ushort ID_GRANITE     = 4000;
    const ushort ID_AMPHIBOLITE = 4001;

    // ── Fluid IDs (ATT_Fluid.json 기준) ──
    const ushort FLUID_NONE  = 0;
    const ushort FLUID_WATER = 1;
    const ushort FLUID_LAVA  = 2;

    void Start()
    {
        if (settings == null || targetUI == null)
        {
            Debug.LogError("ImageGenerator: settings/targetUI 미할당");
            return;
        }
        Repaint();
    }

    public void Repaint()
    {
        if (settings == null || targetUI == null) return;

        ushort[,] bg;
        ushort[,] commonFluid;
        ushort[,] commonSolid = WorldDataGenerator.GenerateCommonSolid(settings, seed, out bg, out commonFluid);

        EnsureTexture(commonSolid.GetLength(0), commonSolid.GetLength(1));
        Paint(commonSolid, commonFluid);
    }

    void EnsureTexture(int w, int h)
    {
        var t2d = targetUI.texture as Texture2D;
        if (t2d == null || t2d.width != w || t2d.height != h)
        {
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = filterMode;
            targetUI.texture = tex;
        }
        else
        {
            tex = t2d;
            tex.filterMode = filterMode;
        }
    }

    void Paint(ushort[,] commonSolid, ushort[,] commonFluid)
    {
        int w = commonSolid.GetLength(0);
        int h = commonSolid.GetLength(1);

        var px = new Color32[w * h];

        for (int y = 0; y < h; y++)
        {
            int yy = flipY ? (h - 1 - y) : y;
            int row = yy * w;

            for (int x = 0; x < w; x++)
            {
                ushort solidId = commonSolid[x, y];
                ushort fluidId = (commonFluid != null &&
                                  commonFluid.GetLength(0) == w &&
                                  commonFluid.GetLength(1) == h)
                                  ? commonFluid[x, y]
                                  : FLUID_NONE;

                Color c = ResolveColor(solidId, fluidId);
                px[row + x] = ToC32(c);
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, false);
    }

    Color ResolveColor(ushort solidId, ushort fluidId)
    {
        // ✅ fluid 우선 처리 (AIR 위에만 보이도록)
        if (solidId == ID_AIR)
        {
            if (fluidId == FLUID_LAVA)  return lava;
            if (fluidId == FLUID_WATER) return water;
        }

        return ResolveSolidColorById(solidId);
    }

    Color ResolveSolidColorById(ushort id)
    {
        switch (id)
        {
            case ID_AIR:  return air;
            case ID_ROCK: return rock;
            case ID_DIRT: return dirt;

            // Grass 3~9
            case ID_GRASS_TOP:
            case ID_GRASS_LEFT:
            case ID_GRASS_RIGHT:
            case ID_GRASS_TOPLEFT:
            case ID_GRASS_TOPRIGHT:
            case ID_GRASS_LEFTRIGHT:
            case ID_GRASS_TOPLEFTRIGHT:
                return grass;

            case ID_CLAY: return clay;
            case ID_MUD:  return mud;

            case ID_SAND:   return sand;
            case ID_GRAVEL: return gravel;

            case ID_TRUNK: return trunk;
            case ID_LEAF:  return leaf;
            case ID_PLANT: return plant;
            case ID_BUSH:  return bush;
            case ID_STONE_PILE:       return stone_Pile;
            case ID_SMALL_STONE_PILE: return small_Stone_Pile;

            case ID_DEAD_BUSH: return dead_Bush;

            // Agave 6 tiles
            case ID_AGAVE_0:
            case ID_AGAVE_1:
            case ID_AGAVE_2:
            case ID_AGAVE_3:
            case ID_AGAVE_4:
            case ID_AGAVE_5:
                return agave;
            case ID_CACTUS:
                return cactus;

            case ID_SANDSTONE:       return sandstone;
            case ID_SANDSTONE_BRICK: return sandstoneBrick;

            // ✅ Volcano solids
            case ID_BASALT:   return basalt;
            case ID_TUFF:     return tuff;
            case ID_ANDESITE: return andesite;

            // ✅ Snow biome solids/decor
            case ID_FROZEN_DIRT: return frozenDirt;

            case ID_FROZEN_GRASS_TOP:
            case ID_FROZEN_GRASS_LEFT:
            case ID_FROZEN_GRASS_RIGHT:
            case ID_FROZEN_GRASS_TOPLEFT:
            case ID_FROZEN_GRASS_TOPRIGHT:
            case ID_FROZEN_GRASS_LEFTRIGHT:
            case ID_FROZEN_GRASS_TOPLEFTRIGHT:
                return frozenGrass;

            case ID_ICE_CELL:  return iceCell;
            case ID_SNOW_CELL: return snowCell;

            case ID_SNOW:         return snow;
            case ID_FROZEN_TRUNK: return frozenTrunk;
            case ID_FROZEN_PLANT: return frozenPlant;
            case ID_FROZEN_BUSH:  return frozenBush;

            case ID_ORE_COAL:   return ore_Coal;
            case ID_ORE_COPPER: return ore_Copper;
            case ID_ORE_IRON:   return ore_Iron;
            case ID_ORE_TIN:    return ore_Tin;

            case ID_GRANITE:     return granite;
            case ID_AMPHIBOLITE: return amphibolite;

            default: return unknownColor;
        }
    }

    static Color32 ToC32(Color c)
    {
        return new Color32(
            (byte)Mathf.RoundToInt(c.r * 255f),
            (byte)Mathf.RoundToInt(c.g * 255f),
            (byte)Mathf.RoundToInt(c.b * 255f),
            (byte)Mathf.RoundToInt(c.a * 255f)
        );
    }
}
