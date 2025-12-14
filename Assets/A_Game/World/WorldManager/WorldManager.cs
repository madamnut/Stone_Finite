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

    [Header("Cell Library")]
    public CellLibrary cellLibrary;

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
    public VfxManager vfx;

    [Header("Corpse")]
    public CorpseLibrary corpseLibrary;

    [Header("엔티티 시스템")]
    public EntityManager entityManager;

    [Header("Mob")]
    public MobLibrary mobLibrary;

    [Header("아이템 라이브러리(인벤 복원용)")]
    public ItemLibrary itemLibrary;

    [Header("Time Settings")]
    public int ticksPerSecond = 20;
    public int minutesPerDay = 24 * 60;
    public int ticksPerDay = 28800;

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
    private const int ATT_BG = 2;
    private const int ATT_FG = 3;

    private int W, H;

    // 외부에서 직접 만지지 않도록 숨김
    private WorldData worldMap;

    private WorldChunkSystem chunkSystem;

    public long worldTick;
    public int worldMinute;
    public int worldHour;
    public int worldDay;
    private long _lastLoggedSecondTick = -1;

    private HashSet<Vector2Int> tickCurr = new();
    private HashSet<Vector2Int> tickNext = new();

    private bool _didQuitSave = false;

    private bool _hasLoadedPlayerData = false;
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

    private readonly HashSet<Vector2Int> _seedSet = new();
    private readonly List<Vector2Int> _seedList = new();

    private readonly HashSet<Vector2Int> _lightChangedSet = new();
    private readonly List<Vector2Int> _lightChangedList = new();

    /*────────────────────────────────────────────────────────────
     * Read-only Query (외부 조회는 여기로만)
     *────────────────────────────────────────────────────────────*/
    public bool InBounds(int x, int y) => worldMap.InBounds(x, y);
    public ushort GetFGId(int x, int y) => worldMap.GetSolidId(x, y);
    public ushort GetBGId(int x, int y) => worldMap.GetBGId(x, y);
    public ushort GetFluidId(int x, int y, out byte amount) => worldMap.GetLiquidId(x, y, out amount);
    public bool IsCollidable(int x, int y) => worldMap.IsCollidable(x, y);

    /*────────────────────────────────────────────────────────────
     * Tick + Light Recalc (통합)
     *────────────────────────────────────────────────────────────*/
    public void EnqTick(int x, int y)
    {
        if ((uint)x >= (uint)W || (uint)y >= (uint)H) return;

        // 새로 추가된 셀에 대해서만 라이트 재계산(중복 폭발 방지)
        if (tickNext.Add(new Vector2Int(x, y)))
        {
            RecalculateLightAt(x, y);
        }
    }

    public void OnCellEdited(int gx, int gy)
    {
        if ((uint)gx >= (uint)W || (uint)gy >= (uint)H) return;

        EnqTick(gx, gy);
        EnqTick(gx + 1, gy);
        EnqTick(gx - 1, gy);
        EnqTick(gx, gy + 1);
        EnqTick(gx, gy - 1);
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

            ushort solidId = worldMap.GetSolidId(gx, gy);
            if (solidId != 0)
            {
                string nm = cellLibrary.GetSolidName(solidId);
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
     * Fluid Simulation (WorldData.liquid 기반)
     *────────────────────────────────────────────────────────────*/
    void StepFluidAt(int x, int y)
    {
        if (!worldMap.InBounds(x, y)) return;

        var l = worldMap.liquid[x, y];
        ushort fluidId = l.id;
        int amt = l.amount;

        // 정합성 정리
        if (amt <= 0)
        {
            if (fluidId != 0)
            {
                SetLiquidInternal(x, y, 0, 0);
                OnCellEdited(x, y);
            }
            return;
        }
        if (fluidId == 0)
        {
            SetLiquidInternal(x, y, 0, 0);
            OnCellEdited(x, y);
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
            var below = worldMap.liquid[x, dy];

            // 다른 유체 혼합 금지
            if (below.amount > 0 && below.id != 0 && below.id != fluidId)
                return;

            int belowAmt = below.amount;
            int cap = WorldData.MaxFluid - belowAmt;
            if (cap > 0)
            {
                int move = Mathf.Min(amt, cap);
                MoveLiquidInternal(x, y, x, dy, fluidId, move);
                OnCellEdited(x, y);
                OnCellEdited(x, dy);
                return;
            }
        }

        // 2) 좌우
        int xl = x - 1, xr = x + 1;
        bool canL = xl >= 0 && !Blocked(xl, y);
        bool canR = xr < W && !Blocked(xr, y);

        int Al = 0, Ar = 0;

        if (canL)
        {
            var c = worldMap.liquid[xl, y];
            if (c.amount > 0 && c.id != 0 && c.id != fluidId) canL = false;
            else Al = c.amount;
        }
        if (canR)
        {
            var c = worldMap.liquid[xr, y];
            if (c.amount > 0 && c.id != 0 && c.id != fluidId) canR = false;
            else Ar = c.amount;
        }

        int capL = canL ? (WorldData.MaxFluid - Al) : 0;
        int capR = canR ? (WorldData.MaxFluid - Ar) : 0;

        int flowL = 0, flowR = 0;

        if (canL)
        {
            int diff = amt - Al;
            if (diff > 0)
            {
                int prop = Mathf.Clamp(Mathf.Max(1, diff / 2), 1, 20);
                flowL = Mathf.Min(prop, capL);
            }
        }
        if (canR)
        {
            int diff = amt - Ar;
            if (diff > 0)
            {
                int prop = Mathf.Clamp(Mathf.Max(1, diff / 2), 1, 20);
                flowR = Mathf.Min(prop, capR);
            }
        }

        int want = flowL + flowR;
        if (want <= 0) return;

        int total = Mathf.Min(amt, want);

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
        else takeR = Mathf.Min(total, flowR);

        if (takeL > 0) MoveLiquidInternal(x, y, xl, y, fluidId, takeL);
        if (takeR > 0) MoveLiquidInternal(x, y, xr, y, fluidId, takeR);

        OnCellEdited(x, y);
        if (takeL > 0) OnCellEdited(xl, y);
        if (takeR > 0) OnCellEdited(xr, y);
    }

    void SetLiquidInternal(int x, int y, ushort id, int newAmount)
    {
        // (요구사항) 액체 밝기도 광원으로 취급: 변경 시 artificial 파동 갱신
        ushort oldSolidId = worldMap.GetSolidId(x, y);
        ushort oldLiquidId = worldMap.liquid[x, y].id;

        newAmount = Mathf.Clamp(newAmount, 0, WorldData.MaxFluid);

        if (newAmount == 0)
        {
            worldMap.ForceLiquid(x, y, new LiquidCell { id = 0, amount = 0, brightness = 0 });
        }
        else
        {
            byte b = cellLibrary.GetLiquidBrightness(id);
            worldMap.ForceLiquid(x, y, new LiquidCell
            {
                id = id,
                amount = (byte)newAmount,
                brightness = b
            });
        }

        MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: true);
        HandleSourceLightChangeAt(x, y, oldSolidId, oldLiquidId);
    }

    void MoveLiquidInternal(int fx, int fy, int tx, int ty, ushort id, int amount)
    {
        if (amount <= 0) return;

        var from = worldMap.liquid[fx, fy];
        var to = worldMap.liquid[tx, ty];

        if (from.amount <= 0 || from.id != id) return;
        if (to.amount > 0 && to.id != 0 && to.id != id) return;

        int fromAmt = from.amount;
        int toAmt = to.amount;

        int move = Mathf.Min(amount, fromAmt);
        move = Mathf.Min(move, WorldData.MaxFluid - toAmt);
        if (move <= 0) return;

        SetLiquidInternal(fx, fy, id, fromAmt - move);
        SetLiquidInternal(tx, ty, id, toAmt + move);
    }

    /*────────────────────────────────────────────────────────────
     * Gravity (WorldData.solid 기반)
     *────────────────────────────────────────────────────────────*/
    void StepGravityAt(int x, int y)
    {
        if (!worldMap.InBounds(x, y)) return;

        var s = worldMap.solid[x, y];
        ushort id = s.id;
        if (id == 0) return;

        bool hasGravity = (s.flags & SolidFlags.HasGravity) != 0;
        if (!hasGravity) return;

        int by = y - 1;
        if (by < 0) return;

        if (worldMap.GetSolidId(x, by) != 0) return;

        ushort oldLiquidId = worldMap.liquid[x, y].id;

        ushort removedId = worldMap.RemoveSolid(x, y);
        if (removedId == 0) return;

        MarkChunkDirty(x, y, markSolid: true);
        OnCellEdited(x, y);

        var pos = new Vector3(x + 0.5f, y + 0.5f, 0f);
        var spr = cellLibrary.GetSolidSprite(id);

        var fb = Instantiate(fallingBlockPrefab, pos, Quaternion.identity);
        fb.Init(id, this, spr);

        entityManager.Register(fb);

        HandleSourceLightChangeAt(x, y, oldSolidId: id, oldLiquidId: oldLiquidId);
    }

    /*────────────────────────────────────────────────────────────
     * World Edit API
     *────────────────────────────────────────────────────────────*/

    // ───────── 설치(Solid) ─────────
    public bool PlaceFG(int x, int y, ushort id)
    {
        if (!worldMap.InBounds(x, y)) return false;
        if (id == 0) return false;

        ushort oldSolidId = worldMap.GetSolidId(x, y);
        ushort oldLiquidId = worldMap.liquid[x, y].id;

        SolidCell src = cellLibrary.MakeSolidCell(id);
        bool ok = worldMap.TryPlaceSolid(x, y, in src);
        if (!ok) return false;

        MarkChunkDirty(x, y, markSolid: true);
        OnCellEdited(x, y);

        HandleSourceLightChangeAt(x, y, oldSolidId, oldLiquidId);
        return true;
    }

    // ───────── 설치(Liquid) ─────────
    public bool PlaceFluid(int x, int y, ushort fluidId, byte amount)
    {
        if (!worldMap.InBounds(x, y)) return false;
        if (fluidId == 0 || amount == 0) return false;

        ushort oldSolidId = worldMap.GetSolidId(x, y);
        ushort oldLiquidId = worldMap.liquid[x, y].id;

        LiquidCell src = cellLibrary.MakeLiquidCell(fluidId, amount);
        bool ok = worldMap.TryPlaceLiquid(x, y, in src, out byte leftover);

        int inserted = amount - leftover;
        if (inserted <= 0) return false;

        MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: true);
        OnCellEdited(x, y);

        HandleSourceLightChangeAt(x, y, oldSolidId, oldLiquidId);
        return ok;
    }

    // ───────── 설치(BG) ─────────
    public bool PlaceBG(int x, int y, ushort id)
    {
        if (!worldMap.InBounds(x, y)) return false;
        if (id == 0) return false;

        ushort oldId = worldMap.bg[x, y];
        if (oldId == id) return false;

        worldMap.ForceBG(x, y, id);

        MarkChunkDirty(x, y, markSolid: false, markBG: true, markLiquid: false);
        OnCellEdited(x, y);
        return true;
    }

    // ───────── 파괴(Solid) ─────────
    public ushort BreakFG(int x, int y)
    {
        if ((uint)x >= (uint)W || (uint)y >= (uint)H) return 0;

        ushort oldSolidId = worldMap.GetSolidId(x, y);
        if (oldSolidId == 0) return 0;

        ushort oldLiquidId = worldMap.liquid[x, y].id;

        ushort removed = worldMap.RemoveSolid(x, y);
        if (removed == 0) return 0;

        MarkChunkDirty(x, y, markSolid: true);
        OnCellEdited(x, y);

        HandleSourceLightChangeAt(x, y, oldSolidId, oldLiquidId);

        // (수정) GetSolidKey -> GetSolidName
        string key = cellLibrary.GetSolidName(removed);
        if (!string.IsNullOrEmpty(key))
        {
            var pos3 = new Vector3(x + 0.5f, y + 0.5f, 0f);
            vfx.EmitBlockAtCell(key, x, y, 1, grid: 3, count: -1);
            itemDropper.SpawnDroppedItems(key, pos3);
        }

        return removed;
    }

    // ───────── 파괴(Liquid) ─────────
    public LiquidCell BreakFluid(int x, int y)
    {
        if ((uint)x >= (uint)W || (uint)y >= (uint)H) return default;

        ushort oldSolidId = worldMap.GetSolidId(x, y);
        ushort oldLiquidId = worldMap.liquid[x, y].id;

        var removed = worldMap.RemoveLiquid(x, y);
        if (removed.id == 0 || removed.amount == 0) return removed;

        MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: true);
        OnCellEdited(x, y);

        HandleSourceLightChangeAt(x, y, oldSolidId, oldLiquidId);
        return removed;
    }

    // ───────── 파괴(BG) ─────────
    public ushort BreakBG(int x, int y)
    {
        if ((uint)x >= (uint)W || (uint)y >= (uint)H) return 0;

        ushort removed = worldMap.RemoveBG(x, y);
        if (removed == 0) return 0;

        MarkChunkDirty(x, y, markSolid: false, markBG: true, markLiquid: false);
        OnCellEdited(x, y);
        return removed;
    }

    // ───────── 기존 API 유지(호환용) ─────────
    public bool PlaceCell(int x, int y, ushort id) => PlaceFG(x, y, id);
    public bool PlaceBgCell(int x, int y, ushort id) => PlaceBG(x, y, id);

    public ushort BreakCell(int x, int y, CellLayer layer)
    {
        return layer == CellLayer.FG ? BreakFG(x, y) : BreakBG(x, y);
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

        string dirBoot = WorldLoadContext.GetSavePath();
        string pathBoot = Path.Combine(dirBoot, "world.bin");
        Debug.Log($"[BOOT] loadType={WorldLoadContext.loadType}, seed={WorldLoadContext.seed}, saveExists={File.Exists(pathBoot)}, path={pathBoot}");

        if (WorldLoadContext.loadType == WorldLoadContext.LoadType.NewWorld)
        {
            Debug.Log("[BOOT] NewWorld branch: Generate → SaveWorld()");
            worldMap = WorldDataGenerator.Generate(settings, WorldLoadContext.seed, cellLibrary);

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
                        ushort solidId = worldMap.GetSolidId(x, y);
                        worldMap.GetLiquidId(x, y, out byte waterAmount);

                        if (waterAmount > 0) break;

                        if (solidId != 0)
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
            cellLibrary,
            RecalculateLightAt
        );
        chunkSystem.InitializePool(initialPoolSize);

        if (WorldLoadContext.loadType == WorldLoadContext.LoadType.NewWorld)
        {
            worldTick = 0L;
            worldMinute = 12 * 60;
            worldHour = 12;
            worldDay = 0;
        }
        else
        {
            if (ticksPerDay > 0 && minutesPerDay > 0)
            {
                long day = worldTick / ticksPerDay;
                long tickOfDay = worldTick % ticksPerDay;
                int ticksPerMin = ticksPerDay / minutesPerDay;

                int baseMinutes = 12 * 60;
                int minuteOfDay = baseMinutes + (ticksPerMin > 0 ? (int)(tickOfDay / ticksPerMin) : 0);
                minuteOfDay %= minutesPerDay;

                worldDay = (int)day;
                worldMinute = minuteOfDay;
                worldHour = worldMinute / 60;
            }
            else
            {
                worldDay = 0;
                worldMinute = 0;
                worldHour = 0;
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
        int m = worldHour * 60 + (worldMinute % 60);
        float off =
            (m >= 300 && m < 540) ? 15f * (1f - (m - 300) / 240f) :
            (m >= 540 && m < 1080) ? 0f :
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

        if (t == 0) return TimeBand.Midnight;
        if (t < 400) return TimeBand.LateNight;
        if (t < 600) return TimeBand.Dawn;
        if (t < 900) return TimeBand.EarlyMorning;
        if (t < 1200) return TimeBand.Morning;
        if (t == 1200) return TimeBand.Noon;
        if (t < 1700) return TimeBand.Afternoon;
        if (t < 1900) return TimeBand.Evening;
        if (t < 2100) return TimeBand.Dusk;
        return TimeBand.Night;
    }

    /*────────────────────────────────────────────────────────────
     * Light (Natural)
     *────────────────────────────────────────────────────────────*/
    public void RecalculateLightAt(int x0, int y0)
    {
        if ((uint)x0 >= (uint)W || (uint)y0 >= (uint)H) return;

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
                if ((uint)nx >= (uint)W || (uint)ny >= (uint)H) continue;

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
                    if ((uint)mx >= (uint)W || (uint)my >= (uint)H) continue;
                    q.Enqueue((mx, my));
                }

                MarkLightDirtyRect(x - 1, y - 1, 3, 3);
            }
        }
    }

    /*────────────────────────────────────────────────────────────
     * Chunk Dirty
     *────────────────────────────────────────────────────────────*/
    public void MarkChunkDirty(int worldX, int worldY, bool markSolid, bool markBG = false, bool markLiquid = false)
    {
        chunkSystem.MarkChunkDirty(worldX, worldY, markSolid, markBG, markLiquid);
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
     * └ 광원값 = max(솔리드 brightness, 리퀴드 brightness)
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
        if ((uint)x >= (uint)W || (uint)y >= (uint)H) return;
        if (v > ART_MAX) v = ART_MAX;
        _incQ.Enqueue(new IncNode(x, y, v));
    }

    private void EnqueueDecrease(int x, int y, byte oldV)
    {
        if (oldV == 0) return;
        if ((uint)x >= (uint)W || (uint)y >= (uint)H) return;

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

        // 1) Decrease
        while (ops > 0 && _decQ.Count > 0)
        {
            ops--;

            var n = _decQ.Dequeue();
            int x = n.x, y = n.y;
            byte v = n.v;

            int nx, ny;
            byte cur;

            nx = x + 1; ny = y;
            if ((uint)nx < (uint)W)
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
                    else RecordSeed(nx, ny);
                }
            }

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
                    else RecordSeed(nx, ny);
                }
            }

            nx = x; ny = y + 1;
            if ((uint)ny < (uint)H)
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
                    else RecordSeed(nx, ny);
                }
            }

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
                    else RecordSeed(nx, ny);
                }
            }
        }

        // 2) seed -> Increase
        if (_decQ.Count == 0 && _seedList.Count > 0)
        {
            for (int i = 0; i < _seedList.Count; i++)
            {
                var p = _seedList[i];
                if ((uint)p.x >= (uint)W || (uint)p.y >= (uint)H) continue;

                byte cur = worldMap.light[p.x, p.y].artificial;
                if (cur > 0) EnqueueIncrease(p.x, p.y, cur);
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

            if ((uint)x >= (uint)W || (uint)y >= (uint)H) continue;

            var lc = worldMap.light[x, y];
            if (v <= lc.artificial) continue;

            lc.artificial = v;
            worldMap.light[x, y] = lc;
            RecordLightChanged(x, y);

            if (v <= 1) continue;

            int nx, ny;
            int cost;
            int nv;

            nx = x + 1; ny = y;
            if ((uint)nx < (uint)W)
            {
                cost = GetArtCost(nx, ny);
                nv = v - cost;
                if (nv > 0 && nv > worldMap.light[nx, ny].artificial)
                    _incQ.Enqueue(new IncNode(nx, ny, (byte)nv));
            }

            nx = x - 1; ny = y;
            if (nx >= 0)
            {
                cost = GetArtCost(nx, ny);
                nv = v - cost;
                if (nv > 0 && nv > worldMap.light[nx, ny].artificial)
                    _incQ.Enqueue(new IncNode(nx, ny, (byte)nv));
            }

            nx = x; ny = y + 1;
            if ((uint)ny < (uint)H)
            {
                cost = GetArtCost(nx, ny);
                nv = v - cost;
                if (nv > 0 && nv > worldMap.light[nx, ny].artificial)
                    _incQ.Enqueue(new IncNode(nx, ny, (byte)nv));
            }

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

    private byte GetSourceBrightness(ushort solidId, ushort liquidId)
    {
        byte sb = cellLibrary.GetSolidBrightness(solidId);
        byte lb = cellLibrary.GetLiquidBrightness(liquidId);
        return (sb >= lb) ? sb : lb;
    }

    // old(솔리드/리퀴드) -> now(솔리드/리퀴드) 의 max 밝기 변화로 artificial 파동 갱신
    private void HandleSourceLightChangeAt(int x, int y, ushort oldSolidId, ushort oldLiquidId)
    {
        if ((uint)x >= (uint)W || (uint)y >= (uint)H) return;

        ushort newSolidId = worldMap.GetSolidId(x, y);
        ushort newLiquidId = worldMap.liquid[x, y].id;

        byte oldB = GetSourceBrightness(oldSolidId, oldLiquidId);
        byte newB = GetSourceBrightness(newSolidId, newLiquidId);

        if (oldB == 0 && newB == 0) return;

        byte oldV = worldMap.light[x, y].artificial;

        if (oldB > 0 && oldB >= newB)
        {
            if (oldV > 0) EnqueueDecrease(x, y, oldV);
        }

        if (newB > 0)
        {
            EnqueueIncrease(x, y, newB);
        }
    }

    /*────────────────────────────────────────────────────────────
     * Save/Load (호출부는 유지)
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
