// ImageGenerator.cs (전체 교체본)
// - WorldDataGenerator.GenerateCommonSolid() 기반 프리뷰
// - Solid ID는 ATT_Solid.json 기준 (WorldDataGenerator 상수와 동일)
// - Fluid는 commonFluid(ATT_Fluid.json) 기준: 0 none, 1 water
// - Grass는 이제 id=3 + meta 변형인데, meta가 프리뷰 API로 안 나와서 단일 색으로 표시

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
    public Color air                = new Color(0, 0, 0, 0);
    public Color rock               = new Color(0.35f, 0.35f, 0.35f, 1);
    public Color dirt               = new Color(0.47f, 0.28f, 0.19f, 1);

    // Grass는 이제 id=3 고정 + meta 변형인데, 여기선 meta 없으므로 대표색 1개만 사용
    public Color grass              = new Color(0.20f, 0.71f, 0.27f, 1);

    // ✅ 변경: Clay id=4, Mud id=5
    public Color clay               = new Color(0.62f, 0.40f, 0.33f, 1);
    public Color mud                = new Color(0.35f, 0.25f, 0.20f, 1);

    public Color sand               = new Color(0.82f, 0.75f, 0.47f, 1);
    public Color gravel             = new Color(0.55f, 0.51f, 0.47f, 1);
    public Color trunk              = new Color(0.43f, 0.27f, 0.12f, 1);
    public Color leaf               = new Color(0.16f, 0.63f, 0.24f, 1);
    public Color plant              = new Color(0.24f, 0.67f, 0.31f, 1);
    public Color bush               = new Color(0.27f, 0.59f, 0.27f, 1);
    public Color stone_Pile         = new Color(0.51f, 0.51f, 0.51f, 1);
    public Color small_Stone_Pile   = new Color(0.59f, 0.59f, 0.59f, 1);
    public Color ore_Coal           = new Color(0.12f, 0.12f, 0.12f, 1);
    public Color ore_Copper         = new Color(0.78f, 0.47f, 0.20f, 1);
    public Color ore_Iron           = new Color(0.71f, 0.71f, 0.78f, 1);
    public Color ore_Tin            = new Color(0.71f, 0.78f, 0.86f, 1);
    public Color granite            = new Color(0.59f, 0.59f, 0.67f, 1);
    public Color amphibolite        = new Color(0.55f, 0.63f, 0.71f, 1);

    [Header("Fluid")]
    public Color water              = new Color(0.20f, 0.43f, 0.82f, 0.78f);

    Texture2D tex;

    // ── Solid IDs (ATT_Solid.json 기준) ──
    const ushort ID_AIR   = 0;
    const ushort ID_ROCK  = 1;
    const ushort ID_DIRT  = 2;
    const ushort ID_GRASS = 3;
    const ushort ID_CLAY  = 4;
    const ushort ID_MUD   = 5;

    const ushort ID_SAND   = 1000;
    const ushort ID_GRAVEL = 1001;

    const ushort ID_TRUNK = 2000;
    const ushort ID_LEAF  = 2001;
    const ushort ID_PLANT = 2002;
    const ushort ID_BUSH  = 2003;
    const ushort ID_STONE_PILE = 2004;
    const ushort ID_SMALL_STONE_PILE = 2005;

    const ushort ID_ORE_COAL   = 3000;
    const ushort ID_ORE_COPPER = 3001;
    const ushort ID_ORE_IRON   = 3002;
    const ushort ID_ORE_TIN    = 3003;

    const ushort ID_GRANITE     = 4000;
    const ushort ID_AMPHIBOLITE = 4001;

    // ── Fluid IDs (ATT_Fluid.json 기준) ──
    const ushort FLUID_NONE  = 0;
    const ushort FLUID_WATER = 1;

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

        // ✅ 변경: GenerateCommonSolid + commonFluid 같이 받기
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
            // ✅ flipY=true면 y를 뒤집어서 표시
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
        // 기본은 Solid
        Color baseC = ResolveSolidColorById(solidId);

        // Fluid overlay 규칙(간단):
        // - 공기 위에 물 있으면 물 색
        // - 그 외는 Solid 유지 (물 색을 섞고 싶으면 여기서 Lerp로 바꾸면 됨)
        if (fluidId == FLUID_WATER && solidId == ID_AIR)
            return water;

        return baseC;
    }

    Color ResolveSolidColorById(ushort id)
    {
        switch (id)
        {
            case ID_AIR:   return air;
            case ID_ROCK:  return rock;

            case ID_DIRT:  return dirt;
            case ID_GRASS: return grass;

            case ID_CLAY:  return clay;
            case ID_MUD:   return mud;

            case ID_SAND:   return sand;
            case ID_GRAVEL: return gravel;

            case ID_TRUNK: return trunk;
            case ID_LEAF:  return leaf;
            case ID_PLANT: return plant;
            case ID_BUSH:  return bush;
            case ID_STONE_PILE: return stone_Pile;
            case ID_SMALL_STONE_PILE: return small_Stone_Pile;

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
