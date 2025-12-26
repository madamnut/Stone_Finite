using UnityEngine;
using UnityEngine.Tilemaps;

public class Chunk : MonoBehaviour
{
    public const int ChunkSize = 16;

    // ── Tilemap 레이어: 후경 + 솔리드 + 유체 ──
    public Tilemap bgTilemap;
    public Tilemap solidTilemap;
    public Tilemap liquidTilemap;

    // ── Light Overlay (Quad) ──
    // 프리팹에서 LightOverlay 오브젝트를 연결 (MeshRenderer 보유)
    public MeshRenderer lightOverlayRenderer;

    // ── 타일 버퍼(재사용) ──
    [HideInInspector] public TileBase[] bgBuffer;
    [HideInInspector] public TileBase[] solidBuffer;
    [HideInInspector] public TileBase[] liquidBuffer;

    // ── Dirty 플래그 ──
    [HideInInspector] public bool bgDirty     = false;
    [HideInInspector] public bool solidDirty  = false;
    [HideInInspector] public bool liquidDirty = false;
    [HideInInspector] public bool lightDirty  = false;

    // ── Liquid Mask (청크별 렌더 분기용) ──
    [HideInInspector] public Texture2D liquidTypeTex;     // 16x16, R=liquidId(0..255)
    [HideInInspector] public Texture2D liquidAmountTex;   // 16x16, R=amount(0..128)
    [HideInInspector] public Color32[] liquidTypePixels;  // 256
    [HideInInspector] public Color32[] liquidAmtPixels;   // 256
    [HideInInspector] public MaterialPropertyBlock liquidMpb;
    [HideInInspector] public TilemapRenderer liquidRenderer;

    // ── Light Texture (청크별 1회 생성, 이후 재사용) ──
    // 18x18: 가운데 16x16 + 테두리 1픽셀 패딩(인접 청크 보간용)
    [HideInInspector] public Texture2D lightTex;          // 18x18, RGBA (A에 어둠 알파)
    [HideInInspector] public Color32[] lightPixels;       // 18*18
    [HideInInspector] public MaterialPropertyBlock lightMpb;

    void Awake()
    {
        // 타일 버퍼
        int ts = ChunkSize * ChunkSize;
        bgBuffer     = new TileBase[ts];
        solidBuffer  = new TileBase[ts];
        liquidBuffer = new TileBase[ts];

        // ── Liquid Mask 기본 리소스 (청크당 1회 생성, 이후 재사용) ──
        liquidRenderer = liquidTilemap.GetComponent<TilemapRenderer>();
        liquidMpb = new MaterialPropertyBlock();

        liquidTypePixels = new Color32[ts];
        liquidAmtPixels  = new Color32[ts];

        liquidTypeTex = new Texture2D(ChunkSize, ChunkSize, TextureFormat.RGBA32, false, true);
        liquidTypeTex.filterMode = FilterMode.Point;
        liquidTypeTex.wrapMode   = TextureWrapMode.Clamp;

        liquidAmountTex = new Texture2D(ChunkSize, ChunkSize, TextureFormat.RGBA32, false, true);
        liquidAmountTex.filterMode = FilterMode.Point;
        liquidAmountTex.wrapMode   = TextureWrapMode.Clamp;

        // ── Light Overlay 기본 리소스 (청크당 1회 생성, 이후 재사용) ──
        lightMpb = new MaterialPropertyBlock();

        // LightOverlayRenderer가 없으면 Light 레이어는 비활성(렌더는 안 하지만 게임 진행엔 영향 없음)
        if (lightOverlayRenderer != null)
        {
            const int L = ChunkSize + 2; // 18
            lightPixels = new Color32[L * L];

            lightTex = new Texture2D(L, L, TextureFormat.RGBA32, false, true);
            lightTex.filterMode = FilterMode.Bilinear;
            lightTex.wrapMode   = TextureWrapMode.Clamp;

            // 초기값: 완전 투명(어둠 없음)
            for (int i = 0; i < lightPixels.Length; i++)
                lightPixels[i] = new Color32(0, 0, 0, 0);

            lightTex.SetPixels32(lightPixels);
            lightTex.Apply(false, false);

            // MPB에 텍스처 바인딩 (프로퍼티 이름은 셰이더에 맞춰 통일)
            lightOverlayRenderer.GetPropertyBlock(lightMpb);
            lightMpb.SetTexture("_LightTex", lightTex);
            lightOverlayRenderer.SetPropertyBlock(lightMpb);
        }
    }
}
