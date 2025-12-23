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

    // defId(JSON 최상단 키) -> 인스턴스 생성기
    readonly Dictionary<string, Func<Multiblock>> _factoryByDefId = new Dictionary<string, Func<Multiblock>>();

    public IReadOnlyDictionary<int, Multiblock> Instances => _instances;

    void Awake()
    {
        // 기본 팩토리 등록 (파생 클래스 준비되면 계속 추가)
        RegisterFactory("Clay Kiln", () => new ClayKiln());

        // RegisterFactory("Campfire", () => new Campfire());
        // RegisterFactory("Primal Workbench", () => new PrimalWorkbench());
        // RegisterFactory("Wooden Chest", () => new WoodenChest());
        // RegisterFactory("Hearth", () => new Hearth());
        // RegisterFactory("Brick Furnace", () => new BrickFurnace());
    }

    /// <summary>defId(=JSON 키) -> 생성기 등록/갱신.</summary>
    public void RegisterFactory(string defId, Func<Multiblock> creator)
    {
        if (string.IsNullOrEmpty(defId) || creator == null)
            return;

        _factoryByDefId[defId] = creator;
    }

    /// <summary>지정 셀을 포함하는 멀티블럭을 조회.</summary>
    public Multiblock GetAtCell(Vector2Int cell)
    {
        _byCell.TryGetValue(cell, out var inst);
        return inst;
    }

    /// <summary>
    /// def(패턴/결과) 기반으로 멀티블럭 인스턴스 생성 + 월드 반영 + 등록.
    /// - originX, originY는 패턴 매칭으로 결정된 "맨 왼쪽, 맨 아래" 좌표
    /// </summary>
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

        // OccupiedCells: 현재 MultiblockLibrary 규칙상 pattern은 빈칸 불가 -> 전 칸 점유
        var occupied = new List<Vector2Int>(width * height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                occupied.Add(new Vector2Int(originX + x, originY + y));
        }

        // result 패턴에 셀 이름이 있으면 그 셀로 교체
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

                    if (!world.cellLibrary.TryGetSolidIdByName(resultCellName, out ushort placeId) || placeId == 0)
                    {
                        Debug.LogWarning($"{LOG_MB} result cell '{resultCellName}' 에 해당하는 ID를 찾지 못함 (wx={originX + x}, wy={originY + y}). 스킵.");
                        continue;
                    }

                    // 일단 FG 레이어에 배치한다고 가정
                    world.PlaceCell(originX + x, originY + y, placeId);
                }
            }
        }

        // 인스턴스 생성 + 공통 초기화
        var inst = creator.Invoke();
        if (inst == null)
        {
            Debug.LogError($"{LOG_MB} Factory returned null for defId='{def.key}'.");
            return null;
        }

        inst.Initialize(world, def.key, origin, width, height, occupied);
        inst.Manager = this;
        inst.InstId  = _nextInstanceId++;

        RegisterInstance(inst);
        return inst;
    }

    /// <summary>매니저에 신규 인스턴스를 등록.</summary>
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
    /// 멀티블럭 인스턴스를 제거.
    /// (월드에서 실제 셀을 부수는 건 WorldManager 쪽에서 담당한다고 가정)
    /// </summary>
    public void Despawn(Multiblock inst)
    {
        if (inst == null) return;

        if (_instances.Remove(inst.InstId))
        {
            foreach (var cell in inst.OccupiedCells)
            {
                if (_byCell.TryGetValue(cell, out var cur) && cur == inst)
                    _byCell.Remove(cell);
            }

            Debug.Log($"{LOG_MB} Despawn multiblock instId={inst.InstId}, def={inst.DefId}");
        }
    }
}
