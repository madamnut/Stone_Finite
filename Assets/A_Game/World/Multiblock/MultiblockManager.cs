using System;
using System.Collections.Generic;
using UnityEngine;

public class MultiblockManager : MonoBehaviour
{
    const string LOG_MB = "[MBLOCK]";

    [Header("Deps")]
    public WorldManager world;

    [SerializeField] ItemLibrary itemLibrary;
    public ItemLibrary ItemLibrary => itemLibrary;

    [Header("UI Bridge")]
    public InteractionController interaction;

    [Header("Modules (Prefabs)")]
    public GameObject primalCraftModule;
    public GameObject campfireModule;
    public GameObject woodenCrateModule; // ✅ 추가

    [Header("VFX")]
    public VfxManager vfx; // ✅ 프리팹/인스턴스는 VfxManager가 가진다.

    readonly Dictionary<int, Multiblock> _instances = new Dictionary<int, Multiblock>();
    readonly Dictionary<Vector2Int, Multiblock> _byCell = new Dictionary<Vector2Int, Multiblock>();
    int _nextInstanceId = 1;

    readonly Dictionary<string, Func<Multiblock>> _factoryByDefId = new Dictionary<string, Func<Multiblock>>();

    // VFX 요청 수집용 버퍼(매 틱 재사용)
    readonly List<Multiblock.VfxRequest> _vfxBuf = new List<Multiblock.VfxRequest>(8);

    public IReadOnlyDictionary<int, Multiblock> Instances => _instances;

    void Awake()
    {
        RegisterFactory("Clay Kiln", () => new ClayKiln());
        RegisterFactory("Primal Workbench", () => new PrimalWorkbench());
        RegisterFactory("Campfire", () => new Campfire());
        RegisterFactory("Wooden Crate", () => new WoodenCrate()); // ✅ 추가
    }

    void Start()
    {
        // VfxManager가 플레이어 거리 컬링을 하므로 player를 넘겨준다.
        if (vfx != null && interaction != null && interaction.player != null)
            vfx.SetPlayer(interaction.player.transform);
    }

    // ✅ 멀티블럭 틱: 물리 틱(FixedUpdate) 기준으로 구동
    void FixedUpdate()
    {
        if (_instances.Count == 0) return;

        // 중간 Despawn 대비 스냅샷
        List<Multiblock> snap = new List<Multiblock>(_instances.Count);
        foreach (var kv in _instances)
            snap.Add(kv.Value);

        for (int i = 0; i < snap.Count; i++)
        {
            var mb = snap[i];
            if (mb == null) continue;

            mb.Tick();
            ApplyVfxRequests(mb);
        }
    }

    void ApplyVfxRequests(Multiblock mb)
    {
        if (vfx == null) return;

        _vfxBuf.Clear();
        mb.GetVfxRequests(_vfxBuf);

        if (_vfxBuf.Count == 0) return;

        // Origin 기준 오프셋 -> 월드좌표로 변환 후 전달
        for (int i = 0; i < _vfxBuf.Count; i++)
        {
            var r = _vfxBuf[i];

            Vector3 pos = new Vector3(
                mb.Origin.x + r.offset.x,
                mb.Origin.y + r.offset.y,
                0f
            );

            vfx.SetLoopVfx(mb.InstId, r.key, r.active, pos);
        }
    }

    public void RegisterFactory(string defId, Func<Multiblock> creator)
    {
        _factoryByDefId[defId] = creator;
    }

    public Multiblock GetAtCell(Vector2Int cell)
    {
        _byCell.TryGetValue(cell, out var inst);
        return inst;
    }

    /// <summary>
    /// 멀티블럭이 점유한 모든 셀의 meta를 변경한다. (id는 유지)
    /// - World write는 Manager가 담당.
    /// - Campfire처럼 "전체 파트가 함께 변하는" 케이스에 사용.
    /// </summary>
    public void ApplyMetaToAllOccupiedCells(Multiblock owner, ushort targetMeta)
    {
        if (owner == null) return;
        if (world == null) return;

        var cells = owner.OccupiedCells;
        if (cells == null) return;

        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];

            ushort id = world.GetSolidId(c.x, c.y);
            if (id == 0) continue;

            // id 유지 + meta만 변경
            world.OverwriteSolid(c.x, c.y, id, targetMeta);
        }
    }

    // ✅ 멀티블럭이 "모듈 이름" + "본인(this)"를 주면, 매니저가 실제 UI를 열고 바인딩까지 한다.
    public void OpenModule(string moduleId, Multiblock owner)
    {
        GameObject prefab = moduleId switch
        {
            "PrimalCraft"   => primalCraftModule,
            "Campfire"      => campfireModule,
            "Wooden Crate"  => woodenCrateModule, // ✅ 추가
            _ => null
        };

        if (prefab == null) return;
        if (interaction == null) return;

        var instGO = interaction.OpenModule(prefab);
        if (instGO == null) return;

        if (moduleId == "Campfire" && owner is Campfire campfire)
        {
            var ui = instGO.GetComponentInChildren<CampfireModule>(true);
            if (ui != null)
                ui.Bind(campfire);
        }
        else if (moduleId == "Wooden Crate" && owner is WoodenCrate crate)
        {
            var ui = instGO.GetComponentInChildren<WoodenCrateModule>(true);
            if (ui != null)
                ui.Bind(crate);
        }
    }

    public Multiblock Create(MultiblockLibrary.Def def, int originX, int originY)
    {
        int width = def.width;
        int height = def.height;

        var origin = new Vector2Int(originX, originY);

        var occupied = new List<Vector2Int>(width * height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                occupied.Add(new Vector2Int(originX + x, originY + y));

        if (!_factoryByDefId.TryGetValue(def.key, out var creator) || creator == null)
        {
            Debug.LogWarning($"{LOG_MB} No factory for defId='{def.key}'. Create skipped.");
            return null;
        }

        var inst = creator.Invoke();

        inst.Initialize(world, def.key, origin, width, height, occupied);
        inst.Manager = this;
        inst.InstId = _nextInstanceId++;

        inst.originalSolidIds.Clear();
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int wx = originX + x;
            int wy = originY + y;
            inst.originalSolidIds[new Vector2Int(wx, wy)] = world.GetSolidId(wx, wy);
        }

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            string resultCellName = def.result[x, y];
            if (string.IsNullOrEmpty(resultCellName))
                continue;

            world.cellLibrary.TryGetSolidIdByName(resultCellName, out ushort placeId);
            world.OverwriteSolid(originX + x, originY + y, placeId);
        }

        RegisterInstance(inst);
        return inst;
    }

    public void RegisterInstance(Multiblock inst)
    {
        _instances.Add(inst.InstId, inst);
        foreach (var cell in inst.OccupiedCells)
            _byCell[cell] = inst;
    }

    public void Despawn(Multiblock inst, Vector2Int brokenCell)
    {
        _instances.Remove(inst.InstId);

        foreach (var cell in inst.OccupiedCells)
        {
            if (_byCell.TryGetValue(cell, out var cur) && cur == inst)
                _byCell.Remove(cell);
        }

        // ✅ 해당 멀티블럭에 속한 모든 루프 VFX 정리 (VfxManager가 인스턴스 소유)
        if (vfx != null)
            vfx.DespawnAllForOwner(inst.InstId);

        foreach (var kv in inst.originalSolidIds)
        {
            var cell = kv.Key;
            if (cell == brokenCell) continue;
            world.OverwriteSolid(cell.x, cell.y, kv.Value);
        }

        Debug.Log($"{LOG_MB} Despawn multiblock instId={inst.InstId}, def={inst.DefId}, restoreExcept={brokenCell}");
    }
}
