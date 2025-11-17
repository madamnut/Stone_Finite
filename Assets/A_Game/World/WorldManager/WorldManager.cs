using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;

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
    [Tooltip("인벤토리 저장/로드 안전 참조용 Player 컴포넌트")]
    public Player playerComp;
    public int ChunkRadius = 7;
    [Tooltip("한 프레임당 최대 로드할 청크 개수")]
    public int maxLoadsPerFrame = 2;

    [Header("Falling Blocks")]
    public FallingBlock fallingBlockPrefab;

    [Header("Drops / VFX")]
    public ItemDropper itemDropper;
    public VfxManager  vfx;

    [Header("Time Settings")]
    public int ticksPerSecond = 20;
    public int minutesPerDay  = 24 * 60;
    public int ticksPerDay    = 28800;

    public enum TimeBand
    {
        Midnight, LateNight, Dawn, EarlyMorning, Morning, Noon, Afternoon, Evening, Dusk, Night
    }

    public const int ChunkSize = 16;
    private const byte NAT_MAX = 20;
    private const byte ART_MAX = 20;

    [Header("Global Brightness Offset (auto by time) 0=밝음, 18=어두움")]
    [Range(0,18)] public byte globalBrightnessOffset = 0;
    private byte _lastBrightnessOffset = 255;

    private const int ATT_AIR = 1;
    private const int ATT_BG  = 2;
    private const int ATT_FG  = 3;

    private int W, H;
    public WorldData worldMap;

    private readonly Queue<GameObject> chunkPool = new();
    private List<Vector2Int> loadList = new();
    private int loadIndex = 0;
    private readonly List<Vector2Int> unloadList = new();
    private readonly HashSet<Vector2Int> currentNeeded = new();

    private bool isLoading = false;
    private Vector2Int lastPlayerChunk;

    private readonly Dictionary<Vector2Int, GameObject> activeChunks = new();

    // 월드 시간
    public long worldTick;
    public int  worldMinute;
    public int  worldHour;
    public int  worldDay;
    private long _lastLoggedSecondTick = -1;

    // 글로벌 틱(물/중력)
    private HashSet<Vector2Int> tickCurr = new();
    private HashSet<Vector2Int> tickNext = new();

    [Header("아이템 라이브러리(인벤 복원용)")]
    public ItemLibrary itemLibrary;

    // 종료 저장 가드
    private bool _didQuitSave = false;

    // 로드시 임시 보관(플레이어/인벤)
    private bool    _hasLoadedPlayerData = false;
    private Vector2 _loadedPlayerPos;
    private List<ItemData> _loadedInventory;

    public void EnqTick(int x, int y)
    {
        if ((uint)x >= W || ((uint)y >= H)) return;
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

    // ───────── 물 ─────────
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

        // 1) 아래로
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

        // 2) 좌우
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

    // ───────── 중력 블록 ─────────
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

    // ───────── 설치 ─────────
    public bool PlaceCell(int x, int y, ushort id)
    {
        if ((uint)x >= W || (uint)y >= H) return false;

        switch (CellLibrary.TypeOf(id))
        {
            case CellType.Solid:
            {
                if (worldMap.solid[x, y].id != 0) return false;

                ushort oldId = worldMap.solid[x, y].id;
                worldMap.SetSolid(x, y, id, CellLibrary.HasGravity(id));
                MarkChunkDirty(x, y, markFG:true, markBG:false, markDeco:false, markLiquid:true);
                OnCellEditedFG(x, y);

                HandleArtificialChange(x, y, oldId, id, isDeco:false);
                return true;
            }

            case CellType.Deco:
            {
                if (worldMap.solid[x, y].id != 0) return false;
                if (worldMap.liquid[x, y].amount > 0) return false;

                ushort oldId = worldMap.deco[x, y].id;
                worldMap.SetDeco(x, y, id, CellLibrary.DependFlagsOf(id));
                MarkChunkDirty(x, y, markFG:false, markBG:false, markDeco:true, markLiquid:false);

                HandleArtificialChange(x, y, oldId, id, isDeco:true);
                return true;
            }

            case CellType.Liquid:
            {
                if (worldMap.solid[x, y].id != 0) return false;
                worldMap.SetLiquid(x, y, id, 100);
                MarkChunkDirty(x, y, markFG:false, markBG:false, markDeco:false, markLiquid:true);
                EnqTick(x, y);
                EnqTick(x-1, y); EnqTick(x+1, y);
                EnqTick(x, y+1); if (y > 0) EnqTick(x, y-1);
                return true;
            }
        }
        return false;
    }

    // ───────── 파괴 ─────────
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

                HandleArtificialChange(x, y, removed, 0, isDeco:false);

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

    // ───────── 생명주기 ─────────
    void Awake()
    {
        W = settings.width;
        H = settings.height;

        // 틱 큐 초기화
        tickCurr.Clear();
        tickNext.Clear();

        // BOOT 로그
        string dirBoot = WorldLoadContext.GetSavePath();
        string pathBoot = Path.Combine(dirBoot, "world.bin");
        Debug.Log($"[BOOT] loadType={WorldLoadContext.loadType}, seed={WorldLoadContext.seed}, saveExists={File.Exists(pathBoot)}, path={pathBoot}");

        // 생성/로드 분기
        if (WorldLoadContext.loadType == WorldLoadContext.LoadType.NewWorld)
        {
            Debug.Log("[BOOT] NewWorld branch: Generate → SaveWorld()");
            worldMap = WorldDataGenerator.Generate(settings, WorldLoadContext.seed);

            // ───────── 새 월드 최초 스폰 위치 결정 ─────────
            if (player != null)
            {
                int centerX = 2500;
                if (centerX < 0) centerX = 0;
                if (centerX >= W) centerX = W - 1;

                bool found = false;
                int spawnX = centerX;
                int spawnY = 0;

                // 2500 → 2499 → 2501 → 2498 → 2502 ... 순서로 탐색
                for (int radius = 0; radius < W; radius++)
                {
                    int[] xs =
                    {
                        centerX,
                        centerX - radius,
                        centerX + radius
                    };

                    foreach (int x in xs)
                    {
                        if (found) break;
                        if (x < 0 || x >= W) continue;

                        // 위에서 아래로 스캔하면서 solid 또는 liquid 중 먼저 만나는 것 판단
                        for (int y = H - 1; y >= 0; y--)
                        {
                            ushort solid = worldMap.solid[x, y].id;
                            byte   water = worldMap.liquid[x, y].amount;

                            if (water > 0)
                            {
                                // 액체가 먼저 나오면 이 x 전체 스킵
                                break;
                            }

                            if (solid != 0)
                            {
                                // 솔리드가 먼저 나왔으므로 스폰 지점은 y + 5
                                int ySpawn = Mathf.Min(y + 5, H - 1);

                                spawnX = x;
                                spawnY = ySpawn;
                                found = true;
                                break;
                            }
                        }
                    }

                    if (found) break;
                }

                if (found)
                {
                    float px = spawnX + 0.5f;
                    float py = spawnY + 0.5f;
                    player.position = new Vector3(px, py, player.position.z);
                    Debug.Log($"[SPAWN] Spawn at X={spawnX}, Y={spawnY}");
                }
                else
                {
                    Debug.LogWarning("[SPAWN] 적절한 스폰 위치를 찾지 못했습니다. 기존 플레이어 위치 유지.");
                }
            }

            // 스폰까지 반영된 상태로 첫 저장
            SaveWorld();
        }
        else if (WorldLoadContext.loadType == WorldLoadContext.LoadType.LoadWorld)
        {
            if (!LoadWorldFromDisk(out worldMap))
            {
                Debug.LogError("[BOOT] 저장 파일을 읽을 수 없습니다. (없음 or 포맷 불일치)");
                SceneManager.LoadScene("Loby");
                return;
            }

            // 월드 로드 성공 시 플레이어/엔티티 데이터 로드
            LoadPlayerData();
            LoadEntities();
        }

        if (chunkRoot == null) chunkRoot = transform;

        for (int i = 0; i < initialPoolSize; i++)
        {
            var go = Instantiate(chunkPrefab, chunkRoot);
            go.SetActive(false);
            chunkPool.Enqueue(go);
        }

        // Player 컴포넌트 자동 보정
        if (playerComp == null && player != null)
        {
            playerComp = player.GetComponent<Player>();
            if (playerComp == null) playerComp = player.GetComponentInParent<Player>();
            if (playerComp == null) playerComp = player.GetComponentInChildren<Player>();
        }
        if (playerComp == null)
            Debug.LogWarning("WorldManager: Player 컴포넌트를 찾지 못했습니다. 인벤토리 저장/로드가 비활성화됩니다.");

        lastPlayerChunk = GetPlayerChunk();

        // 월드 시간 초기화/복원
        if (WorldLoadContext.loadType == WorldLoadContext.LoadType.NewWorld)
        {
            // worldTick = 0을 12:00 기준으로 사용
            worldTick = 0L;
            worldMinute = 12 * 60; // 720 = 12:00
            worldHour = 12;
            worldDay = 0;
        }
        else
        {
            if (ticksPerDay > 0 && minutesPerDay > 0)
            {
                // worldTick = 0 을 "12:00" 으로 간주하는 새로운 기준

                long day       = worldTick / ticksPerDay;
                long tickOfDay = worldTick % ticksPerDay;
                int ticksPerMin = ticksPerDay / minutesPerDay;

                // base = 12:00 (720분)
                int baseMinutes = 12 * 60;

                int minuteOfDay = baseMinutes + (ticksPerMin > 0 ? (int)(tickOfDay / ticksPerMin) : 0);

                // 하루 범위 보정
                minuteOfDay %= minutesPerDay;

                worldDay    = (int)day;
                worldMinute = minuteOfDay;
                worldHour   = worldMinute / 60;
            }
            else
            {
                worldDay    = 0;
                worldMinute = 0;
                worldHour   = 0;
            }
        }
        _lastLoggedSecondTick = worldTick;

        // 인벤 복원은 Start에서 호출
        ApplyTimeSyncedBrightness(forceDirty:true);
    }

    void Start()
    {
        ApplyLoadedPlayerAndInventory(); // 모든 Awake 이후 시점
        StartCoroutine(AutosaveLoop());
    }

    void OnApplicationQuit()
    {
        if (_didQuitSave) return;
        _didQuitSave = true;
        SaveWorld();
    }

    public void OnClickSave()
    {
        SaveWorld();
    }

    void Update()
    {
        UpdateVisibleChunks();
    }

    void LateUpdate() => ProcessDirtyChunks();

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

            ApplyTimeSyncedBrightness(forceDirty:false);
            var band = GetTimeBand();
        }
    }

    IEnumerator AutosaveLoop()
    {
        var wait = new WaitForSecondsRealtime(300f);
        while (true) { yield return wait; SaveWorld(); }
    }

    private void ApplyTimeSyncedBrightness(bool forceDirty)
    {
        int m = worldHour * 60 + (worldMinute % 60); // 0..1439
        float off =
            (m >= 300  && m < 540 ) ? 18f * (1f - (m - 300) / 240f) :
            (m >= 540  && m < 1080) ? 0f :
            (m >= 1080 && m < 1260) ? 18f * ((m - 1080) / 180f) :
                                       18f;
        byte newOffset = (byte)Mathf.RoundToInt(Mathf.Clamp(off, 0f, 18f));

        if (forceDirty || newOffset != globalBrightnessOffset)
        {
            globalBrightnessOffset = newOffset;

            if (newOffset != _lastBrightnessOffset || forceDirty)
            {
                _lastBrightnessOffset = newOffset;
                foreach (var kv in activeChunks)
                {
                    var c = kv.Value.GetComponent<Chunk>();
                    if (c != null) c.lightDirty = true;
                }
            }
        }
    }

    private void UpdateVisibleChunks()
    {
        Vector2Int playerChunk = GetPlayerChunk();

        // 같은 청크에 머물러 있고, 로드 큐도 비었고, 이미 청크가 떠 있으면 계산 스킵
        if (playerChunk == lastPlayerChunk &&
            loadList.Count == 0 &&
            activeChunks.Count > 0)
            return;

        // 순간이동 수준으로 멀리 이동했으면 로드 큐 초기화
        if ((playerChunk - lastPlayerChunk).sqrMagnitude > (ChunkRadius * ChunkRadius * 4))
        {
            loadList.Clear();
            loadIndex = 0;
        }
        lastPlayerChunk = playerChunk;

        // 유효 청크 범위 계산
        int cxMin = 0;
        int cyMin = 0;
        int cxMax = Mathf.Max(0, (W - 1) / ChunkSize);
        int cyMax = Mathf.Max(0, (H - 1) / ChunkSize);

        // 필요한 청크 집합 구성
        currentNeeded.Clear();
        for (int dy = -ChunkRadius; dy <= ChunkRadius; dy++)
        for (int dx = -ChunkRadius; dx <= ChunkRadius; dx++)
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

        if (!isLoading && loadList.Count > 0)
            StartCoroutine(ProcessLoadQueue());
    }

    private IEnumerator ProcessLoadQueue()
    {
        isLoading = true;

        // 유효 청크 범위 계산
        int cxMin = 0, cyMin = 0;
        int cxMax = Mathf.Max(0, (W - 1) / ChunkSize);
        int cyMax = Mathf.Max(0, (H - 1) / ChunkSize);

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
        // 유효 청크 범위 하드 가드
        int cxMax = Mathf.Max(0, (W - 1) / ChunkSize);
        int cyMax = Mathf.Max(0, (H - 1) / ChunkSize);
        if (coord.x < 0 || coord.y < 0 || coord.x > cxMax || coord.y > cyMax) return;

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

        activeChunks[coord] = go;

        RefreshLightLayer(coord);

        c.bgDirty = c.fgDirty = c.decoDirty = c.liquidDirty = c.lightDirty = false;
    }

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

    private void RefreshLightLayer(Vector2Int coord)
    {
        if (!activeChunks.TryGetValue(coord, out var go)) return;
        var c = go.GetComponent<Chunk>();
        if (c == null || c.lightMeshFilter == null) return;

        var mesh = c.lightMeshFilter.sharedMesh;
        if (mesh == null) return;

        int vW = ChunkSize + 1, vH = ChunkSize + 1, vCount = vW * vH;
        var cols = (c.lightColors != null && c.lightColors.Length == vCount)
            ? c.lightColors : new Color32[vCount];

        int sx = coord.x * ChunkSize, sy = coord.y * ChunkSize;

        for (int vy = 0; vy <= ChunkSize; vy++)
        {
            for (int vx = 0; vx <= ChunkSize; vx++)
            {
                int gx = sx + vx, gy = sy + vy;

                int cx0 = Mathf.Clamp(gx - 1, 0, W - 1);
                int cy0 = Mathf.Clamp(gy - 1, 0, H - 1);
                int cx1 = Mathf.Clamp(gx    , 0, W - 1);
                int cy1 = Mathf.Clamp(gy    , 0, H - 1);

                float sum = 0f;

                void Sample(int x, int y)
                {
                    var L = worldMap.light[x, y];
                    int ns = L.natural - globalBrightnessOffset; if (ns < 0) ns = 0;
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

    public void RecalculateLightAt(int x0, int y0)
    {
        if ((uint)x0 >= W || (uint)y0 >= H) return;

        var q = new Queue<(int x, int y)>();
        q.Enqueue((x0, y0));

        (int dx, int dy)[] dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            byte oldN = worldMap.light[x, y].natural;

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

            if (best != oldN)
            {
                var cell = worldMap.light[x, y];
                cell.natural = best;
                worldMap.light[x, y] = cell;

                foreach (var (dx, dy) in dirs)
                {
                    int mx = x + dx, my = y + dy;
                    if ((uint)mx >= W || ((uint)my >= H)) continue;
                    q.Enqueue((mx, my));
                }

                var coord = new Vector2Int(x / ChunkSize, y / ChunkSize);
                if (activeChunks.TryGetValue(coord, out var go))
                    go.GetComponent<Chunk>().lightDirty = true;

                int rx = x % ChunkSize;
                int ry = y % ChunkSize;
                if (rx == 0)
                {
                    var left = new Vector2Int(coord.x - 1, coord.y);
                    if (activeChunks.TryGetValue(left, out var goL))
                        goL.GetComponent<Chunk>().lightDirty = true;
                }
                else if (rx == ChunkSize - 1)
                {
                    var right = new Vector2Int(coord.x + 1, coord.y);
                    if (activeChunks.TryGetValue(right, out var goR))
                        goR.GetComponent<Chunk>().lightDirty = true;
                }
                if (ry == 0)
                {
                    var down = new Vector2Int(coord.x, coord.y - 1);
                    if (activeChunks.TryGetValue(down, out var goD))
                        goD.GetComponent<Chunk>().lightDirty = true;
                }
                else if (ry == ChunkSize - 1)
                {
                    var up = new Vector2Int(coord.x, coord.y + 1);
                    if (activeChunks.TryGetValue(up, out var goU))
                        goU.GetComponent<Chunk>().lightDirty = true;
                }
            }
        }
    }

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

    private void MarkLightDirtyRect(int x, int y, int w, int h)
    {
        int x0 = Mathf.Clamp(x, 0, W-1);
        int y0 = Mathf.Clamp(y, 0, H-1);
        int x1 = Mathf.Clamp(x + w - 1, 0, W-1);
        int y1 = Mathf.Clamp(y + h - 1, 0, H-1);

        int cx0 = x0 / ChunkSize, cy0 = y0 / ChunkSize;
        int cx1 = x1 / ChunkSize, cy1 = y1 / ChunkSize;

        for (int cy = cy0; cy <= cy1; cy++)
            for (int cx = cx0; cx <= cx1; cx++)
            {
                var coord = new Vector2Int(cx, cy);
                if (activeChunks.TryGetValue(coord, out var go))
                    go.GetComponent<Chunk>().lightDirty = true;
            }
    }

    public TimeBand GetTimeBand()
    {
        int h = worldHour;
        int m = worldMinute % 60;
        int t = h * 100 + m;

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

    // ───────── 인공광(스칼라) ─────────
    private void HandleArtificialChange(int x, int y, ushort oldId, ushort newId, bool isDeco)
    {
        byte oldB = CellLibrary.BrightnessOf(oldId);
        byte newB = CellLibrary.BrightnessOf(newId);
        if (oldB == 0 && newB == 0) return;

        int r = Mathf.Max(oldB, newB);
        int x0 = Mathf.Max(0, x - r), y0 = Mathf.Max(0, y - r);
        int x1 = Mathf.Min(W - 1, x + r), y1 = Mathf.Min(H - 1, y + r);

        for (int yy = y0; yy <= y1; yy++)
        for (int xx = x0; xx <= x1; xx++)
        {
            var lc = worldMap.light[xx, yy];
            lc.artificial = 0;
            worldMap.light[xx, yy] = lc;
        }

        for (int yy = y0; yy <= y1; yy++)
        for (int xx = x0; xx <= x1; xx++)
        {
            ushort sid = worldMap.solid[xx, yy].id;
            ushort did = worldMap.deco[xx, yy].id;

            ushort id = (sid != 0) ? sid : did;
            if (id == 0) continue;

            byte b = CellLibrary.BrightnessOf(id);
            if (b > 0) AddLightScalar(xx, yy, b);
        }

        MarkLightDirtyRect(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    private void AddLightScalar(int sx, int sy, byte b)
    {
        if ((uint)sx >= W || ((uint)sy >= H) || b == 0) return;

        var q = new Queue<(int x,int y, byte v)>();
        q.Enqueue((sx, sy, b));

        while (q.Count > 0)
        {
            var (x, y, v) = q.Dequeue();
            if ((uint)x >= W || ((uint)y >= H)) continue;

            var cell = worldMap.light[x, y];
            if (v <= cell.artificial) continue;
            cell.artificial = v;
            worldMap.light[x, y] = cell;

            if (v <= 1) continue;

            void Prop(int nx, int ny)
            {
                if ((uint)nx >= W || ((uint)ny >= H)) return;

                int cost = ATT_AIR;
                if (worldMap.solid[nx, ny].id != 0) cost = ATT_FG;
                else if (worldMap.bg[nx, ny] != 0)  cost = ATT_BG;

                int nv = v - cost;
                if (nv > 0 && nv > worldMap.light[nx, ny].artificial)
                    q.Enqueue((nx, ny, (byte)nv));
            }

            Prop(x + 1, y);
            Prop(x - 1, y);
            Prop(x, y + 1);
            Prop(x, y - 1);
        }
    }

    // ───────── 저장/로드 (WorldSaveSystem 위임) ─────────
    public void SaveWorld()
    {
        Player pCompLog = playerComp;
        if (pCompLog == null && player != null)
        {
            pCompLog = player.GetComponent<Player>();
            if (pCompLog == null) pCompLog = player.GetComponentInParent<Player>();
            if (pCompLog == null) pCompLog = player.GetComponentInChildren<Player>();
        }

        WorldSaveSystem.SaveWorld(
            W,
            H,
            worldMap,
            worldTick,
            tickCurr,
            tickNext,
            pCompLog,
            player,
            itemDropper
        );
    }

    bool LoadWorldFromDisk(out WorldData loaded)
    {
        int w, h;
        long loadedTick;
        bool ok = WorldSaveSystem.LoadWorldFromDisk(
            out loaded,
            out w,
            out h,
            out loadedTick,
            tickCurr,
            tickNext
        );

        if (ok)
        {
            W = w;
            H = h;
            worldTick = loadedTick;
        }

        return ok;
    }

    private void LoadPlayerData()
    {
        _hasLoadedPlayerData = WorldSaveSystem.LoadPlayerData(
            itemLibrary,
            out _loadedPlayerPos,
            out _loadedInventory
        );
    }

    private void LoadEntities()
    {
        WorldSaveSystem.LoadEntities(itemDropper, itemLibrary);
    }

    private void ApplyLoadedPlayerAndInventory()
    {
        if (!_hasLoadedPlayerData || player == null) return;

        // 플레이어 위치 적용
        var pos = player.position;
        player.position = new Vector3(_loadedPlayerPos.x, _loadedPlayerPos.y, pos.z);

        // 인벤토리 적용
        if (_loadedInventory == null) return;

        Player pComp = playerComp;
        if (pComp == null && player != null)
        {
            pComp = player.GetComponent<Player>();
            if (pComp == null) pComp = player.GetComponentInParent<Player>();
            if (pComp == null) pComp = player.GetComponentInChildren<Player>();
        }
        if (pComp == null || pComp.Inventory == null) return;

        var slots = pComp.Inventory.items;
        int n = Mathf.Min(slots.Count, _loadedInventory.Count);

        for (int i = 0; i < n; i++)
        {
            var data = _loadedInventory[i];
            slots[i] = (data != null && data.Count > 0) ? data : null;
        }

        // 남은 슬롯은 비우기
        for (int i = n; i < slots.Count; i++)
            slots[i] = null;

        pComp.Inventory.NotifyChanged();
    }
}
