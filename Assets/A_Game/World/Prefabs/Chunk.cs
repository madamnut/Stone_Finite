using UnityEngine;
using UnityEngine.Tilemaps;

public class Chunk : MonoBehaviour
{
    public const int ChunkSize = 16;

    // ── Tilemap 레이어: 후경 + 전경 ──
    public Tilemap bgTilemap;
    public Tilemap fgTilemap;

    // ── 라이트 메쉬 오브젝트 ──
    public GameObject lightMeshObject;

    [HideInInspector] public MeshFilter   lightMeshFilter;
    [HideInInspector] public MeshRenderer lightMeshRenderer;
    [HideInInspector] public Color32[]    lightColors; // (ChunkSize+1)^2

    // ── 타일 버퍼(재사용) ──
    [HideInInspector] public TileBase[] bgBuffer;
    [HideInInspector] public TileBase[] fgBuffer;

    // ── Dirty 플래그 ──
    [HideInInspector] public bool bgDirty    = false;
    [HideInInspector] public bool fgDirty    = false;
    [HideInInspector] public bool lightDirty = false;

    void Awake()
    {
        // 타일 버퍼
        int ts = ChunkSize * ChunkSize;
        bgBuffer = new TileBase[ts];
        fgBuffer = new TileBase[ts];

        // ── 라이트 메쉬: 미리 준비해 둔 객체에서 한 번에 참조 ──
        if (lightMeshObject == null)
        {
            Debug.LogError("[Chunk] lightMeshObject가 비었습니다. 미리 만든 라이트 오브젝트를 드래그하세요.");
            return;
        }

        lightMeshFilter   = lightMeshObject.GetComponent<MeshFilter>();
        lightMeshRenderer = lightMeshObject.GetComponent<MeshRenderer>();

        if (lightMeshFilter == null || lightMeshRenderer == null)
        {
            Debug.LogError("[Chunk] lightMeshObject에 MeshFilter 또는 MeshRenderer가 없습니다.");
            return;
        }

        // 메쉬가 비어있다면 청크 격자(17×17 정점) 메쉬를 생성
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
            // 기존 메쉬를 재사용. 정점수에 맞춰 컬러 버퍼 준비.
            lightColors = mesh.colors32;
            if (lightColors == null || lightColors.Length != vCount)
                lightColors = new Color32[vCount];
        }

        // 라이트 오브젝트를 청크 로컬(0,0)에 맞춤
        lightMeshObject.transform.SetParent(transform, false);
        lightMeshObject.transform.localPosition = Vector3.zero;
    }
}
