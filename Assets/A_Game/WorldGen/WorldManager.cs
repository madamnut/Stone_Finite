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

    // ─────────────────────────────────────────────────────
    // Water Sim (전역 20Hz)
    // ─────────────────────────────────────────────────────
    [Header("Water Sim")]
    public int   opsBudgetWater    = 3000;   // 틱당 최대 처리 셀
    public float waterTickInterval = 0.05f;  // 20 Hz
    private readonly Queue<Vector2Int> waterQ = new();
    private readonly HashSet<Vector2Int> inQ  = new();
    private Coroutine waterTick;

    void OnEnable()
    {
        if (waterTick == null) waterTick = StartCoroutine(WaterLoop());
    }

    void OnDisable()
    {
        if (waterTick != null) { StopCoroutine(waterTick); waterTick = null; }
    }

    /// <summary>물 셀 시뮬레이트 대상 등록.</summary>
    public void MarkWaterDirty(int x, int y)
    {
        int w = settings.width, h = settings.height;
        if ((uint)x >= w || (uint)y >= h) return;
        var v = new Vector2Int(x, y);
        if (inQ.Add(v)) waterQ.Enqueue(v);
    }

    IEnumerator WaterLoop()
    {
        var wait = new WaitForSeconds(waterTickInterval);
        while (true)
        {
            StepWater();
            yield return wait;
        }
    }

    // 낙하 → 좌우 동시 분배. 모든 연산은 정수. 최소 흐름 1.
    void StepWater()
    {
        int w = settings.width, h = settings.height;
        int ops = 0;

        int iter = Mathf.Min(opsBudgetWater, waterQ.Count);
        for (int k = 0; k < iter; k++)
        {
            var p = waterQ.Dequeue();
            inQ.Remove(p);
            int x = p.x, y = p.y;
            if ((uint)x >= w || (uint)y >= h) continue;

            ref var cell = ref worldMap.liquid[x, y];
            int Wc = cell.amount;
            if (Wc <= 0)
            {
                worldMap.liquid[x, y].id = 0;
                continue;
            }

            bool Blocked(int gx, int gy)
            {
                if ((uint)gx >= w || (uint)gy >= h) return true;
                return worldMap.fg[gx, gy].id != 0;
            }

            // 1) 아래로 낙하
            int dy = y - 1;
            if (dy >= 0 && !Blocked(x, dy))
            {
                int Wd = worldMap.liquid[x, dy].amount;
                int cap = 100 - Wd;
                if (cap > 0)
                {
                    int move = Mathf.Min(Wc, cap);
                    WriteWater(x,  y,  Wc - move);
                    WriteWater(x,  dy, Wd + move);

                    // Enq 축소: 자기/아래/좌/우/위 만
                    Enq(x, y);           // 자기
                    Enq(x, dy);          // 아래 목적지
                    if (x > 0)     Enq(x - 1, y);   // 좌(원셀 이웃)
                    if (x + 1 < w) Enq(x + 1, y);   // 우(원셀 이웃)
                    if (y + 1 < h) Enq(x, y + 1);   // 위

                    ops++;
                    continue;
                }
            }

            // 2) 좌우 동시 분배 (각 방향 최대 20, diff/2의 정수, 최소 1)
            int xl = x - 1, xr = x + 1;

            bool canL = xl >= 0 && !Blocked(xl, y);
            bool canR = xr < w  && !Blocked(xr, y);

            int Wl = canL ? worldMap.liquid[xl, y].amount : 0;
            int Wr = canR ? worldMap.liquid[xr, y].amount : 0;

            int capL = canL ? (100 - Wl) : 0;
            int capR = canR ? (100 - Wr) : 0;

            int flowL = 0, flowR = 0;

            if (canL)
            {
                int diffL = Wc - Wl;
                if (diffL > 0)
                {
                    int propL = Mathf.Clamp(Mathf.Max(1, diffL / 2), 1, 20);
                    flowL = Mathf.Min(propL, capL);
                }
            }
            if (canR)
            {
                int diffR = Wc - Wr;
                if (diffR > 0)
                {
                    int propR = Mathf.Clamp(Mathf.Max(1, diffR / 2), 1, 20);
                    flowR = Mathf.Min(propR, capR);
                }
            }

            int want = flowL + flowR;
            if (want > 0)
            {
                int total = Mathf.Min(Wc, want);
                int takeL = 0, takeR = 0;

                if (flowL > 0 && flowR > 0)
                {
                    int denom = flowL + flowR;
                    takeL = (total * flowL + denom / 2) / denom;
                    if (takeL > flowL) takeL = flowL;
                    takeR = total - takeL;
                    if (takeR > flowR) { takeR = flowR; takeL = total - takeR; }
                }
                else if (flowL > 0)
                {
                    takeL = Mathf.Min(total, flowL);
                }
                else
                {
                    takeR = Mathf.Min(total, flowR);
                }

                WriteWater(x,  y,  Wc - (takeL + takeR));
                if (takeL > 0) WriteWater(xl, y,  Wl + takeL);
                if (takeR > 0) WriteWater(xr, y,  Wr + takeR);

                // Enq 축소: 자기/흘린 좌/흘린 우/위 만
                Enq(x, y);                  // 자기
                if (x > 0)     Enq(x - 1, y);   // 좌(원셀 이웃)
                if (x + 1 < w) Enq(x + 1, y);   // 우(원셀 이웃)
                if (y + 1 < h) Enq(x, y + 1);   // 위

                ops++;
                continue;
            }

            ops++;
        }
    }

    // 전역 쓰기 + 해당 청크 Liquid 레이어만 Dirty
    void WriteWater(int x, int y, int newAmount)
    {
        if ((uint)x >= settings.width || (uint)y >= settings.height) return;

        int cur = worldMap.liquid[x, y].amount;
        newAmount = Mathf.Clamp(newAmount, 0, 100);
        if (cur == newAmount) return;

        worldMap.liquid[x, y].amount = (byte)newAmount;
        worldMap.liquid[x, y].id     = (ushort)(newAmount > 0 ? 60000 : 0);

        MarkChunkDirty(x, y, markFG: false, markBG: false, markDeco: false, markLiquid: true);
    }

    void Enq(int x, int y)
    {
        int w = settings.width, h = settings.height;
        if ((uint)x >= w || (uint)y >= h) return;
        var v = new Vector2Int(x, y);
        if (inQ.Add(v)) waterQ.Enqueue(v);
    }

    // ─────────────────────────────────────────────────────
    // 이하 기존 코드
    // ─────────────────────────────────────────────────────

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

        if ((playerChunk - lastPlayerChunk).sqrMagnitude > (ChunkRadius * ChunkRadius * 4))
            loadList.Clear();
        lastPlayerChunk = playerChunk;

        currentNeeded.Clear();
        for (int dy = -ChunkRadius; dy <= ChunkRadius; dy++)
            for (int dx = -ChunkRadius; dx <= ChunkRadius; dx++)
                currentNeeded.Add(new Vector2Int(playerChunk.x + dx, playerChunk.y + dy));

        unloadList.Clear();
        foreach (var coord in activeChunks.Keys)
            if (!currentNeeded.Contains(coord)) unloadList.Add(coord);
        foreach (var coord in unloadList)
        {
            ReturnToPool(activeChunks[coord]);
            activeChunks.Remove(coord);
        }

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
                    tile.colliderType = Tile.ColliderType.Sprite;
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

                // Liquid → 10단계 시각화(알파 상단 유지, 피벗 중앙)
                var liq = worldMap.liquid[wx, wy];
                liquidBuf[idx] = (liq.amount > 0 && liq.id != 0)
                    ? TileCache.GetWaterByAmount(liq.id, liq.amount)
                    : null;

                // Light
                byte lvl = worldMap.light[wx, wy].natural; // 0..20
                float alpha = 1f - Mathf.Clamp01(lvl / (float)NAT_MAX);
                var lt = ScriptableObject.CreateInstance<Tile>();
                lt.sprite = lightSprite;
                lt.color = new Color(0, 0, 0, alpha);
                lightBuf[idx] = lt;
            }
        }

        c.bgTilemap.SetTilesBlock(bounds, bgBuf);
        c.fgTilemap.SetTilesBlock(bounds, fgBuf);
        c.decoTilemap.SetTilesBlock(bounds, decoBuf);
        c.liquidTilemap.SetTilesBlock(bounds, liquidBuf);
        c.lightTilemap.SetTilesBlock(bounds, lightBuf);

        var coll = c.fgTilemap.GetComponent<TilemapCollider2D>();
        if (coll != null)
        {
            c.fgTilemap.RefreshAllTiles();
            coll.ProcessTilemapChanges();
        }

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
            }

            if (c.fgDirty)
            {
                RefreshChunkLayer(coord, LayerType.FG);
                c.fgDirty = false;

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
                        buf[idx] = (liq.amount > 0 && liq.id != 0)
                            ? TileCache.GetWaterByAmount(liq.id, liq.amount)  // 10단계, 알파 포함, pivot 중앙
                            : null;
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
        private static Tile[] waterLevelTiles; // 0..10, 0=null

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

        // amount(1..100) → 10단계 물 타일
        //  - 상단은 투명(알파 유지)
        //  - 하단만 채워짐
        //  - 피벗 중앙(0.5, 0.5)
        //  - 런타임 생성 캐시
        public static Tile GetWaterByAmount(ushort waterId, int amount)
        {
            if (amount <= 0) return null;
            if (amount > 100) amount = 100;

            int level = (amount - 1) / 10 + 1; // 1..10
            waterLevelTiles ??= new Tile[11];
            if (waterLevelTiles[level] != null) return waterLevelTiles[level];

            var baseTile = Get(waterId);
            var s = baseTile != null ? baseTile.sprite : null;
            if (s == null) return null;

            var tex = s.texture;
            if (tex == null) return null;

            Rect r = s.textureRect;
            int fullW = Mathf.RoundToInt(r.width);
            int fullH = Mathf.RoundToInt(r.height);
            int copyH = Mathf.CeilToInt(fullH * (level / 10f)); // 아래로부터 복사 높이

            var newTex = new Texture2D(fullW, fullH, TextureFormat.RGBA32, false);
            newTex.filterMode = tex.filterMode;
            newTex.wrapMode = TextureWrapMode.Clamp;

            var clear = new Color32(0, 0, 0, 0);
            var buf = new Color32[fullW * fullH];
            for (int i = 0; i < buf.Length; i++) buf[i] = clear;
            newTex.SetPixels32(buf);

            int srcX = Mathf.RoundToInt(r.x);
            int srcY = Mathf.RoundToInt(r.y);
            Color[] src = tex.GetPixels(srcX, srcY, fullW, copyH);
            newTex.SetPixels(0, 0, fullW, copyH, src);

            newTex.Apply(false, false);

            var pivot = new Vector2(0.5f, 0.5f);
            var spr = Sprite.Create(newTex, new Rect(0, 0, fullW, fullH), pivot, s.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            spr.name = $"Water_L{level}";

            var t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = spr;
            t.name   = $"Water_L{level}";
            t.colliderType = Tile.ColliderType.None;

            waterLevelTiles[level] = t;
            return t;
        }
    }
}
