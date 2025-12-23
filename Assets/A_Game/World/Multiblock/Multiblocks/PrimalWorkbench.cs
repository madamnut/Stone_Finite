// PrimalWorkbench.cs
using UnityEngine;

/// <summary>
/// "Primal Workbench" 멀티블럭.
/// - 현재는 베이스 동작만 갖는 최소 구현.
/// - 상호작용/가동 로직은 필요해질 때 OnInteract/Tick에 추가.
/// </summary>
public class PrimalWorkbench : Multiblock
{
    // 필요하면 나중에 상태를 여기에 추가 (예: 진행중 작업, 연료, 인벤토리 등)

    public override void Tick()
    {
        // 현재 동작 없음
    }

    public override void OnInteract(Player player, Vector2Int hitCell)
    {
        // 현재는 동작 없음
        // (UI를 열어야 한다면 여기서 처리하도록 확장)
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
            PayloadJson = null
        };
    }

    public override void FromSaveData(SaveData data)
    {
        // 공통 메타 복원
        DefId   = data.DefId;
        InstId  = data.InstId;
        Origin  = data.Origin;
        Width   = data.Width;
        Height  = data.Height;

        // 현재 라이브러리 규칙상 pattern은 빈칸 불가 -> 직사각형 점유로 복원 가능
        occupiedCells.Clear();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
                occupiedCells.Add(new Vector2Int(Origin.x + x, Origin.y + y));
        }

        // PayloadJson은 현재 사용 안 함
    }
}
