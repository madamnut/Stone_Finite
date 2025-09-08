using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// WorldManager: 청크 풀링, 버퍼 재사용, 타일 캐싱
/// • 청크 크기: 16×16
/// • 플레이어 반경 ChunkRadius 청크만 활성화
/// • 매 프레임 최대 maxLoadsPerFrame개의 청크 로드
/// • Dirty 플래그 기반 레이어별 갱신 지원
/// • Light 레이어 지원, FG 변경 시 국소 재계산
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
    private const byte NAT_MAX = 20;

    // 전체 월드 데이터
    public WorldData worldMap;

    // 풀링 / 로딩 큐 / 임시 리스트
    private readonly Queue<GameObject> chunkPool = new Queue<GameObject>();
    private List<Vector2Int> loadList = new List<Vector2Int>();
    private readonly List<Vector2Int> unloadList = new List<Vector2Int>();

    // 현재 필요 청크 집합
    private readonly HashSet<Vector2Int> currentNeeded = new HashSet<Vector2Int>();

    private bool isLoading = false;
    private Vector2Int lastPlayerChunk;

    // 활성화된 청크
    private readonly Dictionary<Vector2Int, GameObject> activeChunks = new Dictionary<Vector2Int, GameObject>();

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

        // 로딩 후보(거리순)
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

        var c = go.GetComponent<Chunk>();
        if (c == null) return;

        var bgBuf    = c.bgBuffer;
        var fgBuf    = c.fgBuffer;
        var decoBuf  = c.decoBuffer;
        var liquidBuf= c.liquidBuffer;
        var lightBuf = c.lightBuffer;
        int size = ChunkSize * ChunkSize;

        // 버퍼 초기화
        for (int i = 0; i < size; i++)
        {
            bgBuf[i]     = null;
            fgBuf[i]     = null;
            decoBuf[i]   = null;
            liquidBuf[i] = null;
            lightBuf[i]  = null;
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
                ushort fgId = worldMap.fg[wx, wy].id;
                if (fgId != 0)
                {
                    var tile = TileCache.Get(fgId);
                    tile.colliderType = Tile.ColliderType.Sprite; // 솔리드는 항상 콜라이더
                    fgBuf[idx] = tile;
                }

                // Deco
                ushort decoId = worldMap.deco[wx, wy].id;
                if (decoId != 0)
                {
                    var tile = TileCache.Get(decoId);
                    tile.colliderType = Tile.ColliderType.None;
                    decoBuf[idx] = tile;
                }

                // Liquid
                var liq = worldMap.liquid[wx, wy];
                if (liq.amount > 0 && liq.id != 0)
                {
                    var tile = TileCache.Get(liq.id);
                    tile.colliderType = Tile.ColliderType.None;
                    liquidBuf[idx] = tile;
                }

                // Light
                byte lvl = worldMap.light[wx, wy].natural; // 0..20
                float alpha = 1f - Mathf.Clamp01(lvl / (float)NAT_MAX);
                var lt = ScriptableObject.CreateInstance<Tile>();
                lt.sprite = lightSprite;
                lt.color = new Color(0, 0, 0, alpha);
                lightBuf[idx] = lt;
            }
        }

        // Tilemap 적용
        c.bgTilemap.SetTilesBlock(bounds, bgBuf);
        c.fgTilemap.SetTilesBlock(bounds, fgBuf);
        c.decoTilemap.SetTilesBlock(bounds, decoBuf);
        c.liquidTilemap.SetTilesBlock(bounds, liquidBuf);
        c.lightTilemap.SetTilesBlock(bounds, lightBuf);

        // FG 콜라이더 리프레시
        var coll = c.fgTilemap.GetComponent<TilemapCollider2D>();
        if (coll != null)
        {
            c.fgTilemap.RefreshAllTiles();
            coll.ProcessTilemapChanges();
        }

        // Dirty 초기화
        c.bgDirty = c.fgDirty = c.decoDirty = c.liquidDirty = c.lightDirty = false;

        activeChunks[coord] = go;
    }

    /// <summary>
    /// Dirty 플래그가 설정된 청크 레이어들을 갱신. FG 갱신 후 국소 라이트 재계산.
    /// </summary>
    private void ProcessDirtyChunks()
    {
        foreach (var kv in activeChunks)
        {
            var coord = kv.Key;
            var go    = kv.Value;
            var c     = go.GetComponent<Chunk>();

            if (c.bgDirty)
            {
                RefreshChunkLayer(coord, LayerType.BG);
                c.bgDirty = false;
                // 필요 시 BG 변경에도 라이트 재계산을 호출할 수 있음
            }

            if (c.fgDirty)
            {
                RefreshChunkLayer(coord, LayerType.FG);
                c.fgDirty = false;

                // 이 청크 영역 내 국소 라이트 재계산
                int sx = coord.x * ChunkSize, sy = coord.y * ChunkSize;
                for (int y = 0; y < ChunkSize; y++)
                    for (int x = 0; x < ChunkSize; x++)
                        RecalculateLightAt(sx + x, sy + y);
            }

            if (c.decoDirty)
            {
                RefreshChunkLayer(coord, LayerType.Deco);
                c.decoDirty = false;
            }

            if (c.liquidDirty)
            {
                RefreshChunkLayer(coord, LayerType.Liquid);
                c.liquidDirty = false;
            }

            if (c.lightDirty)
            {
                RefreshLightLayer(coord);
                c.lightDirty = false;
            }
        }
    }

    private enum LayerType { BG, FG, Deco, Liquid }

    /// <summary>지정한 좌표의 청크 레이어 하나만 다시 그립니다.</summary>
    private void RefreshChunkLayer(Vector2Int coord, LayerType type)
    {
        var go = activeChunks[coord];
        var c  = go.GetComponent<Chunk>();
        var bounds = new BoundsInt(0, 0, 0, ChunkSize, ChunkSize, 1);

        switch (type)
        {
            case LayerType.BG:
            {
                var buf = c.bgBuffer;
                for (int y = 0; y < ChunkSize; y++)
                    for (int x = 0; x < ChunkSize; x++)
                    {
                        int wx = coord.x * ChunkSize + x;
                        int wy = coord.y * ChunkSize + y;
                        int idx = y * ChunkSize + x;
                        if ((uint)wx >= settings.width || (uint)wy >= settings.height) continue;
                        buf[idx] = TileCache.Get(worldMap.bg[wx, wy]);
                    }
                c.bgTilemap.SetTilesBlock(bounds, buf);
                break;
            }
            case LayerType.FG:
            {
                var buf = c.fgBuffer;
                for (int y = 0; y < ChunkSize; y++)
                    for (int x = 0; x < ChunkSize; x++)
                    {
                        int wx = coord.x * ChunkSize + x;
                        int wy = coord.y * ChunkSize + y;
                        int idx = y * ChunkSize + x;
                        if ((uint)wx >= settings.width || (uint)wy >= settings.height) continue;

                        ushort id = worldMap.fg[wx, wy].id;
                        if (id != 0)
                        {
                            var tile = TileCache.Get(id);
                            tile.colliderType = Tile.ColliderType.Sprite;
                            buf[idx] = tile;
                        }
                        else buf[idx] = null;
                    }
                c.fgTilemap.SetTilesBlock(bounds, buf);
                var coll = c.fgTilemap.GetComponent<TilemapCollider2D>();
                if (coll != null)
                {
                    c.fgTilemap.RefreshAllTiles();
                    coll.ProcessTilemapChanges();
                }
                break;
            }
            case LayerType.Deco:
            {
                var buf = c.decoBuffer;
                for (int y = 0; y < ChunkSize; y++)
                    for (int x = 0; x < ChunkSize; x++)
                    {
                        int wx = coord.x * ChunkSize + x;
                        int wy = coord.y * ChunkSize + y;
                        int idx = y * ChunkSize + x;
                        if ((uint)wx >= settings.width || (uint)wy >= settings.height) continue;

                        ushort id = worldMap.deco[wx, wy].id;
                        buf[idx] = id != 0 ? TileCache.Get(id) : null;
                    }
                c.decoTilemap.SetTilesBlock(bounds, buf);
                break;
            }
            case LayerType.Liquid:
            {
                var buf = c.liquidBuffer;
                for (int y = 0; y < ChunkSize; y++)
                    for (int x = 0; x < ChunkSize; x++)
                    {
                        int wx = coord.x * ChunkSize + x;
                        int wy = coord.y * ChunkSize + y;
                        int idx = y * ChunkSize + x;
                        if ((uint)wx >= settings.width || (uint)wy >= settings.height) continue;

                        var liq = worldMap.liquid[wx, wy];
                        buf[idx] = (liq.amount > 0 && liq.id != 0) ? TileCache.Get(liq.id) : null;
                    }
                c.liquidTilemap.SetTilesBlock(bounds, buf);
                break;
            }
        }
    }

    /// <summary>지정한 좌표의 라이트 레이어만 다시 그립니다.</summary>
    private void RefreshLightLayer(Vector2Int coord)
    {
        var go = activeChunks[coord];
        var c  = go.GetComponent<Chunk>();
        var buf = c.lightBuffer;
        var bounds = new BoundsInt(0, 0, 0, ChunkSize, ChunkSize, 1);

        int sx = coord.x * ChunkSize, sy = coord.y * ChunkSize;
        for (int y = 0; y < ChunkSize; y++)
            for (int x = 0; x < ChunkSize; x++)
            {
                int wx = sx + x, wy = sy + y, idx = y * ChunkSize + x;
                byte lvl = worldMap.light[wx, wy].natural;
                float alpha = 1f - Mathf.Clamp01(lvl / (float)NAT_MAX);
                var lt = ScriptableObject.CreateInstance<Tile>();
                lt.sprite = lightSprite;
                lt.color = new Color(0, 0, 0, alpha);
                buf[idx] = lt;
            }

        c.lightTilemap.SetTilesBlock(bounds, buf);
    }

    /// <summary>
    /// (x0,y0)에서 시작해 국소 BFS로 자연광 재계산. 영향 청크에 lightDirty 설정.
    /// </summary>
    public void RecalculateLightAt(int x0, int y0)
    {
        int w = settings.width, h = settings.height;
        if ((uint)x0 >= w || (uint)y0 >= h) return;

        var q = new Queue<(int x, int y)>();
        q.Enqueue((x0, y0));

        (int dx, int dy)[] dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            byte old = worldMap.light[x, y].natural;

            // 현재 셀을 통과할 때의 감쇄(들어오는 빛에 적용)
            int attenHere = 0;
            if (worldMap.bg[x, y] != 0) attenHere += 1;
            if (worldMap.fg[x, y].id != 0) attenHere += 2;

            byte best = 0;
            foreach (var (dx, dy) in dirs)
            {
                int nx = x + dx, ny = y + dy;
                if ((uint)nx >= w || (uint)ny >= h) continue;

                int cand = worldMap.light[nx, ny].natural - attenHere;
                if (cand > best) best = (byte)Mathf.Clamp(cand, 0, NAT_MAX);
            }

            if (best != old)
            {
                worldMap.light[x, y] = new LightCell { natural = best, artificial = worldMap.light[x, y].artificial };

                foreach (var (dx, dy) in dirs)
                {
                    int mx = x + dx, my = y + dy;
                    if ((uint)mx >= w || (uint)my >= h) continue;
                    q.Enqueue((mx, my));
                }

                var coord = new Vector2Int(x / ChunkSize, y / ChunkSize);
                if (activeChunks.TryGetValue(coord, out var go))
                    go.GetComponent<Chunk>().lightDirty = true;
            }
        }
    }

    /// <summary>월드 좌표의 청크 레이어 Dirty 설정.</summary>
    public void MarkChunkDirty(int worldX, int worldY, bool markFG, bool markBG = false, bool markDeco = false, bool markLiquid = false)
    {
        int cx = Mathf.FloorToInt(worldX / (float)ChunkSize);
        int cy = Mathf.FloorToInt(worldY / (float)ChunkSize);
        var coord = new Vector2Int(cx, cy);
        if (!activeChunks.TryGetValue(coord, out var go)) return;
        var c = go.GetComponent<Chunk>();
        if (markFG)     c.fgDirty = true;
        if (markBG)     c.bgDirty = true;
        if (markDeco)   c.decoDirty = true;
        if (markLiquid) c.liquidDirty = true;
    }

    // ── 타일 캐시: id → Tile ──
    private static class TileCache
    {
        private static readonly Dictionary<ushort, Tile> cache = new Dictionary<ushort, Tile>();
        public static Tile Get(ushort id)
        {
            if (id == 0) return null;
            if (cache.TryGetValue(id, out var tile)) return tile;
            var t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = CellLibrary.GetSprite(id);
            t.name = CellLibrary.GetName(id);
            cache[id] = t;
            return t;
        }
    }
}
