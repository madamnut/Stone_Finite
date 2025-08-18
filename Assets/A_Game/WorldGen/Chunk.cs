using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Chunk: 청크 프리팹에 붙어 BG/FG/Light Tilemap을 인스펙터에서 할당받고,
/// 내부에 TileBase 배열 버퍼를 재사용하며
/// Dirty 플래그로 레이어별 갱신을 지원합니다.
/// </summary>
public class Chunk : MonoBehaviour
{
    public const int ChunkSize = 16;

    public Tilemap bgTilemap;
    public Tilemap fgTilemap;
    public Tilemap lightTilemap;

    // 타일 배열 버퍼 (배열 재사용)
    [HideInInspector] public TileBase[] bgBuffer;
    [HideInInspector] public TileBase[] fgBuffer;
    [HideInInspector] public TileBase[] lightBuffer;

    // Dirty 플래그
    [HideInInspector] public bool bgDirty = false;
    [HideInInspector] public bool fgDirty = false;
    [HideInInspector] public bool lightDirty = false;

    void Awake()
    {
        // 최초 Awake 시에만 버퍼 할당
        int size = ChunkSize * ChunkSize;
        bgBuffer    = new TileBase[size];
        fgBuffer    = new TileBase[size];
        lightBuffer = new TileBase[size];
    }
}
