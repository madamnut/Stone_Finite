// MultiblockManager.cs (전체 교체본)
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
    public GameObject woodenCrateModule;
    public GameObject clayKilnModule;
    public GameObject brickFurnaceModule;
    public GameObject toolbenchModule;    // ✅ 추가
    public GameObject cokeOvenModule;     // ✅ 추가

    [Header("VFX")]
    public VfxManager vfx;

    readonly Dictionary<int, Multiblock> _instances = new Dictionary<int, Multiblock>();
    readonly Dictionary<Vector2Int, Multiblock> _byCell = new Dictionary<Vector2Int, Multiblock>();
    int _nextInstanceId = 1;

    readonly Dictionary<string, Func<Multiblock>> _factoryByDefId = new Dictionary<string, Func<Multiblock>>();

    readonly List<Multiblock.VfxRequest> _vfxBuf = new List<Multiblock.VfxRequest>(8);

    public IReadOnlyDictionary<int, Multiblock> Instances => _instances;

    void Awake()
    {
        RegisterFactory("Clay Kiln", () => new ClayKiln());
        RegisterFactory("Primal Workbench", () => new PrimalWorkbench());
        RegisterFactory("Campfire", () => new Campfire());
        RegisterFactory("Wooden Crate", () => new WoodenCrate());
        RegisterFactory("Brick Furnace", () => new BrickFurnace());
        RegisterFactory("Toolbench", () => new Toolbench()); // ✅ 추가

        RegisterFactory("Coke Oven", () => new CokeOven());   // ✅ 추가 (Def.key와 동일해야 함)
    }

    void Start()
    {
        if (vfx != null && interaction != null && interaction.player != null)
            vfx.SetPlayer(interaction.player.transform);
    }

    void FixedUpdate()
    {
        if (_instances.Count == 0) return;

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

            world.OverwriteSolid(c.x, c.y, id, targetMeta);
        }
    }

    public void OpenModule(string moduleId, Multiblock owner)
    {
        GameObject prefab = moduleId switch
        {
            "PrimalCraft"    => primalCraftModule,
            "Campfire"       => campfireModule,
            "Wooden Crate"   => woodenCrateModule,
            "Clay Kiln"      => clayKilnModule,
            "Brick Furnace"  => brickFurnaceModule,
            "Toolbench"      => toolbenchModule,
            "Coke Oven"      => cokeOvenModule,   // ✅ 추가
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
        else if (moduleId == "Clay Kiln" && owner is ClayKiln kiln)
        {
            var ui = instGO.GetComponentInChildren<ClayKilnModule>(true);
            if (ui != null)
                ui.Bind(kiln);
        }
        else if (moduleId == "Brick Furnace" && owner is BrickFurnace furnace)
        {
            var ui = instGO.GetComponentInChildren<BrickFurnaceModule>(true);
            if (ui != null)
                ui.Bind(furnace);
        }
        else if (moduleId == "Toolbench" && owner is Toolbench toolbench)
        {
            var ui = instGO.GetComponentInChildren<ToolbenchModule>(true);
            if (ui != null)
            {
                // ✅ ToolbenchModule은 CraftModule이 아니므로 여기서 직접 주입
                ui.recipeLibrary = interaction.recipeLibrary;
                ui.player = interaction.player;

                ui.Bind(toolbench);
            }
        }
        else if (moduleId == "Coke Oven" && owner is CokeOven cokeOven)
        {
            var ui = instGO.GetComponentInChildren<CokeOvenModule>(true);
            if (ui != null)
            {
                ui.Bind(cokeOven);
            }
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

    public void LoadFromSaveDatas(List<Multiblock.SaveData> list)
    {
        if (vfx != null && _instances.Count > 0)
        {
            foreach (var kv in _instances)
                vfx.DespawnAllForOwner(kv.Key);
        }

        _instances.Clear();
        _byCell.Clear();

        int maxId = 0;

        if (list == null || list.Count == 0)
        {
            _nextInstanceId = 1;
            return;
        }

        for (int i = 0; i < list.Count; i++)
        {
            var sd = list[i];

            if (string.IsNullOrEmpty(sd.DefId))
                continue;

            if (!_factoryByDefId.TryGetValue(sd.DefId, out var creator) || creator == null)
            {
                Debug.LogWarning($"{LOG_MB} No factory for defId='{sd.DefId}'. Load skipped.");
                continue;
            }

            var occupied = new List<Vector2Int>(sd.Width * sd.Height);
            for (int y = 0; y < sd.Height; y++)
                for (int x = 0; x < sd.Width; x++)
                    occupied.Add(new Vector2Int(sd.Origin.x + x, sd.Origin.y + y));

            var inst = creator.Invoke();

            inst.Initialize(world, sd.DefId, sd.Origin, sd.Width, sd.Height, occupied);
            inst.Manager = this;
            inst.InstId = sd.InstId;

            inst.FromSaveData(sd);

            RegisterInstance(inst);

            if (inst.InstId > maxId)
                maxId = inst.InstId;
        }

        _nextInstanceId = (maxId <= 0) ? 1 : (maxId + 1);
    }
}
