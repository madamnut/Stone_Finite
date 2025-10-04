// ImageGenerator.cs
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
    public Color unknownColor = new Color(1f, 0f, 1f, 1f); // 미지정 또는 예외(Primal Workbench)

    [Header("Name → Color (인스펙터에서 직접 지정)")]
    public Color air                = new Color(0, 0, 0, 0);
    public Color rock               = new Color(0.35f, 0.35f, 0.35f, 1);
    public Color dirt               = new Color(0.47f, 0.28f, 0.19f, 1);
    public Color grass_Left         = new Color(0.16f, 0.67f, 0.24f, 1);
    public Color grass_Top          = new Color(0.20f, 0.71f, 0.27f, 1);
    public Color grass_Right        = new Color(0.16f, 0.67f, 0.24f, 1);
    public Color grass_TopLeft      = new Color(0.18f, 0.69f, 0.25f, 1);
    public Color grass_TopRight     = new Color(0.18f, 0.69f, 0.25f, 1);
    public Color grass_LeftRight    = new Color(0.18f, 0.69f, 0.25f, 1);
    public Color grass_TopLeftRight = new Color(0.20f, 0.71f, 0.27f, 1);
    public Color clay               = new Color(0.62f, 0.40f, 0.33f, 1); // ID:10
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
    public Color water              = new Color(0.20f, 0.43f, 0.82f, 0.78f);

    Texture2D tex;

    void Start()
    {
        if (settings == null || targetUI == null)
        {
            Debug.LogError("ImageGenerator: settings/targetUI 미할당");
            return;
        }

        ushort[,] common = WorldDataGenerator.GenerateCommon(settings, seed, out _);
        EnsureTexture(common.GetLength(0), common.GetLength(1));
        Paint(common);
    }

    public void Repaint()
    {
        if (settings == null || targetUI == null) return;
        ushort[,] common = WorldDataGenerator.GenerateCommon(settings, seed, out _);
        EnsureTexture(common.GetLength(0), common.GetLength(1));
        Paint(common);
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

    void Paint(ushort[,] common)
    {
        int w = common.GetLength(0), h = common.GetLength(1);
        var px = new Color32[w * h];

        for (int y = 0; y < h; y++)
        {
            int yy = flipY ? y : (h - 1 - y);
            int row = yy * w;

            for (int x = 0; x < w; x++)
            {
                ushort id = common[x, y];
                px[row + x] = ToC32(ResolveColorById(id));
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, false);
    }

    // ATT_Cell.json 이름 규칙과 ID를 하드코딩 매핑. Primal Workbench(50000)는 예외로 unknownColor 처리.
    Color ResolveColorById(ushort id)
    {
        switch (id)
        {
            case 0:    return air;
            case 1:    return rock;

            case 2:    return dirt;
            case 3:    return grass_Left;
            case 4:    return grass_Top;
            case 5:    return grass_Right;
            case 6:    return grass_TopLeft;
            case 7:    return grass_TopRight;
            case 8:    return grass_LeftRight;
            case 9:    return grass_TopLeftRight;

            case 10:   return clay;

            case 1000: return sand;
            case 1001: return gravel;

            case 2000: return trunk;
            case 2001: return leaf;
            case 2002: return plant;
            case 2003: return bush;
            case 2004: return stone_Pile;
            case 2005: return small_Stone_Pile;

            case 3000: return ore_Coal;
            case 3001: return ore_Copper;
            case 3002: return ore_Iron;
            case 3003: return ore_Tin;

            case 4000: return granite;
            case 4001: return amphibolite;

            case 50000: return unknownColor; // Primal Workbench 예외

            case 60000: return water;

            default:    return unknownColor;
        }
    }

    static Color32 ToC32(Color c) => new Color32(
        (byte)Mathf.RoundToInt(c.r * 255f),
        (byte)Mathf.RoundToInt(c.g * 255f),
        (byte)Mathf.RoundToInt(c.b * 255f),
        (byte)Mathf.RoundToInt(c.a * 255f));
}
