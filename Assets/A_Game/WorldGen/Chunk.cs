using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Chunk: 청크 프리팹에 붙어 BG/FG Tilemap을 인스펙터에서 할당받고,
/// 내부에 TileBase 배열 버퍼를 재사용합니다.
/// </summary>
public class Chunk : MonoBehaviour
{
    private const int ChunkSize = 16;

    [Tooltip("배경 벽용 Tilemap (BG 레이어)")]
    public Tilemap bgTilemap;

    [Tooltip("전경 지형용 Tilemap (FG 레이어)")]
    public Tilemap fgTilemap;

    // 타일 배열 버퍼 (배열 재사용)
    [HideInInspector] public TileBase[] bgBuffer;
    [HideInInspector] public TileBase[] fgBuffer;

    void Awake()
    {
        // 최초 Awake 시에만 버퍼 할당
        bgBuffer = new TileBase[ChunkSize * ChunkSize];
        fgBuffer = new TileBase[ChunkSize * ChunkSize];
    }
}