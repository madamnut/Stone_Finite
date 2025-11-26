// MultiblockInstanceBase.cs
using System.Collections.Generic;
using UnityEngine;

public abstract class MultiblockInstanceBase
{
    // 어떤 멀티블럭 정의에서 왔는지 (예: "MudFurnace")
    public string defKey;

    // 패턴 (0,0)에 해당하는 월드 좌표
    public int originX;
    public int originY;

    // 패턴 크기 (def.width / def.height)
    public int width;
    public int height;

    // 이 인스턴스가 실제로 차지하는 모든 월드 좌표
    public List<Vector2Int> occupiedCells = new List<Vector2Int>();

    // 디버그/관리용 고유 ID (필요 없으면 0 유지 가능)
    public int instanceId;

    // 월드 틱마다 호출되는 훅
    public virtual void Tick(WorldManager world)
    {
        // 각 멀티블럭 타입에서 필요하면 override
    }

    // 플레이어가 이 멀티블럭의 어떤 셀을 상호작용했을 때 호출
    public virtual void OnInteract(Player player, Vector2Int hitCell)
    {
        // 각 멀티블럭 타입에서 필요하면 override
    }

    // 이 멀티블럭을 구성하는 셀 중 하나가 파괴되었을 때 호출
    public virtual void OnPartBroken(WorldManager world, Vector2Int brokenCell)
    {
        // 각 멀티블럭 타입에서 필요하면 override
    }
}
