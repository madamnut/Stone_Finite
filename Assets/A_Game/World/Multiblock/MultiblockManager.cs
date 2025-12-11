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

    public IReadOnlyDictionary<int, Multiblock> Instances => _instances;

    /// <summary>지정 셀을 포함하는 멀티블럭을 조회.</summary>
    public Multiblock GetAtCell(Vector2Int cell)
    {
        _byCell.TryGetValue(cell, out var inst);
        return inst;
    }

    /// <summary>
    /// MultiblockLibrary.Def에서 클레이 킬른 인스턴스를 생성하고 등록.
    /// - 패턴 매칭으로 originX, originY 결정된 뒤 호출.
    /// - result 패턴에 따라 월드 블럭을 교체하고
    ///   pattern 기준으로 OccupiedCells 를 채운다.
    /// </summary>
    public ClayKiln CreateClayKiln(MultiblockLibrary.Def def, int originX, int originY)
    {
        if (world == null)
        {
            Debug.LogError($"{LOG_MB} MultiblockManager.world not assigned.");
            return null;
        }

        var origin   = new Vector2Int(originX, originY);
        var occupied = new List<Vector2Int>();

        int width  = def.width;
        int height = def.height;

        Debug.Log($"{LOG_MB} CreateClayKiln def='{def.key}' origin=({originX},{originY}) size={width}x{height}");

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int wx = originX + x;
                int wy = originY + y;

                // OccupiedCells 는 pattern 기준으로 채운다 (구조를 이루는 모든 칸)
                string patternKey = def.pattern[x, y];
                if (!string.IsNullOrEmpty(patternKey))
                {
                    occupied.Add(new Vector2Int(wx, wy));
                }

                // result 패턴에 셀 이름이 있으면 그 셀로 교체
                string resultCellName = def.result[x, y];
                if (string.IsNullOrEmpty(resultCellName))
                    continue;

                ushort placeId = 0;
                for (ushort id = 1; id < ushort.MaxValue; id++)
                {
                    string nm = CellLibrary.GetName(id);
                    if (!string.IsNullOrEmpty(nm) && nm == resultCellName)
                    {
                        placeId = id;
                        break;
                    }
                }

                if (placeId == 0)
                {
                    Debug.LogWarning($"{LOG_MB} result cell '{resultCellName}' 에 해당하는 ID를 찾지 못함 (wx={wx}, wy={wy}). 스킵.");
                    continue;
                }

                // 일단 FG 레이어에 배치한다고 가정
                world.PlaceCell(wx, wy, placeId);
            }
        }

        var kiln = new ClayKiln();
        kiln.Initialize(world, def.key, origin, width, height, occupied);
        kiln.Manager = this;
        kiln.InstId  = _nextInstanceId++;

        RegisterInstance(kiln);
        return kiln;
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

        Debug.Log(
            $"{LOG_MB} Registered multiblock instId={inst.InstId}, " +
            $"def={inst.DefId}, cells={inst.OccupiedCells.Count}"
        );
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
