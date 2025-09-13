using UnityEngine;
using UnityEngine.Tilemaps;

public class Chunk : MonoBehaviour
{
    public const int ChunkSize = 16;

    // ── Tilemap 레이어 ──
    public Tilemap bgTilemap;
    public Tilemap fgTilemap;
    public Tilemap decoTilemap;
    public Tilemap liquidTilemap;
    public Tilemap lightTilemap;

    // ── 버퍼 (재사용) ──
    [HideInInspector] public TileBase[] bgBuffer;
    [HideInInspector] public TileBase[] fgBuffer;
    [HideInInspector] public TileBase[] decoBuffer;
    [HideInInspector] public TileBase[] liquidBuffer;
    [HideInInspector] public TileBase[] lightBuffer;

    // ── Dirty 플래그 ──
    [HideInInspector] public bool bgDirty    = false;
    [HideInInspector] public bool fgDirty    = false;
    [HideInInspector] public bool decoDirty  = false;
    [HideInInspector] public bool liquidDirty= false;
    [HideInInspector] public bool lightDirty = false;

    void Awake()
    {
        int size = ChunkSize * ChunkSize;
        bgBuffer     = new TileBase[size];
        fgBuffer     = new TileBase[size];
        decoBuffer   = new TileBase[size];
        liquidBuffer = new TileBase[size];
        lightBuffer  = new TileBase[size];
    }
}
