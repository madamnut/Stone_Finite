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
/// • Dirty 플래그 기반 레이어별 갱신 지원
/// • Light 레이어(tilemap + buffer) 지원, FG 변경 시 빛 국소 재계산
/// </summary>
public class WorldManager : MonoBehaviour
{
    [Header("월드 생성 설정")]
    public WorldGenSettings settings;

    [Header("청크 Prefab & 관리")]
    public GameObject chunkPrefab;
    public Transform chunkRoot;
    public int initialPoolSize = 50;

    [Header("플레이어 및 렌더링 설정")]
    public Transform player;
    public int ChunkRadius = 7;
    [Tooltip("한 프레임당 최대 로드할 청크 개수")]
    public int maxLoadsPerFrame = 2;

    [Header("Light 레이어용 검정 스프라이트")]
    public Sprite lightSprite;

    public const int ChunkSize = 16;

    // 전체 월드 데이터 (fg, bg, light)
    public WorldData worldMap;

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
        if (player == null) Debug.LogError("WorldManager: Player Transform이 없습니다.");

        lastPlayerChunk = GetPlayerChunk();
    }

    void Update() => UpdateVisibleChunks();
    void LateUpdate() => ProcessDirtyChunks();

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
            .OrderBy(c =>
                (c.x - playerChunk.x) * (c.x - playerChunk.x) +
                (c.y - playerChunk.y) * (c.y - playerChunk.y))
            .ToList();

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
        var lightBuf = chunkComp.lightBuffer;
        int size = ChunkSize * ChunkSize;

        // 버퍼 초기화
        for (int i = 0; i < size; i++)
        {
            bgBuf[i] = null;
            fgBuf[i] = null;
            lightBuf[i] = null;
        }

        var bounds = new BoundsInt(0, 0, 0, ChunkSize, ChunkSize, 1);
        for (int y = 0; y < ChunkSize; y++)
        {
            for (int x = 0; x < ChunkSize; x++)
            {
                int wx = coord.x * ChunkSize + x;
                int wy = coord.y * ChunkSize + y;
                int idx = y * ChunkSize + x;
                if (wx < 0 || wx >= settings.width || wy < 0 || wy >= settings.height)
                    continue;

                // BG
                bgBuf[idx] = TileCache.Get(worldMap.bg[wx, wy]);

                // FG
                var cell = worldMap.fg[wx, wy];
                var tile = TileCache.Get(cell.id);
                tile.colliderType = cell.hasCollider
                    ? Tile.ColliderType.Sprite
                    : Tile.ColliderType.None;
                fgBuf[idx] = tile;

                // Light
                byte lvl = worldMap.light[wx, wy];     // 0~20
                float alpha = 1f - (lvl / 20f);         // lvl20→0, lvl0→1
                var lt = ScriptableObject.CreateInstance<Tile>();
                lt.sprite = lightSprite;
                lt.color = new Color(0, 0, 0, alpha);
                lightBuf[idx] = lt;
            }
        }

        // Tilemap 적용
        chunkComp.bgTilemap.SetTilesBlock(bounds, bgBuf);
        chunkComp.fgTilemap.SetTilesBlock(bounds, fgBuf);
        chunkComp.lightTilemap.SetTilesBlock(bounds, lightBuf);

        // 콜라이더 리프레시
        chunkComp.fgTilemap.RefreshAllTiles();
        chunkComp.fgTilemap.GetComponent<TilemapCollider2D>()
                          .ProcessTilemapChanges();

        // Dirty 플래그 초기화
        chunkComp.bgDirty = false;
        chunkComp.fgDirty = false;
        chunkComp.lightDirty = false;

        activeChunks[coord] = go;
    }

    /// <summary>
    /// Dirty 플래그가 설정된 청크 레이어들을 갱신합니다.
    /// FG 더티 시에는 빛을 국소 BFS로 재계산한 뒤 lightDirty도 설정합니다.
    /// </summary>
    private void ProcessDirtyChunks()
    {
        foreach (var kv in activeChunks)
        {
            var coord = kv.Key;
            var go = kv.Value;
            var chunkComp = go.GetComponent<Chunk>();

            // BG 업데이트
            if (chunkComp.bgDirty)
            {
                RefreshChunkLayer(coord, false);
                chunkComp.bgDirty = false;
            }

            // FG 업데이트 → 그 후 빛 재계산
            if (chunkComp.fgDirty)
            {
                RefreshChunkLayer(coord, true);
                chunkComp.fgDirty = false;

                // 이 청크 영역 내 모든 셀에 대해 국소 빛 재계산
                int startX = coord.x * ChunkSize, startY = coord.y * ChunkSize;
                for (int y = 0; y < ChunkSize; y++)
                    for (int x = 0; x < ChunkSize; x++)
                        RecalculateLightAt(startX + x, startY + y);
            }

            // Light 업데이트
            if (chunkComp.lightDirty)
            {
                RefreshLightLayer(coord);
                chunkComp.lightDirty = false;
            }
        }
    }

    /// <summary>
    /// (x0,y0)에서 시작해 국소 BFS로 light 배열 재계산, 영향 청크에 lightDirty 설정
    /// </summary>
    public void RecalculateLightAt(int x0, int y0)
    {
        int w = settings.width, h = settings.height;
        var q = new Queue<(int x, int y)>();
        q.Enqueue((x0, y0));

        (int dx, int dy)[] dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            byte old = worldMap.light[x, y];

            // 주변 4방향 중 가장 밝은 이웃 기준
            byte best = 0;
            foreach (var (dx, dy) in dirs)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;

                int attenuation = (worldMap.bg[x, y] != 0 ? 1 : 0)
                                + (worldMap.fg[x, y].hasCollider ? 2 : 0);
                int cand = worldMap.light[nx, ny] - attenuation;
                if (cand > best) best = (byte)cand;
            }

            if (best < 0) best = 0;

            if (best != old)
            {
                worldMap.light[x, y] = best;

                // 변화 퍼뜨리기 & dirty 표시
                foreach (var (dx, dy) in dirs)
                {
                    int mx = x + dx, my = y + dy;
                    if (mx < 0 || my < 0 || mx >= w || my >= h) continue;
                    q.Enqueue((mx, my));
                }

                var coord = new Vector2Int(x / ChunkSize, y / ChunkSize);
                if (activeChunks.TryGetValue(coord, out var go2))
                    go2.GetComponent<Chunk>().lightDirty = true;
            }
        }
    }

    /// <summary>
    /// 지정한 좌표의 청크 레이어 하나만 다시 그립니다.
    /// </summary>
    private void RefreshChunkLayer(Vector2Int coord, bool isFG)
    {
        var go = activeChunks[coord];
        var chunkComp = go.GetComponent<Chunk>();
        var buf = isFG ? chunkComp.fgBuffer : chunkComp.bgBuffer;
        var tilemap = isFG ? chunkComp.fgTilemap : chunkComp.bgTilemap;
        var bounds = new BoundsInt(0, 0, 0, ChunkSize, ChunkSize, 1);

        for (int y = 0; y < ChunkSize; y++)
            for (int x = 0; x < ChunkSize; x++)
            {
                int wx = coord.x * ChunkSize + x;
                int wy = coord.y * ChunkSize + y;
                int idx = y * ChunkSize + x;
                if (wx < 0 || wy < 0 || wx >= settings.width || wy >= settings.height)
                    continue;

                if (isFG)
                {
                    var cell = worldMap.fg[wx, wy];
                    var tile = TileCache.Get(cell.id);
                    tile.colliderType = cell.hasCollider
                        ? Tile.ColliderType.Sprite
                        : Tile.ColliderType.None;
                    buf[idx] = tile;
                }
                else
                {
                    buf[idx] = TileCache.Get(worldMap.bg[wx, wy]);
                }
            }

        tilemap.SetTilesBlock(bounds, buf);
        if (isFG)
        {
            chunkComp.fgTilemap.RefreshAllTiles();
            chunkComp.fgTilemap.GetComponent<TilemapCollider2D>()
                              .ProcessTilemapChanges();
        }
    }

    /// <summary>
    /// 지정한 좌표의 라이트 레이어만 다시 그립니다.
    /// </summary>
    private void RefreshLightLayer(Vector2Int coord)
    {
        var go = activeChunks[coord];
        var chunkComp = go.GetComponent<Chunk>();
        var buf = chunkComp.lightBuffer;
        var tilemap = chunkComp.lightTilemap;
        var bounds = new BoundsInt(0, 0, 0, ChunkSize, ChunkSize, 1);

        int startX = coord.x * ChunkSize, startY = coord.y * ChunkSize;
        for (int y = 0; y < ChunkSize; y++)
            for (int x = 0; x < ChunkSize; x++)
            {
                int wx = startX + x, wy = startY + y, idx = y * ChunkSize + x;
                byte lvl = worldMap.light[wx, wy];
                float alpha = 1f - (lvl / 20f);
                var lt = ScriptableObject.CreateInstance<Tile>();
                lt.sprite = lightSprite;
                lt.color = new Color(0, 0, 0, alpha);
                buf[idx] = lt;
            }

        tilemap.SetTilesBlock(bounds, buf);
    }

    /// <summary>
    /// 지정한 월드 좌표의 청크 레이어에 Dirty 플래그를 설정합니다.
    /// </summary>
    public void MarkChunkDirty(int worldX, int worldY, bool markFG)
    {
        int cx = Mathf.FloorToInt(worldX / (float)ChunkSize);
        int cy = Mathf.FloorToInt(worldY / (float)ChunkSize);
        var coord = new Vector2Int(cx, cy);
        if (!activeChunks.TryGetValue(coord, out var go)) return;
        var chunkComp = go.GetComponent<Chunk>();
        if (markFG) chunkComp.fgDirty = true;
        else chunkComp.bgDirty = true;
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
            newTile.name = BlockLibrary.GetName(id);
            cache[id] = newTile;
            return newTile;
        }
    }
}
