using UnityEngine;
using UnityEngine.Tilemaps;

public class Chunk : MonoBehaviour
{
    public const int ChunkSize = 16;

    // ── Tilemap 레이어: 후경 + 솔리드 + 유체 ──
    public Tilemap bgTilemap;
    public Tilemap solidTilemap;
    public Tilemap liquidTilemap;

    // ── 라이트 메쉬 오브젝트 ──
    public GameObject lightMeshObject;

    [HideInInspector] public MeshFilter   lightMeshFilter;
    [HideInInspector] public MeshRenderer lightMeshRenderer;
    [HideInInspector] public Color32[]    lightColors; // (ChunkSize+1)^2

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

        // R8을 쓰고 싶으면 TextureFormat.R8로 바꿔도 됨(URP/플랫폼 호환 확인 필요)
        liquidTypeTex = new Texture2D(ChunkSize, ChunkSize, TextureFormat.RGBA32, false, true);
        liquidTypeTex.filterMode = FilterMode.Point;
        liquidTypeTex.wrapMode   = TextureWrapMode.Clamp;

        liquidAmountTex = new Texture2D(ChunkSize, ChunkSize, TextureFormat.RGBA32, false, true);
        liquidAmountTex.filterMode = FilterMode.Point;
        liquidAmountTex.wrapMode   = TextureWrapMode.Clamp;

        // ── 라이트 메쉬 ──
        lightMeshFilter   = lightMeshObject.GetComponent<MeshFilter>();
        lightMeshRenderer = lightMeshObject.GetComponent<MeshRenderer>();

        var mesh = lightMeshFilter.sharedMesh;

        int vW = ChunkSize + 1;
        int vH = ChunkSize + 1;
        int vCount = vW * vH;

        bool needBuild = (mesh == null) || (mesh.vertexCount != vCount);

        if (needBuild)
        {
            mesh = new Mesh
            {
                indexFormat = (vCount > 65000)
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
                name = "ChunkLightMesh"
            };

            var verts = new Vector3[vCount];
            var uvs   = new Vector2[vCount];
            lightColors = new Color32[vCount];

            for (int y = 0; y < vH; y++)
            {
                for (int x = 0; x < vW; x++)
                {
                    int i = y * vW + x;
                    verts[i] = new Vector3(x, y, 0f); // 로컬 0..16 격자
                    uvs[i]   = new Vector2(x / (float)ChunkSize, y / (float)ChunkSize);
                    lightColors[i] = new Color32(0, 0, 0, 0);
                }
            }

            int quadCount = ChunkSize * ChunkSize;
            var tris = new int[quadCount * 6];
            int ti = 0;

            for (int y = 0; y < ChunkSize; y++)
            {
                for (int x = 0; x < ChunkSize; x++)
                {
                    int v0 = y * vW + x;
                    int v1 = v0 + 1;
                    int v2 = v0 + vW;
                    int v3 = v2 + 1;

                    tris[ti++] = v0; tris[ti++] = v2; tris[ti++] = v1;
                    tris[ti++] = v1; tris[ti++] = v2; tris[ti++] = v3;
                }
            }

            mesh.vertices  = verts;
            mesh.uv        = uvs;
            mesh.triangles = tris;
            mesh.colors32  = lightColors;
            mesh.RecalculateBounds();

            lightMeshFilter.sharedMesh = mesh;
        }
        else
        {
            lightColors = mesh.colors32;
            if (lightColors == null || lightColors.Length != vCount)
                lightColors = new Color32[vCount];
        }

        // 라이트 오브젝트를 청크 로컬(0,0)에 맞춤
        lightMeshObject.transform.SetParent(transform, false);
        lightMeshObject.transform.localPosition = Vector3.zero;
    }
}
