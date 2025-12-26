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

    [Header("VFX (Loop Prefabs)")]
    // Campfire 전용: Fire_01 루프 프리팹(Animator 붙은 스프라이트 오브젝트)
    public GameObject fire01Prefab;

    // 가까워지면 활성 / 멀어지면 비활 (일단 플레이어 기준 거리)
    public float vfxActiveRange = 40f;

    // Campfire Fire_01 로드/언로드 관리 (instId -> vfx instance)
    readonly Dictionary<int, GameObject> _campfireFire = new Dictionary<int, GameObject>();

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
        List<Multiblock> snap = new List<Multiblock>(_instances.Count);
        foreach (var kv in _instances)
            snap.Add(kv.Value);

        for (int i = 0; i < snap.Count; i++)
        {
            var mb = snap[i];
            if (mb == null) continue;

            mb.Tick();
            UpdateVfx(mb);
        }
    }

    void UpdateVfx(Multiblock mb)
    {
        // 현재는 Campfire의 Fire_01만 처리
        if (mb is not Campfire cf)
            return;

        int id = cf.InstId;

        bool shouldBurn = cf.Isburning;
        bool inRange = IsInVfxRange(cf);

        // 불 꺼짐 or 멀어짐 => 비활/정리
        if (!shouldBurn || !inRange)
        {
            if (_campfireFire.TryGetValue(id, out var go) && go != null)
            {
                go.SetActive(false);
            }
            return;
        }

        // 켜져야 함 + 범위 안 => 생성 또는 활성
        if (!_campfireFire.TryGetValue(id, out var inst) || inst == null)
        {
            if (fire01Prefab == null) return;

            Vector3 pos = GetCampfireFireWorldPos(cf);
            inst = Instantiate(fire01Prefab, pos, Quaternion.identity);
            inst.name = $"Fire_01(Campfire#{id})";

            _campfireFire[id] = inst;
        }

        inst.transform.position = GetCampfireFireWorldPos(cf);
        if (!inst.activeSelf) inst.SetActive(true);
    }

    bool IsInVfxRange(Multiblock mb)
    {
        if (interaction == null || interaction.player == null)
            return true; // 플레이어 참조 없으면 항상 켠다

        Vector3 p = interaction.player.transform.position;

        // 멀티블럭은 Origin이 "좌하단 셀"이므로 중심을 대충 잡아준다
        Vector3 center = new Vector3(
            mb.Origin.x + mb.Width * 0.5f,
            mb.Origin.y + mb.Height * 0.5f,
            0f
        );

        float r = Mathf.Max(0.01f, vfxActiveRange);
        return (p - center).sqrMagnitude <= r * r;
    }

    Vector3 GetCampfireFireWorldPos(Campfire cf)
    {
        // 요구사항: 오리진 기준 (1, 0.5)
        return new Vector3(cf.Origin.x + 1f, cf.Origin.y + 0.5f, 0f);
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

        // VFX 정리 (Campfire)
        if (inst is Campfire)
        {
            int id = inst.InstId;
            if (_campfireFire.TryGetValue(id, out var go) && go != null)
                Destroy(go);
            _campfireFire.Remove(id);
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