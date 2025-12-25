// MultiblockManager.cs
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

    // ✅ UI Bridge (Inspector에서 할당)
    public InteractionController interaction;

    [Header("Modules (Prefabs)")]
    public GameObject primalCraftModule; // PrimalWorkbench가 열 모듈
    public GameObject campfireModule;    // Campfire가 열 모듈

    readonly Dictionary<int, Multiblock> _instances = new Dictionary<int, Multiblock>();
    readonly Dictionary<Vector2Int, Multiblock> _byCell = new Dictionary<Vector2Int, Multiblock>();
    int _nextInstanceId = 1;

    readonly Dictionary<string, Func<Multiblock>> _factoryByDefId = new Dictionary<string, Func<Multiblock>>();

    public IReadOnlyDictionary<int, Multiblock> Instances => _instances;

    void Awake()
    {
        RegisterFactory("Clay Kiln", () => new ClayKiln());
        RegisterFactory("Primal Workbench", () => new PrimalWorkbench());
        RegisterFactory("Campfire", () => new Campfire());
    }

    // ✅ 멀티블럭 틱: 물리 틱(FixedUpdate) 기준으로 구동
    void FixedUpdate()
    {
        if (_instances.Count == 0) return;

        // Dictionary.Values foreach는 중간에 Despawn되면 예외 가능성 있음
        // → 안전하게 스냅샷 후 Tick
        // (임의 유틸 추가 금지 조건이 있어 지역 리스트로만 처리)
        List<Multiblock> snap = new List<Multiblock>(_instances.Count);
        foreach (var kv in _instances)
            snap.Add(kv.Value);

        for (int i = 0; i < snap.Count; i++)
        {
            var mb = snap[i];
            if (mb != null)
                mb.Tick();
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

    // ✅ 멀티블럭이 "모듈 이름" + "본인(this)"를 주면, 매니저가 실제 UI를 열고 바인딩까지 한다.
    public void OpenModule(string moduleId, Multiblock owner)
    {
        GameObject prefab = moduleId switch
        {
            "PrimalCraft" => primalCraftModule,
            "Campfire"    => campfireModule,
            _ => null
        };

        if (prefab == null) return;
        if (interaction == null) return;

        var instGO = interaction.OpenModule(prefab);
        if (instGO == null) return;

        // 모듈별 바인딩
        if (moduleId == "Campfire")
        {
            var campfire = owner as Campfire;
            if (campfire == null) return;

            var ui = instGO.GetComponentInChildren<CampfireModule>(true);
            if (ui != null)
                ui.Bind(campfire);
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
        {
            for (int x = 0; x < width; x++)
            {
                int wx = originX + x;
                int wy = originY + y;
                inst.originalSolidIds[new Vector2Int(wx, wy)] = world.GetSolidId(wx, wy);
            }
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                string resultCellName = def.result[x, y];
                if (string.IsNullOrEmpty(resultCellName))
                    continue;

                world.cellLibrary.TryGetSolidIdByName(resultCellName, out ushort placeId);
                world.OverwriteSolid(originX + x, originY + y, placeId);
            }
        }

        RegisterInstance(inst);
        return inst;
    }

    public void RegisterInstance(Multiblock inst)
    {
        _instances.Add(inst.InstId, inst);
        foreach (var cell in inst.OccupiedCells)
            _byCell.Add(cell, inst);
    }

    public void Despawn(Multiblock inst, Vector2Int brokenCell)
    {
        _instances.Remove(inst.InstId);

        foreach (var cell in inst.OccupiedCells)
        {
            if (_byCell.TryGetValue(cell, out var cur) && cur == inst)
                _byCell.Remove(cell);
        }

        foreach (var kv in inst.originalSolidIds)
        {
            var cell = kv.Key;
            if (cell == brokenCell) continue;
            world.OverwriteSolid(cell.x, cell.y, kv.Value);
        }

        Debug.Log($"{LOG_MB} Despawn multiblock instId={inst.InstId}, def={inst.DefId}, restoreExcept={brokenCell}");
    }
}
