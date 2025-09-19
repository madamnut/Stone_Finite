// WorldManager.cs
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
/// • Light 레이어 지원, FG(=Solid+Deco) 변경 시 국소 재계산
/// • 글로벌 틱(물/중력): FixedUpdate에서 단일 큐(더블버퍼) 처리
/// • 월드 시간: FixedUpdate(0.05s, 20틱/초) 기준, 24분=1일(1440분=1440초=28800틱)
//// </summary>
public class WorldManager : MonoBehaviour
{
    public enum CellLayer { FG, BG }

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

    [Header("Falling Blocks")]
    public FallingBlock fallingBlockPrefab;

    [Header("Drops / VFX")]
    public ItemDropper itemDropper;
    public VfxManager  vfx;

    [Header("Time Settings")]
    public int ticksPerSecond = 20;     // FixedUpdate=0.05s → 20틱/초
    public int minutesPerDay  = 24 * 60;// 1440
    public int ticksPerDay    = 28800;  // 20 * 1440

    public enum TimeBand
    {
        Midnight,     // 자정
        LateNight,    // 심야
        Dawn,         // 새벽
        EarlyMorning, // 이른아침
        Morning,      // 오전
        Noon,         // 정오
        Afternoon,    // 오후
        Evening,      // 저녁
        Dusk,         // 해질녘
        Night         // 밤
    }

    public const int ChunkSize = 16;
    private const byte NAT_MAX = 20;

    // ── Day/Night Debug ──
    [Header("Day/Night Debug")]
    public KeyCode cycleKey = KeyCode.T;
    [Range(0,20)] public byte dayOffset = 0; // 자연광 전역 감산(0..20). 디버그용은 0..20 핑퐁
    int _dayDir = 1; // +1 ↔ -1

    // Light 공유타일/색상 LUT
    private static Tile sSharedLightTile;
    private static readonly Color[] kAlphaLut = new Color[NAT_MAX + 1]; // 0..20 → 알파

    // 전역 월드 크기 캐시
    private int W, H;

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
    // 월드 시간 상태
    // ─────────────────────────────────────────────────────
    public long worldTick;                 // 누적 틱
    public int  worldMinute;               // 0..1439
    public int  worldHour;                 // 0..23
    public int  worldDay;                  // 0..N
    private long _lastLoggedSecondTick = -1;

    // ─────────────────────────────────────────────────────
    // 글로벌 틱(물/중력)
    // ─────────────────────────────────────────────────────
    private HashSet<Vector2Int> tickCurr = new();
    private HashSet<Vector2Int> tickNext = new();

    public void EnqTick(int x, int y)
    {
        if ((uint)x >= W || (uint)y >= H) return;
        tickNext.Add(new Vector2Int(x, y));
    }

    private void SwapTickBuffers()
    {
        var t = tickCurr;
        tickCurr = tickNext;
        tickNext = t;
        tickNext.Clear();
    }

    void StepTick()
    {
        if (tickCurr.Count == 0) SwapTickBuffers();
        if (tickCurr.Count == 0) return;

        foreach (var p in tickCurr)
        {
            StepGravityAt(p.x, p.y);
            StepWaterAt(p.x, p.y);
        }
        tickCurr.Clear();
    }

    // ─────────────────────────────────────────────────────
    // 물 로직
    // ─────────────────────────────────────────────────────
    void StepWaterAt(int x, int y)
    {
        ref var cell = ref worldMap.liquid[x, y];
        int Wc = cell.amount;

        if (Wc <= 0)
        {
            if (cell.id != 0)
            {
                worldMap.liquid[x, y].id = 0;
                MarkChunkDirty(x, y, markFG:false, markBG:false, markDeco:false, markLiquid:true);
            }
            return;
        }

        bool Blocked(int gx, int gy)
        {
            if ((uint)gx >= W || (uint)gy >= H) return true;
            return worldMap.solid[gx, gy].id != 0;
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

                OnCellEditedFG(x, y);
                OnCellEditedFG(x, dy);
                return;
            }
        }

        // 2) 좌우 동시 분배
        int xl = x - 1, xr = x + 1;
        bool canL = xl >= 0 && !Blocked(xl, y);
        bool canR = xr < W  && !Blocked(xr, y);

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
            else if (flowL > 0) takeL = Mathf.Min(total, flowL);
            else                takeR = Mathf.Min(total, flowR);

            WriteWater(x,  y,  Wc - (takeL + takeR));
            if (takeL > 0) WriteWater(xl, y,  Wl + takeL);
            if (takeR > 0) WriteWater(xr, y,  Wr + takeR);

            OnCellEditedFG(x, y);
            if (takeL > 0) OnCellEditedFG(xl, y);
            if (takeR > 0) OnCellEditedFG(xr, y);
        }
    }

    void WriteWater(int x, int y, int newAmount)
    {
        int cur = worldMap.liquid[x, y].amount;
        newAmount = Mathf.Clamp(newAmount, 0, 100);
        if (cur == newAmount) return;

        worldMap.liquid[x, y].amount = (byte)newAmount;
        worldMap.liquid[x, y].id     = (ushort)(newAmount > 0 ? 60000 : 0);

        MarkChunkDirty(x, y, markFG:false, markBG:false, markDeco:false, markLiquid:true);
    }

    // ─────────────────────────────────────────────────────
    // 중력 블록
    // ─────────────────────────────────────────────────────
    void StepGravityAt(int x, int y)
    {
        ushort id = worldMap.solid[x, y].id;
        if (id == 0) return;
        if (!CellLibrary.HasGravity(id)) return;

        int by = y - 1;
        if (by < 0) return;
        if (worldMap.solid[x, by].id != 0) return;

        worldMap.SetSolid(x, y, 0, false);
        MarkChunkDirty(x, y, markFG:true);

        OnCellEditedFG(x, y);

        if (fallingBlockPrefab != null)
        {
            var pos = new Vector3(x + 0.5f, y + 0.5f, 0f);
            var spr = CellLibrary.GetSprite(id);
            var fb  = Instantiate(fallingBlockPrefab, pos, Quaternion.identity);
            fb.Init(id, this, spr);
        }
    }

    // ─────────────────────────────────────────────────────
    // 설치
    // ─────────────────────────────────────────────────────
    public bool PlaceCell(int x, int y, ushort id)
    {
        if ((uint)x >= W || (uint)y >= H) return false;

        switch (CellLibrary.TypeOf(id))
        {
            case CellType.Solid:
                if (worldMap.solid[x, y].id != 0) return false;
                worldMap.SetSolid(x, y, id, CellLibrary.HasGravity(id));
                MarkChunkDirty(x, y, markFG:true, markBG:false, markDeco:false, markLiquid:true);
                OnCellEditedFG(x, y);
                return true;

            case CellType.Deco:
                if (worldMap.solid[x, y].id != 0) return false;
                if (worldMap.liquid[x, y].amount > 0) return false;
                worldMap.SetDeco(x, y, id, CellLibrary.DependFlagsOf(id));
                MarkChunkDirty(x, y, markFG:false, markBG:false, markDeco:true, markLiquid:false);
                OnCellEditedFG(x, y);
                return true;

            case CellType.Liquid:
                if (worldMap.solid[x, y].id != 0) return false;
                worldMap.SetLiquid(x, y, id, 100);
                MarkChunkDirty(x, y, markFG:false, markBG:false, markDeco:false, markLiquid:true);
                EnqTick(x, y);
                EnqTick(x-1, y); EnqTick(x+1, y);
                EnqTick(x, y+1); if (y > 0) EnqTick(x, y-1);
                return true;
        }
        return false;
    }

    // ─────────────────────────────────────────────────────
    // 파괴
    // ─────────────────────────────────────────────────────
    public ushort BreakCell(int x, int y, CellLayer layer)
    {
        if ((uint)x >= W || (uint)y >= H) return 0;

        switch (layer)
        {
            case CellLayer.FG:
            {
                ushort removed = worldMap.BreakFG(x, y);
                if (removed == 0) return 0;

                MarkChunkDirty(x, y, markFG:true, markBG:false, markDeco:true, markLiquid:false);
                OnCellEditedFG(x, y);

                string key = CellLibrary.GetKey(removed);
                if (!string.IsNullOrEmpty(key))
                {
                    var pos = new Vector3(x + 0.5f, y + 0.5f, 0f);
                    if (vfx != null) vfx.EmitBlockAtCell(key, x, y, 1, grid:3, count:-1);
                    if (itemDropper != null) itemDropper.SpawnDroppedItems(key, pos);
                }
                return removed;
            }
            case CellLayer.BG:
            {
                ushort removed = worldMap.BreakBG(x, y);
                if (removed == 0) return 0;

                MarkChunkDirty(x, y, markFG:false, markBG:true, markDeco:false, markLiquid:false);
                RecalculateLightAt(x, y);

                string key = CellLibrary.GetKey(removed);
                if (!string.IsNullOrEmpty(key))
                {
                    var pos = new Vector3(x + 0.5f, y + 0.5f, 0f);
                    if (vfx != null) vfx.EmitBlockAtCell(key, x, y, 1, grid:3, count:-1);
                    if (itemDropper != null) itemDropper.SpawnDroppedItems(key, pos);
                }
                return removed;
            }
        }
        return 0;
    }

    /// <summary>FG 편집 후 틱 인큐 + 라이트 계산</summary>
    public void OnCellEditedFG(int gx, int gy)
    {
        if ((uint)gx >= W || (uint)gy >= H) return;

        EnqTick(gx, gy);
        EnqTick(gx + 1, gy);
        EnqTick(gx - 1, gy);
        EnqTick(gx, gy + 1);
        EnqTick(gx, gy - 1);

        RecalculateLightAt(gx, gy);
    }

    // ─────────────────────────────────────────────────────
    // 생명주기
    // ─────────────────────────────────────────────────────
    void Awake()
    {
        W = settings.width;
        H = settings.height;

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

        // 시간 초기화
        worldTick = 0L;
        worldMinute = 0;
        worldHour = 0;
        worldDay = 0;
        _lastLoggedSecondTick = -ticksPerSecond;

        // Light 공유 타일/알파 LUT 초기화
        if (sSharedLightTile == null)
        {
            sSharedLightTile = ScriptableObject.CreateInstance<Tile>();
            sSharedLightTile.sprite = lightSprite;
            sSharedLightTile.colliderType = Tile.ColliderType.None;
            sSharedLightTile.name = "LightShared";
        }
        for (int i = 0; i <= NAT_MAX; i++)
        {
            float a = 1f - (i / (float)NAT_MAX);
            kAlphaLut[i] = new Color(0f, 0f, 0f, a);
        }
    }

    void Update()
    {
        // T 키로 dayOffset 0..20..19..0 핑퐁
        if (Input.GetKeyDown(cycleKey))
        {
            int next = (int)dayOffset + _dayDir;
            if (next > 20) { next = 19; _dayDir = -1; }
            else if (next < 0) { next = 1; _dayDir = 1; }

            dayOffset = (byte)next;

            foreach (var kv in activeChunks)
            {
                var c = kv.Value.GetComponent<Chunk>();
                if (c != null) c.lightDirty = true;
            }
            Debug.Log($"[DayNight] dayOffset={dayOffset}");
        }

        UpdateVisibleChunks();
    }

    void LateUpdate() => ProcessDirtyChunks();

    // 글로벌 틱 + 월드 시간
    void FixedUpdate()
    {
        StepTick();

        worldTick++;

        if (worldTick - _lastLoggedSecondTick >= ticksPerSecond)
        {
            _lastLoggedSecondTick += ticksPerSecond;

            worldMinute++;
            if (worldMinute >= minutesPerDay)
            {
                worldMinute = 0;
                worldDay++;
            }
            worldHour = worldMinute / 60;

            var band = GetTimeBand();
        }
    }

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

        var bgBuf     = c.bgBuffer;
        var fgBuf     = c.fgBuffer;
        var decoBuf   = c.decoBuffer;
        var liquidBuf = c.liquidBuffer;
        int size = ChunkSize * ChunkSize;

        for (int i = 0; i < size; i++)
        {
            bgBuf[i]     = null;
            fgBuf[i]     = null;
            decoBuf[i]   = null;
            liquidBuf[i] = null;
        }

        var bounds = new BoundsInt(0, 0, 0, ChunkSize, ChunkSize, 1);
        for (int y = 0; y < ChunkSize; y++)
        {
            for (int x = 0; x < ChunkSize; x++)
            {
                int wx = coord.x * ChunkSize + x;
                int wy = coord.y * ChunkSize + y;
                int idx = y * ChunkSize + x;
                if (wx < 0 || wx >= W || wy < 0 || wy >= H)
                    continue;

                // BG
                bgBuf[idx] = TileCache.Get(worldMap.bg[wx, wy]);

                // Solid
                ushort solidId = worldMap.solid[wx, wy].id;
                if (solidId != 0)
                {
                    var tile = TileCache.Get(solidId);
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

                // Liquid
                var liq = worldMap.liquid[wx, wy];
                liquidBuf[idx] = (liq.amount > 0 && liq.id != 0)
                    ? TileCache.GetWaterByAmount(liq.id, liq.amount)
                    : null;

                // Light: 공유 타일 + 색상
                byte n0  = worldMap.light[wx, wy].natural;
                int  ns  = n0 - dayOffset; if (ns < 0) ns = 0;
                byte art = worldMap.light[wx, wy].artificial;
                byte fin = (byte)((ns > art) ? ns : art);

                var cell = new Vector3Int(x, y, 0);
                c.lightTilemap.SetTile(cell, sSharedLightTile);
                c.lightTilemap.SetTileFlags(cell, TileFlags.None);
                c.lightTilemap.SetColor(cell, kAlphaLut[fin]);
            }
        }

        c.bgTilemap.SetTilesBlock(bounds, bgBuf);
        c.fgTilemap.SetTilesBlock(bounds, fgBuf);
        c.decoTilemap.SetTilesBlock(bounds, decoBuf);
        c.liquidTilemap.SetTilesBlock(bounds, liquidBuf);

        var coll = c.fgTilemap.GetComponent<TilemapCollider2D>();
        if (coll != null)
        {
            c.fgTilemap.RefreshAllTiles();
            coll.ProcessTilemapChanges();
        }

        c.bgDirty = c.fgDirty = c.decoDirty = c.liquidDirty = c.lightDirty = false;

        activeChunks[coord] = go;
    }

    /// <summary>Dirty 청크 갱신</summary>
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

    /// <summary>지정한 좌표의 청크 레이어 하나만 다시 그림.</summary>
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
                        if ((uint)wx >= W || (uint)wy >= H) continue;
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
                        if ((uint)wx >= W || (uint)wy >= H) continue;

                        ushort id = worldMap.solid[wx, wy].id;
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
                        if ((uint)wx >= W || (uint)wy >= H) continue;

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
                        if ((uint)wx >= W || (uint)wy >= H) continue;

                        var liq = worldMap.liquid[wx, wy];
                        buf[idx] = (liq.amount > 0 && liq.id != 0)
                            ? TileCache.GetWaterByAmount(liq.id, liq.amount)
                            : null;
                    }
                c.liquidTilemap.SetTilesBlock(bounds, buf);
                break;
            }
        }
    }

    /// <summary>라이트 레이어만 색 갱신.</summary>
    private void RefreshLightLayer(Vector2Int coord)
    {
        var go = activeChunks[coord];
        var c  = go.GetComponent<Chunk>();

        int sx = coord.x * ChunkSize, sy = coord.y * ChunkSize;
        for (int y = 0; y < ChunkSize; y++)
        for (int x = 0; x < ChunkSize; x++)
        {
            int wx = sx + x, wy = sy + y;

            byte n0  = worldMap.light[wx, wy].natural;
            int  ns  = n0 - dayOffset; if (ns < 0) ns = 0;
            byte art = worldMap.light[wx, wy].artificial;
            byte fin = (byte)((ns > art) ? ns : art);

            var cell = new Vector3Int(x, y, 0);
            c.lightTilemap.SetColor(cell, kAlphaLut[fin]);
        }
    }

    /// <summary>(x0,y0) 국소 BFS로 자연광 재계산.</summary>
    public void RecalculateLightAt(int x0, int y0)
    {
        if ((uint)x0 >= W || (uint)y0 >= H) return;

        var q = new Queue<(int x, int y)>();
        q.Enqueue((x0, y0));

        (int dx, int dy)[] dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            byte old = worldMap.light[x, y].natural;

            int attenHere = 0;
            if (worldMap.bg[x, y] != 0) attenHere += 1;
            if (worldMap.solid[x, y].id != 0) attenHere += 2;

            byte best = 0;
            foreach (var (dx, dy) in dirs)
            {
                int nx = x + dx, ny = y + dy;
                if ((uint)nx >= W || (uint)ny >= H) continue;

                int cand = worldMap.light[nx, ny].natural - attenHere;
                if (cand > best) best = (byte)Mathf.Clamp(cand, 0, NAT_MAX);
            }

            if (best != old)
            {
                worldMap.light[x, y] = new LightCell { natural = best, artificial = worldMap.light[x, y].artificial };

                foreach (var (dx, dy) in dirs)
                {
                    int mx = x + dx, my = y + dy;
                    if ((uint)mx >= W || (uint)my >= H) continue;
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

    // ─────────────────────────────────────────────────────
    // 타임밴드 판정
    // ─────────────────────────────────────────────────────
    public TimeBand GetTimeBand()
    {
        int h = worldHour;
        int m = worldMinute % 60;
        int t = h * 100 + m; // HHMM

        if (t == 0)   return TimeBand.Midnight;
        if (t < 400)  return TimeBand.LateNight;
        if (t < 600)  return TimeBand.Dawn;
        if (t < 900)  return TimeBand.EarlyMorning;
        if (t < 1200) return TimeBand.Morning;
        if (t == 1200)return TimeBand.Noon;
        if (t < 1700) return TimeBand.Afternoon;
        if (t < 1900) return TimeBand.Evening;
        if (t < 2100) return TimeBand.Dusk;
        return TimeBand.Night;
    }

    // ── 타일 캐시 ──
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
            int copyH = Mathf.CeilToInt(fullH * (level / 10f));

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

            var spr = Sprite.Create(newTex, new Rect(0, 0, fullW, fullH), new Vector2(0.5f, 0.5f), s.pixelsPerUnit, 0, SpriteMeshType.FullRect);
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
