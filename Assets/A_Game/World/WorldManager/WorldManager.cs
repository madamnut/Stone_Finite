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

    private const ushort ID_WATER = 60000;

    [Header("Global Brightness Offset (auto by time) 0=밝음, 15=어두움")]
    [Range(0,15)] public byte globalBrightnessOffset = 0;
    private byte _lastBrightnessOffset = 255;

    private const int ATT_AIR = 1;
    private const int ATT_BG  = 2;
    private const int ATT_FG  = 3;

    private int W, H;
    public WorldData worldMap;

    public List<MultiblockInstanceBase> multiblocks = new();
    public Dictionary<Vector2Int, MultiblockInstanceBase> multiblockByCell = new();

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
        if (!worldMap.InBounds(x, y)) return;

        ref var cell = ref worldMap.fg[x, y];
        int Wc = cell.fluidAmount;

        if (Wc <= 0)
        {
            if (cell.fluidId != 0)
            {
                cell.fluidId     = 0;
                cell.fluidAmount = 0;
                MarkChunkDirty(x, y, markFG: true);
            }
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
            int Wd = below.fluidAmount;
            const int MaxFluid = 128;
            int cap = MaxFluid - Wd;
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

        int Wl = 0, Wr = 0;
        if (canL) Wl = worldMap.fg[xl, y].fluidAmount;
        if (canR) Wr = worldMap.fg[xr, y].fluidAmount;

        const int MaxAmt = 128;
        int capL = canL ? (MaxAmt - Wl) : 0;
        int capR = canR ? (MaxAmt - Wr) : 0;

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
        if (!worldMap.InBounds(x, y)) return;

        ref var cell = ref worldMap.fg[x, y];
        int cur = cell.fluidAmount;

        newAmount = Mathf.Clamp(newAmount, 0, 128);
        if (cur == newAmount) return;

        cell.fluidAmount = (byte)newAmount;
        cell.fluidId     = (ushort)(newAmount > 0 ? ID_WATER : 0);

        MarkChunkDirty(x, y, markFG: true);
    }

    //──────────────── Gravity FallingBlock 스폰 경로 수정됨
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

        if (fallingBlockPrefab != null)
        {
            var pos = new Vector3(x + 0.5f, y + 0.5f, 0f);
            var spr = CellLibrary.GetSprite(id);

            // FallingBlock 인스턴스 생성
            var fb = Instantiate(fallingBlockPrefab, pos, Quaternion.identity);
            fb.Init(id, this, spr);

            // 엔티티 등록
            if (entityManager != null)
                entityManager.Register(fb);
        }
    }


    // ───────── 설치 (FG) ─────────
    public bool PlaceCell(int x, int y, ushort id)
    {
        if (!worldMap.InBounds(x, y)) return false;
        if (id == 0) return false;

        // 기존 FG id (인공광 변화 감지용)
        ushort oldId = worldMap.GetFGId(x, y);

        // 유체 배치
        if (id == ID_WATER)
        {
            const byte amount = 128; // 가득 채우기 기준
            worldMap.TryPlaceFluid(x, y, ID_WATER, amount, out byte leftover);
            int inserted = amount - leftover;
            if (inserted <= 0) return false;

            MarkChunkDirty(x, y, markFG: true);
            EnqTick(x, y);
            EnqTick(x - 1, y); EnqTick(x + 1, y);
            EnqTick(x, y + 1); if (y > 0) EnqTick(x, y - 1);
            return true;
        }

        // 일반 FG 배치
        var src = CellLibrary.MakeFgCell(id);
        bool ok = worldMap.TryPlaceFG(x, y, in src);
        if (!ok) return false;

        MarkChunkDirty(x, y, markFG: true);
        OnCellEditedFG(x, y);

        HandleArtificialChange(x, y, oldId, id);
        return true;
    }

    // ───────── 설치 (BG) ─────────
    public bool PlaceBgCell(int x, int y, ushort id)
    {
        if (!worldMap.InBounds(x, y)) return false;
        if (id == 0) return false;

        ushort oldId = worldMap.bg[x, y];
        if (oldId == id) return false; // 이미 동일 셀인 경우 생략

        worldMap.bg[x, y] = id;

        MarkChunkDirty(x, y, markFG: false, markBG: true);
        RecalculateLightAt(x, y);
        return true;
    }

    // ───────── 파괴 ─────────
    public ushort BreakCell(int x, int y, CellLayer layer)
    {
        if ((uint)x >= W || (uint)y >= H) return 0;

        switch (layer)
        {
            case CellLayer.FG:
            {
                var pos = new Vector2Int(x, y);

                // ───────── 멀티블럭 여부 먼저 확인 ─────────
                if (multiblockByCell.TryGetValue(pos, out MultiblockInstanceBase inst))
                {
                    // 1) 클릭한 파츠 제거 + 드랍/이펙트
                    ushort removed = worldMap.RemoveFG(x, y);
                    if (removed != 0)
                    {
                        MarkChunkDirty(x, y, markFG: true);
                        OnCellEditedFG(x, y);
                        HandleArtificialChange(x, y, removed, 0);

                        string key = CellLibrary.GetKey(removed);
                        if (!string.IsNullOrEmpty(key))
                        {
                            var dropPos = new Vector3(x + 0.5f, y + 0.5f, 0f);
                            if (vfx != null)
                                vfx.EmitBlockAtCell(key, x, y, 1, grid: 3, count: -1);
                            if (itemDropper != null)
                                itemDropper.SpawnDroppedItems(key, dropPos);
                        }
                    }

                    // 2) 기본 블럭(Mud) ID 찾기
                    ushort baseId = 0;
                    for (ushort id = 1; id < ushort.MaxValue; id++)
                    {
                        var nm = CellLibrary.GetName(id);
                        if (!string.IsNullOrEmpty(nm) && nm == "Mud")
                        {
                            baseId = id;
                            break;
                        }
                    }

                    if (baseId == 0)
                    {
                        Debug.LogWarning("[MBUILD] BreakCell: 'Mud' 셀 ID를 찾지 못했습니다. 멀티블럭 롤백 생략.");
                    }
                    else
                    {
                        // 3) 멀티블럭이 차지하던 셀 중 클릭한 칸 제외하고 Mud로 롤백
                        foreach (var cellPos in inst.occupiedCells)
                        {
                            if (cellPos == pos) continue;

                            int wx = cellPos.x;
                            int wy = cellPos.y;
                            if (!worldMap.InBounds(wx, wy)) continue;

                            ushort oldId2 = worldMap.GetFGId(wx, wy);

                            var src = CellLibrary.MakeFgCell(baseId);
                            worldMap.ForceFG(wx, wy, in src);

                            MarkChunkDirty(wx, wy, markFG: true);
                            OnCellEditedFG(wx, wy);
                            HandleArtificialChange(wx, wy, oldId2, baseId);
                        }
                    }

                    // 4) 멀티블럭 매핑 제거
                    foreach (var cellPos in inst.occupiedCells)
                        multiblockByCell.Remove(cellPos);

                    multiblocks.Remove(inst);

                    // 5) 훅
                    inst.OnPartBroken(this, pos);

                    return removed;
                }

                // ───────── 일반 단일 FG 블럭 파괴 ─────────
                {
                    ushort removed = worldMap.RemoveFG(x, y);
                    if (removed == 0) return 0;

                    MarkChunkDirty(x, y, markFG: true);
                    OnCellEditedFG(x, y);

                    HandleArtificialChange(x, y, removed, 0);

                    string key = CellLibrary.GetKey(removed);
                    if (!string.IsNullOrEmpty(key))
                    {
                        var pos3 = new Vector3(x + 0.5f, y + 0.5f, 0f);
                        if (vfx != null)
                            vfx.EmitBlockAtCell(key, x, y, 1, grid: 3, count: -1);
                        if (itemDropper != null)
                            itemDropper.SpawnDroppedItems(key, pos3);
                    }
                    return removed;
                }
            }

            case CellLayer.BG:
            {
                ushort removed = worldMap.RemoveBG(x, y);
                if (removed == 0) return 0;

                MarkChunkDirty(x, y, markFG: false, markBG: true);
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

        // 엔티티 시스템 초기 설정 (플레이어/청크 정보 주입)
        if (entityManager != null)
        {
            if (entityManager.player == null && player != null)
                entityManager.player = player;

            entityManager.chunkSize       = ChunkSize;
            entityManager.loadChunkRadius = ChunkRadius;
        }

        // ItemDropper에 EntityManager/Library 자동 주입(있을 때)
        if (itemDropper != null)
        {
            if (itemDropper.entityManager == null)
                itemDropper.entityManager = entityManager;
            if (itemDropper.itemLibrary == null)
                itemDropper.itemLibrary = itemLibrary;
        }

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

                        // 위에서 아래로 스캔하면서 본체/유체 중 먼저 만나는 것 판단
                        for (int y = H - 1; y >= 0; y--)
                        {
                            ushort fgId = worldMap.GetFGId(x, y);
                            worldMap.GetFluidId(x, y, out byte waterAmount);

                            if (waterAmount > 0)
                            {
                                // 액체가 먼저 나오면 이 x 전체 스킵
                                break;
                            }

                            if (fgId != 0)
                            {
                                // 본체가 먼저 나왔으므로 스폰 지점은 y + 5
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

        // 청크 시스템 생성 및 초기화
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

        // Player 컴포넌트 자동 보정
        if (playerComp == null && player != null)
        {
            playerComp = player.GetComponent<Player>();
            if (playerComp == null) playerComp = player.GetComponentInParent<Player>();
            if (playerComp == null) playerComp = player.GetComponentInChildren<Player>();
        }
        if (playerComp == null)
            Debug.LogWarning("WorldManager: Player 컴포넌트를 찾지 못했습니다. 인벤토리 저장/로드가 비활성화됩니다.");

        // 월드 시간 초기화/복원
        if (WorldLoadContext.loadType == WorldLoadContext.LoadType.NewWorld)
        {
            worldTick   = 0L;
            worldMinute = 12 * 60; // 720 = 12:00
            worldHour   = 12;
            worldDay    = 0;
        }
        else
        {
            if (ticksPerDay > 0 && minutesPerDay > 0)
            {
                long day       = worldTick / ticksPerDay;
                long tickOfDay = worldTick % ticksPerDay;
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

        ApplyTimeSyncedBrightness(forceDirty:true);

        if (player != null && chunkSystem != null)
            chunkSystem.ResetLastPlayerChunk(player.position);
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
        if (player != null && chunkSystem != null)
            chunkSystem.UpdateVisibleChunks(player.position, this);

        // 디버그: P 키로 Cow 스폰
        if (Input.GetKeyDown(KeyCode.P))
            SpawnCowNearPlayer();
    }

    void FixedUpdate()
    {
        // 월드 시뮬레이션 (물/중력)
        StepTick();

        // 월드 시간 진행
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

        // 청크/라이트 더티 처리
        if (chunkSystem != null)
            chunkSystem.ProcessDirtyChunks();
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
            (m >= 300  && m < 540 ) ? 15f * (1f - (m - 300) / 240f) :
            (m >= 540  && m < 1080) ? 0f :
            (m >= 1080 && m < 1260) ? 15f * ((m - 1080) / 180f) :
                                       15f;
        byte newOffset = (byte)Mathf.RoundToInt(Mathf.Clamp(off, 0f, 15f));

        if (forceDirty || newOffset != globalBrightnessOffset)
        {
            globalBrightnessOffset = newOffset;

            if ((newOffset != _lastBrightnessOffset || forceDirty) && chunkSystem != null)
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

    // ───────── 라이트 재계산 ─────────
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
                var cell = worldMap.light[x, y];
                cell.natural = best;
                worldMap.light[x, y] = cell;

                foreach (var (dx, dy) in dirs)
                {
                    int mx = x + dx, my = y + dy;
                    if ((uint)mx >= W || ((uint)my >= H)) continue;
                    q.Enqueue((mx, my));
                }

                if (chunkSystem != null)
                    MarkLightDirtyRect(x - 1, y - 1, 3, 3);
            }
        }
    }

    // ───────── 청크 더티 플래그 위임 ─────────
    public void MarkChunkDirty(int worldX, int worldY, bool markFG, bool markBG = false, bool markDeco = false, bool markLiquid = false)
    {
        if (chunkSystem == null) return;
        chunkSystem.MarkChunkDirty(worldX, worldY, markFG, markBG, markDeco, markLiquid);
    }

    private void MarkLightDirtyRect(int x, int y, int w, int h)
    {
        if (chunkSystem == null) return;
        chunkSystem.MarkLightDirtyRect(x, y, w, h);
    }

    // ───────── 인공광(스칼라) ─────────
    private void HandleArtificialChange(int x, int y, ushort oldId, ushort newId)
    {
        byte oldB = CellLibrary.BrightnessOf(oldId);
        byte newB = CellLibrary.BrightnessOf(newId);
        if (oldB == 0 && newB == 0) return;

        int r  = Mathf.Max(oldB, newB);
        int x0 = Mathf.Max(0, x - r);
        int y0 = Mathf.Max(0, y - r);
        int x1 = Mathf.Min(W - 1, x + r);
        int y1 = Mathf.Min(H - 1, y + r);

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
            ushort id = worldMap.GetFGId(xx, yy);
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
                if (worldMap.IsCollidable(nx, ny))      cost = ATT_FG;
                else if (worldMap.bg[nx, ny] != 0)      cost = ATT_BG;

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
        if (entityManager == null)
        {
            Debug.LogWarning("[WorldManager] EntityManager 가 없어 엔티티 로드를 건너뜁니다.");
            return;
        }

        GameObject dropPrefab = null;
        if (itemDropper != null)
            dropPrefab = itemDropper.droppedItemPrefab;

        WorldSaveSystem.LoadEntities(
            entityManager,
            itemLibrary,
            fallingBlockPrefab,
            dropPrefab,
            mobLibrary
        );
    }

    private void ApplyLoadedPlayerAndInventory()
    {
        if (!_hasLoadedPlayerData || player == null) return;

        var pos = player.position;
        player.position = new Vector3(_loadedPlayerPos.x, _loadedPlayerPos.y, pos.z);

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

        for (int i = n; i < slots.Count; i++)
            slots[i] = null;

        pComp.Inventory.NotifyChanged();
    }

    // ───────── 멀티블럭 생성 헬퍼 (MudFurnace 전용) ─────────
    public MudFurnaceInstance CreateMudFurnaceInstance(
        MultiblockLibrary.Definition def,
        int originX,
        int originY)
    {
        var inst = new MudFurnaceInstance
        {
            defKey  = def.key,
            originX = originX,
            originY = originY,
            width   = def.width,
            height  = def.height
        };

        inst.occupiedCells.Clear();

        for (int px = 0; px < def.width; px++)
        {
            for (int py = 0; py < def.height; py++)
            {
                string cellName = def.pattern[px, py];
                if (string.IsNullOrEmpty(cellName)) continue;

                int wx = originX + px;
                int wy = originY + py;
                if (!worldMap.InBounds(wx, wy)) continue;

                var pos = new Vector2Int(wx, wy);
                inst.occupiedCells.Add(pos);
                multiblockByCell[pos] = inst;
            }
        }

        inst.instanceId = multiblocks.Count;
        multiblocks.Add(inst);

        Debug.Log(
            $"[MBUILD] MudFurnaceInstance 생성: def='{def.key}', origin=({originX},{originY}), " +
            $"id={inst.instanceId}, cells={inst.occupiedCells.Count}"
        );

        for (int px = 0; px < def.width; px++)
        {
            for (int py = 0; py < def.height; py++)
            {
                string resultName = def.result[px, py];
                if (string.IsNullOrEmpty(resultName)) continue;

                int wx = originX + px;
                int wy = originY + py;
                if (!worldMap.InBounds(wx, wy)) continue;

                ushort placeId = 0;
                for (ushort id = 1; id < ushort.MaxValue; id++)
                {
                    var nm = CellLibrary.GetName(id);
                    if (!string.IsNullOrEmpty(nm) && nm == resultName)
                    {
                        placeId = id;
                        break;
                    }
                }
                if (placeId == 0)
                {
                    Debug.LogWarning($"[MBUILD] resultName='{resultName}' 에 해당하는 셀 ID를 찾지 못했습니다.");
                    continue;
                }

                ushort oldId = worldMap.GetFGId(wx, wy);

                var src = CellLibrary.MakeFgCell(placeId);
                worldMap.ForceFG(wx, wy, in src);

                MarkChunkDirty(wx, wy, markFG: true);
                OnCellEditedFG(wx, wy);
                HandleArtificialChange(wx, wy, oldId, placeId);
            }
        }

        return inst;
    }

    // ───────── Mob 디버그 스폰 ─────────
    private void SpawnCowNearPlayer()
    {
        if (mobLibrary == null || entityManager == null || player == null)
        {
            Debug.LogWarning("[WorldManager] Cow 스폰 실패: mobLibrary / entityManager / player 참조가 없습니다.");
            return;
        }

        Vector3 spawnPos = player.position + new Vector3(1f, 0f, 0f);
        var mob = mobLibrary.SpawnMob("Cow", spawnPos, entityManager);
        if (mob == null)
            Debug.LogWarning("[WorldManager] Cow 스폰 실패: MobLibrary에서 'Cow' 프리팹을 찾지 못했거나 생성 실패.");
    }
}
