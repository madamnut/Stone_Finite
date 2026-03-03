// WorldManager.cs (전체 교체본 - 요청한 변경만 반영한 버전)
// ✅ 변경사항:
// - Utility Occupied 캐시/판정: "CogwheelOccupied"로 통일
// - BreakUtilityAt(): CogwheelOccupied면 파괴 불가 return 0
// - BreakUtilityAt(): 기어면 기존 로직 유지(네트워크 제거 + footprint 제거 + 드랍)
// - BreakUtilityAt(): 일반 유틸도 DT_Cell 기반 드랍 추가 (utility name 사용)
// - BreakBG(): VFX + DT_Cell 드랍 추가 (BG id는 Solid name 체계로 GetSolidName)
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WorldManager : MonoBehaviour
{
    public enum CellLayer { Solid, BG }

    public enum RelV { Neutral = 0, Up = 1, Down = 2 }
    public enum RelH { Neutral = 0, Left = 1, Right = 2 }

    [Header("월드 생성 설정")]
    public WorldGenSettings settings;

    [Header("Libraries")]
    public CellLibrary cellLibrary;
    public RecipeLibrary recipeLibrary;

    [Header("청크 Prefab & 관리")]
    public GameObject chunkPrefab;
    public Transform chunkRoot;
    public int initialPoolSize = 200;

    [Header("플레이어 및 렌더링 설정")]
    public Transform player;
    public Player playerComp;
    public int ChunkRadius = 7;
    public int maxLoadsPerFrame = 4;

    [Header("Falling Blocks")]
    public FallingBlock fallingBlockPrefab;

    [Header("Drops / VFX")]
    public ItemDropper itemDropper;
    public VfxManager vfx;

    [Header("Multiblock")]
    public MultiblockManager multiblockManager;
    private List<Multiblock.SaveData> _loadedMultiblocks;

    [Header("Corpse")]
    public CorpseLibrary corpseLibrary;

    [Header("엔티티 시스템")]
    public EntityManager entityManager;

    [Header("Mob")]
    public MobLibrary mobLibrary;

    [Header("아이템 라이브러리(인벤 복원용)")]
    public ItemLibrary itemLibrary;

    [Header("Gear Network")]
    public GearNetworkManager gearNetworkManager;

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
    private const int ATT_SOLID = 3;

    private int W, H;

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
    public int artificialLightOpsPerTick = 8000;

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

    // ✅ meta 규칙(고정)
    private const ushort META_DEFAULT = 0; // ✅ 일반 셀(부착 아님) fallback
    private const ushort META_BG = 1;
    private const ushort META_UP = 2;
    private const ushort META_DOWN = 3;
    private const ushort META_LEFT = 4;
    private const ushort META_RIGHT = 5;

    // ✅ Utility: CogwheelOccupied 캐시
    private ushort _utilityOccupiedId = 0;
    private static readonly Vector2Int[] _dirs4 = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    /*────────────────────────────────────────────────────────────
     * Read-only Query
     *────────────────────────────────────────────────────────────*/
    public bool InBounds(int x, int y) => worldMap.InBounds(x, y);

    public ushort GetSolidId(int x, int y)
    {
        if (!worldMap.InBounds(x, y)) return 0;
        return worldMap.GetSolid(x, y).id;
    }

    public ushort GetBGId(int x, int y)
    {
        if (!worldMap.InBounds(x, y)) return 0;
        return worldMap.GetBG(x, y);
    }

    public ushort GetFluidId(int x, int y, out byte amount)
    {
        if (!worldMap.InBounds(x, y)) { amount = 0; return 0; }
        var f = worldMap.GetFluid(x, y);
        amount = f.amount;
        return f.id;
    }

    // ✅ Utility
    public UtilityCell GetUtility(int x, int y)
    {
        if (!worldMap.InBounds(x, y)) return default;
        return worldMap.GetUtility(x, y);
    }

    public ushort GetUtilityId(int x, int y)
    {
        if (!worldMap.InBounds(x, y)) return 0;
        return worldMap.GetUtility(x, y).id;
    }

    public bool IsUtilityEmpty(int x, int y)
    {
        if (!worldMap.InBounds(x, y)) return false;
        return worldMap.GetUtility(x, y).id == 0;
    }

    // ✅ "물리적으로 막는가" (기존 의미 유지: collidable만)
    public bool IsCollidable(int x, int y)
    {
        if (!worldMap.InBounds(x, y)) return true;
        var s = worldMap.GetSolid(x, y);
        if (s.id == 0) return false;
        return (cellLibrary.GetSolidFlags(s.id) & CellLibrary.SolidFlags.Collidable) != 0;
    }

    // ✅ 설치/지지/부착 판정에서 "고정된 지지물"로 취급할 셀
    // - collidable OR platform
    private bool IsSupportSolid(int x, int y)
    {
        if (!worldMap.InBounds(x, y)) return false;

        var s = worldMap.GetSolid(x, y);
        if (s.id == 0) return false;

        var flags = cellLibrary.GetSolidFlags(s.id);
        if ((flags & CellLibrary.SolidFlags.Collidable) != 0) return true;

        return cellLibrary.IsPlatform(s.id);
    }

    private bool HasGravity(ushort solidId)
    {
        return (cellLibrary.GetSolidFlags(solidId) & CellLibrary.SolidFlags.HasGravity) != 0;
    }

    /*────────────────────────────────────────────────────────────
     * Tick + Light Recalc (통합)
     *────────────────────────────────────────────────────────────*/
    public void EnqTick(int x, int y)
    {
        if ((uint)x >= (uint)W || (uint)y >= (uint)H) return;

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
            StepAttachmentAt(p.x, p.y);
            StepGravityAt(p.x, p.y);
            StepFluidAt(p.x, p.y);
        }
        tickCurr.Clear();
    }

    //────────────────────────────────────────────
    // Attachment (연쇄 파괴)
    //────────────────────────────────────────────
    void StepAttachmentAt(int x, int y)
    {
        if (!worldMap.InBounds(x, y)) return;

        var s = worldMap.GetSolid(x, y);
        if (s.id == 0) return;

        if (!cellLibrary.GetAttachedAt(s.id, s.meta, out string attachedAt))
            return;

        if (attachedAt == "BG")
        {
            if (worldMap.GetBG(x, y) == 0)
                BreakSolid(x, y);
            return;
        }

        int sx = x;
        int sy = y;

        switch (attachedAt)
        {
            case "Down": sy = y - 1; break;
            case "Up": sy = y + 1; break;
            case "Left": sx = x - 1; break;
            case "Right": sx = x + 1; break;
            default:
                throw new System.Exception($"[Attachment] Unknown attachedAt='{attachedAt}' (solidId={s.id}, meta={s.meta})");
        }

        if (!worldMap.InBounds(sx, sy))
        {
            BreakSolid(x, y);
            return;
        }

        if (worldMap.GetSolid(sx, sy).id == 0)
        {
            BreakSolid(x, y);
        }
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

        // 기존 샘플 랜덤틱 로직 주석 유지
    }

    /*────────────────────────────────────────────────────────────
     * Fluid Simulation
     *────────────────────────────────────────────────────────────*/
    void StepFluidAt(int x, int y)
    {
        if (!worldMap.InBounds(x, y)) return;

        var l = worldMap.GetFluid(x, y);
        ushort fluidId = l.id;
        int amt = l.amount;

        if (amt <= 0)
        {
            if (fluidId != 0)
            {
                SetFluidInternal(x, y, 0, 0);
                OnCellEdited(x, y);
            }
            return;
        }
        if (fluidId == 0)
        {
            SetFluidInternal(x, y, 0, 0);
            OnCellEdited(x, y);
            return;
        }

        bool Blocked(int gx, int gy)
        {
            if (!worldMap.InBounds(gx, gy)) return true;
            return IsCollidable(gx, gy);
        }

        int dy = y - 1;
        if (dy >= 0 && !Blocked(x, dy))
        {
            var below = worldMap.GetFluid(x, dy);
            if (below.amount > 0 && below.id != 0 && below.id != fluidId)
                return;

            int belowAmt = below.amount;
            int cap = WorldData.MaxFluid - belowAmt;
            if (cap > 0)
            {
                int move = Mathf.Min(amt, cap);
                MoveFluidInternal(x, y, x, dy, fluidId, move);
                OnCellEdited(x, y);
                OnCellEdited(x, dy);
                return;
            }
        }

        int xl = x - 1, xr = x + 1;
        bool canL = xl >= 0 && !Blocked(xl, y);
        bool canR = xr < W && !Blocked(xr, y);

        int Al = 0, Ar = 0;

        if (canL)
        {
            var c = worldMap.GetFluid(xl, y);
            if (c.amount > 0 && c.id != 0 && c.id != fluidId) canL = false;
            else Al = c.amount;
        }
        if (canR)
        {
            var c = worldMap.GetFluid(xr, y);
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

        if (takeL > 0) MoveFluidInternal(x, y, xl, y, fluidId, takeL);
        if (takeR > 0) MoveFluidInternal(x, y, xr, y, fluidId, takeR);

        OnCellEdited(x, y);
        if (takeL > 0) OnCellEdited(xl, y);
        if (takeR > 0) OnCellEdited(xr, y);
    }

    void SetFluidInternal(int x, int y, ushort id, int newAmount)
    {
        var oldS = worldMap.GetSolid(x, y);
        ushort oldSolidId = oldS.id;
        ushort oldSolidMeta = oldS.meta;
        ushort oldFluidId = worldMap.GetFluid(x, y).id;

        newAmount = Mathf.Clamp(newAmount, 0, WorldData.MaxFluid);

        if (IsCollidable(x, y) || id == 0 || newAmount == 0)
        {
            worldMap.SetFluid(x, y, 0, 0);
        }
        else
        {
            worldMap.SetFluid(x, y, id, (byte)newAmount);
        }

        MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: true);
        HandleSourceLightChangeAt(x, y, oldSolidId, oldSolidMeta, oldFluidId);
    }

    void MoveFluidInternal(int fx, int fy, int tx, int ty, ushort id, int amount)
    {
        if (amount <= 0) return;

        var from = worldMap.GetFluid(fx, fy);
        var to = worldMap.GetFluid(tx, ty);

        if (from.amount <= 0 || from.id != id) return;
        if (to.amount > 0 && to.id != 0 && to.id != id) return;

        int fromAmt = from.amount;
        int toAmt = to.amount;

        int move = Mathf.Min(amount, fromAmt);
        move = Mathf.Min(move, WorldData.MaxFluid - toAmt);
        if (move <= 0) return;

        SetFluidInternal(fx, fy, id, fromAmt - move);
        SetFluidInternal(tx, ty, id, toAmt + move);
    }

    /*────────────────────────────────────────────────────────────
     * Gravity
     *────────────────────────────────────────────────────────────*/
    void StepGravityAt(int x, int y)
    {
        if (!worldMap.InBounds(x, y)) return;

        var s = worldMap.GetSolid(x, y);
        ushort id = s.id;
        if (id == 0) return;

        if (!HasGravity(id)) return;

        int by = y - 1;
        if (by < 0) return;

        if (worldMap.GetSolid(x, by).id != 0) return;

        ushort oldFluidId = worldMap.GetFluid(x, y).id;

        worldMap.SetSolid(x, y, 0, 0);

        MarkChunkDirty(x, y, markSolid: true);
        OnCellEdited(x, y);

        var pos = new Vector3(x + 0.5f, y + 0.5f, 0f);
        var spr = cellLibrary.GetSolidSprite(id, s.meta);

        var fb = Instantiate(fallingBlockPrefab, pos, Quaternion.identity);
        fb.Init(id, this, spr);

        entityManager.Register(fb);

        HandleSourceLightChangeAt(x, y, oldSolidId: id, oldSolidMeta: s.meta, oldFluidId: oldFluidId);
    }

    /*────────────────────────────────────────────────────────────
     * 설치 판정 헬퍼
     *────────────────────────────────────────────────────────────*/
    private bool HasAnyNeighborSupport_BGorSolid(int x, int y, bool solidMustBeCollidable)
    {
        bool Check(int nx, int ny)
        {
            if (!worldMap.InBounds(nx, ny)) return false;

            if (worldMap.GetBG(nx, ny) != 0) return true;

            ushort sid = worldMap.GetSolid(nx, ny).id;
            if (sid == 0) return false;

            if (!solidMustBeCollidable) return true;

            return IsSupportSolid(nx, ny);
        }

        if (Check(x - 1, y)) return true;
        if (Check(x + 1, y)) return true;
        if (Check(x, y - 1)) return true;
        if (Check(x, y + 1)) return true;

        return false;
    }

    private bool IsValidSupportForSolidAttach(int sx, int sy)
    {
        if (!worldMap.InBounds(sx, sy)) return false;

        if (worldMap.GetBG(sx, sy) != 0) return true;

        return IsSupportSolid(sx, sy);
    }

    private bool HasVariantMeta(ushort id, ushort meta)
    {
        return cellLibrary.HasSolidVariant(id, meta);
    }

    /*────────────────────────────────────────────────────────────
     * World Edit API (Utility)
     *────────────────────────────────────────────────────────────*/

    public bool SetUtilityExact(int x, int y, ushort id, ushort meta = 0)
    {
        if (!worldMap.InBounds(x, y)) return false;

        worldMap.SetUtility(x, y, id, meta);
        MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);
        return true;
    }

    public bool ClearUtilityExact(int x, int y)
    {
        if (!worldMap.InBounds(x, y)) return false;

        worldMap.SetUtility(x, y, 0, 0);
        MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);
        return true;
    }

    public bool IsUtilityAreaEmpty(Vector2Int center, IReadOnlyList<Vector2Int> offsets)
    {
        if (offsets == null || offsets.Count == 0) return false;

        for (int i = 0; i < offsets.Count; i++)
        {
            int x = center.x + offsets[i].x;
            int y = center.y + offsets[i].y;
            if (!worldMap.InBounds(x, y)) return false;

            if (worldMap.GetUtility(x, y).id != 0)
                return false;
        }

        return true;
    }

    public bool PlaceUtilityFootprint(
        Vector2Int center,
        ushort centerId,
        ushort centerMeta,
        ushort occupiedId,
        IReadOnlyList<Vector2Int> offsets
    )
    {
        if (centerId == 0) return false;
        if (offsets == null || offsets.Count == 0) return false;
        if (!IsUtilityAreaEmpty(center, offsets)) return false;

        for (int i = 0; i < offsets.Count; i++)
        {
            int x = center.x + offsets[i].x;
            int y = center.y + offsets[i].y;

            if (offsets[i].x == 0 && offsets[i].y == 0)
                worldMap.SetUtility(x, y, centerId, centerMeta);
            else
                worldMap.SetUtility(x, y, occupiedId, 0);

            MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);
        }

        return true;
    }

    public bool ClearUtilityFootprint(Vector2Int center, IReadOnlyList<Vector2Int> offsets)
    {
        if (offsets == null || offsets.Count == 0) return false;

        bool any = false;

        for (int i = 0; i < offsets.Count; i++)
        {
            int x = center.x + offsets[i].x;
            int y = center.y + offsets[i].y;
            if (!worldMap.InBounds(x, y)) continue;

            var u = worldMap.GetUtility(x, y);
            if (u.id == 0) continue;

            worldMap.SetUtility(x, y, 0, 0);
            MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);
            any = true;
        }

        return any;
    }

    // ✅ 유틸 파괴(기어 포함): 유틸 편집/오버스피드 파괴 공통 엔트리
    // 정책:
    // - CogwheelOccupied(점유 셀)는 클릭해도 무시(파괴 불가)
    // - 기어 파괴는 "센터 유틸"에서만 발생한다(센터를 파괴하면 footprint/네트워크/드랍 일괄)
    public ushort BreakUtilityAt(int x, int y)
    {
        if ((uint)x >= (uint)W || (uint)y >= (uint)H) return 0;

        CacheUtilityOccupiedIdIfNeeded();

        var u = worldMap.GetUtility(x, y);
        if (u.id == 0) return 0;

        // CogwheelOccupied는 파괴 불가
        if (_utilityOccupiedId != 0 && u.id == _utilityOccupiedId)
            return 0;

        var cell = new Vector2Int(x, y);

        // 1) 기어 센터라면: 네트워크 제거 + footprint 제거 + 드랍(기어/소스/벨트)
        if (gearNetworkManager != null && gearNetworkManager.IsGearOccupiedCell(cell))
        {
            ushort centerUtilityId = u.id;
            ushort centerUtilityMeta = u.meta;

            string droppedSourceId = null;
            List<GearNetworkManager.BeltDrop> droppedBelts = null;

            if (!gearNetworkManager.TryRemoveGearAt(cell, out droppedSourceId, out droppedBelts))
                return 0;

            ClearGearFootprintUtility(cell);

            if (vfx != null && cellLibrary != null)
            {
                var spr = cellLibrary.GetUtilitySprite(centerUtilityId, centerUtilityMeta);
                vfx.EmitBlockAtCell(spr, x, y, 1, grid: 3, count: -1);
            }

            var pos3 = new Vector3(x + 0.5f, y + 0.5f, 0f);

            // 드랍: 기어 본체(센터 유틸 name)
            if (itemDropper != null && cellLibrary != null)
            {
                string gearItemId = cellLibrary.GetUtilityName(centerUtilityId);
                if (!string.IsNullOrEmpty(gearItemId))
                    itemDropper.SpawnDroppedItems(gearItemId, pos3);
            }

            // 드랍: 소스(있으면 1개)
            if (itemDropper != null && !string.IsNullOrEmpty(droppedSourceId) && itemLibrary != null)
            {
                var srcData = itemLibrary.Create(droppedSourceId, 1);
                if (srcData != null)
                    itemDropper.SpawnDroppedItem(srcData, pos3);
            }

            // 드랍: 벨트(기존: material id로 변환)
            if (itemDropper != null && droppedBelts != null && droppedBelts.Count > 0 && itemLibrary != null)
            {
                for (int i = 0; i < droppedBelts.Count; i++)
                {
                    var bd = droppedBelts[i];
                    if (string.IsNullOrEmpty(bd.beltKind) || bd.count <= 0) continue;

                    if (gearNetworkManager.TryGetBeltMaterialItemId(bd.beltKind, out var matId) &&
                        !string.IsNullOrEmpty(matId))
                    {
                        var beltMat = itemLibrary.Create(matId, bd.count);
                        if (beltMat != null)
                            itemDropper.SpawnDroppedItem(beltMat, pos3);
                    }
                }
            }

            return centerUtilityId;
        }

        // 2) 일반 유틸: 1칸 제거 + VFX + DT_Cell 드랍(utility name key)
        worldMap.SetUtility(x, y, 0, 0);
        MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);

        if (vfx != null && cellLibrary != null)
        {
            var spr = cellLibrary.GetUtilitySprite(u.id, u.meta);
            vfx.EmitBlockAtCell(spr, x, y, 1, grid: 3, count: -1);
        }

        if (itemDropper != null && cellLibrary != null)
        {
            string key = cellLibrary.GetUtilityName(u.id);
            if (!string.IsNullOrEmpty(key))
            {
                var pos3 = new Vector3(x + 0.5f, y + 0.5f, 0f);
                itemDropper.SpawnDroppedItems(key, pos3);
            }
        }

        return u.id;
    }

    void ClearGearFootprintUtility(Vector2Int center)
    {
        CacheUtilityOccupiedIdIfNeeded();

        if (worldMap.InBounds(center.x, center.y))
        {
            worldMap.SetUtility(center.x, center.y, 0, 0);
            MarkChunkDirty(center.x, center.y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);
        }

        if (_utilityOccupiedId == 0) return;

        for (int i = 0; i < _dirs4.Length; i++)
        {
            var p = center + _dirs4[i];
            if (!worldMap.InBounds(p.x, p.y)) continue;

            var u = worldMap.GetUtility(p.x, p.y);
            if (u.id != _utilityOccupiedId) continue;

            worldMap.SetUtility(p.x, p.y, 0, 0);
            MarkChunkDirty(p.x, p.y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);
        }
    }

    void CacheUtilityOccupiedIdIfNeeded()
    {
        if (_utilityOccupiedId != 0) return;
        if (cellLibrary == null) return;

        if (cellLibrary.TryGetUtilityIdByName("CogwheelOccupied", out var occ))
            _utilityOccupiedId = occ;
    }

    /*────────────────────────────────────────────────────────────
     * World Edit API (기존 Solid/BG/Fluid)
     *────────────────────────────────────────────────────────────*/
    public void OverwriteSolid(int x, int y, ushort newId, ushort newMeta = 0)
    {
        var cur = worldMap.GetSolid(x, y);
        ushort oldSolidId = cur.id;
        ushort oldSolidMeta = cur.meta;
        ushort oldFluidId = worldMap.GetFluid(x, y).id;

        worldMap.SetSolid(x, y, newId, newMeta);

        if ((cellLibrary.GetSolidFlags(newId) & CellLibrary.SolidFlags.Collidable) != 0)
            worldMap.SetFluid(x, y, 0, 0);

        MarkChunkDirty(x, y, markSolid: true);
        OnCellEdited(x, y);
        HandleSourceLightChangeAt(x, y, oldSolidId, oldSolidMeta, oldFluidId);
    }

    public bool PlaceSolid(int x, int y, ushort id)
        => PlaceSolid(x, y, id, RelV.Neutral, RelH.Neutral);

    private bool PlaceSolidAtEmpty(int x, int y, ushort id, RelV relV, RelH relH)
    {
        if (!worldMap.InBounds(x, y)) return false;
        if (id == 0) return false;

        var curS = worldMap.GetSolid(x, y);
        if (curS.id != 0) return false;

        bool hasBgHere = worldMap.GetBG(x, y) != 0;

        if (!hasBgHere)
        {
            if (!HasAnyNeighborSupport_BGorSolid(x, y, solidMustBeCollidable: true))
                return false;
        }

        var candidates = new List<ushort>(5);

        if (hasBgHere && HasVariantMeta(id, META_BG))
            candidates.Add(META_BG);

        void Add(ushort first, ushort second)
        {
            if (HasVariantMeta(id, first)) candidates.Add(first);
            if (HasVariantMeta(id, second)) candidates.Add(second);
        }

        if (relH == RelH.Left) Add(META_LEFT, META_RIGHT);
        else if (relH == RelH.Right) Add(META_RIGHT, META_LEFT);
        else Add(META_LEFT, META_RIGHT);

        if (relV == RelV.Up) Add(META_UP, META_DOWN);
        else if (relV == RelV.Down) Add(META_DOWN, META_UP);
        else Add(META_DOWN, META_UP);

        var seen = new HashSet<ushort>();
        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            if (!seen.Add(candidates[i]))
                candidates.RemoveAt(i);
        }

        ushort chosenMeta = 0;
        bool found = false;

        bool HasSupportFor(ushort m)
        {
            int sx = x, sy = y;

            switch (m)
            {
                case META_UP: sy = y + 1; break;
                case META_DOWN: sy = y - 1; break;
                case META_LEFT: sx = x - 1; break;
                case META_RIGHT: sx = x + 1; break;
                default: return false;
            }

            return IsValidSupportForSolidAttach(sx, sy);
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            ushort m = candidates[i];

            if (m == META_BG)
            {
                chosenMeta = META_BG;
                found = true;
                break;
            }

            if (!HasSupportFor(m))
                continue;

            chosenMeta = m;
            found = true;
            break;
        }

        if (!found)
        {
            if (HasVariantMeta(id, META_DEFAULT))
            {
                chosenMeta = META_DEFAULT;
                found = true;
            }
        }

        if (!found) return false;

        ushort oldSolidId = 0;
        ushort oldSolidMeta = 0;
        ushort oldFluidId = worldMap.GetFluid(x, y).id;

        worldMap.SetSolid(x, y, id, chosenMeta);

        if ((cellLibrary.GetSolidFlags(id) & CellLibrary.SolidFlags.Collidable) != 0)
            worldMap.SetFluid(x, y, 0, 0);

        MarkChunkDirty(x, y, markSolid: true);
        OnCellEdited(x, y);

        HandleSourceLightChangeAt(x, y, oldSolidId, oldSolidMeta, oldFluidId);
        return true;
    }

    public bool PlaceSolid(int x, int y, ushort id, RelV relV, RelH relH)
    {
        if (!worldMap.InBounds(x, y)) return false;
        if (id == 0) return false;

        var curS = worldMap.GetSolid(x, y);

        if (curS.id != 0)
        {
            if (!IsSupportSolid(x, y))
                return false;

            bool TryNeighbor(int nx, int ny, RelV nRelV, RelH nRelH)
            {
                if (!worldMap.InBounds(nx, ny)) return false;
                if (worldMap.GetSolid(nx, ny).id != 0) return false;

                return PlaceSolidAtEmpty(nx, ny, id, nRelV, nRelH);
            }

            if (relH == RelH.Left)
            {
                if (TryNeighbor(x + 1, y, RelV.Neutral, RelH.Left)) return true;
                if (TryNeighbor(x - 1, y, RelV.Neutral, RelH.Right)) return true;
            }
            else if (relH == RelH.Right)
            {
                if (TryNeighbor(x - 1, y, RelV.Neutral, RelH.Right)) return true;
                if (TryNeighbor(x + 1, y, RelV.Neutral, RelH.Left)) return true;
            }
            else
            {
                if (TryNeighbor(x - 1, y, RelV.Neutral, RelH.Right)) return true;
                if (TryNeighbor(x + 1, y, RelV.Neutral, RelH.Left)) return true;
            }

            if (relV == RelV.Up)
            {
                if (TryNeighbor(x, y - 1, RelV.Up, RelH.Neutral)) return true;
                if (TryNeighbor(x, y + 1, RelV.Down, RelH.Neutral)) return true;
            }
            else if (relV == RelV.Down)
            {
                if (TryNeighbor(x, y + 1, RelV.Down, RelH.Neutral)) return true;
                if (TryNeighbor(x, y - 1, RelV.Up, RelH.Neutral)) return true;
            }
            else
            {
                if (TryNeighbor(x, y - 1, RelV.Up, RelH.Neutral)) return true;
                if (TryNeighbor(x, y + 1, RelV.Down, RelH.Neutral)) return true;
            }

            return false;
        }

        return PlaceSolidAtEmpty(x, y, id, relV, relH);
    }

    public bool PlaceSolidExact(int x, int y, ushort id)
    {
        if (!worldMap.InBounds(x, y)) return false;
        if (id == 0) return false;

        if (worldMap.GetSolid(x, y).id != 0) return false;

        ushort oldFluidId = worldMap.GetFluid(x, y).id;

        worldMap.SetSolid(x, y, id, 0);

        if ((cellLibrary.GetSolidFlags(id) & CellLibrary.SolidFlags.Collidable) != 0)
            worldMap.SetFluid(x, y, 0, 0);

        MarkChunkDirty(x, y, markSolid: true);
        OnCellEdited(x, y);

        HandleSourceLightChangeAt(x, y, oldSolidId: 0, oldSolidMeta: 0, oldFluidId: oldFluidId);
        return true;
    }

    public bool PlaceFluid(int x, int y, ushort fluidId, byte amount)
    {
        if (!worldMap.InBounds(x, y)) return false;
        if (fluidId == 0 || amount == 0) return false;

        var oldS = worldMap.GetSolid(x, y);
        ushort oldSolidId = oldS.id;
        ushort oldSolidMeta = oldS.meta;
        ushort oldFluidId = worldMap.GetFluid(x, y).id;

        if (IsCollidable(x, y)) return false;

        var cur = worldMap.GetFluid(x, y);

        if (cur.id != 0 && cur.amount > 0 && cur.id != fluidId)
            return false;

        int current = cur.amount;
        int space = WorldData.MaxFluid - current;
        if (space <= 0) return false;

        int insert = (amount <= space) ? amount : space;
        int newAmt = current + insert;

        worldMap.SetFluid(x, y, fluidId, (byte)newAmt);

        MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: true);
        OnCellEdited(x, y);

        HandleSourceLightChangeAt(x, y, oldSolidId, oldSolidMeta, oldFluidId);
        return insert > 0;
    }

    public bool PlaceBG(int x, int y, ushort id)
        => PlaceBG(x, y, id, RelV.Neutral, RelH.Neutral);

    public bool PlaceBG(int x, int y, ushort id, RelV relV, RelH relH)
    {
        if (!worldMap.InBounds(x, y)) return false;
        if (id == 0) return false;

        if (worldMap.GetSolid(x, y).id != 0) return false;
        if (worldMap.GetBG(x, y) != 0) return false;

        if (!HasAnyNeighborSupport_BGorSolid(x, y, solidMustBeCollidable: false))
            return false;

        worldMap.SetBG(x, y, id);

        MarkChunkDirty(x, y, markSolid: false, markBG: true, markLiquid: false);
        OnCellEdited(x, y);
        return true;
    }

    public ushort BreakSolid(int x, int y)
    {
        if ((uint)x >= (uint)W || (uint)y >= (uint)H) return 0;

        var s = worldMap.GetSolid(x, y);
        ushort oldSolidId = s.id;
        ushort oldMeta = s.meta;
        if (oldSolidId == 0) return 0;

        ushort oldFluidId = worldMap.GetFluid(x, y).id;

        worldMap.SetSolid(x, y, 0, 0);

        MarkChunkDirty(x, y, markSolid: true);
        OnCellEdited(x, y);

        HandleSourceLightChangeAt(x, y, oldSolidId, oldMeta, oldFluidId);

        string key = cellLibrary.GetSolidName(oldSolidId);

        if (!string.IsNullOrEmpty(key))
        {
            var pos3 = new Vector3(x + 0.5f, y + 0.5f, 0f);

            if (vfx != null)
            {
                var spr = cellLibrary.GetSolidSprite(oldSolidId, oldMeta);
                vfx.EmitBlockAtCell(spr, x, y, 1, grid: 3, count: -1);
            }

            if (itemDropper != null)
                itemDropper.SpawnDroppedItems(key, pos3);
        }

        return oldSolidId;
    }

    public FluidCell BreakFluid(int x, int y)
    {
        if ((uint)x >= (uint)W || (uint)y >= (uint)H) return default;

        var oldS = worldMap.GetSolid(x, y);
        ushort oldSolidId = oldS.id;
        ushort oldSolidMeta = oldS.meta;

        var removed = worldMap.GetFluid(x, y);
        ushort oldFluidId = removed.id;

        if (removed.id == 0 || removed.amount == 0) return removed;

        worldMap.SetFluid(x, y, 0, 0);

        MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: true);
        OnCellEdited(x, y);

        HandleSourceLightChangeAt(x, y, oldSolidId, oldSolidMeta, oldFluidId);
        return removed;
    }

    public ushort BreakBG(int x, int y)
    {
        if ((uint)x >= (uint)W || (uint)y >= (uint)H) return 0;

        ushort removed = worldMap.GetBG(x, y);
        if (removed == 0) return 0;

        worldMap.SetBG(x, y, 0);

        MarkChunkDirty(x, y, markSolid: false, markBG: true, markLiquid: false);
        OnCellEdited(x, y);

        // ✅ VFX + DT_Cell 드랍
        if (cellLibrary != null)
        {
            string key = cellLibrary.GetSolidName(removed);
            if (!string.IsNullOrEmpty(key))
            {
                var pos3 = new Vector3(x + 0.5f, y + 0.5f, 0f);

                if (vfx != null)
                {
                    var spr = cellLibrary.GetSolidSprite(removed, 0);
                    vfx.EmitBlockAtCell(spr, x, y, 1, grid: 2, count: -1);
                }

                if (itemDropper != null)
                    itemDropper.SpawnDroppedItems(key, pos3);
            }
        }

        return removed;
    }

    public bool PlaceCell(int x, int y, ushort id) => PlaceSolid(x, y, id);
    public bool PlaceBgCell(int x, int y, ushort id) => PlaceBG(x, y, id);

    public ushort BreakCell(int x, int y, CellLayer layer)
    {
        return layer == CellLayer.Solid ? BreakSolid(x, y) : BreakBG(x, y);
    }

    /*────────────────────────────────────────────────────────────
     * Lifecycle / Light / SaveLoad ... (아래는 기존 유지)
     *────────────────────────────────────────────────────────────*/

    void Awake()
    {
        W = settings.width;
        H = settings.height;

        tickCurr.Clear();
        tickNext.Clear();

        CacheUtilityOccupiedIdIfNeeded();

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
                        ushort solidId = worldMap.GetSolid(x, y).id;
                        var f = worldMap.GetFluid(x, y);
                        byte waterAmount = f.amount;

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
            if (!LoadWorldFromDisk(out worldMap, out _loadedMultiblocks))
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

        if (multiblockManager != null)
        {
            if (WorldLoadContext.loadType == WorldLoadContext.LoadType.LoadWorld)
                multiblockManager.LoadFromSaveDatas(_loadedMultiblocks);
            else
                multiblockManager.LoadFromSaveDatas(null);
        }
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
        if (gearNetworkManager != null)
        {
            gearNetworkManager.TickSources();
            gearNetworkManager.TickNetworks();
        }

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

    public void RecalculateLightAt(int x0, int y0)
    {
        if ((uint)x0 >= (uint)W || (uint)y0 >= (uint)H) return;

        var q = new Queue<(int x, int y)>();
        q.Enqueue((x0, y0));

        (int dx, int dy)[] dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();

            ushort oldN16 = worldMap.GetNaturalLight(x, y);
            byte oldN = (byte)Mathf.Clamp((int)oldN16, 0, NAT_MAX);

            int attenHere = 0;
            if (worldMap.GetBG(x, y) != 0) attenHere += 1;
            if (IsCollidable(x, y)) attenHere += 2;

            byte best = 0;
            foreach (var (dx, dy) in dirs)
            {
                int nx = x + dx, ny = y + dy;
                if ((uint)nx >= (uint)W || (uint)ny >= (uint)H) continue;

                int nNat = (int)worldMap.GetNaturalLight(nx, ny);
                int cand = nNat - attenHere;
                if (cand > best) best = (byte)Mathf.Clamp(cand, 0, NAT_MAX);
            }

            if (best != oldN)
            {
                worldMap.SetNaturalLight(x, y, best);

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

    public void MarkChunkDirty(int worldX, int worldY, bool markSolid, bool markBG = false, bool markLiquid = false, bool markUtility = false)
    {
        chunkSystem.MarkChunkDirty(worldX, worldY, markSolid, markBG, markLiquid, markUtility);
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

    private int GetArtCost(int nx, int ny)
    {
        int cost = ATT_AIR;
        if (IsCollidable(nx, ny)) cost = ATT_SOLID;
        else if (worldMap.GetBG(nx, ny) != 0) cost = ATT_BG;
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

        ushort cur = worldMap.GetArtificialLight(x, y);
        if (cur != 0)
        {
            worldMap.SetArtificialLight(x, y, 0);
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
                cur = (byte)Mathf.Clamp((int)worldMap.GetArtificialLight(nx, ny), 0, ART_MAX);
                if (cur != 0)
                {
                    if (cur < v)
                    {
                        worldMap.SetArtificialLight(nx, ny, 0);
                        RecordLightChanged(nx, ny);
                        _decQ.Enqueue(new DecNode(nx, ny, cur));
                    }
                    else RecordSeed(nx, ny);
                }
            }

            nx = x - 1; ny = y;
            if (nx >= 0)
            {
                cur = (byte)Mathf.Clamp((int)worldMap.GetArtificialLight(nx, ny), 0, ART_MAX);
                if (cur != 0)
                {
                    if (cur < v)
                    {
                        worldMap.SetArtificialLight(nx, ny, 0);
                        RecordLightChanged(nx, ny);
                        _decQ.Enqueue(new DecNode(nx, ny, cur));
                    }
                    else RecordSeed(nx, ny);
                }
            }

            nx = x; ny = y + 1;
            if ((uint)ny < (uint)H)
            {
                cur = (byte)Mathf.Clamp((int)worldMap.GetArtificialLight(nx, ny), 0, ART_MAX);
                if (cur != 0)
                {
                    if (cur < v)
                    {
                        worldMap.SetArtificialLight(nx, ny, 0);
                        RecordLightChanged(nx, ny);
                        _decQ.Enqueue(new DecNode(nx, ny, cur));
                    }
                    else RecordSeed(nx, ny);
                }
            }

            nx = x; ny = y - 1;
            if (ny >= 0)
            {
                cur = (byte)Mathf.Clamp((int)worldMap.GetArtificialLight(nx, ny), 0, ART_MAX);
                if (cur != 0)
                {
                    if (cur < v)
                    {
                        worldMap.SetArtificialLight(nx, ny, 0);
                        RecordLightChanged(nx, ny);
                        _decQ.Enqueue(new DecNode(nx, ny, cur));
                    }
                    else RecordSeed(nx, ny);
                }
            }
        }

        if (_decQ.Count == 0 && _seedList.Count > 0)
        {
            for (int i = 0; i < _seedList.Count; i++)
            {
                var p = _seedList[i];
                if ((uint)p.x >= (uint)W || (uint)p.y >= (uint)H) continue;

                byte cur = (byte)Mathf.Clamp((int)worldMap.GetArtificialLight(p.x, p.y), 0, ART_MAX);
                if (cur > 0) EnqueueIncrease(p.x, p.y, cur);
            }
            _seedSet.Clear();
            _seedList.Clear();
        }

        while (ops > 0 && _decQ.Count == 0 && _incQ.Count > 0)
        {
            ops--;

            var n = _incQ.Dequeue();
            int x = n.x, y = n.y;
            byte v = n.v;

            if ((uint)x >= (uint)W || (uint)y >= (uint)H) continue;

            byte curA = (byte)Mathf.Clamp((int)worldMap.GetArtificialLight(x, y), 0, ART_MAX);
            if (v <= curA) continue;

            worldMap.SetArtificialLight(x, y, v);
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
                byte nCur = (byte)Mathf.Clamp((int)worldMap.GetArtificialLight(nx, ny), 0, ART_MAX);
                if (nv > 0 && nv > nCur)
                    _incQ.Enqueue(new IncNode(nx, ny, (byte)nv));
            }

            nx = x - 1; ny = y;
            if (nx >= 0)
            {
                cost = GetArtCost(nx, ny);
                nv = v - cost;
                byte nCur = (byte)Mathf.Clamp((int)worldMap.GetArtificialLight(nx, ny), 0, ART_MAX);
                if (nv > 0 && nv > nCur)
                    _incQ.Enqueue(new IncNode(nx, ny, (byte)nv));
            }

            nx = x; ny = y + 1;
            if ((uint)ny < (uint)H)
            {
                cost = GetArtCost(nx, ny);
                nv = v - cost;
                byte nCur = (byte)Mathf.Clamp((int)worldMap.GetArtificialLight(nx, ny), 0, ART_MAX);
                if (nv > 0 && nv > nCur)
                    _incQ.Enqueue(new IncNode(nx, ny, (byte)nv));
            }

            nx = x; ny = y - 1;
            if (ny >= 0)
            {
                cost = GetArtCost(nx, ny);
                nv = v - cost;
                byte nCur = (byte)Mathf.Clamp((int)worldMap.GetArtificialLight(nx, ny), 0, ART_MAX);
                if (nv > 0 && nv > nCur)
                    _incQ.Enqueue(new IncNode(nx, ny, (byte)nv));
            }
        }

        if (_lightChangedList.Count > 0)
            MarkLightDirtyCells(_lightChangedList);
    }

    private byte GetSourceBrightness(ushort solidId, ushort solidMeta, ushort fluidId)
    {
        byte sb = cellLibrary.GetSolidBrightness(solidId, solidMeta);
        byte lb = cellLibrary.GetFluidBrightness(fluidId);
        return (sb >= lb) ? sb : lb;
    }

    private void HandleSourceLightChangeAt(int x, int y, ushort oldSolidId, ushort oldSolidMeta, ushort oldFluidId)
    {
        if ((uint)x >= (uint)W || (uint)y >= (uint)H) return;

        var newS = worldMap.GetSolid(x, y);
        ushort newSolidId = newS.id;
        ushort newSolidMeta = newS.meta;
        ushort newFluidId = worldMap.GetFluid(x, y).id;

        byte oldB = GetSourceBrightness(oldSolidId, oldSolidMeta, oldFluidId);
        byte newB = GetSourceBrightness(newSolidId, newSolidMeta, newFluidId);

        if (oldB == 0 && newB == 0) return;

        byte oldV = (byte)Mathf.Clamp((int)worldMap.GetArtificialLight(x, y), 0, ART_MAX);

        if (oldB > 0 && oldB >= newB)
        {
            if (oldV > 0) EnqueueDecrease(x, y, oldV);
        }

        if (newB > 0)
        {
            EnqueueIncrease(x, y, newB);
        }
    }

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
            entityManager,
            multiblockManager
        );
    }

    bool LoadWorldFromDisk(out WorldData loaded, out List<Multiblock.SaveData> multiblocks)
    {
        int w, h;
        long loadedTick;

        bool ok = WorldSaveSystem.LoadWorldFromDisk(
            out loaded,
            out w,
            out h,
            out loadedTick,
            tickCurr,
            tickNext,
            out multiblocks
        );
        if (ok)
        {
            W = w;
            H = h;
            worldTick = loadedTick;
        }
        else
        {
            multiblocks = null;
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