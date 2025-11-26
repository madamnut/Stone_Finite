// MudFurnaceInstance.cs
using UnityEngine;

public class MudFurnaceInstance : MultiblockInstanceBase
{
    // 슬롯 구성 (UI와 1:1 대응)
    // 연료 입력
    public ItemData fuelInput;

    // 연료 잔여물 출력
    public ItemData fuelByproduct;

    // 가열할 재료 입력
    public ItemData materialInput;

    // 가열된 결과물 출력
    public ItemData materialOutput;

    // 연료/가열 진행도 (틱 단위)
    // 남은 연료 시간 / 최대 연료 시간
    public int fuelRemainingTicks;
    public int fuelTotalTicks;

    // 현재 가열 진행도 / 한 번 가열에 필요한 전체 시간
    public int cookProgressTicks;
    public int cookTotalTicks;

    public override void Tick(WorldManager world)
    {
        // 이후 단계에서 연료 소모, 가열 진행, 스프라이트 갱신 로직 추가 예정
        // Debug.Log($"[MudFurnace] Tick instanceId={instanceId}, origin=({originX},{originY})");
    }

    public override void OnInteract(Player player, Vector2Int hitCell)
    {
        // 이후 단계에서 머드 화로 UI 열기/상태 표시 로직 추가 예정
        // Debug.Log($"[MudFurnace] OnInteract at {hitCell}, instanceId={instanceId}");
    }

    public override void OnPartBroken(WorldManager world, Vector2Int brokenCell)
    {
        // 이후 단계에서:
        // 1) occupiedCells 전체를 기본 블럭으로 롤백
        // 2) 내부 인벤토리 드롭
        // 3) WorldManager의 multiblocks / byCell에서 자신 제거
        // Debug.Log($"[MudFurnace] OnPartBroken at {brokenCell}, instanceId={instanceId}");
    }
}
