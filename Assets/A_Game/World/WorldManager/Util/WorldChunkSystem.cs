using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 청크 풀 / 로드 / 언로드 / 타일맵 리프레시 / 라이트 메쉬까지 담당하는 시스템
/// (WorldManager에서 의존성만 주입해서 사용)
/// </summary>
public class WorldChunkSystem
{
    // ───────── 기본 참조/설정 ─────────
    private readonly int worldWidth;
    private readonly int worldHeight;
    private readonly int chunkSize;
    private readonly int chunkRadius;
    private readonly int maxLoadsPerFrame;

    private readonly WorldData  worldMap;
    private readonly GameObject chunkPrefab;
    private readonly Transform  chunkRoot;

    // FG 타일 편집 시, 해당 영역 라이트 재계산 위해 WorldManager의 메서드를 콜백으로 받음
    private readonly System.Action<int, int> recalcLightAt;

    // 라이트 메쉬 계산용 상수 (WorldManager와 동일, 0~15)
    private const byte NAT_MAX = 15;
    private const byte ART_MAX = 15;

    // 시간에 따라 바뀌는 전역 밝기 오프셋 (WorldManager에서 세팅해줄 것, 0~15)
    private byte globalBrightnessOffset = 0;

    // ───────── 풀 / 로드 큐 / 활성 청크 ─────────
    private readonly Queue<GameObject>                  chunkPool     = new();
    private readonly List<Vector2Int>                   loadList      = new();
    private          int                                loadIndex     = 0;
    private readonly List<Vector2Int>                   unloadList    = new();
    private readonly HashSet<Vector2Int>                currentNeeded = new();
    private readonly Dictionary<Vector2Int, GameObject> activeChunks  = new();

    // ───────── 더티 청크 집합 ─────────
    private readonly HashSet<Vector2Int> fgDirtyChunks    = new();
    private readonly HashSet<Vector2Int> bgDirtyChunks    = new();
    private readonly HashSet<Vector2Int> lightDirtyChunks = new();

    private bool       isLoading       = false;
    private Vector2Int lastPlayerChunk = Vector2Int.zero;

    public IReadOnlyDictionary<Vector2Int, GameObject> ActiveChunks => activeChunks;

    // ───────── 생성자 ─────────
    public WorldChunkSystem(
        int worldWidth,
        int worldHeight,
        int chunkSize,
        int chunkRadius,
        int maxLoadsPerFrame,
        WorldData worldMap,
        GameObject chunkPrefab,
        Transform chunkRoot,
        System.Action<int, int> recalcLightAt
    )
    {
        this.worldWidth       = worldWidth;
        this.worldHeight      = worldHeight;
        this.chunkSize        = chunkSize;
        this.chunkRadius      = chunkRadius;
        this.maxLoadsPerFrame = maxLoadsPerFrame;

        this.worldMap    = worldMap;
        this.chunkPrefab = chunkPrefab;
        this.chunkRoot   = chunkRoot;

        this.recalcLightAt = recalcLightAt;
    }

    // ───────── 외부에서 세팅/호출할 API ─────────

    /// <summary>초기 청크 풀 생성 (WorldManager.Awake 에서 호출)</summary>
    public void InitializePool(int initialPoolSize)
    {
        if (chunkRoot == null || chunkPrefab == null) return;

        for (int i = 0; i < initialPoolSize; i++)
        {
            var go = Object.Instantiate(chunkPrefab, chunkRoot);
            go.SetActive(false);
            chunkPool.Enqueue(go);
        }
    }

    /// <summary>Awake 후, 현재 플레이어 위치 기준으로 lastPlayerChunk 초기화</summary>
    public void ResetLastPlayerChunk(Vector3 playerPosition)
    {
        lastPlayerChunk = GetPlayerChunk(playerPosition);
    }

    /// <summary>시간에 따른 전역 밝기 오프셋(WorldManager에서 계산) 값을 반영</summary>
    public void SetGlobalBrightnessOffset(byte offset)
    {
        globalBrightnessOffset = offset;
    }

    /// <summary>
    /// 플레이어 위치 기준으로 청크 로드/언로드 큐를 구성하고,
    /// 필요 시 코루틴으로 실제 로드를 진행한다.
    /// </summary>
    public void UpdateVisibleChunks(Vector3 playerPosition, MonoBehaviour coroutineHost)
    {
        Vector2Int playerChunk = GetPlayerChunk(playerPosition);

        // 같은 청크에 머물러 있고, 로드 큐도 비었고, 이미 청크가 떠 있으면 계산 스킵
        if (playerChunk == lastPlayerChunk &&
            loadList.Count == 0 &&
            activeChunks.Count > 0)
            return;

        // 순간이동 수준으로 멀리 이동했으면 로드 큐 초기화
        if ((playerChunk - lastPlayerChunk).sqrMagnitude > (chunkRadius * chunkRadius * 4))
        {
            loadList.Clear();
            loadIndex = 0;
        }
        lastPlayerChunk = playerChunk;

        // 유효 청크 범위 계산
        int cxMin = 0;
        int cyMin = 0;
        int cxMax = Mathf.Max(0, (worldWidth  - 1) / chunkSize);
        int cyMax = Mathf.Max(0, (worldHeight - 1) / chunkSize);

        // 필요한 청크 집합 구성
        currentNeeded.Clear();
        for (int dy = -chunkRadius; dy <= chunkRadius; dy++)
        for (int dx = -chunkRadius; dx <= chunkRadius; dx++)
        {
            int cx = playerChunk.x + dx;
            int cy = playerChunk.y + dy;
            if (cx < cxMin || cy < cyMin || cx > cxMax || cy > cyMax) continue; // 범위 밖 제외
            currentNeeded.Add(new Vector2Int(cx, cy));
        }

        // 언로드 대상 계산
        unloadList.Clear();
        foreach (var coord in activeChunks.Keys)
            if (!currentNeeded.Contains(coord)) unloadList.Add(coord);

        foreach (var coord in unloadList)
        {
            ReturnToPool(activeChunks[coord]);
            activeChunks.Remove(coord);
        }

        // 로드 대상 리스트 재구성 (아직 없는 청크만)
        loadList.Clear();
        foreach (var c in currentNeeded)
        {
            if (!activeChunks.ContainsKey(c))
                loadList.Add(c);
        }

        // 플레이어와의 거리 기준 정렬 (가장 가까운 청크부터)
        loadList.Sort((a, b) =>
        {
            int ax = a.x - playerChunk.x;
            int ay = a.y - playerChunk.y;
            int bx = b.x - playerChunk.x;
            int by = b.y - playerChunk.y;

            int da2 = ax * ax + ay * ay;
            int db2 = bx * bx + by * by;
            return da2.CompareTo(db2);
        });

        loadIndex = 0;

        if (!isLoading && loadList.Count > 0 && coroutineHost != null)
            coroutineHost.StartCoroutine(ProcessLoadQueue());
    }

    /// <summary>
    /// 더티 플래그가 켜진 청크의 타일/라이트를 실제로 다시 세팅.
    /// (WorldManager의 FixedUpdate 등에서 호출)
    /// </summary>
    public void ProcessDirtyChunks()
    {
        if (fgDirtyChunks.Count == 0 &&
            bgDirtyChunks.Count == 0 &&
            lightDirtyChunks.Count == 0)
            return;

        // BG
        if (bgDirtyChunks.Count > 0)
        {
            foreach (var coord in bgDirtyChunks)
            {
                if (!activeChunks.TryGetValue(coord, out var go)) continue;
                var c = go.GetComponent<Chunk>();
                if (c == null || !c.bgDirty) continue;

                RefreshChunkLayer(coord, LayerType.BG);
                c.bgDirty = false;
            }
            bgDirtyChunks.Clear();
        }

        // FG
        if (fgDirtyChunks.Count > 0)
        {
            foreach (var coord in fgDirtyChunks)
            {
                if (!activeChunks.TryGetValue(coord, out var go)) continue;
                var c = go.GetComponent<Chunk>();
                if (c == null || !c.fgDirty) continue;

                RefreshChunkLayer(coord, LayerType.FG);
                c.fgDirty = false;

                // FG 변경 시 청크 전체 라이트 재계산은 제거
                // (개별 셀 변경 시 WorldManager 쪽에서 RecalculateLightAt 호출)
            }
            fgDirtyChunks.Clear();
        }

        // Light
        if (lightDirtyChunks.Count > 0)
        {
            foreach (var coord in lightDirtyChunks)
            {
                if (!activeChunks.TryGetValue(coord, out var go)) continue;
                var c = go.GetComponent<Chunk>();
                if (c == null || !c.lightDirty) continue;

                RefreshLightLayer(coord);
                c.lightDirty = false;
            }
            lightDirtyChunks.Clear();
        }
    }

    /// <summary>
    /// 월드 좌표 기준으로 해당하는 청크를 dirty 표시.
    /// (FG/BG 플래그를 선택적으로 켬. deco/liquid 인자는 무시)
    /// </summary>
    public void MarkChunkDirty(
        int worldX,
        int worldY,
        bool markFG,
        bool markBG = false,
        bool markDeco = false,
        bool markLiquid = false)
    {
        int cx = Mathf.FloorToInt(worldX / (float)chunkSize);
        int cy = Mathf.FloorToInt(worldY / (float)chunkSize);
        var coord = new Vector2Int(cx, cy);
        if (!activeChunks.TryGetValue(coord, out var go)) return;
        var c = go.GetComponent<Chunk>();
        if (c == null) return;

        if (markFG)
        {
            c.fgDirty = true;
            fgDirtyChunks.Add(coord);
        }
        if (markBG)
        {
            c.bgDirty = true;
            bgDirtyChunks.Add(coord);
        }
    }

    /// <summary>
    /// [x,y]~[x+w-1,y+h-1] 영역이 걸치는 청크들의 lightDirty를 켜준다.
    /// (WorldManager.HandleArtificialChange 에서 사용 예정)
    /// </summary>
    public void MarkLightDirtyRect(int x, int y, int w, int h)
    {
        int x0 = Mathf.Clamp(x,         0, worldWidth  - 1);
        int y0 = Mathf.Clamp(y,         0, worldHeight - 1);
        int x1 = Mathf.Clamp(x + w - 1, 0, worldWidth  - 1);
        int y1 = Mathf.Clamp(y + h - 1, 0, worldHeight - 1);

        int cx0 = x0 / chunkSize, cy0 = y0 / chunkSize;
        int cx1 = x1 / chunkSize, cy1 = y1 / chunkSize;

        for (int cy = cy0; cy <= cy1; cy++)
        for (int cx = cx0; cx <= cx1; cx++)
        {
            var coord = new Vector2Int(cx, cy);
            if (activeChunks.TryGetValue(coord, out var go))
            {
                var c = go.GetComponent<Chunk>();
                if (c != null)
                {
                    c.lightDirty = true;
                    lightDirtyChunks.Add(coord);
                }
            }
        }
    }

    /// <summary>
    /// 모든 활성 청크의 lightDirty 를 켜줌.
    /// (시간대 변경에 따른 글로벌 밝기 재적용 시 사용)
    /// </summary>
    public void MarkAllChunksLightDirty()
    {
        foreach (var kv in activeChunks)
        {
            var coord = kv.Key;
            var c = kv.Value.GetComponent<Chunk>();
            if (c != null)
            {
                c.lightDirty = true;
                lightDirtyChunks.Add(coord);
            }
        }
    }

    // ───────── 내부 구현 ─────────

    private IEnumerator ProcessLoadQueue()
    {
        isLoading = true;

        // 유효 청크 범위 계산
        int cxMin = 0, cyMin = 0;
        int cxMax = Mathf.Max(0, (worldWidth  - 1) / chunkSize);
        int cyMax = Mathf.Max(0, (worldHeight - 1) / chunkSize);

        int loads = 0;
        while (loads < maxLoadsPerFrame && loadIndex < loadList.Count)
        {
            var coord = loadList[loadIndex++];
            if (!currentNeeded.Contains(coord)) continue;
            if (coord.x < cxMin || coord.y < cyMin || coord.x > cxMax || coord.y > cyMax) continue; // 범위 밖 스킵

            CreateChunk(coord);
            loads++;
        }

        if (loadIndex >= loadList.Count)
        {
            loadList.Clear();
            loadIndex = 0;
        }

        yield return null;
        isLoading = false;
    }

    private Vector2Int GetPlayerChunk(Vector3 p)
    {
        return new Vector2Int(
            Mathf.FloorToInt(p.x / chunkSize),
            Mathf.FloorToInt(p.y / chunkSize)
        );
    }

    private GameObject GetFromPool()
    {
        if (chunkPool.Count > 0) return chunkPool.Dequeue();
        var go = Object.Instantiate(chunkPrefab, chunkRoot);
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
        // 유효 청크 범위 하드 가드
        int cxMax = Mathf.Max(0, (worldWidth  - 1) / chunkSize);
        int cyMax = Mathf.Max(0, (worldHeight - 1) / chunkSize);
        if (coord.x < 0 || coord.y < 0 || coord.x > cxMax || coord.y > cyMax) return;

        var go = GetFromPool();
        go.SetActive(true);
        go.name = $"Chunk_{coord.x}_{coord.y}";
        go.transform.localPosition = new Vector3(coord.x * chunkSize, coord.y * chunkSize, 0f);

        var c = go.GetComponent<Chunk>();
        if (c == null) return;

        var bgBuf = c.bgBuffer;
        var fgBuf = c.fgBuffer;
        int size  = chunkSize * chunkSize;

        for (int i = 0; i < size; i++)
        {
            bgBuf[i] = null;
            fgBuf[i] = null;
        }

        var bounds = new BoundsInt(0, 0, 0, chunkSize, chunkSize, 1);

        for (int y = 0; y < chunkSize; y++)
        for (int x = 0; x < chunkSize; x++)
        {
            int wx = coord.x * chunkSize + x;
            int wy = coord.y * chunkSize + y;
            int idx = y * chunkSize + x;
            if ((uint)wx >= (uint)worldWidth || (uint)wy >= (uint)worldHeight)
                continue;

            // BG
            bgBuf[idx] = TileCache.Get(worldMap.bg[wx, wy]);

            // FG: 본체/유체를 단일 타일맵에서 처리
            var cell = worldMap.fg[wx, wy];

            Tile fgTile = null;

            if (cell.id != 0)
            {
                fgTile = TileCache.Get(cell.id);
                if (fgTile != null)
                {
                    bool collidable = (cell.flags & FgFlags.Collidable) != 0;
                    fgTile.colliderType = collidable ? Tile.ColliderType.Sprite : Tile.ColliderType.None;
                }
            }
            else if (cell.fluidAmount > 0 && cell.fluidId != 0)
            {
                // 본체가 없고 유체만 있는 경우 → 물 타일
                fgTile = TileCache.GetWaterByAmount(cell.fluidId, cell.fluidAmount);
                if (fgTile != null)
                    fgTile.colliderType = Tile.ColliderType.None;
            }

            fgBuf[idx] = fgTile;
        }

        c.bgTilemap.SetTilesBlock(bounds, bgBuf);
        c.fgTilemap.SetTilesBlock(bounds, fgBuf);

        var coll = c.fgTilemap.GetComponent<TilemapCollider2D>();
        if (coll != null)
        {
            c.fgTilemap.RefreshAllTiles();
            coll.ProcessTilemapChanges();
        }

        activeChunks[coord] = go;

        // 라이트 메쉬 초기화
        RefreshLightLayer(coord);

        c.bgDirty = c.fgDirty = c.lightDirty = false;
    }

    private enum LayerType { BG, FG }

    private void RefreshChunkLayer(Vector2Int coord, LayerType type)
    {
        var go = activeChunks[coord];
        var c  = go.GetComponent<Chunk>();
        var bounds = new BoundsInt(0, 0, 0, chunkSize, chunkSize, 1);

        switch (type)
        {
            case LayerType.BG:
            {
                var buf = c.bgBuffer;
                for (int y = 0; y < chunkSize; y++)
                for (int x = 0; x < chunkSize; x++)
                {
                    int wx = coord.x * chunkSize + x;
                    int wy = coord.y * chunkSize + y;
                    int idx = y * chunkSize + x;
                    if ((uint)wx >= (uint)worldWidth || (uint)wy >= (uint)worldHeight) continue;
                    buf[idx] = TileCache.Get(worldMap.bg[wx, wy]);
                }
                c.bgTilemap.SetTilesBlock(bounds, buf);
                break;
            }
            case LayerType.FG:
            {
                var buf = c.fgBuffer;
                for (int y = 0; y < chunkSize; y++)
                for (int x = 0; x < chunkSize; x++)
                {
                    int wx = coord.x * chunkSize + x;
                    int wy = coord.y * chunkSize + y;
                    int idx = y * chunkSize + x;
                    if ((uint)wx >= (uint)worldWidth || (uint)wy >= (uint)worldHeight) continue;

                    var cell = worldMap.fg[wx, wy];
                    Tile tile = null;

                    if (cell.id != 0)
                    {
                        tile = TileCache.Get(cell.id);
                        if (tile != null)
                        {
                            bool collidable = (cell.flags & FgFlags.Collidable) != 0;
                            tile.colliderType = collidable ? Tile.ColliderType.Sprite : Tile.ColliderType.None;
                        }
                    }
                    else if (cell.fluidAmount > 0 && cell.fluidId != 0)
                    {
                        tile = TileCache.GetWaterByAmount(cell.fluidId, cell.fluidAmount);
                        if (tile != null)
                            tile.colliderType = Tile.ColliderType.None;
                    }

                    buf[idx] = tile;
                }

                c.fgTilemap.SetTilesBlock(bounds, buf);
                var coll2 = c.fgTilemap.GetComponent<TilemapCollider2D>();
                if (coll2 != null)
                {
                    c.fgTilemap.RefreshAllTiles();
                    coll2.ProcessTilemapChanges();
                }
                break;
            }
        }
    }

    private void RefreshLightLayer(Vector2Int coord)
    {
        if (!activeChunks.TryGetValue(coord, out var go)) return;
        var c = go.GetComponent<Chunk>();
        if (c == null || c.lightMeshFilter == null) return;

        var mesh = c.lightMeshFilter.sharedMesh;
        if (mesh == null) return;

        int vW = chunkSize + 1, vH = chunkSize + 1, vCount = vW * vH;
        var cols = (c.lightColors != null && c.lightColors.Length == vCount)
            ? c.lightColors : new Color32[vCount];

        int sx = coord.x * chunkSize;
        int sy = coord.y * chunkSize;

        for (int vy = 0; vy <= chunkSize; vy++)
        {
            for (int vx = 0; vx <= chunkSize; vx++)
            {
                int gx = sx + vx, gy = sy + vy;

                int cx0 = Mathf.Clamp(gx - 1, 0, worldWidth  - 1);
                int cy0 = Mathf.Clamp(gy - 1, 0, worldHeight - 1);
                int cx1 = Mathf.Clamp(gx    , 0, worldWidth  - 1);
                int cy1 = Mathf.Clamp(gy    , 0, worldHeight - 1);

                float sum = 0f;

                void Sample(int x, int y)
                {
                    var L = worldMap.light[x, y];

                    // 자연광: 0~15, 전역 오프셋 0~15
                    int ns = L.natural - globalBrightnessOffset;
                    if (ns < 0) ns = 0;

                    float n01 = ns / (float)NAT_MAX;
                    float a01 = L.artificial / (float)ART_MAX;
                    sum += Mathf.Max(n01, a01);
                }

                Sample(cx0, cy0);
                Sample(cx1, cy0);
                Sample(cx0, cy1);
                Sample(cx1, cy1);

                float avg = sum * 0.25f;
                float A01 = 1f - Mathf.Clamp01(avg);
                byte Ab  = (byte)Mathf.RoundToInt(A01 * 255f);

                cols[vy * vW + vx] = new Color32(0, 0, 0, Ab);
            }
        }

        c.lightColors = cols;
        mesh.colors32 = cols;
    }

    // ───────── 타일 캐시 ─────────
    private static class TileCache
    {
        private static readonly Dictionary<ushort, Tile> cache = new();
        private static Tile[] waterLevelTiles;

        public static Tile Get(ushort id)
        {
            if (id == 0) return null;
            if (cache.TryGetValue(id, out var tile)) return tile;
            var t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = CellLibrary.GetSprite(id);
            t.name   = CellLibrary.GetName(id);
            cache[id] = t;
            return t;
        }

        public static Tile GetWaterByAmount(ushort waterId, int amountRaw)
        {
            if (amountRaw <= 0) return null;

            // 내부 표현(0..128)을 0..100 으로 압축 후 10단계로 매핑
            int amount = amountRaw;
            if (amount > 100) amount = 100;

            int level = (amount - 1) / 10 + 1; // 1..10
            if (level < 1) level = 1;
            if (level > 10) level = 10;

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
            int copyH = Mathf.CeilToInt(fullH * (level / 10f));

            var newTex = new Texture2D(fullW, fullH, TextureFormat.RGBA32, false);
            newTex.filterMode = tex.filterMode;
            newTex.wrapMode   = TextureWrapMode.Clamp;

            var clear = new Color32(0, 0, 0, 0);
            var buf   = new Color32[fullW * fullH];
            for (int i = 0; i < buf.Length; i++) buf[i] = clear;
            newTex.SetPixels32(buf);

            int srcX = Mathf.RoundToInt(r.x);
            int srcY = Mathf.RoundToInt(r.y);
            Color[] src = tex.GetPixels(srcX, srcY, fullW, copyH);
            newTex.SetPixels(0, 0, fullW, copyH, src);

            newTex.Apply(false, false);

            var spr = Sprite.Create(
                newTex,
                new Rect(0, 0, fullW, fullH),
                new Vector2(0.5f, 0.5f),
                s.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect
            );
            spr.name = $"Water_L{level}";

            var t = ScriptableObject.CreateInstance<Tile>();
            t.sprite       = spr;
            t.name         = $"Water_L{level}";
            t.colliderType = Tile.ColliderType.None;

            waterLevelTiles[level] = t;
            return t;
        }
    }
}
