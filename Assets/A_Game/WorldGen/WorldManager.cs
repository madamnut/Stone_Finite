using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// WorldManager: 청크 풀링, 코루틴, 버퍼 재사용, 타일 캐싱 최적화
/// • 월드 전체 데이터는 WorldData로 확장된 셀 정보 포함
/// • 청크 크기: 16×16
/// • 플레이어 반경 ChunkRadius 청크만 활성화
/// • 매 프레임 최대 maxLoadsPerFrame개의 청크 로드
/// • 플레이어 주변 청크 우선 로딩
/// </summary>
public class WorldManager : MonoBehaviour
{
    [Header("월드 생성 설정")] public WorldGenSettings settings;

    [Header("청크 Prefab & 관리")]
    public GameObject chunkPrefab;
    public Transform chunkRoot;
    public int initialPoolSize = 50;

    [Header("플레이어 및 렌더링 설정")]
    public Transform player;
    public int ChunkRadius = 7;
    [Tooltip("한 프레임당 최대 로드할 청크 개수")] public int maxLoadsPerFrame = 2;

    public const int ChunkSize = 16;

    // 전체 월드 데이터 (확장된 CellData fg, bg ID 배열)
    private WorldData worldMap;

    // 풀링 / 로딩 큐 / 임시 리스트
    private Queue<GameObject> chunkPool = new Queue<GameObject>();
    private List<Vector2Int> loadList = new List<Vector2Int>();
    private List<Vector2Int> unloadList = new List<Vector2Int>();

    // 현재 필요 청크 집합
    private HashSet<Vector2Int> currentNeeded = new HashSet<Vector2Int>();

    private bool isLoading = false;
    private Vector2Int lastPlayerChunk;

    // 활성화된 청크
    private Dictionary<Vector2Int, GameObject> activeChunks = new Dictionary<Vector2Int, GameObject>();

    void Awake()
    {
        worldMap = WorldDataGenerator.Generate(settings);
        if (chunkRoot == null) chunkRoot = transform;

        for (int i = 0; i < initialPoolSize; i++)
        {
            var go = Instantiate(chunkPrefab, chunkRoot);
            go.SetActive(false);
            chunkPool.Enqueue(go);
        }

        if (chunkPrefab == null) Debug.LogError("WorldManager: Chunk Prefab이 없습니다.");
        if (player == null)      Debug.LogError("WorldManager: Player Transform이 없습니다.");

        lastPlayerChunk = GetPlayerChunk();
    }

    void Update() => UpdateVisibleChunks();

    private void UpdateVisibleChunks()
    {
        Vector2Int playerChunk = GetPlayerChunk();

        // 텔레포트 감지
        if ((playerChunk - lastPlayerChunk).sqrMagnitude > (ChunkRadius * ChunkRadius * 4))
            loadList.Clear();
        lastPlayerChunk = playerChunk;

        // 필요 청크 계산
        currentNeeded.Clear();
        for (int dy = -ChunkRadius; dy <= ChunkRadius; dy++)
            for (int dx = -ChunkRadius; dx <= ChunkRadius; dx++)
                currentNeeded.Add(new Vector2Int(playerChunk.x + dx, playerChunk.y + dy));

        // 언로딩
        unloadList.Clear();
        foreach (var coord in activeChunks.Keys)
            if (!currentNeeded.Contains(coord)) unloadList.Add(coord);
        foreach (var coord in unloadList)
        {
            ReturnToPool(activeChunks[coord]);
            activeChunks.Remove(coord);
        }

        // 로딩 후보
        loadList = currentNeeded
            .Where(c => !activeChunks.ContainsKey(c))
            .OrderBy(c => (c.x - playerChunk.x) * (c.x - playerChunk.x) + (c.y - playerChunk.y) * (c.y - playerChunk.y))
            .ToList();

        // 코루틴 시작
        if (!isLoading && loadList.Count > 0)
            StartCoroutine(ProcessLoadQueue());
    }

    private IEnumerator ProcessLoadQueue()
    {
        isLoading = true;
        int loads = 0;
        while (loads < maxLoadsPerFrame && loadList.Count > 0)
        {
            var coord = loadList[0];
            loadList.RemoveAt(0);
            if (!currentNeeded.Contains(coord)) continue;
            CreateChunk(coord);
            loads++;
        }
        yield return null;
        isLoading = false;
    }

    private Vector2Int GetPlayerChunk()
    {
        Vector3 p = player.position;
        return new Vector2Int(
            Mathf.FloorToInt(p.x / ChunkSize),
            Mathf.FloorToInt(p.y / ChunkSize)
        );
    }

    private GameObject GetFromPool()
    {
        if (chunkPool.Count > 0) return chunkPool.Dequeue();
        var go = Instantiate(chunkPrefab, chunkRoot);
        go.SetActive(false);
        return go;
    }

    private void ReturnToPool(GameObject go)
    {
        go.SetActive(false);
        chunkPool.Enqueue(go);
    }

    private void CreateChunk(Vector2Int coord)
    {
        var go = GetFromPool();
        go.SetActive(true);
        go.name = $"Chunk_{coord.x}_{coord.y}";
        go.transform.localPosition = new Vector3(coord.x * ChunkSize, coord.y * ChunkSize, 0f);

        var chunkComp = go.GetComponent<Chunk>();
        if (chunkComp == null) return;

        var bgBuf = chunkComp.bgBuffer;
        var fgBuf = chunkComp.fgBuffer;
        int size = ChunkSize * ChunkSize;
        for (int i = 0; i < size; i++) { bgBuf[i] = null; fgBuf[i] = null; }

        var bounds = new BoundsInt(0, 0, 0, ChunkSize, ChunkSize, 1);
        for (int y = 0; y < ChunkSize; y++)
            for (int x = 0; x < ChunkSize; x++)
            {
                int wx = coord.x * ChunkSize + x;
                int wy = coord.y * ChunkSize + y;
                int idx = y * ChunkSize + x;
                if (wx < 0 || wx >= settings.width || wy < 0 || wy >= settings.height)
                    continue;

                // 배경 타일 (ID)
                bgBuf[idx] = TileCache.Get(worldMap.bg[wx, wy]);

                // 전경 타일 (CellData 활용)
                var cell = worldMap.fg[wx, wy];
                var tile = TileCache.Get(cell.id);
                tile.colliderType = cell.hasCollider ? Tile.ColliderType.Sprite : Tile.ColliderType.None;
                fgBuf[idx] = tile;
            }

        chunkComp.bgTilemap.SetTilesBlock(bounds, bgBuf);
        chunkComp.fgTilemap.SetTilesBlock(bounds, fgBuf);
        activeChunks[coord] = go;
    }

    // 타일 캐시
    private static class TileCache
    {
        private static Dictionary<ushort, Tile> cache = new Dictionary<ushort, Tile>();
        public static Tile Get(ushort id)
        {
            if (cache.TryGetValue(id, out var tile)) return tile;
            var newTile = ScriptableObject.CreateInstance<Tile>();
            newTile.sprite = BlockLibrary.GetSprite(id);
            newTile.name   = BlockLibrary.GetName(id);
            cache[id] = newTile;
            return newTile;
        }
    }
}
