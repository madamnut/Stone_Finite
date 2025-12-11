using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 클레이 가마 멀티블럭.
/// 현재는 블럭 교체 + 인스턴스 생성만 하고,
/// 실제 연소/가열/상호작용 로직은 나중에 채운다.
/// </summary>
public class ClayKiln : Multiblock
{
    public override void Initialize(
        WorldManager world,
        string defId,
        Vector2Int origin,
        int width,
        int height,
        IEnumerable<Vector2Int> occupied
    )
    {
        base.Initialize(world, defId, origin, width, height, occupied);
        Debug.Log($"[ClayKiln] Initialize: defId={defId}, origin={origin}, size={width}x{height}, cells={OccupiedCells.Count}");
    }

    public override void Tick()
    {
        // 나중에 연소/가열 로직 넣을 자리.
    }

    public override void OnInteract(Player player, Vector2Int hitCell)
    {
        // 나중에:
        // - 불 붙이기
        // - UI 열기
        // - 내부 슬롯 처리
        // 등을 구현.
        Debug.Log($"[ClayKiln] OnInteract at {hitCell}");
    }

    public override void OnCellBroken(Vector2Int brokenCell)
    {
        // 기본 구현은 Despawn(this)이므로, 필요하면 여기서 추가 처리 후 base 호출.
        Debug.Log($"[ClayKiln] OnCellBroken at {brokenCell}");
        base.OnCellBroken(brokenCell);
    }

    public override SaveData ToSaveData()
    {
        return new SaveData
        {
            DefId       = DefId,
            InstId      = InstId,
            Origin      = Origin,
            Width       = Width,
            Height      = Height,
            PayloadJson = string.Empty // 나중에 전용 상태 생기면 채우면 됨
        };
    }

    public override void FromSaveData(SaveData data)
    {
        // World / occupiedCells 는 외부에서 다시 세팅해 줄 것이므로
        // 여기서는 메타만 맞춰둔다.
        DefId  = data.DefId;
        InstId = data.InstId;
        Origin = data.Origin;
        Width  = data.Width;
        Height = data.Height;
    }
}
