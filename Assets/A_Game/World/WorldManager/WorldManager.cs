using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WorldManager : MonoBehaviour
{
    public enum CellLayer { FG, BG }

    [Header("월드 생성 설정")]
    public WorldGenSettings settings;

    [Header("청크 Prefab & 관리")]
    public GameObject chunkPrefab;
    public Transform chunkRoot;
    public int initialPoolSize = 200;

    [Header("플레이어 및 렌더링 설정")]
    public Transform player;
    [Tooltip("인벤토리 저장/로드 안전 참조용 Player 컴포넌트")]
    public Player playerComp;
    public int ChunkRadius = 7;
    [Tooltip("한 프레임당 최대 로드할 청크 개수")]
    public int maxLoadsPerFrame = 4;

    [Header("Falling Blocks")]
    public FallingBlock fallingBlockPrefab;

    [Header("Drops / VFX")]
    public ItemDropper itemDropper;
    public VfxManager  vfx;

    [Header("Corpse")]
    public CorpseLibrary corpseLibrary;

    [Header("엔티티 시스템")]
    public EntityManager entityManager;

    [Header("Mob")]
    public MobLibrary mobLibrary;

    [Header("Time Settings")]
    public int ticksPerSecond = 20;
    public int minutesPerDay  = 24 * 60;
    public int ticksPerDay    = 28800;

    public enum TimeBand
    {
        Midnight, LateNight, Dawn, EarlyMorning, Morning, Noon, Afternoon, Evening, Dusk, Night
    }

    public const int ChunkSize = 16;
    private const byte NAT_MAX = 15;
    private const byte ART_MAX = 15;

    [Header("Global Brightness Offset (auto by time) 0=밝음, 15=어두움")]
    [Range(0, 15)] public byte globalBrightnessOffset = 0;

    [Header("Night Darkness Limit (0=밝음, 15=완전 암흑)")]
    [Range(0, 15)] public byte maxDarknessOffset = 3;

    private byte _lastBrightnessOffset = 255;

    private const int ATT_AIR = 1;
    private const int ATT_BG  = 2;
    private const int ATT_FG  = 3;

    private int W, H;

    // 외부에서 직접 만지지 않도록 숨김 (월드 편집은 반드시 WorldManager API 경유)
    private WorldData worldMap;

    private WorldChunkSystem chunkSystem;

    public long worldTick;
    public int  worldMinute;
    public int  worldHour;
    public int  worldDay;
    private long _lastLoggedSecondTick = -1;

    private HashSet<Vector2Int> tickCurr = new();
    private HashSet<Vector2Int> tickNext = new();

    [Header("아이템 라이브러리(인벤 복원용)")]
    public ItemLibrary itemLibrary;

    private bool _didQuitSave = false;

    private bool    _hasLoadedPlayerData = false;
    private Vector2 _loadedPlayerPos;
    private List<ItemData> _loadedInventory;

    [Header("Random Tick")]
    public int randomTicksPerWorldTick = 64;

    [Header("Artificial Light Flood (Budget)")]
    [Tooltip("FixedUpdate 1회당 인공빛 큐에서 처리할 최대 노드 수(감소+증가 합산)")]
    public int artificialLightOpsPerTick = 8000;

    // ────────────────────────────────────────────────
    // Artificial Light: Increase / Decrease Queues
    // ────────────────────────────────────────────────
    private struct IncNode
    {
        public int x, y;
        public byte v;
        public IncNode(int x, int y, byte v) { this.x = x; this.y = y; this.v = v; }
    }

    private struct DecNode
    {
        public int x, y;
        public byte v;
        public DecNode(int x, int y, byte v) { this.x = x; this.y = y; this.v = v; }
    }

    private readonly Queue<IncNode> _incQ = new();
    private readonly Queue<DecNode> _decQ = new();

    // Decrease 중에 발견된 "다른 광원 영향권" 시드(Decrease 끝난 뒤에만 Increase로 복구)
    private readonly HashSet<Vector2Int> _seedSet  = new();
    private readonly List<Vector2Int>    _seedList = new();

    private readonly HashSet<Vector2Int> _lightChangedSet  = new();
    private readonly List<Vector2Int>    _lightChangedList = new();

    /*────────────────────────────────────────────────────────────
     * Read-only Query (외부 조회는 여기로만)
     *────────────────────────────────────────────────────────────*/
    public bool   InBounds(int x, int y) => worldMap.InBounds(x, y);
    public ushort GetFGId(int x, int y) => worldMap.GetFGId(x, y);
    public ushort GetBGId(int x, int y) => worldMap.GetBGId(x, y);
    public ushort GetFluidId(int x, int y, out byte amount) => worldMap.GetFluidId(x, y, out amount);
    public bool   IsCollidable(int x, int y) => worldMap.IsCollidable(x, y);

    /*────────────────────────────────────────────────────────────
     * Tick
     *────────────────────────────────────────────────────────────*/
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
            StepFluidAt(p.x, p.y);
        }
        tickCurr.Clear();
    }

    void DoRandomTicks()
    {
        if (!Application.isPlaying) return;
        if (randomTicksPerWorldTick <= 0) return;

        Vector3 p = player.position;
        int pcx = Mathf.FloorToInt(p.x / ChunkSize);
        int pcy = Mathf.FloorToInt(p.y / ChunkSize);

        int r = ChunkRadius;

        int cxMin = pcx - r;
        int cxMax = pcx + r;
        int cyMin = pcy - r;
        int cyMax = pcy + r;

        int xMin = cxMin * ChunkSize;
        int xMax = (cxMax + 1) * ChunkSize;
        int yMin = cyMin * ChunkSize;
        int yMax = (cyMax + 1) * ChunkSize;

        if (xMin < 0) xMin = 0;
        if (yMin < 0) yMin = 0;
        if (xMax > W) xMax = W;
        if (yMax > H) yMax = H;

        if (xMin >= xMax || yMin >= yMax) return;

        for (int i = 0; i < randomTicksPerWorldTick; i++)
        {
            int gx = Random.Range(xMin, xMax);
            int gy = Random.Range(yMin, yMax);

            ushort fgId = worldMap.GetFGId(gx, gy);
            if (fgId != 0)
            {
                string nm = CellLibrary.GetName(fgId);
                if (!string.IsNullOrEmpty(nm) && nm.StartsWith("Grass_"))
                {
                    if (Random.value < 0.05f)
                    {
                        Vector3 spawnPos = new Vector3(gx + 0.5f, gy + 1.5f, 0f);
                        mobLibrary.SpawnMob("Cow", spawnPos, entityManager);
                    }
                }
            }
        }
    }

    /*────────────────────────────────────────────────────────────
     * Fluid Simulation (내부 전용: SetFluid/MoveFluid)
     *────────────────────────────────────────────────────────────*/
    void StepFluidAt(int x, int y)
    {
        if (!worldMap.InBounds(x, y)) return;

        ref var cell = ref worldMap.fg[x, y];

        ushort fluidId = cell.fluidId;
        int    Wc      = cell.fluidAmount;

        // 비어있는데 id가 남아있으면 정리
        if (Wc <= 0)
        {
            if (fluidId != 0)
            {
                SetFluidInternal(x, y, 0, 0);
                MarkChunkDirty(x, y, markFG: true);
            }
            return;
        }

        if (fluidId == 0)
        {
            // amount 있는데 id가 0이면 정리(데이터 정합성)
            SetFluidInternal(x, y, 0, 0);
            MarkChunkDirty(x, y, markFG: true);
            return;
        }

        bool Blocked(int gx, int gy)
        {
            if (!worldMap.InBounds(gx, gy)) return true;
            return worldMap.IsCollidable(gx, gy);
        }

        // 1) 아래로
        int dy = y - 1;
        if (dy >= 0 && !Blocked(x, dy))
        {
            ref var below = ref worldMap.fg[x, dy];

            // 다른 유체가 이미 있으면 이동 불가(혼합 금지 정책)
            if (below.fluidAmount > 0 && below.fluidId != 0 && below.fluidId != fluidId)
                return;

            int Wd  = below.fluidAmount;
            int cap = WorldData.MaxFluid - Wd;
            if (cap > 0)
            {
                int move = Mathf.Min(Wc, cap);

                MoveFluidInternal(x, y, x, dy, fluidId, move);

                OnCellEditedFG(x, y);
                OnCellEditedFG(x, dy);
                return;
            }
        }

        // 2) 좌우
        int xl = x - 1, xr = x + 1;
        bool canL = xl >= 0 && !Blocked(xl, y);
        bool canR = xr < W  && !Blocked(xr, y);

        int Wl = 0, Wr = 0;

        if (canL)
        {
            var c = worldMap.fg[xl, y];
            if (c.fluidAmount > 0 && c.fluidId != 0 && c.fluidId != fluidId) canL = false;
            else Wl = c.fluidAmount;
        }
        if (canR)
        {
            var c = worldMap.fg[xr, y];
            if (c.fluidAmount > 0 && c.fluidId != 0 && c.fluidId != fluidId) canR = false;
            else Wr = c.fluidAmount;
        }

        int capL = canL ? (WorldData.MaxFluid - Wl) : 0;
        int capR = canR ? (WorldData.MaxFluid - Wr) : 0;

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

            if (takeL > 0) MoveFluidInternal(x, y, xl, y, fluidId, takeL);
            if (takeR > 0) MoveFluidInternal(x, y, xr, y, fluidId, takeR);

            OnCellEditedFG(x, y);
            if (takeL > 0) OnCellEditedFG(xl, y);
            if (takeR > 0) OnCellEditedFG(xr, y);
        }
    }

    // 내부 전용: 결과값 세팅(시뮬용). 외부에서 호출 금지.
    void SetFluidInternal(int x, int y, ushort fluidId, int newAmount)
    {
        ref var cell = ref worldMap.fg[x, y];

        newAmount = Mathf.Clamp(newAmount, 0, WorldData.MaxFluid);

        if (newAmount == 0)
        {
            cell.fluidId     = 0;
            cell.fluidAmount = 0;
        }
        else
        {
            cell.fluidId     = fluidId;
            cell.fluidAmount = (byte)newAmount;
        }

        MarkChunkDirty(x, y, markFG: true);
    }

    // 내부 전용: 원자적 이동(시뮬용). 외부에서 호출 금지.
    void MoveFluidInternal(int fx, int fy, int tx, int ty, ushort fluidId, int amount)
    {
        if (amount <= 0) return;

        ref var from = ref worldMap.fg[fx, fy];
        ref var to   = ref worldMap.fg[tx, ty];

        if (from.fluidAmount <= 0 || from.fluidId != fluidId) return;
        if (to.fluidAmount > 0 && to.fluidId != 0 && to.fluidId != fluidId) return;

        int fromAmt = from.fluidAmount;
        int toAmt   = to.fluidAmount;

        int move = Mathf.Min(amount, fromAmt);
        move = Mathf.Min(move, WorldData.MaxFluid - toAmt);
        if (move <= 0) return;

        SetFluidInternal(fx, fy, fluidId, fromAmt - move);
        SetFluidInternal(tx, ty, fluidId, toAmt + move);
    }

    /*────────────────────────────────────────────────────────────
     * Gravity
     *────────────────────────────────────────────────────────────*/
    void StepGravityAt(int x, int y)
    {
        if (!worldMap.InBounds(x, y)) return;

        ref var cell = ref worldMap.fg[x, y];
        ushort id = cell.id;
        if (id == 0) return;

        bool hasGravity = (cell.flags & FgFlags.HasGravity) != 0;
        if (!hasGravity) return;

        int by = y - 1;
        if (by < 0) return;

        if (worldMap.GetFGId(x, by) != 0) return;

        ushort removedId = worldMap.RemoveFG(x, y);
        if (removedId == 0) return;

        MarkChunkDirty(x, y, markFG: true);
        OnCellEditedFG(x, y);

        var pos = new Vector3(x + 0.5f, y + 0.5f, 0f);
        var spr = CellLibrary.GetSprite(id);

        var fb = Instantiate(fallingBlockPrefab, pos, Quaternion.identity);
        fb.Init(id, this, spr);

        entityManager.Register(fb);
    }

    /*────────────────────────────────────────────────────────────
     * World Edit API (6종)
     *────────────────────────────────────────────────────────────*/

    // ───────── 설치(FG) ─────────
    public bool PlaceFG(int x, int y, ushort id)
    {
        if (!worldMap.InBounds(x, y)) return false;
        if (id == 0) return false;

        ushort oldId = worldMap.GetFGId(x, y);

        var src = CellLibrary.MakeFgCell(id);
        bool ok = worldMap.TryPlaceFG(x, y, in src);
        if (!ok) return false;

        MarkChunkDirty(x, y, markFG: true);
        OnCellEditedFG(x, y);

        HandleArtificialChange(x, y, oldId, id);
        return true;
    }

    // ───────── 설치(Fluid) ─────────
    public bool PlaceFluid(int x, int y, ushort fluidId, byte amount)
    {
        if (!worldMap.InBounds(x, y)) return false;
        if (fluidId == 0 || amount == 0) return false;

        worldMap.TryPlaceFluid(x, y, fluidId, amount, out byte leftover);
        int inserted = amount - leftover;
        if (inserted <= 0) return false;

        MarkChunkDirty(x, y, markFG: true);
        OnCellEditedFG(x, y);
        return true;
    }

    // ───────── 설치(BG) ─────────
    public bool PlaceBG(int x, int y, ushort id)
    {
        if (!worldMap.InBounds(x, y)) return false;
        if (id == 0) return false;

        ushort oldId = worldMap.bg[x, y];
        if (oldId == id) return false;

        worldMap.bg[x, y] = id;

        MarkChunkDirty(x, y, markFG: false, markBG: true);
        RecalculateLightAt(x, y);
        return true;
    }

    // ───────── 파괴(FG) ─────────
    public ushort BreakFG(int x, int y)
    {
        if ((uint)x >= W || (uint)y >= H) return 0;

        ushort removed = worldMap.RemoveFG(x, y);
        if (removed == 0) return 0;

        MarkChunkDirty(x, y, markFG: true);
        OnCellEditedFG(x, y);

        HandleArtificialChange(x, y, removed, 0);

        string key = CellLibrary.GetKey(removed);
        if (!string.IsNullOrEmpty(key))
        {
            var pos3 = new Vector3(x + 0.5f, y + 0.5f, 0f);
            vfx.EmitBlockAtCell(key, x, y, 1, grid: 3, count: -1);
            itemDropper.SpawnDroppedItems(key, pos3);
        }
        return removed;
    }

    // ───────── 파괴(Fluid) ─────────
    public (ushort removedFluidId, byte removedFluidAmount) BreakFluid(int x, int y)
    {
        if ((uint)x >= W || (uint)y >= H) return (0, 0);

        var removed = worldMap.RemoveFluid(x, y);
        if (removed.removedFluidId == 0 || removed.removedFluidAmount == 0) return removed;

        MarkChunkDirty(x, y, markFG: true);
        OnCellEditedFG(x, y);
        return removed;
    }

    // ───────── 파괴(BG) ─────────
    public ushort BreakBG(int x, int y)
    {
        if ((uint)x >= W || (uint)y >= H) return 0;

        ushort removed = worldMap.RemoveBG(x, y);
        if (removed == 0) return 0;

        MarkChunkDirty(x, y, markFG: false, markBG: true);
        RecalculateLightAt(x, y);
        return removed;
    }

    // ───────── 기존 API 유지(호환용) ─────────
    public bool PlaceCell(int x, int y, ushort id) => PlaceFG(x, y, id);
    public bool PlaceBgCell(int x, int y, ushort id) => PlaceBG(x, y, id);
    public ushort BreakCell(int x, int y, CellLayer layer)
    {
        return layer == CellLayer.FG ? BreakFG(x, y) : BreakBG(x, y);
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

    /*────────────────────────────────────────────────────────────
     * Lifecycle
     *────────────────────────────────────────────────────────────*/
    void Awake()
    {
        W = settings.width;
        H = settings.height;

        tickCurr.Clear();
        tickNext.Clear();

        string dirBoot  = WorldLoadContext.GetSavePath();
        string pathBoot = Path.Combine(dirBoot, "world.bin");
        Debug.Log($"[BOOT] loadType={WorldLoadContext.loadType}, seed={WorldLoadContext.seed}, saveExists={File.Exists(pathBoot)}, path={pathBoot}");

        if (WorldLoadContext.loadType == WorldLoadContext.LoadType.NewWorld)
        {
            Debug.Log("[BOOT] NewWorld branch: Generate → SaveWorld()");
            worldMap = WorldDataGenerator.Generate(settings, WorldLoadContext.seed);

            int centerX = 2500;
            if (centerX < 0) centerX = 0;
            if (centerX >= W) centerX = W - 1;

            bool found = false;
            int spawnX = centerX;
            int spawnY = 0;

            for (int radius = 0; radius < W; radius++)
            {
                int[] xs = { centerX, centerX - radius, centerX + radius };

                foreach (int x in xs)
                {
                    if (found) break;
                    if (x < 0 || x >= W) continue;

                    for (int y = H - 1; y >= 0; y--)
                    {
                        ushort fgId = worldMap.GetFGId(x, y);
                        worldMap.GetFluidId(x, y, out byte waterAmount);

                        if (waterAmount > 0) break;

                        if (fgId != 0)
                        {
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

            LoadPlayerData();
            LoadEntities();
        }

        chunkSystem = new WorldChunkSystem(
            W,
            H,
            ChunkSize,
            ChunkRadius,
            maxLoadsPerFrame,
            worldMap,
            chunkPrefab,
            chunkRoot,
            RecalculateLightAt
        );
        chunkSystem.InitializePool(initialPoolSize);

        if (WorldLoadContext.loadType == WorldLoadContext.LoadType.NewWorld)
        {
            worldTick   = 0L;
            worldMinute = 12 * 60;
            worldHour   = 12;
            worldDay    = 0;
        }
        else
        {
            if (ticksPerDay > 0 && minutesPerDay > 0)
            {
                long day         = worldTick / ticksPerDay;
                long tickOfDay   = worldTick % ticksPerDay;
                int  ticksPerMin = ticksPerDay / minutesPerDay;

                int baseMinutes = 12 * 60;
                int minuteOfDay = baseMinutes + (ticksPerMin > 0 ? (int)(tickOfDay / ticksPerMin) : 0);

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

        ApplyTimeSyncedBrightness(forceDirty: true);
        chunkSystem.ResetLastPlayerChunk(player.position);
    }

    void Start()
    {
        ApplyLoadedPlayerAndInventory();
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
        chunkSystem.UpdateVisibleChunks(player.position, this);
    }

    void FixedUpdate()
    {
        StepTick();
        DoRandomTicks();

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

            ApplyTimeSyncedBrightness(forceDirty: false);
            var band = GetTimeBand();
        }

        ProcessArtificialLightQueues();

        chunkSystem.ProcessDirtyChunks();
    }

    IEnumerator AutosaveLoop()
    {
        var wait = new WaitForSecondsRealtime(300f);
        while (true)
        {
            yield return wait;
            SaveWorld();
        }
    }

    private void ApplyTimeSyncedBrightness(bool forceDirty)
    {
        int m = worldHour * 60 + (worldMinute % 60); // 0..1439
        float off =
            (m >= 300  && m < 540 ) ? 15f * (1f - (m - 300) / 240f) :
            (m >= 540  && m < 1080) ? 0f :
            (m >= 1080 && m < 1260) ? 15f * ((m - 1080) / 180f) :
                                       15f;

        byte newOffset = (byte)Mathf.RoundToInt(Mathf.Clamp(off, 0f, maxDarknessOffset));

        if (forceDirty || newOffset != globalBrightnessOffset)
        {
            globalBrightnessOffset = newOffset;

            if (newOffset != _lastBrightnessOffset || forceDirty)
            {
                _lastBrightnessOffset = newOffset;
                chunkSystem.SetGlobalBrightnessOffset(globalBrightnessOffset);
                chunkSystem.MarkAllChunksLightDirty();
            }
        }
    }

    public TimeBand GetTimeBand()
    {
        int h = worldHour;
        int mm = worldMinute % 60;
        int t = h * 100 + mm;

        if (t == 0)    return TimeBand.Midnight;
        if (t < 400)   return TimeBand.LateNight;
        if (t < 600)   return TimeBand.Dawn;
        if (t < 900)   return TimeBand.EarlyMorning;
        if (t < 1200)  return TimeBand.Morning;
        if (t == 1200) return TimeBand.Noon;
        if (t < 1700)  return TimeBand.Afternoon;
        if (t < 1900)  return TimeBand.Evening;
        if (t < 2100)  return TimeBand.Dusk;
        return TimeBand.Night;
    }

    /*────────────────────────────────────────────────────────────
     * Light
     *────────────────────────────────────────────────────────────*/
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
            if (worldMap.IsCollidable(x, y)) attenHere += 2;

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
                var lc = worldMap.light[x, y];
                lc.natural = best;
                worldMap.light[x, y] = lc;

                foreach (var (dx, dy) in dirs)
                {
                    int mx = x + dx, my = y + dy;
                    if ((uint)mx >= W || ((uint)my >= H)) continue;
                    q.Enqueue((mx, my));
                }

                MarkLightDirtyRect(x - 1, y - 1, 3, 3);
            }
        }
    }

    /*────────────────────────────────────────────────────────────
     * Chunk Dirty
     *────────────────────────────────────────────────────────────*/
    public void MarkChunkDirty(int worldX, int worldY, bool markFG, bool markBG = false, bool markDeco = false, bool markLiquid = false)
    {
        chunkSystem.MarkChunkDirty(worldX, worldY, markFG, markBG, markDeco, markLiquid);
    }

    public void MarkLightDirtyCell(int x, int y)
    {
        chunkSystem.MarkLightDirtyCell(x, y);
    }

    public void MarkLightDirtyCells(List<Vector2Int> cells)
    {
        chunkSystem.MarkLightDirtyCells(cells);
    }

    private void MarkLightDirtyRect(int x, int y, int w, int h)
    {
        chunkSystem.MarkLightDirtyRect(x, y, w, h);
    }

    /*────────────────────────────────────────────────────────────
     * Artificial Light (Increase / Decrease)
     *────────────────────────────────────────────────────────────*/
    private int GetArtCost(int nx, int ny)
    {
        int cost = ATT_AIR;
        if (worldMap.IsCollidable(nx, ny)) cost = ATT_FG;
        else if (worldMap.bg[nx, ny] != 0) cost = ATT_BG;
        return cost;
    }

    private void RecordLightChanged(int x, int y)
    {
        var p = new Vector2Int(x, y);
        if (_lightChangedSet.Add(p))
            _lightChangedList.Add(p);
    }

    private void RecordSeed(int x, int y)
    {
        var p = new Vector2Int(x, y);
        if (_seedSet.Add(p))
            _seedList.Add(p);
    }

    private void EnqueueIncrease(int x, int y, byte v)
    {
        if (v == 0) return;
        if ((uint)x >= W || (uint)y >= H) return;
        if (v > ART_MAX) v = ART_MAX;
        _incQ.Enqueue(new IncNode(x, y, v));
    }

    private void EnqueueDecrease(int x, int y, byte oldV)
    {
        if (oldV == 0) return;
        if ((uint)x >= W || (uint)y >= H) return;

        // 소스는 즉시 0
        var lc = worldMap.light[x, y];
        if (lc.artificial != 0)
        {
            lc.artificial = 0;
            worldMap.light[x, y] = lc;
            RecordLightChanged(x, y);
        }

        _decQ.Enqueue(new DecNode(x, y, oldV));
    }

    private void ProcessArtificialLightQueues()
    {
        if (_decQ.Count == 0 && _incQ.Count == 0) return;
        if (artificialLightOpsPerTick <= 0) return;

        _lightChangedSet.Clear();
        _lightChangedList.Clear();

        int ops = artificialLightOpsPerTick;

        // 규칙:
        // - Decrease가 남아있으면 Decrease만 처리(중간에 Increase 실행 금지)
        // - Decrease가 완전히 끝난 뒤에 seed들을 Increase 큐에 주입
        // - 그 다음 Increase 처리

        // 1) Decrease
        while (ops > 0 && _decQ.Count > 0)
        {
            ops--;

            var n = _decQ.Dequeue();
            int x = n.x, y = n.y;
            byte v = n.v;

            // Decrease 판정은 cost 기반이 아니라, "이웃이 현재값보다 어두우면(작으면) 영향권"으로 처리
            // - cur != 0 && cur < v  => 0으로 지우고 계속 감소
            // - cur >= v             => 다른 광원 영향 가능 -> seed로만 기록(Decrease 끝난 뒤 복구)
            int nx, ny;
            byte cur;

            // +x
            nx = x + 1; ny = y;
            if ((uint)nx < W)
            {
                cur = worldMap.light[nx, ny].artificial;
                if (cur != 0)
                {
                    if (cur < v)
                    {
                        var lc = worldMap.light[nx, ny];
                        lc.artificial = 0;
                        worldMap.light[nx, ny] = lc;
                        RecordLightChanged(nx, ny);
                        _decQ.Enqueue(new DecNode(nx, ny, cur));
                    }
                    else
                    {
                        RecordSeed(nx, ny);
                    }
                }
            }

            // -x
            nx = x - 1; ny = y;
            if (nx >= 0)
            {
                cur = worldMap.light[nx, ny].artificial;
                if (cur != 0)
                {
                    if (cur < v)
                    {
                        var lc = worldMap.light[nx, ny];
                        lc.artificial = 0;
                        worldMap.light[nx, ny] = lc;
                        RecordLightChanged(nx, ny);
                        _decQ.Enqueue(new DecNode(nx, ny, cur));
                    }
                    else
                    {
                        RecordSeed(nx, ny);
                    }
                }
            }

            // +y
            nx = x; ny = y + 1;
            if ((uint)ny < H)
            {
                cur = worldMap.light[nx, ny].artificial;
                if (cur != 0)
                {
                    if (cur < v)
                    {
                        var lc = worldMap.light[nx, ny];
                        lc.artificial = 0;
                        worldMap.light[nx, ny] = lc;
                        RecordLightChanged(nx, ny);
                        _decQ.Enqueue(new DecNode(nx, ny, cur));
                    }
                    else
                    {
                        RecordSeed(nx, ny);
                    }
                }
            }

            // -y
            nx = x; ny = y - 1;
            if (ny >= 0)
            {
                cur = worldMap.light[nx, ny].artificial;
                if (cur != 0)
                {
                    if (cur < v)
                    {
                        var lc = worldMap.light[nx, ny];
                        lc.artificial = 0;
                        worldMap.light[nx, ny] = lc;
                        RecordLightChanged(nx, ny);
                        _decQ.Enqueue(new DecNode(nx, ny, cur));
                    }
                    else
                    {
                        RecordSeed(nx, ny);
                    }
                }
            }
        }

        // 2) Decrease가 완전히 끝난 순간에만 seed -> Increase 주입
        if (_decQ.Count == 0 && _seedList.Count > 0)
        {
            for (int i = 0; i < _seedList.Count; i++)
            {
                var p = _seedList[i];
                if ((uint)p.x >= W || (uint)p.y >= H) continue;

                byte cur = worldMap.light[p.x, p.y].artificial;
                if (cur > 0)
                    EnqueueIncrease(p.x, p.y, cur);
            }
            _seedSet.Clear();
            _seedList.Clear();
        }

        // 3) Increase
        while (ops > 0 && _decQ.Count == 0 && _incQ.Count > 0)
        {
            ops--;

            var n = _incQ.Dequeue();
            int x = n.x, y = n.y;
            byte v = n.v;

            if ((uint)x >= W || (uint)y >= H) continue;

            var lc = worldMap.light[x, y];
            if (v <= lc.artificial) continue;

            lc.artificial = v;
            worldMap.light[x, y] = lc;
            RecordLightChanged(x, y);

            if (v <= 1) continue;

            int nx, ny;
            int cost;
            int nv;

            // +x
            nx = x + 1; ny = y;
            if ((uint)nx < W)
            {
                cost = GetArtCost(nx, ny);
                nv = v - cost;
                if (nv > 0 && nv > worldMap.light[nx, ny].artificial)
                    _incQ.Enqueue(new IncNode(nx, ny, (byte)nv));
            }

            // -x
            nx = x - 1; ny = y;
            if (nx >= 0)
            {
                cost = GetArtCost(nx, ny);
                nv = v - cost;
                if (nv > 0 && nv > worldMap.light[nx, ny].artificial)
                    _incQ.Enqueue(new IncNode(nx, ny, (byte)nv));
            }

            // +y
            nx = x; ny = y + 1;
            if ((uint)ny < H)
            {
                cost = GetArtCost(nx, ny);
                nv = v - cost;
                if (nv > 0 && nv > worldMap.light[nx, ny].artificial)
                    _incQ.Enqueue(new IncNode(nx, ny, (byte)nv));
            }

            // -y
            nx = x; ny = y - 1;
            if (ny >= 0)
            {
                cost = GetArtCost(nx, ny);
                nv = v - cost;
                if (nv > 0 && nv > worldMap.light[nx, ny].artificial)
                    _incQ.Enqueue(new IncNode(nx, ny, (byte)nv));
            }
        }

        if (_lightChangedList.Count > 0)
            MarkLightDirtyCells(_lightChangedList);
    }

    private void HandleArtificialChange(int x, int y, ushort oldId, ushort newId)
    {
        byte oldB = CellLibrary.BrightnessOf(oldId);
        byte newB = CellLibrary.BrightnessOf(newId);
        if (oldB == 0 && newB == 0) return;

        // "이 위치의 기존 인공광 값"을 감소 파동 기준값으로 사용
        byte oldV = 0;
        if ((uint)x < W && (uint)y < H)
            oldV = worldMap.light[x, y].artificial;

        // 제거/감소: 소스가 사라지거나 약해진 경우
        if (oldB > 0 && oldB >= newB)
        {
            if (oldV > 0)
                EnqueueDecrease(x, y, oldV);
        }

        // 추가/증가: 새 소스가 생기거나 더 밝아진 경우
        if (newB > 0)
        {
            EnqueueIncrease(x, y, newB);
        }
    }

    /*────────────────────────────────────────────────────────────
     * Save/Load
     *────────────────────────────────────────────────────────────*/
    public void SaveWorld()
    {
        WorldSaveSystem.SaveWorld(
            W,
            H,
            worldMap,
            worldTick,
            tickCurr,
            tickNext,
            playerComp,
            player,
            entityManager
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
        GameObject dropPrefab = itemDropper.droppedItemPrefab;

        WorldSaveSystem.LoadEntities(
            entityManager,
            itemLibrary,
            fallingBlockPrefab,
            dropPrefab,
            mobLibrary,
            corpseLibrary
        );
    }

    private void ApplyLoadedPlayerAndInventory()
    {
        if (!_hasLoadedPlayerData) return;

        var pos = player.position;
        player.position = new Vector3(_loadedPlayerPos.x, _loadedPlayerPos.y, pos.z);

        var slots = playerComp.Inventory.items;
        int n = Mathf.Min(slots.Count, _loadedInventory.Count);

        for (int i = 0; i < n; i++)
        {
            var data = _loadedInventory[i];
            slots[i] = (data != null && data.Count > 0) ? data : null;
        }

        for (int i = n; i < slots.Count; i++)
            slots[i] = null;

        playerComp.Inventory.NotifyChanged();
    }
}
