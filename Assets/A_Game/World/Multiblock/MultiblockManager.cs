// MultiblockManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 멀티블럭 인스턴스 수명/조회 전담 매니저.
/// 실제 동작(클레이 가마 등)은 Multiblock 파생 클래스에서 처리.
/// </summary>
public class MultiblockManager : MonoBehaviour
{
    const string LOG_MB = "[MBLOCK]";

    [Header("Deps")]
    public WorldManager world;

    readonly Dictionary<int, Multiblock>        _instances = new Dictionary<int, Multiblock>();
    readonly Dictionary<Vector2Int, Multiblock> _byCell    = new Dictionary<Vector2Int, Multiblock>();
    int _nextInstanceId = 1;

    readonly Dictionary<string, Func<Multiblock>> _factoryByDefId = new Dictionary<string, Func<Multiblock>>();

    public IReadOnlyDictionary<int, Multiblock> Instances => _instances;

    void Awake()
    {
        RegisterFactory("Clay Kiln", () => new ClayKiln());
        RegisterFactory("Primal Workbench", () => new PrimalWorkbench());
    }

    public void RegisterFactory(string defId, Func<Multiblock> creator)
    {
        if (string.IsNullOrEmpty(defId) || creator == null)
            return;

        _factoryByDefId[defId] = creator;
    }

    public Multiblock GetAtCell(Vector2Int cell)
    {
        _byCell.TryGetValue(cell, out var inst);
        return inst;
    }

    public Multiblock Create(MultiblockLibrary.Def def, int originX, int originY)
    {
        if (def == null)
            return null;

        if (world == null)
        {
            Debug.LogError($"{LOG_MB} MultiblockManager.world not assigned.");
            return null;
        }

        if (!_factoryByDefId.TryGetValue(def.key, out var creator) || creator == null)
        {
            Debug.LogWarning($"{LOG_MB} No factory for defId='{def.key}'. Create skipped.");
            return null;
        }

        int width  = def.width;
        int height = def.height;

        var origin = new Vector2Int(originX, originY);

        Debug.Log($"{LOG_MB} Create defId='{def.key}' origin=({originX},{originY}) size={width}x{height}");

        var occupied = new List<Vector2Int>(width * height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                occupied.Add(new Vector2Int(originX + x, originY + y));
        }

        // 인스턴스 생성 + 공통 초기화 (원본 스냅샷을 여기에 붙여야 함)
        var inst = creator.Invoke();
        if (inst == null)
        {
            Debug.LogError($"{LOG_MB} Factory returned null for defId='{def.key}'.");
            return null;
        }

        inst.Initialize(world, def.key, origin, width, height, occupied);
        inst.Manager = this;
        inst.InstId  = _nextInstanceId++;

        // ✅ 원본 셀(현재 solidId) 스냅샷 저장 (pattern 매칭이 끝난 시점이므로 그대로 저장하면 됨)
        inst.originalSolidIds.Clear();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int wx = originX + x;
                int wy = originY + y;

                ushort oldId = world.GetSolidId(wx, wy);
                inst.originalSolidIds[new Vector2Int(wx, wy)] = oldId;
            }
        }

        // result 패턴에 셀 이름이 있으면 그 셀로 교체(덮어쓰기)
        if (world.cellLibrary == null)
        {
            Debug.LogError($"{LOG_MB} WorldManager.cellLibrary not assigned.");
        }
        else
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    string resultCellName = def.result[x, y];
                    if (string.IsNullOrEmpty(resultCellName))
                        continue;

                    if (!world.cellLibrary.TryGetSolidIdByName(resultCellName, out ushort placeId))
                    {
                        Debug.LogWarning($"{LOG_MB} result cell '{resultCellName}' ID not found (wx={originX + x}, wy={originY + y}). skip");
                        continue;
                    }

                    world.OverwriteSolid(originX + x, originY + y, placeId);
                }
            }
        }

        RegisterInstance(inst);
        return inst;
    }

    public void RegisterInstance(Multiblock inst)
    {
        if (inst == null) return;

        if (_instances.ContainsKey(inst.InstId))
        {
            Debug.LogWarning($"{LOG_MB} Duplicate multiblock instId={inst.InstId}");
            return;
        }

        _instances.Add(inst.InstId, inst);

        foreach (var cell in inst.OccupiedCells)
        {
            if (_byCell.ContainsKey(cell))
            {
                Debug.LogWarning($"{LOG_MB} cell {cell} already occupied by another multiblock.");
                continue;
            }
            _byCell.Add(cell, inst);
        }

        Debug.Log($"{LOG_MB} Registered multiblock instId={inst.InstId}, def={inst.DefId}, cells={inst.OccupiedCells.Count}");
    }

    /// <summary>
    /// 멀티블럭 인스턴스를 제거 + (요구사항) 남은 칸 원복.
    /// brokenCell은 이미 파괴된 칸이므로 원복 대상에서 제외.
    /// </summary>
    public void Despawn(Multiblock inst, Vector2Int brokenCell)
    {
        if (inst == null) return;

        if (_instances.Remove(inst.InstId))
        {
            foreach (var cell in inst.OccupiedCells)
            {
                if (_byCell.TryGetValue(cell, out var cur) && cur == inst)
                    _byCell.Remove(cell);
            }

            // ✅ 남은 부분 원복
            foreach (var kv in inst.originalSolidIds)
            {
                var cell = kv.Key;
                if (cell == brokenCell)
                    continue;

                ushort restoreId = kv.Value;
                world.OverwriteSolid(cell.x, cell.y, restoreId);
            }

            Debug.Log($"{LOG_MB} Despawn multiblock instId={inst.InstId}, def={inst.DefId}, restoreExcept={brokenCell}");
        }
    }
}
